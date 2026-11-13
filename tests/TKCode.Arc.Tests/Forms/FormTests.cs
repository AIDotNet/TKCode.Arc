using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Forms;

public class FormTests
{
    private static (Form Form, StackPanel Panel) NewForm(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 20 };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }
        return (new Form { Content = panel }, panel);
    }

    /// <summary>A part of <paramref name="item"/>'s own template (the editor inside has parts with the same names).</summary>
    internal static T ItemPart<T>(FormItem item, string name) where T : Control
        => item.GetVisualDescendants().OfType<T>().First(c => c.Name == name && ReferenceEquals(c.TemplatedParent, item) || c.Name == name && IsCodeBuiltPart(c, item));

    // Parts FormItem builds in code (status row, announcer) have no templated parent: they sit directly in its template panels.
    private static bool IsCodeBuiltPart(Control control, FormItem item)
        => control.TemplatedParent is null && control.GetVisualParent() is Control { TemplatedParent: var owner } && ReferenceEquals(owner, item);

    private static FormItem Item(string label, Control content, bool required = false)
        => new() { Label = label, Content = content, IsRequired = required };

    private static Input BoundInput(FormTestModel model, string path = nameof(FormTestModel.Text))
        => new Input().BindTo(TextBox.TextProperty, model, path);

    // --- Joining and leaving ---

    [AvaloniaFact]
    public void Static_Items_Join_The_Form_And_The_Error_Count_Follows()
    {
        var model = new FormTestModel();
        var name = Item("Name", BoundInput(model));
        var email = Item("Email", BoundInput(model, nameof(FormTestModel.Other)));
        var (form, _) = NewForm(name, email);
        TestHelpers.Show(form);

        form.Items.ShouldBe(new[] { name, email }, ignoreOrder: true);
        form.ErrorCount.ShouldBe(0);
        form.HasErrors.ShouldBeFalse();

        model.SetErrors(nameof(FormTestModel.Text), "Enter a name.");
        form.ErrorCount.ShouldBe(1);
        form.HasErrors.ShouldBeTrue();
        name.HasErrors.ShouldBeTrue();
        name.Errors.ShouldBe(new object[] { "Enter a name." });

        model.SetErrors(nameof(FormTestModel.Other), "Enter an email.", "Another error.");
        form.ErrorCount.ShouldBe(2);

        model.ClearAllErrors();
        form.ErrorCount.ShouldBe(0);
        form.HasErrors.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Items_Of_An_ItemsControl_Join_And_Leave_With_Their_Rows()
    {
        var rows = new ObservableCollection<FormTestModel>();
        var list = new ItemsControl
        {
            ItemsSource = rows,
            ItemTemplate = new FuncDataTemplate<FormTestModel>((row, _) => Item("Backup email", BoundInput(row)))
        };
        var (form, _) = NewForm(list);
        TestHelpers.Show(form);

        form.Items.Count.ShouldBe(0);
        var first = new FormTestModel();
        first.SetErrors(nameof(FormTestModel.Text), "Enter an email.");
        rows.Add(first);
        rows.Add(new FormTestModel());
        Dispatcher.UIThread.RunJobs();
        form.Items.Count.ShouldBe(2);
        form.ErrorCount.ShouldBe(1);

        rows.RemoveAt(0);
        Dispatcher.UIThread.RunJobs();
        form.Items.Count.ShouldBe(1);
        form.ErrorCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void An_Item_Moved_To_Another_Form_Leaves_The_First()
    {
        var model = new FormTestModel();
        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        var item = Item("Name", BoundInput(model));
        var (first, firstPanel) = NewForm(item);
        var (second, secondPanel) = NewForm();
        TestHelpers.Show(new StackPanel { Children = { first, second } });
        first.ErrorCount.ShouldBe(1);

        firstPanel.Children.Remove(item);
        secondPanel.Children.Add(item);
        Dispatcher.UIThread.RunJobs();

        first.Items.ShouldBeEmpty();
        first.ErrorCount.ShouldBe(0);
        second.Items.ShouldBe(new[] { item });
        second.ErrorCount.ShouldBe(1);
        item.OwnerForm.ShouldBeSameAs(second);
    }

    [AvaloniaFact]
    public void Items_Belong_To_The_Nearest_Form()
    {
        var outerItem = Item("Outer", new Input());
        var innerItem = Item("Inner", new Input());
        var (inner, _) = NewForm(innerItem);
        var (outer, _) = NewForm(outerItem, inner);
        TestHelpers.Show(outer);

        outer.Items.ShouldBe(new[] { outerItem });
        inner.Items.ShouldBe(new[] { innerItem });
    }

    // --- Collecting ---

    [AvaloniaFact]
    public void Errors_Of_Several_Editors_Merge_In_Document_Order_And_The_First_Shows()
    {
        var model = new FormTestModel();
        var item = new FormItem
        {
            Label = "Phone",
            Content = new StackPanel
            {
                Children = { BoundInput(model, nameof(FormTestModel.Text)), BoundInput(model, nameof(FormTestModel.Other)) }
            }
        };
        var (form, _) = NewForm(item);
        Form.SetErrorTrigger(form, FormErrorTrigger.Change);
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Other), "Enter the number.");
        model.SetErrors(nameof(FormTestModel.Text), "Pick a country.", "Second rule.");

        item.Errors.ShouldBe(new object[] { "Pick a country.", "Second rule.", "Enter the number." });
        item.EffectiveErrorMessage.ShouldBe("Pick a country.");
        form.ErrorCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Conversion_Failure_Is_Collected_And_Blocks_The_Submit()
    {
        var model = new FormTestModel { Number = 3 };
        var input = BoundInput(model, nameof(FormTestModel.Number));
        var item = Item("Age", input);
        var submit = new CountingCommand();
        var (form, _) = NewForm(item);
        form.SubmitCommand = submit;
        TestHelpers.Show(form);

        input.Text = "abc";
        Dispatcher.UIThread.RunJobs();

        model.Number.ShouldBe(3);
        item.HasErrors.ShouldBeTrue();
        item.Errors.Single().ShouldBeAssignableTo<Exception>();
        form.SubmitAsync().Result.ShouldBeFalse();
        submit.Count.ShouldBe(0);
        item.EffectiveErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Explicit_Errors_Win_Over_Binding_Errors_And_Show_At_Once()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var item = Item("Email", input);
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "Binding error.");
        item.EffectiveErrorMessage.ShouldBeNull(); // Blur: not shown yet

        input.ErrorMessage = "Field error.";
        item.EffectiveErrorMessage.ShouldBe("Field error.");

        item.ErrorMessage = "That email is already registered.";
        item.EffectiveErrorMessage.ShouldBe("That email is already registered.");
        item.IsInvalid.ShouldBeTrue();

        item.ErrorMessage = null;
        input.ErrorMessage = null;
        item.EffectiveErrorMessage.ShouldBeNull();
        item.HasErrors.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Error_Converter_Of_The_Editor_Applies()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        DataValidationErrors.SetErrorConverter(input, error => $"Converted: {error}");
        var item = Item("Code", input);
        var (form, _) = NewForm(item);
        Form.SetErrorTrigger(form, FormErrorTrigger.Change);
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "E42");

        item.EffectiveErrorMessage.ShouldBe("Converted: E42");
    }

    // --- When errors show ---

    [AvaloniaFact]
    public void Change_Shows_Errors_As_They_Arrive()
    {
        var model = new FormTestModel();
        var item = Item("Name", BoundInput(model));
        var (form, _) = NewForm(item);
        form.ErrorTrigger = FormErrorTrigger.Change;
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        item.EffectiveErrorMessage.ShouldBe("Required.");
        item.IsInvalid.ShouldBeTrue();
        ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBe("Required.");

        model.SetErrors(nameof(FormTestModel.Text));
        item.EffectiveErrorMessage.ShouldBeNull();
        item.IsInvalid.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Blur_Shows_Errors_Once_Focus_Leaves_The_Item()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var other = new Input();
        var item = Item("Name", input);
        var (form, _) = NewForm(item, other);
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        input.Focus();
        item.EffectiveErrorMessage.ShouldBeNull();
        other.Focus();

        item.EffectiveErrorMessage.ShouldBe("Required.");
        // Fixing the value removes the error at once.
        model.SetErrors(nameof(FormTestModel.Text));
        item.EffectiveErrorMessage.ShouldBeNull();
        model.SetErrors(nameof(FormTestModel.Text), "Too short.");
        item.EffectiveErrorMessage.ShouldBe("Too short.");
    }

    [AvaloniaFact]
    public void Moving_Focus_Into_A_Popup_Of_The_Item_Is_Not_Leaving_It()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var inPopup = new Input();
        var popup = new Avalonia.Controls.Primitives.Popup { Child = inPopup, PlacementTarget = input };
        var item = Item("Name", new StackPanel { Children = { input, popup } });
        var (form, _) = NewForm(item, new Input());
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        input.Focus();
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        inPopup.Focus();

        item.IsRevealed.ShouldBeFalse();
        item.EffectiveErrorMessage.ShouldBeNull();
        popup.IsOpen = false;
    }

    [AvaloniaFact]
    public void Submit_Trigger_Shows_Errors_Only_On_Validate_And_ClearValidation_Hides_Them()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var other = new Input();
        var item = Item("Name", input);
        var (form, _) = NewForm(item, other);
        form.ErrorTrigger = FormErrorTrigger.Submit;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        input.Focus();
        other.Focus();
        item.EffectiveErrorMessage.ShouldBeNull();

        form.Validate().ShouldBeFalse();
        item.EffectiveErrorMessage.ShouldBe("Required.");
        form.IsSubmitAttempted.ShouldBeTrue();
        form.Classes.ShouldContain(Form.SubmittedPseudoClass);

        form.ClearValidation();
        item.EffectiveErrorMessage.ShouldBeNull();
        form.IsSubmitAttempted.ShouldBeFalse();
        form.HasErrors.ShouldBeTrue(); // the view model's errors stay
    }

    [AvaloniaFact]
    public void Change_After_ClearValidation_Opens_On_The_Next_Change()
    {
        var model = new FormTestModel();
        var item = Item("Name", BoundInput(model));
        var (form, _) = NewForm(item);
        form.ErrorTrigger = FormErrorTrigger.Change;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        item.EffectiveErrorMessage.ShouldBe("Required.");

        form.ClearValidation();
        item.EffectiveErrorMessage.ShouldBeNull();

        model.SetErrors(nameof(FormTestModel.Text), "Too short.");
        item.EffectiveErrorMessage.ShouldBe("Too short.");
    }

    [AvaloniaFact]
    public void The_Error_Trigger_Set_On_A_Section_Applies_To_Its_Items()
    {
        var model = new FormTestModel();
        var inSection = Item("In section", BoundInput(model));
        var outside = Item("Outside", BoundInput(model, nameof(FormTestModel.Other)));
        var section = new StackPanel { Children = { inSection } };
        Form.SetErrorTrigger(section, FormErrorTrigger.Change);
        var (form, _) = NewForm(section, outside);
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "A.");
        model.SetErrors(nameof(FormTestModel.Other), "B.");

        inSection.EffectiveErrorMessage.ShouldBe("A.");
        outside.EffectiveErrorMessage.ShouldBeNull();
    }

    // --- Submitting ---

    private static (Form Form, ScrollViewer Scroll, FormItem[] Items) TallForm(FormTestModel model)
    {
        var items = new[]
        {
            Item("First", BoundInput(model, nameof(FormTestModel.Text))),
            Item("Second", new Input()),
            Item("Third", new Input()),
            Item("Fourth", new Input()),
            Item("Fifth", new Input()),
            Item("Last", BoundInput(model, nameof(FormTestModel.Other)))
        };
        var (form, _) = NewForm(items);
        var scroll = new ScrollViewer { Content = form };
        return (form, scroll, items);
    }

    [AvaloniaFact]
    public async Task A_Failed_Submit_Runs_ValidateCommand_And_Focuses_The_First_Item_In_Document_Order()
    {
        var model = new FormTestModel();
        var validate = new CountingCommand(_ => model.SetErrors(nameof(FormTestModel.Other), "Required."));
        var (form, scroll, items) = TallForm(model);
        form.ValidateCommand = validate;
        // The last item joined first: document order, not join order, decides.
        FormSubmitFailedEventArgs? failed = null;
        form.SubmitFailed += (_, e) => failed = e;
        var window = TestHelpers.Show(scroll);
        window.Height = 200;
        Dispatcher.UIThread.RunJobs();
        scroll.Offset.Y.ShouldBe(0);
        model.SetErrors(nameof(FormTestModel.Text), "Enter a name.");
        scroll.Offset = new Vector(0, 10000);
        Dispatcher.UIThread.RunJobs();
        scroll.Offset.Y.ShouldBeGreaterThan(0);

        var result = await form.SubmitAsync();

        result.ShouldBeFalse();
        validate.Count.ShouldBe(1);
        failed.ShouldNotBeNull();
        failed.InvalidItems.ShouldBe(new[] { items[0], items[5] });
        failed.FirstInvalidItem.ShouldBeSameAs(items[0]);
        TopLevel.GetTopLevel(form)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(items[0].Content);
        Dispatcher.UIThread.RunJobs();
        scroll.Offset.Y.ShouldBe(0);
        items[5].EffectiveErrorMessage.ShouldBe("Required.");
    }

    [AvaloniaFact]
    public async Task SuppressFocus_Leaves_Focus_To_The_App()
    {
        var model = new FormTestModel();
        var other = new Input();
        var item = Item("Name", BoundInput(model));
        var (form, _) = NewForm(item, other);
        form.SubmitFailed += (_, e) => e.SuppressFocus = true;
        TestHelpers.Show(form);
        other.Focus();
        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        (await form.SubmitAsync()).ShouldBeFalse();

        TopLevel.GetTopLevel(form)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(other);
    }

    [AvaloniaFact]
    public async Task A_Valid_Submit_Raises_Submitted_And_Runs_The_Command_With_Its_Parameter()
    {
        var order = new List<string>();
        var submit = new CountingCommand(_ => order.Add("command"));
        var (form, _) = NewForm(Item("Name", new Input()));
        form.SubmitCommand = submit;
        form.SubmitCommandParameter = "payload";
        form.Submitted += (_, _) => order.Add("submitted");
        TestHelpers.Show(form);

        (await form.SubmitAsync()).ShouldBeTrue();

        order.ShouldBe(new[] { "submitted", "command" });
        submit.LastParameter.ShouldBe("payload");

        submit.Enabled = false;
        (await form.SubmitAsync()).ShouldBeFalse();
        submit.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Submit_Waits_For_Running_Checks_And_A_Second_Call_Returns_The_Same_Task()
    {
        var model = new FormTestModel();
        var submit = new CountingCommand();
        var item = Item("User name", BoundInput(model));
        item.IsValidating = true;
        var (form, _) = NewForm(item);
        form.SubmitCommand = submit;
        TestHelpers.Show(form);
        form.IsValidating.ShouldBeTrue();
        item.Classes.ShouldContain(FormItem.ValidatingPseudoClass);
        ItemPart<FieldMessage>(item, "PART_Status").Text.ShouldBe("Checking…");

        var first = form.SubmitAsync();
        var second = form.SubmitAsync();
        Dispatcher.UIThread.RunJobs();

        second.ShouldBeSameAs(first);
        first.IsCompleted.ShouldBeFalse();
        submit.Count.ShouldBe(0);

        item.IsValidating = false;
        TestHelpers.PumpUntil(() => first.IsCompleted).ShouldBeTrue();

        first.Result.ShouldBeTrue();
        submit.Count.ShouldBe(1);
        ItemPart<FieldMessage>(item, "PART_Status").Text.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Check_That_Ends_With_An_Error_Fails_The_Submit()
    {
        var model = new FormTestModel();
        var item = Item("User name", BoundInput(model));
        item.IsValidating = true;
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);

        var task = form.SubmitAsync();
        // While checking, the old error is put away.
        model.SetErrors(nameof(FormTestModel.Text), "Old error.");
        item.EffectiveErrorMessage.ShouldBeNull();
        model.SetErrors(nameof(FormTestModel.Text), "That user name is taken.");
        item.IsValidating = false;
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();

        task.Result.ShouldBeFalse();
        item.EffectiveErrorMessage.ShouldBe("That user name is taken.");
    }

    [AvaloniaFact]
    public void ResetFields_Stops_A_Waiting_Submit_Runs_The_Reset_Command_And_Clears()
    {
        var model = new FormTestModel();
        var item = Item("User name", BoundInput(model));
        var (form, _) = NewForm(item);
        var reset = new CountingCommand(_ => model.SetErrors(nameof(FormTestModel.Text), "Required."));
        form.ResetCommand = reset;
        var resetRaised = 0;
        form.FieldsReset += (_, _) => resetRaised++;
        TestHelpers.Show(form);
        item.IsValidating = true;
        var task = form.SubmitAsync();

        form.ResetFields();
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();

        task.Result.ShouldBeFalse();
        reset.Count.ShouldBe(1);
        resetRaised.ShouldBe(1);
        form.IsSubmitAttempted.ShouldBeFalse();
        item.EffectiveErrorMessage.ShouldBeNull(); // the errors the reset re-reported stay hidden
        item.HasErrors.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Action_Buttons_Submit_And_Reset_And_A_Default_Button_Submits_On_Enter()
    {
        var model = new FormTestModel();
        var submit = new CountingCommand();
        var reset = new CountingCommand();
        var input = BoundInput(model);
        var submitButton = new Button { Content = "Create account", IsDefault = true };
        Form.SetAction(submitButton, FormAction.Submit);
        var resetButton = new Button { Content = "Reset", Variant = ButtonVariant.Secondary };
        Form.SetAction(resetButton, FormAction.Reset);
        var (form, _) = NewForm(Item("Name", input), submitButton, resetButton);
        form.SubmitCommand = submit;
        form.ResetCommand = reset;
        var window = TestHelpers.Show(form);

        input.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        submit.Count.ShouldBe(1);

        resetButton.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        reset.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void FocusFirstError_Focuses_The_Item_A_Server_Error_Arrived_On()
    {
        var other = new Input();
        var email = new Input();
        var emailItem = Item("Email", email);
        var (form, _) = NewForm(Item("Name", new Input()), emailItem, other);
        TestHelpers.Show(form);
        other.Focus();

        form.FocusFirstError().ShouldBeFalse();
        emailItem.ErrorMessage = "That email is already registered.";

        form.FocusFirstError().ShouldBeTrue();
        TopLevel.GetTopLevel(form)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(email);
        form.GetInvalidItems().ShouldBe(new[] { emailItem });
    }

    // --- Skipped items ---

    [AvaloniaFact]
    public void Disabled_Hidden_ReadOnly_And_Switched_Off_Items_Do_Not_Count()
    {
        var model = new FormTestModel();
        var items = Enumerable.Range(0, 4).Select(_ => Item("Name", BoundInput(model))).ToArray();
        var (form, _) = NewForm(items);
        form.ErrorTrigger = FormErrorTrigger.Change;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        form.ErrorCount.ShouldBe(4);

        items[0].IsValidationEnabled = false;
        items[1].IsEnabled = false;
        items[2].IsVisible = false;
        Form.SetIsReadOnly(items[3], true);

        form.ErrorCount.ShouldBe(0);
        form.HasErrors.ShouldBeFalse();
        items.ShouldAllBe(i => i.EffectiveErrorMessage == null && !i.HasErrors);
        form.Validate().ShouldBeTrue();

        items[3].ClearValue(Form.IsReadOnlyProperty);
        form.ErrorCount.ShouldBe(1);
        form.IsValidationEnabled = false;
        form.ErrorCount.ShouldBe(0);
        form.HasErrors.ShouldBeFalse();
        form.SubmitAsync().Result.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Disabled_Form_Skips_Every_Item()
    {
        var model = new FormTestModel();
        var (form, _) = NewForm(Item("Name", BoundInput(model)));
        form.ErrorTrigger = FormErrorTrigger.Change;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        form.ErrorCount.ShouldBe(1);

        form.IsEnabled = false;

        form.ErrorCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_ReadOnly_Form_Makes_Its_Text_Fields_ReadOnly_And_Hides_The_Required_Mark()
    {
        var input = new Input();
        var item = Item("Name", input, required: true);
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);
        ItemPart<TextBlock>(item, FormItem.AsteriskPart).IsVisible.ShouldBeTrue();

        form.IsReadOnly = true;

        input.IsReadOnly.ShouldBeTrue();
        item.Classes.ShouldContain(FormItem.ReadOnlyPseudoClass);
        form.Classes.ShouldContain(Form.ReadOnlyPseudoClass);
        ItemPart<TextBlock>(item, FormItem.AsteriskPart).IsVisible.ShouldBeFalse();

        form.IsReadOnly = false;
        input.IsReadOnly.ShouldBeFalse();
    }

    // --- Error source ---

    [AvaloniaFact]
    public async Task The_Error_Source_Blocks_A_Submit_Without_A_Mounted_Item()
    {
        var model = new FormTestModel();
        var submit = new CountingCommand();
        var (form, _) = NewForm(Item("Name", new Input()));
        form.ErrorSource = model;
        form.SubmitCommand = submit;
        FormSubmitFailedEventArgs? failed = null;
        form.SubmitFailed += (_, e) => failed = e;
        TestHelpers.Show(form);

        model.SetErrors("", "The plan does not allow more seats.");
        model.SetErrors("HiddenTabField", "Required.");

        form.HasErrors.ShouldBeTrue();
        form.ErrorCount.ShouldBe(0);
        form.FormErrors.ShouldBe(new object[] { "The plan does not allow more seats." });
        (await form.SubmitAsync()).ShouldBeFalse();
        failed!.FirstInvalidItem.ShouldBeNull();
        failed.FormErrors.ShouldBe(new object[] { "The plan does not allow more seats." });
        submit.Count.ShouldBe(0);

        model.ClearAllErrors();
        form.HasErrors.ShouldBeFalse();
        (await form.SubmitAsync()).ShouldBeTrue();
    }

    // --- Field contract ---

    [AvaloniaFact]
    public void Arc_Fields_Leave_Label_And_Error_To_The_Item_And_Turn_Red_With_It()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        input.Label = "Own label";
        var other = new Input();
        var item = Item("Name", input);
        var (form, _) = NewForm(item, other);
        TestHelpers.Show(form);

        input.IsLabelVisible.ShouldBeFalse();
        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        input.EffectiveErrorMessage.ShouldBeNull();
        TestHelpers.Descendant<FieldMessage>(input, "PART_Error").Text.ShouldBeNull();
        input.IsInvalid.ShouldBeFalse();
        DataValidationErrors.GetHasErrors(input).ShouldBeTrue();

        input.Focus();
        other.Focus();

        item.EffectiveErrorMessage.ShouldBe("Required.");
        input.IsInvalid.ShouldBeTrue();
        TestHelpers.Descendant<FieldFrame>(input).IsInvalid.ShouldBeTrue();
        input.EffectiveErrorMessage.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Pickers_And_Tag_Inputs_Leave_Their_Label_And_Error_To_The_Item()
    {
        var time = new TimePicker { Label = "Start", ErrorMessage = "Pick a time after 9:00." };
        var tags = new TagInput { Label = "Tags" };
        var (form, _) = NewForm(Item("Start", time), Item("Tags", tags));
        TestHelpers.Show(form);

        time.IsLabelVisible.ShouldBeFalse();
        tags.IsLabelVisible.ShouldBeFalse();
        time.EffectiveErrorMessage.ShouldBeNull();
        time.IsInvalid.ShouldBeTrue(); // an explicit error shows at once
        ((FormItem)time.Parent!).EffectiveErrorMessage.ShouldBe("Pick a time after 9:00.");
    }

    [AvaloniaFact]
    public void ValidateAsync_Waits_For_Running_Checks()
    {
        var model = new FormTestModel();
        var item = Item("User name", BoundInput(model));
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);
        item.IsValidating = true;

        form.Validate().ShouldBeFalse();
        var task = form.ValidateAsync();
        Dispatcher.UIThread.RunJobs();
        task.IsCompleted.ShouldBeFalse();

        item.IsValidating = false;
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();
        task.Result.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ClearValidation_On_One_Item_Hides_Only_Its_Errors()
    {
        var model = new FormTestModel();
        var first = Item("First", BoundInput(model));
        var second = Item("Second", BoundInput(model, nameof(FormTestModel.Other)));
        var (form, _) = NewForm(first, second);
        form.ErrorTrigger = FormErrorTrigger.Submit;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "A.");
        model.SetErrors(nameof(FormTestModel.Other), "B.");
        form.Validate();

        first.ClearValidation();

        first.EffectiveErrorMessage.ShouldBeNull();
        second.EffectiveErrorMessage.ShouldBe("B.");
    }

    [AvaloniaFact]
    public void An_Explicit_Item_Error_Turns_Every_Field_Red()
    {
        var first = new Input();
        var second = new Input();
        var item = new FormItem { Label = "Range", Content = new StackPanel { Children = { first, second } } };
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);

        item.ErrorMessage = "The end is before the start.";

        first.IsInvalid.ShouldBeTrue();
        second.IsInvalid.ShouldBeTrue();
        item.ErrorMessage = null;
        first.IsInvalid.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Field_Outside_A_Form_Item_Keeps_Its_Own_Rows()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        input.Label = "Name";
        TestHelpers.Show(input);

        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        input.IsLabelVisible.ShouldBeTrue();
        input.EffectiveErrorMessage.ShouldBe("Required.");
        input.IsInvalid.ShouldBeTrue();
    }

    // --- Placement ---

    [AvaloniaFact]
    public void Tooltip_Placement_Shows_The_Error_In_A_Tooltip_Instead_Of_A_Row()
    {
        var model = new FormTestModel();
        var item = Item("Email", BoundInput(model));
        var (form, _) = NewForm(item);
        form.ErrorTrigger = FormErrorTrigger.Change;
        form.ErrorPlacement = FormErrorPlacement.Tooltip;
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "Enter an email like name@example.com.");

        var presenter = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(item, "PART_ContentPresenter");
        Tooltip.GetTip(presenter).ShouldBe("Enter an email like name@example.com.");
        Tooltip.GetSide(presenter).ShouldBe(TooltipSide.Bottom);
        ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBeNull();
        ItemPart<TextBlock>(item, "PART_ErrorAnnouncer").Text.ShouldBe("Enter an email like name@example.com.");

        form.ErrorPlacement = FormErrorPlacement.Inline;
        Tooltip.GetTip(presenter).ShouldBeNull();
        ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBe("Enter an email like name@example.com.");
    }

    [AvaloniaFact]
    public void None_Placement_Shows_Only_The_Invalid_State()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var item = Item("Email", input);
        var (form, _) = NewForm(item);
        form.ErrorTrigger = FormErrorTrigger.Change;
        form.ErrorPlacement = FormErrorPlacement.None;
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "Required.");

        ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBeNull();
        input.IsInvalid.ShouldBeTrue();
        item.EffectiveErrorMessage.ShouldBe("Required.");
    }

    [AvaloniaFact]
    public void An_Error_Template_Presents_The_First_Error_Object()
    {
        var model = new FormTestModel();
        var item = Item("Email", BoundInput(model));
        item.ErrorTemplate = new FuncDataTemplate<object>((error, _) => new TextBlock { Name = "Templated", Text = $"! {error}" });
        var (form, _) = NewForm(item);
        form.ErrorTrigger = FormErrorTrigger.Change;
        TestHelpers.Show(form);

        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Descendant<TextBlock>(item, "Templated").Text.ShouldBe("! Required.");
        ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBeNull();
    }

    // --- Layout ---

    [AvaloniaFact]
    public void Left_Labels_Share_The_Width_Of_The_Widest_Label_Up_To_The_Limit()
    {
        var shortItem = Item("Age", new Input());
        var longItem = Item("Preferred contact method", new Input());
        var (form, _) = NewForm(shortItem, longItem);
        form.LabelPlacement = FormLabelPlacement.Left;
        var window = TestHelpers.Show(form);
        window.Width = 800;
        Dispatcher.UIThread.RunJobs();

        shortItem.Classes.ShouldContain(FormItem.LabelLeftPseudoClass);
        var shortInput = (Input)shortItem.Content!;
        var longInput = (Input)longItem.Content!;
        var shortLeft = shortInput.TranslatePoint(default, form)!.Value.X;
        var longLeft = longInput.TranslatePoint(default, form)!.Value.X;
        shortLeft.ShouldBe(longLeft, 0.5);
        shortLeft.ShouldBeGreaterThan(0);
        var widest = ItemPart<TextBlock>(longItem, Field.LabelPart).DesiredSize.Width;
        shortLeft.ShouldBeLessThanOrEqualTo(240 + 16 + 0.5);

        // A very long label is capped and wraps.
        longItem.Label = string.Join(" ", Enumerable.Repeat("Preferred contact method", 6));
        Dispatcher.UIThread.RunJobs();
        shortInput.TranslatePoint(default, form)!.Value.X.ShouldBe(240 + 16, 0.5);
        widest.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void A_Fixed_Label_Width_And_A_New_Shared_Size_Scope()
    {
        var first = Item("Age", new Input());
        var section = new StackPanel { Children = { Item("A rather long label", new Input()) } };
        Grid.SetIsSharedSizeScope(section, true);
        var (form, _) = NewForm(first, section);
        form.LabelPlacement = FormLabelPlacement.Left;
        var window = TestHelpers.Show(form);
        window.Width = 800;
        Dispatcher.UIThread.RunJobs();

        var sectionItem = (FormItem)section.Children[0];
        var firstLeft = ((Control)first.Content!).TranslatePoint(default, form)!.Value.X;
        var sectionLeft = ((Control)sectionItem.Content!).TranslatePoint(default, form)!.Value.X;
        firstLeft.ShouldBeLessThan(sectionLeft);

        form.LabelWidth = 120;
        Dispatcher.UIThread.RunJobs();
        ((Control)first.Content!).TranslatePoint(default, form)!.Value.X.ShouldBe(120 + 16, 0.5);
        ((Control)sectionItem.Content!).TranslatePoint(default, form)!.Value.X.ShouldBe(120 + 16, 0.5);
    }

    [AvaloniaFact]
    public void Top_Labels_Sit_Above_The_Editor()
    {
        var input = new Input();
        var item = Item("Name", input);
        var (form, _) = NewForm(item);
        TestHelpers.Show(form);

        var label = ItemPart<TextBlock>(item, Field.LabelPart);
        label.TranslatePoint(default, item)!.Value.Y.ShouldBe(0, 0.5);
        input.TranslatePoint(default, item)!.Value.Y.ShouldBeGreaterThan(label.Bounds.Height);
        input.TranslatePoint(default, item)!.Value.X.ShouldBe(0, 0.5);
    }

    // --- Marks ---

    [AvaloniaFact]
    public void Required_Indicator_Marks()
    {
        var required = Item("Name", new Input(), required: true);
        var optional = Item("Nickname", new Input());
        var (form, _) = NewForm(required, optional);
        TestHelpers.Show(form);

        required.Classes.ShouldContain(FormItem.RequiredPseudoClass);
        ItemPart<TextBlock>(required, FormItem.AsteriskPart).IsVisible.ShouldBeTrue();
        ItemPart<TextBlock>(optional, FormItem.AsteriskPart).IsVisible.ShouldBeFalse();
        ItemPart<TextBlock>(optional, FormItem.OptionalPart).IsVisible.ShouldBeFalse();

        form.RequiredIndicator = FormRequiredIndicator.Optional;
        ItemPart<TextBlock>(required, FormItem.AsteriskPart).IsVisible.ShouldBeFalse();
        ItemPart<TextBlock>(optional, FormItem.OptionalPart).IsVisible.ShouldBeTrue();
        ItemPart<TextBlock>(optional, FormItem.OptionalPart).Text.ShouldBe("Optional");

        form.RequiredIndicator = FormRequiredIndicator.None;
        ItemPart<TextBlock>(optional, FormItem.OptionalPart).IsVisible.ShouldBeFalse();
    }

    // --- Accessibility and localization ---

    [AvaloniaFact]
    public void The_Item_Names_And_Describes_Its_Editor_With_Only_The_Shown_Error()
    {
        var model = new FormTestModel();
        var input = BoundInput(model);
        var other = new Input();
        var item = new FormItem { Label = "User name", Description = "Lowercase letters.", IsRequired = true, Content = input };
        var (form, _) = NewForm(item, other);
        TestHelpers.Show(form);

        AutomationProperties.GetLabeledBy(input).ShouldBeSameAs(ItemPart<TextBlock>(item, Field.LabelPart));
        AutomationProperties.GetName(input).ShouldBe("User name");
        AutomationProperties.GetIsRequiredForForm(input).ShouldBeTrue();
        AutomationProperties.GetHelpText(input).ShouldStartWith("Lowercase letters.");

        model.SetErrors(nameof(FormTestModel.Text), "That user name is taken.");
        AutomationProperties.GetHelpText(input)!.ShouldNotContain("taken");

        input.Focus();
        other.Focus();
        AutomationProperties.GetHelpText(input)!.ShouldContain("Lowercase letters. That user name is taken.");

        AutomationProperties.GetLandmarkType(form).ShouldBe(AutomationLandmarkType.Form);
        ControlAutomationPeer.CreatePeerForElement(form).GetLandmarkType().ShouldBe(AutomationLandmarkType.Form);
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Focuses_The_Target()
    {
        var first = new Input();
        var second = new Input();
        var item = new FormItem { Label = "Second", Target = second, Content = new StackPanel { Children = { first, second } } };
        var (form, _) = NewForm(item);
        var window = TestHelpers.Show(form);

        var label = ItemPart<TextBlock>(item, Field.LabelPart);
        // Headless text has no glyph area to hit; a background makes the whole label hit-testable.
        label.Background = Avalonia.Media.Brushes.Transparent;
        FocusRingInput.Click(window, label);

        TopLevel.GetTopLevel(form)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(second);
        AutomationProperties.GetLabeledBy(second).ShouldBeSameAs(label);
    }

    [AvaloniaFact]
    public async Task A_Failed_Submit_Announces_One_Summary_And_Mutes_The_Rows_Meanwhile()
    {
        var model = new FormTestModel();
        var first = Item("Name", BoundInput(model));
        var second = Item("Email", BoundInput(model, nameof(FormTestModel.Other)));
        var (form, _) = NewForm(first, second);
        form.ErrorTrigger = FormErrorTrigger.Submit;
        TestHelpers.Show(form);
        model.SetErrors(nameof(FormTestModel.Text), "Required.");
        model.SetErrors(nameof(FormTestModel.Other), "Required.");

        var rows = new[] { first, second }.Select(i => ItemPart<FieldMessage>(i, Field.ErrorPart)).ToArray();
        var task = form.SubmitAsync();

        rows.ShouldAllBe(r => AutomationProperties.GetLiveSetting(r) == AutomationLiveSetting.Off);
        form.AnnouncedSummary.ShouldBe("2 fields need attention");
        (await task).ShouldBeFalse();
        Dispatcher.UIThread.RunJobs();
        rows.ShouldAllBe(r => AutomationProperties.GetLiveSetting(r) == AutomationLiveSetting.Assertive);
        AutomationProperties.GetLiveSetting(TestHelpers.Descendant<TextBlock>(form, Form.AnnouncerPart)).ShouldBe(AutomationLiveSetting.Polite);

        model.SetErrors(nameof(FormTestModel.Other));
        form.Validate();
        form.AnnouncedSummary.ShouldBe("1 field needs attention");
    }

    [AvaloniaFact]
    public void The_Texts_Follow_The_App_Language()
    {
        try
        {
            FormLangResources.Get(FormLangResourceKind.Optional).ShouldBe("Optional");
            FormLangResources.Get(FormLangResourceKind.Validating).ShouldBe("Checking…");
            FormLangResources.Format(FormLangResourceKind.InvalidSummaryMany, 3).ShouldBe("3 fields need attention");

            ArcLanguage.Set(LanguageTags.ZhCN);
            FormLangResources.Get(FormLangResourceKind.Optional).ShouldBe("选填");
            FormLangResources.Get(FormLangResourceKind.Validating).ShouldBe("正在检查…");
            FormLangResources.Get(FormLangResourceKind.InvalidSummaryOne).ShouldBe("有 1 个字段需要处理");
            FormLangResources.Format(FormLangResourceKind.InvalidSummaryMany, 3).ShouldBe("有 3 个字段需要处理");

            ArcLanguage.Set(LanguageTags.ZhTW);
            FormLangResources.Get(FormLangResourceKind.Optional).ShouldBe("選填");
            FormLangResources.Get(FormLangResourceKind.Validating).ShouldBe("正在檢查…");
            FormLangResources.Format(FormLangResourceKind.InvalidSummaryMany, 3).ShouldBe("有 3 個欄位需要處理");

            var optional = Item("Nickname", new Input());
            var (form, _) = NewForm(optional);
            form.RequiredIndicator = FormRequiredIndicator.Optional;
            TestHelpers.Show(form);
            ItemPart<TextBlock>(optional, FormItem.OptionalPart).Text.ShouldBe("選填");
        }
        finally
        {
            ArcLanguage.Set(LanguageTags.EnUS);
        }
    }

    [AvaloniaFact]
    public void A_Form_Releases_Its_Error_Source_When_Unloaded()
    {
        var model = new FormTestModel();
        var reference = TestHelpers.ShowAndClose(() =>
        {
            var (form, _) = NewForm(Item("Name", BoundInput(model)));
            form.ErrorSource = model;
            return form;
        });

        TestHelpers.Collect(reference);

        reference.IsAlive.ShouldBeFalse();
        GC.KeepAlive(model);
    }
}

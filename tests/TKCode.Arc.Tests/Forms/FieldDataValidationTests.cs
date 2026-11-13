using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Forms;

/// <summary>
/// Form spec section 4.3: the typed value properties of the Arc fields report their binding's validation errors, so a view
/// model's error reaches the control (Avalonia's <see cref="DataValidationErrors"/>), shows in the field's own error row
/// where it has one, and shows in a <see cref="FormItem"/> around it.
/// </summary>
public class FieldDataValidationTests
{
    private const string Error = "Bound error.";

    /// <summary>
    /// Binds <paramref name="property"/> of a control from <paramref name="create"/> to <paramref name="path"/>, reports an
    /// error and checks that it reaches the control (and <paramref name="ownError"/>, the field's own shown error), that it
    /// clears, and that a form item around a second control shows it while the field leaves its own row closed.
    /// </summary>
    private static void Check<T>(Func<T> create, AvaloniaProperty property, string path, Func<T, string?>? ownError = null,
        Func<T, bool>? ownInvalid = null, Action<FormTestModel>? seed = null) where T : Control
    {
        var model = new FormTestModel();
        seed?.Invoke(model);
        var control = create().BindTo(property, model, path);
        var window = TestHelpers.Show(control);

        model.SetErrors(path, Error);
        Dispatcher.UIThread.RunJobs();
        DataValidationErrors.GetErrors(control).ShouldNotBeNull().ShouldContain(Error);
        DataValidationErrors.GetHasErrors(control).ShouldBeTrue();
        if (ownError is not null)
        {
            ownError(control).ShouldBe(Error);
        }
        if (ownInvalid is not null)
        {
            ownInvalid(control).ShouldBeTrue();
        }

        model.SetErrors(path);
        Dispatcher.UIThread.RunJobs();
        DataValidationErrors.GetHasErrors(control).ShouldBeFalse();
        if (ownError is not null)
        {
            ownError(control).ShouldBeNull();
        }
        if (ownInvalid is not null)
        {
            ownInvalid(control).ShouldBeFalse();
        }
        window.Close();

        var formModel = new FormTestModel();
        seed?.Invoke(formModel);
        var inner = create().BindTo(property, formModel, path);
        var item = new FormItem { Label = "Field", Content = inner };
        var form = new Form { ErrorTrigger = FormErrorTrigger.Change, Content = item };
        TestHelpers.Show(form);

        formModel.SetErrors(path, Error);
        Dispatcher.UIThread.RunJobs();
        item.HasErrors.ShouldBeTrue();
        item.EffectiveErrorMessage.ShouldBe(Error);
        FormTests.ItemPart<FieldMessage>(item, Field.ErrorPart).Text.ShouldBe(Error);
        form.ErrorCount.ShouldBe(1);
        if (ownError is not null)
        {
            ownError(inner).ShouldBeNull(); // the item shows it, not the field's own row
        }
        if (ownInvalid is not null)
        {
            ownInvalid(inner).ShouldBeTrue();
        }
    }

    private static string[] Items() => ["Alpha", "Beta", "Gamma"];

    // --- Toggles ---

    [AvaloniaFact]
    public void Checkbox_IsChecked() =>
        Check(() => new Checkbox { Content = "Accept the terms" }, ToggleButton.IsCheckedProperty, nameof(FormTestModel.NullableBool),
            ownInvalid: c => c.IsInvalid);

    [AvaloniaFact]
    public void Switch_IsChecked() =>
        Check(() => new Switch { Content = "Notifications" }, ToggleButton.IsCheckedProperty, nameof(FormTestModel.NullableBool));

    // --- Text-based fields with a typed value ---

    [AvaloniaFact]
    public void NumberField_Value() =>
        Check(() => new NumberField(), NumberField.ValueProperty, nameof(FormTestModel.Decimal), f => f.EffectiveErrorMessage, f => f.IsInvalid);

    [AvaloniaFact]
    public void MoneyInput_Value() =>
        Check(() => new MoneyInput(), MoneyInput.ValueProperty, nameof(FormTestModel.NullableDecimal), f => f.EffectiveErrorMessage, f => f.IsInvalid);

    [AvaloniaFact]
    public void PhoneInput_Value() =>
        Check(() => new PhoneInput(), PhoneInput.ValueProperty, nameof(FormTestModel.Phone), f => f.EffectiveErrorMessage, f => f.IsInvalid);

    [AvaloniaFact]
    public void DatePicker_SelectedDate() =>
        Check(() => new DatePicker(), DatePicker.SelectedDateProperty, nameof(FormTestModel.Date), f => f.EffectiveErrorMessage, f => f.IsInvalid);

    // --- Selects ---

    [AvaloniaFact]
    public void Select_SelectedValue() =>
        Check(() => new Select { ItemsSource = Items() }, SelectingItemsControl.SelectedValueProperty, nameof(FormTestModel.Object));

    [AvaloniaFact]
    public void Select_SelectedIndex() =>
        Check(() => new Select { ItemsSource = Items() }, SelectingItemsControl.SelectedIndexProperty, nameof(FormTestModel.Index));

    [AvaloniaFact]
    public void Combobox_SelectedValue() =>
        Check(() => new Combobox { ItemsSource = Items() }, SelectingItemsControl.SelectedValueProperty, nameof(FormTestModel.Object));

    [AvaloniaFact]
    public void MorphSelect_SelectedIndex() =>
        Check(() => new MorphSelect { ItemsSource = Items() }, SelectingItemsControl.SelectedIndexProperty, nameof(FormTestModel.Index));

    [AvaloniaFact]
    public void MultiSelect_SelectedItems() =>
        Check(() => new MultiSelect { ItemsSource = Items() }, MultiSelect.SelectedItemsProperty, nameof(FormTestModel.List),
            seed: m => m.List = new List<object>());

    [AvaloniaFact]
    public void RadioGroup_SelectedValue() =>
        Check(() => new RadioGroup { ItemsSource = Items() }, SelectingItemsControl.SelectedValueProperty, nameof(FormTestModel.Object));

    [AvaloniaFact]
    public void RadioCards_SelectedIndex() =>
        Check(() => new RadioCards { ItemsSource = Items() }, SelectingItemsControl.SelectedIndexProperty, nameof(FormTestModel.Index));

    [AvaloniaFact]
    public void SegmentedControl_SelectedValue() =>
        Check(() => new SegmentedControl { ItemsSource = Items() }, SelectingItemsControl.SelectedValueProperty, nameof(FormTestModel.Object));

    [AvaloniaFact]
    public void ChipGroup_SelectedItems() =>
        Check(() => new ChipGroup { ItemsSource = Items() }, ChipGroup.SelectedItemsProperty, nameof(FormTestModel.List),
            seed: m => m.List = new List<object>());

    [AvaloniaFact]
    public void ToggleGroup_SelectedIndex() =>
        Check(() => new ToggleGroup { ItemsSource = Items() }, SelectingItemsControl.SelectedIndexProperty, nameof(FormTestModel.Index));

    [AvaloniaFact]
    public void Cascader_SelectedValue() =>
        Check(() => new Cascader(), Cascader.SelectedValueProperty, nameof(FormTestModel.Object));

    [AvaloniaFact]
    public void TreeSelect_SelectedValue_And_SelectedValues()
    {
        Check(() => new TreeSelect(), TreeSelect.SelectedValueProperty, nameof(FormTestModel.Object));
        Check(() => new TreeSelect { SelectionMode = TreeSelectMode.Multiple }, TreeSelect.SelectedValuesProperty, nameof(FormTestModel.Values));
    }

    [AvaloniaFact]
    public void EnumSelector_SelectedEnum() =>
        Check(() => new EnumSelector(), EnumSelector.SelectedEnumProperty, nameof(FormTestModel.Object));

    // --- Pickers with their own error row ---

    [AvaloniaFact]
    public void TimePicker_SelectedTime() =>
        Check(() => new TimePicker(), TimePicker.SelectedTimeProperty, nameof(FormTestModel.Time), p => p.EffectiveErrorMessage, p => p.IsInvalid);

    [AvaloniaFact]
    public void DateRangePicker_SelectedRange() =>
        Check(() => new DateRangePicker(), DateRangePicker.SelectedRangeProperty, nameof(FormTestModel.Range), p => p.EffectiveErrorMessage);

    [AvaloniaFact]
    public void ColorPicker_Color() =>
        Check(() => new ColorPicker(), ColorPicker.ColorProperty, nameof(FormTestModel.Color), p => p.EffectiveErrorMessage);

    [AvaloniaFact]
    public void TagInput_Value() =>
        Check(() => new TagInput(), TagInput.ValueProperty, nameof(FormTestModel.Tags), t => t.EffectiveErrorMessage, t => t.IsInvalid);

    // --- Other value controls ---

    [AvaloniaFact]
    public void Slider_Value_LowerValue_And_UpperValue()
    {
        Check(() => new Slider(), RangeBase.ValueProperty, nameof(FormTestModel.Double));
        Check(() => new Slider { IsRange = true }, Slider.LowerValueProperty, nameof(FormTestModel.Double));
        Check(() => new Slider { IsRange = true }, Slider.UpperValueProperty, nameof(FormTestModel.Double));
    }

    [AvaloniaFact]
    public void Rating_Value() =>
        Check(() => new Rating(), Rating.ValueProperty, nameof(FormTestModel.Double));

    [AvaloniaFact]
    public void ScrubInput_Value() =>
        Check(() => new ScrubInput(), ScrubInput.ValueProperty, nameof(FormTestModel.Double));

    [AvaloniaFact]
    public void ShortcutRecorder_Value() =>
        Check(() => new ShortcutRecorder(), ShortcutRecorder.ValueProperty, nameof(FormTestModel.Gesture));

    // --- Defaults stay ---

    [AvaloniaFact]
    public void Defaults_Do_Not_Change()
    {
        new Select().SelectedIndex.ShouldBe(-1);
        new RadioGroup().SelectedIndex.ShouldBe(-1);
        new Checkbox().IsChecked.ShouldBe(false);
        new Checkbox().IsInvalid.ShouldBeFalse();
        new Switch().IsChecked.ShouldBe(false);
        new Slider().Value.ShouldBe(0);
        new TimePicker().SelectedTime.ShouldBe(new TimeOnly(9, 0));
        new DateRangePicker().IsLabelVisible.ShouldBeFalse();
        new TagInput().IsLabelVisible.ShouldBeTrue();
        new Input().IsLabelVisible.ShouldBeTrue();
        new Input().IsReadOnly.ShouldBeFalse();
        new Cascader().IsReadOnly.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Bound_Error_Shows_In_The_Field_Error_Row()
    {
        var model = new FormTestModel();
        var number = new NumberField { Label = "Seats" }.BindTo(NumberField.ValueProperty, model, nameof(FormTestModel.Decimal));
        TestHelpers.Show(number);

        model.SetErrors(nameof(FormTestModel.Decimal), "Pick at least one seat.");

        TestHelpers.Descendant<FieldMessage>(number, "PART_Error").Text.ShouldBe("Pick at least one seat.");
        TestHelpers.Descendant<FieldFrame>(number).IsInvalid.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ReadOnly_Capable_Editors_Follow_Form_IsReadOnly()
    {
        var cascader = new Cascader();
        var tree = new TreeSelect();
        var rating = new Rating();
        var tokens = new TokenTextEditor();
        var rich = new RichTextEditor();
        var number = new NumberField();
        var form = new Form { Content = new StackPanel { Children = { cascader, tree, rating, tokens, rich, number } } };
        TestHelpers.Show(form);

        form.IsReadOnly = true;

        cascader.IsReadOnly.ShouldBeTrue();
        tree.IsReadOnly.ShouldBeTrue();
        rating.IsReadOnly.ShouldBeTrue();
        tokens.IsReadOnly.ShouldBeTrue();
        rich.IsReadOnly.ShouldBeTrue();
        number.IsReadOnly.ShouldBeTrue();

        form.IsReadOnly = false;
        cascader.IsReadOnly.ShouldBeFalse();
        rich.IsReadOnly.ShouldBeFalse();
    }
}

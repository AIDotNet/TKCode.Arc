using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class InlineEditTests
{
    private static TextBox Editor(InlineEdit edit) => edit.GetVisualDescendants().OfType<InlineEditEditor>().First();

    [AvaloniaFact]
    public void Enter_Commits_And_Updates_The_Value()
    {
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas" };
        var window = TestHelpers.Show(edit);
        try
        {
            InlineEditCommittedEventArgs? committed = null;
            edit.TextCommitted += (_, e) => committed = e;
            var display = edit.GetVisualDescendants().OfType<InlineEditDisplay>().First();
            display.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeTrue();
            Editor(edit).IsFocused.ShouldBeTrue();
            // A keyboard activation selects everything, ready to retype.
            Editor(edit).SelectedText.ShouldBe("Atlas");

            window.KeyTextInput("Atlas  2026 ");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeFalse();
            committed.ShouldNotBeNull();
            committed!.OldValue.ShouldBe("Atlas");
            committed.NewValue.ShouldBe("Atlas 2026");
            edit.Value.ShouldBe("Atlas 2026");
            edit.Phase.ShouldBe(InlineEditPhase.Saved);
            display.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Cancels_And_Keeps_The_Value()
    {
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas" };
        var window = TestHelpers.Show(edit);
        try
        {
            var canceled = false;
            edit.EditCanceled += (_, _) => canceled = true;
            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("Nope");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            canceled.ShouldBeTrue();
            edit.IsEditing.ShouldBeFalse();
            edit.Value.ShouldBe("Atlas");
            edit.ShownText.ShouldBe("Atlas");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Validation_Keeps_The_Field_Open_With_An_Error()
    {
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas", Validator = v => v.Length < 3 ? "Use at least 3 characters." : null };
        var window = TestHelpers.Show(edit);
        try
        {
            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("A");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeTrue();
            edit.ErrorMessage.ShouldBe("Use at least 3 characters.");
            edit.Classes.ShouldContain(":invalid");
            window.KeyTextInput("da");
            Dispatcher.UIThread.RunJobs();
            edit.ErrorMessage.ShouldBeNull();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.Value.ShouldBe("Ada");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Failed_Save_Rolls_Back_And_Offers_A_Retry()
    {
        var gate = new TaskCompletionSource();
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas", SaveHandler = _ => gate.Task };
        var window = TestHelpers.Show(edit);
        try
        {
            InlineEditSaveFailedEventArgs? failed = null;
            edit.SaveFailed += (_, e) => failed = e;
            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("Orion");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.Phase.ShouldBe(InlineEditPhase.Saving);
            edit.ShownText.ShouldBe("Orion");
            edit.Classes.ShouldContain(":saving");
            edit.Value.ShouldBe("Atlas");

            gate.SetException(new InvalidOperationException("offline"));
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            edit.Phase.ShouldBe(InlineEditPhase.Failed);
            edit.ShownText.ShouldBe("Atlas");
            edit.Value.ShouldBe("Atlas");
            failed.ShouldNotBeNull();
            failed!.AttemptedValue.ShouldBe("Orion");
            edit.Classes.ShouldContain(":flash");
            edit.GetVisualDescendants().OfType<InlineEditRetryLink>().ShouldNotBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Multiline_Shift_Enter_Adds_A_Line_And_Enter_Saves()
    {
        var edit = new InlineEdit { Label = "Description", Variant = InlineEditVariant.Body, IsMultiline = true, Value = "One" };
        var window = TestHelpers.Show(new Border { Width = 360, Child = edit });
        try
        {
            edit.BeginEdit(caret: 3);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
            window.KeyTextInput("Two");
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeFalse();
            edit.Value.Replace("\r\n", "\n").ShouldBe("One\nTwo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void External_Value_Replaces_The_Text_When_Not_Editing()
    {
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas" };
        var window = TestHelpers.Show(edit);
        try
        {
            edit.Value = "Orion";
            Dispatcher.UIThread.RunJobs();
            edit.ShownText.ShouldBe("Orion");
            Avalonia.Automation.AutomationProperties.GetName(edit.GetVisualDescendants().OfType<InlineEditDisplay>().First())
                .ShouldBe("Project name: Orion");
        }
        finally
        {
            window.Close();
        }
    }
}

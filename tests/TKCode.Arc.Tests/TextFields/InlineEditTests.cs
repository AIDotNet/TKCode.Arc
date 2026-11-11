using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.TextFields;

public class InlineEditTests
{
    private static TextBox Editor(InlineEdit edit) => edit.GetVisualDescendants().OfType<InlineEditEditor>().First();

    private static Border Line(InlineEdit edit) => TestHelpers.Descendant<Border>(edit, "PART_Line");

    private static InlineEditBox Box(InlineEdit edit) => TestHelpers.Descendant<InlineEditBox>(edit, InlineEdit.BoxPart);

    private static object? SlotContent(InlineEdit edit) => TestHelpers.Descendant<ArcContentSwap>(edit, InlineEdit.SlotPart).Content;

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

    [AvaloniaFact]
    public void Labels_Localize_Every_Spoken_Text()
    {
        // Issue #110: the announcements and button names were hard-coded English.
        var edit = new InlineEdit
        {
            Label = "项目名称",
            Value = "Atlas",
            Labels = new InlineEditLabels
            {
                Saving = label => $"正在保存{label}",
                Saved = label => $"{label}已保存",
                SaveButton = label => $"保存{label}",
                CancelButton = "取消编辑",
                EditHelp = "激活即可编辑。",
                KeysHelp = "Enter 保存，Esc 取消。"
            }
        };
        var gate = new TaskCompletionSource();
        edit.SaveHandler = _ => gate.Task;
        var window = TestHelpers.Show(edit);
        try
        {
            var display = edit.GetVisualDescendants().OfType<InlineEditDisplay>().First();
            AutomationProperties.GetHelpText(display).ShouldBe("激活即可编辑。");
            var status = edit.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Status");

            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetHelpText(Editor(edit)).ShouldBe("Enter 保存，Esc 取消。");
            var names = edit.GetVisualDescendants().OfType<InlineEditActionButton>().Select(AutomationProperties.GetName).ToList();
            names.ShouldBe(["保存项目名称", "取消编辑"]);

            window.KeyTextInput("2");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            status.Text.ShouldBe("正在保存项目名称");
            gate.SetResult();
            TestHelpers.PumpUntil(() => edit.Phase == InlineEditPhase.Saved).ShouldBeTrue();
            status.Text.ShouldBe("项目名称已保存");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Default_Labels_Keep_The_English_Text()
    {
        var labels = new InlineEditLabels();
        labels.Saving("Project name").ShouldBe("Saving project name");
        labels.Saved("Project name").ShouldBe("Project name saved");
        labels.SaveButton("Project name").ShouldBe("Save project name");
        labels.CancelButton.ShouldBe("Cancel editing");
    }

    [AvaloniaFact]
    public void The_Action_Reserve_Is_Kept_By_Default_And_Dropped_When_Off()
    {
        // Issue #110: a single line always kept 66px for the actions, so a tag could not follow the name.
        var reserved = new InlineEdit { Label = "Project name", Value = "Atlas" };
        var hugging = new InlineEdit { Label = "Project name", Value = "Atlas", ReserveActionSpace = false };
        var reservedTag = new Border { Width = 60, Height = 24 };
        var huggingTag = new Border { Width = 60, Height = 24 };
        var window = TestHelpers.Show(new StackPanel
        {
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { reserved, reservedTag } },
                new StackPanel { Orientation = Orientation.Horizontal, Children = { hugging, huggingTag } }
            }
        });
        try
        {
            reserved.ReserveActionSpace.ShouldBeTrue();
            reserved.Classes.ShouldNotContain(InlineEdit.NoReservePseudoClass);
            hugging.Classes.ShouldContain(InlineEdit.NoReservePseudoClass);
            Box(hugging).Bounds.Width.ShouldBe(Box(reserved).Bounds.Width, 0.01);

            Line(reserved).Bounds.Width.ShouldBe(Box(reserved).Bounds.Width + 66, 0.01);
            Line(hugging).Bounds.Width.ShouldBe(Box(hugging).Bounds.Width, 0.01);
            (reserved.Bounds.Width - hugging.Bounds.Width).ShouldBe(66, 0.01);
            // The tag follows the frame directly.
            huggingTag.Bounds.X.ShouldBe(hugging.Bounds.Right, 0.01);
            (reservedTag.Bounds.X - huggingTag.Bounds.X).ShouldBe(66, 0.01);

            // At rest the reserved line has its pencil hint; without the reserve nothing is drawn past the frame.
            SlotContent(reserved).ShouldNotBeNull();
            SlotContent(hugging).ShouldBeNull();

            // The property switches both ways.
            hugging.ReserveActionSpace = true;
            Dispatcher.UIThread.RunJobs();
            hugging.Bounds.Width.ShouldBe(reserved.Bounds.Width, 0.01);
            SlotContent(hugging).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_The_Reserve_The_Actions_Overlay_What_Follows_While_Editing()
    {
        var edit = new InlineEdit { Label = "Project name", Value = "Atlas", ReserveActionSpace = false };
        var tag = new Border { Width = 120, Height = 40, Background = Brushes.Gray };
        var window = TestHelpers.Show(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { edit, tag }
        });
        try
        {
            edit.ZIndex.ShouldBe(0);
            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            edit.IsEditing.ShouldBeTrue();

            // The reserve does not come back while editing: the control is as wide as its frame, so the text and the tag
            // only move as the box grows with the draft, never by the width of the actions.
            Line(edit).Bounds.Width.ShouldBe(Box(edit).Bounds.Width, 0.01);
            tag.Bounds.X.ShouldBe(edit.Bounds.Right, 0.01);

            // The actions sit past the control's edge, over the tag, and the control draws above it.
            var save = edit.GetVisualDescendants().OfType<InlineEditActionButton>().Single(b => b.IsPrimary);
            var saveLeft = save.TranslatePoint(default, window)!.Value.X;
            saveLeft.ShouldBeGreaterThanOrEqualTo(edit.TranslatePoint(new Point(edit.Bounds.Width, 0), window)!.Value.X - 0.01);
            edit.ZIndex.ShouldBe(1);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var center = save.TranslatePoint(new Point(save.Bounds.Width / 2, save.Bounds.Height / 2), window)!.Value;
            new Rect(tag.TranslatePoint(default, window)!.Value, tag.Bounds.Size).Contains(center).ShouldBeTrue();
            (window.InputHitTest(center) as Visual).ShouldNotBeNull()
                .FindAncestorOfType<InlineEditActionButton>(includeSelf: true).ShouldBeSameAs(save);

            // A save ends with the text alone: no reserve, no mark past the frame, and the control back in its place.
            window.KeyTextInput("Orion");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            edit.Phase.ShouldBe(InlineEditPhase.Saved);
            edit.Value.ShouldBe("Orion");
            edit.ZIndex.ShouldBe(0);
            SlotContent(edit).ShouldBeNull();
            Line(edit).Bounds.Width.ShouldBe(Box(edit).Bounds.Width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Multiline_Field_Keeps_Its_Reserve()
    {
        // A multiline box fills its line, so nothing can follow it; its 38px keeps the text from rewrapping.
        var edit = new InlineEdit
        {
            Label = "Description", Variant = InlineEditVariant.Body, IsMultiline = true, Value = "One", ReserveActionSpace = false
        };
        var window = TestHelpers.Show(new Border { Width = 360, Child = edit });
        try
        {
            Line(edit).Padding.Right.ShouldBe(38);
            (Line(edit).Bounds.Width - Box(edit).Bounds.Width).ShouldBe(38, 0.01);
            SlotContent(edit).ShouldNotBeNull();
            edit.BeginEdit();
            Dispatcher.UIThread.RunJobs();
            edit.ZIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}

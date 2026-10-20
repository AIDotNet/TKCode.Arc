using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class CheckboxTests
{
    private static void Press(Avalonia.Controls.Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Clicking_Indeterminate_Checks_It()
    {
        var box = new Checkbox { Content = "Select all", IsChecked = null };
        var window = TestHelpers.Show(box);
        try
        {
            box.CheckedState.ShouldBe(CheckboxState.Indeterminate);
            box.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            box.IsChecked.ShouldBe(true);
            box.CheckedState.ShouldBe(CheckboxState.Checked);
            Press(window, PhysicalKey.Space);
            box.IsChecked.ShouldBe(false);
            // Radix: Enter does not toggle a checkbox.
            Press(window, PhysicalKey.Enter);
            box.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CheckedState_And_IsChecked_Stay_In_Sync_And_Raise_Typed_Event()
    {
        var box = new Checkbox();
        var changes = new List<(CheckboxState Old, CheckboxState New)>();
        box.CheckedStateChanged += (_, e) => changes.Add((e.OldState, e.NewState));
        box.CheckedState = CheckboxState.Indeterminate;
        box.IsChecked.ShouldBeNull();
        box.IsChecked = true;
        box.CheckedState.ShouldBe(CheckboxState.Checked);
        changes.ShouldBe([(CheckboxState.Unchecked, CheckboxState.Indeterminate), (CheckboxState.Indeterminate, CheckboxState.Checked)]);
    }

    [AvaloniaFact]
    public void Mark_Morphs_To_Dash_And_Draws_In()
    {
        var box = new Checkbox { Content = "Rows" };
        var window = TestHelpers.Show(box);
        try
        {
            var mark = TestHelpers.Descendant<CheckboxMark>(box);
            mark.FadeValue.ShouldBe(0);
            box.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            mark.DrawProgress.ShouldBe(1);
            mark.MorphProgress.ShouldBe(0);
            mark.FillScale.ShouldBe(1);
            box.IsChecked = null;
            Dispatcher.UIThread.RunJobs();
            mark.MorphProgress.ShouldBe(1);
            box.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            mark.FadeValue.ShouldBe(0);
            mark.FillScale.ShouldBe(0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morph_Points_Share_Three_Points()
    {
        CheckboxPathMorph.PointAt(0, 0).ShouldBe(new Avalonia.Point(4.25, 9.25));
        CheckboxPathMorph.PointAt(2, 1).ShouldBe(new Avalonia.Point(13.25, 9));
        CheckboxPathMorph.PointAt(1, 0.5).X.ShouldBe(8.125, 0.0001);
    }

    [AvaloniaFact]
    public void Description_Is_Help_Text_And_Unlabeled_Box_Has_Fallback_Name()
    {
        var box = new Checkbox { Description = "We will email you." };
        AutomationProperties.GetHelpText(box).ShouldBe("We will email you.");
        AutomationProperties.GetName(box).ShouldBe("Checkbox");
        box.Content = "Subscribe";
        AutomationProperties.GetName(box).ShouldBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Hit_Area_Is_44_And_Square_Is_18()
    {
        var box = new Checkbox { Content = "Terms", IsInvalid = true };
        var window = TestHelpers.Show(box);
        try
        {
            box.Classes.Contains(":invalid").ShouldBeTrue();
            box.Bounds.Height.ShouldBe(44);
            TestHelpers.Descendant<CheckboxMark>(box).Bounds.Width.ShouldBe(18);
        }
        finally
        {
            window.Close();
        }
    }
}

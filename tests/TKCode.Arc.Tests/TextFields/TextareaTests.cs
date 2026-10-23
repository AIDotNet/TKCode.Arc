using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

public class TextareaTests
{
    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var textarea = new Textarea { Label = "Bio", Width = 360 };
        var window = TestHelpers.Show(textarea);
        try
        {
            textarea.AcceptsReturn.ShouldBeTrue();
            textarea.TextWrapping.ShouldBe(Avalonia.Media.TextWrapping.Wrap);
            var frame = textarea.GetVisualDescendants().OfType<FieldFrame>().First();
            frame.Bounds.Height.ShouldBe(110, 0.5);
            frame.Padding.Top.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Minimum_Height_Holds_At_Every_Size()
    {
        var textarea = new Textarea { Label = "Bio", Size = ControlSize.Small, Width = 360 };
        var window = TestHelpers.Show(textarea);
        try
        {
            textarea.GetVisualDescendants().OfType<FieldFrame>().First().Bounds.Height.ShouldBe(110, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Adds_A_Line()
    {
        var textarea = new Textarea { Label = "Notes", Width = 360 };
        var window = TestHelpers.Show(textarea);
        try
        {
            textarea.Focus();
            window.KeyTextInput("one");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyTextInput("two");
            Dispatcher.UIThread.RunJobs();
            textarea.Text!.Replace("\r\n", "\n").ShouldBe("one\ntwo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frame_Height_Follows_The_Grip_But_Not_Below_The_Minimum()
    {
        var textarea = new Textarea { Label = "Notes", Width = 360 };
        var window = TestHelpers.Show(textarea);
        try
        {
            var frame = textarea.GetVisualDescendants().OfType<FieldFrame>().First();
            textarea.FrameHeight = 180;
            Dispatcher.UIThread.RunJobs();
            frame.Bounds.Height.ShouldBe(180, 0.5);
            textarea.FrameHeight = 60;
            Dispatcher.UIThread.RunJobs();
            frame.Bounds.Height.ShouldBe(110, 0.5);
            textarea.GetVisualDescendants().OfType<Control>().First(c => c.Name == Textarea.ResizeGripPart).IsVisible.ShouldBeTrue();
            textarea.IsResizable = false;
            Dispatcher.UIThread.RunJobs();
            textarea.GetVisualDescendants().OfType<Control>().First(c => c.Name == Textarea.ResizeGripPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Darkens_The_Border_And_Draws_No_Halo()
    {
        var textarea = new Textarea { Label = "Notes", Width = 360 };
        var window = TestHelpers.Show(textarea);
        try
        {
            var frame = textarea.GetVisualDescendants().OfType<FieldFrame>().First();
            textarea.Focus();
            Dispatcher.UIThread.RunJobs();
            frame.Classes.ShouldContain(FieldFrame.FocusActivePseudoClass);
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(frame, DesignTokens.ArcDesignTokenKind.Foreground));
            // Arc's 3px halo is a transparent focus-ring colour: focus is the border colour alone.
            frame.GetVisualDescendants().OfType<Border>().ShouldNotContain(b => b.Name == "PART_FocusRing");
        }
        finally
        {
            window.Close();
        }
    }
}

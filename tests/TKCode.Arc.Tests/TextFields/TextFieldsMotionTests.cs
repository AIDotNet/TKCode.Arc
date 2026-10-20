using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

/// <summary>Runs the text-field animations at full motion on a manual frame clock.</summary>
public class TextFieldsMotionTests
{
    private static void Run(ManualFrameClock clock, int milliseconds)
    {
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Message_Row_Opens_Rewrites_Rolls_And_Closes()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var message = new FieldMessage { Width = 300, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(message, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            message.Text = "120 characters left";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 60);
            var opening = message.Bounds.Height;
            Run(clock, 1200);
            message.Bounds.Height.ShouldBeGreaterThan(opening);
            var open = message.Bounds.Height;
            open.ShouldBeGreaterThan(20);

            message.Text = "99 characters left";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1200);
            message.LiveWords.ShouldBe(new[] { "99", "characters", "left" });
            message.Text = "Something else entirely";
            Run(clock, 1200);
            message.LiveWords.ShouldBe(new[] { "Something", "else", "entirely" });

            message.Text = null;
            Dispatcher.UIThread.RunJobs();
            Run(clock, 60);
            message.Bounds.Height.ShouldBeGreaterThan(0);
            Run(clock, 1500);
            message.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Password_Toggle_Draws_The_Slash_And_Strength_Shakes_Then_Settles()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var strength = new PasswordStrength { Label = "New password", Width = 380 };
        var window = TestHelpers.Show(strength, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            strength.Text = "Correct-Horse-9";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1500);
            strength.Level.ShouldBe(4);
            strength.GetVisualDescendants().OfType<PasswordRuleMark>().ShouldAllBe(m => m.IsMet);

            strength.IsRevealed = true;
            Dispatcher.UIThread.RunJobs();
            Run(clock, 600);
            strength.GetVisualDescendants().OfType<PasswordEyeGlyph>().First().SlashLength.ShouldBe(1, 0.01);

            strength.ErrorMessage = "Try another one.";
            Dispatcher.UIThread.RunJobs();
            var frame = strength.GetVisualDescendants().OfType<FieldFrame>().First();
            Run(clock, 48);
            var shake = (TranslateTransform)frame.RenderTransform!;
            Math.Abs(shake.X).ShouldBeGreaterThan(0.5);
            Run(clock, 2000);
            Math.Abs(shake.X).ShouldBeLessThan(0.2);

            strength.Text = "abc";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1500);
            strength.Level.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expanding_Search_Morphs_Open_And_Closed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var search = new ExpandingSearch
        {
            Label = "Search",
            ItemsSource = new[] { new ExpandingSearchItem { Title = "Roadmap" }, new ExpandingSearchItem { Title = "Report" } },
            Suggestions = new[] { new ExpandingSearchItem { Title = "Recent doc" } }
        };
        var window = TestHelpers.Show(new Border { Width = 380, Child = search }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var shell = search.GetVisualDescendants().OfType<Panel>().First(p => p.Name == ExpandingSearch.ShellHostPart);
            search.Expand();
            Dispatcher.UIThread.RunJobs();
            Run(clock, 80);
            shell.Bounds.Width.ShouldBeGreaterThan(44);
            shell.Bounds.Width.ShouldBeLessThan(360);
            Run(clock, 1500);
            shell.Bounds.Width.ShouldBe(360, 0.5);
            search.IsPanelOpen.ShouldBeTrue();

            window.KeyTextInput("r");
            Dispatcher.UIThread.RunJobs();
            Run(clock, 300);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Run(clock, 300);
            search.ActiveIndex.ShouldBe(1);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1500);
            shell.Bounds.Width.ShouldBe(44, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Edit_Frame_Follows_The_Draft()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var edit = new InlineEdit { Label = "Name", Value = "Atlas" };
        var window = TestHelpers.Show(new Border { Width = 380, Child = edit }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            edit.BeginEdit(caret: 5);
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1000);
            var box = edit.GetVisualDescendants().OfType<InlineEditBox>().First();
            var before = box.FrameWidth;
            window.KeyTextInput(" redesign and more");
            Dispatcher.UIThread.RunJobs();
            Run(clock, 32);
            box.FrameWidth.ShouldBeGreaterThan(before);
            Run(clock, 1000);
            box.FrameWidth.ShouldBe(box.Bounds.Width, 0.5);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Run(clock, 1000);
            edit.Value.ShouldBe("Atlas redesign and more");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Clear_Button_Pops_In()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var field = new SearchField { Label = "Search", Width = 320 };
        var window = TestHelpers.Show(field, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var presence = field.GetVisualDescendants().OfType<PresenceHost>().First();
            field.Text = "x";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 600);
            presence.Progress.ShouldBe(1, 0.05);
            field.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Run(clock, 600);
            presence.Progress.ShouldBe(0, 0.05);
        }
        finally
        {
            window.Close();
        }
    }
}

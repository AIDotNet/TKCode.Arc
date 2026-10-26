using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class KeyboardTests
{
    private sealed record Setup(Window Window, Page Page, KeyboardInsetSource Source, Border Content);

    private static Setup Create(ArcMotionLevel level, Control? content = null, Control? footer = null, bool scrollable = false)
    {
        var source = new KeyboardInsetSource();
        var body = new Border { Name = "Body" };
        var page = new Page { Content = content ?? body, Footer = footer, IsScrollable = scrollable };
        var window = new Window { Content = page, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        Page.SetKeyboardInsetSource(window, source);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Setup(window, page, source, body);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Keyboard_Shrinks_The_Content_And_Reports_The_Inset_To_Descendants()
    {
        var s = Create(ArcMotionLevel.Off);
        try
        {
            s.Content.Bounds.Height.ShouldBe(600);
            s.Source.Set(300);
            Settle(s.Window);
            s.Page.KeyboardInset.ShouldBe(300);
            Page.GetKeyboardInset(s.Content).ShouldBe(300);
            s.Content.Bounds.Height.ShouldBe(300);

            s.Source.Set(0);
            Settle(s.Window);
            Page.GetKeyboardInset(s.Content).ShouldBe(0);
            s.Content.Bounds.Height.ShouldBe(600);
        }
        finally
        {
            s.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Larger_Than_The_Safe_Area_Replaces_It_And_AvoidKeyboard_Off_Leaves_The_Page_Alone()
    {
        var s = Create(ArcMotionLevel.Off);
        try
        {
            s.Page.UsePlatformSafeArea = false;
            s.Page.SafeArea = new Thickness(0, 40, 0, 34);
            Settle(s.Window);
            s.Content.Bounds.Height.ShouldBe(600 - 40 - 34);
            s.Source.Set(300);
            Settle(s.Window);
            s.Content.Bounds.Height.ShouldBe(600 - 40 - 300);

            s.Page.AvoidKeyboard = false;
            Settle(s.Window);
            s.Content.Bounds.Height.ShouldBe(600 - 40 - 34);
            s.Page.KeyboardInset.ShouldBe(300); // still reported for overlays
        }
        finally
        {
            s.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Footer_Goes_Behind_The_Keyboard()
    {
        var footer = new Border { Height = 50, Name = "Foot" };
        var s = Create(ArcMotionLevel.Off, footer: footer);
        try
        {
            s.Content.Bounds.Height.ShouldBe(550);
            s.Source.Set(300);
            Settle(s.Window);
            // The content ends where the keyboard starts, and the footer is under the keyboard.
            s.Content.Bounds.Height.ShouldBe(300);
            footer.TranslatePoint(default, s.Window)!.Value.Y.ShouldBeGreaterThanOrEqualTo(300 - 0.5);
        }
        finally
        {
            s.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Animates_The_Layout_And_Off_Jumps()
    {
        var s = Create(ArcMotionLevel.Full);
        try
        {
            s.Source.Set(300);
            Dispatcher.UIThread.RunJobs();
            s.Window.UpdateLayout();
            s.Content.Bounds.Height.ShouldBeGreaterThan(300); // not there yet on the first frame
            for (var i = 0; i < 100 && Math.Abs(s.Content.Bounds.Height - 300) > 0.5; i++)
            {
                Thread.Sleep(30);
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Settle(s.Window);
            }
            s.Content.Bounds.Height.ShouldBe(300, 0.5);
        }
        finally
        {
            s.Window.Close();
        }

        var off = Create(ArcMotionLevel.Off);
        try
        {
            off.Source.Set(300);
            Settle(off.Window);
            off.Content.Bounds.Height.ShouldBe(300); // at once
        }
        finally
        {
            off.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Focused_TextField_Stays_Visible_Above_The_Keyboard()
    {
        var field = new TextField { Name = "Field" };
        var stack = new StackPanel { Children = { new Border { Height = 900 }, field } };
        var s = Create(ArcMotionLevel.Off, stack, scrollable: true);
        try
        {
            field.Focus();
            s.Source.Set(300);
            Settle(s.Window);
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            Settle(s.Window);
            var bottom = field.TranslatePoint(new Point(0, field.Bounds.Height), s.Window)!.Value.Y;
            bottom.ShouldBeLessThanOrEqualTo(300 + 0.5);
            bottom.ShouldBeGreaterThan(0);
        }
        finally
        {
            s.Window.Close();
        }
    }
}

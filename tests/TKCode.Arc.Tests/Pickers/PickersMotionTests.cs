using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Pickers;

/// <summary>Runs the pickers' animated paths frame by frame under full motion (ManualFrameClock).</summary>
public class PickersMotionTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static void Run(Window window, ManualFrameClock clock, double seconds)
    {
        var steps = (int)Math.Ceiling(seconds / 0.05);
        for (var i = 0; i < steps; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Pump(window);
        }
    }

    /// <summary>Motion completions hop through the thread pool back to the UI thread; give them a moment.</summary>
    private static void Pump(Window window)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Show(Control content, double width = 900, ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Date_Parts_Roll_And_Settle()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var picker = new DatePicker { Culture = EnUs, SelectedDate = new DateOnly(2026, 10, 20) };
        var window = Show(picker);
        try
        {
            Run(window, clock, 0.2);
            var value = TestHelpers.Descendant<PickerRollText>(picker);
            picker.SelectedDate = new DateOnly(2026, 10, 21);
            Dispatcher.UIThread.RunJobs();
            value.IsRolling.ShouldBeTrue();
            value.Text.ShouldBe("Oct 21, 2026");
            Run(window, clock, 0.6);
            value.IsRolling.ShouldBeFalse();
            picker.SelectedDate = null;
            Run(window, clock, 0.6);
            value.IsPlaceholder.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Color_Panel_Grows_From_The_Swatch_And_Folds_Back()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var picker = new ColorPicker();
        var window = Show(picker);
        try
        {
            Run(window, clock, 0.2);
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = picker.Surface!;
            var trigger = TestHelpers.Descendant<Avalonia.Controls.Border>(picker, "PART_Trigger");
            surface.Shape.Width.ShouldBeLessThan(trigger.Bounds.Width + 1);
            Run(window, clock, 1.2);
            surface.Shape.Width.ShouldBe(picker.Panel!.DesiredSize.Width, 0.5);
            surface.Shape.Radius.ShouldBe(26, 0.1);
            picker.Panel.Body!.Opacity.ShouldBe(1, 0.01);

            picker.Panel.Hue!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            Run(window, clock, 0.6);
            picker.Panel.Hue.ThumbPosition.X.ShouldBe(picker.Panel.Hue.Value.X, 0.001);

            picker.IsDropDownOpen = false;
            Dispatcher.UIThread.RunJobs();
            picker.Popup!.IsOpen.ShouldBeTrue();
            Run(window, clock, 1.2);
            picker.Popup.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Panel_Grows_Slides_Months_And_Shrinks_On_Apply()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var picker = new DateRangePicker { Culture = EnUs, SelectedRange = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3)) };
        var window = Show(picker);
        try
        {
            Run(window, clock, 0.2);
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = picker.Surface!;
            var panel = picker.Panel!;
            Run(window, clock, 1.5);
            surface.Shape.Width.ShouldBe(panel.DesiredSize.Width, 0.5);
            panel.Body!.Opacity.ShouldBe(1, 0.01);
            var flyer = picker.Flyer!;
            flyer.Text.ShouldBe("Oct1–3,2026");

            panel.NextButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            panel.View.ShouldBe(new DateOnly(2026, 10, 1));
            Run(window, clock, 0.8);
            panel.Months!.Panes[0].Month.ShouldBe(new DateOnly(2026, 10, 1));

            panel.Pick(new DateOnly(2026, 10, 12));
            panel.Pick(new DateOnly(2026, 11, 4));
            Run(window, clock, 0.6);
            panel.ApplyButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            picker.SelectedRange.ShouldBe(new DateRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 11, 4)));
            picker.Popup!.IsOpen.ShouldBeTrue();
            Run(window, clock, 1.5);
            picker.Popup.IsOpen.ShouldBeFalse();
            picker.FormattedValue.ShouldBe("Oct 12 – Nov 4, 2026");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Time_List_Glides_To_Keep_The_Highlight_In_View()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var picker = new TimePicker { Culture = EnUs, MinuteStep = 5, SelectedTime = new TimeOnly(12, 0), Width = 240 };
        var window = Show(picker);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Run(window, clock, 0.5);
            var scroll = picker.Panel!.Scroll!;
            var list = picker.List!;
            for (var i = 0; i < 8; i++)
            {
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            }
            Run(window, clock, 1);
            var row = list.RowRect(list.ActiveIndex);
            row.Bottom.ShouldBeLessThanOrEqualTo(scroll.Offset.Y + scroll.Viewport.Height + 0.5);
            row.Top.ShouldBeGreaterThanOrEqualTo(scroll.Offset.Y - 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Instead_Of_Morphing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var color = new ColorPicker();
        var range = new DateRangePicker { Culture = EnUs };
        var window = Show(new StackPanel { Spacing = 20, Children = { color, range } }, 900, ArcMotionLevel.Reduced);
        try
        {
            Run(window, clock, 0.2);
            color.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            color.Surface!.Shape.Width.ShouldBe(color.Panel!.DesiredSize.Width, 0.5);
            Run(window, clock, 0.4);
            color.Surface.Opacity.ShouldBe(1, 0.01);
            color.IsDropDownOpen = false;
            Run(window, clock, 0.5);
            color.Popup!.IsOpen.ShouldBeFalse();

            range.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            range.Surface!.Shape.Width.ShouldBe(range.Panel!.DesiredSize.Width, 0.5);
            range.Panel.Pick(range.Panel.Today);
            range.Panel.ApplyButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Run(window, clock, 0.5);
            range.Popup!.IsOpen.ShouldBeFalse();
            range.SelectedRange.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}

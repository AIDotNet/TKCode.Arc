using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Pickers;

public class DateRangePickerTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Window Show(Control content, double width = 900)
    {
        var window = TestHelpers.Show(content);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static DateRangePickerPanel Open(DateRangePicker picker)
    {
        picker.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();
        return picker.Panel!;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var picker = new DateRangePicker();
        var window = Show(picker);
        try
        {
            picker.Label.ShouldBe("Date range");
            picker.Placeholder.ShouldBe("Select dates");
            picker.SelectedRange.ShouldBeNull();
            picker.FirstDayOfWeek.ShouldBe(DayOfWeek.Sunday);
            picker.MonthCount.ShouldBe(DateRangePickerMonths.Auto);
            DateRangePresets.Default.Select(p => p.Label).ShouldBe(
                ["Today", "Yesterday", "Last 7 days", "Last 30 days", "This month", "Last month", "This quarter", "Year to date"]);
            var value = TestHelpers.Descendant<PickerRollText>(picker);
            value.IsPlaceholder.ShouldBeTrue();
            value.Text.ShouldBe("Selectdates");
            TestHelpers.Descendant<Border>(picker, "PART_Trigger").Bounds.Height.ShouldBe(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wide_Boundary_Shows_Two_Months_And_Narrow_One()
    {
        var wide = new DateRangePicker { Culture = EnUs };
        var window = Show(wide, 900);
        try
        {
            var panel = Open(wide);
            panel.IsCompact.ShouldBeFalse();
            panel.Months!.Panes.Count.ShouldBe(2);
            panel.PresetButtons.Count.ShouldBe(8);
            wide.IsDropDownOpen = false;
        }
        finally
        {
            window.Close();
        }

        var narrow = new DateRangePicker { Culture = EnUs };
        window = Show(narrow, 500);
        try
        {
            var panel = Open(narrow);
            panel.IsCompact.ShouldBeTrue();
            panel.Months!.Panes.Count.ShouldBe(1);
            panel.Months.CellSize.ShouldBeInRange(32, 42);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_Is_A_Draft_Until_Apply()
    {
        var applied = new List<DateRange?>();
        var start = new DateOnly(2026, 10, 1);
        var picker = new DateRangePicker { Culture = EnUs, SelectedRange = new DateRange(start, start.AddDays(2)) };
        picker.SelectedRangeChanged += (_, e) => applied.Add(e.NewRange);
        var window = Show(picker);
        try
        {
            var panel = Open(picker);
            panel.View.ShouldBe(new DateOnly(2026, 9, 1)); // the range's month lands on the right
            panel.Pick(new DateOnly(2026, 10, 20));
            panel.Anchor.ShouldBe(new DateOnly(2026, 10, 20));
            panel.Shown.ShouldBe(new DateRange(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20)));
            ((IDateRangePickerMonthHost)panel).OnDayHovered(new DateOnly(2026, 10, 12));
            panel.Shown.ShouldBe(new DateRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 20)));
            panel.Pick(new DateOnly(2026, 10, 12));
            panel.Draft.ShouldBe(new DateRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 20)));
            picker.SelectedRange.ShouldBe(new DateRange(start, start.AddDays(2)));
            picker.Flyer!.Text.ShouldBe("Oct12–20,2026");

            panel.CancelButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedRange.ShouldBe(new DateRange(start, start.AddDays(2)));

            panel = Open(picker);
            panel.Draft.ShouldBe(new DateRange(start, start.AddDays(2)));
            panel.Pick(new DateOnly(2026, 10, 5));
            panel.Pick(new DateOnly(2026, 10, 9));
            panel.ApplyButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedRange.ShouldBe(new DateRange(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9)));
            applied.ShouldBe([new DateRange(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9))]);
            picker.FormattedValue.ShouldBe("Oct 5 – 9, 2026");
            window.FocusManager!.GetFocusedElement().ShouldBe(picker);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Days_Extends_The_Preview_And_Pages_Months()
    {
        var picker = new DateRangePicker { Culture = EnUs, SelectedRange = new DateRange(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14)) };
        var window = Show(picker);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var panel = picker.Panel!;
            picker.IsDropDownOpen.ShouldBeTrue();
            var focused = window.FocusManager!.GetFocusedElement().ShouldBeOfType<DateRangePickerDayCell>();
            focused.Date.ShouldBe(new DateOnly(2026, 10, 14));

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None); // anchor
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            panel.Shown.ShouldBe(new DateRange(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 22)));
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            panel.Draft.ShouldBe(new DateRange(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 22)));

            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeOfType<DateRangePickerDayCell>().Date.ShouldBe(new DateOnly(2026, 11, 22));
            panel.View.ShouldBe(new DateOnly(2026, 10, 1));
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            panel.View.ShouldBe(new DateOnly(2026, 11, 1));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeOfType<DateRangePickerDayCell>().Date.ShouldBe(new DateOnly(2026, 12, 20));

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedRange.ShouldBe(new DateRange(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 14)));
            window.FocusManager!.GetFocusedElement().ShouldBe(picker);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Presets_Set_The_Draft_And_Highlight_Themselves()
    {
        var picker = new DateRangePicker { Culture = EnUs };
        var window = Show(picker);
        try
        {
            var panel = Open(picker);
            var today = panel.Today;
            panel.ActivePreset.ShouldBe(-1);
            panel.PresetButtons[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            panel.Draft.ShouldBe(new DateRange(today.AddDays(-6), today));
            panel.ActivePreset.ShouldBe(2);
            panel.PresetButtons[2].Classes.Contains(":active").ShouldBeTrue();
            panel.ApplyButton!.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Limits_Disable_Days_And_Month_Buttons()
    {
        var picker = new DateRangePicker
        {
            Culture = EnUs,
            MinDate = new DateOnly(2026, 9, 10),
            MaxDate = new DateOnly(2026, 10, 25),
            SelectedRange = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3))
        };
        var window = Show(picker);
        try
        {
            var panel = Open(picker);
            panel.PreviousButton!.IsEnabled.ShouldBeFalse();
            panel.NextButton!.IsEnabled.ShouldBeFalse();
            panel.Months!.FindCell(new DateOnly(2026, 9, 9))!.IsDisabled.ShouldBeTrue();
            panel.Months.FindCell(new DateOnly(2026, 10, 26))!.IsDisabled.ShouldBeTrue();
            panel.Months.FindCell(new DateOnly(2026, 10, 2))!.IsInRange.ShouldBeTrue();
            panel.Months.FindCell(new DateOnly(2026, 10, 1))!.IsEdge.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_Outside_Discards_The_Draft()
    {
        var other = new Avalonia.Controls.Button { Content = "Other", Margin = new Thickness(0, 620, 0, 0) };
        var picker = new DateRangePicker { Culture = EnUs };
        var window = Show(new Panel { Children = { picker, other } });
        try
        {
            var panel = Open(picker);
            panel.Pick(panel.Today);
            var point = other.TranslatePoint(new Point(5, 5), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.Popup!.IsOpen.ShouldBeFalse();
            picker.SelectedRange.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Grows_From_The_Trigger_Away_From_The_Window_Corner()
    {
        var picker = new DateRangePicker { Culture = EnUs, Margin = new Thickness(160, 120, 0, 0) };
        var window = Show(picker);
        try
        {
            Open(picker);
            var surface = picker.Surface!;
            var trigger = TestHelpers.Descendant<Border>(picker, "PART_Trigger").TranslatePoint(default, window)!.Value;
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Near_The_Bottom_The_Page_Scrolls_And_The_Panel_Still_Grows_From_The_Trigger()
    {
        var picker = new DateRangePicker { Culture = EnUs };
        var page = new StackPanel { Children = { new Border { Height = 600 }, picker, new Border { Height = 900 } } };
        var viewer = new ScrollViewer { Content = page };
        var window = Show(viewer);
        try
        {
            Open(picker);
            viewer.Offset.Y.ShouldBeGreaterThan(0);
            var surface = picker.Surface!;
            surface.Shape.Y.ShouldBe(0);
            var trigger = TestHelpers.Descendant<Border>(picker, "PART_Trigger").TranslatePoint(default, window)!.Value;
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
            var bottom = surface.TranslatePoint(surface.ShapeRect.BottomLeft, window)!.Value.Y;
            bottom.ShouldBe(window.ClientSize.Height - PickerMorphPlacement.Edge, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Room_To_Scroll_The_Panel_Rises_From_The_Trigger()
    {
        var picker = new DateRangePicker { Culture = EnUs, Margin = new Thickness(40, 600, 0, 0) };
        var window = Show(picker);
        try
        {
            Open(picker);
            var surface = picker.Surface!;
            surface.Shape.Y.ShouldBeLessThan(0);
            var trigger = TestHelpers.Descendant<Border>(picker, "PART_Trigger").TranslatePoint(default, window)!.Value;
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
            var bottom = surface.TranslatePoint(surface.ShapeRect.BottomLeft, window)!.Value.Y;
            bottom.ShouldBe(window.ClientSize.Height - PickerMorphPlacement.Edge, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}

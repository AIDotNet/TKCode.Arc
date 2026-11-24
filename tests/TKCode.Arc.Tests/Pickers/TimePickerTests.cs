using Avalonia;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Pickers;

public class TimePickerTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 480;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var picker = new TimePicker { Label = "Start", Culture = EnUs };
        var window = Show(picker);
        try
        {
            picker.SelectedTime.ShouldBe(new TimeOnly(9, 0));
            picker.MinuteStep.ShouldBe(15);
            picker.OptionCount.ShouldBe(96);
            picker.IsTwelveHour.ShouldBeTrue();
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("9:00 AM");
            picker.FormatTime(new TimeOnly(0, 30)).ShouldBe("12:30 AM");
            picker.FormatTime(new TimeOnly(13, 5)).ShouldBe("1:05 PM");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clock_Follows_The_Culture()
    {
        var picker = new TimePicker { Culture = CultureInfo.GetCultureInfo("de-DE"), SelectedTime = new TimeOnly(14, 30) };
        var window = Show(picker);
        try
        {
            picker.IsTwelveHour.ShouldBeFalse();
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("14:30");
            picker.ClockFormat = TimePickerClockFormat.TwelveHour;
            picker.IsTwelveHour.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_Moves_Wraps_And_Chooses()
    {
        var changes = new List<TimeOnly?>();
        var picker = new TimePicker { Culture = EnUs, MinuteStep = 30, SelectedTime = new TimeOnly(0, 0) };
        picker.SelectedTimeChanged += (_, e) => changes.Add(e.NewTime);
        var window = Show(picker);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            picker.List!.Count.ShouldBe(48);
            picker.List.ActiveIndex.ShouldBe(0);
            picker.List.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            picker.List.ActiveIndex.ShouldBe(47);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            picker.List.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 30));
            picker.IsDropDownOpen.ShouldBeFalse();
            changes.ShouldBe([new TimeOnly(0, 30)]);

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            picker.IsDropDownOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 30));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Centres_The_Chosen_Time()
    {
        var picker = new TimePicker { Culture = EnUs, SelectedTime = new TimeOnly(12, 0) };
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var scroll = picker.Panel!.Scroll!;
            var list = picker.List!;
            list.ActiveIndex.ShouldBe(48);
            var row = list.RowRect(48);
            var middle = scroll.Offset.Y + scroll.Viewport.Height / 2;
            Math.Abs(row.Center.Y - middle).ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    // ---------------------------------------------------------------- #194: seconds and columns

    [AvaloniaFact]
    public void Second_Precision_Shows_Seconds_And_Opens_Columns()
    {
        var picker = new TimePicker
        {
            Culture = EnUs,
            Precision = TimePickerPrecision.Second,
            MinuteStep = 1,
            SelectedTime = new TimeOnly(13, 5, 42)
        };
        var window = Show(picker);
        try
        {
            picker.SecondStep.ShouldBe(1);
            picker.DropDownLayout.ShouldBe(TimePickerDropDownLayout.Auto);
            picker.UsesColumns.ShouldBeTrue();
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("1:05:42 PM");
            picker.FormatTime(new TimeOnly(0, 0, 7)).ShouldBe("12:00:07 AM");
            picker.ClockFormat = TimePickerClockFormat.TwentyFourHour;
            picker.FormatTime(new TimeOnly(9, 3, 7)).ShouldBe("09:03:07");

            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            var panel = picker.Panel!;
            panel.Scroll!.IsVisible.ShouldBeFalse();
            var columns = panel.VisibleColumns;
            columns.Select(c => c.Kind).ShouldBe([TimePickerColumnKind.Hour, TimePickerColumnKind.Minute, TimePickerColumnKind.Second]);
            columns[0].List.Count.ShouldBe(24);
            columns[1].List.Count.ShouldBe(60);
            columns[2].List.Count.ShouldBe(60);
            columns[0].List.SelectedIndex.ShouldBe(13);
            columns[1].List.SelectedIndex.ShouldBe(5);
            columns[2].List.SelectedIndex.ShouldBe(42);
            columns[2].List.LabelAt(42).ShouldBe("42");
            // The columns share the panel's width and every column centres its chosen value.
            columns[0].Scroll.Bounds.Width.ShouldBeGreaterThan(40);
            var row = columns[2].List.RowRect(42);
            var middle = columns[2].Scroll.Offset.Y + columns[2].Scroll.Viewport.Height / 2;
            Math.Abs(row.Center.Y - middle).ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Default_Stays_A_Minute_List()
    {
        var picker = new TimePicker { Culture = EnUs };
        var window = Show(picker);
        try
        {
            picker.Precision.ShouldBe(TimePickerPrecision.Minute);
            picker.UsesColumns.ShouldBeFalse();
            picker.SelectedTime = new TimeOnly(9, 0, 30);
            TestHelpers.Descendant<PickerRollText>(picker).Text.ShouldBe("9:00 AM");
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            picker.Panel!.Scroll!.IsVisible.ShouldBeTrue();
            picker.Panel.VisibleColumns.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Layout_Shortens_A_One_Minute_List_And_Adds_Meridiem()
    {
        var picker = new TimePicker
        {
            Culture = EnUs,
            MinuteStep = 1,
            DropDownLayout = TimePickerDropDownLayout.Columns,
            SelectedTime = new TimeOnly(18, 45, 30)
        };
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            var columns = picker.Panel!.VisibleColumns;
            columns.Select(c => c.Kind).ShouldBe([TimePickerColumnKind.Hour, TimePickerColumnKind.Minute, TimePickerColumnKind.Meridiem]);
            columns[0].List.Count.ShouldBe(12);
            columns[0].List.LabelAt(0).ShouldBe("12");
            columns[0].List.SelectedIndex.ShouldBe(6);
            columns[2].List.SelectedIndex.ShouldBe(1);
            columns[2].List.LabelAt(1).ShouldBe("PM");
            // The panel is at least as wide as the trigger, and every column fits its widest value with the dot clear of it.
            var frame = TestHelpers.Descendant<FieldFrame>(picker, TimePicker.FramePart);
            picker.Panel.Bounds.Width.ShouldBeGreaterThanOrEqualTo(frame.Bounds.Width - 0.5);
            columns.Select(c => c.Scroll.Bounds.Width).Distinct().Count().ShouldBe(1);

            // A click on a cell applies that part at once and keeps the list open; minute precision drops the seconds.
            picker.ChooseColumnCell(TimePickerColumnKind.Meridiem, 0);
            picker.SelectedTime.ShouldBe(new TimeOnly(6, 45));
            picker.IsDropDownOpen.ShouldBeTrue();
            picker.ChooseColumnCell(TimePickerColumnKind.Hour, 0);
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 45));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Column_Keyboard_Moves_Between_Columns_Steps_Types_And_Closes()
    {
        var changes = new List<TimeOnly?>();
        var picker = new TimePicker
        {
            Culture = EnUs,
            ClockFormat = TimePickerClockFormat.TwentyFourHour,
            Precision = TimePickerPrecision.Second,
            MinuteStep = 5,
            SecondStep = 15,
            SelectedTime = new TimeOnly(23, 55, 0)
        };
        var window = Show(picker);
        try
        {
            picker.SelectedTimeChanged += (_, e) => changes.Add(e.NewTime);
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            var columns = picker.Panel!.VisibleColumns;
            columns[1].List.Count.ShouldBe(12);
            columns[2].List.Count.ShouldBe(4);
            picker.ActiveColumn.ShouldBe(TimePickerColumnKind.Hour);
            columns[0].List.ActiveIndex.ShouldBe(23);

            // Down in the hour column wraps 23 -> 0 and applies it at once.
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 55, 0));

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            picker.ActiveColumn.ShouldBe(TimePickerColumnKind.Second);
            columns[0].List.ActiveIndex.ShouldBe(-1);
            columns[2].List.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 55, 45));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 55, 0));

            // Typing digits jumps the active column to the matching value.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            picker.ActiveColumn.ShouldBe(TimePickerColumnKind.Minute);
            window.KeyTextInput("3");
            window.KeyTextInput("5");
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 35, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyTextInput("3");
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 35, 30));

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.SelectedTime.ShouldBe(new TimeOnly(0, 35, 30));
            changes.Last().ShouldBe(new TimeOnly(0, 35, 30));
        }
        finally
        {
            window.Close();
        }
    }

    // ---------------------------------------------------------------- #195: prefix

    [AvaloniaFact]
    public void Prefix_Defaults_To_A_Clock_Per_Instance_And_Can_Be_Hidden_Or_Replaced()
    {
        var first = new TimePicker { Culture = EnUs };
        var second = new TimePicker { Culture = EnUs };
        var hidden = new TimePicker { Culture = EnUs, Prefix = null };
        var custom = new TimePicker { Culture = EnUs, Prefix = "At" };
        var window = Show(new StackPanel { Children = { first, second, hidden, custom } });
        try
        {
            var a = first.Prefix.ShouldBeOfType<ArcGlyph>();
            var b = second.Prefix.ShouldBeOfType<ArcGlyph>();
            a.ShouldNotBeSameAs(b);
            a.Data.ShouldBeSameAs(ArcIcons.Clock3);
            a.IsEffectivelyVisible.ShouldBeTrue();

            var frame = TestHelpers.Descendant<FieldFrame>(hidden, TimePicker.FramePart);
            frame.Prefix.ShouldBeNull();
            TestHelpers.Descendant<ContentPresenter>(frame, FieldFrame.PrefixPart).IsVisible.ShouldBeFalse();
            // The value starts where the frame's padding ends: no icon and no gap.
            var withIcon = TestHelpers.Descendant<PickerRollText>(first).TranslatePoint(default, first)!.Value.X;
            var without = TestHelpers.Descendant<PickerRollText>(hidden).TranslatePoint(default, hidden)!.Value.X;
            (withIcon - without).ShouldBe(16 + 9, 0.5);

            TestHelpers.Descendant<FieldFrame>(custom, TimePicker.FramePart).Prefix.ShouldBe("At");
        }
        finally
        {
            window.Close();
        }
    }
}

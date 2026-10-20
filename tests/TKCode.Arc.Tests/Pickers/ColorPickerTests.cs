using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Pickers;

public class ColorPickerTests
{
    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 640;
        window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ColorPicker Open(ColorPicker picker)
    {
        picker.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();
        return picker;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var picker = new ColorPicker();
        var window = Show(picker);
        try
        {
            picker.Color.ShouldBe(Color.Parse("#2F6BFF"));
            picker.HexValue.ShouldBe("#2F6BFF");
            picker.Label.ShouldBe("Color");
            picker.Format.ShouldBe(ColorPickerFormat.Hex);
            picker.MaxSwatches.ShouldBe(7);
            picker.ContrastBackground.ShouldBe(Colors.White);
            TestHelpers.Descendant<Border>(picker, "PART_Trigger").Bounds.Height.ShouldBe(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formats_And_Parses_Like_Arc()
    {
        var hsva = ColorPickerMath.Parse("#2F6BFF")!.Value;
        ColorPickerMath.Format(hsva, ColorPickerFormat.Hex).ShouldBe("#2F6BFF");
        ColorPickerMath.Format(hsva, ColorPickerFormat.Rgb).ShouldBe("rgb(47 107 255)");
        ColorPickerMath.Format(hsva, ColorPickerFormat.Hsl).ShouldBe("hsl(223 100% 59%)");
        ColorPickerMath.Format(hsva, ColorPickerFormat.Oklch).ShouldStartWith("oklch(");
        var translucent = hsva with { A = 0.5 };
        ColorPickerMath.ToHex(translucent).ShouldBe("#2F6BFF80");
        ColorPickerMath.Format(translucent, ColorPickerFormat.Rgb).ShouldBe("rgb(47 107 255 / 50%)");

        ColorPickerMath.ToHex(ColorPickerMath.Parse("rgb(255, 0, 0)")!.Value).ShouldBe("#FF0000");
        ColorPickerMath.ToHex(ColorPickerMath.Parse("rgba(255 0 0 / 50%)")!.Value).ShouldBe("#FF000080");
        ColorPickerMath.ToHex(ColorPickerMath.Parse("hsl(120deg, 100%, 50%)")!.Value).ShouldBe("#00FF00");
        ColorPickerMath.ToHex(ColorPickerMath.Parse("#abc")!.Value).ShouldBe("#AABBCC");
        ColorPickerMath.ToHex(ColorPickerMath.Parse("oklch(62.8% 0.258 29.2)")!.Value).ShouldBe("#FF0000");
        ColorPickerMath.Parse("not a color").ShouldBeNull();
        ColorPickerMath.Parse("rgb(-1 0 0)").ShouldBeNull();

        // Hue survives a trip through gray.
        var gray = ColorPickerMath.Parse("#808080", 200)!.Value;
        gray.H.ShouldBe(200);

        ColorPickerMath.Level(ColorPickerMath.Contrast(ColorPickerMath.Parse("#000")!.Value, new ColorPickerRgba(1, 1, 1, 1))).ShouldBe("AAA");
        ColorPickerMath.Level(4.5).ShouldBe("AA");
        ColorPickerMath.Level(3).ShouldBe("AA large");
        ColorPickerMath.Level(2.9).ShouldBe("Fails");
    }

    [AvaloniaFact]
    public void Typed_Values_Apply_On_Enter_And_Invalid_Text_Is_Flagged()
    {
        var hexes = new List<string>();
        var picker = new ColorPicker();
        picker.ColorChanged += (_, e) => hexes.Add(e.Hex);
        var window = Show(picker);
        try
        {
            Open(picker);
            var input = picker.Panel!.Input!;
            input.Text.ShouldBe("#2F6BFF");
            input.Focus();
            input.Text = "rgb(255 0 0 / 50%)";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.HexValue.ShouldBe("#FF000080");
            picker.Color.ShouldBe(Color.FromArgb(128, 255, 0, 0));
            hexes.ShouldBe(["#FF000080"]);

            input.Text = "banana";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            picker.IsInputInvalid.ShouldBeTrue();
            picker.Panel.Error!.IsVisible.ShouldBeTrue();
            picker.HexValue.ShouldBe("#FF000080");

            // Escape drops the draft first, then closes the panel.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            picker.IsInputInvalid.ShouldBeFalse();
            input.Text.ShouldBe("#FF000080");
            picker.IsDropDownOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBe(picker);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sliders_And_Area_Follow_The_Keyboard()
    {
        var picker = new ColorPicker { Color = Color.Parse("#FF0000") };
        var window = Show(picker);
        try
        {
            Open(picker);
            var hue = picker.Panel!.Hue!;
            hue.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            picker.Hsva.H.ShouldBe(1, 0.001);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            picker.Hsva.H.ShouldBe(11, 0.001);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            picker.Hsva.H.ShouldBe(359.9, 0.001);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            picker.Hsva.H.ShouldBe(0, 0.001);

            var alpha = picker.Panel.Alpha!;
            alpha.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            picker.Hsva.A.ShouldBe(0.9, 0.001);
            picker.HexValue.ShouldBe("#FF0000E6");

            var area = picker.Panel.Area!;
            area.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
            picker.Hsva.V.ShouldBe(0.9, 0.001);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            picker.Hsva.S.ShouldBe(0.99, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Area_Picks_Saturation_And_Brightness()
    {
        var picker = new ColorPicker { Color = Color.Parse("#FF0000") };
        var window = Show(picker);
        try
        {
            Open(picker);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var area = picker.Panel!.Area!;
            var origin = area.TranslatePoint(default, window)!.Value;
            var size = area.Bounds.Size;
            window.MouseDown(origin + new Point(size.Width * 0.5, size.Height * 0.25), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            picker.Hsva.S.ShouldBe(0.5, 0.02);
            picker.Hsva.V.ShouldBe(0.75, 0.02);
            window.MouseMove(origin + new Point(size.Width + 40, -30), RawInputModifiers.LeftMouseButton);
            window.MouseUp(origin + new Point(size.Width + 40, -30), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            picker.Hsva.S.ShouldBe(1, 0.001);
            picker.Hsva.V.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Format_Button_Cycles_The_Field()
    {
        var picker = new ColorPicker();
        var window = Show(picker);
        try
        {
            Open(picker);
            var format = picker.Panel!.FormatButton!;
            format.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            picker.Format.ShouldBe(ColorPickerFormat.Rgb);
            picker.Panel.Input!.Text.ShouldBe("rgb(47 107 255)");
            picker.Panel.FormatLabel!.Text.ShouldBe("RGB");
            format.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            format.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            format.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            picker.Format.ShouldBe(ColorPickerFormat.Hex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swatches_Save_Reorder_And_Delete()
    {
        var picker = new ColorPicker { MaxSwatches = 2 };
        var window = Show(picker);
        try
        {
            Open(picker);
            var add = picker.Panel!.AddButton!;
            add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            picker.Color = Color.Parse("#00FF00");
            add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            picker.Color = Color.Parse("#0000FF");
            add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            picker.Swatches!.Select(s => s.Color).ShouldBe([Color.Parse("#0000FF"), Color.Parse("#00FF00")]);

            var row = picker.Panel.Swatches!;
            row.FocusSwatch(picker.Swatches![0].Id).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            Dispatcher.UIThread.RunJobs();
            picker.Swatches!.Select(s => s.Color).ShouldBe([Color.Parse("#00FF00"), Color.Parse("#0000FF")]);

            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.Swatches!.Select(s => s.Color).ShouldBe([Color.Parse("#00FF00")]);

            // Applying a swatch sets the color.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.HexValue.ShouldBe("#00FF00");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_And_A_Press_Outside_Closes()
    {
        var other = new Avalonia.Controls.Button { Content = "Other", Margin = new Thickness(0, 600, 0, 0) };
        var picker = new ColorPicker();
        var root = new Panel { Children = { picker, other } };
        var window = Show(root);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeTrue();
            picker.Popup!.IsOpen.ShouldBeTrue();
            var point = other.TranslatePoint(new Point(5, 5), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen.ShouldBeFalse();
            picker.Popup.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Grows_From_The_Trigger_Away_From_The_Window_Corner()
    {
        var picker = new ColorPicker { Margin = new Thickness(160, 120, 0, 0) };
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

    private static Point TriggerOrigin(ColorPicker picker, Window window) =>
        TestHelpers.Descendant<Border>(picker, "PART_Trigger").TranslatePoint(default, window)!.Value;

    private static Rect ShapeInWindow(ColorPicker picker, Window window)
    {
        var surface = picker.Surface!;
        var rect = surface.ShapeRect;
        return new Rect(surface.TranslatePoint(rect.Position, window)!.Value, rect.Size);
    }

    [AvaloniaFact]
    public void Near_The_Bottom_The_Page_Scrolls_And_The_Panel_Still_Grows_From_The_Trigger()
    {
        var picker = new ColorPicker();
        var page = new StackPanel { Children = { new Border { Height = 600 }, picker, new Border { Height = 900 } } };
        var viewer = new ScrollViewer { Content = page };
        var window = Show(viewer);
        try
        {
            Open(picker);
            viewer.Offset.Y.ShouldBeGreaterThan(0);
            var surface = picker.Surface!;
            surface.Shape.Y.ShouldBe(0);
            var trigger = TriggerOrigin(picker, window);
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
            ShapeInWindow(picker, window).Bottom.ShouldBe(window.ClientSize.Height - PickerMorphPlacement.Edge, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Room_To_Scroll_The_Panel_Rises_From_The_Trigger()
    {
        var picker = new ColorPicker { Margin = new Thickness(40, 600, 0, 0) };
        var window = Show(picker);
        try
        {
            Open(picker);
            var surface = picker.Surface!;
            surface.Shape.Y.ShouldBeLessThan(0);
            var trigger = TriggerOrigin(picker, window);
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
            var shape = ShapeInWindow(picker, window);
            shape.Bottom.ShouldBe(window.ClientSize.Height - PickerMorphPlacement.Edge, 0.5);
            shape.Top.ShouldBeGreaterThanOrEqualTo(PickerMorphPlacement.Edge - 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Open_Panel_Follows_The_Trigger_When_The_Page_Scrolls()
    {
        var picker = new ColorPicker();
        var page = new StackPanel { Children = { new Border { Height = 550 }, picker, new Border { Height = 900 } } };
        var viewer = new ScrollViewer { Content = page, Offset = new Vector(0, 300) };
        var window = Show(viewer);
        try
        {
            Open(picker);
            // Scrolling back carries the panel past the window's bottom edge: it stays on the trigger, never slides.
            viewer.Offset = new Vector(0, 100);
            Dispatcher.UIThread.RunJobs();
            var surface = picker.Surface!;
            var trigger = TriggerOrigin(picker, window);
            trigger.Y.ShouldBe(450, 0.5);
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
    public void Near_The_Right_Edge_The_Panel_Shifts_Left_From_The_Trigger()
    {
        var picker = new ColorPicker { Margin = new Thickness(520, 40, 0, 0) };
        var window = Show(picker);
        try
        {
            Open(picker);
            var surface = picker.Surface!;
            surface.Shape.X.ShouldBeLessThan(0);
            var trigger = TriggerOrigin(picker, window);
            var origin = surface.TranslatePoint(surface.Origin, window)!.Value;
            origin.X.ShouldBe(trigger.X, 0.5);
            origin.Y.ShouldBe(trigger.Y, 0.5);
            ShapeInWindow(picker, window).Right.ShouldBe(window.ClientSize.Width - PickerMorphPlacement.Edge, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}

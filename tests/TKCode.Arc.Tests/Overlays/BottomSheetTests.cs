using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class BottomSheetTests
{
    private static (Window Window, BottomSheet Sheet) Setup(ArcMotionLevel level = ArcMotionLevel.Off, Action<BottomSheet>? configure = null)
    {
        var body = new StackPanel();
        for (var i = 0; i < 30; i++)
        {
            body.Children.Add(new TextBlock { Text = $"Row {i}", Height = 24 });
        }
        var sheet = new BottomSheet { Title = "Trip details", Description = "Lisbon, 3 nights", Content = body };
        configure?.Invoke(sheet);
        var window = TestHelpers.Show(new StackPanel { Children = { sheet } }, level);
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (window, sheet);
    }

    [AvaloniaFact]
    public void Opens_At_The_Initial_Detent_With_The_Default_Detents()
    {
        var (window, sheet) = Setup();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.EffectiveDetents.ShouldBe([0.45, 0.92]);
            sheet.DetentIndex.ShouldBe(0);
            var surface = (BottomSheetSurface)sheet.Surface!;
            // Tallest detent 0.92 × 600 plus the 160px hidden extension.
            surface.Bounds.Height.ShouldBe(0.92 * 600 + 160, 0.5);
            // Resting at 0.45: offset below the tallest detent = full × (1 − 0.45/0.92).
            sheet.SheetOffset.ShouldBe(0.92 * 600 * (1 - 0.45 / 0.92), 0.5);
            // At the smallest detent 78% of the dim remains.
            sheet.Presence!.ScrimOpacity.ShouldBe(0.78, 0.001);
            surface.Classes.Contains(BottomSheetSurface.ExpandedPseudoClass).ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetName(surface.Grabber!).ShouldBe("Expand sheet");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detents_Parse_Sort_And_Grabber_Keys_Move_Between_Them()
    {
        var (window, sheet) = Setup(configure: s => s.Detents = BottomSheetDetents.Parse("0.9, 0.3, 0.6"));
        try
        {
            var changes = new List<int>();
            sheet.DetentChanged += (_, e) => changes.Add(e.Index);
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.EffectiveDetents.ShouldBe([0.3, 0.6, 0.9]);
            var grabber = ((BottomSheetSurface)sheet.Surface!).Grabber!;
            grabber.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            sheet.DetentIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            sheet.DetentIndex.ShouldBe(2);
            sheet.SheetOffset.ShouldBe(0, 0.5);
            Avalonia.Automation.AutomationProperties.GetName(grabber).ShouldBe("Collapse sheet");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            sheet.DetentIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            sheet.DetentIndex.ShouldBe(2);
            changes.ShouldBe([1, 2, 0, 2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reading_Keys_On_The_Sheet_Expand_It_First()
    {
        var (window, sheet) = Setup();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = (BottomSheetSurface)sheet.Surface!;
            surface.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            sheet.DetentIndex.ShouldBe(1);
            surface.Body!.Offset.Y.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            surface.Body.Offset.Y.ShouldBe(48, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Down_From_The_Smallest_Detent_Closes()
    {
        var (window, sheet) = Setup(ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            var rest = sheet.SheetOffset;
            rest.ShouldBe(0.92 * 600 * (1 - 0.45 / 0.92), 0.5);
            var header = ((BottomSheetSurface)sheet.Surface!).Header!;
            var start = header.TranslatePoint(new Point(200, 40), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 10), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(0, 200), RawInputModifiers.LeftMouseButton);
            sheet.SheetOffset.ShouldBe(rest + 200, 0.5);
            window.MouseUp(start + new Vector(0, 200), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            sheet.IsOpen.ShouldBeFalse();
            sheet.LastCloseReason.ShouldBe(DismissReason.Gesture);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            sheet.Presence!.State.ShouldBe(ModalState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Up_Past_The_Tallest_Detent_Rubber_Bands()
    {
        var (window, sheet) = Setup(ArcMotionLevel.Full, s => s.InitialDetentIndex = 1);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            sheet.SheetOffset.ShouldBe(0, 0.5);
            var header = ((BottomSheetSurface)sheet.Surface!).Header!;
            var start = header.TranslatePoint(new Point(200, 40), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, -10), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(0, -300), RawInputModifiers.LeftMouseButton);
            var stretched = -sheet.SheetOffset;
            stretched.ShouldBeGreaterThan(0);
            stretched.ShouldBeLessThan(120);
            stretched.ShouldBe(global::TKCode.Arc.Gestures.RubberBand.Apply(300, 120), 0.5);
            window.MouseUp(start + new Vector(0, -300), MouseButton.Left);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            sheet.SheetOffset.ShouldBe(0, 0.5);
            sheet.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

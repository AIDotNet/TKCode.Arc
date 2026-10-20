using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

public class SignaturePadTests
{
    private static (SignaturePad Pad, Window Window) Show(double width = 600)
    {
        var pad = new SignaturePad { Width = width, Signer = "Ada Lovelace" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = pad });
        window.Width = width + 40;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (pad, window);
    }

    private static void Draw(Window window, SignaturePad pad, params Point[] points)
    {
        var canvas = pad.Pad!;
        Point At(Point p) => canvas.TranslatePoint(p, window)!.Value;
        window.MouseDown(At(points[0]), MouseButton.Left);
        foreach (var point in points.Skip(1))
        {
            window.MouseMove(At(point));
        }
        window.MouseUp(At(points[^1]), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var (pad, window) = Show();
        try
        {
            pad.Hint.ShouldBe("Sign here");
            pad.InkColor.ShouldBe(SignatureInkColor.Black);
            pad.InkWidth.ShouldBe(SignatureInkWidth.Medium);
            pad.FileName.ShouldBe("signature");
            pad.IsEmpty.ShouldBeTrue();
            pad.Classes.ShouldContain(":empty");
            // The pad keeps the 600 : 260 ratio of its coordinate space.
            pad.Pad!.Bounds.Width.ShouldBe(600, 0.5);
            pad.Pad!.Bounds.Height.ShouldBe(260, 0.5);
            pad.Pad!.IsHintShown.ShouldBeTrue();
            pad.AccessibleDescription.ShouldBe("Signature pad. Empty. Draw with a pointer, pen, or finger.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drawing_Captures_The_Pointer_And_Commits_A_Stroke()
    {
        var (pad, window) = Show();
        try
        {
            var changes = 0;
            pad.StrokesChanged += (_, _) => changes++;
            Draw(window, pad, new Point(100, 150), new Point(140, 120), new Point(180, 160), new Point(240, 130));
            pad.Strokes.Count.ShouldBe(1);
            var stroke = pad.Strokes[0];
            stroke.Points.Count.ShouldBe(4);
            stroke.Points[0].Pressure.ShouldBe(-1);
            stroke.Points[0].X.ShouldBe(100, 0.5);
            stroke.Color.ShouldBe(SignatureInkColor.Black);
            pad.IsEmpty.ShouldBeFalse();
            pad.CanUndo.ShouldBeTrue();
            changes.ShouldBe(1);
            pad.Pad!.IsHintShown.ShouldBeFalse();
            pad.Pad!.IsFocused.ShouldBeTrue();
            pad.Status!.Message.ShouldBe("Stroke added. 1 in the signature.");
            pad.AccessibleDescription.ShouldContain("1 stroke.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Undo_Redo_And_Clear()
    {
        var (pad, window) = Show();
        try
        {
            Draw(window, pad, new Point(100, 150), new Point(200, 150));
            Draw(window, pad, new Point(100, 180), new Point(200, 180));
            pad.Strokes.Count.ShouldBe(2);
            Key(window, PhysicalKey.Z, RawInputModifiers.Control);
            pad.Strokes.Count.ShouldBe(1);
            pad.Status!.Message.ShouldBe("Undone");
            Key(window, PhysicalKey.Y, RawInputModifiers.Control);
            pad.Strokes.Count.ShouldBe(2);
            Key(window, PhysicalKey.Delete);
            pad.Strokes.Count.ShouldBe(0);
            pad.Status!.Message.ShouldBe("Signature cleared. Undo brings it back.");
            pad.Pad!.IsHintShown.ShouldBeTrue();
            Key(window, PhysicalKey.Z, RawInputModifiers.Control);
            pad.Strokes.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choices_Move_With_Arrow_Keys_And_New_Strokes_Use_Them()
    {
        var (pad, window) = Show();
        try
        {
            var colors = TestHelpers.Descendant<SignaturePadChoice>(pad, "PART_Colors");
            colors.Options[0].IsChecked.ShouldBe(true);
            colors.Options[0].Focus();
            Key(window, PhysicalKey.ArrowRight);
            pad.InkColor.ShouldBe(SignatureInkColor.Blue);
            colors.Options[1].IsChecked.ShouldBe(true);
            colors.Options[1].IsFocused.ShouldBeTrue();
            colors.Options[1].IsTabStop.ShouldBeTrue();
            colors.Options[0].IsTabStop.ShouldBeFalse();
            pad.Status!.Message.ShouldBe("Blue ink selected");
            pad.InkWidth = SignatureInkWidth.Bold;
            Draw(window, pad, new Point(100, 150), new Point(200, 150));
            pad.Strokes[0].Color.ShouldBe(SignatureInkColor.Blue);
            pad.Strokes[0].Width.ShouldBe(SignatureInkWidth.Bold);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_With_Motion_Off_Finishes_At_Once()
    {
        var (pad, window) = Show();
        try
        {
            Draw(window, pad, new Point(100, 150), new Point(200, 150));
            pad.Replay();
            Dispatcher.UIThread.RunJobs();
            pad.IsReplaying.ShouldBeFalse();
            pad.Status!.Message.ShouldBe("Replay finished");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Svg_Export_Is_Cropped_To_The_Ink_In_Print_Colors()
    {
        var (pad, window) = Show();
        try
        {
            pad.ExportSvg().ShouldBe(string.Empty);
            Draw(window, pad, new Point(100, 150), new Point(200, 150));
            pad.InkColor = SignatureInkColor.Violet;
            Draw(window, pad, new Point(120, 100), new Point(160, 110));
            var svg = pad.ExportSvg();
            svg.ShouldStartWith("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"84 84 132 82\"");
            svg.ShouldContain("fill=\"#17171a\"");
            svg.ShouldContain("fill=\"#5b35c8\"");
            SignatureExport.ToSvgPathData(pad.Strokes[0]).ShouldStartWith("M");
            SignatureExport.ToSvgPathData(pad.Strokes[0]).ShouldEndWith("Z");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Png_Export_Has_The_Scaled_Ink_Size()
    {
        var (pad, window) = Show();
        try
        {
            Should.Throw<InvalidOperationException>(() => pad.ExportPng());
            Draw(window, pad, new Point(100, 150), new Point(200, 150));
            using var bitmap = pad.ExportBitmap(2);
            var box = SignatureExport.InkBounds(pad.Strokes);
            bitmap.PixelSize.Width.ShouldBe((int)Math.Ceiling(box.Width * 2));
            bitmap.PixelSize.Height.ShouldBe((int)Math.Ceiling(box.Height * 2));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Outline_Thins_With_Speed_And_Uses_Pen_Pressure()
    {
        var slow = Enumerable.Range(0, 20).Select(i => new InkPoint(10 + i * 1.5, 50, -1, i * 16)).ToList();
        var fast = Enumerable.Range(0, 20).Select(i => new InkPoint(10 + i * 12, 50, -1, i * 16)).ToList();
        static double Width(IReadOnlyList<InkPoint> points)
        {
            var outline = SignatureExport.GetStrokeOutline(points, InkOutlineOptions.For(5.6));
            return outline.Max(p => p.Y) - outline.Min(p => p.Y);
        }
        Width(fast).ShouldBeLessThan(Width(slow));

        var dot = SignatureExport.GetStrokeOutline(new[] { new InkPoint(5, 5, -1, 0) }, InkOutlineOptions.For(5.6));
        dot.Count.ShouldBe(18);

        var light = Enumerable.Range(0, 10).Select(i => new InkPoint(i * 3, 0, 0.1, i)).ToList();
        var heavy = Enumerable.Range(0, 10).Select(i => new InkPoint(i * 3, 0, 0.9, i)).ToList();
        var pen = InkOutlineOptions.For(5.6) with { SimulatePressure = false };
        double PenWidth(List<InkPoint> points)
        {
            var outline = SignatureExport.GetStrokeOutline(points, pen);
            return outline.Max(p => p.Y) - outline.Min(p => p.Y);
        }
        PenWidth(light).ShouldBeLessThan(PenWidth(heavy));
    }

    [AvaloniaFact]
    public void Reset_Loads_Strokes_Without_History()
    {
        var (pad, window) = Show();
        try
        {
            var stroke = new InkStroke("a", new[] { new InkPoint(10, 10, -1, 0), new InkPoint(50, 20, -1, 30) }, SignatureInkColor.Blue, SignatureInkWidth.Fine, 0);
            pad.Reset(new[] { stroke });
            pad.Strokes.Count.ShouldBe(1);
            pad.CanUndo.ShouldBeFalse();
            pad.Reset();
            pad.IsEmpty.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

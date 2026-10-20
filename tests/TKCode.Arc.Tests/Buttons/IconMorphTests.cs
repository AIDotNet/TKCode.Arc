using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Buttons;

public class IconMorphTests
{
    [AvaloniaFact]
    public void Defaults_To_A_24px_Decorative_Icon()
    {
        var icon = new IconMorph();
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { icon } });
        try
        {
            icon.DesiredSize.Width.ShouldBe(24);
            icon.DesiredSize.Height.ShouldBe(24);
            icon.StrokeThickness.ShouldBe(1.75);
            icon.IsHitTestVisible.ShouldBeFalse();
            icon.Focusable.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(icon).ShouldBe(AccessibilityView.Raw);
            icon.LikeBrush.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Kind_Renders_Both_States()
    {
        var icon = new IconMorph { IconSize = 32 };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { icon } });
        try
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
            foreach (var kind in Enum.GetValues<IconMorphKind>())
            {
                icon.Kind = kind;
                foreach (var active in new[] { true, false, true })
                {
                    icon.IsActive = active;
                    Dispatcher.UIThread.RunJobs();
                    using var context = bitmap.CreateDrawingContext();
                    icon.Render(context);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Play_Morphs_Into_The_Pause_Bars()
    {
        var icon = new IconMorph { Kind = IconMorphKind.Play };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { icon } });
        try
        {
            var frame = icon.Outline!.Current!;
            frame.ContourCount.ShouldBe(2);
            frame.GetContour(0)[0].ShouldBe(new Point(7, 4.5));
            icon.IsActive = true;
            Dispatcher.UIThread.RunJobs();
            // Motion is off in tests: the outline lands on the pause bars at once.
            icon.Outline.Current!.GetContour(0)[0].ShouldBe(new Point(6.5, 5));
            icon.Outline.Current!.GetContour(1)[0].ShouldBe(new Point(14, 5));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morph_Rides_An_Interruptible_Spring()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var icon = new IconMorph { Kind = IconMorphKind.Menu };
            var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { icon } }, ArcMotionLevel.Full);
            try
            {
                icon.IsActive = true;
                clock.Advance(TimeSpan.FromMilliseconds(100));
                var mid = icon.Progress;
                mid.ShouldBeGreaterThan(0.05);
                mid.ShouldBeLessThan(1);
                // Toggling back reverses from where the icon is.
                icon.IsActive = false;
                clock.Advance(TimeSpan.FromMilliseconds(16));
                icon.Progress.ShouldBeGreaterThan(0.01);
                clock.Advance(TimeSpan.FromSeconds(2));
                icon.Progress.ShouldBe(0, 0.001);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Bell_Rings_And_Heart_Bursts_Only_With_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var bell = new IconMorph { Kind = IconMorphKind.Bell };
            var heart = new IconMorph { Kind = IconMorphKind.Heart };
            var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { bell, heart } }, ArcMotionLevel.Full);
            try
            {
                bell.IsActive = true;
                heart.IsActive = true;
                clock.Advance(TimeSpan.FromMilliseconds(50));
                bell.IsMomentPlaying.ShouldBeTrue();
                heart.IsMomentPlaying.ShouldBeTrue();
                clock.Advance(TimeSpan.FromSeconds(1.2));
                Dispatcher.UIThread.RunJobs();
                bell.IsMomentPlaying.ShouldBeFalse();

                ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
                heart.IsActive = false;
                heart.IsActive = true;
                heart.IsMomentPlaying.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void MorphGlyph_Scales_Its_Stroke_And_Morphs_From_The_Current_Outline()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var glyph = new MorphGlyph { Data = "M4 6h16 M4 12h16 M4 18h16", Size = 24 };
            var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { glyph } }, ArcMotionLevel.Full);
            try
            {
                glyph.DesiredSize.Width.ShouldBe(24);
                glyph.Morph!.Current!.GetContour(0)[0].ShouldBe(new Point(4, 6));
                _ = glyph.MorphTo("M6 6 L18 18 M12 12h0 M6 18 L18 6");
                clock.Advance(TimeSpan.FromMilliseconds(80));
                var p = glyph.Morph.Current!.GetContour(0)[0];
                p.X.ShouldBeGreaterThan(4);
                p.X.ShouldBeLessThan(6);
                glyph.Morph.IsAnimating.ShouldBeTrue();
                clock.Advance(TimeSpan.FromSeconds(2));
                glyph.Morph.Current!.GetContour(0)[0].X.ShouldBe(6, 1e-3);
                glyph.Morph.Current!.GetContour(0)[^1].Y.ShouldBe(18, 1e-3);
            }
            finally
            {
                window.Close();
            }
        }
    }
}

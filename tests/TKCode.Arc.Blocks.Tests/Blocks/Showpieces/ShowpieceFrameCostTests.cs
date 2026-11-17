using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>What runs every frame allocates nothing; the live region announces repeats.</summary>
public class ShowpieceFrameCostTests
{
    private const double Frame = 1 / 60.0;

    [Fact]
    public void Camera_Frames_Allocate_Nothing()
    {
        var flat = new ShowpieceCamera2D
        {
            MaxZoom = 53,
            Insets = new Thickness(24, 64, 24, 60),
            HasInertia = true,
            ZoomOverflow = ShowpieceZoomOverflow.RubberBand,
            FlightMode = ShowpieceFlightMode.VanWijk
        };
        flat.SetViewport(new Size(663, 400));
        flat.SetContent(new Rect(0, 0, 1236, 682.5));
        var changes = 0;
        flat.Changed += () => changes++;
        var orbit = new ShowpieceOrbitCamera { IsDriftEnabled = true, HasPitchInertia = true };
        // Warm up the code paths (JIT, first-time statics).
        flat.FlyTo(new Point(300, 200), 6);
        orbit.AnimateTo(new ShowpieceOrbitPose { Yaw = 1, Zoom = 2 }, ShowpieceSprings.OrbitReset);
        for (var i = 0; i < 120; i++)
        {
            flat.Advance(Frame);
            orbit.Advance(Frame);
        }

        flat.FlyTo(new Point(900, 500), 12);
        orbit.BeginDrag();
        orbit.DragBy(new Vector(40, 600));
        orbit.EndDrag(new Vector(500, 0));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 240; i++)
        {
            flat.Advance(Frame);
            orbit.Advance(Frame);
            _ = flat.WorldToScreen(new Point(i, i));
            _ = orbit.GetView(new Point(300, 150), 801).Project(new ShowpieceVec3(1, 0, 1), out _, out _);
        }
        GC.GetAllocatedBytesForCurrentThread().ShouldBe(before);
        changes.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Cached_Text_And_Brush_Lookups_Allocate_Nothing()
    {
        var cache = new ShowpieceTextCache();
        var typeface = Typeface.Default;
        for (var i = 0; i < 3; i++)
        {
            _ = cache.Get("Platform", typeface, 13.1);
            _ = cache.Brush(Colors.Black, 0.41);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            _ = cache.Get("Platform", typeface, 13.1);
            _ = cache.Brush(Colors.Black, 0.41);
        }
        GC.GetAllocatedBytesForCurrentThread().ShouldBe(before);
    }

    [AvaloniaFact]
    public void Live_Region_Announces_Repeats()
    {
        var region = new ShowpieceLiveRegion();
        AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
        region.Announce("Camera on Lisbon");
        AutomationProperties.GetName(region).ShouldBe("Camera on Lisbon");
        region.Announce("Camera on Lisbon");
        AutomationProperties.GetName(region).ShouldNotBe("Camera on Lisbon");
        region.LastAnnouncement.ShouldBe("Camera on Lisbon");
        region.Announce(null);
        region.LastAnnouncement.ShouldBe("Camera on Lisbon");
    }
}

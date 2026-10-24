using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Gestures;

namespace TKCode.Arc.Core.Tests.Gestures;

public class WheelRouterTests
{
    private static readonly TimeSpan T0 = TimeSpan.FromSeconds(10);

    [Fact]
    public void Pan_Mode_Pans_With_The_Wheel_And_Zooms_With_The_Modifier()
    {
        var router = new WheelRouter();
        // One notch down (Avalonia −1) moves the content up 50px.
        router.Route(new Vector(0, -1), KeyModifiers.None, T0).ShouldBe(WheelAction.PanBy(new Vector(0, -50)));
        // Trackpad: both axes.
        router.Route(new Vector(0.4, -0.2), KeyModifiers.None, T0 + TimeSpan.FromMilliseconds(16))
            .ShouldBe(WheelAction.PanBy(new Vector(20, -10)));
        // Ctrl and ⌘ zoom: one notch up ×1.246.
        foreach (var modifier in new[] { KeyModifiers.Control, KeyModifiers.Meta })
        {
            var action = router.Route(new Vector(0, 1), modifier, T0 + TimeSpan.FromSeconds(1));
            action.Kind.ShouldBe(WheelActionKind.Zoom);
            action.ZoomFactor.ShouldBe(Math.Exp(0.22), 1e-12);
            action.IsHandled.ShouldBeTrue();
        }
    }

    [Fact]
    public void Shift_Turns_A_Vertical_Wheel_Sideways()
    {
        var router = new WheelRouter();
        router.Route(new Vector(0, -2), KeyModifiers.Shift, T0).ShouldBe(WheelAction.PanBy(new Vector(-100, 0)));
        // A delta that is already horizontal is kept.
        router.Route(new Vector(-1, 0), KeyModifiers.Shift, T0).ShouldBe(WheelAction.PanBy(new Vector(-50, 0)));
    }

    [Fact]
    public void Zoom_Mode_Zooms_Every_Wheel_Event()
    {
        var router = new WheelRouter { Mode = WheelMode.Zoom };
        router.Route(new Vector(0, -1), KeyModifiers.None, T0).ZoomFactor.ShouldBe(Math.Exp(-0.22), 1e-12);
        router.Route(new Vector(1, 0), KeyModifiers.None, T0).ShouldBe(WheelAction.None);
    }

    [Fact]
    public void Notched_Zoom_Passes_Trackpad_Scrolling_To_The_Page()
    {
        var router = new WheelRouter { Mode = WheelMode.NotchedZoom };
        router.Route(new Vector(0, 1), KeyModifiers.None, T0).Kind.ShouldBe(WheelActionKind.Zoom);
        // A precise event starts a precise burst: whole deltas inside it still pass through.
        router.Route(new Vector(0, 0.35), KeyModifiers.None, T0 + TimeSpan.FromSeconds(1)).ShouldBe(WheelAction.None);
        router.Route(new Vector(0, 1), KeyModifiers.None, T0 + TimeSpan.FromSeconds(1.1)).ShouldBe(WheelAction.None);
        // After a rest the next notch zooms again.
        router.Route(new Vector(0, 1), KeyModifiers.None, T0 + TimeSpan.FromSeconds(2)).Kind.ShouldBe(WheelActionKind.Zoom);
        // Ctrl always zooms.
        router.Route(new Vector(0, 0.35), KeyModifiers.Control, T0 + TimeSpan.FromSeconds(2.05)).Kind.ShouldBe(WheelActionKind.Zoom);
    }

    [Fact]
    public void Modifier_Zoom_And_Pass_Through_Leave_Plain_Scrolling_Alone()
    {
        var modifier = new WheelRouter { Mode = WheelMode.ModifierZoom };
        modifier.Route(new Vector(0, 1), KeyModifiers.None, T0).ShouldBe(WheelAction.None);
        modifier.Route(new Vector(0, 1), KeyModifiers.Control, T0).Kind.ShouldBe(WheelActionKind.Zoom);

        var pass = new WheelRouter { Mode = WheelMode.PassThrough };
        pass.Route(new Vector(0, 1), KeyModifiers.Control, T0).ShouldBe(WheelAction.None);
        pass.RouteMagnify(0.1).ShouldBe(WheelAction.None);
        pass.RoutePinch(1.2).ShouldBe(WheelAction.None);
    }

    [Fact]
    public void Magnify_Follows_The_Fingers()
    {
        var router = new WheelRouter();
        router.RouteMagnify(0.25).ZoomFactor.ShouldBe(1.25, 1e-12);
        router.RouteMagnify(-1.5).ZoomFactor.ShouldBe(1);
    }

    [Fact]
    public void Pinch_Zooms_By_The_Change_Since_The_Last_Update()
    {
        var router = new WheelRouter();
        router.RoutePinch(1.2).ZoomFactor.ShouldBe(1.2, 1e-12);
        router.RoutePinch(1.5).ZoomFactor.ShouldBe(1.25, 1e-12);
        router.EndPinch();
        router.RoutePinch(0.5).ZoomFactor.ShouldBe(0.5, 1e-12);
        router.RoutePinch(0).ShouldBe(WheelAction.None);
    }

    [Fact]
    public void Device_Guess_Matches_Notches_And_Precise_Deltas()
    {
        WheelRouter.Classify(new Vector(0, 1)).ShouldBe(WheelDevice.Notched);
        WheelRouter.Classify(new Vector(0, -3)).ShouldBe(WheelDevice.Notched);
        WheelRouter.Classify(new Vector(0, 0.5)).ShouldBe(WheelDevice.Precise);
        WheelRouter.Classify(new Vector(0.1, 1)).ShouldBe(WheelDevice.Precise);
        WheelRouter.Classify(new Vector(0, 1.3)).ShouldBe(WheelDevice.Precise);
    }
}

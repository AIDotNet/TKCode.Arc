using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="SourceLinkOverlay"/>: the path between a span and a field, the length of its dash, the appearance (draw, far
/// dot, gloss) on a manual clock, switching and hiding, the ends going out of view or away, following moving ends, no work
/// while hidden, and the static line below full motion.
/// </summary>
public class SourceLinkOverlayTests
{
    private static readonly Color Tone = Color.Parse("#2F7DD1");

    /// <summary>Two ends the test moves and hides; the overlay asks them through the delegates.</summary>
    private sealed class Ends
    {
        public Rect Span = new(60, 80, 120, 16);
        public Rect Field = new(360, 180, 220, 36);
        public bool SpanVisible = true;
        public bool FieldVisible = true;
        public bool SpanGone;
        public int Asked;

        public SourceLinkAnchor? Start()
        {
            Asked++;
            return SpanGone ? null : new SourceLinkAnchor(Span, SpanVisible);
        }

        public SourceLinkAnchor? End() => new SourceLinkAnchor(Field, FieldVisible);
    }

    private static (AiReplyRig Rig, SourceLinkOverlay Overlay) Stage(ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null)
    {
        var overlay = new SourceLinkOverlay();
        var rig = new AiReplyRig(new Panel { Height = 320, Children = { overlay } }, level, clock, 640);
        return (rig, overlay);
    }

    private static void Show(SourceLinkOverlay overlay, Ends ends, Color? tone = null) => overlay.Show(ends.Start, ends.End, tone ?? Tone);

    [AvaloniaFact]
    public void The_Path_Runs_From_Right_Of_The_Span_To_Inside_The_Field()
    {
        var (rig, overlay) = Stage();
        using var _ = rig;
        var ends = new Ends();
        Show(overlay, ends);
        rig.Settle();
        overlay.IsShown.ShouldBeTrue();
        var link = overlay.Current.ShouldNotBeNull();
        link.HasAppeared.ShouldBeTrue();
        // Start: 4px right of the span, at its middle; end: 7px into the field, at its middle.
        link.From.ShouldBe(new Point(180 + 4, 88));
        link.To.ShouldBe(new Point(360 + 7, 198));
        var (c1, c2) = SourceLinkOverlay.ControlPoints(link.From, link.To);
        c1.ShouldBe(new Point((184 + 367) / 2.0, 88));
        c2.ShouldBe(new Point((184 + 367) / 2.0, 198));
        // The length of the S curve, against a much finer sum; the dash covers it all once drawn.
        var fine = SourceLinkOverlay.CubicLength(link.From, c1, c2, link.To, 4000);
        link.Length.ShouldBe(fine, 0.01);
        link.Length.ShouldBeGreaterThan(Point.Distance(link.From, link.To));
        link.LineDash.Length.ShouldBe(link.Length + 1, 0.001);
        link.LineDash.Offset.ShouldBe(0, 0.001);
        link.Path.ShouldNotBeNull();
        link.Path!.Bounds.Left.ShouldBe(184, 0.01);
        link.Path.Bounds.Right.ShouldBe(367, 0.01);
        link.Tone.ShouldBe(Tone);
    }

    [AvaloniaFact]
    public void A_Straight_Line_Has_The_Distance_As_Its_Length()
    {
        var from = new Point(0, 50);
        var to = new Point(200, 50);
        var (c1, c2) = SourceLinkOverlay.ControlPoints(from, to);
        SourceLinkOverlay.CubicLength(from, c1, c2, to).ShouldBe(200, 1e-9);
    }

    [AvaloniaFact]
    public void The_Gloss_Is_The_Tone_Lightened_Toward_White()
    {
        var (rig, overlay) = Stage();
        using var _ = rig;
        Show(overlay, new Ends());
        var gloss = overlay.Current!.GlossColorValue;
        var (l, _, _) = Oklch.FromColor(gloss);
        l.ShouldBe(0.91, 0.01);
        // Still the tone's hue: bluer than it is red.
        gloss.B.ShouldBeGreaterThan(gloss.R);
    }

    [AvaloniaFact]
    public void Off_And_Reduced_Show_A_Static_Line_And_Hide_At_Once()
    {
        foreach (var level in new[] { ArcMotionLevel.Off, ArcMotionLevel.Reduced })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var (rig, overlay) = Stage(level, clock);
            using var _ = rig;
            var ends = new Ends();
            Show(overlay, ends);
            rig.Settle();
            var first = overlay.Current.ShouldNotBeNull();
            first.Draw.Value.ShouldBe(1);
            first.Far.Value.ShouldBe(1);
            first.Opacity.Value.ShouldBe(1);
            first.Gloss.Value.ShouldBe(SourceLinkOverlay.GlossTo);
            first.Draw.IsAnimating.ShouldBeFalse();
            // Switching replaces at once: nothing fades out, nothing fades in.
            var other = new Ends { Span = new Rect(60, 140, 90, 16) };
            Show(overlay, other);
            overlay.Leaving.ShouldBeNull();
            var second = overlay.Current.ShouldNotBeNull();
            second.ShouldNotBeSameAs(first);
            second.Opacity.Value.ShouldBe(1);
            second.Draw.Value.ShouldBe(1);
            // An end out of view hides the line at once (no 120ms fade).
            other.FieldVisible = false;
            rig.Frames(17);
            second.InView.Value.ShouldBe(0);
            second.Alpha.ShouldBe(0);
            other.FieldVisible = true;
            rig.Frames(17);
            second.InView.Value.ShouldBe(1);
            overlay.Hide();
            overlay.IsShown.ShouldBeFalse();
            overlay.Current.ShouldBeNull();
            overlay.Leaving.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Draws_The_Line_Then_Grows_The_Far_Dot_And_Runs_The_Gloss_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        Show(overlay, new Ends());
        var link = overlay.Current.ShouldNotBeNull();
        link.Draw.Value.ShouldBe(0);
        link.Far.Value.ShouldBe(0);
        link.Gloss.Value.ShouldBe(SourceLinkOverlay.GlossFrom);
        link.Opacity.Value.ShouldBe(1, "the first link does not fade in");
        // The dash hides everything that is not drawn yet.
        link.LineDash.Offset.ShouldBe(link.LineDash.Length, 0.001);

        rig.Frames(100);
        link.Draw.Value.ShouldBeInRange(0.2, 0.999);
        link.LineDash.Offset.ShouldBeInRange(0.5, link.LineDash.Length - 0.5);
        link.Far.Value.ShouldBe(0, "the far dot waits 170ms");
        link.Gloss.Value.ShouldBe(SourceLinkOverlay.GlossFrom, "the gloss waits 135ms");

        rig.Frames(150);
        // 250ms: drawn; the far dot is on its way (170 + 140); the gloss has started.
        link.Draw.Value.ShouldBe(1);
        link.LineDash.Offset.ShouldBe(0, 0.001);
        link.Far.Value.ShouldBeInRange(0.05, 0.999);
        link.Gloss.Value.ShouldBeInRange(SourceLinkOverlay.GlossFrom + 0.001, 0.5);
        var stops = link.GlossStops;
        stops.Lead.ShouldBeLessThanOrEqualTo(stops.Peak);
        stops.Peak.ShouldBeLessThanOrEqualTo(stops.Tail);

        rig.Frames(100);
        link.Far.Value.ShouldBe(1);

        rig.Frames(300);
        // 650ms: the peak is past the middle.
        link.Gloss.Value.ShouldBeInRange(0.5, SourceLinkOverlay.GlossTo - 0.001);
        link.GlossStops.Peak.ShouldBe(Math.Clamp(link.Gloss.Value, 0, 1), 1e-9);

        rig.Frames(200);
        // Once: it ends past the far end and stays there.
        link.Gloss.Value.ShouldBe(SourceLinkOverlay.GlossTo);
        link.Gloss.IsAnimating.ShouldBeFalse();
        link.GlossStops.ShouldBe((1.0, 1.0, 1.0));
        rig.Frames(500);
        link.Gloss.Value.ShouldBe(SourceLinkOverlay.GlossTo);
    }

    [AvaloniaFact]
    public void Switching_Fades_The_Old_Link_Out_Where_It_Was_And_Fades_The_New_One_In_While_It_Draws()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        var first = new Ends();
        Show(overlay, first);
        rig.Frames(1000);
        var old = overlay.Current.ShouldNotBeNull();
        var oldFrom = old.From;

        var second = new Ends { Span = new Rect(60, 200, 80, 16), Field = new Rect(360, 40, 220, 36) };
        Show(overlay, second);
        overlay.Leaving.ShouldBeSameAs(old);
        var link = overlay.Current.ShouldNotBeNull();
        link.FadesIn.ShouldBeTrue();
        link.Opacity.Value.ShouldBe(0);
        link.Draw.Value.ShouldBe(0);

        // One frame in (both on their way; the standard curve drops the old one fast).
        rig.Frames(17);
        old.Opacity.Value.ShouldBeInRange(0.01, 0.95);
        link.Opacity.Value.ShouldBeInRange(0.001, 0.5);
        // The old link stays where it was: it is not asked again.
        first.Span = new Rect(0, 0, 10, 10);
        rig.Frames(17);
        old.From.ShouldBe(oldFrom);

        rig.Frames(117);
        // 150ms: the old one is gone (120ms); the new one is still fading in (300ms).
        overlay.Leaving.ShouldBeNull();
        link.Opacity.Value.ShouldBeInRange(0.5, 0.999);
        link.Draw.Value.ShouldBeGreaterThan(0.5);

        rig.Frames(200);
        link.Opacity.Value.ShouldBe(1);
        link.Draw.Value.ShouldBe(1);
        link.Far.Value.ShouldBe(1);
        link.Gloss.Value.ShouldBeLessThan(SourceLinkOverlay.GlossTo, "its own gloss runs");
    }

    [AvaloniaFact]
    public void Showing_The_Same_Ends_Again_Keeps_The_Link()
    {
        var (rig, overlay) = Stage();
        using var _ = rig;
        var ends = new Ends();
        Func<SourceLinkAnchor?> start = ends.Start;
        Func<SourceLinkAnchor?> end = ends.End;
        overlay.Show(start, end, Tone);
        var link = overlay.Current;
        overlay.Show(start, end, Tone);
        overlay.Current.ShouldBeSameAs(link);
        overlay.Show(start, end, Colors.Orange);
        overlay.Current.ShouldNotBeSameAs(link);
        overlay.Current!.Tone.ShouldBe(Colors.Orange);
    }

    [AvaloniaFact]
    public void Hiding_Fades_Out_In_120ms_And_Stops_Asking_The_Ends()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        var ends = new Ends();
        Show(overlay, ends);
        rig.Frames(400);
        overlay.IsFollowing.ShouldBeTrue();
        var asked = ends.Asked;
        rig.Frames(100);
        // About one question per frame while shown.
        (ends.Asked - asked).ShouldBeInRange(5, 7);

        var link = overlay.Current!;
        overlay.Hide();
        overlay.IsShown.ShouldBeFalse();
        overlay.IsFollowing.ShouldBeFalse();
        overlay.Leaving.ShouldBeSameAs(link);
        rig.Frames(17);
        link.Opacity.Value.ShouldBeInRange(0.01, 0.95);
        rig.Frames(117);
        overlay.Leaving.ShouldBeNull();

        // No work while hidden.
        asked = ends.Asked;
        var tracked = overlay.TrackCount;
        rig.Frames(300);
        ends.Asked.ShouldBe(asked);
        overlay.TrackCount.ShouldBe(tracked);
        // Hiding again does nothing.
        overlay.Hide();
        overlay.Leaving.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Line_Follows_Moving_Ends_On_The_Next_Frame_And_Rebuilds_Only_When_They_Move()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        var ends = new Ends();
        Show(overlay, ends);
        rig.Frames(1000);
        var link = overlay.Current!;
        var path = link.Path;
        rig.Frames(50);
        link.Path.ShouldBeSameAs(path, "nothing moved, nothing rebuilt");

        // The form scrolls by 40: the field's end follows on the next frame, with no lag.
        ends.Field = ends.Field.Translate(new Vector(0, -40));
        rig.Frames(17);
        link.To.ShouldBe(new Point(367, 158));
        link.Path.ShouldNotBeSameAs(path);
        link.LineDash.Offset.ShouldBe(0, 0.001);
        link.LineDash.Length.ShouldBe(link.Length + 1, 0.001);

        // Zoom: the span grows and moves; the start follows.
        for (var i = 1; i <= 5; i++)
        {
            ends.Span = new Rect(60, 80 + i * 4, 120 + i * 10, 16 + i);
            rig.Frames(17);
            link.From.ShouldBe(new Point(180 + i * 10 + 4, 80 + i * 4 + (16 + i) / 2.0));
        }
        link.Alpha.ShouldBe(1);
    }

    [AvaloniaFact]
    public void An_End_Out_Of_View_Fades_The_Line_In_120ms_And_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        var ends = new Ends();
        Show(overlay, ends);
        rig.Frames(1000);
        var link = overlay.Current!;

        ends.SpanVisible = false;
        rig.Frames(50);
        link.InView.Value.ShouldBeInRange(0.05, 0.95);
        rig.Frames(100);
        link.InView.Value.ShouldBe(0);
        link.Alpha.ShouldBe(0);
        overlay.IsShown.ShouldBeTrue("still shown, only out of view");

        ends.SpanVisible = true;
        rig.Frames(50);
        link.InView.Value.ShouldBeInRange(0.05, 0.95);
        rig.Frames(100);
        link.Alpha.ShouldBe(1);
        // It does not draw itself again.
        link.Draw.Value.ShouldBe(1);

        // An end that is gone fades the line the same way, where it last was.
        var from = link.From;
        ends.SpanGone = true;
        rig.Frames(150);
        link.Alpha.ShouldBe(0);
        link.From.ShouldBe(from);
    }

    [AvaloniaFact]
    public void A_Link_Waits_Until_Both_Ends_Are_In_View_Before_It_Draws()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, overlay) = Stage(ArcMotionLevel.Full, clock);
        using var _ = rig;
        // The field is scrolled away (the owner will bring it into view).
        var ends = new Ends { FieldVisible = false };
        Show(overlay, ends);
        rig.Frames(300);
        var link = overlay.Current!;
        link.HasGeometry.ShouldBeTrue();
        link.HasAppeared.ShouldBeFalse();
        link.Draw.Value.ShouldBe(1, "nothing has started");

        ends.FieldVisible = true;
        rig.Frames(17);
        link.HasAppeared.ShouldBeTrue();
        link.Draw.Value.ShouldBeLessThan(1);
        rig.Frames(300);
        link.Draw.Value.ShouldBe(1);

        // Hiding a link that never appeared just drops it.
        var waiting = new Ends { SpanVisible = false };
        Show(overlay, waiting);
        overlay.Hide();
        overlay.Leaving.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Ends_Follow_At_Every_Motion_Level()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var (rig, overlay) = Stage(level, clock);
            using var _ = rig;
            var ends = new Ends();
            Show(overlay, ends);
            overlay.IsFollowing.ShouldBeTrue();
            ends.Span = ends.Span.Translate(new Vector(0, 30));
            rig.Frames(17);
            overlay.Current!.From.Y.ShouldBe(118);
        }
    }

    [AvaloniaFact]
    public void The_Overlay_Is_Decorative_And_Never_Hit()
    {
        var (rig, overlay) = Stage();
        using var _ = rig;
        Show(overlay, new Ends());
        overlay.IsHitTestVisible.ShouldBeFalse();
        overlay.ClipToBounds.ShouldBeFalse();
        overlay.Focusable.ShouldBeFalse();
        AutomationProperties.GetAccessibilityView(overlay).ShouldBe(AccessibilityView.Raw);
        var peer = ControlAutomationPeer.CreatePeerForElement(overlay);
        peer.IsContentElement().ShouldBeFalse();
        peer.IsControlElement().ShouldBeFalse();
        // The ring of the dot on the paper stays white; the form's is the surface.
        TestHelpers.ColorOf(overlay.StartDotStroke).ShouldBe(Colors.White);
        TestHelpers.ColorOf(overlay.EndDotStroke).ShouldBe(ArcTokens.GetColor(overlay, DesignTokens.ArcDesignTokenKind.Surface));
        // A render pass with a link shown goes through.
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        rig.Settle();
    }
}

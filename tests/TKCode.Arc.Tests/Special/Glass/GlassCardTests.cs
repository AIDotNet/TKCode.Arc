using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Glass;

/// <summary>
/// The glass card and the shared glass material: when the backdrop shows through (and when it must not), the copy being
/// taken again when the card moves, reduced transparency, the pointer sheen, the lean and the tilt signal.
/// </summary>
public class GlassCardTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Panel Root, Canvas Scene, GlassCard Card) Scene(double maxTilt = 0)
    {
        var scene = new Canvas { Background = Brushes.SteelBlue };
        scene.Children.Add(new Border { Width = 80, Height = 80, Background = Brushes.Orange });
        var card = new GlassCard
        {
            Width = 200,
            Height = 120,
            MaxTilt = maxTilt,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(40, 30, 0, 0),
            Content = new TextBlock { Text = "Evening swim" }
        };
        var root = new Panel { Children = { scene, card } };
        GlassBackdrop.SetSource(root, scene);
        return (root, scene, card);
    }

    private static Point At(Visual target, Window window, double fx, double fy) =>
        target.TranslatePoint(new Point(target.Bounds.Width * fx, target.Bounds.Height * fy), window) ?? default;

    [AvaloniaFact]
    public void The_backdrop_shows_through_a_card_inside_the_source_container()
    {
        var (root, scene, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        card.IsFrosted.ShouldBeTrue();
        card.Pane!.Frost.Source.ShouldBe(scene);
        card.Pane.Frost.IsVisible.ShouldBeTrue();
        card.Pane.Frost.Effect.ShouldBeOfType<ImmutableBlurEffect>().Radius.ShouldBe(22);
        window.Close();
    }

    [AvaloniaFact]
    public void Moving_the_card_against_the_source_takes_the_copy_again()
    {
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        var frost = card.Pane!.Frost;
        var before = frost.Paints;
        before.ShouldBeGreaterThan(0);
        card.Margin = new Thickness(90, 50, 0, 0);
        Pump();
        Pump();
        frost.Paints.ShouldBeGreaterThan(before);
        window.Close();
    }

    [AvaloniaFact]
    public void An_ancestor_source_or_no_source_leaves_a_tinted_pane()
    {
        var card = new GlassCard { Width = 200, Height = 100 };
        var root = new Panel { Children = { card } };
        GlassBackdrop.SetSource(root, root);
        var window = TestHelpers.Show(root);
        Pump();
        // The source contains the card: copying it would paint the card inside itself.
        card.IsFrosted.ShouldBeFalse();
        GlassBackdrop.SetSource(root, null);
        Pump();
        card.IsFrosted.ShouldBeFalse();
        card.Pane!.IsSolid.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Reduce_transparency_turns_the_glass_into_an_opaque_surface()
    {
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        card.Pane!.ReduceTransparency = true;
        Pump();
        card.IsFrosted.ShouldBeFalse();
        card.Pane.IsSolid.ShouldBeTrue();
        card.Pane.ReduceTransparency = false;
        Pump();
        card.IsFrosted.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void The_reduce_transparency_token_reaches_the_glass()
    {
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        card.Pane!.ReduceTransparency.ShouldBeFalse();
        card.Pane.SolidFill.ShouldNotBeNull();
        card.Pane.TintColor.A.ShouldBeLessThan((byte)200);
        window.Close();
    }

    [AvaloniaFact]
    public void The_sheen_follows_the_pointer_and_fades_when_it_leaves()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root, ArcMotionLevel.Full);
        Pump();
        var gloss = card.Pane!.Gloss;
        window.MouseMove(At(card, window, 0.25, 0.5), RawInputModifiers.None);
        window.MouseMove(At(card, window, 0.75, 0.25), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        gloss.SheenAlpha.ShouldBe(1, 0.01);
        gloss.SheenPosition.X.ShouldBe(150, 1);
        gloss.SheenPosition.Y.ShouldBe(30, 1);
        window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        gloss.SheenAlpha.ShouldBe(0, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_shows_no_sheen_and_no_lean()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene(maxTilt: 10);
        var window = TestHelpers.Show(root, ArcMotionLevel.Reduced);
        Pump();
        window.MouseMove(At(card, window, 0.9, 0.1), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        card.Pane!.Gloss.SheenAlpha.ShouldBe(0);
        card.CurrentLean.ShouldBe(default);
        card.RenderTransform.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void With_max_tilt_the_card_leans_toward_the_pointer()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene(maxTilt: 10);
        var window = TestHelpers.Show(root, ArcMotionLevel.Full);
        Pump();
        window.MouseMove(At(card, window, 0.5, 0.5), RawInputModifiers.None);
        window.MouseMove(At(card, window, 0.95, 0.05), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        card.CurrentLean.X.ShouldBe(9, 0.3);
        card.CurrentLean.Y.ShouldBe(9, 0.3);
        card.RenderTransform.ShouldBeOfType<Rotate3DTransform>().AngleY.ShouldBe(9, 0.3);
        window.MouseMove(new Point(390, 290), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        card.CurrentLean.ShouldBe(default);
        card.RenderTransform.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Without_max_tilt_the_card_stays_flat()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root, ArcMotionLevel.Full);
        Pump();
        window.MouseMove(At(card, window, 1, 0), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        card.CurrentLean.ShouldBe(default);
        card.RenderTransform.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void The_tilt_signal_drives_the_light_while_the_pointer_is_away()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene(maxTilt: 8);
        var window = TestHelpers.Show(root, ArcMotionLevel.Full);
        Pump();
        card.Tilt = new Vector(1, -1);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        card.Pane!.Gloss.SheenAlpha.ShouldBe(1, 0.01);
        card.Pane.Gloss.SheenPosition.X.ShouldBe(200, 1);
        card.Pane.Gloss.SheenPosition.Y.ShouldBe(0, 1);
        card.CurrentLean.X.ShouldBe(8, 0.2);
        card.CurrentLean.Y.ShouldBe(8, 0.2);
        // Out of range values are clamped.
        card.Tilt = new Vector(3, double.NaN);
        card.Tilt.ShouldBe(new Vector(1, 0));
        card.Tilt = default;
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        card.Pane.Gloss.SheenAlpha.ShouldBe(0, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void A_live_backdrop_redraws_every_frame()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (root, _, card) = Scene();
        GlassBackdrop.SetIsLive(root, true);
        var window = TestHelpers.Show(root, ArcMotionLevel.Off);
        Pump();
        var frost = card.Pane!.Frost;
        var before = frost.Paints;
        for (var i = 0; i < 5; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
        frost.Paints.ShouldBeGreaterThan(before);
        window.Close();
    }

    [AvaloniaFact]
    public void The_card_is_a_group_and_not_focusable()
    {
        var (root, _, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        card.Focusable.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(card).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        window.Close();
    }
}

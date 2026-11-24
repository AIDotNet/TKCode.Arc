using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc.Tests.Special.Glass;

/// <summary>
/// <see cref="GlassBackdrop.ModeProperty"/> (#188): the brush copy stays the default; the native mode puts a blur on the
/// frost's <see cref="Visual.BackdropEffect"/> instead, needs no source, and picks the backdrop cache. The headless test
/// platform draws nothing, so these tests check the wiring; the pixels were compared with Skia separately.
/// </summary>
public class GlassBackdropModeTests
{
    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Panel Root, Canvas Scene, GlassCard Card) Scene()
    {
        var scene = new Canvas { Background = Brushes.SteelBlue };
        scene.Children.Add(new Border { Width = 80, Height = 80, Background = Brushes.Orange });
        var card = new GlassCard
        {
            Width = 200,
            Height = 120,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(40, 30, 0, 0),
            Content = new TextBlock { Text = "Evening swim" }
        };
        var root = new Panel { Children = { scene, card } };
        GlassBackdrop.SetSource(root, scene);
        return (root, scene, card);
    }

    [AvaloniaFact]
    public void The_brush_copy_stays_the_default()
    {
        var (root, scene, card) = Scene();
        var window = TestHelpers.Show(root);
        Pump();
        GlassBackdrop.GetMode(card).ShouldBe(GlassBackdropMode.Brush);
        GlassBackdrop.GetCache(card).ShouldBeNull();
        var frost = card.Pane!.Frost;
        frost.Source.ShouldBe(scene);
        frost.Effect.ShouldBeOfType<ImmutableBlurEffect>().Radius.ShouldBe(22);
        frost.BackdropEffect.ShouldBeNull();
        frost.Paints.ShouldBeGreaterThan(0);
        // The copy covers the pane exactly; it reaches past the edges by drawing, not by its bounds.
        frost.Bounds.ShouldBe(new Rect(card.Pane.Bounds.Size));
        window.Close();
    }

    [AvaloniaFact]
    public void Native_mode_puts_the_blur_on_the_backdrop_and_drops_the_brush_copy()
    {
        var (root, _, card) = Scene();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        var frost = card.Pane!.Frost;
        card.IsFrosted.ShouldBeTrue();
        frost.IsVisible.ShouldBeTrue();
        frost.Source.ShouldBeNull();
        frost.Effect.ShouldBeNull();
        frost.BackdropEffect.ShouldBeOfType<ImmutableBlurEffect>().Radius.ShouldBe(22);
        // Null: the compositor chooses (a retained snapshot for a blur).
        frost.BackdropEffectCache.ShouldBeNull();
        frost.Paints.ShouldBe(0);
        // The blur reads past the rounded edge (the clip cuts it), as the copy does.
        frost.Bounds.Width.ShouldBeGreaterThan(card.Pane.Bounds.Width);
        frost.Bounds.X.ShouldBeLessThan(0);

        // Moving the card takes no copy: the compositor reads what is behind it.
        card.Margin = new Thickness(90, 50, 0, 0);
        Pump();
        Pump();
        frost.Paints.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Native_mode_needs_no_source()
    {
        var card = new GlassCard { Width = 200, Height = 100 };
        var root = new Panel { Background = Brushes.Teal, Children = { card } };
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        card.IsFrosted.ShouldBeTrue();
        card.Pane!.Frost.BackdropEffect.ShouldNotBeNull();
        // Back to the brush without a source: a tinted pane.
        GlassBackdrop.SetMode(root, GlassBackdropMode.Brush);
        Pump();
        card.IsFrosted.ShouldBeFalse();
        card.Pane.Frost.BackdropEffect.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Switching_back_to_the_brush_restores_the_copy()
    {
        var (root, scene, card) = Scene();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Brush);
        Pump();
        var frost = card.Pane!.Frost;
        frost.Source.ShouldBe(scene);
        frost.BackdropEffect.ShouldBeNull();
        frost.BackdropEffectCache.ShouldBeNull();
        frost.Effect.ShouldBeOfType<ImmutableBlurEffect>();
        frost.Bounds.ShouldBe(new Rect(card.Pane.Bounds.Size));
        frost.Paints.ShouldBeGreaterThan(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_live_backdrop_reads_again_on_every_draw_and_an_explicit_cache_wins()
    {
        var (root, _, card) = Scene();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        GlassBackdrop.SetIsLive(root, true);
        var window = TestHelpers.Show(root);
        Pump();
        var frost = card.Pane!.Frost;
        frost.BackdropEffectCache.ShouldBeOfType<VolatileBackdropEffectCache>();
        // No redraw loop: the compositor sees the source change by itself.
        for (var i = 0; i < 5; i++)
        {
            Pump();
        }
        frost.Paints.ShouldBe(0);

        var retained = new RetainedBackdropEffectCache();
        GlassBackdrop.SetCache(root, retained);
        Pump();
        frost.BackdropEffectCache.ShouldBeSameAs(retained);
        GlassBackdrop.SetCache(root, null);
        GlassBackdrop.SetIsLive(root, false);
        Pump();
        frost.BackdropEffectCache.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void The_blur_radius_follows_the_pane()
    {
        var (root, _, card) = Scene();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        card.Pane!.BlurRadius = 8;
        Pump();
        card.Pane.Frost.BackdropEffect.ShouldBeOfType<ImmutableBlurEffect>().Radius.ShouldBe(8);
        card.Pane.BlurRadius = 0;
        Pump();
        // No blur: the backdrop already shows through the translucent tint.
        card.Pane.Frost.BackdropEffect.ShouldBeNull();
        card.IsFrosted.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Reduce_transparency_drops_the_native_backdrop()
    {
        var (root, _, card) = Scene();
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        card.Pane!.ReduceTransparency = true;
        Pump();
        card.IsFrosted.ShouldBeFalse();
        card.Pane.IsSolid.ShouldBeTrue();
        card.Pane.Frost.BackdropEffect.ShouldBeNull();
        card.Pane.ReduceTransparency = false;
        Pump();
        card.IsFrosted.ShouldBeTrue();
        card.Pane.Frost.BackdropEffect.ShouldNotBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Every_glass_control_takes_the_native_backdrop()
    {
        var segmented = new GlassSegmented { Items = { new GlassSegment { Content = "Day" }, new GlassSegment { Content = "Week" } } };
        var tabs = new GlassTabBar { Items = { new GlassTab { Content = "Home" }, new GlassTab { Content = "Search" } } };
        var root = new StackPanel { Background = Brushes.Teal, Children = { segmented, tabs } };
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        segmented.Pane!.Frost.BackdropEffect.ShouldNotBeNull();
        segmented.Pane.IsFrosted.ShouldBeTrue();
        tabs.Pane!.Frost.BackdropEffect.ShouldNotBeNull();
        tabs.Pane.IsFrosted.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_menu_popup_in_the_overlay_layer_takes_the_native_backdrop_without_a_source()
    {
        var menu = new GlassMenu { Items = { new GlassMenuItem { Content = "New note" }, new GlassMenuItem { Content = "Delete" } } };
        var button = new ArcButton { Content = "Arrange" };
        GlassMenu.SetMenu(button, menu);
        var root = new Panel { Background = Brushes.Teal, Children = { button } };
        GlassBackdrop.SetMode(root, GlassBackdropMode.Native);
        var window = TestHelpers.Show(root);
        Pump();
        menu.Open(button);
        Pump();
        menu.IsOpen.ShouldBeTrue();
        GlassBackdrop.GetMode(menu).ShouldBe(GlassBackdropMode.Native);
        menu.Pane!.IsFrosted.ShouldBeTrue();
        menu.Pane.Frost.BackdropEffect.ShouldNotBeNull();
        menu.Close(returnFocus: false);
        Pump();
        window.Close();
    }
}

using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

/// <summary>Runs the animated paths with full motion on a manual clock and checks where they settle.</summary>
public class ButtonsMotionTests
{
    private static void Settle(ManualFrameClock clock, int milliseconds = 1500)
    {
        for (var t = 0; t < milliseconds; t += 50)
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void ConfirmMorph_Springs_The_Pill_Between_Faces_And_Hands_Back_To_Auto()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var morph = new ConfirmMorph { Icon = ArcIcons.Trash2 };
            var window = TestHelpers.Show(new StackPanel { Children = { morph } }, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var pill = Part<Border>(morph, ConfirmMorph.PillPart);
                var idleWidth = pill.Bounds.Width;
                morph.Ask();
                Dispatcher.UIThread.RunJobs();
                clock.Advance(TimeSpan.FromMilliseconds(60));
                Dispatcher.UIThread.RunJobs();
                double.IsNaN(pill.Width).ShouldBeFalse();
                pill.Width.ShouldBeGreaterThan(idleWidth - 1);
                Settle(clock);
                double.IsNaN(pill.Width).ShouldBeTrue();
                Part<Panel>(morph, "PART_IdleFace").IsVisible.ShouldBeFalse();
                var face = Part<Panel>(morph, "PART_ConfirmFace");
                face.IsVisible.ShouldBeTrue();
                face.Children.ShouldAllBe(c => Math.Abs(c.Opacity - 1) < 1e-6 && c.Effect == null);
                pill.Bounds.Width.ShouldBeGreaterThan(idleWidth);
                morph.Cancel();
                Settle(clock);
                Part<Panel>(morph, "PART_ConfirmFace").IsVisible.ShouldBeFalse();
                pill.Bounds.Width.ShouldBe(idleWidth, 0.5);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void ActionButton_And_HoldToConfirm_Swap_Glyphs_With_Motion()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var gate = new TaskCompletionSource();
            var action = new ActionButton { AsyncAction = () => gate.Task };
            var hold = new HoldToConfirm { HoldDuration = TimeSpan.FromMilliseconds(60) };
            var window = TestHelpers.Show(new StackPanel { Spacing = 8, Children = { action, hold } }, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                action.Focus();
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Settle(clock, 300);
                gate.SetResult();
                WaitFor(() => action.State == ActionButtonState.Success).ShouldBeTrue();
                Settle(clock);
                var icon = Part<ButtonsGlyphSwap>(action, ActionButton.IconPart);
                icon.Current.ShouldBeOfType<StrokeDrawGlyph>();
                ((StrokeDrawGlyph)icon.Current!).Progress.ShouldBe(1, 1e-6);
                Avalonia.VisualTree.VisualExtensions.GetVisualChildren(icon).Count().ShouldBe(1);

                hold.Focus();
                window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
                WaitFor(() => hold.IsConfirmed).ShouldBeTrue();
                window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
                Settle(clock);
                hold.Classes.ShouldContain(":confirmed");
                Part<Border>(hold, HoldToConfirm.FillPart).IsVisible.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void ExpandingButtonGroup_Springs_Widths_And_FloatingButtonGroup_Glides()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var reply = new ExpandingButtonGroupItem { Label = "Reply", Icon = ArcIcons.Reply };
            var archive = new ExpandingButtonGroupItem { Label = "Archive", Icon = ArcIcons.Archive };
            var expanding = new ExpandingButtonGroup { Items = { reply, archive } };
            var undo = new FloatingButtonGroupItem { Label = "Undo", Icon = ArcIcons.Undo2, IsIconOnly = true };
            var share = new FloatingButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2 };
            var floating = new FloatingButtonGroup { Items = { undo, share } };
            var window = TestHelpers.Show(new StackPanel { Spacing = 8, Children = { expanding, floating } }, ArcMotionLevel.Full);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var expanded = reply.Bounds.Width;
                archive.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                clock.Advance(TimeSpan.FromMilliseconds(80));
                Dispatcher.UIThread.RunJobs();
                archive.Bounds.Width.ShouldBeGreaterThan(36);
                archive.Bounds.Width.ShouldBeLessThan(expanded + 20);
                Settle(clock);
                reply.Bounds.Width.ShouldBe(36, 0.5);
                archive.Bounds.Width.ShouldBeGreaterThan(60);

                undo.Focus(NavigationMethod.Tab);
                Settle(clock, 400);
                var highlight = Part<GlidingIndicator>(floating, FloatingButtonGroup.HighlightPart);
                highlight.CurrentRect.Width.ShouldBe(36, 0.5);
                window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
                clock.Advance(TimeSpan.FromMilliseconds(50));
                highlight.CurrentRect.Width.ShouldBeGreaterThan(36);
                highlight.CurrentRect.Width.ShouldBeLessThan(share.Bounds.Width);
                Settle(clock);
                highlight.CurrentRect.Width.ShouldBe(share.Bounds.Width, 0.5);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task ThemeSwitcher_Reveals_The_New_Theme_Under_A_Snapshot()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var switcher = new ThemeSwitcher { Variant = ThemeSwitcherVariant.Reveal };
            // The overlay layer holds the snapshot. Avalonia's default Window template has none (AtomUI's window had one), so
            // the content sits in a VisualLayerManager with the overlay layer on, as in GalleryWindow's template.
            var layers = new VisualLayerManager { EnableOverlayLayer = true, Child = new StackPanel { Children = { switcher } } };
            var window = new Window { Content = layers, Width = 400, Height = 300 };
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var layer = OverlayLayer.GetOverlayLayer(switcher);
                layer.ShouldNotBeNull();
                Part<Button>(switcher, ThemeSwitcher.ButtonPart).Focus();
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                var sawOverlay = false;
                for (var i = 0; i < 300 && (switcher.ActualThemeVariant != ThemeVariant.Dark || layer!.Children.OfType<ThemeSwitcherSnapshot>().Any()); i++)
                {
                    sawOverlay |= layer!.Children.OfType<ThemeSwitcherSnapshot>().Any();
                    clock.Advance(TimeSpan.FromMilliseconds(16));
                    await Task.Delay(2);
                    Dispatcher.UIThread.RunJobs();
                }
                sawOverlay.ShouldBeTrue();
                switcher.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);
                layer!.Children.OfType<ThemeSwitcherSnapshot>().ShouldBeEmpty();
            }
            finally
            {
                window.Close();
                await TestHelpers.ApplyAsync(null);
            }
        }
    }
}

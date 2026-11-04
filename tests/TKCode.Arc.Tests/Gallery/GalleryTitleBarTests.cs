using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The desktop window is an ArcWindow whose Arc TitleBar carries the logo, the title and the keep-on-top pin; off macOS the
/// bar draws the caption buttons, on macOS it keeps the traffic lights and leaves room for them. The bar itself is covered
/// by ArcWindowTests; these check how the gallery uses it.
/// </summary>
public class GalleryTitleBarTests
{
    private static GalleryWindow Open(bool drawsCaptionButtons)
    {
        var window = new GalleryWindow(drawsCaptionButtons) { Width = 1280, Height = 860 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Press(AvaloniaButton button) => button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));

    [AvaloniaFact]
    public void The_Window_Extends_Into_The_Decorations_With_The_Bar_Above_The_Shell()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            window.ShouldBeAssignableTo<ArcWindow>();
            window.ExtendClientAreaToDecorationsHint.ShouldBeTrue();
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(40);
            var bar = window.TitleBar.ShouldNotBeNull();
            bar.Bounds.Height.ShouldBe(40);
            bar.TranslatePoint(default, window).ShouldBe(new Point(0, 0));
            bar.Bounds.Width.ShouldBe(window.Bounds.Width);
            window.Shell.TranslatePoint(default, window)!.Value.Y.ShouldBe(40);
            window.MinWidth.ShouldBe(900);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_MacOS_The_Bar_Has_Logo_Title_Pin_And_Caption_Buttons()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            var bar = window.TitleBar!;
            bar.EffectiveCaptionButtons.ShouldBe(TitleBarCaptionButtons.Drawn);
            bar.Icon.ShouldBeOfType<Image>().Source.ShouldNotBeNull();
            Part<TextBlock>(bar, "PART_Title").Text.ShouldBe(window.Title);
            Part<Border>(bar, "PART_SystemInset").IsEffectivelyVisible.ShouldBeFalse();

            AvaloniaButton[] buttons =
            [
                window.PinButton,
                Part<AvaloniaButton>(bar, TitleBar.MinimizeButtonPart),
                Part<AvaloniaButton>(bar, TitleBar.MaximizeButtonPart),
                Part<AvaloniaButton>(bar, TitleBar.CloseButtonPart)
            ];
            foreach (var button in buttons)
            {
                button.IsEffectivelyVisible.ShouldBeTrue();
                Tooltip.GetTip(button).ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
            }
            // Left to right: pin, minimize, maximize, close; close ends the bar.
            var lefts = buttons.Select(b => b.TranslatePoint(default, bar)!.Value.X).ToArray();
            lefts.ShouldBe(lefts.Order().ToArray());
            var close = buttons[^1];
            (close.TranslatePoint(default, bar)!.Value.X + close.Bounds.Width).ShouldBeGreaterThan(bar.Bounds.Width - 16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void On_MacOS_The_Bar_Keeps_The_Traffic_Lights()
    {
        var window = Open(drawsCaptionButtons: false);
        try
        {
            var bar = window.TitleBar!;
            bar.EffectiveCaptionButtons.ShouldBe(TitleBarCaptionButtons.System);
            Part<StackPanel>(bar, "PART_CaptionButtons").IsEffectivelyVisible.ShouldBeFalse();
            window.PinButton.IsEffectivelyVisible.ShouldBeTrue();
            // The logo starts right of the traffic lights.
            Part<Border>(bar, "PART_SystemInset").IsEffectivelyVisible.ShouldBeTrue();
            ((Control)bar.Icon!).TranslatePoint(default, bar)!.Value.X.ShouldBeGreaterThanOrEqualTo(78);

            // Full screen hides the lights, so their room goes too.
            window.WindowState = WindowState.FullScreen;
            Dispatcher.UIThread.RunJobs();
            Part<Border>(bar, "PART_SystemInset").IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Pin_Toggles_Topmost_And_Its_Tooltip()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            var pin = window.PinButton;
            window.Topmost.ShouldBeFalse();
            Tooltip.GetTip(pin).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowPin));

            Press(pin);
            window.Topmost.ShouldBeTrue();
            Tooltip.GetTip(pin).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowUnpin));
            pin.Variant.ShouldBe(ButtonVariant.Secondary);

            Press(pin);
            window.Topmost.ShouldBeFalse();
            pin.Variant.ShouldBe(ButtonVariant.Ghost);

            // A change from elsewhere shows on the pin as well.
            window.Topmost = true;
            Tooltip.GetTip(pin).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowUnpin));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Overlay_Layer_Still_Hosts_Popups_And_Covers_The_Bar()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            var layers = window.GetVisualDescendants().OfType<VisualLayerManager>().ShouldHaveSingleItem();
            layers.Name.ShouldBe("PART_VisualLayerManager");
            OverlayLayer.GetOverlayLayer(window.PinButton).ShouldNotBeNull();
            OverlayLayer.GetOverlayLayer(window.Shell).ShouldBeSameAs(OverlayLayer.GetOverlayLayer(window.TitleBar!));
            window.TitleBar!.GetVisualAncestors().ShouldContain(layers);
            window.Shell.GetVisualAncestors().ShouldContain(layers);
        }
        finally
        {
            window.Close();
        }
    }
}

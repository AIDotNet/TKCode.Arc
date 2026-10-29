using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Gallery;

/// <summary>
/// The desktop window draws its own title bar (GalleryTitleBar): logo and title, the keep-on-top pin and, off macOS, the
/// caption buttons; on macOS it keeps the traffic lights and leaves room for them.
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

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void The_Window_Extends_Into_The_Decorations_With_The_Bar_Above_The_Shell()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            window.ExtendClientAreaToDecorationsHint.ShouldBeTrue();
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(GalleryTitleBar.BarHeight);
            var bar = window.TitleBar;
            bar.IsVisible.ShouldBeTrue();
            bar.Bounds.Height.ShouldBe(GalleryTitleBar.BarHeight);
            bar.TranslatePoint(default, window).ShouldBe(new Point(0, 0));
            bar.Bounds.Width.ShouldBe(window.Bounds.Width);
            // The shell starts under the bar.
            window.Shell.TranslatePoint(default, window)!.Value.Y.ShouldBe(GalleryTitleBar.BarHeight);
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
            var bar = window.TitleBar;
            bar.DrawsCaptionButtons.ShouldBeTrue();
            bar.LeadingInset.ShouldBe(0);
            bar.Brand.GetVisualDescendants().OfType<Image>().ShouldHaveSingleItem().Source.ShouldNotBeNull();
            bar.Brand.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == window.Title);

            bar.MinimizeButton.ShouldNotBeNull();
            bar.MaximizeButton.ShouldNotBeNull();
            bar.CloseButton.ShouldNotBeNull();
            var buttons = new[] { bar.PinButton, bar.MinimizeButton!, bar.MaximizeButton!, bar.CloseButton! };
            foreach (var button in buttons)
            {
                button.IsEffectivelyVisible.ShouldBeTrue();
                Tooltip.GetTip(button).ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
            }
            // Left to right: pin, minimize, maximize, close; close ends the bar.
            var lefts = buttons.Select(b => b.TranslatePoint(default, bar)!.Value.X).ToArray();
            lefts.ShouldBe(lefts.Order().ToArray());
            var close = bar.CloseButton!;
            (close.TranslatePoint(default, bar)!.Value.X + close.Bounds.Width).ShouldBeGreaterThan(bar.Bounds.Width - 16);
            Tooltip.GetTip(close).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowClose));
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
            var bar = window.TitleBar;
            bar.DrawsCaptionButtons.ShouldBeFalse();
            bar.MinimizeButton.ShouldBeNull();
            bar.MaximizeButton.ShouldBeNull();
            bar.CloseButton.ShouldBeNull();
            bar.Buttons.Children.OfType<Button>().ShouldHaveSingleItem().ShouldBeSameAs(bar.PinButton);
            // The logo starts right of the traffic lights.
            bar.LeadingInset.ShouldBeGreaterThan(60);
            bar.Brand.TranslatePoint(default, bar)!.Value.X.ShouldBeGreaterThanOrEqualTo(bar.LeadingInset);

            // Full screen hides the lights, so their room goes too.
            window.WindowState = WindowState.FullScreen;
            Dispatcher.UIThread.RunJobs();
            bar.LeadingInset.ShouldBe(0);
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
            var pin = window.TitleBar.PinButton;
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
    public void Caption_Buttons_Minimize_Maximize_Restore_And_Close()
    {
        var window = Open(drawsCaptionButtons: true);
        var bar = window.TitleBar;

        Press(bar.MaximizeButton!);
        window.WindowState.ShouldBe(WindowState.Maximized);
        Tooltip.GetTip(bar.MaximizeButton!).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowRestore));

        Press(bar.MaximizeButton!);
        window.WindowState.ShouldBe(WindowState.Normal);
        Tooltip.GetTip(bar.MaximizeButton!).ShouldBe(GalleryText.Get(GalleryShellLangResourceKind.WindowMaximize));

        bar.ToggleMaximize();
        window.WindowState.ShouldBe(WindowState.Maximized);
        bar.ToggleMaximize();
        window.WindowState.ShouldBe(WindowState.Normal);

        Press(bar.MinimizeButton!);
        window.WindowState.ShouldBe(WindowState.Minimized);
        window.WindowState = WindowState.Normal;

        var closed = false;
        window.Closed += (_, _) => closed = true;
        Press(bar.CloseButton!);
        Dispatcher.UIThread.RunJobs();
        closed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Close_Turns_Danger_Under_The_Pointer_And_Back()
    {
        var window = Open(drawsCaptionButtons: true);
        try
        {
            var close = window.TitleBar.CloseButton!;
            var before = close.Background;
            var center = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            close.IsPointerOver.ShouldBeTrue();
            close.Background.ShouldBeAssignableTo<Avalonia.Media.ISolidColorBrush>()!.Color
                 .ShouldBe(ArcTokens.GetColor(close, DesignTokens.ArcDesignTokenKind.DangerSubtle));
            close.Foreground.ShouldBeAssignableTo<Avalonia.Media.ISolidColorBrush>()!.Color
                 .ShouldBe(ArcTokens.GetColor(close, DesignTokens.ArcDesignTokenKind.Danger));

            window.MouseMove(new Point(600, 400));
            Dispatcher.UIThread.RunJobs();
            close.IsPointerOver.ShouldBeFalse();
            close.Background.ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Overlay_Layer_Still_Hosts_Popups_And_Covers_The_Bar()
    {
        var window = new GalleryWindow(drawsCaptionButtons: true) { Width = 1280, Height = 860 };
        VisualLayerManager? named = null;
        window.TemplateApplied += (_, e) => named = e.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager");
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var layers = window.GetVisualDescendants().OfType<VisualLayerManager>().ShouldHaveSingleItem();
            named.ShouldBeSameAs(layers);
            OverlayLayer.GetOverlayLayer(window.TitleBar.PinButton).ShouldNotBeNull();
            OverlayLayer.GetOverlayLayer(window.Shell).ShouldBeSameAs(OverlayLayer.GetOverlayLayer(window.TitleBar));
            window.TitleBar.GetVisualAncestors().ShouldContain(layers);
            window.Shell.GetVisualAncestors().ShouldContain(layers);
        }
        finally
        {
            window.Close();
        }
    }
}

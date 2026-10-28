using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/settings: a switch between three views, each one block at full width. Account is the settings page
/// block with its four panes and floating save bar, Security the two-factor wizard, password form and sessions, and
/// Preferences the demo's own panel (appearance, accent, compact sidebar, sign out).
/// </summary>
public sealed class DemoSettingsPage : DemoPageView
{
    public DemoSettingsPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        SetViews(
        [
            new DemoView("account", "Account", () => new DemoSettingsAccount()),
            new DemoView("security", "Security", () => new DemoSettingsSecurity()),
            new DemoView("preferences", "Preferences", () => new DemoSettingsPreferences(host))
        ]);
    }
}

/// <summary>Small pieces the three settings views share.</summary>
internal static class DemoSettingsUi
{
    /// <summary>
    /// A text that screen readers announce when it changes (uiarc.dev's polite live region). It takes no room and is not
    /// seen.
    /// </summary>
    public static TextBlock LiveRegion()
    {
        var live = new TextBlock { Opacity = 0, Height = 1, Width = 1, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Automation.AutomationProperties.SetLiveSetting(live, Avalonia.Automation.AutomationLiveSetting.Polite);
        return live;
    }

    /// <summary>Muted body text that wraps.</summary>
    public static TextBlock Muted(string text, double size = DemoUi.BodySize)
        => DemoUi.Text(text, TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextSecondary, size);
}

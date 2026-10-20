using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ThemeSwitcherPage
{
    private static Control Variant(ThemeSwitcherVariant variant)
    {
        var caption = GalleryUi.Text(variant.ToString(), ArcDesignTokenKind.TextMuted, 12);
        return GalleryUi.Column(8,
            GalleryUi.Row(new ThemeSwitcher { Variant = variant }, new ThemeSwitcher { Variant = variant, IsIconOnly = true }),
            caption);
    }

    public static Control Create()
    {
        var variants = GalleryUi.Row(
            Variant(ThemeSwitcherVariant.Reveal),
            Variant(ThemeSwitcherVariant.Eclipse),
            Variant(ThemeSwitcherVariant.Split),
            Variant(ThemeSwitcherVariant.Rise));

        // Controlled: the app handles the request itself; here it only flips a local appearance.
        var status = GalleryUi.Text("Next: Dark", ArcDesignTokenKind.TextSecondary, 13);
        var controlled = new ThemeSwitcher { Appearance = ThemeSwitcherAppearance.Light, Variant = ThemeSwitcherVariant.Rise };
        controlled.ThemeChangeRequested += (_, e) =>
        {
            e.Handled = true;
            controlled.Appearance = e.Next;
            status.Text = $"Requested {e.Next} ({e.Variant}) from {e.Origin.X:0},{e.Origin.Y:0}; the app decides what to apply.";
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(controlled);
        row.Children.Add(status);

        return GalleryUi.Column(20,
            GalleryUi.Card("Variants", "Each switch changes the whole app between light and dark through AtomUI's theme manager. The old window is kept as a snapshot over the new one and revealed by variant: a circle from the button, an eclipsing disc, a centre seam, a rising edge. Reduced motion switches without the transition.", variants),
            GalleryUi.Card("Handled by the app", "Mark ThemeChangeRequested handled to apply the theme yourself (optionally inside ThemeSwitcher.RunTransitionAsync).", row));
    }
}

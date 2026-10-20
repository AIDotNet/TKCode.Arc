using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class IconMorphPage
{
    private static Control Toggle(IconMorphKind kind, string off, string on, double size = 24, Action<IconMorph>? configure = null)
    {
        var icon = new IconMorph { Kind = kind, IconSize = size };
        configure?.Invoke(icon);
        var button = new Button
        {
            Content = icon,
            Variant = ButtonVariant.Ghost,
            Width = size + 20,
            Height = size + 20,
            Padding = new Avalonia.Thickness(0)
        };
        AutomationProperties.SetName(button, off);
        button.Click += (_, _) =>
        {
            icon.IsActive = !icon.IsActive;
            AutomationProperties.SetName(button, icon.IsActive ? on : off);
        };
        var caption = GalleryUi.Text(kind.ToString(), ArcDesignTokenKind.TextMuted, 12);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        return GalleryUi.Column(6, button, caption);
    }

    public static Control Create()
    {
        var all = GalleryUi.Row(
            Toggle(IconMorphKind.Menu, "Open menu", "Close menu"),
            Toggle(IconMorphKind.Play, "Play", "Pause"),
            Toggle(IconMorphKind.Plus, "Add", "Added"),
            Toggle(IconMorphKind.Copy, "Copy", "Copied"),
            Toggle(IconMorphKind.Theme, "Dark mode", "Light mode"),
            Toggle(IconMorphKind.Chevron, "Expand", "Collapse"),
            Toggle(IconMorphKind.Bell, "Turn notifications on", "Turn notifications off"),
            Toggle(IconMorphKind.Heart, "Like", "Unlike"));

        var sizes = GalleryUi.Row(
            Toggle(IconMorphKind.Menu, "Open menu", "Close menu", 16),
            Toggle(IconMorphKind.Menu, "Open menu", "Close menu", 24),
            Toggle(IconMorphKind.Menu, "Open menu", "Close menu", 32),
            Toggle(IconMorphKind.Play, "Play", "Pause", 16),
            Toggle(IconMorphKind.Play, "Play", "Pause", 32));

        var chevrons = GalleryUi.Row(
            Toggle(IconMorphKind.Chevron, "Expand", "Collapse"),
            Toggle(IconMorphKind.Chevron, "Expand", "Collapse", 24, i =>
            {
                i.ChevronPointing = IconMorphChevronPointing.Right;
                i.ChevronTurn = 90;
            }),
            Toggle(IconMorphKind.Heart, "Like", "Unlike", 32),
            Toggle(IconMorphKind.Bell, "Turn notifications on", "Turn notifications off", 32));

        var morph = new TKCode.Arc.Primitives.MorphGlyph { Data = "M4 6h16 M4 12h16 M4 18h16", Size = 32 };
        var shapes = new[] { "M4 6h16 M4 12h16 M4 18h16", "M6 6 L18 18 M12 12h0 M6 18 L18 6", "M4 12.5l5 5L20 6.5" };
        var index = 0;
        var morphButton = new Button { Content = morph, Variant = ButtonVariant.Ghost, Width = 52, Height = 52, Padding = new Avalonia.Thickness(0) };
        AutomationProperties.SetName(morphButton, "Morph to the next shape");
        morphButton.Click += (_, _) => morph.Data = shapes[index = (index + 1) % shapes.Length];

        return GalleryUi.Column(20,
            GalleryUi.Card("Eight pairs", "Click each icon. Lines rotate and slide, outlines interpolate, the bell rings and the heart bursts. Toggle quickly: every morph reverses from where it is.", all),
            GalleryUi.Card("Sizes", "The stroke scales with the size (1.75 on the 24-unit grid).", sizes),
            GalleryUi.Card("Chevron and moments", "ChevronTurn 180 flips down to up; 90 turns right to down. Ring and burst replay on every turn on.", chevrons),
            GalleryUi.Card("MorphGlyph (P16)", "The path morph primitive on any path data: menu to close keeps its vertices; close to check resamples by arc length.", GalleryUi.Row(morphButton)));
    }
}

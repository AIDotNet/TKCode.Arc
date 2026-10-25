using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The small "New" pill that marks a component added recently (<see cref="Catalog.GalleryEntry.AddedOn"/>), as uiarc.dev
/// does: an accent-subtle surface with accent text. Used in the sidebar rows and next to a page's title.
/// </summary>
public sealed class GalleryNewBadge : Border
{
    public GalleryNewBadge(double fontSize = 11)
    {
        var label = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = FontWeight.Medium,
            LineHeight = fontSize + 3,
            TextWrapping = TextWrapping.NoWrap
        };
        GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.NewBadge);
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.Accent);
        ArcTokens.Bind(label, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        Child = label;
        Padding = new Thickness(7, 1);
        CornerRadius = new CornerRadius(999);
        VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
    }
}

using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

/// <summary>Explicit page registry (no reflection, AOT friendly).</summary>
public static partial class GalleryPages
{
    private static readonly Dictionary<string, Func<Control>> s_factories = new(StringComparer.Ordinal);

    static GalleryPages()
    {
        RegisterAll();
    }

    static partial void RegisterAll();

    public static void Register(string className, Func<Control> factory) => s_factories[className] = factory;

    public static bool IsImplemented(string className) => s_factories.ContainsKey(className);

    /// <summary>Builds the full page for <paramref name="entry"/>: header, tabs and its examples (or a placeholder).</summary>
    public static Control Create(GalleryEntry entry)
    {
        var implemented = s_factories.TryGetValue(entry.ClassName, out var factory);
        var body = implemented ? factory!() : Placeholder(entry);
        return new GalleryPageView(entry, body, implemented);
    }

    private static Control Placeholder(GalleryEntry entry)
    {
        var note = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.BindFormat(note, TextBlock.TextProperty, GalleryShellLangResourceKind.NotBuiltYet, entry.Title);
        var content = GalleryUi.Column(12, GalleryUi.Glyph(ArcIcons.Hourglass, 28, ArcDesignTokenKind.TextMuted), note);
        foreach (var child in content.Children)
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.PlannedTitle);
        var description = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.BindFormat(description, TextBlock.TextProperty, GalleryShellLangResourceKind.PlannedDescription, entry.ClassName, entry.Phase);
        return GalleryUi.Card("Planned", title, description, content);
    }
}

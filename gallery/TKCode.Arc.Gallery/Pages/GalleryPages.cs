using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

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
        var content = GalleryUi.Column(12,
            GalleryUi.Glyph(ArcIcons.Hourglass, 28, ArcDesignTokenKind.TextMuted),
            GalleryUi.Text($"{entry.Title} is not built yet.", ArcDesignTokenKind.TextSecondary, 14));
        foreach (var child in content.Children)
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        return GalleryUi.Card("Planned", $"{entry.ClassName} is scheduled for phase P{entry.Phase}.", content);
    }
}

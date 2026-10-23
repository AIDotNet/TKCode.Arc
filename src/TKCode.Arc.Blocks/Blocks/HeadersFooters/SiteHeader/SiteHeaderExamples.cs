// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli): siteHeaderExampleItems.
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arc's example items: what a <see cref="SiteHeader"/> without items shows.</summary>
internal static class SiteHeaderExamples
{
    // 24x24 Lucide paths (the Core icon subset does not carry these five).
    private const string Boxes = "M2.97 12.92A2 2 0 0 0 2 14.63v3.24a2 2 0 0 0 .97 1.71l3 1.8a2 2 0 0 0 2.06 0L12 19v-5.5l-5-3-4.03 2.42Z M7 16.5l-4.74-2.85 M7 16.5l5-3 M7 16.5v5.17 M12 13.5V19l3.97 2.38a2 2 0 0 0 2.06 0l3-1.8a2 2 0 0 0 .97-1.71v-3.24a2 2 0 0 0-.97-1.71L17 10.5l-5 3Z M17 16.5l-5-3 M17 16.5l4.74-2.85 M17 16.5v5.17 M7.97 4.42A2 2 0 0 0 7 6.13v4.37l5 3 5-3V6.13a2 2 0 0 0-.97-1.71l-3-1.8a2 2 0 0 0-2.06 0l-3 1.8Z M12 8 7.26 5.15 M12 8l4.74-2.85 M12 13.5V8";

    private const string PanelsTopLeft = "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M3 9h18 M9 21V9";

    private const string Route = "M3 19a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M9 19h8.5a3.5 3.5 0 0 0 0-7h-11a3.5 3.5 0 0 1 0-7H15 M15 5a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z";

    private const string History = "M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8 M3 3v5h5 M12 7v5l4 2";

    private const string MessagesSquare = "M16 10a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 14.286V4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z M20 9a2 2 0 0 1 2 2v10.286a.71.71 0 0 1-1.212.502l-2.202-2.202A2 2 0 0 0 17.172 19H10a2 2 0 0 1-2-2v-1";

    /// <summary>Creates Product, Resources, Pricing and Customers with Arc's link texts and icons (the feature card has no picture).</summary>
    public static IEnumerable<SiteHeaderItem> Create()
    {
        yield return new SiteHeaderItem
        {
            Value = "product",
            Label = "Product",
            Links =
            {
                Link("Components", "140 interactive React components", ArcIcons.Parse(Boxes)),
                Link("Blocks", "Complete sections, ready to ship", ArcIcons.Parse(PanelsTopLeft)),
                Link("Templates", "Starter sites with every page", ArcIcons.LayoutTemplate),
                Link("Themes", "Tune color, radius, and motion", ArcIcons.Palette)
            },
            Feature = new SiteHeaderFeature { Title = "What's new in 2.4", Description = "Site headers, footers, and hero sections." }
        };
        yield return new SiteHeaderItem
        {
            Value = "resources",
            Label = "Resources",
            Links =
            {
                Link("Documentation", "Install, theme, and compose", ArcIcons.BookOpen),
                Link("Guides", "Patterns for real product work", ArcIcons.Parse(Route)),
                Link("Changelog", "Every release, week by week", ArcIcons.Parse(History)),
                Link("Community", "Questions, answers, and showcases", ArcIcons.Parse(MessagesSquare))
            }
        };
        yield return new SiteHeaderItem { Value = "pricing", Label = "Pricing" };
        yield return new SiteHeaderItem { Value = "customers", Label = "Customers" };
    }

    private static SiteHeaderLink Link(string label, string description, Geometry icon)
        => new() { Label = label, Description = description, Icon = icon };
}

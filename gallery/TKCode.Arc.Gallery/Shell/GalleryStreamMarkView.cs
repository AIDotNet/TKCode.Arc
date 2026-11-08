using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The StreamMark product page (<c>/streammark</c>): what it is, a reply streaming live, what it does, and its packages.
/// StreamMark is a product of its own, in its own repository; Arc's <see cref="Markdown"/> control is built on it.
/// </summary>
internal sealed class GalleryStreamMarkView : UserControl
{
    public static Uri RepositoryUrl { get; } = new("https://github.com/AtomUICommunity/StreamMark");

    private static readonly (string Package, GalleryShellLangResourceKind Text)[] s_packages =
    [
        ("TKCode.StreamMark.Avalonia", GalleryShellLangResourceKind.StreamMarkPackageAvalonia),
        ("TKCode.StreamMark.Core", GalleryShellLangResourceKind.StreamMarkPackageCore),
        ("TKCode.StreamMark.Highlighting.TextMate", GalleryShellLangResourceKind.StreamMarkPackageTextMate),
        ("TKCode.StreamMark.RaTeX", GalleryShellLangResourceKind.StreamMarkPackageRaTeX),
        ("TKCode.StreamMark.Mermaid", GalleryShellLangResourceKind.StreamMarkPackageMermaid)
    ];

    private static readonly (Geometry Glyph, GalleryShellLangResourceKind Title, GalleryShellLangResourceKind Text)[] s_features =
    [
        (ArcIcons.Zap, GalleryShellLangResourceKind.StreamMarkFeatureStreaming, GalleryShellLangResourceKind.StreamMarkFeatureStreamingText),
        (ArcIcons.Monitor, GalleryShellLangResourceKind.StreamMarkFeatureNative, GalleryShellLangResourceKind.StreamMarkFeatureNativeText),
        (ArcIcons.BookOpen, GalleryShellLangResourceKind.StreamMarkFeatureScience, GalleryShellLangResourceKind.StreamMarkFeatureScienceText),
        (ArcIcons.Cpu, GalleryShellLangResourceKind.StreamMarkFeatureAot, GalleryShellLangResourceKind.StreamMarkFeatureAotText)
    ];

    public GalleryStreamMarkView(GalleryShell shell)
    {
        var column = new StackPanel();
        column.Children.Add(BuildHero(shell));
        Stage = new GalleryStreamStage { EndWidth = 300, StackBelow = 820 };
        column.Children.Add(Section(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.StreamMarkLiveTitle)), Stage));
        column.Children.Add(Section(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.StreamMarkFeaturesTitle)), BuildFeatures()));
        column.Children.Add(Section(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.StreamMarkPackagesTitle)), BuildPackages()));
        column.Children.Add(GallerySiteFooter.Create(shell));
        Content = GallerySite.Page(column, out _);
    }

    /// <summary>The live reply.</summary>
    public GalleryStreamStage Stage { get; }

    private static Control Section(Control heading, Control body)
    {
        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(heading);
        section.Children.Add(body);
        return section;
    }

    private Control BuildHero(GalleryShell shell)
    {
        var badge = new Badge { Tone = BadgeTone.Info, HorizontalAlignment = HorizontalAlignment.Left };
        GalleryText.BindFormat(badge, ContentControl.ContentProperty, GalleryShellLangResourceKind.StreamMarkBadge, GallerySite.StreamMarkVersion);
        var title = GallerySite.Reveal(GalleryShellLangResourceKind.StreamMarkTitle, TextLevel.H1);
        var subtitle = GallerySite.Text(GalleryShellLangResourceKind.StreamMarkSubtitle, ArcDesignTokenKind.TextSecondary, 17);
        var content = new StackPanel { Spacing = 18, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(badge);
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(GallerySite.Install("dotnet add package TKCode.StreamMark.Avalonia"));
        content.Children.Add(GallerySite.Actions(
            GallerySite.Action(GalleryShellLangResourceKind.StreamMarkGitHub, ButtonVariant.Primary, () => GallerySite.Open(this, RepositoryUrl), ArcIcons.ArrowUpRight),
            GallerySite.Action(GalleryShellLangResourceKind.StreamMarkInArc, ButtonVariant.Secondary,
                               () => shell.Navigate(GalleryRoute.To(GalleryCatalog.AllEntries.First(entry => entry.ClassName == "Markdown"))), ArcIcons.ArrowRight),
            GallerySite.Action(GalleryShellLangResourceKind.NavProjects, ButtonVariant.Ghost, () => shell.Navigate(GalleryRoute.Projects))));
        return GalleryHomeView.HeroCard(content);
    }

    private static Control BuildFeatures()
    {
        var grid = new GalleryGrid { MinItemWidth = 240, MaxColumns = 4, Spacing = 14 };
        foreach (var (glyph, title, text) in s_features)
        {
            var stack = new StackPanel { Spacing = 8 };
            var mark = GalleryUi.Glyph(glyph, 18, ArcDesignTokenKind.Foreground);
            mark.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(mark);
            stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
            stack.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 13.5));
            grid.Children.Add(GallerySite.Panel(stack, padding: 16));
        }
        return grid;
    }

    private static Control BuildPackages()
    {
        var rows = new StackPanel();
        foreach (var (package, text) in s_packages)
        {
            var name = GalleryUi.Mono(package, ArcDesignTokenKind.Foreground, 13);
            name.TextWrapping = TextWrapping.Wrap;
            var row = new GalleryTwoPane { StackBelow = 620, StartShare = 0.5, Spacing = 4, Margin = new Thickness(16, 12) };
            row.Children.Add(name);
            row.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 13.5));
            var line = new Border { BorderThickness = new Thickness(0, rows.Children.Count == 0 ? 0 : 1, 0, 0), Child = row };
            ArcTokens.Bind(line, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            rows.Children.Add(line);
        }
        var panel = GallerySite.Panel(rows, padding: 0);
        panel.ClipToBounds = true;
        return panel;
    }
}

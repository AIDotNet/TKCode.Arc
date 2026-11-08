using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The site's recommendation of the desktop app: why (this page runs in WebAssembly, and its performance does not represent
/// the app), the files of the newest release with the visitor's system first, and a comparison. The comparison states facts
/// only; put measured numbers here once both hosts have been measured the same way. The release list is fetched when the
/// section first comes into view; without it the buttons open the page of all releases.
/// </summary>
internal sealed class GalleryDownloadSection : GalleryTwoPane
{
    private static readonly Download[] s_downloads =
    [
        new("osx-arm64", GalleryClientOs.MacOS, GalleryShellLangResourceKind.DownloadMacArm),
        new("osx-x64", GalleryClientOs.MacOS, GalleryShellLangResourceKind.DownloadMacIntel),
        new("win-x64", GalleryClientOs.Windows, GalleryShellLangResourceKind.DownloadWindows),
        new("linux-x64", GalleryClientOs.Linux, GalleryShellLangResourceKind.DownloadLinux)
    ];

    private static readonly (GalleryShellLangResourceKind Row, GalleryShellLangResourceKind Web, GalleryShellLangResourceKind Desktop)[] s_comparison =
    [
        (GalleryShellLangResourceKind.CompareRuntime, GalleryShellLangResourceKind.CompareRuntimeWeb, GalleryShellLangResourceKind.CompareRuntimeDesktop),
        (GalleryShellLangResourceKind.CompareThreads, GalleryShellLangResourceKind.CompareThreadsWeb, GalleryShellLangResourceKind.CompareThreadsDesktop),
        (GalleryShellLangResourceKind.CompareDrawing, GalleryShellLangResourceKind.CompareDrawingWeb, GalleryShellLangResourceKind.CompareDrawingDesktop),
        (GalleryShellLangResourceKind.CompareStartup, GalleryShellLangResourceKind.CompareStartupWeb, GalleryShellLangResourceKind.CompareStartupDesktop),
        (GalleryShellLangResourceKind.CompareMedia, GalleryShellLangResourceKind.CompareMediaWeb, GalleryShellLangResourceKind.CompareMediaDesktop),
        (GalleryShellLangResourceKind.CompareBest, GalleryShellLangResourceKind.CompareBestWeb, GalleryShellLangResourceKind.CompareBestDesktop)
    ];

    private readonly List<(Download Download, Button Button, TextBlock Size)> _rows = new();
    private GalleryRelease? _release;

    public GalleryDownloadSection()
    {
        Spacing = 32;
        StackBelow = 900;
        StartShare = 0.44;
        Children.Add(BuildOffer());
        Children.Add(BuildComparison());
        _ = new GalleryFirstSight(this, () => _ = LoadAsync());
    }

    /// <summary>The systems in the order they are offered: the visitor's first.</summary>
    public IEnumerable<string> OfferedRuntimes => _rows.Select(row => row.Download.Runtime);

    /// <summary>Where a download button goes.</summary>
    public Uri UrlOf(string runtime) => _release?.AssetFor(runtime)?.Url ?? _release?.Url ?? GalleryReleases.AllUrl;

    private Control BuildOffer()
    {
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.DownloadEyebrow,
                                               GallerySite.Reveal(GalleryShellLangResourceKind.DownloadTitle),
                                               GalleryShellLangResourceKind.DownloadText));
        // A phone cannot install it: say so before the list, which stays for whoever wants the link.
        if (GalleryHost.ClientOs == GalleryClientOs.Mobile)
        {
            var alert = new Alert { Tone = AlertTone.Info };
            GalleryText.Bind(alert, ContentControl.ContentProperty, GalleryShellLangResourceKind.DownloadMobileNote);
            stack.Children.Add(alert);
        }
        var list = new StackPanel { Spacing = 8 };
        var first = true;
        foreach (var download in s_downloads.OrderByDescending(d => d.Os == GalleryHost.ClientOs))
        {
            list.Children.Add(BuildRow(download, first && download.Os == GalleryHost.ClientOs));
            first = false;
        }
        stack.Children.Add(list);
        stack.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.DownloadNote, ArcDesignTokenKind.TextMuted, 12.5));
        var all = GallerySite.Action(GalleryShellLangResourceKind.DownloadAll, ButtonVariant.Ghost, () => GallerySite.Open(this, _release?.Url ?? GalleryReleases.AllUrl), ArcIcons.ArrowUpRight, ControlSize.Small);
        all.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(all);
        return stack;
    }

    private Button BuildRow(Download download, bool isPrimary)
    {
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        GalleryText.Bind(name, TextBlock.TextProperty, download.Name);
        var size = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        var glyph = new ArcGlyph { Data = ArcIcons.Download, Size = 15, VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(name, 1);
        Grid.SetColumn(size, 2);
        row.Children.Add(glyph);
        row.Children.Add(name);
        row.Children.Add(size);
        var button = new Button
        {
            Variant = isPrimary ? ButtonVariant.Primary : ButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = row
        };
        GalleryText.Bind(button, AutomationProperties.NameProperty, download.Name);
        button.Click += (_, _) => GallerySite.Open(this, UrlOf(download.Runtime));
        _rows.Add((download, button, size));
        return button;
    }

    private async Task LoadAsync()
    {
        _release = await GalleryReleases.Provider();
        if (_release is null)
        {
            return;
        }
        foreach (var (download, _, size) in _rows)
        {
            if (_release.AssetFor(download.Runtime) is { } asset)
            {
                size.Text = GallerySite.FormatSize(asset.Size);
            }
        }
    }

    private static Control BuildComparison()
    {
        var rows = new StackPanel { Spacing = 0 };
        foreach (var (row, web, desktop) in s_comparison)
        {
            var cells = new GalleryTwoPane { StackBelow = 460, Spacing = 14 };
            cells.Children.Add(Cell(GalleryShellLangResourceKind.CompareWeb, web, ArcDesignTokenKind.TextSecondary));
            cells.Children.Add(Cell(GalleryShellLangResourceKind.CompareDesktop, desktop, ArcDesignTokenKind.Foreground));

            var stack = new StackPanel { Spacing = 8, Margin = new Thickness(16, 12) };
            stack.Children.Add(GallerySite.Text(row, ArcDesignTokenKind.TextMuted, 12, FontWeight.Medium));
            stack.Children.Add(cells);

            var line = new Border { BorderThickness = new Thickness(0, rows.Children.Count == 0 ? 0 : 1, 0, 0), Child = stack };
            ArcTokens.Bind(line, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            rows.Children.Add(line);
        }
        var panel = GallerySite.Panel(rows, padding: 0);
        panel.ClipToBounds = true;
        panel.VerticalAlignment = VerticalAlignment.Top;
        return panel;
    }

    private static Control Cell(GalleryShellLangResourceKind host, GalleryShellLangResourceKind text, ArcDesignTokenKind color)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(GallerySite.Text(host, ArcDesignTokenKind.TextMuted, 11.5));
        stack.Children.Add(GallerySite.Text(text, color, 13.5));
        return stack;
    }

    private sealed record Download(string Runtime, GalleryClientOs Os, GalleryShellLangResourceKind Name);
}

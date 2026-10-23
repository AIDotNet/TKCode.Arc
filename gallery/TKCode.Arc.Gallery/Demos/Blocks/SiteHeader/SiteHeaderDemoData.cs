// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli): the preview's sample page and copy.
// Arc's photographs are not part of its MIT registry, so the picture here is drawn in code.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Sample data of the SiteHeader demos: the feature picture and the example sets.</summary>
public static class SiteHeaderDemoData
{
    /// <summary>What the feature picture shows (the card's image description).</summary>
    public const string FacadeDescription = "A white tiled building facade with curved balconies";

    private const int PictureWidth = 640;
    private const int PictureHeight = 400;

    private static RenderTargetBitmap? s_facade;

    /// <summary>A white, softly lit facade of curved balconies, drawn in code (16:10).</summary>
    public static IImage Facade => s_facade ??= DrawFacade();

    /// <summary>Arc's example items with the feature picture on the Product card.</summary>
    public static List<SiteHeaderItem> ExampleItems()
    {
        var items = SiteHeader.CreateExampleItems().ToList();
        if (items.Count > 0 && items[0].Feature is { } feature)
        {
            feature.Image = Facade;
            feature.ImageDescription = FacadeDescription;
        }
        return items;
    }

    /// <summary>Three plain links with addresses, for the simple example.</summary>
    public static List<SiteHeaderItem> DocsItems() =>
    [
        new SiteHeaderItem { Value = "guides", Label = "Guides", Href = "https://example.com/guides" },
        new SiteHeaderItem { Value = "reference", Label = "Reference", Href = "https://example.com/reference" },
        new SiteHeaderItem { Value = "blog", Label = "Blog", Href = "https://example.com/blog" }
    ];

    /// <summary>A mega item set for a different product: two panels with links, one with a feature card.</summary>
    public static List<SiteHeaderItem> StudioItems()
    {
        var items = ExampleItems();
        items[0].Label = "Studio";
        items[0].Value = "studio";
        items[0].Feature = new SiteHeaderFeature
        {
            Title = "Spring release",
            Description = "Faster previews and a calmer inspector.",
            Href = "https://example.com/release",
            Image = Facade,
            ImageDescription = FacadeDescription
        };
        return items;
    }

    /// <summary>The soft brand wash behind the sample page's title (Arc's soft gradient, 135 degrees).</summary>
    public static IBrush SoftWash(bool isDark) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(isDark ? Oklch.ToColor(0.235, 0.016, 300) : Oklch.ToColor(0.975, 0.016, 300), 0),
            new GradientStop(isDark ? Oklch.ToColor(0.24, 0.014, 65) : Oklch.ToColor(0.98, 0.016, 65), 1)
        }
    };

    /// <summary>
    /// A small page that scrolls under <paramref name="header"/>, like Arc's preview: a 520 high frame with a title over the
    /// soft wash (it slides under the header) and six rows of three blocks. The frame is the header's scroll source.
    /// </summary>
    /// <param name="header">The header to put on top.</param>
    /// <param name="height">The frame's height.</param>
    /// <param name="title">The block that shows the section name.</param>
    public static Border CreateFrame(SiteHeader header, double height, out TextBlock title)
    {
        title = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Text = "Product" };
        ArcTokens.Bind(title, TextBlock.FontSizeProperty, ArcDesignTokenKind.Type3XL);
        ArcTokens.Bind(title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        var hero = new Border { Padding = new Thickness(24, 128, 24, 72), Child = title };
        var rows = new StackPanel { Spacing = 12, Margin = new Thickness(24) };
        var grids = new List<Grid>();
        for (var i = 0; i < 6; i++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
            for (var c = 0; c < 3; c++)
            {
                var block = new Border { Height = 120, CornerRadius = new CornerRadius(16) };
                ArcTokens.Bind(block, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
                Grid.SetColumn(block, c);
                row.Children.Add(block);
            }
            grids.Add(row);
            rows.Children.Add(row);
        }
        var page = new StackPanel { Margin = new Thickness(0, -65, 0, 0) };
        page.Children.Add(hero);
        page.Children.Add(rows);
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(page);
        var scroll = new ScrollViewer { Content = stack };
        var frame = new Border
        {
            Height = height,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Child = scroll
        };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        void Wash() => hero.Background = SoftWash(frame.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark);
        frame.AttachedToVisualTree += (_, _) => Wash();
        frame.ActualThemeVariantChanged += (_, _) => Wash();
        // A frame 520 wide or less shows one block per row and a tighter title (Arc's container query).
        frame.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width <= 520;
            hero.Padding = narrow ? new Thickness(16, 112, 16, 56) : new Thickness(24, 128, 24, 72);
            foreach (var grid in grids)
            {
                Grid.SetColumnSpan(grid.Children[0], narrow ? 3 : 1);
                for (var c = 1; c < grid.Children.Count; c++)
                {
                    grid.Children[c].IsVisible = !narrow;
                }
            }
        };
        return frame;
    }

    private static RenderTargetBitmap DrawFacade()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(PictureWidth, PictureHeight), new Vector(96, 96));
        using var context = bitmap.CreateDrawingContext();
        const double w = PictureWidth;
        const double h = PictureHeight;
        context.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.1, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.9, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xE8, 0xE9, 0xEC), 0),
                new GradientStop(Color.FromRgb(0xC9, 0xCC, 0xD2), 1)
            }
        }, null, new Rect(0, 0, w, h));

        // Curved balconies: each band is a wide S curve, lit from the left, with a thin shadow line under it.
        for (var band = 0; band < 6; band++)
        {
            var top = 20 + band * 62;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(-20, top + 30), true);
                g.CubicBezierTo(new Point(w * 0.25, top - 40 + band * 6), new Point(w * 0.45, top + 90), new Point(w * 0.72, top + 14));
                g.CubicBezierTo(new Point(w * 0.85, top - 14), new Point(w * 0.95, top + 20), new Point(w + 20, top + 6));
                g.LineTo(new Point(w + 20, top + 38));
                g.CubicBezierTo(new Point(w * 0.95, top + 52), new Point(w * 0.85, top + 22), new Point(w * 0.72, top + 50));
                g.CubicBezierTo(new Point(w * 0.45, top + 128), new Point(w * 0.25, top - 4 + band * 6), new Point(-20, top + 66));
                g.EndFigure(true);
            }
            var light = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xFB, 0xFA, 0xF8), 0),
                    new GradientStop(Color.FromRgb(0xEC, 0xE8, 0xE2), 1)
                }
            };
            context.DrawGeometry(light, new Pen(new SolidColorBrush(Color.FromArgb(70, 90, 86, 80)), 1.2), geometry);
        }

        // Tile joints: a faint grid across the whole facade.
        var joint = new Pen(new SolidColorBrush(Color.FromArgb(34, 70, 70, 76)), 1);
        for (var x = 0; x <= w; x += 40)
        {
            context.DrawLine(joint, new Point(x, 0), new Point(x, h));
        }
        for (var y = 0; y <= h; y += 40)
        {
            context.DrawLine(joint, new Point(0, y), new Point(w, y));
        }
        return bitmap;
    }
}

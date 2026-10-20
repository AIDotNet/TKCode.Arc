using AtomUI.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class CarouselPage
{
    private sealed record Suite(string Name, string Detail, int Scene, string? Photo = null);

    private static readonly Suite[] s_suites =
    [
        new("Harbour suite", "Sea view · 42 m² · from €310", 0, "https://picsum.photos/seed/arc-harbour/680/440"),
        new("Garden loft", "Courtyard · 36 m² · from €240", 5, "https://picsum.photos/seed/arc-garden/680/440"),
        new("Lighthouse room", "Cliff edge · 28 m² · from €190", 3, "https://picsum.photos/seed/arc-lighthouse/680/440"),
        new("Dune studio", "Beach access · 31 m² · from €220", 2, "https://picsum.photos/seed/arc-dune/680/440"),
        new("Night terrace", "Rooftop · 54 m² · from €420", 4, "https://picsum.photos/seed/arc-terrace/680/440")
    ];

    public static Control Create()
    {
        var basic = new Carousel
        {
            Label = "From the Harbour suites",
            ItemsSource = s_suites,
            ItemTemplate = new FuncDataTemplate<Suite>((suite, _) => suite is null ? null : Slide(suite, photos: false))
        };

        var rotating = new Carousel
        {
            Label = "Seasonal offers",
            Interval = TimeSpan.FromSeconds(4),
            ItemsSource = s_suites,
            SelectedIndex = 2,
            ItemTemplate = new FuncDataTemplate<Suite>((suite, _) => suite is null ? null : Slide(suite, photos: false))
        };

        var numbers = Enumerable.Range(1, 120).ToList();
        var readout = GalleryUi.Mono("Slide 1 of 120");
        var longCarousel = new Carousel
        {
            Label = "Archive",
            ItemsSource = numbers,
            SlideLabelFormatter = (index, count) => $"Archive photo {index + 1} of {count}",
            ItemTemplate = new FuncDataTemplate<int>((number, _) => Frame(new MediaArt { Scene = number, Badge = number.ToString() }, 180))
        };
        longCarousel.SelectionChanged += (_, _) => readout.Text = $"Slide {longCarousel.SelectedIndex + 1} of {numbers.Count}";

        var photos = new Carousel
        {
            Label = "Suites, photographed",
            ItemsSource = s_suites,
            ItemTemplate = new FuncDataTemplate<Suite>((suite, _) => suite is null ? null : Slide(suite, photos: true))
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Slides",
                "Drag, flick, swipe a trackpad or use ← / → and Home / End. The active slide sits in the center while its neighbours shrink and dim with distance; the indicator pill travels with the track. Tap a neighbour to bring it in.",
                basic),
            GalleryUi.Card("Rotation",
                "With an interval a play control appears; the pill drains while a slide is on show. Rotation pauses on hover, keyboard focus and drag, while scrolled out of view or minimized, and is unavailable with reduced motion.",
                rotating),
            GalleryUi.Card("Long carousels",
                "120 slides: only the slides around the current position are realized.",
                GalleryUi.Column(12, longCarousel, readout)),
            GalleryUi.Card("Photos",
                "Slides with AsyncImage photos over a drawn scene, so they still look complete offline.",
                photos));
    }

    private static Control Slide(Suite suite, bool photos)
    {
        var art = new Panel { Height = 200 };
        art.Children.Add(new MediaArt { Scene = suite.Scene });
        if (photos && suite.Photo is { } url)
        {
            art.Children.Add(new AsyncImage { Source = ImageSource.Parse(url), Stretch = Stretch.UniformToFill });
        }
        var text = GalleryUi.Column(4,
            GalleryUi.Text(suite.Name, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium),
            GalleryUi.Text(suite.Detail, ArcDesignTokenKind.TextSecondary, 14));
        text.Margin = new Thickness(18, 14, 18, 18);
        var content = new StackPanel();
        content.Children.Add(art);
        content.Children.Add(text);
        return Frame(content, double.NaN);
    }

    private static Border Frame(Control content, double height)
    {
        var frame = new Border { BorderThickness = new Thickness(1), ClipToBounds = true, Child = content, Height = height };
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(frame, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
        return frame;
    }
}

using Avalonia.Controls;
using Avalonia.Data.Converters;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class CarouselLongCarouselsDemo : UserControl
{
    private readonly List<int> _slides = Enumerable.Range(1, 120).ToList();

    public CarouselLongCarouselsDemo()
    {
        InitializeComponent();
        Archive.SlideLabelFormatter = (index, count) => $"Archive photo {index + 1} of {count}";
        Archive.ItemsSource = _slides;
    }

    /// <summary>
    /// Draws a numbered landscape. MediaArt is the gallery's stand-in for photography and its Scene and Badge are not
    /// bindable, so the template reaches it through this converter.
    /// </summary>
    public static IValueConverter NumberedArt { get; } =
        new FuncValueConverter<int, Control?>(number => new MediaArt { Scene = number, Badge = number.ToString() });

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Readout.Text = $"Slide {Archive.SelectedIndex + 1} of {_slides.Count}";
    }
}

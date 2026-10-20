using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The slide track of a <see cref="Carousel"/>: a horizontal row of equal-width slides with the start and end
/// padding that centers the first and last slide. The track offset is a render transform, and each slide's
/// scale and opacity are written straight from the track position, so dragging and gliding never re-layout.
/// Only the slides near the current position are realized; long carousels keep a handful of containers.
/// </summary>
internal sealed class CarouselTrackPanel : MediaVirtualPanel
{
    private readonly TranslateTransform _translate = new();
    private int _low;
    private int _high = -1;
    private double _height;
    private double _width;
    private bool _measuring;

    public CarouselTrackPanel()
    {
        RenderTransform = _translate;
        ClipToBounds = false;
    }

    private Carousel? Owner => ItemsControl as Carousel;

    /// <summary>Indices currently realized (inclusive range; empty when high &lt; low).</summary>
    internal (int Low, int High) RealizedRange => (_low, _high);

    /// <summary>Number of realized slide containers.</summary>
    internal int RealizedCount => Realized.Count;

    protected override bool ShouldRealize(int index)
        => (index >= _low && index <= _high) || index == (Owner?.SelectedIndex ?? -1);

    protected override void OnItemsReset()
    {
        _height = 0;
        Owner?.OnItemsReset();
    }

    private (int Low, int High) WindowFor(double progress, double viewport, double step, int count)
    {
        if (count == 0)
        {
            return (0, -1);
        }
        // Everything visible in the viewport plus one slide either side.
        var radius = step > 0 ? (int)Math.Ceiling(viewport / 2 / step) + 1 : count;
        var low = Math.Max(0, (int)Math.Floor(progress) - radius);
        var high = Math.Min(count - 1, (int)Math.Ceiling(progress) + radius);
        return (low, high);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var count = ItemCount;
        if (owner is null)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 400;
        _width = width;
        _measuring = true;
        try
        {
            owner.OnTrackMeasured(width);
        }
        finally
        {
            _measuring = false;
        }
        var slideWidth = owner.SlideWidthPx;
        (_low, _high) = WindowFor(owner.Progress, width, owner.Step, count);
        RecycleUnwanted();
        var height = 0.0;
        for (var i = _low; i <= _high; i++)
        {
            var slide = Realize(i);
            if (slide is null)
            {
                continue;
            }
            slide.Measure(new Size(slideWidth, double.PositiveInfinity));
            height = Math.Max(height, slide.DesiredSize.Height);
        }
        var selected = owner.SelectedIndex;
        if (selected >= 0 && selected < count && (selected < _low || selected > _high) && Realize(selected) is { } current)
        {
            current.Measure(new Size(slideWidth, double.PositiveInfinity));
            height = Math.Max(height, current.DesiredSize.Height);
        }
        // Slides share the tallest height seen, so realizing a shorter neighbour never makes the track jump.
        _height = Math.Max(_height, height);
        return new Size(width, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = Owner;
        if (owner is null)
        {
            return finalSize;
        }
        var slideWidth = owner.SlideWidthPx;
        var pad = (finalSize.Width - slideWidth) / 2;
        foreach (var pair in Realized)
        {
            pair.Value.Arrange(new Rect(pad + pair.Key * owner.Step, 0, slideWidth, finalSize.Height));
        }
        UpdateTrack();
        return finalSize;
    }

    protected override void OnRealized(Control container, int index)
    {
        Owner?.PrepareSlide(container, index);
    }

    /// <summary>Applies the track offset and every slide's scale and opacity; re-realizes when the window moves.</summary>
    internal void UpdateTrack()
    {
        var owner = Owner;
        if (owner is null)
        {
            return;
        }
        _translate.X = owner.TrackOffset;
        var progress = owner.Progress;
        foreach (var pair in Realized)
        {
            if (pair.Value is CarouselSlide slide)
            {
                slide.SetDistance(progress - pair.Key);
            }
        }
        if (_measuring)
        {
            return;
        }
        var window = WindowFor(progress, _width, owner.Step, ItemCount);
        if (window != (_low, _high))
        {
            InvalidateMeasure();
        }
    }

    /// <summary>The realized slide container at <paramref name="index"/>, if any.</summary>
    internal Control? SlideAt(int index) => Realized.TryGetValue(index, out var slide) ? slide : null;

    internal SortedDictionary<int, Control> RealizedSlides => Realized;
}

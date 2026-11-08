using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The player's outer frame: its height follows its width at <see cref="Aspect"/> (width ÷ height), whatever its child is,
/// so it also works as the same-size placeholder while the player's root is in the full-screen overlay (spec section 10).
/// With no finite width it uses the finite height, and with neither it is 640 wide.
/// </summary>
internal sealed class VideoPlayerFrame : Panel
{
    /// <summary>Width divided by height (16:9 by default).</summary>
    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<VideoPlayerFrame, double>(nameof(Aspect), 16.0 / 9.0);

    /// <summary>Height added below the picture (the control bar in the browser's Native presentation); 0 by default.</summary>
    public static readonly StyledProperty<double> ExtraHeightProperty =
        AvaloniaProperty.Register<VideoPlayerFrame, double>(nameof(ExtraHeight));

    static VideoPlayerFrame()
    {
        AffectsMeasure<VideoPlayerFrame>(AspectProperty, ExtraHeightProperty);
    }

    /// <inheritdoc cref="AspectProperty"/>
    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    /// <inheritdoc cref="ExtraHeightProperty"/>
    public double ExtraHeight { get => GetValue(ExtraHeightProperty); set => SetValue(ExtraHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var aspect = double.IsFinite(Aspect) && Aspect > 0.05 ? Aspect : 16.0 / 9.0;
        var extra = double.IsFinite(ExtraHeight) && ExtraHeight > 0 ? ExtraHeight : 0;
        double width;
        double height;
        if (!double.IsInfinity(availableSize.Width))
        {
            width = availableSize.Width;
            height = width / aspect + extra;
        }
        else if (!double.IsInfinity(availableSize.Height))
        {
            height = availableSize.Height;
            width = Math.Max(0, height - extra) * aspect;
        }
        else
        {
            width = 640;
            height = width / aspect + extra;
        }
        var size = new Size(width, height);
        foreach (var child in Children)
        {
            child.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }
}

/// <summary>The control bar (spec section 5): the seek bar over the button row. The theme fades and lifts it in and out.</summary>
internal sealed class VideoControlBar : Border
{
    protected override Type StyleKeyOverride => typeof(VideoControlBar);
}

/// <summary>The gradient behind the control bar and the full-screen title; fades with them.</summary>
internal sealed class VideoScrim : Border
{
    /// <summary>Defines the <see cref="Tint"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<VideoScrim, IBrush?>(nameof(Tint));

    /// <summary>Defines the <see cref="FromTop"/> property.</summary>
    public static readonly StyledProperty<bool> FromTopProperty =
        AvaloniaProperty.Register<VideoScrim, bool>(nameof(FromTop));

    /// <summary>The color at the dark end (color tokens resolve to solid brushes).</summary>
    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Whether the dark end is at the top (the full-screen title) instead of the bottom (the control bar).</summary>
    public bool FromTop
    {
        get => GetValue(FromTopProperty);
        set => SetValue(FromTopProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(VideoScrim);

    // The brush is made here: a token resource on a GradientStop in the theme does not resolve (a stop is not a control, so it
    // stayed transparent), while one on a property of the control does.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TintProperty || change.Property == FromTopProperty)
        {
            var dark = (Tint as ISolidColorBrush)?.Color ?? default;
            var clear = Color.FromArgb(0, dark.R, dark.G, dark.B);
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(FromTop ? dark : clear, 0),
                    new GradientStop(FromTop ? clear : dark, 1),
                },
            };
        }
    }
}

/// <summary>Hosts the player's root in the full-screen overlay, filling the overlay (spec section 10).</summary>
internal sealed class VideoFullScreenHost : Panel
{
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a <see cref="MediaPlayerSwap"/> replaces its content (Arc Media player, design reference).</summary>
internal enum MediaPlayerSwapKind
{
    /// <summary>The cover: old and new crossfade over 240 ms on the standard curve, nothing moves.</summary>
    Cover,

    /// <summary>The title block: the old one lifts by <see cref="MediaPlayerSwap.Travel"/> and fades while the new one rises into place, on the Smooth spring.</summary>
    Title,

    /// <summary>The play / pause glyph: old and new crossfade over 120 ms while scaling between 0.6 and 1 through a 2px blur.</summary>
    Icon,
}

/// <summary>
/// Shows one child and animates the change to the next: the incoming and outgoing children overlap and move on one
/// progress value, and the outgoing child is removed when it finishes. Only the incoming child sizes the panel. Reduced
/// and Off motion swap at once. Code-driven because the swap is one shared progress for two children that may be
/// interrupted midway (a quick double skip): the next swap starts from the outgoing child's current opacity.
/// </summary>
internal sealed class MediaPlayerSwap : Panel
{
    /// <summary>How the content changes.</summary>
    public static readonly StyledProperty<MediaPlayerSwapKind> KindProperty =
        AvaloniaProperty.Register<MediaPlayerSwap, MediaPlayerSwapKind>(nameof(Kind));

    /// <summary>How far the title travels (10).</summary>
    public static readonly StyledProperty<double> TravelProperty =
        AvaloniaProperty.Register<MediaPlayerSwap, double>(nameof(Travel), 10);

    private Control? _current;
    private Control? _outgoing;
    private double _outgoingFrom = 1;
    private MotionValue<double>? _progress;
    private int _generation;

    /// <inheritdoc cref="KindProperty"/>
    public MediaPlayerSwapKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TravelProperty"/>
    public double Travel { get => GetValue(TravelProperty); set => SetValue(TravelProperty, value); }

    /// <summary>The child being shown (the incoming one during a swap).</summary>
    public Control? Current => _current;

    /// <summary>Whether a swap is running.</summary>
    public bool IsSwapping => _outgoing is not null;

    /// <summary>Shows <paramref name="next"/>; animates when <paramref name="animate"/> and the motion level is Full.</summary>
    public void Show(Control? next, bool animate)
    {
        if (ReferenceEquals(next, _current))
        {
            return;
        }
        _generation++;
        _progress?.Stop();
        if (_outgoing is not null)
        {
            Children.Remove(_outgoing);
            _outgoing = null;
        }
        var previous = _current;
        _current = next;
        var full = animate && previous is not null && next is not null && this.IsAttachedToVisualTree() && ArcMotion.IsFull(this);
        if (!full)
        {
            if (previous is not null)
            {
                Children.Remove(previous);
            }
            if (next is not null)
            {
                Reset(next);
                Children.Add(next);
            }
            return;
        }
        _outgoing = previous;
        _outgoingFrom = previous!.Opacity;
        Children.Add(next!);
        _progress ??= CreateProgress();
        _progress.Jump(0);
        Apply(0);
        var generation = _generation;
        var spec = Kind switch
        {
            MediaPlayerSwapKind.Title => MotionSpec.Spring(ArcSpring.Smooth),
            MediaPlayerSwapKind.Cover => MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard),
            _ => MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard),
        };
        _ = Finish(_progress.AnimateTo(1, spec), generation);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = default(Size);
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            if (ReferenceEquals(child, _current))
            {
                desired = child.DesiredSize;
            }
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var rect = Kind switch
            {
                MediaPlayerSwapKind.Cover => new Rect(finalSize),
                // The old title keeps its own height and stays pinned to the top, so it never jumps as it leaves.
                MediaPlayerSwapKind.Title => new Rect(0, 0, finalSize.Width, size.Height),
                _ => new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height),
            };
            child.Arrange(rect);
        }
        return finalSize;
    }

    private async Task Finish(Task<bool> animation, int generation)
    {
        await animation;
        if (generation != _generation)
        {
            return;
        }
        if (_outgoing is not null)
        {
            Children.Remove(_outgoing);
            _outgoing = null;
        }
        if (_current is not null)
        {
            Reset(_current);
        }
    }

    private MotionValue<double> CreateProgress()
    {
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        progress.Changed += Apply;
        return progress;
    }

    private void Apply(double p)
    {
        if (_current is { } incoming)
        {
            Place(incoming, p, entering: true);
        }
        if (_outgoing is { } outgoing)
        {
            Place(outgoing, p, entering: false);
        }
    }

    private void Place(Control child, double p, bool entering)
    {
        var shown = entering ? p : _outgoingFrom * (1 - p);
        child.Opacity = shown;
        switch (Kind)
        {
            case MediaPlayerSwapKind.Title:
            {
                // Transforms are reused across frames: only their values change.
                if (child.RenderTransform is not TranslateTransform translate)
                {
                    translate = new TranslateTransform();
                    child.RenderTransform = translate;
                }
                translate.Y = entering ? Travel * (1 - p) : -Travel * p;
                break;
            }
            case MediaPlayerSwapKind.Icon:
            {
                var amount = entering ? 1 - p : p;
                if (child.RenderTransform is not ScaleTransform scale)
                {
                    scale = new ScaleTransform();
                    child.RenderTransform = scale;
                }
                scale.ScaleX = scale.ScaleY = 1 - 0.4 * amount;
                child.Effect = ArcBlur.Get(2 * amount);
                break;
            }
        }
    }

    private static void Reset(Control child)
    {
        child.Opacity = 1;
        child.RenderTransform = null;
        child.Effect = null;
        child.RenderTransformOrigin = RelativePoint.Center;
    }
}

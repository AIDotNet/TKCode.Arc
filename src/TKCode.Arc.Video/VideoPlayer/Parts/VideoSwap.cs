using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Shows one glyph and animates the change to the next (the play / pause button, spec section 8): the two layers crossfade
/// while the incoming one scales from 0.8 to 1 on the snappy spring, and the outgoing one shrinks to 0.8. Reduced and Off
/// motion swap at once. Copied in shape from the Blocks MediaPlayer's swap (internal there, ADR 0036 decision 5).
/// Code-driven because one progress value moves two children and a quick double toggle must start from where the
/// interrupted swap left the outgoing glyph, which a ControlTheme transition cannot express.
/// </summary>
internal sealed class VideoSwap : Panel
{
    private Control? _current;
    private Control? _outgoing;
    private double _outgoingFrom = 1;
    private MotionValue<double>? _progress;
    private int _generation;

    /// <summary>The child being shown (the incoming one during a swap).</summary>
    public Control? Current => _current;

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
        _ = Finish(_progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy)), _generation);
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
            child.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
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
        child.Opacity = Math.Clamp(entering ? p : _outgoingFrom * (1 - p), 0, 1);
        var amount = Math.Clamp(entering ? 1 - p : p, 0, 1);
        // The transform is reused across frames: only its values change.
        if (child.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform();
            child.RenderTransform = scale;
            child.RenderTransformOrigin = RelativePoint.Center;
        }
        scale.ScaleX = scale.ScaleY = 1 - 0.2 * amount;
    }

    private static void Reset(Control child)
    {
        child.Opacity = 1;
        child.RenderTransform = null;
        child.RenderTransformOrigin = RelativePoint.Center;
    }
}

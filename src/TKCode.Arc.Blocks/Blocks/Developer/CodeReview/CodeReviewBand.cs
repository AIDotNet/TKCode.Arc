using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// A band in the flow of the diff (a comment box, a thread, the approval status): its height follows its content on the
/// block's spring, so the lines below slide instead of jumping (Arc animates height and opacity on the smooth spring).
/// Only a change of content animates; a new width (a resize that rewraps the text) and the first measure jump. Collapsing
/// (<see cref="IsExpanded"/> false) springs to zero and raises <see cref="Collapsed"/>. The content fades in when the band
/// opens and out when it folds. Clips only while it moves or is folded, so focus rings stay whole at rest.
/// </summary>
internal sealed class CodeReviewBand : Decorator
{
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private double _current;
    private double _target = -1;
    private double _lastWidth = double.NaN;
    private bool _expanded = true;
    private bool _measured;
    private bool _opensOnAttach;

    /// <summary>Raised when a fold reached zero height.</summary>
    public event Action<CodeReviewBand>? Collapsed;

    /// <summary>Whether the band shows its content; false springs it shut.</summary>
    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }
            _expanded = value;
            Fade(value);
            InvalidateMeasure();
        }
    }

    /// <summary>True while the height springs.</summary>
    public bool IsMoving => _height?.IsAnimating == true;

    /// <summary>A band that starts shut and opens once it is shown.</summary>
    public static CodeReviewBand Opening(Control content)
    {
        var band = new CodeReviewBand { Child = content, _expanded = false, _opensOnAttach = true };
        content.Opacity = 0;
        return band;
    }

    /// <summary>Jumps to the content's height with no animation (the next layout).</summary>
    public void Settle()
    {
        _measured = false;
        _height?.Stop();
        InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_opensOnAttach)
        {
            _opensOnAttach = false;
            // Opening(): open on the next layout pass, so the band grows from zero in place.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsExpanded = true, Avalonia.Threading.DispatcherPriority.Render);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var content = _expanded ? child.DesiredSize.Height : 0;
        if (!_measured)
        {
            _measured = true;
            Jump(content);
        }
        else if (Math.Abs(content - _target) > 0.25)
        {
            var passive = !double.IsNaN(_lastWidth) && Math.Abs(availableSize.Width - _lastWidth) > 0.25 && !IsMoving;
            if (passive || VisualRoot is null || !ArcMotion.IsFull(this))
            {
                Jump(content);
                if (!_expanded)
                {
                    Collapsed?.Invoke(this);
                }
            }
            else
            {
                Animate(content);
            }
        }
        _lastWidth = availableSize.Width;
        return new Size(child.DesiredSize.Width, _current);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is not null)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(child.DesiredSize.Height, finalSize.Height)));
            ClipToBounds = IsMoving || _current < child.DesiredSize.Height - 0.5;
        }
        return finalSize;
    }

    private void Jump(double value)
    {
        _target = value;
        _current = value;
        _height?.Jump(value);
    }

    private void Animate(double target)
    {
        _target = target;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, _current, MotionVectors.Double);
            _height.Changed += v =>
            {
                _current = Math.Max(0, v);
                InvalidateMeasure();
            };
        }
        _ = Run(target);
    }

    private async Task Run(double target)
    {
        var done = await _height!.AnimateTo(target, MotionSpec.Spring(CodeReviewMotion.Spring));
        InvalidateMeasure();
        if (done && !_expanded && target <= 0)
        {
            Collapsed?.Invoke(this);
        }
    }

    private void Fade(bool show)
    {
        if (Child is not { } child)
        {
            return;
        }
        if (VisualRoot is null || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            child.Opacity = show ? 1 : 0;
            return;
        }
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, child.Opacity, MotionVectors.Double);
            _fade.Changed += v =>
            {
                if (Child is { } c)
                {
                    c.Opacity = Math.Clamp(v, 0, 1);
                }
            };
        }
        _ = _fade.AnimateTo(show ? 1 : 0, MotionSpec.Tween(show ? CodeReviewMotion.FadeIn : TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty)
        {
            _measured = _measured && change.NewValue is not null;
        }
    }

    /// <summary>The current height (tests).</summary>
    internal double CurrentHeight => _current;
}

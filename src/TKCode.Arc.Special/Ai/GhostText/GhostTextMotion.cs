using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The motion of <see cref="GhostText"/>, as the spec gives it.</summary>
internal static class GhostTextMotion
{
    /// <summary>Each streamed chunk fades in, opacity only.</summary>
    public static readonly TimeSpan ChunkFadeIn = TimeSpan.FromMilliseconds(140);

    /// <summary>Escape and leaving the field fade the ghost out.</summary>
    public static readonly TimeSpan FadeOut = TimeSpan.FromMilliseconds(110);

    /// <summary>Accepted text eases from the ghost ink to the text ink in place.</summary>
    public static readonly TimeSpan InkMerge = TimeSpan.FromMilliseconds(200);

    /// <summary>The field's height follows the text, ghost included.</summary>
    public static readonly TimeSpan Height = TimeSpan.FromMilliseconds(180);

    /// <summary>The chip's width follows its content, anchored to its right edge.</summary>
    public static readonly TimeSpan ChipWidth = TimeSpan.FromMilliseconds(200);

    /// <summary>Chip states crossfade: the old one out in 100ms, the new one in over 160ms.</summary>
    public static readonly TimeSpan ChipOut = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan ChipIn = TimeSpan.FromMilliseconds(160);

    /// <summary>How long a key cap stays pressed when its key or handle method fires.</summary>
    public static readonly TimeSpan KeyPress = TimeSpan.FromMilliseconds(160);

    /// <summary>How long the chip reads "Accepted" before it returns to the model's name (interpretation).</summary>
    public static readonly TimeSpan AcceptedHold = TimeSpan.FromMilliseconds(1400);

    /// <summary>Below this field width the key hints step aside and only the chip remains.</summary>
    public const double NarrowWidth = 520;
}

/// <summary>
/// A box whose width or height follows its child on a plain tween (the field's height on 180ms, the chip's width on 200ms),
/// never a spring, so it cannot bounce. The child keeps its own size and is clipped while the box catches up: the height
/// box pins it to the top (the text never scrolls under the caret mid-tween), the width box to the right edge. Only with
/// full motion, attached and after the first layout; otherwise the size jumps. Code-driven because the target is a measured
/// size, which a ControlTheme transition cannot follow.
/// </summary>
internal sealed class GhostTextSizeBox : Decorator
{
    private MotionValue<double>? _size;
    private double _target = double.NaN;
    private bool _measuring;

    public GhostTextSizeBox()
    {
        ClipToBounds = true;
    }

    /// <summary>Animates the height (true) or the width (false).</summary>
    public bool IsVertical { get; set; } = true;

    /// <summary>The tween's duration.</summary>
    public TimeSpan Duration { get; set; } = GhostTextMotion.Height;

    /// <summary>True while the size is moving.</summary>
    public bool IsMoving => _size?.IsAnimating == true;

    /// <summary>The size the box is heading to (the child's).</summary>
    public double TargetSize => _target;

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize);
        var desired = child.DesiredSize;
        var target = IsVertical ? desired.Height : desired.Width;
        if (_size is null)
        {
            _size = new MotionValue<double>(this, target, MotionVectors.Double);
            _size.Changed += _ =>
            {
                if (!_measuring)
                {
                    InvalidateMeasure();
                }
            };
            _target = target;
        }
        else if (Math.Abs(target - _target) > 0.25)
        {
            _target = target;
            _measuring = true;
            try
            {
                if (IsLoaded && ArcMotion.IsFull(this))
                {
                    _ = _size.AnimateTo(target, MotionSpec.Tween(Duration, ArcEasings.Standard));
                }
                else
                {
                    _size.Jump(target);
                }
            }
            finally
            {
                _measuring = false;
            }
        }
        var current = _size.Value;
        return IsVertical ? new Size(desired.Width, current) : new Size(current, desired.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var desired = child.DesiredSize;
            if (IsVertical)
            {
                child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(desired.Height, 0)));
            }
            else
            {
                var width = desired.Width;
                var x = child.HorizontalAlignment == HorizontalAlignment.Left ? 0 : finalSize.Width - width;
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
            }
        }
        return finalSize;
    }
}

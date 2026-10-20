using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One place in an <see cref="AvatarGroup"/> stack: a member's <see cref="Avatar"/> or the "+N" overflow chip. It
/// draws the 1px ring outside the 2px surface border, carries every hover move as a single translate (so the stack
/// never changes size), shows the member's name above itself, and enters / leaves by growing its slot (P19).
/// </summary>
internal sealed class AvatarGroupSlot : Control
{
    private readonly AvatarGroup _group;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private readonly TranslateTransform _tipTranslate = new();
    private Border? _tip;
    private TextBlock? _tipText;
    private ArcContentSwap? _count;
    private MotionValue<double>? _presence;
    private MotionValue<Vector>? _offset;
    private MotionValue<double>? _emphasis;
    private MotionValue<double>? _tipShown;
    private IPen? _ringPen;
    private IPen? _ringHoverPen;
    private IBrush? _ringBrush;
    private IBrush? _ringHoverBrush;

    public AvatarGroupSlot(AvatarGroup group, object key, Control content, bool isOverflow)
    {
        _group = group;
        Key = key;
        Content = content;
        IsOverflow = isOverflow;
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
        LogicalChildren.Add(content);
        VisualChildren.Add(content);
    }

    public object Key { get; }

    /// <summary>Bindings from the member data; disposed when the slot leaves.</summary>
    public List<IDisposable> Bindings { get; } = new();

    public void DisposeBindings()
    {
        foreach (var binding in Bindings)
        {
            binding.Dispose();
        }
        Bindings.Clear();
    }

    public Control Content { get; }

    public bool IsOverflow { get; }

    public bool IsLeaving { get; set; }

    /// <summary>Slot presence: 0 collapsed, 1 open (may overshoot on the morph spring).</summary>
    public double Presence => _presence?.Value ?? 1;

    public Vector Offset => _offset?.Value ?? default;

    public double Emphasis => _emphasis?.Value ?? 0;

    public double TipOpacity => _tipShown?.Value ?? 0;

    public string? TipText { get; set; }

    /// <summary>Creates an overflow chip with a rolling count.</summary>
    public static Border CreateChip(out ArcContentSwap count)
    {
        count = new ArcContentSwap
        {
            Mode = ContentSwapMode.Roll,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAccessibilityView(count, AccessibilityView.Raw);
        var chip = new Border { ClipToBounds = true, Child = count };
        AutomationProperties.SetControlTypeOverride(chip, AutomationControlType.Image);
        return chip;
    }

    public void AttachCount(ArcContentSwap count) => _count = count;

    public void SetCount(int overflow, int direction)
    {
        if (_count is null)
        {
            return;
        }
        _count.Direction = direction;
        _count.Content = "+" + overflow.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Opens (or closes) the slot. The first state of a new group applies without motion.</summary>
    public Task<bool> AnimatePresence(double target, bool animate)
    {
        _presence ??= CreateValue(animate ? 1 - target : target, measure: true);
        if (!animate)
        {
            _presence.Jump(target);
            return Task.FromResult(true);
        }
        // Arc uses the morph spring for the slot width, opacity and the 0.9 scale together.
        return _presence.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
    }

    public void AnimateOffset(Vector target, bool full)
    {
        _offset ??= new MotionValue<Vector>(this, default, MotionVectors.Vector);
        if (_offset.Target == target)
        {
            return;
        }
        _offset.Changed -= ApplyTransforms;
        _offset.Changed += ApplyTransforms;
        if (!full || !IsLoaded)
        {
            _offset.Jump(full ? target : default);
            return;
        }
        _ = _offset.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }

    public void SetEmphasis(bool on, ArcMotionLevel level)
    {
        _emphasis ??= CreateValue(0, measure: false);
        _tipShown ??= CreateValue(0, measure: false);
        if (on)
        {
            EnsureTip();
        }
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            _emphasis.Jump(on ? 1 : 0);
            _tipShown.Jump(on ? 1 : 0);
            return;
        }
        var fast = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _emphasis.AnimateTo(on ? 1 : 0, fast);
        if (level == ArcMotionLevel.Reduced)
        {
            _tipShown.Jump(on ? 1 : 0);
            return;
        }
        _ = _tipShown.AnimateTo(on ? 1 : 0, on
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, TimeSpan.FromMilliseconds(60))
            : fast);
    }

    private void EnsureTip()
    {
        if (_tip is not null || string.IsNullOrEmpty(TipText))
        {
            return;
        }
        _tipText = new TextBlock { Text = TipText, TextWrapping = TextWrapping.NoWrap };
        _tip = new Border { Child = _tipText, IsHitTestVisible = false, Opacity = 0, RenderTransform = _tipTranslate };
        AutomationProperties.SetAccessibilityView(_tip, AccessibilityView.Raw);
        _group.StyleTip(_tip, _tipText);
        LogicalChildren.Add(_tip);
        VisualChildren.Add(_tip);
        InvalidateMeasure();
    }

    private MotionValue<double> CreateValue(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += v =>
        {
            ApplyTransforms(default);
            if (measure)
            {
                (Parent as Layoutable)?.InvalidateMeasure();
                if (IsLeaving && v <= 0)
                {
                    _group.OnSlotClosed(this);
                }
            }
        };
        return value;
    }

    private void ApplyTransforms(Vector _)
    {
        var presence = Presence;
        Opacity = Math.Clamp(presence, 0, 1);
        var scale = 0.9 + 0.1 * presence;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        var offset = Offset;
        _translate.X = offset.X;
        _translate.Y = offset.Y;
        if (_tip is not null)
        {
            var shown = Math.Clamp(TipOpacity, 0, 1);
            _tip.Opacity = shown;
            // The name settles 3px down into place; reduced motion only fades it.
            _tipTranslate.Y = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? 3 * (1 - shown) : 0;
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var d = _group.Diameter;
        Content.Measure(new Size(d, d));
        _tip?.Measure(Size.Infinity);
        return new Size(d, d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var d = _group.Diameter;
        Content.Arrange(new Rect(0, 0, d, d));
        if (_tip is not null)
        {
            var size = _tip.DesiredSize;
            _tip.Arrange(new Rect((d - size.Width) / 2, -8 - size.Height, size.Width, size.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var ring = _group.RingBrush;
        var ringHover = _group.RingHoverBrush;
        if (!ReferenceEquals(ring, _ringBrush))
        {
            _ringBrush = ring;
            _ringPen = ring is null ? null : new Pen(ring, 1);
        }
        if (!ReferenceEquals(ringHover, _ringHoverBrush))
        {
            _ringHoverBrush = ringHover;
            _ringHoverPen = ringHover is null ? null : new Pen(ringHover, 1);
        }
        var d = _group.Diameter;
        var center = new Point(d / 2, d / 2);
        var radius = d / 2 + 0.5;
        var emphasis = Math.Clamp(Emphasis, 0, 1);
        if (_ringPen is not null && emphasis < 1)
        {
            context.DrawEllipse(null, _ringPen, center, radius, radius);
        }
        if (_ringHoverPen is not null && emphasis > 0)
        {
            using (context.PushOpacity(emphasis))
            {
                context.DrawEllipse(null, _ringHoverPen, center, radius, radius);
            }
        }
    }
}

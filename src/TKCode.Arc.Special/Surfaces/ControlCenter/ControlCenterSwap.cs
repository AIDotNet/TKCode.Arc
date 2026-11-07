using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a <see cref="ControlCenterSwap"/> trades its content.</summary>
internal enum ControlCenterSwapKind
{
    /// <summary>
    /// Text "blur roll" (state lines, the mode description): the old line fades and blurs out (2px) in about 120ms while it
    /// rises 8px; the new one comes up from 8px below on a fast spring (ω ≈ 42) and fades and unblurs over 200ms
    /// (0.16, 1, 0.3, 1). Both run at once and the old line leaves the layout at once.
    /// </summary>
    Roll,

    /// <summary>Icon "blur swap": the old glyph shrinks to 0.7, blurs and fades in about 130ms; the new one grows from 0.7 (ω ≈ 14) and unblurs over 150ms.</summary>
    Pop
}

/// <summary>
/// One slot of a <see cref="ControlCenterSwap"/>: a decorator whose opacity, offset, scale and blur the swap writes every
/// frame (a cached matrix transform and the shared blur cache, so a frame allocates nothing).
/// </summary>
internal sealed class ControlCenterSwapSlot : Decorator
{
    private readonly MatrixTransform _transform = new();
    private double _appliedBlur = -1;

    public ControlCenterSwapSlot()
    {
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public object? Key { get; set; }

    public void Apply(double opacity, double y, double scale, double blur)
    {
        Opacity = opacity;
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, y);
        var quantized = Math.Round(blur / ArcBlur.Step) * ArcBlur.Step;
        if (!quantized.Equals(_appliedBlur))
        {
            _appliedBlur = quantized;
            Effect = ArcBlur.Get(quantized);
        }
    }
}

/// <summary>
/// The control center's content swap (blur roll for text, blur swap for icons, measured from Arc Control center). It
/// keeps at most two slots: the current content, measured and in the layout, and the outgoing one, drawn over the same
/// box until it has faded. Blur is attached only while a swap runs. Reduced motion keeps the fade and the blur but drops
/// the travel and the scale; Off swaps at once.
/// <para>Why code: a presence crossfade with a spring on one channel and tweens on the others, retargetable mid-flight, is
/// not something ControlTheme transitions express.</para>
/// </summary>
internal sealed class ControlCenterSwap : Panel
{
    private static readonly Easing s_exitEase = new SplineEasing(0, 0, 0.58, 1);
    private static readonly SpringSpec s_rollSpring = SpringSpec.FromVisual(2 * Math.PI / (1.2 * 42), 0);
    private static readonly SpringSpec s_popSpring = SpringSpec.FromVisual(2 * Math.PI / (1.2 * 14), 0);

    private ControlCenterSwapSlot? _current;
    private ControlCenterSwapSlot? _outgoing;
    private readonly MotionValue<double> _in;
    private readonly MotionValue<double> _travel;
    private readonly MotionValue<double> _out;
    private bool _full;

    public ControlCenterSwap()
    {
        ClipToBounds = false;
        _in = new MotionValue<double>(this, 1, MotionVectors.Double);
        _travel = new MotionValue<double>(this, 0, MotionVectors.Double);
        _out = new MotionValue<double>(this, 1, MotionVectors.Double);
        _in.Changed += _ => ApplyFrame();
        _travel.Changed += _ => ApplyFrame();
        _out.Changed += _ => ApplyFrame();
    }

    public ControlCenterSwapKind Kind { get; set; } = ControlCenterSwapKind.Roll;

    /// <summary>The key of the content now showing.</summary>
    public object? CurrentKey => _current?.Key;

    /// <summary>The content now showing.</summary>
    public Control? Current => _current?.Child;

    /// <summary>True while an outgoing content is still on screen.</summary>
    public bool IsSwapping => _outgoing is not null;

    /// <summary>
    /// Shows <paramref name="content"/> (identified by <paramref name="key"/>; the same key again does nothing). The first
    /// content, or <paramref name="animate"/> false, appears at once.
    /// </summary>
    public void Show(object key, Func<Control> content, bool animate = true)
    {
        if (_current is not null && Equals(_current.Key, key))
        {
            return;
        }
        var slot = new ControlCenterSwapSlot { Key = key, Child = content() };
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is null || !animate || level == ArcMotionLevel.Off || VisualRoot is null)
        {
            ClearOutgoing();
            if (_current is not null)
            {
                Children.Remove(_current);
            }
            _current = slot;
            Children.Add(slot);
            _in.Jump(1);
            _travel.Jump(0);
            _out.Jump(1);
            slot.Apply(1, 0, 1, 0);
            return;
        }
        ClearOutgoing();
        _outgoing = _current;
        _current = slot;
        Children.Add(slot);
        _full = level == ArcMotionLevel.Full;
        _in.Jump(0);
        _out.Jump(0);
        _travel.Jump(1);
        ApplyFrame();
        var pop = Kind == ControlCenterSwapKind.Pop;
        _ = _in.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(pop ? 150 : 200), ArcEasings.Enter));
        _ = _travel.AnimateTo(0, MotionSpec.Spring(pop ? s_popSpring : s_rollSpring));
        RemoveWhenFaded(_outgoing);
    }

    private async void RemoveWhenFaded(ControlCenterSwapSlot exiting)
    {
        var done = await _out.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), s_exitEase));
        if (done && ReferenceEquals(_outgoing, exiting))
        {
            ClearOutgoing();
            ApplyFrame();
        }
    }

    private void ClearOutgoing()
    {
        if (_outgoing is null)
        {
            return;
        }
        Children.Remove(_outgoing);
        _outgoing = null;
    }

    /// <summary>Writes the current frame: the slots' opacity, travel, scale and blur.</summary>
    private void ApplyFrame()
    {
        var pop = Kind == ControlCenterSwapKind.Pop;
        if (_current is { } current)
        {
            var inT = _in.Value;
            // The travel spring settles from 1 to 0 (a reduced level jumps it to 0).
            var travel = _full ? _travel.Value : 0;
            var y = pop ? 0 : 8 * travel;
            var scale = pop && _full ? 1 - 0.3 * travel : 1;
            var blur = _outgoing is null ? 0 : 2 * (1 - inT);
            current.Apply(_outgoing is null ? 1 : inT, y, scale, blur);
        }
        if (_outgoing is { } outgoing)
        {
            // The fade and blur finish at ~120ms (0.6 of the 200ms run); the travel uses the whole run.
            var t = _out.Value;
            var fade = Math.Clamp(t / 0.6, 0, 1);
            var y = pop || !_full ? 0 : -8 * t;
            var scale = pop && _full ? 1 - 0.3 * fade : 1;
            outgoing.Apply(1 - fade, y, scale, 2 * fade);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }
        return _current?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (ReferenceEquals(child, _current))
            {
                child.Arrange(new Rect(finalSize));
                continue;
            }
            // The outgoing content keeps its own size, placed like the current one (left, or centered for icons).
            var size = child.DesiredSize;
            var x = HorizontalContentAlignment == HorizontalAlignment.Center ? (finalSize.Width - size.Width) / 2 : 0;
            child.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }

    /// <summary>Where the outgoing content sits: left (text) or centered (icons).</summary>
    public HorizontalAlignment HorizontalContentAlignment { get; set; } = HorizontalAlignment.Left;
}

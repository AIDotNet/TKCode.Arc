using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="FilterToolbarChipSlot"/> reveals its child.</summary>
internal enum FilterToolbarSlotMode
{
    /// <summary>A chip: fades in from 0.9 scale out of a soft blur (scale on a snappy spring).</summary>
    Chip,

    /// <summary>A text action: rises 0.3em out of a soft blur.</summary>
    Text
}

/// <summary>
/// Arc's chip slot: a slot whose width (and trailing gap) opens or collapses on a smooth spring so the neighbors,
/// the toolbar and its actions follow in the normal flow, while the child keeps its natural width and reveals
/// itself (opacity, scale or rise, blur). While the slot moves it clips. A slot that jumps to another line glides
/// there from where it was seen (<see cref="GlideFrom"/>).
/// </summary>
internal sealed class FilterToolbarChipSlot : Decorator
{
    private readonly TranslateTransform _glide = new();
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _rise = new();
    private readonly BlurEffect _blur = new();
    private MotionValue<double>? _presence;
    private MotionValue<double>? _reveal;
    private MotionValue<double>? _pop;
    private MotionValue<Vector>? _offset;
    private bool _present = true;
    private bool _entering = true;

    public FilterToolbarChipSlot()
    {
        RenderTransform = _glide;
    }

    public FilterToolbarSlotMode Mode { get; set; } = FilterToolbarSlotMode.Chip;

    /// <summary>Gap after the slot in the flow; it collapses with the slot.</summary>
    public double Gap { get; set; } = 8;

    /// <summary>The chip id this slot shows.</summary>
    public string? Key { get; set; }

    public FilterChip? Chip { get; set; }

    public bool IsLeaving => !_present;

    /// <summary>Last arranged position in the panel (for reflow glides).</summary>
    public Point? LastPosition { get; set; }

    /// <summary>0 = collapsed, 1 = open.</summary>
    public double LayoutFactor => Math.Clamp(_presence?.Value ?? (_present ? 1 : 0), 0, 1);

    /// <summary>Raised when an exit finished (the owner removes or hides the slot).</summary>
    public event EventHandler? Exited;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty && change.NewValue is Control child)
        {
            child.RenderTransform = new TransformGroup { Children = { _scale, _rise } };
            child.RenderTransformOrigin = RelativePoint.Center;
            Apply();
        }
    }

    /// <summary>Shows the slot; without animation (first state, reduced or off motion) it is simply there.</summary>
    public void Enter(bool animate)
    {
        _present = true;
        _entering = true;
        IsHitTestVisible = true;
        if (Child is { } child)
        {
            child.IsVisible = true;
        }
        if (!animate || !ArcMotion.IsFull(this))
        {
            // Arc's reduced motion skips the enter (initial false); off motion is instant.
            _presence?.Jump(1);
            _reveal?.Jump(1);
            _pop?.Jump(1);
            Apply();
            InvalidateMeasure();
            return;
        }
        _presence ??= Create(0, relayout: true);
        _reveal ??= Create(0, relayout: false);
        _pop ??= Create(0.9, relayout: false);
        if (_presence.Value >= 0.999 && !_presence.IsAnimating)
        {
            _presence.Jump(0);
            _reveal.Jump(0);
            _pop.Jump(Mode == FilterToolbarSlotMode.Chip ? 0.9 : 1);
        }
        _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        if (Mode == FilterToolbarSlotMode.Chip)
        {
            _ = _pop.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
    }

    /// <summary>Collapses the slot; <see cref="Exited"/> fires when it is closed.</summary>
    public void Exit(bool animate)
    {
        if (!_present)
        {
            return;
        }
        _present = false;
        _entering = false;
        IsHitTestVisible = false;
        if (!animate || !ArcMotion.IsFull(this))
        {
            _presence?.Jump(0);
            _reveal?.Jump(0);
            Apply();
            InvalidateMeasure();
            Finish();
            return;
        }
        _presence ??= Create(1, relayout: true);
        _reveal ??= Create(1, relayout: false);
        _pop ??= Create(1, relayout: false);
        var leave = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = _reveal.AnimateTo(0, leave);
        if (Mode == FilterToolbarSlotMode.Chip)
        {
            _ = _pop.AnimateTo(0.9, leave);
        }
        _ = WaitAndFinish(_presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)));
    }

    private async Task WaitAndFinish(Task<bool> task)
    {
        var reached = await task;
        if (reached && !_present)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (Child is { } child)
        {
            child.IsVisible = false;
        }
        Exited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Offsets the slot by <paramref name="delta"/> (old minus new position) and springs it home.</summary>
    public void GlideFrom(Vector delta)
    {
        if (_offset is null)
        {
            _offset = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _offset.Changed += v =>
            {
                _glide.X = v.X;
                _glide.Y = v.Y;
            };
        }
        _offset.Jump(_offset.Value + delta);
        _ = _offset.AnimateTo(default, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private MotionValue<double> Create(double initial, bool relayout)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            Apply();
            if (relayout)
            {
                InvalidateMeasure();
            }
        };
        return value;
    }

    private void Apply()
    {
        if (Child is not { } child)
        {
            return;
        }
        var reveal = Math.Clamp(_reveal?.Value ?? (_present ? 1 : 0), 0, 1);
        child.Opacity = reveal;
        var scale = Mode == FilterToolbarSlotMode.Chip ? _pop?.Value ?? 1 : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        if (Mode == FilterToolbarSlotMode.Text)
        {
            // 0.3em at 14px: the text rises in and lifts away.
            _rise.Y = (1 - reveal) * 4.2 * (_entering ? 1 : -1);
        }
        var radius = (1 - reveal) * (_entering ? 4 : 2);
        if (radius > 0.05)
        {
            _blur.Radius = radius;
            child.Effect = _blur;
        }
        else if (child.Effect is not null)
        {
            child.Effect = null;
        }
        ClipToBounds = LayoutFactor < 0.999;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var desired = child.DesiredSize;
        var factor = LayoutFactor;
        var width = factor >= 0.999 ? Math.Min(desired.Width, availableSize.Width) : desired.Width * factor;
        return new Size(width, desired.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var width = LayoutFactor >= 0.999 ? finalSize.Width : Math.Max(child.DesiredSize.Width, finalSize.Width);
            child.Arrange(new Rect(0, 0, width, finalSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The chip row: wraps slots with Arc's 8px gap, centers each line on the 36px control height, and lets a chip
/// that wraps to another line glide there instead of teleporting (only for a moment after the filters change,
/// so window resizes settle at once).
/// </summary>
internal sealed class FilterToolbarChipPanel : Panel
{
    public double Gap { get; set; } = 8;

    public double MinLineHeight { get; set; } = 36;

    private long _armedUntil;

    /// <summary>Allows reflow glides for 900ms (Arc arms layout motion after each filter change).</summary>
    public void Arm() => _armedUntil = Environment.TickCount64 + 900;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        double x = 0, lineHeight = MinLineHeight, total = 0, widest = 0;
        var lines = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            var factor = child is FilterToolbarChipSlot slot ? slot.LayoutFactor : 1;
            var gap = Gap * factor;
            if (x > 0 && x + size.Width > width + 0.5)
            {
                total += lineHeight + Gap;
                lines++;
                widest = Math.Max(widest, x);
                x = 0;
                lineHeight = MinLineHeight;
            }
            x += size.Width + gap;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        widest = Math.Max(widest, x);
        total += lineHeight;
        _ = lines;
        return new Size(double.IsFinite(width) ? Math.Min(width, widest) : widest, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var glide = Environment.TickCount64 < _armedUntil && ArcMotion.IsFull(this);
        var line = _line;
        line.Clear();
        double x = 0, top = 0, lineHeight = MinLineHeight;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var factor = child is FilterToolbarChipSlot slot ? slot.LayoutFactor : 1;
            if (x > 0 && x + size.Width > width + 0.5)
            {
                Place(line, top, lineHeight, glide);
                top += lineHeight + Gap;
                x = 0;
                lineHeight = MinLineHeight;
                line.Clear();
            }
            line.Add((child, x));
            x += size.Width + Gap * factor;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        Place(line, top, lineHeight, glide);
        return finalSize;
    }

    private readonly List<(Control Child, double X)> _line = new();

    private static void Place(List<(Control Child, double X)> line, double top, double lineHeight, bool glide)
    {
        foreach (var (child, x) in line)
        {
            var size = child.DesiredSize;
            var y = top + (lineHeight - size.Height) / 2;
            if (child is FilterToolbarChipSlot slot)
            {
                if (glide && slot.LastPosition is { } last && !slot.IsLeaving)
                {
                    var jx = last.X - x;
                    var jy = last.Y - y;
                    if (Math.Abs(jy) >= 1 || Math.Abs(jx) >= 40)
                    {
                        slot.GlideFrom(new Vector(jx, jy));
                    }
                }
                slot.LastPosition = new Point(x, y);
            }
            child.Arrange(new Rect(x, y, size.Width, size.Height));
        }
    }
}

/// <summary>Lays out the toolbar: chips beside the actions, or stacked with the actions at the end below a breakpoint.</summary>
internal sealed class FilterToolbarLayout : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<FilterToolbarLayout, double>(nameof(Gap), 16);

    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<FilterToolbarLayout, double>(nameof(Breakpoint), 520);

    static FilterToolbarLayout()
    {
        AffectsMeasure<FilterToolbarLayout>(GapProperty, BreakpointProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double Breakpoint { get => GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }

    public bool IsStacked { get; private set; }

    public event EventHandler? StackedChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var frame = Children[0];
        var actions = Children[1];
        var stacked = double.IsFinite(availableSize.Width) && availableSize.Width < Breakpoint;
        if (stacked != IsStacked)
        {
            IsStacked = stacked;
            StackedChanged?.Invoke(this, EventArgs.Empty);
        }
        actions.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        if (stacked)
        {
            frame.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            return new Size(Math.Max(frame.DesiredSize.Width, actions.DesiredSize.Width),
                frame.DesiredSize.Height + Gap + actions.DesiredSize.Height);
        }
        var room = Math.Max(0, availableSize.Width - actions.DesiredSize.Width - Gap);
        frame.Measure(new Size(room, double.PositiveInfinity));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : frame.DesiredSize.Width + Gap + actions.DesiredSize.Width;
        return new Size(width, Math.Max(frame.DesiredSize.Height, actions.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var frame = Children[0];
        var actions = Children[1];
        var actionsSize = actions.DesiredSize;
        if (IsStacked)
        {
            frame.Arrange(new Rect(0, 0, finalSize.Width, frame.DesiredSize.Height));
            var y = frame.DesiredSize.Height + Gap;
            actions.Arrange(new Rect(Math.Max(0, finalSize.Width - actionsSize.Width), y, actionsSize.Width, actionsSize.Height));
            return finalSize;
        }
        var room = Math.Max(0, finalSize.Width - actionsSize.Width - Gap);
        frame.Arrange(new Rect(0, 0, room, frame.DesiredSize.Height));
        actions.Arrange(new Rect(finalSize.Width - actionsSize.Width, 0, actionsSize.Width, actionsSize.Height));
        return finalSize;
    }
}

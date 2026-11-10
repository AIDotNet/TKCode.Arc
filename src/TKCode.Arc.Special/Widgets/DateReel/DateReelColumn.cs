using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One wheel of a <see cref="DateReel"/>: the days, the months or the years drawn on a drum that turns under a drag, a
/// flick, the mouse wheel or the keys, and always comes to rest on a row. Rows are drawn by this one control from cached
/// text layouts, so turning allocates nothing.
/// </summary>
internal sealed class DateReelColumn : Control
{
    private readonly DragTracker _drag;
    private readonly Dictionary<int, TextLayout> _layouts = [];
    private MotionValue<double>? _spring;
    private double _position;
    private double _dragStart;
    private double _wheel;
    private int _index;
    private int _count;
    private bool _loops;
    private Pen? _focusPen;

    static DateReelColumn()
    {
        FocusableProperty.OverrideDefaultValue<DateReelColumn>(true);
        ClipToBoundsProperty.OverrideDefaultValue<DateReelColumn>(false);
    }

    public DateReelColumn(DateReel owner, DateReelPart part)
    {
        Owner = owner;
        Part = part;
        _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Vertical });
        _drag.Pressed += (_, _) => Focus(NavigationMethod.Pointer);
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += (_, _) => Commit(IndexOf(Math.Round(_position)), Math.Round(_position), 0);
        _drag.Released += OnTapped;
    }

    public DateReel Owner { get; }

    public DateReelPart Part { get; }

    /// <summary>The row the wheel shows or is heading for.</summary>
    public int SelectedIndex => _index;

    /// <summary>Where the wheel is drawn now, in rows (fractional while it turns).</summary>
    public double Position => _position;

    public int Count => _count;

    public bool Loops => _loops;

    /// <summary>True while the wheel is still turning toward its row.</summary>
    public bool IsTurning => _spring?.IsAnimating == true || _drag.IsDragging;

    private double ItemHeight => Owner.ItemHeight;

    private int VisibleRows => Owner.VisibleItemCount;

    private int IndexOf(double position) => DateReelMath.IndexAt(position, _count, _loops);

    /// <summary>Takes the wheel's length and looping from the reel; a new length rests the wheel on <paramref name="index"/>.</summary>
    public void Configure(int count, bool loops, int index)
    {
        var changed = count != _count || loops != _loops;
        _count = count;
        _loops = loops;
        if (changed)
        {
            _spring?.Stop();
            _index = Math.Clamp(index, 0, Math.Max(0, count - 1));
            Jump(_index);
            InvalidateMeasure();
        }
    }

    /// <summary>Drops the cached text (a new culture or font).</summary>
    public void ClearText()
    {
        foreach (var layout in _layouts.Values)
        {
            layout.Dispose();
        }
        _layouts.Clear();
        _focusPen = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Turns the wheel to <paramref name="index"/>, the short way round, on a spring or at once.</summary>
    public void ShowIndex(int index, bool animate)
    {
        if (index == _index || _drag.IsDragging)
        {
            return;
        }
        _index = index;
        var target = DateReelMath.PositionFor(_position, index, _count, _loops);
        if (!animate || !IsAttached() || !ArcMotion.IsFull(this))
        {
            Jump(target);
            return;
        }
        _ = Spring().AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private bool IsAttached() => TopLevel.GetTopLevel(this) is not null;

    private MotionValue<double> Spring()
    {
        if (_spring is null)
        {
            _spring = new MotionValue<double>(this, _position, MotionVectors.Double);
            _spring.Changed += Show;
        }
        return _spring;
    }

    private void Jump(double position)
    {
        _spring?.Jump(position);
        Show(position);
    }

    private void Show(double position)
    {
        _position = position;
        InvalidateVisual();
    }

    /// <summary>Rests on a row picked by input and tells the reel; a flick keeps its speed into the spring.</summary>
    private void Commit(int index, double target, double velocity)
    {
        _index = index;
        if (IsAttached() && ArcMotion.IsFull(this))
        {
            _ = MomentumProjector.SettleTo(Spring(), target, velocity, ArcSpring.Smooth);
        }
        else
        {
            Jump(target);
        }
        Owner.OnColumnPicked(this, index);
    }

    /// <summary>Moves by whole rows from the row the wheel is heading for (keys, mouse wheel).</summary>
    public void MoveBy(int rows)
    {
        if (_count <= 0 || rows == 0)
        {
            return;
        }
        var basePosition = DateReelMath.PositionFor(_position, _index, _count, _loops);
        var target = basePosition + rows;
        if (!_loops)
        {
            target = Math.Clamp(target, 0, _count - 1);
        }
        Commit(IndexOf(target), target, _spring?.Velocity ?? 0);
    }

    /// <summary>Moves to a row, the short way round.</summary>
    public void MoveTo(int index)
    {
        if (_count <= 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, _count - 1);
        Commit(index, DateReelMath.PositionFor(_position, index, _count, _loops), 0);
    }

    // --- pointer --------------------------------------------------------------------------------------------------

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        _spring?.Stop();
        _dragStart = _position;
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        var raw = _dragStart - e.Offset.Y / ItemHeight;
        if (!_loops)
        {
            raw = RubberBand.Clamp(raw, 0, Math.Max(0, _count - 1), VisibleRows);
        }
        Jump(raw);
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        var velocity = -e.Velocity.Y / ItemHeight;
        var target = DateReelMath.Settle(_position, velocity, _count, _loops, Owner.FlickDecay);
        Commit(IndexOf(target), target, velocity);
    }

    private void OnTapped(object? sender, DragGestureEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || this.TranslatePoint(default, top) is not { } origin)
        {
            return;
        }
        var offset = e.Position.Y - origin.Y - Bounds.Height / 2;
        var delta = DateReelMath.DeltaAt(offset, DateReelMath.Step(VisibleRows), Bounds.Height / 2);
        var rows = (int)Math.Round(delta);
        if (rows != 0)
        {
            MoveBy(rows);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _wheel -= e.Delta.Y;
        var rows = (int)Math.Truncate(_wheel);
        if (rows != 0)
        {
            _wheel -= rows;
            MoveBy(rows);
        }
        e.Handled = true;
    }

    // --- keyboard -------------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Up:
                MoveBy(-1);
                break;
            case Key.Down:
                MoveBy(1);
                break;
            case Key.PageUp:
                MoveBy(-Owner.PageRows);
                break;
            case Key.PageDown:
                MoveBy(Owner.PageRows);
                break;
            case Key.Home:
                MoveTo(0);
                break;
            case Key.End:
                MoveTo(_count - 1);
                break;
            case Key.Left:
                Owner.FocusNext(this, -1);
                break;
            case Key.Right:
                Owner.FocusNext(this, 1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DateReelColumnAutomationPeer(this);

    // --- drawing --------------------------------------------------------------------------------------------------

    private TextLayout Layout(int index)
    {
        if (!_layouts.TryGetValue(index, out var layout))
        {
            layout = new TextLayout(Owner.TextOf(Part, index), Owner.ItemTypeface, Owner.FontSize, Owner.Foreground);
            _layouts[index] = layout;
        }
        return layout;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        using var widest = new TextLayout(Owner.WidestText(Part), Owner.ItemTypeface, Owner.FontSize, Owner.Foreground);
        var width = Math.Ceiling(widest.WidthIncludingTrailingWhitespace) + 2 * Owner.ColumnPadding;
        return new Size(width, VisibleRows * ItemHeight);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole wheel takes the pointer, not only the drawn text.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_count <= 0)
        {
            return;
        }
        var centerY = bounds.Height / 2;
        var centerX = bounds.Width / 2;
        var radius = bounds.Height / 2;
        var step = DateReelMath.Step(VisibleRows);
        var reach = Math.PI / 2 / step;
        var fade = Owner.EdgeFade;
        var first = (long)Math.Floor(_position - reach);
        var last = (long)Math.Ceiling(_position + reach);
        for (var k = first; k <= last; k++)
        {
            if (!_loops && (k < 0 || k >= _count))
            {
                continue;
            }
            var (offset, scale, angle, visible) = DateReelMath.Project(k - _position, step, radius);
            if (!visible || scale < 0.05)
            {
                continue;
            }
            var index = _loops ? DateReelMath.Wrap(k, _count) : (int)k;
            var layout = Layout(index);
            var opacity = 1 - fade * Math.Pow(Math.Abs(angle) / (Math.PI / 2), 1.5);
            if (!Owner.IsAllowed(Part, index))
            {
                opacity *= 0.35;
            }
            var matrix = Matrix.CreateScale(1, scale) * Matrix.CreateTranslation(centerX, centerY + offset);
            using (context.PushTransform(matrix))
            using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
            {
                layout.Draw(context, new Point(-layout.WidthIncludingTrailingWhitespace / 2, -layout.Height / 2));
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && Owner.FocusBrush is { } focus)
        {
            _focusPen ??= new Pen(focus, 2);
            var radiusOf = Owner.BandCornerRadius.TopLeft + 2;
            var ring = new Rect(-2, centerY - ItemHeight / 2 - 2, bounds.Width + 4, ItemHeight + 4);
            context.DrawRectangle(null, _focusPen, ring, radiusOf, radiusOf);
        }
    }
}

/// <summary>A wheel as a spinner: its row as a range value, and the row's text as its help text.</summary>
internal sealed class DateReelColumnAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public DateReelColumnAutomationPeer(DateReelColumn owner) : base(owner)
    {
    }

    private new DateReelColumn Owner => (DateReelColumn)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Owner.Owner.LabelOf(Owner.Part);

    protected override string? GetHelpTextCore() => Owner.Owner.TextOf(Owner.Part, Owner.SelectedIndex);

    public bool IsReadOnly => !Owner.IsEffectivelyEnabled;

    public double Minimum => 0;

    public double Maximum => Math.Max(0, Owner.Count - 1);

    public double Value => Owner.SelectedIndex;

    public double LargeChange => Owner.Owner.PageRows;

    public double SmallChange => 1;

    public void SetValue(double value) => Owner.MoveTo((int)Math.Round(value));
}

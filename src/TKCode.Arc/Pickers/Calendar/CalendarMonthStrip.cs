using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Months side by side on one clipped strip; the position is measured in months and driven by a single spring,
/// so rapid clicks retarget it instead of queueing panes. Only the panes either side of the position and the
/// month being navigated to exist (pooled), and moving them is a render transform, never a relayout.
/// </summary>
internal sealed class CalendarMonthStrip : Control
{
    private const double PaneGap = 16;
    private static readonly SpringSpec StripSpring = SpringSpec.FromVisual(0.36, 0);

    private readonly Dictionary<int, CalendarMonthPane> _panes = new();
    private readonly Stack<CalendarMonthPane> _pool = new();
    private MotionValue<double>? _position;
    private int _target = int.MinValue;
    private Calendar? _owner;
    private double _width = Calendar.DefaultWidth;

    public CalendarMonthStrip()
    {
        ClipToBounds = true;
    }

    internal Calendar? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            foreach (var pane in _panes.Values)
            {
                RemovePane(pane);
            }
            _panes.Clear();
            _pool.Clear();
        }
    }

    public CalendarMonthPane? PresentPane => _panes.GetValueOrDefault(_target);

    public IEnumerable<CalendarMonthPane> Panes => _panes.Values;

    /// <summary>Moves the strip to <paramref name="index"/>; the first month and reduced motion land in place.</summary>
    public void NavigateTo(int index, bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        var first = _position is null;
        _position ??= CreatePosition(index);
        _target = index;
        if (first || !animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _position.Jump(index);
        }
        else
        {
            var from = _position.Value;
            // Long jumps (Today, a year with Shift) start one month away so the strip never scrolls through the months between.
            if (Math.Abs(index - from) > 2)
            {
                _position.Jump(index - Math.Sign(index - from));
            }
            _ = _position.AnimateTo(index, MotionSpec.Spring(StripSpring));
        }
        SyncPanes();
    }

    /// <summary>Reconfigures every live pane (selection, limits, today, culture changed).</summary>
    public void Refresh(CalendarSelectionHandoff? handoff)
    {
        foreach (var (index, pane) in _panes)
        {
            pane.Configure(index, index == _target, handoff);
        }
    }

    private MotionValue<double> CreatePosition(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => SyncPanes();
        return value;
    }

    private void SyncPanes()
    {
        if (_owner is null || _position is null)
        {
            return;
        }
        var position = _position.Value;
        var low = (int)Math.Floor(position + 1e-3);
        var high = (int)Math.Ceiling(position - 1e-3);
        Span<int> wanted = [low, high, _target];
        List<int>? stale = null;
        foreach (var index in _panes.Keys)
        {
            if (!wanted.Contains(index))
            {
                (stale ??= new List<int>()).Add(index);
            }
        }
        if (stale is not null)
        {
            foreach (var index in stale)
            {
                var pane = _panes[index];
                _panes.Remove(index);
                pane.IsVisible = false;
                _pool.Push(pane);
            }
        }
        var added = false;
        foreach (var index in wanted)
        {
            if (_panes.TryGetValue(index, out var pane))
            {
                if (pane.IsPresent != (index == _target))
                {
                    pane.Configure(index, index == _target, null);
                }
                continue;
            }
            pane = _pool.Count > 0 ? _pool.Pop() : CreatePane();
            pane.IsVisible = true;
            pane.Configure(index, index == _target, null);
            _panes[index] = pane;
            added = true;
        }
        foreach (var (index, pane) in _panes)
        {
            var offset = index - position;
            if (pane.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform();
                pane.RenderTransform = translate;
            }
            translate.X = offset * (_width + PaneGap);
            pane.Opacity = 1 - Math.Min(1, Math.Abs(offset)) * 0.6;
        }
        if (added)
        {
            InvalidateMeasure();
        }
    }

    private CalendarMonthPane CreatePane()
    {
        var pane = new CalendarMonthPane(_owner!);
        VisualChildren.Add(pane);
        LogicalChildren.Add(pane);
        return pane;
    }

    private void RemovePane(CalendarMonthPane pane)
    {
        VisualChildren.Remove(pane);
        LogicalChildren.Remove(pane);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Calendar.DefaultWidth : availableSize.Width;
        foreach (var pane in _panes.Values)
        {
            pane.Measure(new Size(width, double.PositiveInfinity));
        }
        return new Size(width, CalendarMonthPane.HeightFor(width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var changed = Math.Abs(finalSize.Width - _width) > 0.1;
        _width = finalSize.Width;
        foreach (var pane in _panes.Values)
        {
            pane.Arrange(new Rect(finalSize));
        }
        if (changed)
        {
            // A width change snaps the strip (passive size change), it never animates.
            SyncPanes();
        }
        return finalSize;
    }
}

using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The clipped window the visible months travel through (P25). The months shown together are one group; moving to
/// other months slides a new group in from the side you travel toward (x ±48 → 0 on the SLIDE spring, 0.4s, bounce
/// 0.06, through a soft blur) while the old group leaves the other way (−36, 0.14s fade) and stops taking input.
/// Groups are pooled; a leaving group returns to the pool when it has faded. Reduced motion crossfades.
/// </summary>
internal sealed class DateRangePickerMonthsView : Control
{
    public const double MonthGap = 24;
    internal static readonly SpringSpec Slide = SpringSpec.FromVisual(0.4, 0.06);

    private readonly IDateRangePickerMonthHost _host;
    private readonly List<Group> _leaving = new();
    private readonly Stack<Group> _pool = new();
    private Group? _current;
    private double _cell = 36;
    private int _count = 2;

    public DateRangePickerMonthsView(IDateRangePickerMonthHost host)
    {
        _host = host;
        ClipToBounds = true;
    }

    /// <summary>The group taking input (the months on screen).</summary>
    public IReadOnlyList<DateRangePickerMonthPane> Panes => _current?.Panes ?? (IReadOnlyList<DateRangePickerMonthPane>)Array.Empty<DateRangePickerMonthPane>();

    public double CellSize => _cell;

    public int Count => _count;

    public DateRangePickerDayCell? FindCell(DateOnly date)
    {
        if (_current is null)
        {
            return null;
        }
        foreach (var pane in _current.Panes)
        {
            if (pane.FindCell(date) is { } cell)
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>Sets the month count and cell size (a new layout snaps, it never animates).</summary>
    public void SetLayout(int count, double cell)
    {
        if (count == _count && Math.Abs(cell - _cell) < 0.01)
        {
            return;
        }
        _count = count;
        _cell = cell;
        foreach (var group in _leaving.ToArray())
        {
            Release(group);
        }
        if (_current is not null)
        {
            _current.Resize(count, cell);
        }
        InvalidateMeasure();
    }

    /// <summary>
    /// Shows the months starting at <paramref name="first"/>. <paramref name="direction"/> 1 slides forward, -1 back,
    /// 0 (or the same months) updates in place, animating the highlight when <paramref name="animate"/>.
    /// </summary>
    public void Show(DateOnly first, int direction, DayOfWeek firstDay, DateRange? range, DateOnly? tabbable, DateOnly today,
                     DateOnly? min, DateOnly? max, CultureInfo culture, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_current is not null && _current.First == first && _current.Panes.Count == _count)
        {
            _current.Configure(first, firstDay, range, tabbable, today, min, max, culture, animate);
            return;
        }
        var outgoing = _current;
        var incoming = Rent();
        incoming.Configure(first, firstDay, range, tabbable, today, min, max, culture, animate: false);
        _current = incoming;
        incoming.IsHitTestVisible = true;
        incoming.IsEnabled = true;
        incoming.ZIndex = 1;
        var motion = outgoing is not null && direction != 0 && IsLoaded && level != ArcMotionLevel.Off;
        if (!motion)
        {
            if (outgoing is not null)
            {
                Release(outgoing);
            }
            incoming.Place(0, 1, 0);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        incoming.Enter(direction, full);
        // A leaving set turns inert, so focus and queries only find the current one.
        outgoing!.IsHitTestVisible = false;
        outgoing.IsEnabled = false;
        outgoing.ZIndex = 0;
        _leaving.Add(outgoing);
        outgoing.Exit(direction, full).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (_leaving.Contains(outgoing))
            {
                Release(outgoing);
            }
        }), TaskScheduler.Default);
    }

    private Group Rent()
    {
        var group = _pool.Count > 0 ? _pool.Pop() : CreateGroup();
        group.Resize(_count, _cell);
        group.IsVisible = true;
        return group;
    }

    private Group CreateGroup()
    {
        var group = new Group(_host);
        VisualChildren.Add(group);
        LogicalChildren.Add(group);
        return group;
    }

    private void Release(Group group)
    {
        _leaving.Remove(group);
        group.Stop();
        group.IsVisible = false;
        group.IsHitTestVisible = false;
        _pool.Push(group);
    }

    public static Size SizeFor(int count, double cell) =>
        new(count * cell * 7 + (count - 1) * MonthGap, DateRangePickerMonthPane.TitleHeight + DateRangePickerMonthPane.WeekdayHeight + cell * 6);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = SizeFor(_count, _cell);
        foreach (var visual in VisualChildren)
        {
            if (visual is Group group)
            {
                group.Measure(size);
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            if (visual is Group group)
            {
                group.Arrange(new Rect(finalSize));
            }
        }
        return finalSize;
    }

    /// <summary>The months shown together; moved by a render transform with opacity and blur.</summary>
    private sealed class Group : Control
    {
        private readonly IDateRangePickerMonthHost _host;
        private readonly List<DateRangePickerMonthPane> _panes = new();
        private readonly TranslateTransform _shift = new();
        private MotionValue<double>? _x;
        private MotionValue<double>? _opacity;
        private MotionValue<double>? _blur;
        private double _cell = 36;

        public Group(IDateRangePickerMonthHost host)
        {
            _host = host;
            RenderTransform = _shift;
            ClipToBounds = false;
        }

        public DateOnly First { get; private set; }

        public IReadOnlyList<DateRangePickerMonthPane> Panes => _panes;

        public void Resize(int count, double cell)
        {
            _cell = cell;
            while (_panes.Count < count)
            {
                var pane = new DateRangePickerMonthPane(_host);
                _panes.Add(pane);
                VisualChildren.Add(pane);
                LogicalChildren.Add(pane);
            }
            while (_panes.Count > count)
            {
                var pane = _panes[^1];
                _panes.RemoveAt(_panes.Count - 1);
                VisualChildren.Remove(pane);
                LogicalChildren.Remove(pane);
            }
            foreach (var pane in _panes)
            {
                pane.CellSize = cell;
            }
            InvalidateMeasure();
        }

        public void Configure(DateOnly first, DayOfWeek firstDay, DateRange? range, DateOnly? tabbable, DateOnly today,
                              DateOnly? min, DateOnly? max, CultureInfo culture, bool animate)
        {
            First = first;
            for (var i = 0; i < _panes.Count; i++)
            {
                _panes[i].Configure(CalendarMath.AddMonths(first, i), firstDay, range, tabbable, today, min, max, culture, animate);
            }
        }

        private MotionValue<double> Value(ref MotionValue<double>? field, double initial, Action<double> apply)
        {
            if (field is null)
            {
                field = new MotionValue<double>(this, initial, MotionVectors.Double);
                field.Changed += apply;
            }
            return field;
        }

        private MotionValue<double> X => Value(ref _x, 0, v => _shift.X = v);
        private MotionValue<double> Fade => Value(ref _opacity, 1, v => Opacity = Math.Clamp(v, 0, 1));
        private MotionValue<double> BlurValue => Value(ref _blur, 0, v => Effect = PickerDrawing.Blur(v));

        public void Place(double x, double opacity, double blur)
        {
            X.Jump(x);
            Fade.Jump(opacity);
            BlurValue.Jump(blur);
        }

        public void Enter(int direction, bool full)
        {
            if (!full)
            {
                Place(0, 0, 0);
                _ = Fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
                return;
            }
            Place(direction * 48, 0, 4);
            _ = X.AnimateTo(0, MotionSpec.Spring(Slide));
            _ = Fade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), ArcEasings.Enter));
            _ = BlurValue.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(240), ArcEasings.Enter));
        }

        public Task<bool> Exit(int direction, bool full)
        {
            if (!full)
            {
                return Fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard));
            }
            _ = X.AnimateTo(direction * -36, MotionSpec.Spring(Slide));
            _ = BlurValue.AnimateTo(4, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
            return Fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(140), ArcEasings.Standard));
        }

        public void Stop()
        {
            _x?.Stop();
            _opacity?.Stop();
            _blur?.Stop();
            Effect = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var pane in _panes)
            {
                pane.Measure(Size.Infinity);
            }
            return SizeFor(_panes.Count, _cell);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;
            foreach (var pane in _panes)
            {
                pane.Arrange(new Rect(x, 0, _cell * 7, pane.DesiredSize.Height));
                x += _cell * 7 + MonthGap;
            }
            return finalSize;
        }
    }
}

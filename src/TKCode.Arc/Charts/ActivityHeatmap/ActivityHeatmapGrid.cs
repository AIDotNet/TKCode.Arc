using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The day grid of an <see cref="ActivityHeatmap"/>: month and weekday labels, about 370 squares, the selection ring and
/// the shared tooltip, all drawn by one focusable control and hit-tested by arithmetic. Reveal waves and recolor sweeps
/// are evaluated per square against one clock.
/// </summary>
internal sealed class ActivityHeatmapGrid : Avalonia.Controls.Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double MonthsHeight = 16;
    private const double RowGap = 8;
    private const double Wave = 0.42;

    private readonly ActivityHeatmap _owner;
    private readonly ChartsBClock _clock;
    private readonly ChartsBReveal _reveal;
    private readonly MotionValue<double>[] _dim = new MotionValue<double>[5];
    private readonly MotionValue<Point> _ring;
    private readonly MotionValue<double> _ringOpacity;
    private readonly MotionValue<Point> _tipPosition;
    private readonly MotionValue<double> _tipWidth;
    private readonly MotionValue<double> _tipOpacity;
    private readonly MotionValue<double> _tipScale;
    private readonly MotionValue<double> _tipRoll;
    private readonly Dictionary<string, MotionValue<double>> _monthX = new();
    private Track[] _tracks = [];
    private int _weeks;
    private double _scroll;
    private bool _pinnedRight = true;
    private int _hover = -1;
    private int? _focusIndex;
    private bool _keyboardFocus;
    private int _tipIndex = -1;
    private double _tipValue;
    private bool _tipOpen;
    private int _tipDirection;
    private FormattedText? _tipPrimary;
    private FormattedText? _tipSecondary;
    private FormattedText? _tipOldPrimary;
    private FormattedText? _tipOldSecondary;
    private IDisposable? _hideTimer;
    private bool _ringShown;
    private ChartsBAutomationPeer? _peer;
    private string? _reading;
    private readonly List<(string Key, FormattedText Text, int Column)> _months = new();
    private readonly FormattedText?[] _weekdays = new FormattedText?[7];
    private int _textVersion = -1;

    /// <summary>Per-square animation; times are relative to the clock start.</summary>
    private struct Track
    {
        public Color ColorFrom;
        public Color ColorTo;
        public double ColorDelay;
        public double ColorDuration;
        public double ScaleFrom;
        public double ScaleTo;
        public double ScaleDelay;
        public double ScaleDuration;
        public double OpacityFrom;
        public double OpacityTo;
        public double OpacityDelay;
        public double OpacityDuration;
    }

    public ActivityHeatmapGrid(ActivityHeatmap owner)
    {
        _owner = owner;
        Focusable = true;
        ClipToBounds = false;
        _clock = new ChartsBClock(this, InvalidateVisual);
        _reveal = new ChartsBReveal(this, 0.35, OnReveal);
        for (var i = 0; i < 5; i++)
        {
            _dim[i] = Value(0);
        }
        _ring = new MotionValue<Point>(this, default, MotionVectors.Point);
        _ring.Changed += _ => InvalidateVisual();
        _ringOpacity = Value(0);
        _tipPosition = new MotionValue<Point>(this, default, MotionVectors.Point);
        _tipPosition.Changed += _ => InvalidateVisual();
        _tipWidth = Value(0);
        _tipOpacity = Value(0);
        _tipScale = Value(0.96);
        _tipRoll = Value(1);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public string? Reading => _reading;

    internal int? FocusIndex => _focusIndex;

    internal bool IsTooltipOpen => _tipOpen;

    internal int TooltipIndex => _tipIndex;

    internal double ScrollOffset => _scroll;

    private ActivityHeatmapModel Model => _owner.Model;

    private double Cell => _owner.CellSizeFor(Bounds.Width);

    private double Step => Cell + _owner.CellGap;

    private double CellsLeft => _owner.WeekdayColumn + _owner.CellGap;

    private double ScrollMax => Math.Max(0, Model.Weeks * Step - _owner.CellGap - (Bounds.Width - CellsLeft));

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this, AutomationControlType.Table);

    string IChartsBAccessible.ChartRoleDescription => "activity grid";

    string IChartsBAccessible.ChartName => _owner.Label ?? _owner.Summary;

    string IChartsBAccessible.ChartHelp => _reading ?? $"{_owner.Summary}. {_owner.LegendDescription}";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _reveal.Detach();
        _hideTimer?.Dispose();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 860 : availableSize.Width;
        var cell = _owner.CellSizeFor(width);
        return new Size(width, MonthsHeight + RowGap + 7 * (cell + _owner.CellGap) - _owner.CellGap);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            ClampScroll();
        }
    }

    // ---- model ------------------------------------------------------------------------------------------------

    private double WaveStep(int weeks) => Math.Min(0.012, Wave / Math.Max(1, weeks - 1 + 6));

    private (double Forward, double Back) WaveDelay(int column, int row, int weeks)
    {
        var step = WaveStep(weeks);
        var diagonal = Math.Max(1, weeks - 1 + 6);
        return ((column + row) * step, (diagonal - column - row) * step);
    }

    /// <summary>A new range recolors the grid in a sweep: forward when it is later than the last one, back when earlier.</summary>
    public void OnModelChanged(ActivityHeatmapModel previous)
    {
        var model = Model;
        var direction = model.Start >= previous.Start ? 1 : -1;
        var count = model.Weeks * 7;
        var animate = _reveal.IsRevealed && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && _tracks.Length > 0;
        var full = animate && ArcMotion.IsFull(this);
        var t = _clock.IsRunning ? _clock.Time : double.PositiveInfinity;
        var next = new Track[count];
        for (var p = 0; p < count; p++)
        {
            var column = p / 7;
            var row = p % 7;
            var index = model.IndexAt(column, row);
            var inRange = index >= 0;
            var color = _owner.LevelColor(inRange ? model.Levels[index] : 0);
            // Days outside the range shrink away in the sweep (Reduced: fade) and grow back when a range needs them.
            var fadeOnly = animate && !full;
            var scale = inRange || fadeOnly ? 1 : 0;
            var opacity = inRange || !fadeOnly ? 1 : 0;
            if (!animate || p >= _tracks.Length)
            {
                next[p] = Rest(color, scale, opacity);
                continue;
            }
            var (colorNow, scaleNow, opacityNow) = Evaluate(_tracks[p], t);
            var (forward, back) = WaveDelay(column, row, model.Weeks);
            var delay = full ? direction > 0 ? forward : back : 0;
            next[p] = new Track
            {
                ColorFrom = colorNow, ColorTo = color, ColorDelay = delay, ColorDuration = full ? 0.32 : ArcMotionPresets.Fast.TotalSeconds,
                ScaleFrom = scaleNow, ScaleTo = scale, ScaleDelay = delay, ScaleDuration = ArcMotionPresets.CssSpring.TotalSeconds,
                OpacityFrom = opacityNow, OpacityTo = opacity, OpacityDelay = 0, OpacityDuration = ArcMotionPresets.Fast.TotalSeconds
            };
        }
        _tracks = next;
        if (animate)
        {
            _clock.Start(Wave + 0.75);
        }
        else
        {
            _clock.Finish();
        }
        var monthsGlide = animate && full && _weeks > 0;
        _weeks = model.Weeks;
        _textVersion = -1;
        SyncMonths(monthsGlide);
        if (_focusIndex is { } focus && focus >= model.Length)
        {
            _focusIndex = model.Length > 0 ? model.Length - 1 : null;
        }
        if (_tipIndex >= model.Length)
        {
            HideTooltip(0);
        }
        else if (_tipIndex >= 0)
        {
            ShowTooltip(_tipIndex, instant: true);
        }
        OnSelectionChanged();
        OnHighlightChanged();
        ClampScroll();
        Focusable = model.Length > 0;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Track Rest(Color color, double scale, double opacity) => new()
    {
        ColorFrom = color, ColorTo = color, ScaleFrom = scale, ScaleTo = scale, OpacityFrom = opacity, OpacityTo = opacity,
        ColorDuration = 0.001, ScaleDuration = 0.001, OpacityDuration = 0.001
    };

    private void OnReveal(bool animate)
    {
        var model = Model;
        var count = model.Weeks * 7;
        _tracks = new Track[count];
        for (var p = 0; p < count; p++)
        {
            var column = p / 7;
            var row = p % 7;
            var index = model.IndexAt(column, row);
            var color = _owner.LevelColor(index >= 0 ? model.Levels[index] : 0);
            var inRange = index >= 0;
            _tracks[p] = Rest(color, inRange ? 1 : 0, 1);
            if (animate && inRange)
            {
                // Squares wave in along the diagonal: fade with the enter curve, grow on the CSS spring.
                var (delay, _) = WaveDelay(column, row, model.Weeks);
                _tracks[p].OpacityFrom = 0;
                _tracks[p].OpacityDelay = delay;
                _tracks[p].OpacityDuration = 0.36;
                _tracks[p].ScaleFrom = 0.5;
                _tracks[p].ScaleDelay = delay;
                _tracks[p].ScaleDuration = ArcMotionPresets.CssSpring.TotalSeconds;
            }
        }
        _weeks = model.Weeks;
        if (animate)
        {
            _clock.Start(Wave + 0.7);
        }
        else
        {
            _clock.Finish();
        }
        SyncMonths(false);
        OnSelectionChanged();
        InvalidateVisual();
    }

    private static (Color Color, double Scale, double Opacity) Evaluate(in Track track, double t)
    {
        if (double.IsPositiveInfinity(t))
        {
            return (track.ColorTo, track.ScaleTo, track.OpacityTo);
        }
        var c = ArcEasings.Standard.Ease(ChartsBMath.Clamp((t - track.ColorDelay) / track.ColorDuration, 0, 1));
        var s = ArcEasings.CssSpring.Ease(ChartsBMath.Clamp((t - track.ScaleDelay) / track.ScaleDuration, 0, 1));
        var o = ArcEasings.Enter.Ease(ChartsBMath.Clamp((t - track.OpacityDelay) / track.OpacityDuration, 0, 1));
        return (ChartsBMath.Lerp(track.ColorFrom, track.ColorTo, c), ChartsBMath.Mix(track.ScaleFrom, track.ScaleTo, s),
            ChartsBMath.Mix(track.OpacityFrom, track.OpacityTo, o));
    }

    public void OnPaletteChanged()
    {
        _textVersion = -1;
        var model = Model;
        for (var p = 0; p < _tracks.Length; p++)
        {
            var index = model.IndexAt(p / 7, p % 7);
            var color = _owner.LevelColor(index >= 0 ? model.Levels[index] : 0);
            _tracks[p].ColorFrom = color;
            _tracks[p].ColorTo = color;
        }
        if (_tipIndex >= 0)
        {
            ShowTooltip(_tipIndex, instant: true);
        }
        InvalidateVisual();
    }

    /// <summary>Month labels glide to their new week when a range starts on a different weekday.</summary>
    private void SyncMonths(bool glide)
    {
        var seen = new Dictionary<int, int>();
        var keys = new HashSet<string>();
        foreach (var (month, column, _) in Model.Months)
        {
            var occurrence = seen.TryGetValue(month % 12, out var n) ? n : 0;
            seen[month % 12] = occurrence + 1;
            var key = $"{month % 12}-{(occurrence == 0 ? 0 : 1)}";
            keys.Add(key);
            if (!_monthX.TryGetValue(key, out var x))
            {
                x = Value(column);
                _monthX[key] = x;
            }
            else if (glide)
            {
                _ = x.AnimateTo(column, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            }
            else
            {
                x.Jump(column);
            }
        }
        foreach (var stale in _monthX.Keys.Where(k => !keys.Contains(k)).ToList())
        {
            _monthX.Remove(stale);
        }
    }

    public void OnHighlightChanged()
    {
        var highlight = _owner.Highlight;
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        for (var level = 0; level < 5; level++)
        {
            var target = highlight is { } h && h != level ? 1 : 0;
            if (animate)
            {
                _ = _dim[level].AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _dim[level].Jump(target);
            }
        }
    }

    /// <summary>The selection ring glides between days (CSS spring) and fades in where it first lands.</summary>
    public void OnSelectionChanged()
    {
        var model = Model;
        var index = _owner.SelectedDate is { } date ? date.DayNumber - model.Start.DayNumber : -1;
        var shown = index >= 0 && index < model.Length && _reveal.IsRevealed;
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        if (shown)
        {
            var (column, row) = model.PositionOf(index);
            var target = new Point(column, row);
            if (_ringShown && animate && ArcMotion.IsFull(this))
            {
                _ = _ring.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            }
            else
            {
                _ring.Jump(target);
            }
        }
        _ringShown = shown;
        if (animate)
        {
            _ = _ringOpacity.AnimateTo(shown ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _ringOpacity.Jump(shown ? 1 : 0);
        }
    }

    // ---- tooltip ----------------------------------------------------------------------------------------------

    private (string Primary, string Secondary) ContentFor(int index)
    {
        var count = Model.Counts[index];
        var date = Model.DateAt(index).ToDateTime(TimeOnly.MinValue);
        var culture = _owner.EffectiveCulture;
        var primary = count > 0 ? $"{_owner.Number(count)} {_owner.Noun(count)}" : $"No {_owner.UnitOther}";
        return (primary, date.ToString("ddd, MMM d, yyyy", culture));
    }

    private string LongLabel(int index)
    {
        var count = Model.Counts[index];
        var date = Model.DateAt(index).ToDateTime(TimeOnly.MinValue);
        return $"{(count > 0 ? _owner.Number(count) : "No")} {_owner.Noun(count)}, {date.ToString("dddd, MMMM d, yyyy", _owner.EffectiveCulture)}";
    }

    /// <summary>One bubble for the whole grid: it jumps into place when it opens and glides on a spring between cells after that.</summary>
    private void ShowTooltip(int index, bool instant = false)
    {
        if (index < 0 || index >= Model.Length)
        {
            return;
        }
        _hideTimer?.Dispose();
        _hideTimer = null;
        var palette = _owner.Palette;
        var (primary, secondary) = ContentFor(index);
        var full = IsLoaded && ArcMotion.IsFull(this);
        var level = ArcMotion.GetEffectiveLevel(this);
        var gliding = _tipOpen && !instant && level != ArcMotionLevel.Off;
        var value = Model.Counts[index];
        var changed = index != _tipIndex || _tipPrimary is null;
        if (changed && gliding)
        {
            _tipOldPrimary = _tipPrimary;
            _tipOldSecondary = _tipSecondary;
            _tipDirection = Math.Sign(value - _tipValue);
            _tipRoll.Jump(0);
            _ = _tipRoll.AnimateTo(1, MotionSpec.Tween(full ? ArcMotionPresets.Standard : ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        else
        {
            _tipOldPrimary = null;
            _tipOldSecondary = null;
            _tipRoll.Jump(1);
        }
        _tipIndex = index;
        _tipValue = value;
        _tipPrimary = ChartsBText.Make(primary, palette, palette.TypeSM, palette.Background, FontWeight.Medium);
        _tipSecondary = ChartsBText.Make(secondary, palette, palette.TypeXS, ChartsBMath.MixIn(palette.Background, 0.68, palette.Foreground));
        var width = Math.Ceiling(Math.Max(_tipPrimary.WidthIncludingTrailingWhitespace, _tipSecondary.WidthIncludingTrailingWhitespace));
        var anchor = Anchor(index, width);
        if (gliding && full)
        {
            _ = _tipPosition.AnimateTo(anchor, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _tipWidth.AnimateTo(width, MotionSpec.Spring(ArcSpring.Morph));
        }
        else
        {
            _tipPosition.Jump(anchor);
            _tipWidth.Jump(width);
        }
        if (!_tipOpen)
        {
            _tipOpen = true;
            if (full)
            {
                _ = _tipOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _ = _tipScale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else if (level == ArcMotionLevel.Reduced && IsLoaded)
            {
                _tipScale.Jump(1);
                _ = _tipOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _tipOpacity.Jump(1);
                _tipScale.Jump(1);
            }
        }
        _reading = LongLabel(index);
        _peer?.Announce();
        InvalidateVisual();
    }

    private Point Anchor(int index, double textWidth)
    {
        var (column, row) = Model.PositionOf(index);
        var cell = Cell;
        var half = textWidth / 2 + 13;
        var x = ChartsBMath.Clamp(CellsLeft + column * Step - _scroll + cell / 2, half + 2, Math.Max(half + 2, Bounds.Width - half - 2));
        return new Point(x, MonthsHeight + RowGap + row * Step);
    }

    private void HideTooltip(int delayMs)
    {
        _hideTimer?.Dispose();
        _hideTimer = null;
        if (delayMs > 0)
        {
            _hideTimer = DispatcherTimer.RunOnce(() => HideTooltip(0), TimeSpan.FromMilliseconds(delayMs));
            return;
        }
        if (!_tipOpen)
        {
            return;
        }
        _tipOpen = false;
        if (IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            var spec = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
            _ = _tipOpacity.AnimateTo(0, spec);
            if (ArcMotion.IsFull(this))
            {
                _ = _tipScale.AnimateTo(0.96, spec);
            }
        }
        else
        {
            _tipOpacity.Jump(0);
            _tipScale.Jump(0.96);
        }
        _reading = null;
        _peer?.Announce();
        InvalidateVisual();
    }

    public void RefreshTooltip()
    {
        if (_tipIndex >= 0 && _tipOpen)
        {
            ShowTooltip(_tipIndex, instant: true);
        }
    }

    // ---- interaction ------------------------------------------------------------------------------------------

    private int IndexAt(Point point)
    {
        var cell = Cell;
        var step = Step;
        var x = point.X - CellsLeft + _scroll;
        var y = point.Y - MonthsHeight - RowGap;
        if (x < 0 || y < 0 || point.X < CellsLeft)
        {
            return -1;
        }
        var column = (int)Math.Floor(x / step);
        var row = (int)Math.Floor(y / step);
        if (row > 6 || column >= Model.Weeks || x - column * step > cell || y - row * step > cell)
        {
            return -1;
        }
        return Model.IndexAt(column, row);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        var hover = e.Pointer.Type == PointerType.Mouse ? index : -1;
        if (hover != _hover)
        {
            _hover = hover;
            InvalidateVisual();
        }
        if (index >= 0)
        {
            if (index != _tipIndex || !_tipOpen)
            {
                ShowTooltip(index);
            }
            else
            {
                _hideTimer?.Dispose();
            }
        }
        else
        {
            var point = e.GetPosition(this);
            if (point.Y < MonthsHeight + RowGap - 4 || point.X < CellsLeft - 4)
            {
                LeaveGrid(e.Pointer.Type);
            }
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        LeaveGrid(e.Pointer.Type);
        InvalidateVisual();
    }

    // A keyboard user keeps their bubble on the focused day; a tap lingers long enough to read.
    private void LeaveGrid(PointerType type)
    {
        if (_keyboardFocus && IsFocused && _focusIndex is { } focus)
        {
            ShowTooltip(focus);
            return;
        }
        HideTooltip(type == PointerType.Touch ? 2400 : 110);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var index = IndexAt(e.GetPosition(this));
        if (index < 0)
        {
            return;
        }
        _keyboardFocus = false;
        _focusIndex = index;
        Focus(NavigationMethod.Pointer);
        ShowTooltip(index);
        _owner.Select(index);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (ScrollMax <= 0)
        {
            return;
        }
        var delta = Math.Abs(e.Delta.X) > 0 ? e.Delta.X : (e.KeyModifiers & KeyModifiers.Shift) != 0 ? e.Delta.Y : 0;
        if (delta == 0)
        {
            return;
        }
        ScrollTo(_scroll - delta * 50);
        e.Handled = true;
    }

    private void ScrollTo(double offset)
    {
        _pinnedRight = false;
        _scroll = offset;
        ClampScroll();
        _pinnedRight = _scroll >= ScrollMax - 0.5;
        if (_tipOpen && _tipIndex >= 0)
        {
            _tipPosition.Jump(Anchor(_tipIndex, _tipWidth.Target));
        }
        InvalidateVisual();
    }

    /// <summary>Keeps the offset in range; a grid showing its newest week keeps showing it as it resizes.</summary>
    private void ClampScroll()
    {
        var max = ScrollMax;
        _scroll = _pinnedRight ? max : ChartsBMath.Clamp(_scroll, 0, max);
    }

    private void EnsureVisible(int index)
    {
        var (column, _) = Model.PositionOf(index);
        var left = column * Step;
        var right = left + Cell;
        var view = Bounds.Width - CellsLeft;
        if (left < _scroll)
        {
            ScrollTo(left);
        }
        else if (right > _scroll + view)
        {
            ScrollTo(right - view);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (Model.Length == 0)
        {
            return;
        }
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        if (_keyboardFocus)
        {
            var selected = _owner.SelectedDate is { } date ? date.DayNumber - Model.Start.DayNumber : -1;
            _focusIndex ??= selected >= 0 && selected < Model.Length ? selected : Model.Length - 1;
            EnsureVisible(_focusIndex.Value);
            ShowTooltip(_focusIndex.Value);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        HideTooltip(0);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Model.Length == 0)
        {
            return;
        }
        var at = _focusIndex ?? Model.Length - 1;
        int? next = e.Key switch
        {
            Key.Up => at - 1,
            Key.Down => at + 1,
            Key.Left => at - 7,
            Key.Right => at + 7,
            Key.Home => 0,
            Key.End => Model.Length - 1,
            _ => null
        };
        if (next is { } target)
        {
            e.Handled = true;
            _keyboardFocus = true;
            _focusIndex = (int)ChartsBMath.Clamp(target, 0, Model.Length - 1);
            EnsureVisible(_focusIndex.Value);
            ShowTooltip(_focusIndex.Value);
            InvalidateVisual();
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            _focusIndex ??= at;
            _owner.Select(_focusIndex.Value);
        }
        else if (e.Key == Key.Escape && _tipOpen)
        {
            e.Handled = true;
            HideTooltip(0);
        }
    }

    // ---- drawing ----------------------------------------------------------------------------------------------

    private void EnsureText()
    {
        var palette = _owner.Palette;
        if (_textVersion == palette.Version)
        {
            return;
        }
        _textVersion = palette.Version;
        _months.Clear();
        var seen = new Dictionary<int, int>();
        foreach (var (month, column, label) in Model.Months)
        {
            var occurrence = seen.TryGetValue(month % 12, out var n) ? n : 0;
            seen[month % 12] = occurrence + 1;
            _months.Add(($"{month % 12}-{(occurrence == 0 ? 0 : 1)}", ChartsBText.Make(label, palette, palette.TypeXS, palette.TextMuted), column));
        }
        var culture = _owner.EffectiveCulture;
        for (var row = 0; row < 7; row++)
        {
            var weekday = (row + (int)_owner.FirstDayOfWeek) % 7;
            _weekdays[row] = weekday % 2 == 1
                ? ChartsBText.Make(culture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)weekday), palette, palette.TypeXS, palette.TextMuted)
                : null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }
        EnsureText();
        var palette = _owner.Palette;
        var model = Model;
        var cell = Cell;
        var step = Step;
        var left = CellsLeft;
        var top = MonthsHeight + RowGap;
        var radius = cell * 0.28;
        var scrolling = ScrollMax > 0;
        using var fade = context.PushOpacity(_reveal.Fade);
        using (scrolling ? context.PushClip(new Rect(left - 6, -6, width - left + 12, Bounds.Height + 12)) : default(DrawingContext.PushedState?))
        {
            foreach (var (key, text, column) in _months)
            {
                var x = left + (_monthX.TryGetValue(key, out var mx) ? mx.Value : column) * step - _scroll;
                context.DrawText(text, new Point(x, (MonthsHeight - text.Height) / 2));
            }
            if (_reveal.IsRevealed)
            {
                var t = _clock.IsRunning ? _clock.Time : double.PositiveInfinity;
                var animating = _clock.IsRunning;
                var count = Math.Min(_tracks.Length, model.Weeks * 7);
                var firstColumn = Math.Max(0, (int)Math.Floor(_scroll / step) - 1);
                var lastColumn = Math.Min(model.Weeks - 1, (int)Math.Ceiling((_scroll + width) / step) + 1);
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    for (var row = 0; row < 7; row++)
                    {
                        var p = column * 7 + row;
                        if (p >= count)
                        {
                            break;
                        }
                        var (color, scale, opacity) = Evaluate(_tracks[p], t);
                        var index = model.IndexAt(column, row);
                        if (index >= 0)
                        {
                            opacity *= 1 - 0.8 * _dim[model.Levels[index]].Value;
                        }
                        if (scale <= 0.01 || opacity <= 0.002)
                        {
                            continue;
                        }
                        var rect = new Rect(left + column * step - _scroll, top + row * step, cell, cell);
                        var drawn = scale >= 0.999 ? rect : rect.CenterRect(new Rect(0, 0, cell * scale, cell * scale));
                        var brush = animating ? new ImmutableSolidColorBrush(color) : palette.Brush(color);
                        using (context.PushOpacity(opacity))
                        {
                            context.DrawRectangle(brush, null, new RoundedRect(drawn, drawn.Width * 0.28));
                        }
                    }
                }
                // Hover (mouse) and the keyboard cursor: a 1.5px outline 1px outside the square.
                var outline = palette.Pen(ChartsBMath.Alpha(palette.Foreground, 0.45), 1.5);
                DrawOutline(context, outline, _hover, cell, step, left, top, radius);
                if (_keyboardFocus && IsFocused && _focusIndex is { } focused && focused != _hover)
                {
                    DrawOutline(context, outline, focused, cell, step, left, top, radius);
                }
                if (_ringOpacity.Value > 0.001)
                {
                    var at = _ring.Value;
                    var rect = new Rect(left + at.X * step - _scroll - 3, top + at.Y * step - 3, cell + 6, cell + 6).Deflate(0.75);
                    using (context.PushOpacity(_ringOpacity.Value))
                    {
                        context.DrawRectangle(null, palette.Pen(palette.Foreground, 1.5), new RoundedRect(rect, radius + 2.25));
                    }
                }
            }
        }
        // Weekday labels stay pinned while a narrow grid scrolls under them.
        if (scrolling && _scroll > 0)
        {
            context.DrawRectangle(palette.Brush(palette.Surface), null, new Rect(-6, top - 6, left + 3, 7 * step + 6));
        }
        for (var row = 0; row < 7; row++)
        {
            if (_weekdays[row] is { } text)
            {
                context.DrawText(text, new Point(0, top + row * step + (cell - text.Height) / 2));
            }
        }
        DrawTooltip(context, palette);
    }

    private void DrawOutline(DrawingContext context, IPen outline, int index, double cell, double step, double left, double top, double radius)
    {
        if (index < 0 || index >= Model.Length)
        {
            return;
        }
        var (column, row) = Model.PositionOf(index);
        var rect = new Rect(left + column * step - _scroll, top + row * step, cell, cell).Inflate(1.75);
        context.DrawRectangle(null, outline, new RoundedRect(rect, radius + 1.75));
    }

    private void DrawTooltip(DrawingContext context, ChartsBPalette palette)
    {
        var opacity = _tipOpacity.Value;
        if (opacity <= 0.001 || _tipPrimary is null || _tipSecondary is null)
        {
            return;
        }
        var anchor = _tipPosition.Value;
        var textWidth = _tipWidth.Value;
        var lineOne = palette.TypeSM * 1.3;
        var lineTwo = palette.TypeXS * 1.35;
        var size = new Size(textWidth + 24 + 2, 7 + 8 + 2 + lineOne + lineTwo);
        var bubble = new Rect(anchor.X - size.Width / 2, anchor.Y - 8 - size.Height, size.Width, size.Height);
        var scale = _tipScale.Value;
        var origin = new Point(anchor.X, bubble.Bottom);
        using (context.PushTransform(Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(origin.X, origin.Y)))
        using (context.PushOpacity(opacity))
        {
            var border = ChartsBMath.MixIn(palette.Background, 0.14, palette.Foreground);
            context.DrawRectangle(palette.Brush(palette.Foreground), palette.Pen(border, 1),
                new RoundedRect(bubble.Deflate(0.5), palette.RadiusControl), palette.ShadowRaised);
            var body = new Rect(bubble.X + 13, bubble.Y + 8, textWidth, lineOne + lineTwo);
            using (context.PushClip(body.Inflate(8).Intersect(bubble.Deflate(1))))
            {
                var roll = _tipRoll.Value;
                var shift = palette.TypeSM * 0.3 * (_tipDirection == 0 ? 1 : _tipDirection);
                if (roll < 0.999 && _tipOldPrimary is not null && _tipOldSecondary is not null)
                {
                    // The old copy lifts away faster than the new one rises in.
                    var exit = ChartsBMath.Clamp(roll * 2, 0, 1);
                    using (context.PushOpacity(1 - exit))
                    {
                        DrawLines(context, _tipOldPrimary, _tipOldSecondary, body, -shift * exit, lineOne);
                    }
                }
                using (context.PushOpacity(roll))
                {
                    DrawLines(context, _tipPrimary, _tipSecondary, body, shift * (1 - roll), lineOne);
                }
            }
        }
    }

    private static void DrawLines(DrawingContext context, FormattedText primary, FormattedText secondary, Rect body, double offset, double lineOne)
    {
        context.DrawText(primary, new Point(body.X, body.Y + offset + (lineOne - primary.Height) / 2));
        context.DrawText(secondary, new Point(body.X, body.Y + lineOne + offset));
    }
}

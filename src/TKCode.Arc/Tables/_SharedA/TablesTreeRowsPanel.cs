using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How rows of a <see cref="TablesTreeRowsPanel"/> enter and leave.</summary>
internal sealed class TablesTreeRowMotion
{
    /// <summary>Rows of a just-opened branch fade in one after another (Arc TreeView: StaggerItem, at most 7 steps).</summary>
    public bool Stagger { get; init; }

    /// <summary>Horizontal offset an entering row starts from (TreeView -6px), springing to 0.</summary>
    public double EnterOffset { get; init; }

    /// <summary>Horizontal offset a leaving row travels to (TreeView -4px).</summary>
    public double ExitOffset { get; init; }

    public TimeSpan EnterFade { get; init; } = ArcMotionPresets.Fast;

    public Easing EnterFadeEasing { get; init; } = ArcEasings.Standard;

    public TimeSpan ExitFade { get; init; } = ArcMotionPresets.Fast;

    /// <summary>Duration of the per-row reveal used for decorations such as branch lines.</summary>
    public TimeSpan RevealDuration { get; init; } = ArcMotionPresets.Standard;

    /// <summary>Rows animated per inserted or removed run; the rest of a long run appears at once.</summary>
    public int AnimatedRunCap { get; init; } = 48;

    /// <summary>Animated rows per update, across runs.</summary>
    public int AnimatedUpdateCap { get; init; } = 96;

    /// <summary>When more rows than this are visible, changes apply instantly (Arc JsonViewer: 400).</summary>
    public int BulkLimit { get; init; } = int.MaxValue;
}

/// <summary>
/// A row of a <see cref="TablesTreeRowsPanel"/>: a self-drawn, recyclable control. The panel binds it to a row
/// model, arranges it (height clipped while it opens or closes) and drives its enter / exit opacity and offset.
/// </summary>
internal abstract class TablesTreeRow : Control
{
    private readonly TranslateTransform _shift = new();

    protected TablesTreeRow()
    {
        RenderTransform = _shift;
        ClipToBounds = true;
        Focusable = true;
    }

    public object? Model { get; private set; }

    public object? Key { get; private set; }

    /// <summary>A leaving row kept on screen while its space closes.</summary>
    public bool IsGhost { get; private set; }

    /// <summary>0..1 reveal of an entering row (decorations such as branch lines grow with it).</summary>
    public double Reveal { get; private set; } = 1;

    internal void Bind(object? model, object? key, bool ghost)
    {
        var previous = Model;
        var sameKey = key is not null && Equals(key, Key);
        Model = model;
        Key = key;
        IsGhost = ghost;
        IsHitTestVisible = !ghost && model is not null;
        Focusable = !ghost && model is not null;
        OnBound(previous, sameKey);
        InvalidateVisual();
    }

    /// <summary>Re-reads the bound model's state (selection, tab stop, label, open state).</summary>
    public void Refresh() => Bind(Model, Key, IsGhost);

    internal void SetMotion(double opacity, double offset, double reveal)
    {
        if (Math.Abs(Opacity - opacity) > 0.0005)
        {
            Opacity = opacity;
        }
        if (Math.Abs(_shift.X - offset) > 0.001)
        {
            _shift.X = offset;
        }
        if (Math.Abs(Reveal - reveal) > 0.0005)
        {
            Reveal = reveal;
            InvalidateVisual();
        }
    }

    /// <summary>Called after the model changed; <paramref name="sameKey"/> means the same logical row was rebound (state may animate).</summary>
    protected abstract void OnBound(object? previous, bool sameKey);
}

/// <summary>
/// Shared row engine of TreeView and JsonViewer: a flat list of visible rows with fixed slot height,
/// virtualized against the effective viewport (works inside its own ScrollViewer or a page scroller), with
/// pooled self-drawn rows and Arc's row motion. The owner rebuilds the visible list on every change and calls
/// <see cref="SetRows"/>; the panel diffs it by key, so opened branches grow in (each row's height springs from
/// zero, P6 smooth) and closed ones shrink away while their rows fade, without per-row controls staying alive.
/// Only rows near the viewport animate (up to <see cref="TablesTreeRowMotion.AnimatedRunCap"/> per run), so
/// thousands of rows cost the same as a screenful. A <see cref="Primitives.GlidingIndicator"/> under the rows
/// marks the selected / current row and glides between rows (P7).
/// Row motion is code-driven (one <see cref="MotionValue{T}"/> clock per opened or closed run): rows are pooled
/// and self-drawn, and each row's height, delay and fade depend on its place in the run, which ControlTheme
/// transitions on individual controls cannot express.
/// </summary>
internal abstract class TablesTreeRowsPanel : Control
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TablesTreeRowsPanel, double>(nameof(RowHeight), 36);

    static TablesTreeRowsPanel()
    {
        AffectsMeasure<TablesTreeRowsPanel>(RowHeightProperty);
    }

    private static readonly IReadOnlyList<object> s_empty = Array.Empty<object>();

    private IReadOnlyList<object> _rows = s_empty;
    private Dictionary<object, int> _index = new();
    private readonly Dictionary<object, TablesTreeRow> _live = new();
    private readonly Stack<TablesTreeRow> _pool = new();
    private readonly List<object> _scratchKeys = new();
    private readonly List<EnterBlock> _blocks = new();
    private readonly List<ExitGap> _gaps = new();
    private readonly List<AnimEntry> _entries = new();
    private double[] _deficitPrefix = new double[1];
    private double[] _gapPrefix = new double[1];
    private Rect _viewport;
    private bool _hasViewport;
    private int _realizedFirst = -1;
    private int _realizedLast = -1;
    private object? _highlightKey;
    private bool _highlightPending;
    private bool _highlightAnimate;
    private Rect _highlightTarget;
    private bool _highlightShown;
    private double _arrangedWidth;

    protected TablesTreeRowsPanel()
    {
        Highlight = new Primitives.GlidingIndicator { Spring = ArcSpring.Morph };
        LogicalChildren.Add(Highlight);
        VisualChildren.Add(Highlight);
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>Selection / current-row fill under the rows.</summary>
    public Primitives.GlidingIndicator Highlight { get; }

    /// <summary>Inset of the highlight inside a row slot.</summary>
    public Thickness HighlightInset { get; set; }

    public TablesTreeRowMotion Motion { get; set; } = new();

    /// <summary>Key of the row that must stay realized (the focused, tab-stop row).</summary>
    public object? PinnedKey { get; set; }

    public IReadOnlyList<object> Rows => _rows;

    public int Count => _rows.Count;

    /// <summary>Whether rows are still opening or closing.</summary>
    public bool HasRowMotion => _blocks.Count > 0 || _gaps.Count > 0;

    public int IndexOfKey(object? key) => key is not null && _index.TryGetValue(key, out var index) ? index : -1;

    /// <summary>The realized row for <paramref name="key"/>, if any.</summary>
    public TablesTreeRow? RowFor(object? key) => key is not null && _live.TryGetValue(key, out var row) ? row : null;

    /// <summary>Realized live rows (tests and automation).</summary>
    public IEnumerable<TablesTreeRow> RealizedRows => _live.Values;

    protected abstract TablesTreeRow CreateRow();

    protected abstract object KeyOf(object model);

    #region Rows and diffing

    /// <summary>
    /// Replaces the visible rows. With <paramref name="animate"/> the change is diffed by key: inserted runs open
    /// and removed runs close (only near the viewport and with full motion). <paramref name="staggerAfterKey"/> is
    /// the branch that was just opened; its new rows stagger in.
    /// </summary>
    public void SetRows(IReadOnlyList<object> rows, bool animate, object? staggerAfterKey = null)
    {
        var old = _rows;
        var newIndex = new Dictionary<object, int>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            newIndex.TryAdd(KeyOf(rows[i]), i);
        }
        var canAnimate = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full && old.Count > 0
                         && rows.Count <= Motion.BulkLimit && old.Count <= Motion.BulkLimit;
        List<Run>? removed = null;
        List<Run>? inserted = null;
        if (canAnimate && !Diff(old, rows, newIndex, out removed, out inserted))
        {
            canAnimate = false;
        }
        if (!canAnimate)
        {
            _rows = rows;
            _index = newIndex;
            CancelAnimations();
            RebindLive();
            InvalidateMeasure();
            return;
        }
        // Positions of the old layout decide which runs are near the viewport.
        var window = RealizationWindow();
        var budget = Motion.AnimatedUpdateCap;
        var newGaps = new List<ExitGap>();
        foreach (var run in removed!)
        {
            var top = RowTop(run.Start);
            var bottom = RowTop(run.Start + run.Count);
            var count = Math.Min(Math.Min(run.Count, Motion.AnimatedRunCap), budget);
            if (count <= 0 || bottom < window.Top || top > window.Bottom)
            {
                continue;
            }
            budget -= count;
            var gap = new ExitGap(count)
            {
                AnchorKey = run.Anchor < rows.Count ? KeyOf(rows[run.Anchor]) : null
            };
            for (var k = 0; k < count; k++)
            {
                var model = old[run.Start + k];
                var key = KeyOf(model);
                gap.Models[k] = model;
                gap.Keys[k] = key;
                gap.Heights0[k] = RowHeightAt(run.Start + k);
                if (_live.Remove(key, out var row))
                {
                    row.Bind(model, key, true);
                    gap.Rows[k] = row;
                }
            }
            newGaps.Add(gap);
        }
        var oldIndex = _index;
        _rows = rows;
        _index = newIndex;

        foreach (var run in inserted!)
        {
            var count = Math.Min(Math.Min(run.Count, Motion.AnimatedRunCap), budget);
            if (count <= 0)
            {
                continue;
            }
            // Where the run lands in the old layout: right after the row before it.
            var before = run.Start > 0 && oldIndex.TryGetValue(KeyOf(rows[run.Start - 1]), out var b) ? b + 1 : 0;
            var top = RowTopOld(before);
            if (top > window.Bottom || top + count * RowHeight < window.Top)
            {
                continue;
            }
            budget -= count;
            var block = new EnterBlock(count)
            {
                Stagger = Motion.Stagger && staggerAfterKey is not null && run.Start > 0 && Equals(KeyOf(rows[run.Start - 1]), staggerAfterKey)
            };
            for (var k = 0; k < count; k++)
            {
                block.Keys[k] = KeyOf(rows[run.Start + k]);
            }
            // Rows coming back while their old space is still closing continue from that height.
            var reentered = FindGapHolding(block.Keys[0]);
            if (reentered is not null)
            {
                block.S0 = reentered.Factor;
                RemoveGap(reentered);
            }
            StartBlock(block);
        }
        foreach (var gap in newGaps)
        {
            StartGap(gap);
        }
        RebindLive();
        InvalidateMeasure();
    }

    private readonly record struct Run(int Start, int Count, int Anchor);

    /// <summary>Linear merge of two pre-order row lists. False when rows were reordered (no animation then).</summary>
    private bool Diff(IReadOnlyList<object> old, IReadOnlyList<object> rows, Dictionary<object, int> newIndex,
                      out List<Run> removed, out List<Run> inserted)
    {
        removed = new List<Run>();
        inserted = new List<Run>();
        var oldIndex = _index;
        int i = 0, j = 0;
        while (i < old.Count || j < rows.Count)
        {
            if (i < old.Count && j < rows.Count && Equals(KeyOf(old[i]), KeyOf(rows[j])))
            {
                i++;
                j++;
                continue;
            }
            if (i < old.Count && !newIndex.ContainsKey(KeyOf(old[i])))
            {
                var start = i;
                while (i < old.Count && !newIndex.ContainsKey(KeyOf(old[i])))
                {
                    i++;
                }
                removed.Add(new Run(start, i - start, j));
                continue;
            }
            if (j < rows.Count && !oldIndex.ContainsKey(KeyOf(rows[j])))
            {
                var start = j;
                while (j < rows.Count && !oldIndex.ContainsKey(KeyOf(rows[j])))
                {
                    j++;
                }
                inserted.Add(new Run(start, j - start, start));
                continue;
            }
            return false;
        }
        return true;
    }

    /// <summary>Live rows whose key survived get the new model; the rest return to the pool.</summary>
    private void RebindLive()
    {
        _scratchKeys.Clear();
        foreach (var pair in _live)
        {
            if (_index.TryGetValue(pair.Key, out var index))
            {
                var model = _rows[index];
                pair.Value.Bind(model, pair.Key, false);
            }
            else
            {
                _scratchKeys.Add(pair.Key);
            }
        }
        foreach (var key in _scratchKeys)
        {
            if (_live.Remove(key, out var row))
            {
                Recycle(row);
            }
        }
        _scratchKeys.Clear();
    }

    #endregion

    #region Animation state

    private sealed class EnterBlock
    {
        public EnterBlock(int count) => Keys = new object[count];

        public object[] Keys { get; }
        public double S0 { get; set; }
        public bool Stagger { get; init; }
        public MotionValue<double>? Clock { get; set; }
        public double Duration { get; set; }
        public double Time => Clock?.Value ?? Duration;
        public double Factor { get; set; } = 1;
    }

    private sealed class ExitGap
    {
        public ExitGap(int count)
        {
            Models = new object[count];
            Keys = new object[count];
            Heights0 = new double[count];
            Rows = new TablesTreeRow?[count];
        }

        public object[] Models { get; }
        public object[] Keys { get; }
        public double[] Heights0 { get; }
        public TablesTreeRow?[] Rows { get; }
        public object? AnchorKey { get; init; }
        public int At { get; set; }
        public MotionValue<double>? Clock { get; set; }
        public double Duration { get; set; }
        public double Time => Clock?.Value ?? Duration;
        public double Factor { get; set; } = 1;
        public double Height { get; set; }
        public double Top { get; set; }
    }

    private readonly record struct AnimEntry(int Index, EnterBlock Block, int Order);

    private static double SettleSeconds => ArcMotionPresets.Smooth.EstimateSettle().TotalSeconds;

    private void StartBlock(EnterBlock block)
    {
        var maxDelay = block.Stagger ? Math.Min(block.Keys.Length - 1, 7) * ArcMotionPresets.StaggerItem.TotalSeconds : 0;
        block.Duration = maxDelay + Math.Max(SettleSeconds, Math.Max(Motion.EnterFade.TotalSeconds, Motion.RevealDuration.TotalSeconds));
        block.Clock = CreateClock(block.Duration);
        _blocks.Add(block);
    }

    private void StartGap(ExitGap gap)
    {
        gap.Duration = Math.Max(SettleSeconds, Motion.ExitFade.TotalSeconds);
        gap.Clock = CreateClock(gap.Duration);
        _gaps.Add(gap);
    }

    private MotionValue<double> CreateClock(double duration)
    {
        var clock = new MotionValue<double>(this, 0, MotionVectors.Double);
        clock.Changed += _ => InvalidateMeasure();
        _ = clock.AnimateTo(duration, MotionSpec.Tween(TimeSpan.FromSeconds(duration), new LinearEasing()));
        return clock;
    }

    private ExitGap? FindGapHolding(object key)
    {
        foreach (var gap in _gaps)
        {
            if (Array.IndexOf(gap.Keys, key) >= 0)
            {
                return gap;
            }
        }
        return null;
    }

    private void RemoveGap(ExitGap gap)
    {
        gap.Clock?.Stop();
        foreach (var row in gap.Rows)
        {
            if (row is not null)
            {
                Recycle(row);
            }
        }
        Array.Clear(gap.Rows);
        _gaps.Remove(gap);
    }

    private void CancelAnimations()
    {
        foreach (var block in _blocks)
        {
            block.Clock?.Stop();
        }
        _blocks.Clear();
        foreach (var gap in _gaps.ToArray())
        {
            RemoveGap(gap);
        }
        _entries.Clear();
        _deficitPrefix = new double[1];
        _gapPrefix = new double[1];
    }

    private static double EnterFactor(double s0, double t, double duration)
    {
        if (t >= duration)
        {
            return 1;
        }
        var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, s0 - 1, 0, t);
        return Math.Clamp(1 + x, 0, 1);
    }

    private static double ExitFactor(double t, double duration)
    {
        if (t >= duration)
        {
            return 0;
        }
        var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, 1, 0, t);
        return Math.Clamp(x, 0, 1);
    }

    /// <summary>Recomputes block factors, the sorted animated rows and the gap positions for the current time.</summary>
    private void UpdateAnimationState()
    {
        for (var i = _blocks.Count - 1; i >= 0; i--)
        {
            var block = _blocks[i];
            if (block.Clock is null || (!block.Clock.IsAnimating && block.Time >= block.Duration - 1e-6))
            {
                _blocks.RemoveAt(i);
            }
        }
        for (var i = _gaps.Count - 1; i >= 0; i--)
        {
            var gap = _gaps[i];
            var at = gap.AnchorKey is null ? _rows.Count : IndexOfKey(gap.AnchorKey);
            if (at < 0 || gap.Clock is null || (!gap.Clock.IsAnimating && gap.Time >= gap.Duration - 1e-6))
            {
                RemoveGap(gap);
                continue;
            }
            gap.At = at;
        }
        _entries.Clear();
        var h = RowHeight;
        foreach (var block in _blocks)
        {
            block.Factor = EnterFactor(block.S0, block.Time, block.Duration);
            for (var k = 0; k < block.Keys.Length; k++)
            {
                if (_index.TryGetValue(block.Keys[k], out var index))
                {
                    _entries.Add(new AnimEntry(index, block, k));
                }
            }
        }
        _entries.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        if (_deficitPrefix.Length < _entries.Count + 1)
        {
            _deficitPrefix = new double[_entries.Count + 1];
        }
        _deficitPrefix[0] = 0;
        for (var k = 0; k < _entries.Count; k++)
        {
            _deficitPrefix[k + 1] = _deficitPrefix[k] + h * (1 - _entries[k].Block.Factor);
        }
        _gaps.Sort(static (a, b) => a.At.CompareTo(b.At));
        if (_gapPrefix.Length < _gaps.Count + 1)
        {
            _gapPrefix = new double[_gaps.Count + 1];
        }
        _gapPrefix[0] = 0;
        for (var k = 0; k < _gaps.Count; k++)
        {
            var gap = _gaps[k];
            gap.Factor = ExitFactor(gap.Time, gap.Duration);
            var total = 0.0;
            foreach (var height in gap.Heights0)
            {
                total += height;
            }
            gap.Height = total * gap.Factor;
            _gapPrefix[k + 1] = _gapPrefix[k] + gap.Height;
        }
        for (var k = 0; k < _gaps.Count; k++)
        {
            var gap = _gaps[k];
            gap.Top = RowTopRowsOnly(gap.At) + _gapPrefix[k];
        }
    }

    #endregion

    #region Geometry

    private double RowTopRowsOnly(int index)
    {
        var top = index * RowHeight;
        if (_entries.Count > 0)
        {
            top -= _deficitPrefix[CountEntriesBefore(index)];
        }
        return top;
    }

    /// <summary>Current top of row <paramref name="index"/> (index == Count gives the extent).</summary>
    public double RowTop(int index)
    {
        var top = RowTopRowsOnly(index);
        if (_gaps.Count > 0)
        {
            top += _gapPrefix[CountGapsAtOrBefore(index)];
        }
        return top;
    }

    /// <summary>Top of a row in the layout before the last <see cref="SetRows"/> call (uses the same state).</summary>
    private double RowTopOld(int index) => RowTop(index);

    /// <summary>Current height of row <paramref name="index"/>.</summary>
    public double RowHeightAt(int index)
    {
        if (_entries.Count > 0)
        {
            var k = CountEntriesBefore(index);
            if (k < _entries.Count && _entries[k].Index == index)
            {
                return RowHeight * _entries[k].Block.Factor;
            }
        }
        return RowHeight;
    }

    /// <summary>Where a row ends up once every running animation has settled.</summary>
    public double SettledRowTop(int index) => index * RowHeight;

    /// <summary>Total height of the rows right now.</summary>
    public double Extent => RowTop(_rows.Count);

    private int CountEntriesBefore(int index)
    {
        int lo = 0, hi = _entries.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (_entries[mid].Index < index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    private int CountGapsAtOrBefore(int index)
    {
        int lo = 0, hi = _gaps.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (_gaps[mid].At <= index)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    /// <summary>First row whose bottom is below <paramref name="y"/>.</summary>
    private int FirstRowEndingAfter(double y)
    {
        int lo = 0, hi = _rows.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (RowTop(mid + 1) > y)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }
        return lo;
    }

    /// <summary>Last row whose top is above <paramref name="y"/>.</summary>
    private int LastRowStartingBefore(double y)
    {
        int lo = 0, hi = _rows.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (RowTop(mid) < y)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo - 1;
    }

    private Rect RealizationWindow()
    {
        if (!_hasViewport)
        {
            return new Rect(0, 0, 0, 2000);
        }
        if (_viewport.Width <= 0 || _viewport.Height <= 0)
        {
            return default;
        }
        var buffer = Math.Max(RowHeight * 4, _viewport.Height * 0.5);
        return new Rect(0, _viewport.Top - buffer, 0, _viewport.Height + 2 * buffer);
    }

    private void OnEffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e)
    {
        var first = !_hasViewport;
        _viewport = e.EffectiveViewport;
        _hasViewport = true;
        if (_rows.Count == 0)
        {
            return;
        }
        if (_viewport.Width <= 0 || _viewport.Height <= 0)
        {
            return;
        }
        var visibleFirst = FirstRowEndingAfter(_viewport.Top);
        var visibleLast = LastRowStartingBefore(_viewport.Bottom);
        if (first || visibleFirst < _realizedFirst || visibleLast > _realizedLast)
        {
            InvalidateMeasure();
        }
    }

    #endregion

    #region Realization and layout

    private TablesTreeRow Rent()
    {
        if (_pool.TryPop(out var row))
        {
            row.IsVisible = true;
            return row;
        }
        row = CreateRow();
        LogicalChildren.Add(row);
        VisualChildren.Add(row);
        return row;
    }

    private void Recycle(TablesTreeRow row)
    {
        row.Bind(null, null, false);
        row.SetMotion(1, 0, 1);
        row.IsVisible = false;
        _pool.Push(row);
    }

    /// <summary>Realizes the row at <paramref name="index"/> now (it then stays while it is <see cref="PinnedKey"/>).</summary>
    public TablesTreeRow? Realize(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return null;
        }
        var key = KeyOf(_rows[index]);
        if (_live.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var row = Rent();
        row.Bind(_rows[index], key, false);
        _live[key] = row;
        var width = _arrangedWidth > 0 ? _arrangedWidth : Bounds.Width;
        row.Measure(new Size(width, RowHeight));
        row.Arrange(new Rect(0, RowTop(index), width, RowHeightAt(index)));
        InvalidateMeasure();
        return row;
    }

    /// <summary>Marks the highlighted row; it glides there when <paramref name="animate"/> (and lands in place otherwise).</summary>
    public void SetHighlight(object? key, bool animate)
    {
        _highlightKey = key;
        _highlightAnimate = animate;
        _highlightPending = true;
        InvalidateArrange();
    }

    public object? HighlightKey => _highlightKey;

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateAnimationState();
        var h = RowHeight;
        var window = RealizationWindow();
        int first = 0, last = -1;
        if (_rows.Count > 0 && window.Height > 0)
        {
            first = FirstRowEndingAfter(window.Top);
            last = Math.Min(_rows.Count - 1, LastRowStartingBefore(window.Bottom));
        }
        _realizedFirst = first;
        _realizedLast = last;
        var pinnedIndex = IndexOfKey(PinnedKey);

        _scratchKeys.Clear();
        foreach (var pair in _live)
        {
            var index = IndexOfKey(pair.Key);
            if (index < 0 || ((index < first || index > last) && index != pinnedIndex))
            {
                _scratchKeys.Add(pair.Key);
            }
        }
        foreach (var key in _scratchKeys)
        {
            if (_live.Remove(key, out var row))
            {
                Recycle(row);
            }
        }
        _scratchKeys.Clear();
        for (var i = first; i <= last; i++)
        {
            EnsureLive(i);
        }
        if (pinnedIndex >= 0)
        {
            EnsureLive(pinnedIndex);
        }
        foreach (var gap in _gaps)
        {
            var y = gap.Top;
            for (var k = 0; k < gap.Models.Length; k++)
            {
                var height = gap.Heights0[k] * gap.Factor;
                var visible = height > 0.5 && y + height > window.Top && y < window.Bottom;
                if (visible && gap.Rows[k] is null)
                {
                    var row = Rent();
                    row.Bind(gap.Models[k], gap.Keys[k], true);
                    gap.Rows[k] = row;
                }
                else if (!visible && gap.Rows[k] is { } row)
                {
                    Recycle(row);
                    gap.Rows[k] = null;
                }
                y += height;
            }
        }

        var rowSize = new Size(availableSize.Width, h);
        var width = 0.0;
        foreach (var row in _live.Values)
        {
            row.Measure(rowSize);
            width = Math.Max(width, row.DesiredSize.Width);
        }
        foreach (var gap in _gaps)
        {
            foreach (var row in gap.Rows)
            {
                row?.Measure(rowSize);
            }
        }
        Highlight.Measure(availableSize);
        return new Size(width, Extent);
    }

    private void EnsureLive(int index)
    {
        var model = _rows[index];
        var key = KeyOf(model);
        if (!_live.ContainsKey(key))
        {
            var row = Rent();
            row.Bind(model, key, false);
            _live[key] = row;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        _arrangedWidth = width;
        var h = RowHeight;
        foreach (var pair in _live)
        {
            var index = IndexOfKey(pair.Key);
            if (index < 0)
            {
                continue;
            }
            var row = pair.Value;
            var top = RowTop(index);
            var height = RowHeightAt(index);
            row.Arrange(new Rect(0, top, width, Math.Max(0, height)));
            ApplyEnterMotion(row, index);
        }
        foreach (var gap in _gaps)
        {
            var y = gap.Top;
            var p = Math.Clamp(gap.Time / Math.Max(Motion.ExitFade.TotalSeconds, 1e-6), 0, 1);
            var eased = ArcEasings.Standard.Ease(p);
            for (var k = 0; k < gap.Models.Length; k++)
            {
                var height = gap.Heights0[k] * gap.Factor;
                if (gap.Rows[k] is { } row)
                {
                    row.Arrange(new Rect(0, y, width, Math.Max(0, height)));
                    row.SetMotion(1 - eased, Motion.ExitOffset * eased, 1);
                }
                y += height;
            }
        }
        Highlight.Arrange(new Rect(0, 0, width, Math.Max(finalSize.Height, Extent)));
        UpdateHighlight(width);
        return new Size(width, Math.Max(finalSize.Height, Extent));
    }

    private void ApplyEnterMotion(TablesTreeRow row, int index)
    {
        if (_entries.Count == 0)
        {
            row.SetMotion(1, 0, 1);
            return;
        }
        var k = CountEntriesBefore(index);
        if (k >= _entries.Count || _entries[k].Index != index)
        {
            row.SetMotion(1, 0, 1);
            return;
        }
        var entry = _entries[k];
        var block = entry.Block;
        var delay = block.Stagger ? Math.Min(entry.Order, 7) * ArcMotionPresets.StaggerItem.TotalSeconds : 0;
        var t = Math.Max(0, block.Time - delay);
        var fade = Math.Clamp(t / Math.Max(Motion.EnterFade.TotalSeconds, 1e-6), 0, 1);
        var opacity = block.Time >= block.Duration ? 1 : Motion.EnterFadeEasing.Ease(fade);
        var offset = 0.0;
        if (Motion.EnterOffset != 0 && block.Time < block.Duration)
        {
            var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, 1, 0, t);
            offset = Motion.EnterOffset * Math.Clamp(x, -0.2, 1);
        }
        var reveal = block.Time >= block.Duration
            ? 1
            : ArcEasings.Enter.Ease(Math.Clamp(t / Math.Max(Motion.RevealDuration.TotalSeconds, 1e-6), 0, 1));
        row.SetMotion(opacity, offset, reveal);
    }

    private void UpdateHighlight(double width)
    {
        var index = IndexOfKey(_highlightKey);
        if (index < 0)
        {
            if (_highlightShown)
            {
                Highlight.Hide(false);
                _highlightShown = false;
            }
            _highlightPending = false;
            return;
        }
        var inset = HighlightInset;
        var target = new Rect(inset.Left, RowTop(index) + inset.Top, Math.Max(0, width - inset.Left - inset.Right),
            Math.Max(0, RowHeightAt(index) - inset.Top - inset.Bottom));
        if (_highlightPending || !_highlightShown)
        {
            Highlight.MoveTo(target, _highlightAnimate && _highlightShown);
            _highlightPending = false;
            _highlightShown = true;
        }
        else if (target != _highlightTarget)
        {
            // The row moved with the layout; keep gliding if a glide is under way, otherwise follow it exactly.
            var gliding = Highlight.CurrentRect != _highlightTarget;
            Highlight.MoveTo(target, gliding);
        }
        _highlightTarget = target;
    }

    #endregion
}

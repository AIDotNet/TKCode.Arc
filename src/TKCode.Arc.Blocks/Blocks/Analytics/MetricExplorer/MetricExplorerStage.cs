using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The block's body: a grid of cards that opens into the chart. The surfaces behind both are drawn by one child and follow
/// their layout rectangles on a spring (the opened card's surface grows into the chart, shrinks back on close); the card
/// contents and the chart content fade in with a short rise and blur when they appear. The block's height changes at once,
/// the surface and the content animate into it.
/// </summary>
/// <remarks>
/// Code-driven motion: the entrance of one element is an opacity, a rise and a blur that all follow one progress value
/// started with per-element delays (chart 120ms, cards 80 / 160ms), and each element's progress must restart from where it is
/// when the state flips again; one loop advances them all and applies them to plain controls.
/// </remarks>
internal sealed class MetricExplorerStage : Panel
{
    private readonly MetricExplorerSurfaces _surfaces = new();
    private readonly MetricExplorerPanel _panel = new();
    private readonly List<MetricExplorerCard> _cards = new();
    private readonly List<AnalyticsTween> _cardEntrance = new();
    private readonly List<TranslateTransform> _cardShift = new();
    private readonly TranslateTransform _panelShift = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _panelEntrance = new(1);
    private MetricExplorerGeometry _geometry = new();
    private MetricExplorerState? _state;
    private bool _open;
    private int _selected;
    private bool _compact;
    private Rect[] _slots = [];
    private double _width;
    private double _panelHeight;
    private bool _configured;
    private bool _animateArrange;

    public MetricExplorerStage()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Children.Add(_surfaces);
        _panel.RenderTransform = _panelShift;
        _panel.IsVisible = false;
        Children.Add(_panel);
        _panel.MetricChosen += i => MetricChosen?.Invoke(i);
        _panel.RangeChosen += i => RangeChosen?.Invoke(i);
        _panel.CloseRequested += () => CloseRequested?.Invoke();
        _panel.ScrubChanged += i => ScrubChanged?.Invoke(i);
    }

    /// <summary>Raised when a card is activated: its index and whether the keyboard did it.</summary>
    public event Action<int, bool>? OpenRequested;

    /// <summary>Raised by the close button and Escape.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when a tab is chosen.</summary>
    public event Action<int>? MetricChosen;

    /// <summary>Raised when a range is chosen.</summary>
    public event Action<int>? RangeChosen;

    /// <summary>Raised when the inspected day changes.</summary>
    public event Action<int?>? ScrubChanged;

    /// <summary>The open chart content (tests).</summary>
    internal MetricExplorerPanel Panel => _panel;

    /// <summary>The surfaces (tests).</summary>
    internal MetricExplorerSurfaces Surfaces => _surfaces;

    /// <summary>The cards (tests).</summary>
    internal IReadOnlyList<MetricExplorerCard> Cards => _cards;

    /// <summary>The cards' slots in the collapsed grid (tests).</summary>
    internal IReadOnlyList<Rect> Slots => _slots;

    /// <summary>True while an entrance runs (tests).</summary>
    internal bool IsEntering => _loop.IsActive;

    /// <summary>Opacity of card <paramref name="index"/>'s content now (tests).</summary>
    internal double CardOpacity(int index) => _cards[index].Opacity;

    /// <summary>Opacity of the chart content now (tests).</summary>
    internal double PanelOpacity => _panel.Opacity;

    /// <summary>The shared geometry.</summary>
    public MetricExplorerGeometry Geometry
    {
        get => _geometry;
        set
        {
            _geometry = value;
            _surfaces.Radius = value.SurfaceRadius;
            _panel.Geometry = value;
            foreach (var card in _cards)
            {
                card.Geometry = value;
            }
            InvalidateMeasure();
        }
    }

    /// <summary>Moves focus to card <paramref name="index"/> (with the keyboard ring unless <paramref name="method"/> is Pointer).</summary>
    public void FocusCard(int index, NavigationMethod method = NavigationMethod.Tab)
    {
        if ((uint)index < (uint)_cards.Count)
        {
            _cards[index].Focus(method);
        }
    }

    /// <summary>Moves keyboard focus to the selected tab of the open chart.</summary>
    public bool FocusTabs() => _panel.FocusTabs();

    /// <summary>Shows <paramref name="state"/>, collapsed or open on metric <c>state.Metric</c>.</summary>
    public void Configure(MetricExplorerState state, bool open, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var attached = this.IsAttachedToVisualTree();
        var wasOpen = _open;
        var animateOpen = animate && _configured && attached && open != wasOpen;
        _animateArrange = animateOpen;
        _state = state;
        _selected = Math.Clamp(state.Metric, 0, Math.Max(0, state.Series.Length - 1));
        _open = open;

        EnsureCards(state.Series.Length);
        if (state.Series.Length == 0 || state.Ranges.Length == 0)
        {
            _open = false;
            _panel.IsVisible = false;
            _configured = true;
            InvalidateMeasure();
            return;
        }
        var range = state.Ranges[Math.Clamp(state.Range, 0, state.Ranges.Length - 1)];
        for (var i = 0; i < state.Series.Length; i++)
        {
            SetCard(_cards[i], state.Series[i], range, state.Texts);
        }
        _panel.IsVisible = open;
        if (open)
        {
            _panel.Apply(state, animate && _configured && wasOpen);
        }
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].Focusable = !open;
            _cards[i].IsHitTestVisible = !open;
            AutomationProperties.SetAccessibilityView(_cards[i], open ? AccessibilityView.Raw : AccessibilityView.Content);
        }

        if (animateOpen)
        {
            if (open)
            {
                for (var i = 0; i < _cards.Count; i++)
                {
                    JumpCard(i, 0);
                }
                StartEntrance(ref _panelEntrance, TimeSpan.FromMilliseconds(120), level);
            }
            else
            {
                _panelEntrance.Jump(0);
                for (var i = 0; i < _cards.Count; i++)
                {
                    var delay = TimeSpan.FromMilliseconds(i == _selected ? 160 : 80);
                    var tween = _cardEntrance[i];
                    tween.Jump(0);
                    StartEntrance(ref tween, delay, level);
                    _cardEntrance[i] = tween;
                }
            }
            _loop.Start();
        }
        else if (!_configured || open != wasOpen)
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                JumpCard(i, open ? 0 : 1);
            }
            _panelEntrance.Jump(open ? 1 : 0);
        }
        _configured = true;
        ApplyEntrance();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void JumpCard(int i, double value)
    {
        var tween = _cardEntrance[i];
        tween.Jump(value);
        _cardEntrance[i] = tween;
    }

    // Full: 240ms on the enter curve after the delay; reduced: a 120ms fade; off: at once.
    private static void StartEntrance(ref AnalyticsTween tween, TimeSpan delay, ArcMotionLevel level)
    {
        switch (level)
        {
            case ArcMotionLevel.Full:
                tween.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter, delay);
                break;
            case ArcMotionLevel.Reduced:
                tween.Start(1, ArcMotionPresets.Instant, AnalyticsEasings.EaseOut);
                break;
            default:
                tween.Jump(1);
                break;
        }
    }

    private bool Advance(double dt)
    {
        var moving = _panelEntrance.Advance(dt);
        for (var i = 0; i < _cardEntrance.Count; i++)
        {
            var tween = _cardEntrance[i];
            moving |= tween.Advance(dt);
            _cardEntrance[i] = tween;
        }
        ApplyEntrance();
        return moving;
    }

    private void ApplyEntrance()
    {
        var full = ArcMotion.IsFull(this);
        Apply(_panel, _panelShift, _panelEntrance.Value, full);
        for (var i = 0; i < _cards.Count; i++)
        {
            Apply(_cards[i], _cardShift[i], _open ? 0 : _cardEntrance[i].Value, full);
        }
    }

    private static void Apply(Control control, TranslateTransform shift, double progress, bool full)
    {
        progress = Math.Clamp(progress, 0, 1);
        control.Opacity = progress;
        shift.Y = full ? 6 * (1 - progress) : 0;
        var radius = full ? 4 * (1 - progress) : 0;
        control.Effect = radius < 0.05 ? null : ArcBlur.Get(radius);
    }

    private void EnsureCards(int count)
    {
        if (_cards.Count == count)
        {
            return;
        }
        foreach (var card in _cards)
        {
            Children.Remove(card);
        }
        _cards.Clear();
        _cardEntrance.Clear();
        _cardShift.Clear();
        _surfaces.SetCount(count);
        for (var i = 0; i < count; i++)
        {
            var card = new MetricExplorerCard { Geometry = _geometry };
            var shift = new TranslateTransform();
            card.RenderTransform = shift;
            var index = i;
            card.Invoked += (_, keyboard) => OpenRequested?.Invoke(index, keyboard);
            card.HoverChanged += (_, hovered) => _surfaces.SetHover(index, hovered);
            _cards.Add(card);
            _cardShift.Add(shift);
            _cardEntrance.Add(new AnalyticsTween(1));
            // Cards sit between the surfaces (first) and the chart content (last).
            Children.Insert(1 + i, card);
        }
    }

    private void SetCard(MetricExplorerCard card, MetricExplorerSeries series, ExplorerRange range, MetricExplorerTexts texts)
    {
        var latest = series.Latest;
        var change = series.Change(latest, series.At(range.Days));
        var (direction, tone) = series.Trend(change);
        var word = direction switch { AnalyticsDirection.Up => texts.Up, AnalyticsDirection.Down => texts.Down, _ => texts.Flat };
        var changeText = series.ChangeText(change);
        var plain = !double.IsFinite(change)
            ? AnalyticsFormat.Missing
            : series.ChangeMode == ExplorerChangeMode.Points
                ? AnalyticsFormat.Points(Math.Abs(change), 1, false, texts.PointsUnit)
                : AnalyticsFormat.Percent(Math.Abs(change), 1);
        var name = string.Format(CultureInfo.CurrentCulture, texts.CardNameFormat, series.Title, series.ValueText(latest), word, plain,
            range.CardText);
        card.SetContent(series.Title, series.ValueText(latest), changeText, range.CardText, direction, tone,
            MetricExplorerMath.SparklineSamples(series.Values, range.Days, range.SparklineStep), name);
    }

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 760 : availableSize.Width;
        _width = width;
        _compact = _geometry.IsCompact(width);
        var count = _cards.Count;
        var columns = _geometry.ColumnsFor(width, count);
        var gap = _compact ? _geometry.CompactCardGap : _geometry.CardGap;
        var cardWidth = Math.Max(0, (width - gap * (columns - 1)) / columns);
        var rows = count == 0 ? 0 : (count + columns - 1) / columns;
        var heights = new double[rows];
        for (var i = 0; i < count; i++)
        {
            _cards[i].IsCompact = _compact;
            _cards[i].Measure(new Size(cardWidth, double.PositiveInfinity));
            heights[i / columns] = Math.Max(heights[i / columns], _cards[i].DesiredSize.Height);
        }
        _slots = new Rect[count];
        var y = 0.0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns && r * columns + c < count; c++)
            {
                _slots[r * columns + c] = new Rect(c * (cardWidth + gap), y, cardWidth, heights[r]);
            }
            y += heights[r] + gap;
        }
        var collapsed = rows == 0 ? 0 : y - gap;
        _surfaces.Measure(new Size(width, 0));
        if (_open)
        {
            _panel.Measure(new Size(width, double.PositiveInfinity));
            _panelHeight = _panel.DesiredSize.Height;
            return new Size(width, _panelHeight);
        }
        _panel.Measure(new Size(width, 0));
        return new Size(width, collapsed);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _surfaces.Arrange(new Rect(finalSize));
        for (var i = 0; i < _cards.Count && i < _slots.Length; i++)
        {
            _cards[i].Arrange(_slots[i]);
        }
        if (_open)
        {
            _panel.Arrange(new Rect(0, 0, finalSize.Width, _panelHeight));
        }
        else
        {
            _panel.Arrange(new Rect(0, 0, finalSize.Width, 0));
        }
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_open && i == _selected)
            {
                _surfaces.SetTarget(i, new Rect(0, 0, finalSize.Width, _panelHeight), 1, _animateArrange);
            }
            else
            {
                _surfaces.SetTarget(i, _slots[i], _open ? 0 : 1, _animateArrange);
            }
        }
        _animateArrange = false;
        return finalSize;
    }

    #endregion
}

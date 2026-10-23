using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A toggle whose state its owner sets (a click only reports itself): the grouping tabs, the MRR / Accounts switch and the
/// totals rows (<c>aria-pressed</c>).
/// </summary>
internal class GalaxyToggle : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(GalaxyToggle);

    protected override void Toggle()
    {
        // The owner decides what is on.
    }
}

/// <summary>One grouping tab ("All", "Plan" …).</summary>
internal sealed class GalaxyTab : GalaxyToggle
{
    protected override Type StyleKeyOverride => typeof(GalaxyTab);

    protected override AutomationPeer OnCreateAutomationPeer() => new GalaxyTabAutomationPeer(this);
}

internal sealed class GalaxyTabAutomationPeer : ToggleButtonAutomationPeer
{
    public GalaxyTabAutomationPeer(ToggleButton owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
}

/// <summary>One button of the MRR / Accounts switch.</summary>
internal sealed class GalaxySortButton : GalaxyToggle
{
    protected override Type StyleKeyOverride => typeof(GalaxySortButton);
}

/// <summary>
/// The grouping tabs (<c>role=tablist</c>): a row of <see cref="GalaxyTab"/>s on a muted track with one surface pill that
/// glides to the selected tab (<see cref="GlidingIndicator"/>, snappy spring). Arrows, Home and End move the selection;
/// only the selected tab is a tab stop. Scrolls sideways when narrower than its tabs.
/// </summary>
[TemplatePart(PillPart, typeof(GlidingIndicator))]
[TemplatePart(ItemsPart, typeof(StackPanel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
internal sealed class GalaxyTabStrip : TemplatedControl
{
    public const string PillPart = "PART_Pill";
    public const string ItemsPart = "PART_Items";
    public const string ScrollPart = "PART_Scroll";

    private readonly List<GalaxyTab> _tabs = new();
    private GlidingIndicator? _pill;
    private StackPanel? _items;
    private ScrollViewer? _scroll;
    private int _selected;
    private bool _animateNext;

    /// <summary>A tab was chosen (index).</summary>
    public event Action<int>? Chosen;

    public int SelectedIndex => _selected;

    public IReadOnlyList<GalaxyTab> Tabs => _tabs;

    protected override AutomationPeer OnCreateAutomationPeer() => new GalaxyTabStripAutomationPeer(this);

    public void SetTabs(IReadOnlyList<string> headers, int selected, bool animate)
    {
        var same = headers.Count == _tabs.Count;
        for (var i = 0; same && i < headers.Count; i++)
        {
            same = Equals(_tabs[i].Content, headers[i]);
        }
        if (!same)
        {
            foreach (var tab in _tabs)
            {
                tab.Click -= OnTabClick;
                tab.KeyDown -= OnTabKeyDown;
            }
            _items?.Children.Clear();
            _tabs.Clear();
            foreach (var header in headers)
            {
                var tab = new GalaxyTab { Content = header };
                AutomationProperties.SetName(tab, header);
                tab.Click += OnTabClick;
                tab.KeyDown += OnTabKeyDown;
                _tabs.Add(tab);
                _items?.Children.Add(tab);
            }
        }
        Select(selected, animate && same);
    }

    /// <summary>Marks tab <paramref name="index"/> (−1: none, the pill hides) and glides the pill to it.</summary>
    public void Select(int index, bool animate)
    {
        _selected = index;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var on = i == index;
            _tabs[i].IsChecked = on;
            KeyboardNavigation.SetIsTabStop(_tabs[i], on || index < 0 && i == 0);
        }
        _animateNext = animate;
        InvalidateArrange();
        PlacePill();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pill = e.NameScope.Find<GlidingIndicator>(PillPart);
        _items = e.NameScope.Find<StackPanel>(ItemsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        if (_items is not null)
        {
            _items.Children.Clear();
            foreach (var tab in _tabs)
            {
                _items.Children.Add(tab);
            }
            _items.LayoutUpdated -= OnItemsLaidOut;
            _items.LayoutUpdated += OnItemsLaidOut;
        }
    }

    private Rect _pillRect;

    private void OnItemsLaidOut(object? sender, EventArgs e) => PlacePill();

    private void PlacePill()
    {
        if (_pill is null || _items is null)
        {
            return;
        }
        if (_selected < 0 || _selected >= _tabs.Count)
        {
            _pillRect = default;
            _pill.Hide(_animateNext);
            _animateNext = false;
            return;
        }
        var tab = _tabs[_selected];
        if (tab.Bounds.Width <= 0)
        {
            return;
        }
        var rect = tab.Bounds;
        if (rect == _pillRect && _pill.HasTarget)
        {
            return;
        }
        _pillRect = rect;
        _pill.MoveTo(rect, _animateNext, ArcSpring.Snappy);
        _animateNext = false;
        tab.BringIntoView();
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is GalaxyTab tab)
        {
            Choose(_tabs.IndexOf(tab));
        }
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return;
        }
        Chosen?.Invoke(index);
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tabs.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var current = sender is GalaxyTab tab ? _tabs.IndexOf(tab) : _selected;
        var next = e.Key switch
        {
            Key.Right => (current + 1) % _tabs.Count,
            Key.Left => (current - 1 + _tabs.Count) % _tabs.Count,
            Key.Home => 0,
            Key.End => _tabs.Count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        Choose(next);
        _tabs[next].Focus(NavigationMethod.Directional);
    }
}

internal sealed class GalaxyTabStripAutomationPeer : ControlAutomationPeer
{
    public GalaxyTabStripAutomationPeer(Control owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
}

/// <summary>
/// A 4 px bar of the totals list: a capsule track and a fill <see cref="Fraction"/> of its width, made of one segment per
/// health state (2 px apart, widths by weight). Width and weights move on Arc's 580 ms CSS spring curve.
/// </summary>
/// <remarks>Code-driven motion: the segments' weights move together with the fill, which a transition on one property cannot express.</remarks>
internal sealed class GalaxyBar : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<GalaxyBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<GalaxyBar, double>(nameof(SegmentGap), 2);

    static GalaxyBar()
    {
        AffectsRender<GalaxyBar>(TrackBrushProperty, SegmentGapProperty);
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    private MotionValue<double>? _fraction;
    private MotionValue<double>? _blend;
    private double[] _from = Array.Empty<double>();
    private double[] _to = Array.Empty<double>();
    private IBrush?[] _brushes = Array.Empty<IBrush?>();
    private bool _hasData;

    /// <summary>The fill's target share of the track (tests).</summary>
    internal double Fraction => _fraction?.Target ?? 0;

    /// <summary>The segments' target weights (tests).</summary>
    internal IReadOnlyList<double> Weights => _to;

    private MotionValue<double> FractionValue()
    {
        if (_fraction is null)
        {
            _fraction = new MotionValue<double>(this, 0, MotionVectors.Double);
            _fraction.Changed += _ => InvalidateVisual();
            _blend = new MotionValue<double>(this, 1, MotionVectors.Double);
            _blend.Changed += _ => InvalidateVisual();
        }
        return _fraction;
    }

    /// <summary>Sets the fill share and the segments (weights and brushes, same length).</summary>
    public void Set(double fraction, ReadOnlySpan<double> weights, IBrush?[] brushes, bool animate)
    {
        var value = FractionValue();
        fraction = Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1);
        var current = CurrentWeights();
        _from = current.Length == weights.Length ? current : weights.ToArray();
        _to = weights.ToArray();
        _brushes = brushes;
        var full = animate && _hasData && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        _hasData = true;
        var spec = MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring);
        if (full)
        {
            _ = value.AnimateTo(fraction, spec);
            _blend!.Jump(0);
            _ = _blend.AnimateTo(1, spec);
        }
        else
        {
            value.Jump(fraction);
            _blend!.Jump(1);
        }
        InvalidateVisual();
    }

    private double[] CurrentWeights()
    {
        var t = _blend?.Value ?? 1;
        var result = new double[_to.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var from = i < _from.Length ? _from[i] : _to[i];
            result[i] = from + (_to[i] - from) * t;
        }
        return result;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(bounds, radius));
        }
        var fill = bounds.Width * Math.Clamp(_fraction?.Value ?? 0, 0, 1);
        if (fill <= 0.01 || _to.Length == 0)
        {
            return;
        }
        var t = _blend?.Value ?? 1;
        double total = 0;
        var active = 0;
        for (var i = 0; i < _to.Length; i++)
        {
            var w = Weight(i, t);
            total += w;
            if (w > 0)
            {
                active++;
            }
        }
        if (total <= 0)
        {
            return;
        }
        var gaps = Math.Max(0, active - 1) * SegmentGap;
        var room = Math.Max(0, fill - gaps);
        var x = 0.0;
        for (var i = 0; i < _to.Length; i++)
        {
            var w = Weight(i, t);
            if (w <= 0)
            {
                continue;
            }
            var width = room * w / total;
            if (i < _brushes.Length && _brushes[i] is { } brush && width > 0.05)
            {
                context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, 0, width, bounds.Height), Math.Min(radius, width / 2)));
            }
            x += width + SegmentGap;
        }
    }

    private double Weight(int i, double t)
    {
        var from = i < _from.Length ? _from[i] : _to[i];
        return Math.Max(0, from + (_to[i] - from) * t);
    }
}

/// <summary>
/// One row of the totals list: a button (<c>aria-pressed</c> while its cluster is focused) with the name and the rolling
/// value, a <see cref="GalaxyBar"/>, and a caption ("300 accounts · 10% at risk").
/// </summary>
[TemplatePart(ValuePart, typeof(ShowpieceNumber))]
[TemplatePart(BarPart, typeof(GalaxyBar))]
internal sealed class GalaxyGroupRow : GalaxyToggle
{
    public const string ValuePart = "PART_Value";
    public const string BarPart = "PART_Bar";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<GalaxyGroupRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<GalaxyGroupRow, string?>(nameof(Caption));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>The cluster key this row stands for.</summary>
    public string Key { get; set; } = string.Empty;

    private ShowpieceNumber? _value;
    private GalaxyBar? _bar;
    private double _number;
    private Func<double, string>? _formatter;
    private ShowpieceNumberFormat _format;
    private (double Fraction, double[] Weights, IBrush?[] Brushes)? _pendingBar;

    protected override Type StyleKeyOverride => typeof(GalaxyGroupRow);

    internal ShowpieceNumber? ValueNumber => _value;

    internal GalaxyBar? Bar => _bar;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _value = e.NameScope.Find<ShowpieceNumber>(ValuePart);
        _bar = e.NameScope.Find<GalaxyBar>(BarPart);
        ApplyValue();
        if (_pendingBar is { } bar)
        {
            _bar?.Set(bar.Fraction, bar.Weights, bar.Brushes, animate: false);
        }
    }

    /// <summary>The number on the right (formatted by <paramref name="formatter"/>, or as <paramref name="format"/>).</summary>
    public void SetNumber(double value, ShowpieceNumberFormat format, Func<double, string>? formatter)
    {
        _number = value;
        _format = format;
        _formatter = formatter;
        ApplyValue();
    }

    private void ApplyValue()
    {
        if (_value is null)
        {
            return;
        }
        _value.Formatter = _formatter;
        _value.Format = _format;
        _value.Value = _number;
    }

    public void SetBar(double fraction, double[] weights, IBrush?[] brushes, bool animate)
    {
        _pendingBar = (fraction, weights, brushes);
        _bar?.Set(fraction, weights, brushes, animate);
    }
}

/// <summary>
/// The rows of the totals list stacked in <see cref="Order"/>; when the order changes the rows that move slide from their
/// old place to the new one (FLIP, critical spring ω 11, about 0.55 s for two rows).
/// </summary>
/// <remarks>Code-driven motion: a FLIP needs the old and new arranged positions of each row, known only during arrange.</remarks>
internal sealed class GalaxyRowsPanel : Panel
{
    private static readonly SpringSpec s_reorder = ShowpieceSprings.Critical(11);

    private readonly Dictionary<Control, (TranslateTransform Transform, MotionValue<double> Offset)> _motion = new();
    private readonly Dictionary<Control, double> _lastTop = new();
    private int[] _order = Array.Empty<int>();
    private bool _flip;

    /// <summary>Child indices top to bottom.</summary>
    public IReadOnlyList<int> Order => _order;

    /// <summary>Puts the rows in <paramref name="order"/>; the moved ones slide when <paramref name="animate"/>.</summary>
    public void SetOrder(int[] order, bool animate)
    {
        _order = order;
        _flip = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        InvalidateArrange();
    }

    /// <summary>Forgets the old positions (new rows enter without sliding).</summary>
    public void Reset()
    {
        foreach (var (_, entry) in _motion)
        {
            entry.Offset.Dispose();
        }
        _motion.Clear();
        _lastTop.Clear();
        _order = Array.Empty<int>();
        _flip = false;
    }

    private IEnumerable<Control> Ordered()
    {
        if (_order.Length == Children.Count)
        {
            foreach (var index in _order)
            {
                yield return Children[index];
            }
            yield break;
        }
        foreach (var child in Children)
        {
            yield return child;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            height += child.DesiredSize.Height;
            width = Math.Max(width, child.DesiredSize.Width);
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var child in Ordered())
        {
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, finalSize.Width, height));
            if (_flip && _lastTop.TryGetValue(child, out var old) && Math.Abs(old - y) > 0.5)
            {
                var entry = Motion(child);
                // Start where the row is drawn now (its old top plus any running offset) and spring to its new place.
                var from = old + entry.Offset.Value - y;
                entry.Offset.Jump(from);
                _ = entry.Offset.AnimateTo(0, MotionSpec.Spring(s_reorder));
            }
            _lastTop[child] = y;
            y += height;
        }
        _flip = false;
        return finalSize;
    }

    private (TranslateTransform Transform, MotionValue<double> Offset) Motion(Control child)
    {
        if (_motion.TryGetValue(child, out var entry))
        {
            return entry;
        }
        var transform = new TranslateTransform();
        child.RenderTransform = transform;
        var offset = new MotionValue<double>(child, 0, MotionVectors.Double);
        offset.Changed += v => transform.Y = v;
        entry = (transform, offset);
        _motion[child] = entry;
        return entry;
    }
}

/// <summary>
/// The label above a cluster: its name, and its MRR (rolling) with the number of accounts. Enters 120 ms after a grouping
/// change, rising 4 px over 240 ms on the enter curve; dims to 30% while another cluster is focused or hovered.
/// </summary>
[TemplatePart(InnerPart, typeof(Control))]
[TemplatePart(MrrPart, typeof(ShowpieceNumber))]
[PseudoClasses(DimmedPseudoClass)]
internal sealed class GalaxyClusterLabel : TemplatedControl
{
    public const string InnerPart = "PART_Inner";
    public const string MrrPart = "PART_Mrr";
    public const string DimmedPseudoClass = ":dimmed";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GalaxyClusterLabel, string?>(nameof(Text));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<GalaxyClusterLabel, string?>(nameof(Detail));

    public static readonly StyledProperty<double> MrrProperty =
        AvaloniaProperty.Register<GalaxyClusterLabel, double>(nameof(Mrr));

    public static readonly StyledProperty<Func<double, string>?> MrrFormatterProperty =
        AvaloniaProperty.Register<GalaxyClusterLabel, Func<double, string>?>(nameof(MrrFormatter));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The text after the MRR (" · 300 accounts").</summary>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public double Mrr { get => GetValue(MrrProperty); set => SetValue(MrrProperty, value); }

    public Func<double, string>? MrrFormatter { get => GetValue(MrrFormatterProperty); set => SetValue(MrrFormatterProperty, value); }

    private readonly TranslateTransform _place = new();
    private readonly TranslateTransform _rise = new();
    private Control? _inner;
    private MotionValue<double>? _enter;
    private TimeSpan? _pendingDelay;
    private Point _anchor = new(double.NaN, double.NaN);

    public GalaxyClusterLabel()
    {
        RenderTransform = _place;
        IsHitTestVisible = false;
        Focusable = false;
    }

    protected override Type StyleKeyOverride => typeof(GalaxyClusterLabel);

    public bool IsDimmed
    {
        get => PseudoClasses.Contains(DimmedPseudoClass);
        set => PseudoClasses.Set(DimmedPseudoClass, value);
    }

    /// <summary>Entrance progress (tests).</summary>
    internal double EnterProgress => _enter?.Value ?? 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _inner = e.NameScope.Find<Control>(InnerPart);
        if (_inner is not null)
        {
            _inner.RenderTransform = _rise;
            ApplyEnter(_enter?.Value ?? 1);
        }
        if (_pendingDelay is { } delay)
        {
            _pendingDelay = null;
            Enter(delay);
        }
    }

    /// <summary>Plays the entrance after <paramref name="delay"/> (Arc's <c>labelIn</c>; jumps unless the motion is full).</summary>
    public void Enter(TimeSpan delay)
    {
        _enter ??= CreateEnter();
        if (!IsLoaded && _inner is null)
        {
            _pendingDelay = delay;
            _enter.Jump(0);
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _enter.Jump(1);
            return;
        }
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
    }

    private MotionValue<double> CreateEnter()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplyEnter;
        return value;
    }

    private void ApplyEnter(double progress)
    {
        if (_inner is null)
        {
            return;
        }
        _inner.Opacity = Math.Clamp(progress, 0, 1);
        _rise.Y = 4 * (1 - progress);
    }

    /// <summary>Puts the label's bottom center at <paramref name="anchor"/> (stage px).</summary>
    public void Place(Point anchor)
    {
        if (anchor == _anchor && DesiredSize == _placedSize)
        {
            return;
        }
        _anchor = anchor;
        Reposition();
    }

    /// <summary>Places the label again for its current size (after a measure).</summary>
    internal void Reposition()
    {
        var anchor = _anchor;
        if (double.IsNaN(anchor.X))
        {
            return;
        }
        _placedSize = DesiredSize;
        var x = Math.Round(anchor.X - DesiredSize.Width / 2);
        var y = Math.Round(anchor.Y - DesiredSize.Height);
        if (_place.X != x)
        {
            _place.X = x;
        }
        if (_place.Y != y)
        {
            _place.Y = y;
        }
    }

    private Size _placedSize;
}

/// <summary>Holds the cluster labels at the stage's origin; each label moves itself with a render transform.</summary>
internal sealed class GalaxyLabelLayer : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(child.DesiredSize));
            if (child is GalaxyClusterLabel label)
            {
                label.Reposition();
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The built-in account card: name, "<c>plan · region · industry</c>", a divider, MRR / owner / health rows (the health
/// with its color dot) and the account's note.
/// </summary>
[PseudoClasses(HasNotePseudoClass)]
internal sealed class GalaxyAccountCard : TemplatedControl
{
    public const string HasNotePseudoClass = ":has-note";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> MrrLabelProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(MrrLabel));

    public static readonly StyledProperty<string?> MrrValueProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(MrrValue));

    public static readonly StyledProperty<string?> OwnerLabelProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(OwnerLabel));

    public static readonly StyledProperty<string?> OwnerValueProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(OwnerValue));

    public static readonly StyledProperty<string?> HealthLabelProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(HealthLabel));

    public static readonly StyledProperty<string?> HealthValueProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(HealthValue));

    public static readonly StyledProperty<IBrush?> HealthBrushProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, IBrush?>(nameof(HealthBrush));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<GalaxyAccountCard, string?>(nameof(Note));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    public string? MrrLabel { get => GetValue(MrrLabelProperty); set => SetValue(MrrLabelProperty, value); }

    public string? MrrValue { get => GetValue(MrrValueProperty); set => SetValue(MrrValueProperty, value); }

    public string? OwnerLabel { get => GetValue(OwnerLabelProperty); set => SetValue(OwnerLabelProperty, value); }

    public string? OwnerValue { get => GetValue(OwnerValueProperty); set => SetValue(OwnerValueProperty, value); }

    public string? HealthLabel { get => GetValue(HealthLabelProperty); set => SetValue(HealthLabelProperty, value); }

    public string? HealthValue { get => GetValue(HealthValueProperty); set => SetValue(HealthValueProperty, value); }

    public IBrush? HealthBrush { get => GetValue(HealthBrushProperty); set => SetValue(HealthBrushProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    protected override Type StyleKeyOverride => typeof(GalaxyAccountCard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NoteProperty)
        {
            PseudoClasses.Set(HasNotePseudoClass, !string.IsNullOrEmpty(Note));
        }
    }
}

/// <summary>A legend entry: a color dot and its label.</summary>
internal sealed class GalaxyLegendItem : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GalaxyLegendItem, string?>(nameof(Text));

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<GalaxyLegendItem, IBrush?>(nameof(DotBrush));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(GalaxyLegendItem);
}

/// <summary>The legend's size entry: a small and a large muted dot, then its label ("Size shows MRR").</summary>
internal sealed class GalaxySizeLegend : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<GalaxySizeLegend, string?>(nameof(Text));

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<GalaxySizeLegend, IBrush?>(nameof(DotBrush));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(GalaxySizeLegend);
}

/// <summary>
/// The text alternative of the canvas (Arc's visually hidden data table): one named text per cluster of the current
/// grouping. Takes no space and no input.
/// </summary>
internal sealed class GalaxyHiddenTable : Panel
{
    public GalaxyHiddenTable()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
    }

    /// <summary>The rows (tests and screen readers).</summary>
    public IReadOnlyList<string> Rows => Children.OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();

    public void SetRows(string? caption, IReadOnlyList<string> rows)
    {
        AutomationProperties.SetName(this, caption);
        while (Children.Count > rows.Count)
        {
            Children.RemoveAt(Children.Count - 1);
        }
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= Children.Count)
            {
                Children.Add(new TextBlock { FontSize = 1 });
            }
            var text = (TextBlock)Children[i];
            text.Text = rows[i];
            AutomationProperties.SetName(text, rows[i]);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(1, 1));
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, 0, 0, 0));
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GalaxyHiddenTableAutomationPeer(this);
}

internal sealed class GalaxyHiddenTableAutomationPeer : ControlAutomationPeer
{
    public GalaxyHiddenTableAutomationPeer(Control owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsOffscreenCore() => false;
}

/// <summary>The "Show all" pill in the stage's corner (shown while a cluster is focused).</summary>
internal sealed class GalaxyChipButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<GalaxyChipButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(GalaxyChipButton);
}

/// <summary>Immutable brushes of one color at an opacity (legend dots, bar segments, card dots).</summary>
internal static class GalaxyBrushes
{
    public static IBrush Of(Color color, double opacity = 1) => new ImmutableSolidColorBrush(color, Math.Clamp(opacity, 0, 1));
}

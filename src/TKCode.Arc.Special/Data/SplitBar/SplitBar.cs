using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One horizontal bar split into parts in proportion to their values, with a legend of labels, values and shares under
/// it. When a value changes, a segment is added or one is removed, every part springs to its new share (the springs keep
/// their speed when interrupted, so live data never jumps). Pointing at a part, or moving through the parts with the
/// arrow keys, lifts it and dims the others; a click or Enter raises <see cref="SegmentInvoked"/>. A <see cref="Total"/>
/// larger than the sum leaves the rest of the track empty, shown in the legend as <see cref="RemainderLabel"/>.
/// </summary>
[TemplatePart(TrackPart, typeof(SplitBarTrack))]
[TemplatePart(LegendPart, typeof(Panel))]
[TemplatePart(SumPart, typeof(RollingDigits))]
[TemplatePart(OfTotalPart, typeof(TextBlock))]
[PseudoClasses(EmptyPseudoClass, HighlightedPseudoClass)]
public class SplitBar : TemplatedControl
{
    /// <summary>The drawn bar.</summary>
    public const string TrackPart = "PART_Track";

    /// <summary>The panel that holds the legend items.</summary>
    public const string LegendPart = "PART_Legend";

    /// <summary>The rolling sum in the header.</summary>
    public const string SumPart = "PART_Sum";

    /// <summary>The "of total" words after the sum.</summary>
    public const string OfTotalPart = "PART_OfTotal";

    /// <summary>Set while there is nothing to show (no segment has a value).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while one segment is highlighted.</summary>
    public const string HighlightedPseudoClass = ":highlighted";

    /// <summary>The parts from a view model; when set it replaces <see cref="Segments"/>.</summary>
    public static readonly StyledProperty<IEnumerable<SplitBarSegment>?> ItemsSourceProperty =
        AvaloniaProperty.Register<SplitBar, IEnumerable<SplitBarSegment>?>(nameof(ItemsSource));

    /// <summary>The heading above the bar, also the accessible name; null shows no heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(Title));

    /// <summary>
    /// The amount the full bar stands for. NaN (the default) makes the bar the sum of its parts; a larger total leaves the
    /// rest empty. A total smaller than the sum is ignored.
    /// </summary>
    public static readonly StyledProperty<double> TotalProperty =
        AvaloniaProperty.Register<SplitBar, double>(nameof(Total), double.NaN);

    /// <summary>The legend label of the empty rest when <see cref="Total"/> is larger than the sum; null leaves it out of the legend.</summary>
    public static readonly StyledProperty<string?> RemainderLabelProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(RemainderLabel), "Remaining");

    /// <summary>Digits after the decimal point in values (0 to 6).</summary>
    public static readonly StyledProperty<int> ValueDecimalsProperty =
        AvaloniaProperty.Register<SplitBar, int>(nameof(ValueDecimals), coerce: static (_, v) => Math.Clamp(v, 0, 6));

    /// <summary>Text before every value, such as "$".</summary>
    public static readonly StyledProperty<string?> ValuePrefixProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(ValuePrefix));

    /// <summary>Text after every value, such as " GB".</summary>
    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(ValueSuffix));

    /// <summary>Shows the legend under the bar (default true).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(IsLegendVisible), true);

    /// <summary>Shows the rolling sum (and "of total") beside the title (default true).</summary>
    public static readonly StyledProperty<bool> IsSumVisibleProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(IsSumVisible), true);

    /// <summary>Shows each part's share as a percentage in the legend (default true).</summary>
    public static readonly StyledProperty<bool> ShowsPercentProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(ShowsPercent), true);

    /// <summary>The part that is lifted while the others dim; -1 for none. Pointing and the arrow keys set it.</summary>
    public static readonly StyledProperty<int> HighlightedIndexProperty =
        AvaloniaProperty.Register<SplitBar, int>(nameof(HighlightedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised when a part is clicked, or chosen with Enter or Space on the focused bar.</summary>
    public static readonly RoutedEvent<SplitBarSegmentEventArgs> SegmentInvokedEvent =
        RoutedEvent.Register<SplitBar, SplitBarSegmentEventArgs>(nameof(SegmentInvoked), RoutingStrategies.Bubble);

    /// <summary>The spring the shares move on: a short settle with a touch of overshoot.</summary>
    internal static readonly SpringSpec ShareSpring = SpringSpec.FromVisual(0.45, 0.1);

    private readonly List<SplitBarSlot> _slots = new();
    private readonly List<SplitBarSegment> _watched = new();
    private readonly LiveLoop _loop;
    private INotifyCollectionChanged? _observedSource;
    private SplitBarTrack? _track;
    private Panel? _legend;
    private RollingDigits? _sum;
    private TextBlock? _ofTotal;
    private SplitBarLegendItem? _remainderItem;
    private double _wall;
    private double _scale = 1;
    private double _remainder;
    private bool _primed;
    private bool _attached;

    static SplitBar()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SplitBar>(AutomationControlType.Group);
    }

    /// <summary>Creates an empty split bar.</summary>
    public SplitBar()
    {
        _loop = new LiveLoop(this, OnFrame);
        Segments.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                Sync();
            }
        };
        UpdateName();
    }

    /// <summary>The parts, in order (the XAML content); ignored while <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<SplitBarSegment> Segments { get; } = new();

    /// <summary>The parts from a view model; when set it replaces <see cref="Segments"/>.</summary>
    public IEnumerable<SplitBarSegment>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The heading above the bar, also the accessible name; null shows no heading.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>
    /// The amount the full bar stands for. NaN (the default) makes the bar the sum of its parts; a larger total leaves the
    /// rest empty. A total smaller than the sum is ignored.
    /// </summary>
    public double Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>The legend label of the empty rest when <see cref="Total"/> is larger than the sum; null leaves it out of the legend.</summary>
    public string? RemainderLabel { get => GetValue(RemainderLabelProperty); set => SetValue(RemainderLabelProperty, value); }

    /// <summary>Digits after the decimal point in values (0 to 6).</summary>
    public int ValueDecimals { get => GetValue(ValueDecimalsProperty); set => SetValue(ValueDecimalsProperty, value); }

    /// <summary>Text before every value, such as "$".</summary>
    public string? ValuePrefix { get => GetValue(ValuePrefixProperty); set => SetValue(ValuePrefixProperty, value); }

    /// <summary>Text after every value, such as " GB".</summary>
    public string? ValueSuffix { get => GetValue(ValueSuffixProperty); set => SetValue(ValueSuffixProperty, value); }

    /// <summary>Shows the legend under the bar (default true).</summary>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <summary>Shows the rolling sum (and "of total") beside the title (default true).</summary>
    public bool IsSumVisible { get => GetValue(IsSumVisibleProperty); set => SetValue(IsSumVisibleProperty, value); }

    /// <summary>Shows each part's share as a percentage in the legend (default true).</summary>
    public bool ShowsPercent { get => GetValue(ShowsPercentProperty); set => SetValue(ShowsPercentProperty, value); }

    /// <summary>The part that is lifted while the others dim; -1 for none. Pointing and the arrow keys set it.</summary>
    public int HighlightedIndex { get => GetValue(HighlightedIndexProperty); set => SetValue(HighlightedIndexProperty, value); }

    /// <summary>Raised when a part is clicked, or chosen with Enter or Space on the focused bar.</summary>
    public event EventHandler<SplitBarSegmentEventArgs>? SegmentInvoked
    {
        add => AddHandler(SegmentInvokedEvent, value);
        remove => RemoveHandler(SegmentInvokedEvent, value);
    }

    /// <summary>The share each part heads to, in order (0 to 1 of the bar).</summary>
    public IReadOnlyList<double> Shares
    {
        get
        {
            var shares = new List<double>();
            foreach (var slot in _slots)
            {
                if (!slot.IsLeaving)
                {
                    shares.Add(slot.TargetShare);
                }
            }
            return shares;
        }
    }

    /// <summary>The slots being drawn, leaving ones included (track and tests).</summary>
    internal IReadOnlyList<SplitBarSlot> Slots => _slots;

    /// <summary>Seconds of frames the bar's clock has run.</summary>
    internal double Wall => _wall;

    /// <summary>The empty rest's share (0 to 1).</summary>
    internal double RemainderShare => _remainder;

    /// <summary>True while a share is still moving.</summary>
    internal bool IsMoving
    {
        get
        {
            foreach (var slot in _slots)
            {
                if (slot.Share.IsMoving(_wall) || slot.Dim.IsMoving(_wall))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The visible (not leaving) segments in order.</summary>
    internal IReadOnlyList<SplitBarSegment> Current
    {
        get
        {
            var list = new List<SplitBarSegment>();
            foreach (var slot in _slots)
            {
                if (!slot.IsLeaving)
                {
                    list.Add(slot.Segment);
                }
            }
            return list;
        }
    }

    /// <summary>Formats a value as the bar shows it.</summary>
    public string FormatValue(double value)
        => string.Concat(ValuePrefix, value.ToString("N" + ValueDecimals.ToString(CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("en-US")), ValueSuffix);

    /// <summary>The sentence a screen reader hears for one part ("Photos, 42 GB, 42 percent").</summary>
    internal string Describe(int index)
    {
        var current = Current;
        if (index < 0 || index >= current.Count)
        {
            return string.Empty;
        }
        var segment = current[index];
        var share = Math.Round(ShareOf(segment) * 100);
        return string.Create(CultureInfo.InvariantCulture,
            $"{segment.Label ?? string.Empty}, {FormatValue(Math.Max(0, segment.Value))}, {share:0} percent");
    }

    /// <summary>The target share of a segment (0 to 1).</summary>
    internal double ShareOf(SplitBarSegment segment) => _scale > 0 ? Math.Max(0, segment.Value) / _scale : 0;

    /// <summary>Raises <see cref="SegmentInvoked"/> for the visible part at <paramref name="index"/>.</summary>
    internal void Invoke(int index)
    {
        var current = Current;
        if (index < 0 || index >= current.Count)
        {
            return;
        }
        RaiseEvent(new SplitBarSegmentEventArgs(SegmentInvokedEvent, this, current[index], index, ShareOf(current[index])));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track = e.NameScope.Find<SplitBarTrack>(TrackPart);
        _legend = e.NameScope.Find<Panel>(LegendPart);
        _sum = e.NameScope.Find<RollingDigits>(SumPart);
        _ofTotal = e.NameScope.Find<TextBlock>(OfTotalPart);
        _track?.Attach(this);
        _remainderItem = null;
        _legend?.Children.Clear();
        foreach (var slot in _slots)
        {
            slot.LegendItem = null;
        }
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveSource(ItemsSource);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        // A view model's list and segments can outlive the bar: listen only while it is shown.
        ObserveSource(null);
        Watch(Array.Empty<SplitBarSegment>());
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource(_attached ? ItemsSource : null);
            Sync();
        }
        else if (change.Property == TotalProperty || change.Property == RemainderLabelProperty)
        {
            Sync();
        }
        else if (change.Property == ValueDecimalsProperty || change.Property == ValuePrefixProperty ||
                 change.Property == ValueSuffixProperty || change.Property == ShowsPercentProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateName();
        }
        else if (change.Property == HighlightedIndexProperty)
        {
            UpdateHighlight();
        }
    }

    private void ObserveSource(IEnumerable<SplitBarSegment>? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void Watch(IReadOnlyList<SplitBarSegment> segments)
    {
        foreach (var segment in _watched)
        {
            segment.PropertyChanged -= OnSegmentChanged;
        }
        _watched.Clear();
        foreach (var segment in segments)
        {
            if (!_watched.Contains(segment))
            {
                segment.PropertyChanged += OnSegmentChanged;
                _watched.Add(segment);
            }
        }
    }

    private void OnSegmentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SplitBarSegment.ValueProperty)
        {
            Retarget();
        }
        else if (e.Property == SplitBarSegment.LabelProperty || e.Property == SplitBarSegment.BrushProperty)
        {
            UpdateTexts();
            _track?.InvalidateVisual();
        }
    }

    /// <summary>Matches the slots to the segments: new ones grow in from nothing, removed ones shrink out.</summary>
    private void Sync()
    {
        var segments = (ItemsSource ?? Segments).Where(s => s is not null).Distinct().ToList();
        if (_attached)
        {
            Watch(segments);
        }
        var animate = CanAnimate;
        var kept = new List<SplitBarSlot>();
        var index = 0;
        foreach (var segment in segments)
        {
            var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.Segment, segment) && !s.IsLeaving);
            if (slot is null)
            {
                // A new part grows in from nothing (the first data shows at once).
                slot = new SplitBarSlot(segment);
                slot.Share.Jump(0);
            }
            slot.ColorIndex = index++;
            kept.Add(slot);
        }
        // Leaving parts stay right after the part they followed, so the bar closes the gap where it opened.
        var result = new List<SplitBarSlot>(kept);
        var insertAt = 0;
        foreach (var slot in _slots)
        {
            if (kept.Contains(slot))
            {
                insertAt = result.IndexOf(slot) + 1;
                continue;
            }
            if (!slot.IsLeaving)
            {
                slot.IsLeaving = true;
                if (slot.LegendItem is { } item)
                {
                    _legend?.Children.Remove(item);
                    slot.LegendItem = null;
                }
            }
            if (animate)
            {
                result.Insert(insertAt++, slot);
            }
        }
        _slots.Clear();
        _slots.AddRange(result);
        Retarget();
        RebuildLegend();
    }

    private bool CanAnimate => _primed && _attached && ArcMotion.IsFull(this);

    /// <summary>Points every share at its value's part of the scale.</summary>
    private void Retarget()
    {
        double sum = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving)
            {
                sum += Math.Max(0, slot.Segment.Value);
            }
        }
        var total = Total;
        _scale = double.IsFinite(total) && total > sum ? total : sum;
        _remainder = _scale > 0 ? Math.Max(0, 1 - sum / _scale) : 0;
        var animate = CanAnimate;
        foreach (var slot in _slots)
        {
            var target = slot.IsLeaving ? 0 : ShareOf(slot.Segment);
            slot.TargetShare = target;
            if (animate)
            {
                slot.Share.Spring(target, _wall, ShareSpring);
            }
            else
            {
                slot.Share.Jump(target);
            }
        }
        if (!animate)
        {
            _slots.RemoveAll(s => s.IsLeaving);
        }
        _primed = _primed || _attached;
        PseudoClasses.Set(EmptyPseudoClass, sum <= 0);
        if (HighlightedIndex >= Current.Count)
        {
            SetCurrentValue(HighlightedIndexProperty, -1);
        }
        UpdateTexts();
        UpdateHighlight();
        Kick();
    }

    private void Kick()
    {
        _track?.InvalidateVisual();
        if (IsMoving)
        {
            _loop.Start();
        }
    }

    private bool OnFrame(double dt)
    {
        _wall += dt;
        if (!ArcMotion.IsFull(this))
        {
            foreach (var slot in _slots)
            {
                slot.Share.Jump(slot.TargetShare);
                slot.Dim.Jump(slot.Dim.Target);
            }
        }
        var moving = IsMoving;
        if (!moving && _slots.RemoveAll(s => s.IsLeaving) > 0)
        {
            UpdateName();
        }
        _track?.InvalidateVisual();
        foreach (var slot in _slots)
        {
            slot.LegendItem?.SetDim(slot.Dim.Value(_wall));
        }
        return moving;
    }

    private void RebuildLegend()
    {
        if (_legend is null)
        {
            return;
        }
        var order = 0;
        foreach (var slot in _slots)
        {
            if (slot.IsLeaving)
            {
                continue;
            }
            if (slot.LegendItem is null)
            {
                slot.LegendItem = new SplitBarLegendItem(this, slot);
            }
            var at = _legend.Children.IndexOf(slot.LegendItem);
            if (at != order)
            {
                if (at >= 0)
                {
                    _legend.Children.RemoveAt(at);
                }
                _legend.Children.Insert(Math.Min(order, _legend.Children.Count), slot.LegendItem);
            }
            order++;
        }
        var showRemainder = _remainder > 0.0005 && RemainderLabel is not null;
        if (showRemainder)
        {
            _remainderItem ??= new SplitBarLegendItem(this, null);
            if (!_legend.Children.Contains(_remainderItem))
            {
                _legend.Children.Add(_remainderItem);
            }
        }
        else if (_remainderItem is not null)
        {
            _legend.Children.Remove(_remainderItem);
        }
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        double sum = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving)
            {
                sum += Math.Max(0, slot.Segment.Value);
            }
        }
        if (_sum is not null)
        {
            _sum.Decimals = ValueDecimals;
            _sum.Prefix = ValuePrefix;
            _sum.Suffix = ValueSuffix;
            _sum.Value = sum;
            _sum.IsInstant = !_primed;
        }
        if (_ofTotal is not null)
        {
            var total = Total;
            var hasTotal = double.IsFinite(total) && total > 0;
            _ofTotal.IsVisible = hasTotal;
            _ofTotal.Text = hasTotal ? "of " + FormatValue(total) : null;
        }
        var colorIndex = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving)
            {
                slot.LegendItem?.Update(colorIndex);
                colorIndex++;
            }
        }
        if (_remainderItem is not null)
        {
            _remainderItem.UpdateRemainder(RemainderLabel ?? string.Empty, _remainder * _scale, _remainder);
        }
        if (_track is not null)
        {
            AutomationProperties.SetName(_track, TrackName());
        }
        UpdateName();
    }

    private string TrackName()
    {
        var current = Current;
        if (HighlightedIndex >= 0 && HighlightedIndex < current.Count)
        {
            return Describe(HighlightedIndex);
        }
        var parts = new List<string>();
        for (var i = 0; i < current.Count; i++)
        {
            parts.Add(Describe(i));
        }
        if (_remainder > 0.0005 && RemainderLabel is not null)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{RemainderLabel}, {FormatValue(_remainder * _scale)}, {Math.Round(_remainder * 100):0} percent"));
        }
        return string.Join("; ", parts);
    }

    private void UpdateName() => AutomationProperties.SetName(this, Title ?? "Split bar");

    private void UpdateHighlight()
    {
        var index = HighlightedIndex;
        var animate = CanAnimate;
        var visible = 0;
        foreach (var slot in _slots)
        {
            var dim = slot.IsLeaving ? 0 : index >= 0 && visible != index ? 1 : 0;
            if (!slot.IsLeaving)
            {
                visible++;
            }
            if (Math.Abs(slot.Dim.Target - dim) > 0.001)
            {
                if (animate)
                {
                    slot.Dim.Tween(dim, _wall, ArcMotionPresets.Fast.TotalSeconds, ArcEasings.Standard);
                }
                else
                {
                    slot.Dim.Jump(dim);
                }
            }
            if (!animate)
            {
                slot.LegendItem?.SetDim(dim);
            }
        }
        PseudoClasses.Set(HighlightedPseudoClass, index >= 0);
        if (_track is not null)
        {
            AutomationProperties.SetName(_track, TrackName());
        }
        Kick();
        if (animate)
        {
            _loop.Start();
        }
    }
}

/// <summary>One part of the bar as drawn: its segment, its springing share and its dimming.</summary>
internal sealed class SplitBarSlot
{
    public SplitBarSlot(SplitBarSegment segment)
    {
        Segment = segment;
    }

    public SplitBarSegment Segment { get; }

    public ExplainerValue Share;

    public ExplainerValue Dim;

    public double TargetShare { get; set; }

    public bool IsLeaving { get; set; }

    public int ColorIndex { get; set; }

    public SplitBarLegendItem? LegendItem { get; set; }
}

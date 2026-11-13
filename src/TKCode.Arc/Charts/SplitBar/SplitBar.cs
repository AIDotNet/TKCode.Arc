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
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One 100% bar split into shares, for traffic sources, budgets, storage or votes. Resting on a part (or moving to it
/// with the arrow keys) widens it a little, enough to show its own label when it is small, while the others give way
/// and dim, and the readout above names it with its share and value. Labels sit inside a part when they fit and move
/// to a lane under the bar with leader lines when they do not. The legend switches parts off and on: a part that is
/// off collapses in place and the rest reflow on springs, their percentages rolling to the new shares. A
/// <see cref="Total"/> larger than the sum leaves the rest of the track empty, named <see cref="RemainderLabel"/>.
/// </summary>
[TemplatePart(TrackPart, typeof(SplitBarTrack))]
[TemplatePart(LegendPart, typeof(Panel))]
[TemplatePart(SumPart, typeof(RollingDigits))]
[TemplatePart(OfTotalPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(EmptyPseudoClass, HighlightedPseudoClass)]
public partial class SplitBar : TemplatedControl
{
    /// <summary>The drawn bar.</summary>
    public const string TrackPart = "PART_Track";

    /// <summary>The panel that holds the legend items.</summary>
    public const string LegendPart = "PART_Legend";

    /// <summary>The rolling total in the readout.</summary>
    public const string SumPart = "PART_Sum";

    /// <summary>The "of total" words after the total when <see cref="Total"/> is set.</summary>
    public const string OfTotalPart = "PART_OfTotal";

    /// <summary>The polite live region that reads out the highlighted part and parts switched off or on.</summary>
    public const string StatusPart = "PART_Status";

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

    /// <summary>One line under the title; null shows none.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(Description));

    /// <summary>The word after values in the readout and in what screen readers hear, such as "sessions"; null for none.</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(Unit));

    /// <summary>The label of the readout while no part is highlighted (default "Total").</summary>
    public static readonly StyledProperty<string?> TotalLabelProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(TotalLabel), "Total");

    /// <summary>
    /// The amount the full bar stands for. NaN (the default) makes the bar the sum of its parts; a larger total leaves the
    /// rest empty. A total smaller than the sum is ignored.
    /// </summary>
    public static readonly StyledProperty<double> TotalProperty =
        AvaloniaProperty.Register<SplitBar, double>(nameof(Total), double.NaN);

    /// <summary>The legend label of the empty rest when <see cref="Total"/> is larger than the sum; null leaves it out of the legend.</summary>
    public static readonly StyledProperty<string?> RemainderLabelProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(RemainderLabel), "Remaining");

    /// <summary>Digits after the decimal point in values (0 to 6). Shares always show one.</summary>
    public static readonly StyledProperty<int> ValueDecimalsProperty =
        AvaloniaProperty.Register<SplitBar, int>(nameof(ValueDecimals), coerce: static (_, v) => Math.Clamp(v, 0, 6));

    /// <summary>Text before every value, such as "$".</summary>
    public static readonly StyledProperty<string?> ValuePrefixProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(ValuePrefix));

    /// <summary>Text after every value, such as " GB".</summary>
    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<SplitBar, string?>(nameof(ValueSuffix));

    /// <summary>Shows the legend under the bar (default true). Each entry switches its part and highlights it on hover.</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(IsLegendVisible), true);

    /// <summary>Shows the readout (the rolling total, or the highlighted part) beside the title (default true).</summary>
    public static readonly StyledProperty<bool> IsSumVisibleProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(IsSumVisible), true);

    /// <summary>Shows each part's share as a percentage in the legend (default true); false shows its value instead.</summary>
    public static readonly StyledProperty<bool> ShowsPercentProperty =
        AvaloniaProperty.Register<SplitBar, bool>(nameof(ShowsPercent), true);

    /// <summary>The part that is widened while the others dim; -1 for none. Pointing and the arrow keys set it.</summary>
    public static readonly StyledProperty<int> HighlightedIndexProperty =
        AvaloniaProperty.Register<SplitBar, int>(nameof(HighlightedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Raised when a part is clicked, or chosen with Enter or Space on the focused bar. When no handler marks it handled,
    /// Enter and Space then switch the part off, as the legend does.
    /// </summary>
    public static readonly RoutedEvent<SplitBarSegmentEventArgs> SegmentInvokedEvent =
        RoutedEvent.Register<SplitBar, SplitBarSegmentEventArgs>(nameof(SegmentInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a part is switched off or back on, by the legend, the keyboard or code (<see cref="SplitBarSegment.IsHidden"/>).</summary>
    public static readonly RoutedEvent<SplitBarHiddenChangedEventArgs> HiddenChangedEvent =
        RoutedEvent.Register<SplitBar, SplitBarHiddenChangedEventArgs>(nameof(HiddenChanged), RoutingStrategies.Bubble);

    private readonly List<SplitBarSlot> _slots = new();
    private readonly List<SplitBarSegment> _watched = new();
    private readonly LiveLoop _loop;
    private INotifyCollectionChanged? _observedSource;
    private SplitBarTrack? _track;
    private Panel? _legend;
    private TextBlock? _status;
    private SplitBarLegendItem? _remainderItem;
    private double _wall;
    private double _scale = 1;
    private double _remainder;
    private double _sum;
    private bool _primed;
    private bool _attached;
    private bool _settling;

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

    /// <summary>One line under the title; null shows none.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The word after values in the readout and in what screen readers hear, such as "sessions"; null for none.</summary>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>The label of the readout while no part is highlighted (default "Total").</summary>
    public string? TotalLabel { get => GetValue(TotalLabelProperty); set => SetValue(TotalLabelProperty, value); }

    /// <summary>
    /// The amount the full bar stands for. NaN (the default) makes the bar the sum of its parts; a larger total leaves the
    /// rest empty. A total smaller than the sum is ignored.
    /// </summary>
    public double Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>The legend label of the empty rest when <see cref="Total"/> is larger than the sum; null leaves it out of the legend.</summary>
    public string? RemainderLabel { get => GetValue(RemainderLabelProperty); set => SetValue(RemainderLabelProperty, value); }

    /// <summary>Digits after the decimal point in values (0 to 6). Shares always show one.</summary>
    public int ValueDecimals { get => GetValue(ValueDecimalsProperty); set => SetValue(ValueDecimalsProperty, value); }

    /// <summary>Text before every value, such as "$".</summary>
    public string? ValuePrefix { get => GetValue(ValuePrefixProperty); set => SetValue(ValuePrefixProperty, value); }

    /// <summary>Text after every value, such as " GB".</summary>
    public string? ValueSuffix { get => GetValue(ValueSuffixProperty); set => SetValue(ValueSuffixProperty, value); }

    /// <summary>Shows the legend under the bar (default true). Each entry switches its part and highlights it on hover.</summary>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <summary>Shows the readout (the rolling total, or the highlighted part) beside the title (default true).</summary>
    public bool IsSumVisible { get => GetValue(IsSumVisibleProperty); set => SetValue(IsSumVisibleProperty, value); }

    /// <summary>Shows each part's share as a percentage in the legend (default true); false shows its value instead.</summary>
    public bool ShowsPercent { get => GetValue(ShowsPercentProperty); set => SetValue(ShowsPercentProperty, value); }

    /// <summary>The part that is widened while the others dim; -1 for none. Pointing and the arrow keys set it.</summary>
    public int HighlightedIndex { get => GetValue(HighlightedIndexProperty); set => SetValue(HighlightedIndexProperty, value); }

    /// <summary>
    /// Raised when a part is clicked, or chosen with Enter or Space on the focused bar. When no handler marks it handled,
    /// Enter and Space then switch the part off, as the legend does.
    /// </summary>
    public event EventHandler<SplitBarSegmentEventArgs>? SegmentInvoked
    {
        add => AddHandler(SegmentInvokedEvent, value);
        remove => RemoveHandler(SegmentInvokedEvent, value);
    }

    /// <summary>Raised when a part is switched off or back on, by the legend, the keyboard or code (<see cref="SplitBarSegment.IsHidden"/>).</summary>
    public event EventHandler<SplitBarHiddenChangedEventArgs>? HiddenChanged
    {
        add => AddHandler(HiddenChangedEvent, value);
        remove => RemoveHandler(HiddenChangedEvent, value);
    }

    /// <summary>The share each part heads to, in order (0 to 1 of the bar; 0 for a part that is switched off).</summary>
    public IReadOnlyList<double> Shares
    {
        get
        {
            var shares = new List<double>();
            foreach (var slot in _slots)
            {
                if (!slot.IsLeaving)
                {
                    shares.Add(slot.BaseShare);
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

    /// <summary>The sum of the parts that are on.</summary>
    internal double Sum => _sum;

    internal SplitBarTrack? Track => _track;

    /// <summary>What the live region said last (tests).</summary>
    internal string? LastAnnouncement => _status?.Text;

    /// <summary>The visible (not leaving) segments in order, parts that are off included.</summary>
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
        => string.Concat(ValuePrefix, value.ToString("N" + ValueDecimals.ToString(CultureInfo.InvariantCulture), Culture), ValueSuffix);

    internal static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A share as the bar writes it: one decimal and a percent sign ("41.6%").</summary>
    internal static string FormatShare(double share) => (share * 100).ToString("0.0", Culture) + "%";

    /// <summary>A value with the unit after it ("24,630 sessions").</summary>
    internal string FormatWithUnit(double value)
        => string.IsNullOrEmpty(Unit) ? FormatValue(value) : FormatValue(value) + " " + Unit;

    /// <summary>The sentence a screen reader hears for one part ("Direct, 24.8%, 24,630 sessions.").</summary>
    internal string Describe(int index)
    {
        var slot = SlotAt(index);
        if (slot is null)
        {
            return string.Empty;
        }
        if (slot.Segment.IsHidden)
        {
            return (slot.Segment.Label ?? string.Empty) + ", hidden.";
        }
        return $"{slot.Segment.Label}, {FormatShare(slot.BaseShare)}, {FormatWithUnit(Math.Max(0, slot.Segment.Value))}.";
    }

    /// <summary>The target share of a segment (0 to 1), 0 while it is switched off.</summary>
    internal double ShareOf(SplitBarSegment segment)
        => _scale > 0 && !segment.IsHidden ? Math.Max(0, segment.Value) / _scale : 0;

    /// <summary>The slot of the visible (not leaving) part at <paramref name="index"/>.</summary>
    internal SplitBarSlot? SlotAt(int index)
    {
        if (index < 0)
        {
            return null;
        }
        var visible = 0;
        foreach (var slot in _slots)
        {
            if (slot.IsLeaving)
            {
                continue;
            }
            if (visible == index)
            {
                return slot;
            }
            visible++;
        }
        return null;
    }

    /// <summary>The index of <paramref name="slot"/> among the visible (not leaving) parts; -1 when it is leaving.</summary>
    internal int IndexOf(SplitBarSlot slot)
    {
        var visible = 0;
        foreach (var s in _slots)
        {
            if (s.IsLeaving)
            {
                continue;
            }
            if (ReferenceEquals(s, slot))
            {
                return visible;
            }
            visible++;
        }
        return -1;
    }

    /// <summary>
    /// Raises <see cref="SegmentInvoked"/> for the visible part at <paramref name="index"/>; returns true when a handler
    /// marked it handled.
    /// </summary>
    internal bool Invoke(int index)
    {
        var slot = SlotAt(index);
        if (slot is null)
        {
            return false;
        }
        var args = new SplitBarSegmentEventArgs(SegmentInvokedEvent, this, slot.Segment, index, slot.BaseShare);
        RaiseEvent(args);
        return args.Handled;
    }

    /// <summary>
    /// Switches the part at <paramref name="index"/> off or back on. The last part that is on stays on: its legend entry
    /// shakes and the live region says why. Returns true when the part changed.
    /// </summary>
    internal bool Toggle(int index)
    {
        var slot = SlotAt(index);
        if (slot is null)
        {
            return false;
        }
        var segment = slot.Segment;
        if (!segment.IsHidden && OnCount() <= 1)
        {
            Refuse(slot);
            return false;
        }
        segment.SetCurrentValue(SplitBarSegment.IsHiddenProperty, !segment.IsHidden);
        return true;
    }

    private int OnCount()
    {
        var count = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving && !slot.Segment.IsHidden)
            {
                count++;
            }
        }
        return count;
    }

    private void Refuse(SplitBarSlot slot)
    {
        Announce($"{slot.Segment.Label} is the only share shown, so it stays on.");
        if (CanAnimate)
        {
            slot.ShakeStart = _wall;
            slot.IsShaking = true;
            _loop.Start();
        }
    }

    internal void Announce(string message)
    {
        if (_status is not null)
        {
            // Setting the same text again would not be read out a second time.
            _status.Text = _status.Text == message ? message + " " : message;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track?.Detach();
        _track = e.NameScope.Find<SplitBarTrack>(TrackPart);
        _legend = e.NameScope.Find<Panel>(LegendPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        ApplyReadoutTemplate(e.NameScope);
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
                 change.Property == ValueSuffixProperty || change.Property == ShowsPercentProperty ||
                 change.Property == UnitProperty || change.Property == TotalLabelProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateName();
            UpdateTrackName();
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
        else if (e.Property == SplitBarSegment.IsHiddenProperty && sender is SplitBarSegment segment)
        {
            var hidden = segment.IsHidden;
            var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.Segment, segment) && !s.IsLeaving);
            if (hidden && slot is not null && HighlightedIndex == IndexOf(slot))
            {
                SetCurrentValue(HighlightedIndexProperty, -1);
            }
            Retarget();
            Announce($"{segment.Label} {(hidden ? "hidden" : "shown")}.");
            RaiseEvent(new SplitBarHiddenChangedEventArgs(HiddenChangedEvent, this, segment, hidden));
        }
        else if (e.Property == SplitBarSegment.LabelProperty || e.Property == SplitBarSegment.BrushProperty)
        {
            if (sender is SplitBarSegment changed)
            {
                foreach (var slot in _slots)
                {
                    if (ReferenceEquals(slot.Segment, changed))
                    {
                        slot.ResetMeasures();
                    }
                }
            }
            Retarget();
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
            else
            {
                _track?.Release(slot);
            }
        }
        _slots.Clear();
        _slots.AddRange(result);
        Retarget();
        RebuildLegend();
    }

    private bool CanAnimate => !_settling && _primed && _attached && ArcMotion.IsFull(this);

    private void Kick()
    {
        _track?.Refresh();
        if (IsMoving)
        {
            _loop.Start();
        }
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
        var animate = CanAnimate;
        var colorIndex = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving)
            {
                slot.LegendItem?.Update(colorIndex, animate);
                colorIndex++;
            }
        }
        if (_remainderItem is not null)
        {
            _remainderItem.UpdateRemainder(RemainderLabel ?? string.Empty, _remainder * _scale, _remainder, animate);
        }
        UpdateReadout(animate);
        _track?.UpdateLabels(animate);
        UpdateTrackName();
        UpdateName();
    }

    internal void OnPaletteChanged() => UpdateTexts();

    private void UpdateTrackName()
    {
        if (_track is not null)
        {
            AutomationProperties.SetName(_track, TrackName());
        }
    }

    /// <summary>"Title: A 41.6%, B 24.8%. Use the arrow keys to read each share. C hidden."</summary>
    private string TrackName()
    {
        var parts = new List<string>();
        var hidden = new List<string>();
        foreach (var slot in _slots)
        {
            if (slot.IsLeaving)
            {
                continue;
            }
            if (slot.Segment.IsHidden)
            {
                hidden.Add($"{slot.Segment.Label} hidden.");
            }
            else
            {
                parts.Add($"{slot.Segment.Label} {FormatShare(slot.BaseShare)}");
            }
        }
        if (_remainder > 0.0005 && RemainderLabel is not null)
        {
            parts.Add($"{RemainderLabel} {FormatShare(_remainder)}");
        }
        var text = string.Join(", ", parts) + ". Use the arrow keys to read each share.";
        if (!string.IsNullOrEmpty(Title))
        {
            text = Title + ": " + text;
        }
        if (hidden.Count > 0)
        {
            text += " " + string.Join(" ", hidden);
        }
        return text;
    }

    private void UpdateName() => AutomationProperties.SetName(this, Title ?? "Split bar");
}

/// <summary>One part of the bar as drawn: its segment and everything that moves about it.</summary>
internal sealed class SplitBarSlot
{
    public SplitBarSlot(SplitBarSegment segment)
    {
        Segment = segment;
    }

    public SplitBarSegment Segment { get; }

    /// <summary>How wide the part is (fraction of the bar), on the edge spring; it starts where the parts before it end.</summary>
    public SplitBarValue Share;

    /// <summary>Dimming while another part is highlighted (0 to 1).</summary>
    public SplitBarValue Dim;

    /// <summary>From the resting fill (0) to the highlighted fill (1).</summary>
    public SplitBarValue Fill;

    /// <summary>How open the name of the inside label is (0 to 1), and how visible the inside label is.</summary>
    public SplitBarValue NameOpen;

    public SplitBarValue Inside;

    /// <summary>How visible the lane label is (0 to 1) and where its left edge is (px).</summary>
    public SplitBarValue Outside;

    public SplitBarValue OutsideX;

    /// <summary>The share of the parts that are on (the number the labels show).</summary>
    public double BaseShare { get; set; }

    /// <summary>The share it heads to, widened while it is highlighted.</summary>
    public double TargetShare { get; set; }

    public SplitBarLabelMode Mode { get; set; }

    /// <summary>True while its label sits in the lane (the label may still be fading).</summary>
    public bool IsOutside { get; set; }

    public bool IsLeaving { get; set; }

    public int ColorIndex { get; set; }

    public bool IsShaking { get; set; }

    public double ShakeStart { get; set; }

    public SplitBarLegendItem? LegendItem { get; set; }

    /// <summary>The inside and lane labels, made by the track when first needed.</summary>
    public SplitBarInsideLabel? InsideLabel { get; set; }

    public SplitBarLaneLabel? LaneLabel { get; set; }

    /// <summary>Measured widths of the label texts (NaN until measured).</summary>
    public double NameWidth { get; set; } = double.NaN;

    public double LaneNameWidth { get; set; } = double.NaN;

    public string? MeasuredLabel { get; set; }

    /// <summary>The fills the track drew last, kept while the color, the surface and the mixes stay the same.</summary>
    public IBrush? FillCache { get; set; }

    public IBrush? HighlightCache { get; set; }

    public IBrush? FillColor { get; set; }

    public IBrush? FillSurface { get; set; }

    public double FillMix { get; set; }

    public double HighlightMix { get; set; }

    private StreamGeometry? _leader;
    private double _leaderX0 = double.NaN;
    private double _leaderX1;
    private double _leaderTop;
    private double _leaderDepth;

    /// <summary>
    /// The S curve from the middle of the part down to its lane label, rebuilt only when an end moved: one cubic from
    /// (x0, top) to (x1, top + depth) with both handles at half the depth.
    /// </summary>
    public StreamGeometry Leader(double x0, double x1, double top, double depth)
    {
        if (_leader is null || Math.Abs(x0 - _leaderX0) > 0.01 || Math.Abs(x1 - _leaderX1) > 0.01 ||
            Math.Abs(top - _leaderTop) > 0.01 || Math.Abs(depth - _leaderDepth) > 0.01)
        {
            _leaderX0 = x0;
            _leaderX1 = x1;
            _leaderTop = top;
            _leaderDepth = depth;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                var mid = top + depth / 2;
                g.BeginFigure(new Point(x0, top), false);
                g.CubicBezierTo(new Point(x0, mid), new Point(x1, mid), new Point(x1, top + depth));
                g.EndFigure(false);
            }
            _leader = geometry;
        }
        return _leader;
    }

    public void ResetMeasures()
    {
        NameWidth = double.NaN;
        LaneNameWidth = double.NaN;
        MeasuredLabel = null;
    }
}

/// <summary>How a part shows its label.</summary>
internal enum SplitBarLabelMode
{
    /// <summary>No label (the part is off or leaving).</summary>
    None,

    /// <summary>Name and share inside the part.</summary>
    Full,

    /// <summary>Only the share inside the part.</summary>
    Share,

    /// <summary>In the lane under the bar, with a leader line.</summary>
    Lane
}

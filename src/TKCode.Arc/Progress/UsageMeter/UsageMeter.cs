using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Where a <see cref="UsageMeter"/> stands against its limit.</summary>
public enum UsageMeterStatus
{
    /// <summary>Under the warning threshold.</summary>
    Ok,

    /// <summary>At or past <see cref="UsageMeter.WarnThreshold"/> of the limit.</summary>
    Near,

    /// <summary>Past the limit.</summary>
    Over
}

/// <summary>
/// Shows what fills a fixed allowance and how close it is to the limit (Arc UsageMeter): a title with a status badge,
/// the rolling total, a bar of accent steps (one per category) and a legend that doubles as the keyboard path. The
/// first time 40% of the meter is visible the categories grow in left to right; later changes re-flow every category
/// on its own smooth spring (P18, P23) and the numbers roll the way they moved (P14). Past the limit the bar is
/// struck through with a hatch beside a limit marker, so the overage reads without color. Hovering or focusing a
/// category previews it (the others dim); a press pins it until pressed again or Escape.
/// Categories come from <see cref="ItemsSource"/> or the inline <see cref="Segments"/>.
/// </summary>
[TemplatePart(BarHostPart, typeof(Decorator))]
[TemplatePart(LegendPart, typeof(UsageMeterLegendPanel))]
[PseudoClasses(NearPseudoClass, OverPseudoClass)]
public partial class UsageMeter : TemplatedControl
{
    public const string BarHostPart = "PART_BarHost";
    public const string LegendPart = "PART_Legend";
    public const string BadgeIconPart = "PART_BadgeIcon";
    public const string NearPseudoClass = ":near";
    public const string OverPseudoClass = ":over";

    internal const string FreeKey = "\u0001free";

    /// <summary>What is measured, such as "Workspace storage".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<UsageMeter, string?>(nameof(Label));

    /// <summary>Bound categories (<see cref="UsageSegment"/>); takes precedence over <see cref="Segments"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<UsageMeter, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The plan limit, in the same unit as the categories.</summary>
    public static readonly StyledProperty<double> LimitProperty =
        AvaloniaProperty.Register<UsageMeter, double>(nameof(Limit), 100);

    /// <summary>Unit shown after amounts, such as "GB" (Arc <c>unit</c>).</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<UsageMeter, string>(nameof(Unit), string.Empty);

    /// <summary>Digits after the decimal point (Arc: 1).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<UsageMeter, int>(nameof(Decimals), 1, coerce: (_, v) => Math.Clamp(v, 0, 6));

    /// <summary>Legend label of the remaining allowance (Arc <c>freeLabel</c>, "Free").</summary>
    public static readonly StyledProperty<string> FreeLabelProperty =
        AvaloniaProperty.Register<UsageMeter, string>(nameof(FreeLabel), "Free");

    /// <summary>Legend label of the overage once past the limit (Arc <c>overLabel</c>, "Over limit").</summary>
    public static readonly StyledProperty<string> OverLabelProperty =
        AvaloniaProperty.Register<UsageMeter, string>(nameof(OverLabel), "Over limit");

    /// <summary>Share of the limit at which the meter warns that it is almost full (Arc <c>warnAt</c>, 0.9).</summary>
    public static readonly StyledProperty<double> WarnThresholdProperty =
        AvaloniaProperty.Register<UsageMeter, double>(nameof(WarnThreshold), 0.9);

    /// <summary>Where the total stands against the limit.</summary>
    public static readonly DirectProperty<UsageMeter, UsageMeterStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<UsageMeter, UsageMeterStatus>(nameof(Status), o => o.Status);

    /// <summary>Sum of all categories.</summary>
    public static readonly DirectProperty<UsageMeter, double> TotalProperty =
        AvaloniaProperty.RegisterDirect<UsageMeter, double>(nameof(Total), o => o.Total);

    internal static readonly DirectProperty<UsageMeter, double> HeadlineProperty =
        AvaloniaProperty.RegisterDirect<UsageMeter, double>(nameof(Headline), o => o.Headline);

    internal static readonly DirectProperty<UsageMeter, string?> CaptionProperty =
        AvaloniaProperty.RegisterDirect<UsageMeter, string?>(nameof(Caption), o => o.Caption);

    internal static readonly DirectProperty<UsageMeter, string?> BadgeTextProperty =
        AvaloniaProperty.RegisterDirect<UsageMeter, string?>(nameof(BadgeText), o => o.BadgeText);

    internal static readonly StyledProperty<IBrush?> AccentBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(SurfaceBrush));
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(TrackBrush));
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(DangerBrush));
    internal static readonly StyledProperty<IBrush?> WarningBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(WarningBrush));
    internal static readonly StyledProperty<IBrush?> BorderStrongBrushProperty = 
        AvaloniaProperty.Register<UsageMeter, IBrush?>(nameof(BorderStrongBrush));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public double Limit { get => GetValue(LimitProperty); set => SetValue(LimitProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string FreeLabel { get => GetValue(FreeLabelProperty); set => SetValue(FreeLabelProperty, value); }
    public string OverLabel { get => GetValue(OverLabelProperty); set => SetValue(OverLabelProperty, value); }
    public double WarnThreshold { get => GetValue(WarnThresholdProperty); set => SetValue(WarnThresholdProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    internal IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    internal IBrush? BorderStrongBrush { get => GetValue(BorderStrongBrushProperty); set => SetValue(BorderStrongBrushProperty, value); }

    /// <summary>Inline categories (<c>&lt;arc:UsageSegment Id="…" Label="…" Value="…"/&gt;</c>); ignored while <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<UsageSegment> Segments { get; } = new();

    private UsageMeterStatus _status;

    public UsageMeterStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    private double _total;

    /// <summary>Sum of all categories (rounded to <see cref="Decimals"/>).</summary>
    public double Total
    {
        get => _total;
        private set => SetAndRaise(TotalProperty, ref _total, value);
    }

    private double _headline;

    internal double Headline
    {
        get => _headline;
        private set => SetAndRaise(HeadlineProperty, ref _headline, value);
    }

    private string? _caption;

    internal string? Caption
    {
        get => _caption;
        private set => SetAndRaise(CaptionProperty, ref _caption, value);
    }

    private string? _badgeText;

    internal string? BadgeText
    {
        get => _badgeText;
        private set => SetAndRaise(BadgeTextProperty, ref _badgeText, value);
    }

    private UsageMeterBar? _bar;
    private UsageMeterLegendPanel? _legend;
    private ArcContentSwap? _badgeIcon;
    private readonly List<UsageMeterLegendItem> _items = new();
    private readonly List<UsageSegment> _subscribed = new();
    private INotifyCollectionChanged? _observed;
    private ViewportVisibility? _visibility;
    private bool _ready;
    private bool _revealed;
    private string? _hovered;
    private string? _focused;
    private string? _pinned;
    private int _roving;
    private UsageMeterStatus? _announcedStatus;
    private double _announcedOver = double.NaN;

    internal UsageMeterPalette? Palette { get; private set; }

    internal IReadOnlyList<UsageSegment> CurrentSegments { get; private set; } = Array.Empty<UsageSegment>();

    internal IReadOnlyList<UsageMeterLegendItem> LegendItems => _items;

    internal UsageMeterBar? Bar => _bar;

    /// <summary>The category being previewed or pinned (hover, then focus, then pin).</summary>
    internal string? ActiveKey => _hovered ?? _focused ?? _pinned;

    internal string? PinnedKey => _pinned;

    static UsageMeter()
    {
        FocusableProperty.OverrideDefaultValue<UsageMeter>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<UsageMeter>(AutomationControlType.Group);
        AutomationProperties.LiveSettingProperty.OverrideDefaultValue<UsageMeter>(AutomationLiveSetting.Polite);
    }

    public UsageMeter()
    {
        Segments.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                Resubscribe();
                Refresh();
            }
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var host = e.NameScope.Find<Decorator>(BarHostPart);
        _bar = new UsageMeterBar(this);
        AutomationProperties.SetControlTypeOverride(_bar, AutomationControlType.Image);
        if (host is not null)
        {
            host.Child = _bar;
        }
        _legend = e.NameScope.Find<UsageMeterLegendPanel>(LegendPart);
        _badgeIcon = e.NameScope.Find<ArcContentSwap>(BadgeIconPart);
        _items.Clear();
        _legend?.Children.Clear();
        BuildPalette();
        Resubscribe();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSource();
        Resubscribe();
        Refresh();
        if (_ready)
        {
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _ready = true;
            Refresh();
            return;
        }
        // The categories grow in the first time 40% of the meter is visible (P18).
        _visibility = new ViewportVisibility(this, 0.4, once: true);
        _visibility.VisibilityChanged += (_, visible) =>
        {
            if (visible && !_ready)
            {
                // Started after the first layout settles, so the growth never loses its first frames (Arc's soon()).
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!_ready)
                    {
                        _ready = true;
                        Refresh();
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
            _observed = null;
        }
        foreach (var segment in _subscribed)
        {
            segment.PropertyChanged -= OnSegmentChanged;
        }
        _subscribed.Clear();
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    // User data is observed only while the meter is in the tree, so a long-lived source never keeps it alive.
    private void ObserveSource()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
        }
        _observed = IsAttached ? ItemsSource as INotifyCollectionChanged : null;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourceChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource();
            Resubscribe();
            Refresh();
        }
        else if (change.Property == LimitProperty || change.Property == UnitProperty || change.Property == DecimalsProperty ||
                 change.Property == FreeLabelProperty || change.Property == OverLabelProperty ||
                 change.Property == WarnThresholdProperty || change.Property == LabelProperty)
        {
            Refresh();
        }
        else if (change.Property == AccentBrushProperty || change.Property == SurfaceBrushProperty ||
                 change.Property == TrackBrushProperty || change.Property == DangerBrushProperty ||
                 change.Property == WarningBrushProperty || change.Property == BorderStrongBrushProperty ||
                 change.Property == ForegroundProperty)
        {
            BuildPalette();
            Refresh();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Resubscribe();
        Refresh();
    }

    private void Resubscribe()
    {
        foreach (var segment in _subscribed)
        {
            segment.PropertyChanged -= OnSegmentChanged;
        }
        _subscribed.Clear();
        var list = new List<UsageSegment>();
        IEnumerable source = ItemsSource ?? Segments;
        foreach (var item in source)
        {
            if (item is UsageSegment segment)
            {
                list.Add(segment);
                if (IsAttached)
                {
                    segment.PropertyChanged += OnSegmentChanged;
                    _subscribed.Add(segment);
                }
            }
        }
        CurrentSegments = list;
    }

    private void OnSegmentChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    /// <summary>Accent steps, strongest first, so the meter reads as one allowance: 100%, 62%, 36%, 22% accent into surface.</summary>
    internal static Color[] SegmentColors(Color accent, Color surface) =>
    [
        accent, Oklch.MixOklab(surface, accent, 0.62), Oklch.MixOklab(surface, accent, 0.36), Oklch.MixOklab(surface, accent, 0.22)
    ];

    private void BuildPalette()
    {
        var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.Black;
        var surface = (SurfaceBrush as ISolidColorBrush)?.Color ?? Colors.White;
        Palette = new UsageMeterPalette
        {
            Segments = SegmentColors(accent, surface).Select(c => (IBrush)new ImmutableSolidColorBrush(c)).ToArray(),
            Track = TrackBrush ?? Brushes.LightGray,
            Surface = SurfaceBrush ?? Brushes.White,
            Danger = DangerBrush ?? Brushes.Red,
            Foreground = Foreground ?? Brushes.Black
        };
        // Swatches and the badge icon are rebuilt with the new colors.
        foreach (var item in _items)
        {
            item.Tag = null;
        }
        _iconStatus = null;
        _bar?.InvalidateVisual();
    }

    /// <summary>Formats an amount like Arc: fixed decimals (en-US) plus the unit.</summary>
    internal string Format(double value) =>
        value.ToString("F" + Decimals, CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);

    [GeneratedRegex(@"\.0+(?= |$)")]
    private static partial Regex TrailingZeros();

    internal string LimitText => TrailingZeros().Replace(Format(Limit), string.Empty);

    private double Round(double value)
    {
        var factor = Math.Pow(10, Decimals);
        return Math.Round(value * factor, MidpointRounding.AwayFromZero) / factor;
    }

    /// <summary>Status for a total against a limit and warning share.</summary>
    internal static UsageMeterStatus StatusFor(double total, double limit, double warnAt) =>
        total > limit ? UsageMeterStatus.Over : total >= limit * warnAt ? UsageMeterStatus.Near : UsageMeterStatus.Ok;

    private void Refresh()
    {
        var segments = CurrentSegments;
        var total = Round(segments.Sum(s => s.Value));
        var free = Round(Math.Max(0, Limit - total));
        var over = Round(Math.Max(0, total - Limit));
        var status = StatusFor(total, Limit, WarnThreshold);
        Total = total;
        Status = status;
        PseudoClasses.Set(NearPseudoClass, status == UsageMeterStatus.Near);
        PseudoClasses.Set(OverPseudoClass, status == UsageMeterStatus.Over);
        var level = ArcMotion.GetEffectiveLevel(this);

        _bar?.SetData(segments, Limit, reveal: _ready && !_revealed, _ready, level);
        if (_ready && _bar is not null)
        {
            _revealed = true;
        }

        SyncLegend(segments, status, free, over);

        var active = ActiveKey;
        var activeItem = _items.FirstOrDefault(i => i.Key == active);
        Headline = _ready ? activeItem?.Value ?? total : 0;
        var unit = Unit;
        if (activeItem is null)
        {
            Caption = $"{unit} of {LimitText} used".TrimStart();
        }
        else if (activeItem.Key == FreeKey)
        {
            Caption = $"{unit} {(status == UsageMeterStatus.Over ? "over the limit" : "free")}".TrimStart();
        }
        else
        {
            var share = Limit > 0 ? (int)Math.Round(activeItem.Value / Limit * 100, MidpointRounding.AwayFromZero) : 0;
            Caption = $"{unit} in {activeItem.Label} · {share}% of plan".TrimStart();
        }
        BadgeText = status switch
        {
            UsageMeterStatus.Over => $"{Format(over)} over",
            UsageMeterStatus.Near => "Almost full",
            _ => $"{Format(free)} free"
        };
        UpdateBadgeIcon(status);
        _bar?.SetHighlight(active, level);
        foreach (var item in _items)
        {
            item.IsDimmed = active is not null && active != item.Key;
            item.IsChecked = _pinned == item.Key;
        }

        var parts = string.Join(", ", segments.Select(s => $"{s.Label} {Format(s.Value)}"));
        var tail = status == UsageMeterStatus.Over
            ? $"Over the limit by {Format(over)}."
            : $"{Format(free)} free{(status == UsageMeterStatus.Near ? ", almost full" : string.Empty)}.";
        if (_bar is not null)
        {
            AutomationProperties.SetName(_bar, $"{Label}: {Format(total)} of {LimitText} used. {parts}. {tail}");
        }
        AutomationProperties.SetName(this, Label ?? string.Empty);
        // Status changes are announced, and so is a growing overage; routine changes inside a status stay quiet.
        if (_announcedStatus != status || (status == UsageMeterStatus.Over && !over.Equals(_announcedOver)))
        {
            var first = _announcedStatus is null;
            _announcedStatus = status;
            _announcedOver = over;
            if (!first)
            {
                AutomationProperties.SetItemStatus(this, status switch
                {
                    UsageMeterStatus.Over => $"Over the plan limit by {Format(over)}.",
                    UsageMeterStatus.Near => $"Almost full. {Format(free)} free.",
                    _ => $"Under the limit. {Format(free)} free."
                });
            }
        }
    }

    private UsageMeterStatus? _iconStatus;

    private void UpdateBadgeIcon(UsageMeterStatus status)
    {
        if (_badgeIcon is null || _iconStatus == status)
        {
            return;
        }
        _iconStatus = status;
        _badgeIcon.Content = status switch
        {
            UsageMeterStatus.Over => Glyph(ArcIcons.TriangleAlert, DangerBrush, 14),
            UsageMeterStatus.Near => Glyph(ArcIcons.CircleAlert, WarningBrush, 14),
            _ => null
        };
        _badgeIcon.IsVisible = status != UsageMeterStatus.Ok;
    }

    private static ArcGlyph Glyph(Geometry data, IBrush? brush, double size) =>
        new() { Data = data, Size = size, StrokeThickness = 2, Foreground = brush };

    private void SyncLegend(IReadOnlyList<UsageSegment> segments, UsageMeterStatus status, double free, double over)
    {
        if (_legend is null)
        {
            return;
        }
        var wanted = segments.Select(s => s.Key).Append(FreeKey).ToList();
        var byKey = _items.ToDictionary(i => i.Key);
        _items.Clear();
        for (var i = 0; i < wanted.Count; i++)
        {
            if (!byKey.Remove(wanted[i], out var item))
            {
                item = new UsageMeterLegendItem(this, wanted[i]);
            }
            _items.Add(item);
        }
        if (!_legend.Children.SequenceEqual(_items))
        {
            _legend.Children.Clear();
            foreach (var item in _items)
            {
                _legend.Children.Add(item);
            }
        }
        _roving = Math.Clamp(_roving, 0, _items.Count - 1);
        var palette = Palette;
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var isFree = item.Key == FreeKey;
            var segment = isFree ? null : segments[i];
            var label = isFree ? status == UsageMeterStatus.Over ? OverLabel : FreeLabel : segment!.Label ?? string.Empty;
            var value = isFree ? status == UsageMeterStatus.Over ? over : free : Math.Max(0, segment!.Value);
            item.Label = label;
            item.Value = _ready ? value : 0;
            item.Decimals = Decimals;
            item.Unit = Unit;
            item.IsTabStop = i == _roving;
            AutomationProperties.SetName(item, $"{label}, {Format(value)}");
            var warning = isFree && status == UsageMeterStatus.Over;
            var keyKind = warning ? "alert" : isFree ? "free" : $"swatch{Math.Min(i, 3)}";
            if (!Equals(item.Tag, keyKind) && palette is not null)
            {
                item.Tag = keyKind;
                item.KeyContent = warning
                    ? Glyph(ArcIcons.TriangleAlert, DangerBrush, 12)
                    : new Border
                    {
                        Width = 10,
                        Height = 10,
                        CornerRadius = new CornerRadius(3),
                        Background = isFree ? palette.Track : palette.Segments[Math.Min(i, palette.Segments.Length - 1)],
                        BorderBrush = isFree ? BorderStrongBrush : null,
                        BorderThickness = new Thickness(isFree ? 1 : 0)
                    };
            }
        }
    }

    internal void SetHovered(string? key)
    {
        if (_hovered == key)
        {
            return;
        }
        _hovered = key;
        Refresh();
    }

    internal void SetFocused(UsageMeterLegendItem? item)
    {
        var key = item?.Key;
        if (item is not null)
        {
            _roving = _items.IndexOf(item);
        }
        if (_focused == key)
        {
            return;
        }
        _focused = key;
        Refresh();
    }

    internal void TogglePin(string? key)
    {
        _pinned = key is null || _pinned == key ? null : key;
        var index = _items.FindIndex(i => i.Key == key);
        if (index >= 0)
        {
            _roving = index;
        }
        Refresh();
    }

    /// <summary>Clears a pinned category (Escape).</summary>
    public void ClearPin()
    {
        if (_pinned is null)
        {
            return;
        }
        _pinned = null;
        Refresh();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<UsageMeterLegendItem>().FirstOrDefault() is not { } item)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_pinned is not null || _focused is not null)
            {
                e.Handled = true;
                ClearPin();
            }
            return;
        }
        var at = _items.IndexOf(item);
        int? next = e.Key switch
        {
            Key.Right or Key.Down => at + 1,
            Key.Left or Key.Up => at - 1,
            Key.Home => 0,
            Key.End => _items.Count - 1,
            _ => null
        };
        if (next is not { } target || _items.Count == 0)
        {
            return;
        }
        e.Handled = true;
        _roving = (target + _items.Count) % _items.Count;
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].IsTabStop = i == _roving;
        }
        _items[_roving].Focus(NavigationMethod.Directional);
    }
}

using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Choosing seats on a map of a venue or a cabin: sections of seats colored by their price tier, taken seats hollow, a
/// legend with the prices, and a summary of the chosen seats with fees, a total and Continue. Seats are chosen with the
/// pointer or with the arrow keys and Enter; at most <see cref="MaxSelection"/> at once. Every seat is drawn by one control,
/// so large venues stay light.
/// <code>
/// &lt;arc:SeatMap Title="Harbor Hall · Fri, Nov 20" MaxSelection="6" ServiceFee="3.5" /&gt;
/// </code>
/// </summary>
[TemplatePart(CanvasPart, typeof(SeatMapCanvas))]
[TemplatePart(SummaryPart, typeof(StackPanel))]
[TemplatePart(LegendPart, typeof(WrapPanel))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(CompactPseudoClass, FullPseudoClass)]
public class SeatMap : TemplatedControl
{
    public const string CanvasPart = "PART_Canvas";
    public const string SummaryPart = "PART_Summary";
    public const string LegendPart = "PART_Legend";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The block is narrower than its compact threshold: the summary sits under the map.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>As many seats are chosen as <see cref="MaxSelection"/> allows.</summary>
    public const string FullPseudoClass = ":full";

    /// <summary>The heading over the map (the venue and the date).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SeatMap, string?>(nameof(Title));

    /// <summary>Most seats chosen at once (6).</summary>
    public static readonly StyledProperty<int> MaxSelectionProperty =
        AvaloniaProperty.Register<SeatMap, int>(nameof(MaxSelection), 6, coerce: (_, v) => Math.Max(1, v));

    /// <summary>The fee added per chosen seat (0 hides the fees row).</summary>
    public static readonly StyledProperty<decimal> ServiceFeeProperty =
        AvaloniaProperty.Register<SeatMap, decimal>(nameof(ServiceFee), coerce: (_, v) => Math.Max(0, v));

    /// <summary>The ids of seats that are already sold ("ORC-C-12").</summary>
    public static readonly StyledProperty<IEnumerable<string>?> TakenSeatsProperty =
        AvaloniaProperty.Register<SeatMap, IEnumerable<string>?>(nameof(TakenSeats));

    /// <summary>The symbol written before prices ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<SeatMap, string>(nameof(CurrencySymbol), "$");

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<SeatMapLabels> LabelsProperty =
        AvaloniaProperty.Register<SeatMap, SeatMapLabels>(nameof(Labels), new SeatMapLabels());

    /// <summary>The chosen seats, in the order they were chosen.</summary>
    public static readonly DirectProperty<SeatMap, IReadOnlyList<SeatMapSeat>> SelectedSeatsProperty =
        AvaloniaProperty.RegisterDirect<SeatMap, IReadOnlyList<SeatMapSeat>>(nameof(SelectedSeats), o => o.SelectedSeats);

    /// <summary>The price of the chosen seats plus their fees.</summary>
    public static readonly DirectProperty<SeatMap, decimal> TotalProperty =
        AvaloniaProperty.RegisterDirect<SeatMap, decimal>(nameof(Total), o => o.Total);

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<SeatMap, double>(nameof(CompactThreshold), 780);

    /// <summary>Raised when a seat is chosen or released.</summary>
    public static readonly RoutedEvent<SeatMapSelectionEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<SeatMap, SeatMapSelectionEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when Continue is pressed with seats chosen.</summary>
    public static readonly RoutedEvent<SeatMapSelectionEventArgs> ContinueRequestedEvent =
        RoutedEvent.Register<SeatMap, SeatMapSelectionEventArgs>(nameof(ContinueRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<SeatMapSelectionEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ContinueRequestedEvent"/>
    public event EventHandler<SeatMapSelectionEventArgs>? ContinueRequested
    {
        add => AddHandler(ContinueRequestedEvent, value);
        remove => RemoveHandler(ContinueRequestedEvent, value);
    }

    private readonly List<SeatMapSeat> _chosen = new();
    private List<SeatMapSeat> _seats = new();
    private Dictionary<string, int> _index = new();
    private HashSet<string> _taken = new();
    private IReadOnlyList<SeatMapSeat> _selected = [];
    private decimal _total;
    private INameScope? _scope;
    private SeatMapCanvas? _canvas;
    private StackPanel? _summary;
    private WrapPanel? _legend;
    private FinanceAnnouncer? _announcer;
    private bool _isCompact;

    static SeatMap()
    {
        FocusableProperty.OverrideDefaultValue<SeatMap>(false);
    }

    public SeatMap()
    {
        Sections.CollectionChanged += OnLayoutChanged;
        Tiers.CollectionChanged += OnLayoutChanged;
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="MaxSelectionProperty"/>
    public int MaxSelection { get => GetValue(MaxSelectionProperty); set => SetValue(MaxSelectionProperty, value); }

    /// <inheritdoc cref="ServiceFeeProperty"/>
    public decimal ServiceFee { get => GetValue(ServiceFeeProperty); set => SetValue(ServiceFeeProperty, value); }

    /// <inheritdoc cref="TakenSeatsProperty"/>
    public IEnumerable<string>? TakenSeats { get => GetValue(TakenSeatsProperty); set => SetValue(TakenSeatsProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SeatMapLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectedSeatsProperty"/>
    public IReadOnlyList<SeatMapSeat> SelectedSeats { get => _selected; private set => SetAndRaise(SelectedSeatsProperty, ref _selected, value); }

    /// <inheritdoc cref="TotalProperty"/>
    public decimal Total { get => _total; private set => SetAndRaise(TotalProperty, ref _total, value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The sections of seats.</summary>
    public AvaloniaList<SeatMapSection> Sections { get; } = new();

    /// <summary>The price tiers, in legend order.</summary>
    public AvaloniaList<SeatMapTier> Tiers { get; } = new();

    /// <summary>Every seat as laid out, section by section.</summary>
    public IReadOnlyList<SeatMapSeat> Seats => _seats;

    /// <summary>True while the block is narrower than its compact threshold.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>The drawn map (tests).</summary>
    internal SeatMapCanvas? Canvas => _canvas;

    /// <summary>Whether the seat <paramref name="id"/> is sold.</summary>
    public bool IsTaken(string id) => _taken.Contains(id);

    /// <summary>
    /// Chooses the seat <paramref name="id"/>, or releases it when it is chosen. Returns false for an unknown or taken seat, or
    /// when <see cref="MaxSelection"/> seats are already chosen.
    /// </summary>
    public bool ToggleSeat(string id)
    {
        if (!_index.TryGetValue(id, out var index) || _taken.Contains(id))
        {
            return false;
        }
        var seat = _seats[index];
        var labels = Labels;
        var existing = _chosen.FindIndex(s => s.Id == id);
        bool chosen;
        if (existing >= 0)
        {
            _chosen.RemoveAt(existing);
            chosen = false;
        }
        else
        {
            if (_chosen.Count >= MaxSelection)
            {
                _announcer?.Announce(labels.Limit(MaxSelection));
                return false;
            }
            _chosen.Add(seat);
            chosen = true;
        }
        Changed();
        _announcer?.Announce(labels.AnnounceToggle(SeatText(seat), chosen, _chosen.Count));
        return true;
    }

    /// <summary>Releases every chosen seat.</summary>
    public void ClearSelection()
    {
        if (_chosen.Count == 0)
        {
            return;
        }
        _chosen.Clear();
        Changed();
    }

    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        return amount.ToString(amount == Math.Round(amount) ? "C0" : "C2", format);
    }

    private string SeatText(SeatMapSeat seat) => Labels.Seat(seat.Section.Name, seat.Row, seat.Number);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_canvas is not null)
        {
            _canvas.SeatInvoked -= OnSeatInvoked;
            _canvas.CursorMoved -= OnCursorMoved;
        }
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _canvas = _scope.Find<SeatMapCanvas>(CanvasPart);
        _summary = _scope.Find<StackPanel>(SummaryPart);
        _legend = _scope.Find<WrapPanel>(LegendPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_canvas is not null)
        {
            _canvas.SeatInvoked += OnSeatInvoked;
            _canvas.CursorMoved += OnCursorMoved;
        }
        Relayout();
        UpdateCompact(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == TakenSeatsProperty)
        {
            _taken = TakenSeats?.ToHashSet() ?? new HashSet<string>();
            _chosen.RemoveAll(s => _taken.Contains(s.Id));
            Changed(raise: false);
        }
        else if (p == LabelsProperty || p == CurrencySymbolProperty)
        {
            Relayout();
        }
        else if (p == MaxSelectionProperty || p == ServiceFeeProperty)
        {
            while (_chosen.Count > MaxSelection)
            {
                _chosen.RemoveAt(_chosen.Count - 1);
            }
            Changed(raise: false);
        }
        else if (p == TitleProperty)
        {
            UpdateTitle();
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateCompact();
        }
    }

    private void OnLayoutChanged(object? sender, NotifyCollectionChangedEventArgs e) => Relayout();

    private void OnSeatInvoked(int index)
    {
        if (index >= 0 && index < _seats.Count)
        {
            ToggleSeat(_seats[index].Id);
        }
    }

    private void OnCursorMoved(int index)
    {
        if (_canvas is null || index < 0 || index >= _seats.Count)
        {
            return;
        }
        var seat = _seats[index];
        var labels = Labels;
        var state = _taken.Contains(seat.Id) ? labels.TakenState : _chosen.Contains(seat) ? labels.ChosenState : labels.Available;
        AutomationProperties.SetName(_canvas, labels.SeatName(SeatText(seat), Money(seat.Price), state));
    }

    private void Relayout()
    {
        _seats = SeatMapLayout.Build(Sections, Tiers);
        _index = new Dictionary<string, int>();
        for (var i = 0; i < _seats.Count; i++)
        {
            _index.TryAdd(_seats[i].Id, i);
        }
        _taken = TakenSeats?.ToHashSet() ?? new HashSet<string>();
        _chosen.RemoveAll(s => !_index.ContainsKey(s.Id) || _taken.Contains(s.Id));
        var labels = Labels;
        if (_canvas is not null)
        {
            _canvas.SetSeats(_seats, seat => labels.Tier(SeatText(seat), Money(seat.Price)), labels.Stage);
            AutomationProperties.SetName(_canvas, labels.MapName);
            AutomationProperties.SetHelpText(_canvas, labels.MapHelp);
        }
        UpdateTitle();
        BuildLegend();
        Changed(raise: false);
    }

    private void UpdateTitle()
    {
        if (_scope?.Find<TextBlock>("PART_Title") is { } title)
        {
            title.Text = Title;
            title.IsVisible = !string.IsNullOrEmpty(Title);
        }
    }

    private void BuildLegend()
    {
        if (_legend is null)
        {
            return;
        }
        var labels = Labels;
        _legend.Children.Clear();
        foreach (var tier in Tiers)
        {
            _legend.Children.Add(Entry("s" + Math.Clamp(tier.ColorIndex, 1, 4), labels.Tier(tier.Name, Money(tier.Price))));
        }
        _legend.Children.Add(Entry("taken", labels.Taken));
        _legend.Children.Add(Entry("chosen", labels.Chosen));

        static Control Entry(string dotClass, string text)
        {
            var dot = new SeatMapLegendDot();
            dot.Classes.Add(dotClass);
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { dot, label } };
        }
    }

    private void Changed(bool raise = true)
    {
        SelectedSeats = _chosen.ToList();
        var subtotal = _chosen.Sum(s => s.Price);
        Total = subtotal + ServiceFee * _chosen.Count;
        PseudoClasses.Set(FullPseudoClass, _chosen.Count >= MaxSelection);
        var chosenSet = _chosen.Select(s => s.Id).ToHashSet();
        _canvas?.SetStates(i => _taken.Contains(_seats[i].Id), i => chosenSet.Contains(_seats[i].Id));
        BuildSummary(subtotal);
        if (raise)
        {
            RaiseEvent(new SeatMapSelectionEventArgs(SelectionChangedEvent, SelectedSeats, Total));
        }
    }

    private void BuildSummary(decimal subtotal)
    {
        if (_summary is null)
        {
            return;
        }
        var labels = Labels;
        _summary.Children.Clear();
        var heading = new TextBlock { Text = labels.SummaryHeading };
        ArcTokens.Bind(heading, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        _summary.Children.Add(heading);
        if (_chosen.Count == 0)
        {
            _summary.Children.Add(Muted(new TextBlock { Text = labels.NothingChosen, TextWrapping = TextWrapping.Wrap }));
        }
        var list = new StackPanel { Spacing = 6 };
        foreach (var seat in _chosen)
        {
            var text = SeatText(seat);
            var remove = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = new ArcGlyph { Data = ArcIcons.X, Size = 14 },
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(remove, labels.Remove(text));
            var id = seat.Id;
            remove.Click += (_, _) =>
            {
                var at = _chosen.FindIndex(s => s.Id == id);
                ToggleSeat(id);
                FocusRemoveAfter(at);
            };
            var dot = new SeatMapLegendDot();
            dot.Classes.Add("s" + Math.Clamp(seat.Tier?.ColorIndex ?? 1, 1, 4));
            var price = new TextBlock { Text = Money(seat.Price), VerticalAlignment = VerticalAlignment.Center, FontFeatures = FontFeatureCollection.Parse("+tnum") };
            var name = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var row = new ArcShrinkRow { Spacing = 8, Children = { dot, name, price, remove } };
            ArcShrinkRow.SetIsShrinkable(name, true);
            list.Children.Add(row);
        }
        _summary.Children.Add(list);
        if (_chosen.Count >= MaxSelection)
        {
            _summary.Children.Add(Muted(new TextBlock { Text = labels.Limit(MaxSelection), TextWrapping = TextWrapping.Wrap }));
        }
        var totals = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
        totals.Children.Add(Pair(labels.Subtotal(_chosen.Count), Money(subtotal), false));
        if (ServiceFee > 0)
        {
            totals.Children.Add(Pair(labels.Fees, Money(ServiceFee * _chosen.Count), false));
        }
        totals.Children.Add(Pair(labels.Total, Money(Total), true));
        var rule = new Border { Padding = new Thickness(0, 10, 0, 0), Child = totals };
        ArcTokens.Bind(rule, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(rule, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        _summary.Children.Add(rule);
        var go = new Button
        {
            Variant = ButtonVariant.Primary,
            Content = labels.Continue,
            IsEnabled = _chosen.Count > 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        go.Click += (_, _) =>
        {
            if (_chosen.Count > 0)
            {
                RaiseEvent(new SeatMapSelectionEventArgs(ContinueRequestedEvent, SelectedSeats, Total));
            }
        };
        _summary.Children.Add(go);
    }

    private void FocusRemoveAfter(int index)
    {
        var buttons = _summary?.GetLogicalDescendants().OfType<Button>().Where(b => b.Variant == ButtonVariant.Ghost).ToList();
        if (buttons is { Count: > 0 })
        {
            buttons[Math.Clamp(index, 0, buttons.Count - 1)].Focus();
        }
        else
        {
            _canvas?.Focus();
        }
    }

    private static Control Pair(string label, string value, bool strong)
    {
        var row = new DockPanel();
        var amount = new TextBlock { Text = value, FontFeatures = FontFeatureCollection.Parse("+tnum") };
        DockPanel.SetDock(amount, Dock.Right);
        var name = new TextBlock { Text = label };
        if (strong)
        {
            ArcTokens.Bind(amount, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            ArcTokens.Bind(amount, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeBase);
            ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        else
        {
            Muted(name);
        }
        row.Children.Add(amount);
        row.Children.Add(name);
        return row;
    }

    private static TextBlock Muted(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return text;
    }

    private void UpdateCompact(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactThreshold : _isCompact;
        if (!force && compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }
}

/// <summary>A dot of the legend and of the summary rows (classes s1–s4, taken, chosen).</summary>
internal sealed class SeatMapLegendDot : Border
{
    protected override Type StyleKeyOverride => typeof(SeatMapLegendDot);
}

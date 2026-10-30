using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What a row needs besides its provider.</summary>
internal readonly record struct ProviderStatusRowContext(
    ProviderStatusLabels Labels,
    bool IsNarrow,
    bool IsExpanded,
    DateOnly Today,
    bool ShowsLink);

/// <summary>
/// One provider of a <see cref="ProviderStatus"/>: the mark, the name over its detail (or its incident's title), the health
/// as an icon and label, the median latency, the 30 day uptime bar and percentage, and, with an incident, a chevron and a
/// full-row button that opens the incident's updates newest first. Values change with a single crossfade in reserved
/// cells (Arc's label swap: out in 100ms, in over 160ms rising 2px), so nothing reflows. The row is reused while its
/// provider stays, and updates are reused while they stay, so a check never rebuilds what did not change.
/// </summary>
[TemplatePart(TogglePart, typeof(ProviderStatusRowButton))]
[TemplatePart(HeadPart, typeof(ProviderStatusRowPanel))]
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(MonogramPart, typeof(Border))]
[TemplatePart(MonogramTextPart, typeof(TextBlock))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(DetailPart, typeof(ArcContentSwap))]
[TemplatePart(HealthPart, typeof(ArcContentSwap))]
[TemplatePart(LatencyPart, typeof(ArcContentSwap))]
[TemplatePart(BarPart, typeof(ProviderStatusUptimeBar))]
[TemplatePart(UptimePart, typeof(ArcContentSwap))]
[TemplatePart(PanelPart, typeof(AiHeightBox))]
[TemplatePart(IncidentTitlePart, typeof(TextBlock))]
[TemplatePart(UpdatesPart, typeof(StackPanel))]
[TemplatePart(LinkPart, typeof(AvaloniaButton))]
[TemplatePart(LinkTextPart, typeof(TextBlock))]
[PseudoClasses(NarrowPseudoClass, IncidentPseudoClass, ExpandedPseudoClass, ResolvedPseudoClass, DegradedPseudoClass, OutagePseudoClass)]
internal sealed class ProviderStatusRow : TemplatedControl
{
    internal const string TogglePart = "PART_Toggle";
    internal const string HeadPart = "PART_Head";
    internal const string MarkPart = "PART_Mark";
    internal const string MonogramPart = "PART_Monogram";
    internal const string MonogramTextPart = "PART_MonogramText";
    internal const string NamePart = "PART_Name";
    internal const string DetailPart = "PART_Detail";
    internal const string HealthPart = "PART_Health";
    internal const string LatencyPart = "PART_Latency";
    internal const string BarPart = "PART_Bar";
    internal const string UptimePart = "PART_Uptime";
    internal const string PanelPart = "PART_Panel";
    internal const string IncidentTitlePart = "PART_IncidentTitle";
    internal const string UpdatesPart = "PART_Updates";
    internal const string LinkPart = "PART_Link";
    internal const string LinkTextPart = "PART_LinkText";

    internal const string NarrowPseudoClass = ":narrow";
    internal const string IncidentPseudoClass = ":incident";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string ResolvedPseudoClass = ":resolved";
    internal const string DegradedPseudoClass = ":degraded";
    internal const string OutagePseudoClass = ":outage";

    /// <summary>The health icon's size.</summary>
    public static readonly StyledProperty<double> HealthIconSizeProperty =
        AvaloniaProperty.Register<ProviderStatusRow, double>(nameof(HealthIconSize), 15);

    /// <summary>The space between the health icon and its label.</summary>
    public static readonly StyledProperty<double> HealthGapProperty =
        AvaloniaProperty.Register<ProviderStatusRow, double>(nameof(HealthGap), 6);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly List<ProviderStatusUpdateView> _updateViews = new();
    private ProviderStatusItem? _item;
    private ProviderStatusRowContext _context = new(ProviderStatusLabels.Default, false, false, default, false);
    private ProviderStatusDayCell[] _window = ProviderStatusFormat.Window(null, DateOnly.FromDateTime(DateTime.UtcNow));
    private string? _incidentId;
    private bool _applied;

    private ProviderStatusRowButton? _toggle;
    private ProviderStatusRowPanel? _head;
    private ArcGlyph? _mark;
    private Border? _monogram;
    private TextBlock? _monogramText;
    private TextBlock? _name;
    private ArcContentSwap? _detail;
    private ArcContentSwap? _health;
    private ArcContentSwap? _latency;
    private ProviderStatusUptimeBar? _bar;
    private ArcContentSwap? _uptime;
    private AiHeightBox? _panel;
    private TextBlock? _incidentTitle;
    private StackPanel? _updates;
    private AvaloniaButton? _link;
    private TextBlock? _linkText;

    private string? _shownDetail;
    private (ProviderStatusHealth Health, string Text)? _shownHealth;
    private string? _shownLatency;
    private string? _shownUptime;

    public double HealthIconSize { get => GetValue(HealthIconSizeProperty); set => SetValue(HealthIconSizeProperty, value); }

    public double HealthGap { get => GetValue(HealthGapProperty); set => SetValue(HealthGapProperty, value); }

    /// <summary>The provider shown.</summary>
    internal ProviderStatusItem? Item => _item;

    /// <summary>The 30 days the bar shows.</summary>
    internal ProviderStatusDayCell[] Window => _window;

    /// <summary>The 30 day uptime, or null.</summary>
    internal double? Uptime => ProviderStatusFormat.Average(_window);

    internal ProviderStatusUptimeBar? Bar => _bar;

    internal ProviderStatusRowButton? Toggle => _toggle;

    internal AiHeightBox? Panel => _panel;

    internal ProviderStatusRowPanel? Head => _head;

    internal AvaloniaButton? Link => _link;

    /// <summary>The update views in the open panel, newest first.</summary>
    internal IReadOnlyList<ProviderStatusUpdateView> UpdateViews => _updateViews;

    /// <summary>The texts drawn now (tests).</summary>
    internal string? HealthText => _shownHealth?.Text;

    internal string? LatencyText => _shownLatency;

    internal string? UptimeText => _shownUptime;

    internal string? DetailText => _shownDetail;

    /// <summary>Raised when the row's button is pressed.</summary>
    internal event Action<ProviderStatusRow>? ToggleRequested;

    /// <summary>Raised when the Status page link is pressed.</summary>
    internal event Action<ProviderStatusRow>? LinkRequested;

    /// <summary>Raised when the bar's shown day changes: the bar, the day (or -1) and whether a key moved it.</summary>
    internal event Action<ProviderStatusUptimeBar, int, bool>? DayActiveChanged;

    /// <summary>The icon of a health (null: no data).</summary>
    internal static Geometry IconOf(ProviderStatusHealth? health) => health switch
    {
        ProviderStatusHealth.Operational => ArcIcons.CircleCheck,
        ProviderStatusHealth.Degraded => ArcIcons.TriangleAlert,
        ProviderStatusHealth.Outage => ArcIcons.CircleX,
        _ => ArcIcons.CircleMinus
    };

    /// <summary>The token of a health's color.</summary>
    internal static ArcDesignTokenKind InkOf(ProviderStatusHealth health) => health switch
    {
        ProviderStatusHealth.Degraded => ArcDesignTokenKind.Warning,
        ProviderStatusHealth.Outage => ArcDesignTokenKind.Danger,
        _ => ArcDesignTokenKind.Success
    };

    /// <summary>Shows <paramref name="item"/>; parts that already show a value are left alone.</summary>
    internal void Apply(ProviderStatusItem item, ProviderStatusRowContext context)
    {
        var first = !_applied;
        _item = item;
        _context = context;
        _window = ProviderStatusFormat.Window(item.Days, context.Today);
        Refresh(first);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggle;
        }
        if (_link is not null)
        {
            _link.Click -= OnLink;
        }
        if (_bar is not null)
        {
            _bar.ActiveChanged -= OnBarActive;
        }
        _toggle = e.NameScope.Find<ProviderStatusRowButton>(TogglePart);
        _head = e.NameScope.Find<ProviderStatusRowPanel>(HeadPart);
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _monogram = e.NameScope.Find<Border>(MonogramPart);
        _monogramText = e.NameScope.Find<TextBlock>(MonogramTextPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _detail = e.NameScope.Find<ArcContentSwap>(DetailPart);
        _health = e.NameScope.Find<ArcContentSwap>(HealthPart);
        _latency = e.NameScope.Find<ArcContentSwap>(LatencyPart);
        _bar = e.NameScope.Find<ProviderStatusUptimeBar>(BarPart);
        _uptime = e.NameScope.Find<ArcContentSwap>(UptimePart);
        _panel = e.NameScope.Find<AiHeightBox>(PanelPart);
        if (_panel is not null)
        {
            _panel.HeightSpring = AiHeightBox.PanelSpring;
        }
        _incidentTitle = e.NameScope.Find<TextBlock>(IncidentTitlePart);
        _updates = e.NameScope.Find<StackPanel>(UpdatesPart);
        _link = e.NameScope.Find<AvaloniaButton>(LinkPart);
        _linkText = e.NameScope.Find<TextBlock>(LinkTextPart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggle;
        }
        if (_link is not null)
        {
            _link.Click += OnLink;
        }
        if (_bar is not null)
        {
            _bar.ActiveChanged += OnBarActive;
        }
        _shownDetail = null;
        _shownHealth = null;
        _shownLatency = null;
        _shownUptime = null;
        _incidentId = null;
        _updateViews.Clear();
        _updates?.Children.Clear();
        if (_item is not null)
        {
            Refresh(instant: true);
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => ToggleRequested?.Invoke(this);

    private void OnBarActive(ProviderStatusUptimeBar bar, int index, bool fromKeyboard) => DayActiveChanged?.Invoke(bar, index, fromKeyboard);

    private void OnLink(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        LinkRequested?.Invoke(this);
    }

    private void Refresh(bool instant)
    {
        if (_item is not { } item || _toggle is null)
        {
            return;
        }
        _applied = true;
        var labels = _context.Labels;
        var incident = item.Incident;
        var narrow = _context.IsNarrow;
        var resolved = incident is not null && ProviderStatusFormat.IsResolved(incident);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(IncidentPseudoClass, incident is not null);
        PseudoClasses.Set(ResolvedPseudoClass, resolved);
        PseudoClasses.Set(DegradedPseudoClass, item.Health == ProviderStatusHealth.Degraded);
        PseudoClasses.Set(OutagePseudoClass, item.Health == ProviderStatusHealth.Outage);
        var expanded = _context.IsExpanded && incident is not null;
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        if (_head is not null)
        {
            _head.IsNarrow = narrow;
        }

        // Mark: the provider's glyph, or its first letter in a tinted square.
        if (_mark is not null)
        {
            _mark.Data = item.Mark;
            _mark.IsVisible = item.Mark is not null;
            if (item.MarkBrush is { } markBrush)
            {
                _mark.Foreground = markBrush;
            }
            else
            {
                _mark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = item.Mark is null;
            if (item.MarkBrush is { } tint)
            {
                _monogram.Background = tint;
            }
            else
            {
                _monogram.ClearValue(Border.BackgroundProperty);
            }
        }
        if (_monogramText is not null)
        {
            _monogramText.Text = ProviderStatusFormat.Monogram(item.Name);
        }
        if (_name is not null && _name.Text != item.Name)
        {
            _name.Text = item.Name;
        }

        var detail = incident?.Title ?? item.Detail ?? string.Empty;
        if (_detail is not null && _shownDetail != detail)
        {
            _shownDetail = detail;
            _detail.Content = Text(detail, trims: true);
        }

        var healthText = labels.HealthText(item.Health);
        if (_health is not null && _shownHealth != (item.Health, healthText))
        {
            _shownHealth = (item.Health, healthText);
            _health.Content = HealthContent(item.Health, healthText);
        }

        var latency = ProviderStatusFormat.Latency(item.LatencyMs, labels);
        if (narrow && item.LatencyMs is not null)
        {
            latency = labels.LatencyNarrow(latency);
        }
        if (_latency is not null && _shownLatency != latency)
        {
            _shownLatency = latency;
            _latency.Content = Text(latency, trims: true, tabular: true);
        }

        var uptime = ProviderStatusFormat.Uptime(Uptime, labels);
        if (_uptime is not null && _shownUptime != uptime)
        {
            _shownUptime = uptime;
            _uptime.Content = Text(uptime, trims: false, tabular: true);
        }

        if (_bar is not null)
        {
            _bar.SetDays(_window);
            var (degradedDays, outageDays) = ProviderStatusFormat.Counts(_window);
            AutomationProperties.SetName(_bar, labels.BarName(item.Name, uptime, degradedDays, outageDays));
        }

        _toggle.IsVisible = incident is not null;
        _toggle.IsExpanded = expanded;
        AutomationProperties.SetName(_toggle, incident is null ? null : labels.IncidentRow(item.Name, healthText, incident.Title));

        SyncPanel(incident, instant);
        if (_panel is not null && _panel.IsExpanded != expanded)
        {
            if (expanded && instant)
            {
                _panel.OpenInstantly();
            }
            else
            {
                _panel.IsExpanded = expanded;
            }
        }
    }

    private void SyncPanel(ProviderStatusIncident? incident, bool instant)
    {
        if (_updates is null)
        {
            return;
        }
        var labels = _context.Labels;
        if (incident is null)
        {
            // The panel keeps the last incident while it folds away.
            return;
        }
        if (_incidentTitle is not null)
        {
            _incidentTitle.Text = incident.Title;
        }
        if (_link is not null)
        {
            _link.IsVisible = _context.ShowsLink;
            AutomationProperties.SetName(_link, labels.StatusPage);
        }
        if (_linkText is not null)
        {
            _linkText.Text = labels.StatusPage;
        }
        AutomationProperties.SetName(_updates, labels.Updates);
        if (_incidentId != incident.Id)
        {
            _incidentId = incident.Id;
            _updateViews.Clear();
            _updates.Children.Clear();
            instant = true;
        }
        var newest = ProviderStatusFormat.Newest(incident);
        var fades = !instant && _panel?.IsExpanded == true && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var kept = new List<ProviderStatusUpdateView>(newest.Count);
        foreach (var update in newest)
        {
            var view = _updateViews.Find(v => v.Update is { } u && SameUpdate(u, update));
            var isNew = view is null;
            view ??= new ProviderStatusUpdateView();
            view.Update = update;
            view.Stage = update.Stage;
            view.StageText = labels.StageText(update.Stage);
            view.TimeText = ProviderStatusFormat.TimeOf(update, labels);
            view.Message = update.Message;
            if (isNew && fades)
            {
                // A new update fades in while the panel springs to its new height.
                view.Opacity = 0;
                var fading = view;
                Dispatcher.UIThread.Post(() => fading.Opacity = 1, DispatcherPriority.Loaded);
            }
            kept.Add(view);
        }
        _updateViews.Clear();
        _updateViews.AddRange(kept);
        for (var i = 0; i < kept.Count; i++)
        {
            var view = kept[i];
            var at = _updates.Children.IndexOf(view);
            if (at != i)
            {
                if (at >= 0)
                {
                    _updates.Children.RemoveAt(at);
                }
                _updates.Children.Insert(i, view);
            }
            view.SetPosition(i == 0, i == kept.Count - 1);
        }
        while (_updates.Children.Count > kept.Count)
        {
            _updates.Children.RemoveAt(_updates.Children.Count - 1);
        }
    }

    private static bool SameUpdate(ProviderStatusUpdate a, ProviderStatusUpdate b) =>
        ReferenceEquals(a, b) || (a.Stage == b.Stage && a.At == b.At && a.AtText == b.AtText && a.Message == b.Message);

    private static TextBlock Text(string text, bool trims, bool tabular = false)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (trims)
        {
            block.TextTrimming = TextTrimming.CharacterEllipsis;
        }
        if (tabular)
        {
            block.FontFeatures = s_tabular;
        }
        return block;
    }

    private Control HealthContent(ProviderStatusHealth health, string text)
    {
        var glyph = new ArcGlyph { Data = IconOf(health), Size = HealthIconSize, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, InkOf(health));
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = HealthGap, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(glyph);
        panel.Children.Add(Text(text, trims: true));
        return panel;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>A list item named by the provider and its health.</summary>
    private sealed class RowPeer(ProviderStatusRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore()
        {
            var row = (ProviderStatusRow)Owner;
            return row._item is { } item ? item.Name + ", " + row._context.Labels.HealthText(item.Health) : null;
        }
    }
}

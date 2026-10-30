using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A status card for the AI providers a product depends on (Arc Provider status, design reference). Each row shows the
/// provider's mark, its health as an icon and label (Operational, Degraded, Outage), the median latency in tabular
/// numerals and a 30 day uptime bar of small ticks with a tooltip per day. A row with an incident is a button that opens
/// its updates (Investigating, Identified, Monitoring, Resolved), newest first; new updates grow the panel smoothly. The
/// header sums up health across providers and checks again on a visible countdown ring, with a refresh button, a Checking
/// state and an error state that keeps the last results. Cards narrower than 560px take two lines per row.
/// <para>
/// Give it <see cref="Providers"/> and a <see cref="RefreshHandler"/> that fetches fresh status (from your own
/// aggregation endpoint). With neither, a <see cref="Simulation"/> plays its story on simulated checks; without one the
/// card says there are no providers.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SummaryPart, typeof(ArcContentSwap))]
[TemplatePart(CountdownPart, typeof(Control))]
[TemplatePart(RingPart, typeof(ArcRing))]
[TemplatePart(SpinnerPart, typeof(ArcSpinner))]
[TemplatePart(CountdownTextPart, typeof(TextBlock))]
[TemplatePart(CheckingTextPart, typeof(ToolCallText))]
[TemplatePart(RefreshPart, typeof(AvaloniaButton))]
[TemplatePart(ColumnsPart, typeof(ProviderStatusRowPanel))]
[TemplatePart(RowsPart, typeof(ProviderStatusRowList))]
[TemplatePart(SkeletonPart, typeof(ProviderStatusSkeleton))]
[TemplatePart(FootnotePart, typeof(ContentPresenter))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CheckingPseudoClass, ErrorPseudoClass, LoadingPseudoClass)]
public class ProviderStatus : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string SummaryPart = "PART_Summary";
    internal const string CountdownPart = "PART_Countdown";
    internal const string RingPart = "PART_Ring";
    internal const string SpinnerPart = "PART_Spinner";
    internal const string CountdownTextPart = "PART_CountdownText";
    internal const string CheckingTextPart = "PART_CheckingText";
    internal const string RefreshPart = "PART_Refresh";
    internal const string ColumnsPart = "PART_Columns";
    internal const string RowsPart = "PART_Rows";
    internal const string SkeletonPart = "PART_Skeleton";
    internal const string FootnotePart = "PART_Footnote";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string NarrowPseudoClass = ":narrow";
    internal const string CheckingPseudoClass = ":checking";
    internal const string ErrorPseudoClass = ":error";
    internal const string LoadingPseudoClass = ":loading";

    /// <summary>Below this card width rows take two lines (a container rule: it follows the card, not the window).</summary>
    public const double NarrowWidth = 560;

    /// <summary>The longest wait before a retry after a failed check.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    /// <summary>The providers to show. Leave it null (with no <see cref="RefreshHandler"/>) to play <see cref="Simulation"/>. A check that returns providers writes them here.</summary>
    public static readonly StyledProperty<IReadOnlyList<ProviderStatusItem>?> ProvidersProperty =
        AvaloniaProperty.Register<ProviderStatus, IReadOnlyList<ProviderStatusItem>?>(nameof(Providers), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Fetches fresh status on the countdown and on the refresh button. Return the new providers, or update
    /// <see cref="Providers"/> yourself and return null. Throw to show the error state: the last results stay and the
    /// countdown turns into a retry. A newer check cancels the older one through the token. Without it the refresh controls
    /// are hidden.
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<IReadOnlyList<ProviderStatusItem>?>>?> RefreshHandlerProperty =
        AvaloniaProperty.Register<ProviderStatus, Func<CancellationToken, Task<IReadOnlyList<ProviderStatusItem>?>>?>(nameof(RefreshHandler));

    /// <summary>
    /// Time between automatic checks (30 seconds by default). The countdown pauses while the card is off screen or hidden;
    /// retries after an error wait at most 10 seconds. Null turns automatic checks off and keeps the refresh button.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> RefreshIntervalProperty =
        AvaloniaProperty.Register<ProviderStatus, TimeSpan?>(nameof(RefreshInterval), TimeSpan.FromSeconds(30));

    /// <summary>Shows skeleton rows with the same row height, for the first load.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProviderStatus, bool>(nameof(IsLoading));

    /// <summary>An error to show in the header, such as a failed first load. Errors thrown by the refresh handler show on their own.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<ProviderStatus, string?>(nameof(ErrorText));

    /// <summary>The card's title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProviderStatus, string>(nameof(Title), "AI providers");

    /// <summary>The provider whose incident is open (two-way), or null.</summary>
    public static readonly StyledProperty<string?> ExpandedIdProperty =
        AvaloniaProperty.Register<ProviderStatus, string?>(nameof(ExpandedId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>A quiet line under the list, such as how latency and uptime are measured. A playing simulation shows a sample data note.</summary>
    public static readonly StyledProperty<object?> FootnoteProperty =
        AvaloniaProperty.Register<ProviderStatus, object?>(nameof(Footnote));

    /// <summary>A one-off message for screen readers, such as the result of an action; it is read each time it changes.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<ProviderStatus, string?>(nameof(Notice));

    /// <summary>
    /// A simulated status feed to play while <see cref="Providers"/> and <see cref="RefreshHandler"/> are null: its first
    /// step shows at once, and each simulated check on the countdown moves its story on. Null (the default) shows no
    /// providers. Give each card its own simulation.
    /// </summary>
    public static readonly StyledProperty<ProviderStatusSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ProviderStatus, ProviderStatusSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ProviderStatusLabels> LabelsProperty =
        AvaloniaProperty.Register<ProviderStatus, ProviderStatusLabels>(nameof(Labels), ProviderStatusLabels.Default);

    /// <summary>True while a check runs.</summary>
    public static readonly DirectProperty<ProviderStatus, bool> IsCheckingProperty =
        AvaloniaProperty.RegisterDirect<ProviderStatus, bool>(nameof(IsChecking), o => o.IsChecking);

    /// <summary>Raised when an incident row is opened or closed by the person. Bubbles.</summary>
    public static readonly RoutedEvent<ProviderStatusExpandedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ProviderStatus, ProviderStatusExpandedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised by the Status page link of an open incident. Bubbles. When nothing handles it, the link opens the incident's
    /// <see cref="ProviderStatusIncident.Url"/>; with neither a handler nor a URL the link is hidden.
    /// </summary>
    public static readonly RoutedEvent<ProviderStatusIncidentEventArgs> IncidentOpenedEvent =
        RoutedEvent.Register<ProviderStatus, ProviderStatusIncidentEventArgs>(nameof(IncidentOpened), RoutingStrategies.Bubble);

    private readonly Dictionary<string, ProviderStatusRow> _rows = new();
    private readonly LiveLoop _countdown;
    private CancellationTokenSource? _checkSource;
    private bool _checking;
    private string? _refreshError;
    private double _left;
    private double _total;
    private int _shownSeconds = -1;
    private bool _narrow;
    private int _incidentHandlers;
    private ArcPopup? _tipPopup;
    private ProviderStatusDayTip? _tip;
    private ProviderStatusUptimeBar? _tipBar;
    private string? _shownSummary;

    private TextBlock? _title;
    private ArcContentSwap? _summary;
    private Control? _countdownBox;
    private ArcRing? _ring;
    private ArcSpinner? _spinner;
    private TextBlock? _countdownText;
    private ToolCallText? _checkingText;
    private AvaloniaButton? _refresh;
    private ProviderStatusRowPanel? _columns;
    private ProviderStatusRowList? _rowList;
    private ProviderStatusSkeleton? _skeleton;
    private ContentPresenter? _footnote;
    private Control? _footnoteBox;
    private object? _footnoteSource;
    private ExplainerAnnouncer? _announcer;

    /// <summary>Creates the card.</summary>
    public ProviderStatus()
    {
        // The countdown advances only on frames, so it pauses by itself while the card is off screen, hidden or away.
        _countdown = new LiveLoop(this, OnCountdownFrame) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <inheritdoc cref="ProvidersProperty"/>
    public IReadOnlyList<ProviderStatusItem>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <inheritdoc cref="RefreshHandlerProperty"/>
    public Func<CancellationToken, Task<IReadOnlyList<ProviderStatusItem>?>>? RefreshHandler
    {
        get => GetValue(RefreshHandlerProperty);
        set => SetValue(RefreshHandlerProperty, value);
    }

    /// <inheritdoc cref="RefreshIntervalProperty"/>
    public TimeSpan? RefreshInterval { get => GetValue(RefreshIntervalProperty); set => SetValue(RefreshIntervalProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ExpandedIdProperty"/>
    public string? ExpandedId { get => GetValue(ExpandedIdProperty); set => SetValue(ExpandedIdProperty, value); }

    /// <inheritdoc cref="FootnoteProperty"/>
    public object? Footnote { get => GetValue(FootnoteProperty); set => SetValue(FootnoteProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ProviderStatusSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ProviderStatusLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsCheckingProperty"/>
    public bool IsChecking
    {
        get => _checking;
        private set => SetAndRaise(IsCheckingProperty, ref _checking, value);
    }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<ProviderStatusExpandedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    /// <inheritdoc cref="IncidentOpenedEvent"/>
    public event EventHandler<ProviderStatusIncidentEventArgs>? IncidentOpened
    {
        add
        {
            AddHandler(IncidentOpenedEvent, value);
            _incidentHandlers++;
            RefreshRows();
        }
        remove
        {
            RemoveHandler(IncidentOpenedEvent, value);
            _incidentHandlers = Math.Max(0, _incidentHandlers - 1);
            RefreshRows();
        }
    }

    /// <summary>True while a simulation plays (no providers, no refresh handler, and a simulation).</summary>
    internal bool UsesSample => Providers is null && RefreshHandler is null && Simulation is not null;

    /// <summary>The providers on screen.</summary>
    internal IReadOnlyList<ProviderStatusItem> EffectiveProviders => Providers ?? (UsesSample ? Simulation!.Current : []);

    /// <summary>The interval in effect (the simulation's while it plays), or null without automatic checks.</summary>
    internal TimeSpan? EffectiveInterval => UsesSample ? Simulation!.Interval : RefreshInterval;

    /// <summary>Whether the refresh controls show.</summary>
    internal bool CanRefresh => UsesSample || RefreshHandler is not null;

    /// <summary>The error the header shows: the app's, or the last failed check's.</summary>
    internal string? DisplayedError => !string.IsNullOrEmpty(ErrorText) ? ErrorText : _refreshError;

    /// <summary>Seconds left until the next check (0 when none is counting down).</summary>
    internal double SecondsLeft => _countdown.IsActive ? Math.Max(0, _left) : 0;

    /// <summary>The countdown's length in seconds.</summary>
    internal double CountdownLength => _total;

    /// <summary>True while the countdown is wanted (it may still wait off screen).</summary>
    internal bool IsCountingDown => _countdown.IsActive;

    /// <summary>The header's summary or error text.</summary>
    internal string? SummaryText => _shownSummary;

    internal string? CountdownText => _countdownText?.Text;

    internal ArcRing? Ring => _ring;

    internal ArcSpinner? Spinner => _spinner;

    internal AvaloniaButton? RefreshButton => _refresh;

    internal ProviderStatusSkeleton? Skeleton => _skeleton;

    internal ProviderStatusRowList? RowList => _rowList;

    internal ContentPresenter? FootnotePresenter => _footnote;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal ArcPopup? TipPopup => _tipPopup;

    internal ProviderStatusDayTip? Tip => _tip;

    internal bool IsNarrow => _narrow;

    /// <summary>The row of a provider, if shown.</summary>
    internal ProviderStatusRow? RowOf(string id) => _rows.GetValueOrDefault(id);

    /// <summary>
    /// Checks now: aborts a check in flight, shows Checking, runs the refresh handler (or the simulation's check) and
    /// restarts the countdown. A failure keeps the last results and shows the error. Does nothing without a handler.
    /// </summary>
    public async Task CheckNow()
    {
        var handler = RefreshHandler;
        var simulation = handler is null && UsesSample ? Simulation : null;
        var sample = simulation is not null;
        if (handler is null && !sample)
        {
            return;
        }
        _checkSource?.Cancel();
        var source = new CancellationTokenSource();
        _checkSource = source;
        IsChecking = true;
        _countdown.Stop();
        UpdateHeader();
        try
        {
            IReadOnlyList<ProviderStatusItem>? result;
            if (simulation is not null)
            {
                result = await simulation.CheckAsync(source.Token);
            }
            else
            {
                result = await handler!(source.Token);
            }
            if (!ReferenceEquals(source, _checkSource))
            {
                return;
            }
            _refreshError = null;
            if (result is not null)
            {
                if (sample)
                {
                    RefreshRows();
                }
                else
                {
                    SetCurrentValue(ProvidersProperty, result);
                }
            }
            _announcer?.Announce(Labels.Checked(Summary()));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            if (!ReferenceEquals(source, _checkSource))
            {
                return;
            }
            _refreshError = error is ProviderStatusSimulationException || string.IsNullOrWhiteSpace(error.Message)
                ? Labels.RefreshFailed
                : error.Message;
            _announcer?.Announce(_refreshError);
        }
        finally
        {
            if (ReferenceEquals(source, _checkSource))
            {
                _checkSource = null;
                IsChecking = false;
                RestartCountdown();
                UpdateHeader();
            }
            source.Dispose();
        }
    }

    /// <summary>Runs <paramref name="seconds"/> of countdown by hand (tests: headless frames do not advance it).</summary>
    internal void AdvanceCountdown(double seconds) => OnCountdownFrame(seconds);

    private bool OnCountdownFrame(double dt)
    {
        if (_checking || _total <= 0)
        {
            return false;
        }
        _left = Math.Max(0, _left - Math.Max(0, dt));
        UpdateCountdown();
        if (_left <= 0)
        {
            _ = CheckNow();
            return false;
        }
        return true;
    }

    /// <summary>Starts the countdown again: the interval, or the retry wait after a failure (at most 10 seconds).</summary>
    private void RestartCountdown()
    {
        var interval = EffectiveInterval;
        if (!CanRefresh || interval is not { } wait || wait <= TimeSpan.Zero)
        {
            _total = 0;
            _left = 0;
            _countdown.Stop();
            UpdateCountdown();
            return;
        }
        if (_refreshError is not null && wait > MaxRetryDelay)
        {
            wait = MaxRetryDelay;
        }
        _total = wait.TotalSeconds;
        _left = _total;
        _shownSeconds = -1;
        _countdown.Start();
        UpdateCountdown();
    }

    /// <summary>The ring drains linearly; the seconds text changes at most once a second.</summary>
    private void UpdateCountdown()
    {
        if (_ring is not null)
        {
            _ring.Value = _total > 0 ? _left / _total : 0;
        }
        if (_countdownText is null)
        {
            return;
        }
        var seconds = (int)Math.Ceiling(_left - 1e-6);
        if (seconds == _shownSeconds && _countdownText.Text is { Length: > 0 })
        {
            return;
        }
        _shownSeconds = seconds;
        var labels = Labels;
        _countdownText.Text = _total <= 0 ? string.Empty
            : _refreshError is not null ? labels.RetryIn(seconds)
            : _narrow ? labels.NextCheckShort(seconds)
            : labels.NextCheck(seconds);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_refresh is not null)
        {
            _refresh.Click -= OnRefreshClick;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _summary = e.NameScope.Find<ArcContentSwap>(SummaryPart);
        _countdownBox = e.NameScope.Find<Control>(CountdownPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _spinner = e.NameScope.Find<ArcSpinner>(SpinnerPart);
        _countdownText = e.NameScope.Find<TextBlock>(CountdownTextPart);
        _checkingText = e.NameScope.Find<ToolCallText>(CheckingTextPart);
        _refresh = e.NameScope.Find<AvaloniaButton>(RefreshPart);
        _columns = e.NameScope.Find<ProviderStatusRowPanel>(ColumnsPart);
        _rowList = e.NameScope.Find<ProviderStatusRowList>(RowsPart);
        _skeleton = e.NameScope.Find<ProviderStatusSkeleton>(SkeletonPart);
        _footnote = e.NameScope.Find<ContentPresenter>(FootnotePart);
        _footnoteBox = e.NameScope.Find<Control>("PART_FootnoteBox");
        _footnoteSource = null;
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_refresh is not null)
        {
            _refresh.Click += OnRefreshClick;
        }
        foreach (var row in _rows.Values)
        {
            Unhook(row);
        }
        _rows.Clear();
        _rowList?.Children.Clear();
        _shownSummary = null;
        _shownSeconds = -1;
        UpdateNarrow(Bounds.Width);
        RefreshAll();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == ProvidersProperty || p == ExpandedIdProperty)
        {
            if (p == ProvidersProperty && RefreshHandler is null && (change.OldValue is null) != (change.NewValue is null))
            {
                // The app's own providers stop a playing simulation; clearing them hands the card back to it.
                _checkSource?.Cancel();
                if (!_checking)
                {
                    RestartCountdown();
                }
            }
            RefreshRows();
            UpdateHeader();
        }
        else if (p == RefreshHandlerProperty || p == RefreshIntervalProperty || p == SimulationProperty)
        {
            if (p != RefreshIntervalProperty)
            {
                _checkSource?.Cancel();
            }
            RefreshAll();
            if (!_checking)
            {
                RestartCountdown();
            }
        }
        else if (p == LabelsProperty || p == IsLoadingProperty || p == ErrorTextProperty || p == TitleProperty || p == FootnoteProperty)
        {
            RefreshAll();
        }
        else if (p == NoticeProperty)
        {
            _announcer?.Announce(Notice);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_checking && !_countdown.IsActive)
        {
            RestartCountdown();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HideTip();
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateNarrow(e.NewSize.Width);
    }

    private void UpdateNarrow(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        var narrow = width < NarrowWidth;
        if (narrow == _narrow && _rows.Count > 0)
        {
            return;
        }
        _narrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        HideTip();
        RefreshAll();
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => _ = CheckNow();

    private void RefreshAll()
    {
        RefreshRows();
        UpdateHeader();
    }

    private string Summary()
    {
        var (operational, degraded, outage) = ProviderStatusFormat.Health(EffectiveProviders);
        return Labels.Summary(operational, degraded, outage);
    }

    private void UpdateHeader()
    {
        if (_title is null)
        {
            return;
        }
        var labels = Labels;
        _title.Text = Title;
        var error = DisplayedError;
        var loading = IsLoading;
        PseudoClasses.Set(ErrorPseudoClass, error is not null);
        PseudoClasses.Set(CheckingPseudoClass, _checking);
        PseudoClasses.Set(LoadingPseudoClass, loading);

        var summary = error ?? (loading ? labels.Loading : EffectiveProviders.Count == 0 ? labels.NoProviders : Summary());
        var key = (error is not null ? "!" : "") + summary;
        if (_summary is not null && _shownSummary != key)
        {
            _shownSummary = key;
            _summary.Content = error is not null ? ErrorContent(error) : SummaryContent(summary, loading);
        }

        var canRefresh = CanRefresh;
        if (_countdownBox is not null)
        {
            _countdownBox.IsVisible = canRefresh && (_checking || EffectiveInterval is not null);
        }
        if (_ring is not null)
        {
            _ring.IsVisible = !_checking && _total > 0;
        }
        if (_spinner is not null)
        {
            // Reduced motion drops the spinner; the Checking label stays.
            _spinner.IsVisible = _checking && ArcMotion.IsFull(this);
        }
        if (_countdownText is not null)
        {
            _countdownText.IsVisible = !_checking;
        }
        if (_checkingText is not null)
        {
            _checkingText.IsVisible = _checking;
            _checkingText.Text = labels.Checking;
            _checkingText.IsShimmering = _checking;
        }
        if (_refresh is not null)
        {
            _refresh.IsVisible = canRefresh;
            var name = _refreshError is not null ? labels.Retry : labels.CheckNow;
            AutomationProperties.SetName(_refresh, name);
            Tooltip.SetTip(_refresh, name);
        }
        if (_countdownText is not null)
        {
            _shownSeconds = -1;
            UpdateCountdown();
        }

        if (_columns is not null)
        {
            _columns.IsVisible = !_narrow;
            SetColumnText(1, labels.ProviderColumn);
            SetColumnText(2, labels.StatusColumn);
            SetColumnText(3, labels.LatencyColumn);
            SetColumnText(4, labels.HistoryColumn);
            SetColumnText(5, labels.UptimeColumn);
        }
        if (_skeleton is not null)
        {
            _skeleton.IsVisible = loading;
            _skeleton.IsNarrow = _narrow;
            _skeleton.RowCount = Providers is { Count: > 0 } known ? known.Count : 4;
        }
        if (_rowList is not null)
        {
            _rowList.IsVisible = !loading;
            AutomationProperties.SetName(_rowList, Title);
        }
        if (_footnote is not null)
        {
            var footnote = Footnote ?? (UsesSample ? labels.SampleFootnote : null);
            if (!Equals(_footnoteSource, footnote) || _footnote.Content is null != footnote is null)
            {
                _footnoteSource = footnote;
                // A string footnote wraps; anything else is shown as given.
                _footnote.Content = footnote is string text ? new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } : footnote;
            }
            if (_footnoteBox is not null)
            {
                _footnoteBox.IsVisible = footnote is not null;
            }
        }
        AutomationProperties.SetName(this, Title);
    }

    private void SetColumnText(int index, string text)
    {
        if (_columns is not null && index < _columns.Children.Count && _columns.Children[index] is TextBlock block)
        {
            block.Text = text;
        }
    }

    private static Control SummaryContent(string text, bool loading)
    {
        return new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = loading ? 0.8 : 1
        };
    }

    private static Control ErrorContent(string text)
    {
        var glyph = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        var block = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
        Grid.SetColumn(block, 1);
        grid.Children.Add(glyph);
        grid.Children.Add(block);
        return grid;
    }

    /// <summary>One row per provider, reused by id and kept in the given order.</summary>
    private void RefreshRows()
    {
        if (_rowList is null)
        {
            return;
        }
        var providers = EffectiveProviders;
        var labels = Labels;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seen = new HashSet<string>();
        var index = 0;
        foreach (var provider in providers)
        {
            var id = string.IsNullOrEmpty(provider.Id) ? provider.Name : provider.Id;
            if (!seen.Add(id))
            {
                continue;
            }
            if (!_rows.TryGetValue(id, out var row))
            {
                row = new ProviderStatusRow();
                row.ToggleRequested += OnRowToggle;
                row.LinkRequested += OnRowLink;
                row.DayActiveChanged += OnBarActive;
                _rows[id] = row;
            }
            var showsLink = provider.Incident is { } incident && (incident.Url is not null || _incidentHandlers > 0 || UsesSample);
            row.Apply(provider, new ProviderStatusRowContext(labels, _narrow, ExpandedId == id && provider.Incident is not null, today, showsLink));
            var at = _rowList.Children.IndexOf(row);
            if (at != index)
            {
                if (at >= 0)
                {
                    _rowList.Children.RemoveAt(at);
                }
                _rowList.Children.Insert(index, row);
            }
            index++;
        }
        foreach (var id in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            var row = _rows[id];
            Unhook(row);
            _rows.Remove(id);
            _rowList.Children.Remove(row);
        }
        if (_tipBar is { ActiveIndex: >= 0 } shown)
        {
            ShowTip(shown, shown.ActiveIndex, announce: false);
        }
    }

    private void Unhook(ProviderStatusRow row)
    {
        row.ToggleRequested -= OnRowToggle;
        row.LinkRequested -= OnRowLink;
        row.DayActiveChanged -= OnBarActive;
        if (row.Bar is { } bar && ReferenceEquals(bar, _tipBar))
        {
            HideTip();
        }
    }

    private void OnRowToggle(ProviderStatusRow row)
    {
        if (row.Item is not { Incident: not null } item)
        {
            return;
        }
        var id = string.IsNullOrEmpty(item.Id) ? item.Name : item.Id;
        var next = ExpandedId == id ? null : id;
        SetCurrentValue(ExpandedIdProperty, next);
        RaiseEvent(new ProviderStatusExpandedEventArgs(ExpandedChangedEvent, next));
    }

    private void OnRowLink(ProviderStatusRow row)
    {
        if (row.Item is not { Incident: { } incident } item)
        {
            return;
        }
        var args = new ProviderStatusIncidentEventArgs(IncidentOpenedEvent, item, incident);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (incident.Url is { } url && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(url);
        }
        else if (UsesSample)
        {
            _announcer?.Announce(Labels.Simulated);
        }
    }

    private void OnBarActive(ProviderStatusUptimeBar bar, int index, bool fromKeyboard)
    {
        if (index < 0)
        {
            if (ReferenceEquals(bar, _tipBar))
            {
                HideTip();
            }
            return;
        }
        if (_tipBar is { } other && !ReferenceEquals(other, bar) && other.ActiveIndex >= 0)
        {
            other.SetActive(-1, fromKeyboard: false);
        }
        ShowTip(bar, index, announce: fromKeyboard);
    }

    /// <summary>Opens (or moves) the one day tooltip of the card above day <paramref name="index"/> of <paramref name="bar"/>.</summary>
    private void ShowTip(ProviderStatusUptimeBar bar, int index, bool announce)
    {
        var cell = bar.Cells[index];
        var isToday = index == ProviderStatusFormat.WindowDays - 1;
        EnsureTip();
        _tip!.Show(cell, isToday, Labels);
        _tipBar = bar;
        var popup = _tipPopup!;
        popup.PlacementTarget = bar;
        popup.PlacementRect = bar.TickRect(index);
        if (popup.IsOpen)
        {
            popup.Reposition();
        }
        else
        {
            popup.IsOpen = true;
        }
        if (announce)
        {
            var labels = Labels;
            var uptime = cell.Uptime is { } u ? labels.DayUptime(labels.Percent(u)) : labels.NoData;
            var health = cell.Health is { } h ? labels.HealthText(h) : labels.NoData;
            _announcer?.Announce(labels.Day(isToday ? labels.Today : labels.Date(cell.Date), health, uptime, cell.Note));
        }
    }

    private void HideTip()
    {
        _tipBar = null;
        if (_tipPopup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    private void EnsureTip()
    {
        if (_tipPopup is not null)
        {
            return;
        }
        _tip = new ProviderStatusDayTip { IsHitTestVisible = false };
        _tipPopup = new ArcPopup
        {
            Child = _tip,
            Placement = PlacementMode.Top,
            IsLightDismissEnabled = false,
            EnterOffset = 3,
            EnterScale = 0.97,
            MarginToAnchor = 8,
            TakesFocusFromNativeControl = false,
            WindowManagerAddShadowHint = false,
            IsHitTestVisible = false
        };
        // One tooltip for the whole card: it takes the card's styles and theme.
        ((ISetLogicalParent)_tipPopup).SetParent(this);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    /// <summary>The card is a labelled group named by its title.</summary>
    private sealed class CardPeer(ProviderStatus owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((ProviderStatus)Owner).Title;
    }
}

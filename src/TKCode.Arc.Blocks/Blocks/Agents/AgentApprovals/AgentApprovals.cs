using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A queue of actions AI agents want to take that need a person's approval (send an email, make a payment, deploy):
/// the waiting requests on one side, and the selected one in full on the other, with the agent's reason, the facts of the
/// action, its risk, an optional note and Approve / Deny. A high-risk approval asks for an extra check first. A decided
/// request leaves the queue and the next one is selected; <see cref="Decided"/> reports each answer, and an optional
/// <see cref="DecisionHandler"/> sends it to your backend first. Without <see cref="Requests"/> it shows sample requests.
/// </summary>
/// <example><code>
/// &lt;arc:AgentApprovals Requests="{Binding PendingApprovals}" Decided="OnDecided" /&gt;
/// </code></example>
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(ApprovePart, typeof(Button))]
[TemplatePart(DenyPart, typeof(Button))]
[TemplatePart(ReasonPart, typeof(TextBox))]
[TemplatePart(ConfirmPart, typeof(CheckBox))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(EmptyPseudoClass, HighRiskPseudoClass, CompactPseudoClass, BusyPseudoClass)]
public partial class AgentApprovals : TemplatedControl
{
    public const string ListPart = "PART_List";
    public const string ApprovePart = "PART_Approve";
    public const string DenyPart = "PART_Deny";
    public const string ReasonPart = "PART_Reason";
    public const string ConfirmPart = "PART_Confirm";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while nothing is waiting.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while the selected request is high risk.</summary>
    public const string HighRiskPseudoClass = ":high-risk";

    /// <summary>Set while the queue is at most 680 wide: the list sits above the detail.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while a decision is being sent.</summary>
    public const string BusyPseudoClass = ":busy";

    #region Properties

    /// <summary>
    /// The requests. A collection that raises change notifications is followed. Decided requests leave the queue on
    /// screen; the collection itself is never changed. Null (the default) shows sample requests.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AgentApprovalRequest>?> RequestsProperty =
        AvaloniaProperty.Register<AgentApprovals, IReadOnlyList<AgentApprovalRequest>?>(nameof(Requests));

    /// <summary>The request shown in full (two-way: the list writes it).</summary>
    public static readonly StyledProperty<AgentApprovalRequest?> SelectedRequestProperty =
        AvaloniaProperty.Register<AgentApprovals, AgentApprovalRequest?>(nameof(SelectedRequest), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the queue shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AgentApprovalsLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentApprovals, AgentApprovalsLabels>(nameof(Labels), new AgentApprovalsLabels());

    /// <summary>How many requests are waiting.</summary>
    public static readonly DirectProperty<AgentApprovals, int> PendingCountProperty =
        AvaloniaProperty.RegisterDirect<AgentApprovals, int>(nameof(PendingCount), o => o.PendingCount);

    /// <summary>True while the queue is at most 680 wide.</summary>
    public static readonly DirectProperty<AgentApprovals, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AgentApprovals, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentApprovals, double>(nameof(CompactWidth), 680);

    /// <summary>Raised after a request was approved or denied (after <see cref="DecisionHandler"/> finished).</summary>
    public static readonly RoutedEvent<AgentApprovalDecidedEventArgs> DecidedEvent =
        RoutedEvent.Register<AgentApprovals, AgentApprovalDecidedEventArgs>(nameof(Decided), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RequestsProperty"/>
    public IReadOnlyList<AgentApprovalRequest>? Requests { get => GetValue(RequestsProperty); set => SetValue(RequestsProperty, value); }

    /// <inheritdoc cref="SelectedRequestProperty"/>
    public AgentApprovalRequest? SelectedRequest { get => GetValue(SelectedRequestProperty); set => SetValue(SelectedRequestProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentApprovalsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PendingCountProperty"/>
    public int PendingCount { get => _pendingCount; private set => SetAndRaise(PendingCountProperty, ref _pendingCount, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <summary>
    /// Sends a decision to your backend before the request leaves the queue: (request, decision, note, token). Throw to
    /// keep the request and show the message. Null (the default) decides at once.
    /// </summary>
    public Func<AgentApprovalRequest, AgentApprovalDecision, string?, CancellationToken, Task>? DecisionHandler { get; set; }

    /// <inheritdoc cref="DecidedEvent"/>
    public event EventHandler<AgentApprovalDecidedEventArgs>? Decided
    {
        add => AddHandler(DecidedEvent, value);
        remove => RemoveHandler(DecidedEvent, value);
    }

    #endregion

    private readonly HashSet<AgentApprovalRequest> _decided = new();
    private readonly Dictionary<AgentApprovalRequest, AgentApprovalsRow> _rows = new();
    private IReadOnlyList<AgentApprovalRequest>? _sample;
    private INotifyCollectionChanged? _observed;
    private StackPanel? _list;
    private Button? _approve;
    private Button? _deny;
    private TextBox? _reason;
    private CheckBox? _confirm;
    private AiAnnouncer? _announcer;
    private int _pendingCount;
    private bool _isCompact;
    private bool _busy;
    private CancellationTokenSource? _sending;

    static AgentApprovals()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AgentApprovals>(AutomationLandmarkType.Region);
    }

    /// <summary>The current time for relative times and the samples; tests replace it.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>The waiting requests, in order.</summary>
    internal IReadOnlyList<AgentApprovalRequest> Pending
    {
        get
        {
            var all = Requests ?? (_sample ??= AgentApprovalsSamples.Requests(Clock()));
            var pending = new List<AgentApprovalRequest>(all.Count);
            foreach (var request in all)
            {
                if (!_decided.Contains(request))
                {
                    pending.Add(request);
                }
            }
            return pending;
        }
    }

    internal AgentApprovalsRow? RowOf(AgentApprovalRequest request) => _rows.GetValueOrDefault(request);

    internal AiAnnouncer? Announcer => _announcer;

    internal Button? ApproveButton => _approve;

    internal Button? DenyButton => _deny;

    internal CheckBox? ConfirmBox => _confirm;

    internal TextBox? ReasonBox => _reason;

    #region Public actions

    /// <summary>Approves the selected request (a high-risk one only once the extra check is ticked).</summary>
    public Task ApproveAsync() => DecideAsync(AgentApprovalDecision.Approved);

    /// <summary>Denies the selected request with the note in the field.</summary>
    public Task DenyAsync() => DecideAsync(AgentApprovalDecision.Denied);

    private async Task DecideAsync(AgentApprovalDecision decision)
    {
        if (_busy || SelectedRequest is not { } request || _decided.Contains(request))
        {
            return;
        }
        if (decision == AgentApprovalDecision.Approved && request.Risk == AgentApprovalRisk.High && _confirm?.IsChecked != true)
        {
            return;
        }
        var reason = string.IsNullOrWhiteSpace(_reason?.Text) ? null : _reason!.Text!.Trim();
        if (DecisionHandler is { } handler)
        {
            SetBusy(true);
            var sending = _sending = new CancellationTokenSource();
            try
            {
                await handler(request, decision, reason, sending.Token);
            }
            catch (OperationCanceledException)
            {
                SetBusy(false);
                return;
            }
            catch (Exception exception)
            {
                SetBusy(false);
                SetStatus(Labels.Failed(exception.Message), true);
                _announcer?.Announce(Labels.Failed(exception.Message));
                return;
            }
            SetBusy(false);
        }
        var index = IndexOfPending(request);
        _decided.Add(request);
        var pending = Pending;
        var next = pending.Count == 0 ? null : pending[Math.Clamp(index, 0, pending.Count - 1)];
        var done = Labels.Done(decision, request.Title);
        SetCurrentValue(SelectedRequestProperty, next);
        Rebuild();
        SetStatus(done, false);
        _announcer?.Announce(done);
        RaiseEvent(new AgentApprovalDecidedEventArgs(DecidedEvent, request, decision, reason));
        if (next is not null && RowOf(next) is { } row)
        {
            row.Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>Puts every decided request back in the queue (the samples use it to start over).</summary>
    public void Reset()
    {
        _decided.Clear();
        _sample = null;
        Rebuild();
        SetCurrentValue(SelectedRequestProperty, Pending.FirstOrDefault());
        SetStatus(null, false);
    }

    private int IndexOfPending(AgentApprovalRequest request)
    {
        var pending = Pending;
        for (var i = 0; i < pending.Count; i++)
        {
            if (ReferenceEquals(pending[i], request))
            {
                return i;
            }
        }
        return 0;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_approve is not null)
        {
            _approve.IsLoading = busy;
        }
        if (_deny is not null)
        {
            _deny.IsEnabled = !busy;
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_approve is not null)
        {
            _approve.Click -= OnApproveClick;
        }
        if (_deny is not null)
        {
            _deny.Click -= OnDenyClick;
        }
        if (_confirm is not null)
        {
            _confirm.IsCheckedChanged -= OnConfirmChanged;
        }
        _list?.Children.Clear();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _list = scope.Find<StackPanel>(ListPart);
        _approve = scope.Find<Button>(ApprovePart);
        _deny = scope.Find<Button>(DenyPart);
        _reason = scope.Find<TextBox>(ReasonPart);
        _confirm = scope.Find<CheckBox>(ConfirmPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        FindDetailParts(scope);
        if (_approve is not null)
        {
            _approve.Click += OnApproveClick;
        }
        if (_deny is not null)
        {
            _deny.Click += OnDenyClick;
        }
        if (_confirm is not null)
        {
            _confirm.IsCheckedChanged += OnConfirmChanged;
        }
        Rebuild();
        if (SelectedRequest is null)
        {
            SetCurrentValue(SelectedRequestProperty, Pending.FirstOrDefault());
        }
        ShowDetail();
    }

    private void OnApproveClick(object? sender, RoutedEventArgs e) => _ = ApproveAsync();

    private void OnDenyClick(object? sender, RoutedEventArgs e) => _ = DenyAsync();

    private void OnConfirmChanged(object? sender, RoutedEventArgs e) => UpdateButtons();

    private void Rebuild()
    {
        var pending = Pending;
        PendingCount = pending.Count;
        PseudoClasses.Set(EmptyPseudoClass, pending.Count == 0);
        if (_list is not null)
        {
            var keep = new HashSet<AgentApprovalRequest>(pending);
            foreach (var request in _rows.Keys.ToArray())
            {
                if (!keep.Contains(request))
                {
                    _rows.Remove(request);
                }
            }
            _list.Children.Clear();
            foreach (var request in pending)
            {
                if (!_rows.TryGetValue(request, out var row))
                {
                    row = new AgentApprovalsRow { Request = request, Owner = this };
                    _rows[request] = row;
                }
                row.Refresh();
                row.IsSelected = ReferenceEquals(request, SelectedRequest);
                _list.Children.Add(row);
            }
            AutomationProperties.SetName(_list, Labels.Title);
        }
        UpdateHeader();
    }

    /// <summary>Up and Down move the selection through the list (and the focus with it); Home and End jump to the ends.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not AgentApprovalsRow row || row.Request is not { } request)
        {
            return;
        }
        var pending = Pending;
        var index = IndexOfPending(request);
        var target = e.Key switch
        {
            Key.Down => index + 1,
            Key.Up => index - 1,
            Key.Home => 0,
            Key.End => pending.Count - 1,
            _ => -1
        };
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || target < 0 || target >= pending.Count)
        {
            return;
        }
        e.Handled = true;
        SetCurrentValue(SelectedRequestProperty, pending[target]);
        RowOf(pending[target])?.Focus(NavigationMethod.Directional);
    }

    internal void Select(AgentApprovalRequest request) => SetCurrentValue(SelectedRequestProperty, request);

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RequestsProperty)
        {
            _decided.Clear();
            Observe(change.GetNewValue<IReadOnlyList<AgentApprovalRequest>?>());
            Rebuild();
            SetCurrentValue(SelectedRequestProperty, Pending.FirstOrDefault());
        }
        else if (change.Property == SelectedRequestProperty)
        {
            _sending?.Cancel();
            foreach (var (request, row) in _rows)
            {
                row.IsSelected = ReferenceEquals(request, change.NewValue);
            }
            if (_reason is not null)
            {
                _reason.Text = string.Empty;
            }
            if (_confirm is not null)
            {
                _confirm.IsChecked = false;
            }
            SetStatus(null, false);
            ShowDetail();
        }
        else if (change.Property == LabelsProperty)
        {
            Rebuild();
            ShowDetail();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    private void Observe(IReadOnlyList<AgentApprovalRequest>? requests)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnRequestsChanged;
        }
        _observed = requests as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnRequestsChanged;
        }
    }

    private void OnRequestsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
        if (SelectedRequest is null || !Pending.Contains(SelectedRequest))
        {
            SetCurrentValue(SelectedRequestProperty, Pending.FirstOrDefault());
        }
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _sending?.Cancel();
    }

    /// <summary>The glyph of an action kind.</summary>
    internal static Geometry GlyphOf(AgentApprovalKind kind) => kind switch
    {
        AgentApprovalKind.Email => ArcIcons.Mail,
        AgentApprovalKind.Payment => ArcIcons.CreditCard,
        AgentApprovalKind.Deploy => ArcIcons.CloudUpload,
        AgentApprovalKind.DataChange => ArcIcons.Database,
        AgentApprovalKind.Access => ArcIcons.Key,
        _ => ArcIcons.Zap
    };

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentApprovalsAutomationPeer(this);

    private sealed class AgentApprovalsAutomationPeer(AgentApprovals owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((AgentApprovals)Owner).Labels.Title;
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(AddAreaPart, typeof(Control))]
[TemplatePart(AddHeadingPart, typeof(TextBlock))]
[TemplatePart(AddHintPart, typeof(TextBlock))]
[TemplatePart(DomainLabelPart, typeof(TextBlock))]
[TemplatePart(DomainInputPart, typeof(Input))]
[TemplatePart(AddButtonPart, typeof(Button))]
[TemplatePart(DomainAreaPart, typeof(Control))]
[TemplatePart(DomainIconPart, typeof(Border))]
[TemplatePart(DomainTextPart, typeof(TextBlock))]
[TemplatePart(DomainDetailPart, typeof(ArcContentSwap))]
[TemplatePart(DomainStatusPart, typeof(ArcContentSwap))]
[TemplatePart(RemoveButtonPart, typeof(Button))]
[TemplatePart(ColumnHeaderPart, typeof(Grid))]
[TemplatePart(RecordsPart, typeof(Panel))]
[TemplatePart(PlaceholdersPart, typeof(Panel))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(EmptyTextPart, typeof(TextBlock))]
[TemplatePart(FooterPart, typeof(Control))]
[TemplatePart(FooterStatusPart, typeof(ArcContentSwap))]
[TemplatePart(FooterActionPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(DeveloperAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, EnterPseudoClass, AddingPseudoClass, RecordsPseudoClass,
    CheckingPseudoClass, InvalidPseudoClass, VerifiedPseudoClass)]
public partial class DomainVerify
{
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string AddAreaPart = "PART_AddArea";
    internal const string AddHeadingPart = "PART_AddHeading";
    internal const string AddHintPart = "PART_AddHint";
    internal const string DomainLabelPart = "PART_DomainLabel";
    internal const string DomainInputPart = "PART_Domain";
    internal const string AddButtonPart = "PART_Add";
    internal const string DomainAreaPart = "PART_DomainArea";
    internal const string DomainIconPart = "PART_DomainIcon";
    internal const string DomainTextPart = "PART_DomainText";
    internal const string DomainDetailPart = "PART_DomainDetail";
    internal const string DomainStatusPart = "PART_DomainStatus";
    internal const string RemoveButtonPart = "PART_Remove";
    internal const string ColumnHeaderPart = "PART_ColumnHeader";
    internal const string RecordsPart = "PART_Records";
    internal const string PlaceholdersPart = "PART_Placeholders";
    internal const string EmptyPart = "PART_Empty";
    internal const string EmptyTextPart = "PART_EmptyText";
    internal const string FooterPart = "PART_Footer";
    internal const string FooterStatusPart = "PART_FooterStatus";
    internal const string FooterActionPart = "PART_FooterAction";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>The pseudo-class while the card is a bordered card.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>The pseudo-class below <see cref="CompactWidth"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Enter"/>.</summary>
    public const string EnterPseudoClass = ":enter";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Adding"/>.</summary>
    public const string AddingPseudoClass = ":adding";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Records"/>.</summary>
    public const string RecordsPseudoClass = ":records";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Checking"/>.</summary>
    public const string CheckingPseudoClass = ":checking";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Invalid"/>.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>The pseudo-class of <see cref="DomainVerifyPhase.Verified"/>.</summary>
    public const string VerifiedPseudoClass = ":verified";

    private TextBlock? _title;
    private TextBlock? _description;
    private Control? _addArea;
    private TextBlock? _addHeading;
    private TextBlock? _addHint;
    private TextBlock? _domainLabel;
    private Input? _input;
    private Button? _add;
    private Control? _domainArea;
    private Border? _domainIcon;
    private TextBlock? _domainText;
    private ArcContentSwap? _domainDetail;
    private ArcContentSwap? _domainStatus;
    private Button? _remove;
    private Grid? _columnHeader;
    private Panel? _recordsHost;
    private Panel? _placeholders;
    private Control? _empty;
    private TextBlock? _emptyText;
    private Control? _footer;
    private ArcContentSwap? _footerStatus;
    private Button? _footerAction;
    private DeveloperAnnouncer? _announcer;

    private readonly List<DomainRecord> _records = new();
    private readonly List<DomainVerifyRow> _rows = new();
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _autoPlay;
    private ViewportVisibility? _viewport;
    private DomainRegions _regions;
    private string? _footerError;
    private string? _detailKey;
    private string? _footerKey;
    private int _run;
    private bool _compact;
    private bool _inputInitialized;
    private bool _autoPlayStarted;
    private bool _interacted;
    private string _lastDefault = string.Empty;
    private string? _errorFor;

    /// <summary>Scales the delays of the simulated server and of the autoplay script; 0 removes them (tests).</summary>
    internal double SimulationScale { get; set; } = 1;

    internal IReadOnlyList<DomainVerifyRow> Rows => _rows;

    internal IReadOnlyList<DomainRecord> RecordList => _records;

    internal Input? DomainInput => _input;

    internal Button? AddButton => _add;

    internal Button? RemoveButton => _remove;

    internal Button? FooterAction => _footerAction;

    internal ArcContentSwap? FooterStatus => _footerStatus;

    internal ArcContentSwap? DomainStatus => _domainStatus;

    internal ArcContentSwap? DomainDetail => _domainDetail;

    internal Control? EmptyState => _empty;

    internal Grid? ColumnHeader => _columnHeader;

    internal DeveloperAnnouncer? Announcer => _announcer;

    /// <summary>True while the card is in its stacked layout (narrower than <see cref="CompactWidth"/>).</summary>
    public bool IsCompact => _compact;

    /// <summary>The footer text now (or the message of the last failure).</summary>
    public string? FooterText => _footerError ?? FooterTextFor(Phase);

    static DomainVerify()
    {
        FocusableProperty.OverrideDefaultValue<DomainVerify>(false);
    }

    /// <summary>Creates the card.</summary>
    public DomainVerify()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        PseudoClasses.Set(EnterPseudoClass, true);
        AddHandler(PointerPressedEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnUserInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    #region Public methods

    /// <summary>
    /// Adds a domain as the "Add domain" button does: <paramref name="domain"/> (or the text in the field when null) is
    /// reduced to a host name and checked, then <see cref="GetRecordsAction"/> is awaited and the records are listed.
    /// Returns true when the records are listed.
    /// </summary>
    public async Task<bool> AddDomainAsync(string? domain = null)
    {
        if (Phase != DomainVerifyPhase.Enter)
        {
            return false;
        }
        var host = DomainVerifySimulation.Normalize(domain ?? _input?.Text);
        if (!DomainVerifySimulation.IsValid(host))
        {
            SetInputError(Labels.DomainInvalid);
            Announce(Labels.DomainInvalid);
            return false;
        }
        SetInputError(null);
        if (_input is not null && domain is not null)
        {
            _input.Text = host;
        }
        _footerError = null;
        var cts = NewOperation();
        SetPhase(DomainVerifyPhase.Adding);
        try
        {
            var records = GetRecordsAction is { } get
                ? await get(host, cts.Token)
                : await SimulateRecordsAsync(host, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            if (records is null || records.Count == 0)
            {
                throw new InvalidOperationException(Labels.AddFailed);
            }
            _records.Clear();
            _records.AddRange(records);
            Domain = host;
            BuildRows();
            var focusInside = IsKeyboardFocusWithin;
            SetPhase(DomainVerifyPhase.Records);
            Announce(Labels.AddedAnnouncement(host));
            if (focusInside)
            {
                _footerAction?.Focus();
            }
            return true;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (ReferenceEquals(_operation, cts) && Phase == DomainVerifyPhase.Adding)
            {
                SetPhase(DomainVerifyPhase.Enter);
            }
            return false;
        }
        catch (Exception exception)
        {
            RaiseEvent(new DomainVerifyFailedEventArgs(FailedEvent, this, host, exception));
            var message = string.IsNullOrWhiteSpace(exception.Message) ? Labels.AddFailed : exception.Message;
            SetPhase(DomainVerifyPhase.Enter);
            SetInputError(message);
            Announce(message);
            return false;
        }
    }

    /// <summary>
    /// Runs a check as the footer button does: polls <see cref="VerifyAction"/> every <see cref="PollInterval"/> until every
    /// record is valid (then the certificate is issued) or one is conflicting. Returns when the run has ended; the phase
    /// tells how (<see cref="DomainVerifyPhase.Verified"/>, <see cref="DomainVerifyPhase.Invalid"/>, or back at
    /// <see cref="DomainVerifyPhase.Records"/> after a failure).
    /// </summary>
    public async Task CheckAsync()
    {
        if (Phase is not (DomainVerifyPhase.Records or DomainVerifyPhase.Invalid) || _domain is not { } host)
        {
            return;
        }
        var cts = NewOperation();
        var ct = cts.Token;
        var run = ++_run;
        _footerError = null;
        _regions = default;
        foreach (var row in _rows)
        {
            row.SetState(DomainRowState.Checking, null);
        }
        SetPhase(DomainVerifyPhase.Checking);
        Announce(Labels.CheckingAnnouncement);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                var context = new DomainVerifyContext(attempt, run);
                DomainVerifyResult result;
                if (VerifyAction is { } verify)
                {
                    result = await verify(host, _records, context, ct);
                }
                else
                {
                    await Delay(TimeSpan.FromMilliseconds(600), ct);
                    result = DomainVerifySimulation.Check(_records, context);
                }
                ct.ThrowIfCancellationRequested();

                var invalid = Apply(result);
                if (invalid is not null)
                {
                    SetPhase(DomainVerifyPhase.Invalid);
                    Announce(Labels.NeedsFixAnnouncement(invalid.Record.Type));
                    return;
                }
                if (_rows.All(r => r.State == DomainRowState.Valid))
                {
                    await IssueAsync(host, cts);
                    return;
                }
                await (PollInterval is null && VerifyAction is null ? SimulatedDelay(800, ct) : Delay(EffectivePollInterval, ct));
                foreach (var row in _rows.Where(r => r.State == DomainRowState.NotFound))
                {
                    row.SetState(DomainRowState.Checking, null);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (ReferenceEquals(_operation, cts) && Phase == DomainVerifyPhase.Checking)
            {
                ResetRows();
                SetPhase(DomainVerifyPhase.Records);
            }
        }
        catch (Exception exception)
        {
            Fail(host, exception);
        }
    }

    /// <summary>
    /// Removes the domain as the close button and "Add another" do: cancels any running step, clears the records, raises
    /// <see cref="Removed"/> and returns to the empty field.
    /// </summary>
    public void Remove()
    {
        var old = _domain;
        var focusInside = IsKeyboardFocusWithin;
        _operation?.Cancel();
        _operation = null;
        _records.Clear();
        ClearRows();
        _footerError = null;
        _regions = default;
        Domain = null;
        Certificate = null;
        SetPhase(DomainVerifyPhase.Enter);
        if (_input is not null)
        {
            _input.Text = string.Empty;
            SetInputError(null);
        }
        if (focusInside)
        {
            _input?.Focus();
        }
        Announce(Labels.RemovedAnnouncement);
        if (old is not null)
        {
            var args = new DomainRemovedEventArgs(RemovedEvent, this, old);
            RaiseEvent(args);
            if (RemovedCommand is { } command && command.CanExecute(args))
            {
                command.Execute(args);
            }
        }
    }

    /// <summary>Stops the simulated flow, as a click in the card does; whatever step is running finishes.</summary>
    public void StopAutoPlay()
    {
        _interacted = true;
        _autoPlay?.Cancel();
    }

    #endregion

    #region Template and lifetime

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _input.TextChanged -= OnInputTextChanged;
        }
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_footerAction is not null)
        {
            _footerAction.Click -= OnFooterActionClick;
        }

        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _addArea = e.NameScope.Find<Control>(AddAreaPart);
        _addHeading = e.NameScope.Find<TextBlock>(AddHeadingPart);
        _addHint = e.NameScope.Find<TextBlock>(AddHintPart);
        _domainLabel = e.NameScope.Find<TextBlock>(DomainLabelPart);
        _input = e.NameScope.Find<Input>(DomainInputPart);
        _add = e.NameScope.Find<Button>(AddButtonPart);
        _domainArea = e.NameScope.Find<Control>(DomainAreaPart);
        _domainIcon = e.NameScope.Find<Border>(DomainIconPart);
        _domainText = e.NameScope.Find<TextBlock>(DomainTextPart);
        _domainDetail = e.NameScope.Find<ArcContentSwap>(DomainDetailPart);
        _domainStatus = e.NameScope.Find<ArcContentSwap>(DomainStatusPart);
        _remove = e.NameScope.Find<Button>(RemoveButtonPart);
        _columnHeader = e.NameScope.Find<Grid>(ColumnHeaderPart);
        _recordsHost = e.NameScope.Find<Panel>(RecordsPart);
        _placeholders = e.NameScope.Find<Panel>(PlaceholdersPart);
        _empty = e.NameScope.Find<Control>(EmptyPart);
        _emptyText = e.NameScope.Find<TextBlock>(EmptyTextPart);
        _footer = e.NameScope.Find<Control>(FooterPart);
        _footerStatus = e.NameScope.Find<ArcContentSwap>(FooterStatusPart);
        _footerAction = e.NameScope.Find<Button>(FooterActionPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        _detailKey = null;
        _footerKey = null;

        if (_input is not null)
        {
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Bubble);
            _input.TextChanged += OnInputTextChanged;
            if (!_inputInitialized)
            {
                _inputInitialized = true;
                _lastDefault = DefaultDomain ?? string.Empty;
                _input.Text = _lastDefault;
            }
        }
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
            _remove.Content = DomainVerifyUi.Glyph(ArcIcons.X, 16, ArcDesignTokenKind.TextSecondary);
        }
        if (_footerAction is not null)
        {
            _footerAction.Click += OnFooterActionClick;
        }
        if (_domainIcon is not null)
        {
            _domainIcon.Child = DomainVerifyUi.Glyph(ArcIcons.Globe, 16, ArcDesignTokenKind.TextSecondary);
        }
        if (_recordsHost is not null)
        {
            AutomationProperties.SetControlTypeOverride(_recordsHost, AutomationControlType.List);
        }
        ApplyLabels();
        RebuildPlaceholders();
        RebuildRows();
        ApplyPhase();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ArmAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewport?.Dispose();
        _viewport = null;
        _autoPlay?.Cancel();
        _autoPlayStarted = false;
        _operation?.Cancel();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateCompact(change.GetNewValue<Rect>().Width);
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            ApplyLabels();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            RebuildPlaceholders();
            RebuildRows();
            ApplyPhase();
        }
        else if (change.Property == ProviderProperty)
        {
            UpdateDomainDetail();
        }
        else if (change.Property == DefaultDomainProperty)
        {
            // An untouched field follows the default; a field the person typed in keeps its text.
            if (_input is not null && Phase == DomainVerifyPhase.Enter && (_input.Text ?? string.Empty) == _lastDefault)
            {
                _lastDefault = change.GetNewValue<string?>() ?? string.Empty;
                _input.Text = _lastDefault;
            }
        }
        else if (change.Property == RowPaddingProperty || change.Property == CompactRowPaddingProperty)
        {
            ApplyRowPadding();
        }
    }

    #endregion

    #region Flow

    private TimeSpan EffectivePollInterval =>
        PollInterval ?? (VerifyAction is null ? TimeSpan.FromMilliseconds(800) : TimeSpan.FromSeconds(5));

    private bool IsSimulated => VerifyAction is null;

    private CancellationTokenSource NewOperation()
    {
        _operation?.Cancel();
        _operation = new CancellationTokenSource();
        return _operation;
    }

    // A configured PollInterval is real time; the simulated steps below scale with SimulationScale.
    private static Task Delay(TimeSpan time, CancellationToken ct) =>
        time <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(time, ct);

    private Task SimulatedDelay(double milliseconds, CancellationToken ct) =>
        SimulationScale <= 0 ? Task.CompletedTask : Task.Delay(TimeSpan.FromMilliseconds(milliseconds * SimulationScale), ct);

    private async Task<IReadOnlyList<DomainRecord>> SimulateRecordsAsync(string host, CancellationToken ct)
    {
        await SimulatedDelay(700, ct);
        return DomainVerifySimulation.DefaultRecords(host);
    }

    /// <summary>Writes a result into the rows and the footer; returns the first conflicting row, if any.</summary>
    private DomainVerifyRow? Apply(DomainVerifyResult result)
    {
        _regions = result.Regions;
        DomainVerifyRow? invalid = null;
        foreach (var row in _rows)
        {
            var check = result.Records.FirstOrDefault(c => c.Id == row.Record.Id);
            switch (check?.Status)
            {
                case DomainRecordStatus.Valid:
                    row.SetState(DomainRowState.Valid, check);
                    break;
                case DomainRecordStatus.Invalid:
                    row.SetState(DomainRowState.Invalid, check);
                    invalid ??= row;
                    break;
                default:
                    row.SetState(DomainRowState.NotFound, check);
                    break;
            }
        }
        UpdateFooter();
        return invalid;
    }

    private async Task IssueAsync(string host, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        SetPhase(DomainVerifyPhase.Verified);
        Announce(Labels.VerifiedAnnouncement(host));
        DomainCertificate certificate;
        try
        {
            if (IssueCertificateAction is { } issue)
            {
                certificate = await issue(host, ct);
            }
            else
            {
                await SimulatedDelay(1400, ct);
                certificate = new DomainCertificate("Let's Encrypt", "in 60 days");
            }
            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (ReferenceEquals(_operation, cts) && Phase == DomainVerifyPhase.Verified && Certificate is null)
            {
                SetPhase(DomainVerifyPhase.Records);
            }
            return;
        }
        catch (Exception exception)
        {
            Fail(host, exception);
            return;
        }
        Certificate = certificate;
        UpdateDomainDetail();
        Announce(Labels.CertificateAnnouncement(certificate.Issuer));
        var args = new DomainVerifiedEventArgs(VerifiedEvent, this, host, certificate);
        RaiseEvent(args);
        if (VerifiedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private void Fail(string host, Exception exception)
    {
        RaiseEvent(new DomainVerifyFailedEventArgs(FailedEvent, this, host, exception));
        var message = string.IsNullOrWhiteSpace(exception.Message) ? Labels.CheckFailed : exception.Message;
        _footerError = message;
        Certificate = null;
        // Rows that were still being checked go back to "not checked"; the ones that came back valid stay valid.
        foreach (var row in _rows.Where(r => r.State is DomainRowState.Checking or DomainRowState.NotFound))
        {
            row.SetState(DomainRowState.NotChecked, null);
        }
        SetPhase(DomainVerifyPhase.Records);
        Announce(Labels.FailedAnnouncement(message));
    }

    private void SetPhase(DomainVerifyPhase phase)
    {
        var old = Phase;
        if (old == phase)
        {
            ApplyPhase();
            return;
        }
        Phase = phase;
        ApplyPhase();
        RaiseEvent(new DomainVerifyPhaseChangedEventArgs(PhaseChangedEvent, this, old, phase));
    }

    private void Announce(string text) =>
        _announcer?.Announce(IsSimulated ? $"{text}. {Labels.SimulatedNote}" : text);

    #endregion

    #region Autoplay

    private bool IsAutoPlayEnabled =>
        AutoPlay ?? (GetRecordsAction is null && VerifyAction is null && IssueCertificateAction is null);

    private void ArmAutoPlay()
    {
        if (_autoPlayStarted || _interacted || !IsAutoPlayEnabled || Phase != DomainVerifyPhase.Enter)
        {
            return;
        }
        _viewport?.Dispose();
        _viewport = new ViewportVisibility(this, 0.5, once: true);
        _viewport.VisibilityChanged += (_, visible) =>
        {
            if (visible)
            {
                _ = RunAutoPlayAsync();
            }
        };
    }

    /// <summary>
    /// Plays add, check, fix and verify once. Each step waits a moment so that it can be read; a click or key press in the
    /// card stops the script between steps and leaves the card where it is.
    /// </summary>
    internal async Task RunAutoPlayAsync()
    {
        if (_autoPlayStarted || _interacted || !IsAutoPlayEnabled)
        {
            return;
        }
        _autoPlayStarted = true;
        _autoPlay?.Dispose();
        _autoPlay = new CancellationTokenSource();
        var ct = _autoPlay.Token;
        try
        {
            await SimulatedDelay(1000, ct);
            if (Phase == DomainVerifyPhase.Enter)
            {
                if (string.IsNullOrWhiteSpace(_input?.Text))
                {
                    _input!.Text = "app.acme.com";
                }
                await AddDomainAsync();
            }
            await SimulatedDelay(900, ct);
            if (Phase == DomainVerifyPhase.Records)
            {
                await CheckAsync();
            }
            await SimulatedDelay(1600, ct);
            if (Phase == DomainVerifyPhase.Invalid)
            {
                await CheckAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // The person took over.
        }
    }

    private void OnUserInput(object? sender, RoutedEventArgs e)
    {
        if (!_interacted)
        {
            StopAutoPlay();
        }
    }

    #endregion

    #region Parts

    private void OnAddClick(object? sender, RoutedEventArgs e) => _ = AddDomainAsync();

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Remove();

    private void OnFooterActionClick(object? sender, RoutedEventArgs e)
    {
        if (Phase == DomainVerifyPhase.Verified)
        {
            Remove();
        }
        else
        {
            _ = CheckAsync();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && Phase == DomainVerifyPhase.Enter)
        {
            e.Handled = true;
            _ = AddDomainAsync();
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Only an edit clears the error: setting the error does not change the text, but a TextChanged that was queued for an
        // earlier assignment can still arrive after it.
        if (_input is not null && !string.IsNullOrEmpty(_input.ErrorMessage) && _input.Text != _errorFor)
        {
            SetInputError(null);
        }
    }

    private void SetInputError(string? message)
    {
        if (_input is not null)
        {
            _errorFor = message is null ? null : _input.Text;
            _input.ErrorMessage = message;
        }
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        // One pixel of hysteresis at the threshold so that a width that sits on it does not flip the layout.
        var compact = _compact ? width < CompactWidth + 1 : width < CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var row in _rows)
        {
            row.SetCompact(compact);
        }
        ApplyRowPadding();
        _detailKey = null;
        UpdateDomainDetail();
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(_title, Title);
        SetText(_description, Description);
        SetText(_addHeading, labels.AddHeading);
        SetText(_addHint, labels.AddHint);
        SetText(_domainLabel, labels.DomainLabel);
        SetText(_emptyText, labels.EmptyState);
        if (_input is not null)
        {
            _input.Placeholder = labels.DomainPlaceholder;
            AutomationProperties.SetName(_input, labels.DomainLabel);
        }
        if (_recordsHost is not null)
        {
            AutomationProperties.SetName(_recordsHost, labels.ListName);
        }
        if (_columnHeader is not null)
        {
            var texts = new[] { labels.TypeHeader, labels.NameHeader, labels.ValueHeader, labels.StatusHeader };
            for (var i = 0; i < _columnHeader.Children.Count && i < texts.Length; i++)
            {
                if (_columnHeader.Children[i] is TextBlock block)
                {
                    block.Text = texts[i];
                }
            }
        }
        _footerKey = null;
        _detailKey = null;
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
            block.IsVisible = !string.IsNullOrEmpty(text);
        }
    }

    private void ApplyRowPadding()
    {
        var padding = _compact ? CompactRowPadding : RowPadding;
        foreach (var row in _rows)
        {
            row.Padding = padding;
        }
        RebuildPlaceholders();
    }

    private void RebuildPlaceholders()
    {
        if (_placeholders is null)
        {
            return;
        }
        _placeholders.Children.Clear();
        var padding = _compact ? CompactRowPadding : RowPadding;
        foreach (var width in new[] { 64.0, 88.0, 76.0 })
        {
            _placeholders.Children.Add(DomainVerifyRow.CreatePlaceholder(width, padding));
        }
    }

    private void RebuildRows()
    {
        if (_recordsHost is null)
        {
            return;
        }
        var states = _rows.ToDictionary(r => r.Record.Id, r => r.State);
        ClearRows();
        if (_records.Count > 0)
        {
            BuildRows(animate: false, states);
        }
    }

    private void BuildRows(bool animate = true, IReadOnlyDictionary<string, DomainRowState>? states = null)
    {
        if (_recordsHost is null)
        {
            return;
        }
        var level = animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var padding = _compact ? CompactRowPadding : RowPadding;
        for (var i = 0; i < _records.Count; i++)
        {
            var row = new DomainVerifyRow(_records[i], Labels) { Padding = padding };
            row.SetCompact(_compact);
            if (states is not null && states.TryGetValue(_records[i].Id, out var state))
            {
                row.SetState(state, null, announce: false);
            }
            if (i > 0)
            {
                ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
                ArcTokens.Bind(row, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
            }
            _rows.Add(row);
            _recordsHost.Children.Add(row);
            row.PlayEntrance(level, i);
        }
    }

    private void ClearRows()
    {
        _rows.Clear();
        _recordsHost?.Children.Clear();
    }

    private void ResetRows()
    {
        foreach (var row in _rows)
        {
            row.SetState(DomainRowState.NotChecked, null);
        }
    }

    private void ApplyPhase()
    {
        var phase = Phase;
        PseudoClasses.Set(EnterPseudoClass, phase == DomainVerifyPhase.Enter);
        PseudoClasses.Set(AddingPseudoClass, phase == DomainVerifyPhase.Adding);
        PseudoClasses.Set(RecordsPseudoClass, phase == DomainVerifyPhase.Records);
        PseudoClasses.Set(CheckingPseudoClass, phase == DomainVerifyPhase.Checking);
        PseudoClasses.Set(InvalidPseudoClass, phase == DomainVerifyPhase.Invalid);
        PseudoClasses.Set(VerifiedPseudoClass, phase == DomainVerifyPhase.Verified);

        var entering = phase is DomainVerifyPhase.Enter or DomainVerifyPhase.Adding;
        if (_addArea is not null)
        {
            _addArea.IsVisible = entering;
        }
        if (_domainArea is not null)
        {
            _domainArea.IsVisible = !entering;
        }
        if (_add is not null)
        {
            _add.IsLoading = phase == DomainVerifyPhase.Adding;
            _add.Content = phase == DomainVerifyPhase.Adding ? Labels.Adding : Labels.AddDomain;
            AutomationProperties.SetName(_add, (string)_add.Content);
        }
        if (_input is not null)
        {
            _input.IsReadOnly = phase == DomainVerifyPhase.Adding;
        }
        if (_placeholders is not null)
        {
            _placeholders.IsVisible = entering;
        }
        if (_empty is not null)
        {
            _empty.IsVisible = phase == DomainVerifyPhase.Enter;
        }
        if (_recordsHost is not null)
        {
            _recordsHost.IsVisible = !entering;
        }
        if (_remove is not null)
        {
            _remove.IsVisible = !entering;
            AutomationProperties.SetName(_remove, Labels.RemoveName(_domain ?? string.Empty));
        }
        if (_footer is not null)
        {
            _footer.IsVisible = !entering;
        }
        if (_domainText is not null)
        {
            _domainText.Text = _domain;
        }
        UpdateDomainStatus();
        UpdateDomainDetail();
        UpdateFooter();
    }

    private string? FooterTextFor(DomainVerifyPhase phase) => phase switch
    {
        DomainVerifyPhase.Records => Labels.FooterRecords,
        DomainVerifyPhase.Checking => Labels.FooterChecking(_regions.Propagated, _regions.Total),
        DomainVerifyPhase.Invalid => Labels.FooterInvalid,
        DomainVerifyPhase.Verified => Labels.FooterVerified,
        _ => null,
    };

    private void UpdateFooter()
    {
        if (_footerStatus is null || _footerAction is null)
        {
            return;
        }
        var phase = Phase;
        var text = FooterText;
        var key = $"{phase}|{text}|{_footerError is not null}";
        if (key != _footerKey)
        {
            _footerKey = key;
            if (_footerError is not null)
            {
                _footerStatus.Content = DomainVerifyUi.Text(text, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Danger, lineHeight: 19.6);
            }
            else if (phase == DomainVerifyPhase.Checking)
            {
                var shimmer = new TextShimmer { Text = text, IsActive = true, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(shimmer, TextShimmer.FontSizeProperty, ArcDesignTokenKind.TypeSM);
                ArcTokens.Bind(shimmer, TextShimmer.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
                ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                _footerStatus.Content = shimmer;
            }
            else
            {
                _footerStatus.Content = text is null
                    ? null
                    : DomainVerifyUi.Text(text, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
            }
        }
        var action = phase switch
        {
            DomainVerifyPhase.Invalid => Labels.CheckAgain,
            DomainVerifyPhase.Verified => Labels.AddAnother,
            DomainVerifyPhase.Checking => Labels.Checking,
            _ => Labels.CheckRecords,
        };
        _footerAction.Content = action;
        _footerAction.IsLoading = phase == DomainVerifyPhase.Checking;
        _footerAction.Variant = phase == DomainVerifyPhase.Verified ? ButtonVariant.Secondary : ButtonVariant.Primary;
        AutomationProperties.SetName(_footerAction, action);
    }

    private void UpdateDomainStatus()
    {
        if (_domainStatus is null)
        {
            return;
        }
        var labels = Labels;
        Control? content = Phase switch
        {
            DomainVerifyPhase.Records => DomainVerifyUi.IconLabel(
                DomainVerifyUi.Glyph(ArcIcons.Clock, 14, ArcDesignTokenKind.TextMuted), labels.DomainWaiting, ArcDesignTokenKind.TextMuted),
            DomainVerifyPhase.Checking => DomainVerifyUi.IconLabel(
                DomainVerifyUi.Spinner(), labels.DomainChecking, ArcDesignTokenKind.TextSecondary),
            DomainVerifyPhase.Invalid => DomainVerifyUi.IconLabel(
                DomainVerifyUi.Glyph(ArcIcons.TriangleAlert, 14, ArcDesignTokenKind.Danger), labels.DomainNeedsFix, ArcDesignTokenKind.Danger),
            DomainVerifyPhase.Verified => DomainVerifyUi.IconLabel(
                DomainVerifyUi.Glyph(ArcIcons.CircleCheck, 14, ArcDesignTokenKind.Success), labels.DomainVerified, ArcDesignTokenKind.Success),
            _ => null,
        };
        _domainStatus.Content = content;
    }

    /// <summary>The domain row's second line: the DNS host and, once verified, the certificate state.</summary>
    private void UpdateDomainDetail()
    {
        if (_domainDetail is null)
        {
            return;
        }
        var labels = Labels;
        var provider = Provider;
        var certificate = Certificate;
        var verified = Phase == DomainVerifyPhase.Verified;
        var key = $"{provider?.Name}|{verified}|{certificate?.Issuer}|{_compact}|{_domain}";
        if (key == _detailKey)
        {
            return;
        }
        _detailKey = key;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (provider is not null)
        {
            if (provider.Mark is not null)
            {
                row.Children.Add(DomainVerifyUi.Glyph(provider.Mark, 14, ArcDesignTokenKind.TextMuted));
            }
            row.Children.Add(DomainVerifyUi.Text(_compact ? labels.ProviderLineShort(provider.Name) : labels.ProviderLine(provider.Name),
                ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
        }
        if (verified)
        {
            if (provider is not null)
            {
                row.Children.Add(DomainVerifyUi.Text("·", ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
            }
            if (certificate is null)
            {
                var shimmer = new TextShimmer { Text = labels.IssuingCertificate, IsActive = true, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(shimmer, TextShimmer.FontSizeProperty, ArcDesignTokenKind.TypeXS);
                ArcTokens.Bind(shimmer, TextShimmer.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
                ArcTokens.Bind(shimmer, TextShimmer.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                row.Children.Add(shimmer);
            }
            else
            {
                var text = _compact
                    ? labels.CertificateIssuedShort(certificate.Issuer)
                    : labels.CertificateIssued(certificate.Issuer, certificate.Renews);
                row.Children.Add(DomainVerifyUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8));
            }
        }
        _domainDetail.Content = row.Children.Count == 0 ? null : row;
    }

    #endregion
}

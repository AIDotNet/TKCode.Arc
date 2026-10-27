using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(CreatePart, typeof(DeveloperMorphPanel))]
[TemplatePart(CountPart, typeof(ArcContentSwap))]
[TemplatePart(CountWordsPart, typeof(TextBlock))]
[TemplatePart(CountGroupPart, typeof(Control))]
[TemplatePart(ListPart, typeof(DeveloperListPanel))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(EmptyTitlePart, typeof(TextBlock))]
[TemplatePart(EmptyDescriptionPart, typeof(TextBlock))]
[TemplatePart(EmptyActionPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(DeveloperAnnouncer))]
[PseudoClasses(FramedPseudoClass, NarrowPseudoClass, CompactPseudoClass, EmptyPseudoClass, FormPseudoClass,
    GeneratingPseudoClass, RevealPseudoClass)]
public partial class ApiKeys
{
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string CreatePart = "PART_Create";
    internal const string CountPart = "PART_Count";
    internal const string CountWordsPart = "PART_CountWords";
    internal const string CountGroupPart = "PART_CountGroup";
    internal const string ListPart = "PART_Keys";
    internal const string EmptyPart = "PART_Empty";
    internal const string EmptyTitlePart = "PART_EmptyTitle";
    internal const string EmptyDescriptionPart = "PART_EmptyDescription";
    internal const string EmptyActionPart = "PART_EmptyAction";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is narrower than 700: rows drop the scopes column and the creation date.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is narrower than 540: tighter side paddings, stacked details.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while no key is listed.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while the creation form is open.</summary>
    public const string FormPseudoClass = ":form";

    /// <summary>Set while a key is being generated.</summary>
    public const string GeneratingPseudoClass = ":generating";

    /// <summary>Set while the one-time secret is shown.</summary>
    public const string RevealPseudoClass = ":reveal";

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ApiKeys, double>(nameof(NarrowWidth), 700);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ApiKeys, double>(nameof(CompactWidth), 540);

    internal static readonly StyledProperty<double> StackedFormWidthProperty =
        AvaloniaProperty.Register<ApiKeys, double>(nameof(StackedFormWidth), 560);

    internal static readonly StyledProperty<double> DatesMinWidthProperty =
        AvaloniaProperty.Register<ApiKeys, double>(nameof(DatesMinWidth), 128);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double StackedFormWidth { get => GetValue(StackedFormWidthProperty); set => SetValue(StackedFormWidthProperty, value); }

    internal double DatesMinWidth { get => GetValue(DatesMinWidthProperty); set => SetValue(DatesMinWidthProperty, value); }

    #endregion

    private TextBlock? _title;
    private TextBlock? _description;
    private DeveloperMorphPanel? _create;
    private ArcContentSwap? _count;
    private TextBlock? _countWords;
    private Control? _countGroup;
    private DeveloperListPanel? _list;
    private Control? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyDescription;
    private Button? _emptyAction;
    private DeveloperAnnouncer? _announcer;
    private Control? _bodyHost;

    private ApiKeyForm _form;
    private ApiKeyReveal _reveal;
    private readonly Dictionary<ApiKeyItem, ApiKeyRow> _rows = new();
    private readonly HashSet<ApiKeyItem> _gone = new();
    private readonly Dictionary<ApiKeyItem, CancellationTokenSource> _revoking = new();
    private List<ApiKeyItem> _shown = new();
    private INotifyCollectionChanged? _observedKeys;
    private CancellationTokenSource? _generating;
    private ApiKeyItem? _fresh;
    private ApiKeysCloseReason? _closeReason;
    private bool _keyboard;
    private bool _narrow;
    private bool _compact;
    private bool _syncingOpen;
    private int _attempt;
    private int _shownCount = -1;

    static ApiKeys()
    {
        FocusableProperty.OverrideDefaultValue<ApiKeys>(false);
    }

    public ApiKeys()
    {
        Keys.CollectionChanged += (_, _) =>
        {
            if (KeysSource is null)
            {
                SyncKeys();
            }
        };
        PseudoClasses.Set(FramedPseudoClass, true);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        _form = CreateForm();
        _reveal = CreateReveal();
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The keys the block lists now, in order (from <see cref="KeysSource"/> or <see cref="Keys"/>).</summary>
    public IReadOnlyList<ApiKeyItem> EffectiveKeys => _shown;

    /// <summary>The scopes the form offers now (from <see cref="ScopesSource"/> or <see cref="Scopes"/>).</summary>
    public IReadOnlyList<ApiKeyScope> EffectiveScopes =>
        (ScopesSource ?? Scopes).OfType<ApiKeyScope>().ToList();

    internal ApiKeyForm Form => _form;

    internal ApiKeyReveal Reveal => _reveal;

    internal DeveloperMorphPanel? CreatePanel => _create;

    internal DeveloperListPanel? List => _list;

    internal DeveloperAnnouncer? Announcer => _announcer;

    internal Control? EmptyState => _empty;

    internal Button? EmptyActionButton => _emptyAction;

    internal ArcContentSwap? CountSwap => _count;

    internal bool LastInputWasKeyboard => _keyboard;

    internal ApiKeyRow? RowFor(ApiKeyItem item) => _rows.TryGetValue(item, out var row) ? row : null;

    /// <summary>"Now" for the dates (<see cref="ReferenceTime"/> or the clock).</summary>
    internal DateTimeOffset Now => ReferenceTime ?? DateTimeOffset.Now;

    internal bool IsStale(ApiKeyItem item, DateTimeOffset now) =>
        item.LastUsedAt is { } used && now - used > StaleAfter;

    #region Public methods

    /// <summary>Opens the "Create key" form (a fresh one).</summary>
    public void OpenCreate() => SetCurrentValue(IsCreateOpenProperty, true);

    /// <summary>Closes the panel; a running generate is cancelled and a shown secret is dropped.</summary>
    public void CloseCreate() => CloseCreateCore(ApiKeysCloseReason.Programmatic);

    /// <summary>Opens the details of <paramref name="item"/> (closing any other), or closes every row when null.</summary>
    public void Expand(ApiKeyItem? item) => SetCurrentValue(ExpandedKeyProperty, item);

    /// <summary>Cancels a running generate; the form stays open.</summary>
    public void CancelGenerate() => _generating?.Cancel();

    /// <summary>
    /// Submits the open form as Generate does: checks it, then awaits <see cref="GenerateAction"/>. Returns true when a
    /// key was created.
    /// </summary>
    public Task<bool> GenerateAsync() => SubmitAsync();

    /// <summary>
    /// Revokes <paramref name="item"/> as a confirmed hold does (without the hold): raises <see cref="KeyRevoking"/>,
    /// strikes the row, awaits <see cref="RevokeAction"/> and folds the row away. Returns true when it was revoked.
    /// </summary>
    public Task<bool> RevokeAsync(ApiKeyItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RevokeCoreAsync(item, RowFor(item));
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_create is not null)
        {
            _create.Opened -= OnCreateOpened;
            _create.Closed -= OnCreateClosed;
            _create.SizeChanged -= OnCreateSizeChanged;
            _create.Content = null;
        }
        if (_list is not null)
        {
            _list.ItemExited -= OnRowExited;
            _list.ItemPrepared -= OnRowPrepared;
            _list.Items = null;
            _list.ItemTemplate = null;
        }
        if (_emptyAction is not null)
        {
            _emptyAction.Click -= OnEmptyAction;
        }
        DetachBodyHost();

        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _create = e.NameScope.Find<DeveloperMorphPanel>(CreatePart);
        _count = e.NameScope.Find<ArcContentSwap>(CountPart);
        _countWords = e.NameScope.Find<TextBlock>(CountWordsPart);
        _countGroup = e.NameScope.Find<Control>(CountGroupPart);
        _list = e.NameScope.Find<DeveloperListPanel>(ListPart);
        _empty = e.NameScope.Find<Control>(EmptyPart);
        _emptyTitle = e.NameScope.Find<TextBlock>(EmptyTitlePart);
        _emptyDescription = e.NameScope.Find<TextBlock>(EmptyDescriptionPart);
        _emptyAction = e.NameScope.Find<Button>(EmptyActionPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        _rows.Clear();
        _shownCount = -1;

        if (_create is not null)
        {
            _create.Opened += OnCreateOpened;
            _create.Closed += OnCreateClosed;
            _create.SizeChanged += OnCreateSizeChanged;
            if (_form.Parent is not null || _form.GetVisualParent() is not null)
            {
                ReplaceForm();
            }
            _create.Content = _form;
            _create.Title = Labels.CreateKey;
        }
        if (_list is not null)
        {
            _list.Motion = DeveloperListMotion.Insert;
            _list.FlashInset = default;
            _list.FlashCornerRadius = default;
            _list.ItemPrepared += OnRowPrepared;
            _list.ItemExited += OnRowExited;
            _list.ItemTemplate = new FuncDataTemplate<ApiKeyItem>((item, _) => item is null ? null : CreateRow(item));
            AutomationProperties.SetAccessibilityView(_list, AccessibilityView.Content);
        }
        if (_emptyAction is not null)
        {
            _emptyAction.Click += OnEmptyAction;
        }
        PanelState = ApiKeysPanelState.Closed;
        ApplyTexts();
        SyncKeys();
        ApplyBreakpoints(Bounds.Width);
        if (IsCreateOpen)
        {
            _create?.Open();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveKeys(KeysSource);
        SyncKeys();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ObserveKeys(null);
        _generating?.Cancel();
        foreach (var cts in _revoking.Values.ToList())
        {
            cts.Cancel();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeysSourceProperty)
        {
            ObserveKeys(change.GetNewValue<IEnumerable?>());
            SyncKeys();
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == LabelsProperty || change.Property == TitleProperty
                 || change.Property == DescriptionProperty || change.Property == NameMaxLengthProperty
                 || change.Property == EmptyActionTextProperty)
        {
            ApplyTexts();
            RefreshRows();
        }
        else if (change.Property == ReferenceTimeProperty || change.Property == StaleAfterProperty
                 || change.Property == RevokeHoldDurationProperty)
        {
            RefreshRows();
        }
        else if (change.Property == ExpandedKeyProperty)
        {
            OnExpandedChanged(change.GetOldValue<ApiKeyItem?>(), change.GetNewValue<ApiKeyItem?>());
        }
        else if (change.Property == IsCreateOpenProperty)
        {
            if (_syncingOpen || _create is null)
            {
                return;
            }
            if (change.GetNewValue<bool>())
            {
                _create.Open();
            }
            else
            {
                CloseCreateCore(ApiKeysCloseReason.Programmatic);
            }
        }
        else if (change.Property == PanelStateProperty)
        {
            var state = change.GetNewValue<ApiKeysPanelState>();
            PseudoClasses.Set(FormPseudoClass, state == ApiKeysPanelState.Form);
            PseudoClasses.Set(GeneratingPseudoClass, state == ApiKeysPanelState.Generating);
            PseudoClasses.Set(RevealPseudoClass, state == ApiKeysPanelState.Reveal);
        }
        else if (change.Property == BoundsProperty)
        {
            ApplyBreakpoints(change.GetNewValue<Rect>().Width);
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        _keyboard = true;
        // Escape while generating gives up the request and stays in the form (the panel's own Escape would close it).
        if (e.Key == Key.Escape && !e.Handled && PanelState == ApiKeysPanelState.Generating)
        {
            _generating?.Cancel();
            e.Handled = true;
        }
    }

    #region Texts and breakpoints

    private void ApplyTexts()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        if (_create is not null)
        {
            _create.OpenName = labels.CreateKey;
            _create.CloseName = labels.Close;
            if (PanelState != ApiKeysPanelState.Reveal)
            {
                _create.Title = labels.CreateKey;
            }
            else
            {
                _create.Title = labels.KeyCreated;
            }
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.ListName);
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = labels.EmptyTitle;
        }
        if (_emptyDescription is not null)
        {
            _emptyDescription.Text = labels.EmptyDescription;
        }
        if (_emptyAction is not null)
        {
            _emptyAction.Content = EmptyActionText;
            _emptyAction.IsVisible = !string.IsNullOrEmpty(EmptyActionText);
        }
        _form.Configure(labels, NameMaxLength);
        _reveal.Configure(labels);
        UpdateCount(animate: false);
    }

    private void ApplyBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        // One pixel of hysteresis so a width sitting on the threshold does not flip back and forth.
        var narrow = _narrow ? width < NarrowWidth + 1 : width < NarrowWidth;
        var compact = _compact ? width < CompactWidth + 1 : width < CompactWidth;
        if (narrow == _narrow && compact == _compact)
        {
            return;
        }
        _narrow = narrow;
        _compact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var row in _rows.Values)
        {
            row.SetLayout(narrow, compact);
        }
    }

    private void OnCreateSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateStacked();

    private void UpdateStacked()
    {
        if (_create is null)
        {
            return;
        }
        // The open panel spans the toolbar: decide from that width, not from the moving shell.
        var width = _create.Bounds.Width;
        if (width > 0)
        {
            _form.IsStacked = width <= StackedFormWidth;
        }
    }

    private void RefreshRows()
    {
        foreach (var row in _rows.Values)
        {
            row.Refresh();
        }
    }

    #endregion

    #region Keys

    private void ObserveKeys(IEnumerable? source)
    {
        var next = VisualRoot is null ? null : source as INotifyCollectionChanged;
        if (ReferenceEquals(next, _observedKeys))
        {
            return;
        }
        if (_observedKeys is not null)
        {
            _observedKeys.CollectionChanged -= OnKeysSourceChanged;
        }
        _observedKeys = next;
        if (_observedKeys is not null)
        {
            _observedKeys.CollectionChanged += OnKeysSourceChanged;
        }
    }

    private void OnKeysSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncKeys();

    private IEnumerable SourceKeys => KeysSource ?? Keys;

    private void SyncKeys()
    {
        var present = new HashSet<ApiKeyItem>();
        var shown = new List<ApiKeyItem>();
        foreach (var entry in SourceKeys)
        {
            if (entry is ApiKeyItem item && present.Add(item) && !_gone.Contains(item))
            {
                shown.Add(item);
            }
        }
        // A revoked key the app keeps in its own list stays hidden; one it removed is forgotten.
        _gone.RemoveWhere(i => !present.Contains(i));
        _shown = shown;
        if (_list is not null)
        {
            _list.Items = shown.ToArray();
        }
        if (ExpandedKey is { } expanded && !shown.Contains(expanded))
        {
            SetCurrentValue(ExpandedKeyProperty, null);
        }
        UpdateCount(animate: true);
        UpdateFirstRow();
        UpdateEmpty();
    }

    private ApiKeyRow CreateRow(ApiKeyItem item)
    {
        var row = new ApiKeyRow(this, item);
        row.SetLayout(_narrow, _compact);
        row.SetExpanded(ReferenceEquals(ExpandedKey, item), animate: false);
        return row;
    }

    private void OnRowPrepared(object? sender, DeveloperListItemEventArgs e)
    {
        if (e.Item is ApiKeyItem item && e.Visual is ApiKeyRow row)
        {
            _rows[item] = row;
        }
        UpdateFirstRow();
    }

    private void OnRowExited(object? sender, DeveloperListItemEventArgs e)
    {
        if (e.Item is ApiKeyItem item && _rows.TryGetValue(item, out var row) && ReferenceEquals(row, e.Visual))
        {
            _rows.Remove(item);
        }
        UpdateEmpty();
    }

    private void UpdateFirstRow()
    {
        var first = _shown.FirstOrDefault();
        foreach (var (item, row) in _rows)
        {
            row.SetFirst(ReferenceEquals(item, first));
        }
    }

    private void UpdateEmpty()
    {
        // The empty state appears once the last row has folded away, and leaves as soon as a key arrives.
        var empty = _shown.Count == 0 && (_list?.ExitingCount ?? 0) == 0;
        PseudoClasses.Set(EmptyPseudoClass, empty);
        if (_empty is not null)
        {
            _empty.IsVisible = empty;
        }
    }

    private void UpdateCount(bool animate)
    {
        var count = _shown.Count;
        ActiveCount = count;
        if (_count is not null && _shownCount != count)
        {
            var previous = _shownCount;
            _count.Direction = previous < 0 || count >= previous ? 1 : -1;
            if (!animate || previous < 0)
            {
                var mode = _count.Mode;
                _count.Mode = ContentSwapMode.None;
                _count.Content = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
                _count.Mode = mode;
            }
            else
            {
                _count.Content = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
            }
            _shownCount = count;
        }
        if (_countWords is not null)
        {
            _countWords.Text = Labels.CountWords(count);
        }
        if (_countGroup is not null)
        {
            AutomationProperties.SetName(_countGroup, Labels.CountName(count));
        }
    }

    private bool InsertIntoSource(ApiKeyItem item)
    {
        if (KeysSource is null)
        {
            Keys.Insert(0, item);
            return true;
        }
        if (KeysSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            list.Insert(0, item);
            if (KeysSource is not INotifyCollectionChanged)
            {
                SyncKeys();
            }
            return true;
        }
        return false;
    }

    private void RemoveFromSource(ApiKeyItem item)
    {
        if (KeysSource is null)
        {
            _gone.Add(item);
            Keys.Remove(item);
            return;
        }
        _gone.Add(item);
        if (KeysSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            list.Remove(item);
            if (KeysSource is INotifyCollectionChanged)
            {
                return;
            }
        }
        SyncKeys();
    }

    #endregion

    #region Expansion

    internal void ToggleExpanded(ApiKeyItem item) =>
        SetCurrentValue(ExpandedKeyProperty, ReferenceEquals(ExpandedKey, item) ? null : item);

    private void OnExpandedChanged(ApiKeyItem? oldItem, ApiKeyItem? newItem)
    {
        if (newItem is not null && !_shown.Contains(newItem))
        {
            SetCurrentValue(ExpandedKeyProperty, oldItem is not null && _shown.Contains(oldItem) ? oldItem : null);
            return;
        }
        // Accordion: the old row closes in the same frame as the new one opens.
        if (oldItem is not null && _rows.TryGetValue(oldItem, out var oldRow) && !oldItem.IsRevoked)
        {
            oldRow.SetExpanded(false, animate: true);
        }
        if (newItem is not null && _rows.TryGetValue(newItem, out var newRow))
        {
            newRow.SetExpanded(true, animate: true);
            _announcer?.Announce(Labels.DetailsShown(newItem.Name));
        }
        RaiseEvent(new ApiKeyExpandedEventArgs(ExpandedKeyChangedEvent, this, oldItem, newItem));
    }

    #endregion

    #region Create panel

    private void CloseCreateCore(ApiKeysCloseReason reason)
    {
        if (_create is null || !_create.IsOpen)
        {
            if (IsCreateOpen)
            {
                SyncOpen(false);
            }
            return;
        }
        _closeReason = reason;
        _create.Close(reason switch
        {
            ApiKeysCloseReason.Toggle => DeveloperMorphCloseReason.Toggle,
            ApiKeysCloseReason.Escape => DeveloperMorphCloseReason.Escape,
            _ => DeveloperMorphCloseReason.Programmatic,
        });
    }

    private void SyncOpen(bool open)
    {
        _syncingOpen = true;
        try
        {
            SetCurrentValue(IsCreateOpenProperty, open);
        }
        finally
        {
            _syncingOpen = false;
        }
    }

    private void OnCreateOpened(object? sender, EventArgs e)
    {
        if (_create is null)
        {
            return;
        }
        // Arc resets the form on every open, also after Cancel.
        _generating?.Cancel();
        _generating = null;
        _attempt = 0;
        GenerateError = null;
        if (!ReferenceEquals(_create.Content, _form))
        {
            ReplaceForm();
            _create.Content = _form;
        }
        _form.Reset(DefaultEnvironment, EffectiveScopes);
        _form.SetGenerateError(null);
        _create.Title = Labels.CreateKey;
        _create.InitialFocus = _form.NameInput;
        UpdateStacked();
        AttachBodyHost();
        SyncOpen(true);
        SetPanelState(ApiKeysPanelState.Form, null);
    }

    private void OnCreateClosed(object? sender, DeveloperMorphClosedEventArgs e)
    {
        var reason = _closeReason ?? e.Reason switch
        {
            DeveloperMorphCloseReason.Toggle => ApiKeysCloseReason.Toggle,
            DeveloperMorphCloseReason.Escape => ApiKeysCloseReason.Escape,
            _ => ApiKeysCloseReason.Programmatic,
        };
        _closeReason = null;
        _generating?.Cancel();
        _generating = null;
        _form.SetBusy(false);
        // The secret leaves with the panel; the fresh row keeps its tint for 300ms, then fades over 1.2s.
        _reveal.Clear();
        if (_fresh is { } fresh)
        {
            fresh.IsFresh = false;
            _list?.EndFlash(fresh);
            _fresh = null;
        }
        if (_create is not null)
        {
            _create.Title = Labels.CreateKey;
        }
        SyncOpen(false);
        SetPanelState(ApiKeysPanelState.Closed, reason);
    }

    private void AttachBodyHost()
    {
        if (_bodyHost is not null || _create is null)
        {
            return;
        }
        _bodyHost = _create.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c.Name == DeveloperMorphPanel.BodyHostPart);
        if (_bodyHost is not null)
        {
            _bodyHost.PropertyChanged += OnBodyHostChanged;
        }
    }

    private void DetachBodyHost()
    {
        if (_bodyHost is not null)
        {
            _bodyHost.PropertyChanged -= OnBodyHostChanged;
            _bodyHost = null;
        }
    }

    private void OnBodyHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Once the closed panel's body is out of sight, put the form back without a visible swap (a hidden swap is instant).
        if (e.Property == IsVisibleProperty && !e.GetNewValue<bool>() && _create is { IsOpen: false } create
            && !ReferenceEquals(create.Content, _form))
        {
            ReplaceForm();
            create.Content = _form;
        }
    }

    // A fresh form for every open and a fresh reveal for every key: an outgoing copy may still sit in the body's
    // crossfade, and a control cannot have two parents.
    private ApiKeyForm CreateForm()
    {
        var form = new ApiKeyForm();
        form.Submitted += OnFormSubmitted;
        form.Cancelled += OnFormCancelled;
        form.Configure(Labels, NameMaxLength);
        return form;
    }

    private void ReplaceForm()
    {
        _form.Submitted -= OnFormSubmitted;
        _form.Cancelled -= OnFormCancelled;
        _form = CreateForm();
    }

    private ApiKeyReveal CreateReveal()
    {
        var reveal = new ApiKeyReveal();
        reveal.DoneClicked += OnRevealDone;
        reveal.Copied += OnSecretCopied;
        reveal.Configure(Labels);
        return reveal;
    }

    private void ReplaceReveal()
    {
        _reveal.Clear();
        _reveal.DoneClicked -= OnRevealDone;
        _reveal.Copied -= OnSecretCopied;
        _reveal = CreateReveal();
    }

    private void OnFormSubmitted(object? sender, EventArgs e) => _ = SubmitAsync();

    private void OnFormCancelled(object? sender, EventArgs e) => CloseCreateCore(ApiKeysCloseReason.Cancel);

    private void OnRevealDone(object? sender, EventArgs e) => CloseCreateCore(ApiKeysCloseReason.Done);

    private void SetPanelState(ApiKeysPanelState state, ApiKeysCloseReason? reason)
    {
        var old = PanelState;
        if (old == state)
        {
            return;
        }
        PanelState = state;
        RaiseEvent(new ApiKeysPanelStateChangedEventArgs(PanelStateChangedEvent, this, old, state, reason));
    }

    private async Task<bool> SubmitAsync()
    {
        if (PanelState != ApiKeysPanelState.Form)
        {
            return false;
        }
        var method = ApiKeyFocus.Method(_keyboard);
        var name = _form.NameText.Trim();
        if (name.Length == 0)
        {
            _form.ShowNameError(method);
            return false;
        }
        var scopes = _form.CheckedScopes;
        if (scopes.Count == 0)
        {
            _form.ShowScopeError(method);
            return false;
        }
        var request = new ApiKeyRequest(name, _form.Environment, scopes, ++_attempt);
        var cancellation = new CancellationTokenSource();
        _generating = cancellation;
        GenerateError = null;
        _form.SetGenerateError(null);
        _form.SetBusy(true);
        SetPanelState(ApiKeysPanelState.Generating, null);

        ApiKeyCreated created;
        try
        {
            created = GenerateAction is { } action
                ? await action(request, cancellation.Token)
                : await SimulateGenerateAsync(request, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(_generating, cancellation))
            {
                _generating = null;
                _form.SetBusy(false);
                SetPanelState(ApiKeysPanelState.Form, null);
            }
            return false;
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(_generating, cancellation))
            {
                return false;
            }
            _generating = null;
            var error = string.IsNullOrWhiteSpace(exception.Message) || exception is OperationCanceledException
                ? Labels.GenerateFailed
                : exception.Message;
            GenerateError = error;
            _form.SetBusy(false);
            _form.SetGenerateError(error);
            SetPanelState(ApiKeysPanelState.Form, null);
            _announcer?.Announce(error);
            RaiseEvent(new ApiKeyCreateFailedEventArgs(KeyCreateFailedEvent, this, request, exception));
            return false;
        }
        if (!ReferenceEquals(_generating, cancellation))
        {
            return false;
        }
        _generating = null;
        _form.SetBusy(false);

        var item = created.Item;
        item.IsRevoked = false;
        item.IsFresh = true;
        _fresh = item;
        _gone.Remove(item);
        if (_list is not null)
        {
            _list.Change(DeveloperListMotion.Insert, () => InsertIntoSource(item));
        }
        else
        {
            InsertIntoSource(item);
        }
        // Highlight it until the panel closes (also when the app adds the row itself in KeyCreated).
        RaiseEvent(new ApiKeyCreatedEventArgs(KeyCreatedEvent, this, item, request));
        if (KeyCreatedCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
        _list?.Flash(item, DeveloperFlash.Held);

        ReplaceReveal();
        _reveal.Show(created.Secret);
        if (_create is not null)
        {
            _create.Title = Labels.KeyCreated;
            _create.Content = _reveal;
        }
        SetPanelState(ApiKeysPanelState.Reveal, null);
        _announcer?.Announce(Labels.CreatedAnnouncement(item.Name));
        return true;
    }

    private async Task<ApiKeyCreated> SimulateGenerateAsync(ApiKeyRequest request, CancellationToken token)
    {
        await Task.Delay(DeveloperSprings.SimulatedGenerate, token);
        var environment = request.Environment == ApiKeyEnvironment.Live ? "live" : "test";
        var prefix = DeveloperText.Format(SimulatedPrefixFormat, environment);
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var body = new char[40];
        for (var i = 0; i < body.Length; i++)
        {
            body[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }
        var secret = prefix + new string(body);
        var item = new ApiKeyItem
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Environment = request.Environment,
            Prefix = prefix,
            LastFour = secret[^4..],
            CreatedAt = Now,
            CreatedBy = CurrentUserName,
        };
        item.Scopes.AddRange(request.Scopes);
        return new ApiKeyCreated(item, secret);
    }

    private void OnSecretCopied(object? sender, EventArgs e)
    {
        _announcer?.Announce(Labels.CopiedAnnouncement);
        if (_fresh is { } item)
        {
            RaiseEvent(new ApiKeySecretCopiedEventArgs(SecretCopiedEvent, this, item));
        }
    }

    #endregion

    #region Revoke

    internal void OnRevokeConfirmed(ApiKeyRow row) => _ = RevokeCoreAsync(row.Item, row);

    private async Task<bool> RevokeCoreAsync(ApiKeyItem item, ApiKeyRow? row)
    {
        if (item.IsRevoked || !_shown.Contains(item))
        {
            return false;
        }
        var revoking = new ApiKeyRevokingEventArgs(KeyRevokingEvent, this, item);
        RaiseEvent(revoking);
        if (revoking.Cancel)
        {
            row?.ResetHold();
            return false;
        }
        // Struck through and dimmed at once; the server and a short pause run side by side before the row folds away.
        item.IsRevoked = true;
        if (row?.RevokeButton is { IsConfirmed: false } hold)
        {
            hold.IsConfirmed = true;
        }
        var cancellation = new CancellationTokenSource();
        _revoking[item] = cancellation;
        var pause = RevokeExitDelay > TimeSpan.Zero ? Task.Delay(RevokeExitDelay, cancellation.Token) : Task.CompletedTask;
        try
        {
            if (RevokeAction is { } action)
            {
                await action(item, cancellation.Token);
            }
            await pause;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _revoking.Remove(item);
            return false;
        }
        catch (Exception exception)
        {
            _revoking.Remove(item);
            item.IsRevoked = false;
            RowFor(item)?.ResetHold();
            var error = string.IsNullOrWhiteSpace(exception.Message) ? Labels.RevokeFailed : exception.Message;
            _announcer?.Announce(Labels.RevokeFailedAnnouncement(item.Name, error));
            RaiseEvent(new ApiKeyRevokeFailedEventArgs(KeyRevokeFailedEvent, this, item, exception));
            return false;
        }
        _revoking.Remove(item);

        var index = _shown.IndexOf(item);
        if (index < 0)
        {
            return false;
        }
        MoveFocusAway(item, index);
        RemoveFromSource(item);
        _announcer?.Announce(Labels.RevokedAnnouncement(item.Name));
        RaiseEvent(new ApiKeyRevokedEventArgs(KeyRevokedEvent, this, item, index));
        if (KeyRevokedCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
        return true;
    }

    private void MoveFocusAway(ApiKeyItem item, int index)
    {
        // Arc drops the focus to the page here; it goes to the next row, else the previous one, else the Create key pill.
        if (RowFor(item) is not { } row || !row.IsKeyboardFocusWithin)
        {
            return;
        }
        var method = ApiKeyFocus.Method(_keyboard);
        ApiKeyItem? target = null;
        for (var i = index + 1; i < _shown.Count && target is null; i++)
        {
            target = _shown[i].IsRevoked ? null : _shown[i];
        }
        for (var i = index - 1; i >= 0 && target is null; i--)
        {
            target = _shown[i].IsRevoked ? null : _shown[i];
        }
        if (target is not null && RowFor(target)?.Trigger is { } trigger)
        {
            trigger.Focus(method);
            return;
        }
        _create?.Toggle?.Focus(method);
    }

    #endregion

    private void OnEmptyAction(object? sender, RoutedEventArgs e)
    {
        var args = new RoutedEventArgs(EmptyActionInvokedEvent, this);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (EmptyActionCommand is { } command)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
            return;
        }
        OpenCreate();
    }
}

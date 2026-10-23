using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class ContextWindow
{
    private const double NumberDuration = 0.4;
    private const double MenuRowHeight = 38;
    private const double MenuRowInset = 10;
    private const double MenuCheckSlot = 32;
    private const string ModelGroup = "context-window-models";

    private static readonly Easing s_numberEasing = new CubicEaseOut();

    // Template parts.
    private Border? _card;
    private ContextWindowReadout? _readout;
    private ContextWindowModelPill? _pill;
    private ContextWindowGrid? _grid;
    private ContextWindowBodyPanel? _body;
    private Panel? _legendHost;
    private TextBlock? _cellCaption;
    private TextBlock? _thresholdCaption;
    private AvaloniaButton? _send;
    private AvaloniaButton? _attach;
    private AvaloniaButton? _compact;
    private TextBlock? _sendText;
    private TextBlock? _attachText;
    private TextBlock? _compactText;
    private ContextWindowStatusLine? _status;
    private ExplainerAnnouncer? _announcer;
    private ContextWindowFocusHolder? _focusHolder;
    private readonly ContextWindowLegendRow?[] _rows = new ContextWindowLegendRow?[7];
    private MenuSurface? _menu;
    private bool _isBuilt;

    // Data in effect: the given lists, or the sample session.
    private List<ContextWindowItem> _items = new();
    private IReadOnlyList<ContextWindowModel> _models = Array.Empty<ContextWindowModel>();
    private IReadOnlyList<ContextWindowTurn> _turns = Array.Empty<ContextWindowTurn>();
    private IReadOnlyList<ContextWindowTurn> _files = Array.Empty<ContextWindowTurn>();
    private ContextWindowModel? _model;
    private bool _usesSamples;
    private bool _dataRead;
    private List<ContextWindowItem>? _sampleItems;
    private IReadOnlyList<ContextWindowModel>? _sampleModels;
    private IReadOnlyList<ContextWindowTurn>? _sampleTurns;
    private IReadOnlyList<ContextWindowTurn>? _sampleFiles;
    private int _turnCursor;
    private int _fileCursor;
    private int _created;
    private ContextWindowLayout? _layout;
    private bool _writing;
    private AvaloniaList<ContextWindowItem>? _observedItems;
    private AvaloniaList<ContextWindowModel>? _observedModels;
    private AvaloniaList<ContextWindowTurn>? _observedTurns;
    private AvaloniaList<ContextWindowTurn>? _observedFiles;
    private readonly HashSet<ContextWindowItem> _observedItemSet = new();

    // The status line.
    private string _statusText = string.Empty;
    private ContextWindowTone _statusTone;
    private bool _statusIsIdle = true;

    // Timers: the wait before an automatic compaction and the summarizing phase.
    private IDisposable? _pendingCompaction;
    private IDisposable? _pendingLanding;
    private ContextWindowCompactReason _summarizingReason;

    // Focus kept inside the card while the actions are off.
    private Control? _restoreFocus;
    private bool _restoreKeyboard;
    private bool _focusSelectedOnOpen;

    // Rolling numbers on one clock (Full motion only).
    private LiveLoop? _numbers;
    private double _numbersNow;
    private ExplainerValue _usedValue;
    private readonly ExplainerValue[] _partValues = new ExplainerValue[7];
    private double _shownWindow;

    private ContextWindowPart? _hovered;

    /// <summary>The timers behind the automatic compaction and the summarizing phase (tests replace them).</summary>
    internal IContextWindowTimers Timers { get; set; } = ContextWindowDispatcherTimers.Instance;

    /// <summary>The arrangement on screen (tests).</summary>
    internal ContextWindowLayout? CurrentLayout => _layout;

    /// <summary>The items in effect: the given ones or the sample session (tests).</summary>
    internal IReadOnlyList<ContextWindowItem> EffectiveItems => _items;

    /// <summary>The models in effect (tests).</summary>
    internal IReadOnlyList<ContextWindowModel> EffectiveModels => _models;

    /// <summary>The model in effect.</summary>
    internal ContextWindowModel? EffectiveModel => _model;

    /// <summary>True while the card shows the sample session.</summary>
    internal bool UsesSamples => _usesSamples;

    /// <summary>The status sentence and tone (tests).</summary>
    internal string StatusText => _statusText;

    internal ContextWindowTone StatusTone => _statusTone;

    internal ContextWindowGrid? GridPartControl => _grid;

    internal ContextWindowReadout? ReadoutPartControl => _readout;

    internal ContextWindowModelPill? Pill => _pill;

    internal MenuSurface? ModelMenu => _menu;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal ContextWindowFocusHolder? FocusHolder => _focusHolder;

    internal IReadOnlyList<ContextWindowLegendRow?> LegendRows => _rows;

    internal AvaloniaButton? SendButton => _send;

    internal AvaloniaButton? AttachButton => _attach;

    internal AvaloniaButton? CompactButton => _compact;

    internal TextBlock? CellCaption => _cellCaption;

    internal TextBlock? ThresholdCaption => _thresholdCaption;

    private int ThresholdPercent => ContextWindowFormat.ThresholdPercent(Threshold);

    private void InitializeBehavior()
    {
        // Code-driven motion: the readout and legend numbers roll on every frame for 400ms from the value on screen; the
        // text is drawn (not laid out) each frame, which a ControlTheme transition cannot do.
        _numbers = new LiveLoop(this, StepNumbers) { MinimumLevel = ArcMotionLevel.Full };
        ObserveData();
    }

    #region Public actions

    /// <summary>
    /// Adds the next turn of <see cref="Turns"/> as a message at the end of the window (starting over after the last). Does
    /// nothing while summarizing or without turns.
    /// </summary>
    public void Send()
    {
        EnsureData();
        if (_phase != ContextWindowPhase.Idle || _turns.Count == 0)
        {
            return;
        }
        var turn = _turns[_turnCursor % _turns.Count];
        _turnCursor = (_turnCursor + 1) % _turns.Count;
        var item = new ContextWindowItem(ContextWindowCategory.Messages, turn.Label, turn.Tokens, $"turn-{++_created}");
        InsertItem(item);
        Rebuild(ContextWindowWave.Append);
        ReportAddition(item, Labels.Sent(turn.Label, ContextWindowFormat.Tokens(turn.Tokens)), ContextWindowChange.Sent);
    }

    /// <summary>
    /// Adds the next file of <see cref="Files"/> at the end of the files and memory (starting over after the last); the
    /// summary and the conversation move along. Does nothing while summarizing or without files.
    /// </summary>
    public void Attach()
    {
        EnsureData();
        if (_phase != ContextWindowPhase.Idle || _files.Count == 0)
        {
            return;
        }
        var file = _files[_fileCursor % _files.Count];
        _fileCursor = (_fileCursor + 1) % _files.Count;
        var item = new ContextWindowItem(ContextWindowCategory.Files, file.Label, file.Tokens, $"file-{++_created}");
        InsertItem(item);
        Rebuild(ContextWindowWave.Insert);
        ReportAddition(item, Labels.Attached(file.Label, ContextWindowFormat.Tokens(file.Tokens)), ContextWindowChange.Attached);
    }

    /// <summary>
    /// Adds an item of your own (a real message or file) with the same wave, status and threshold check as Send message
    /// and Attach file: a message goes to the end, any other item to the end of its category. Ignored while summarizing.
    /// </summary>
    public void AddItem(ContextWindowItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureData();
        if (_phase != ContextWindowPhase.Idle)
        {
            return;
        }
        var wasSample = _usesSamples;
        if (wasSample)
        {
            // Your first item starts your own session: the sample items, turns and files go away.
            _usesSamples = false;
            _sampleItems = null;
            _items = new List<ContextWindowItem>();
            _turns = Turns?.ToArray() ?? Array.Empty<ContextWindowTurn>();
            _files = Files?.ToArray() ?? Array.Empty<ContextWindowTurn>();
        }
        var index = InsertItem(item);
        Rebuild(!wasSample && item.Category == ContextWindowCategory.Messages && index == _items.Count - 1
            ? ContextWindowWave.Append
            : ContextWindowWave.Insert);
        var tokens = ContextWindowFormat.Tokens(item.Tokens);
        var text = item.Category == ContextWindowCategory.Files ? Labels.Attached(item.Label, tokens) : Labels.Added(item.Label, tokens);
        ReportAddition(item, text, ContextWindowChange.Added);
    }

    /// <summary>
    /// Folds every message but the newest <see cref="KeepRecentMessages"/> into the summary: about 700ms of summarizing
    /// (every action off), then the summary lands after the files and the space comes back. Does nothing unless
    /// <see cref="CanCompact"/>.
    /// </summary>
    public void Compact()
    {
        EnsureData();
        if (!CanCompact)
        {
            return;
        }
        CancelPendingCompaction();
        BeginSummarizing(ContextWindowCompactReason.Manual);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _card = e.NameScope.Find<Border>("PART_Card");
        _readout = e.NameScope.Find<ContextWindowReadout>(ReadoutPart);
        _pill = e.NameScope.Find<ContextWindowModelPill>(ModelPillPart);
        _grid = e.NameScope.Find<ContextWindowGrid>(GridPart);
        _body = e.NameScope.Find<ContextWindowBodyPanel>(BodyPart);
        _legendHost = e.NameScope.Find<Panel>(LegendPart);
        _cellCaption = e.NameScope.Find<TextBlock>(CellCaptionPart);
        _thresholdCaption = e.NameScope.Find<TextBlock>(ThresholdCaptionPart);
        _send = e.NameScope.Find<AvaloniaButton>(SendButtonPart);
        _attach = e.NameScope.Find<AvaloniaButton>(AttachButtonPart);
        _compact = e.NameScope.Find<AvaloniaButton>(CompactButtonPart);
        _sendText = e.NameScope.Find<TextBlock>(SendTextPart);
        _attachText = e.NameScope.Find<TextBlock>(AttachTextPart);
        _compactText = e.NameScope.Find<TextBlock>(CompactTextPart);
        _status = e.NameScope.Find<ContextWindowStatusLine>(StatusPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _focusHolder = e.NameScope.Find<ContextWindowFocusHolder>(FocusHolderPart);
        if (e.NameScope.Find<ArcGlyph>("PART_AttachIcon") is { } attachIcon)
        {
            attachIcon.Data = ContextWindowIcons.FilePlusCorner;
        }
        Hook();
        BuildLegend();
        EnsureMenu();
        EnsureData();
        BuildMenu();
        _layout = null;
        UpdatePill();
        UpdateStaticTexts();
        Rebuild(ContextWindowWave.None);
        if (_statusText.Length == 0 || _statusIsIdle)
        {
            ShowIdleStatus(announce: false);
        }
        else
        {
            _status?.Show(_statusText, _statusTone, animate: false);
        }
        _isBuilt = true;
        UpdateBreakpoints(Bounds.Width);
    }

    private void Hook()
    {
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_attach is not null)
        {
            _attach.Click += OnAttachClick;
        }
        if (_compact is not null)
        {
            _compact.Click += OnCompactClick;
        }
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
            _pill.AddHandler(KeyDownEvent, OnPillKeyDown, RoutingStrategies.Tunnel);
        }
        if (_grid is not null)
        {
            _grid.PaletteChanged += OnPaletteChanged;
        }
    }

    private void Unhook()
    {
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_attach is not null)
        {
            _attach.Click -= OnAttachClick;
        }
        if (_compact is not null)
        {
            _compact.Click -= OnCompactClick;
        }
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
            _pill.RemoveHandler(KeyDownEvent, OnPillKeyDown);
        }
        if (_grid is not null)
        {
            _grid.PaletteChanged -= OnPaletteChanged;
        }
        foreach (var row in _rows)
        {
            if (row is not null)
            {
                row.PointerEntered -= OnRowPointerEntered;
                row.PointerExited -= OnRowPointerExited;
            }
        }
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => Send();

    private void OnAttachClick(object? sender, RoutedEventArgs e) => Attach();

    private void OnCompactClick(object? sender, RoutedEventArgs e) => Compact();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        // Breakpoints follow the card's content width (the card less its padding), never the window.
        var padding = _card?.Padding ?? new Thickness(24);
        var content = width - padding.Left - padding.Right;
        var stacked = content <= StackWidth + 0.01;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactHeaderPseudoClass, content <= HeaderStackWidth + 0.01);
        if (_body is not null)
        {
            _body.IsStacked = stacked;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _menu?.Close(returnFocus: false);
        CancelPendingCompaction();
        if (_phase == ContextWindowPhase.Summarizing)
        {
            // Timers only run while the card is shown: a compaction in flight lands at once.
            _pendingLanding?.Dispose();
            _pendingLanding = null;
            Land();
        }
        SettleNumbers();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    private sealed class CardPeer(ContextWindow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name
            ? name
            : ((ContextWindow)Owner).Labels.Region;
    }

    #endregion

    #region Data

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == ItemsProperty || p == ModelsProperty || p == TurnsProperty || p == FilesProperty)
        {
            ObserveData();
            OnDataChanged();
        }
        else if (p == SelectedModelProperty)
        {
            OnSelectedModelChanged();
        }
        else if (p == ThresholdProperty || p == KeepRecentMessagesProperty)
        {
            if (_isBuilt)
            {
                Rebuild(ContextWindowWave.Insert);
                if (_statusIsIdle)
                {
                    ShowIdleStatus(announce: false);
                }
            }
        }
        else if (p == LabelsProperty)
        {
            if (_isBuilt)
            {
                BuildMenu();
                UpdatePill();
                UpdateStaticTexts();
                UpdateLayoutTexts();
                if (_statusIsIdle)
                {
                    ShowIdleStatus(announce: false);
                }
            }
        }
        else if (p == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
        {
            SettleNumbers();
        }
    }

    private void EnsureData()
    {
        if (!_dataRead)
        {
            ReadData();
        }
    }

    private void ReadData()
    {
        var models = Models;
        var items = Items;
        var turns = Turns;
        var files = Files;
        var samples = (models?.Count ?? 0) == 0 && (items?.Count ?? 0) == 0 && (turns?.Count ?? 0) == 0 && (files?.Count ?? 0) == 0;
        if (samples)
        {
            if (!_usesSamples || _sampleItems is null)
            {
                _sampleItems = ContextWindowSamples.CreateItems().ToList();
                _turnCursor = 0;
                _fileCursor = 0;
            }
            _items = _sampleItems;
            _turns = _sampleTurns ??= ContextWindowSamples.CreateTurns();
            _files = _sampleFiles ??= ContextWindowSamples.CreateFiles();
        }
        else
        {
            if (_usesSamples)
            {
                _turnCursor = 0;
                _fileCursor = 0;
            }
            _items = items is null ? new List<ContextWindowItem>() : items.ToList();
            _turns = turns?.ToArray() ?? Array.Empty<ContextWindowTurn>();
            _files = files?.ToArray() ?? Array.Empty<ContextWindowTurn>();
        }
        _usesSamples = samples;
        ReadModels();
        _dataRead = true;
    }

    private void ReadModels()
    {
        var models = Models;
        if (models is { Count: > 0 })
        {
            _models = models.ToArray();
        }
        else if (!_usesSamples && SelectedModel is { } only)
        {
            _models = [only];
        }
        else
        {
            _models = _sampleModels ??= ContextWindowSamples.CreateModels();
        }
        _model = ResolveModel();
    }

    private ContextWindowModel? ResolveModel()
    {
        var selected = SelectedModel;
        if (selected is not null)
        {
            foreach (var model in _models)
            {
                if (ReferenceEquals(model, selected))
                {
                    return model;
                }
            }
            if (selected.Id.Length > 0)
            {
                foreach (var model in _models)
                {
                    if (model.Id == selected.Id)
                    {
                        return model;
                    }
                }
            }
        }
        return _models.Count > 0 ? _models[0] : selected;
    }

    private void ObserveData()
    {
        Swap(ref _observedItems, Items);
        Swap(ref _observedModels, Models);
        Swap(ref _observedTurns, Turns);
        Swap(ref _observedFiles, Files);
        ObserveItemProperties();
    }

    private void Swap<T>(ref AvaloniaList<T>? field, AvaloniaList<T>? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        if (field is not null)
        {
            field.CollectionChanged -= OnDataCollectionChanged;
        }
        field = value;
        if (field is not null)
        {
            field.CollectionChanged += OnDataCollectionChanged;
        }
    }

    private void ObserveItemProperties()
    {
        var current = Items;
        foreach (var item in _observedItemSet)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _observedItemSet.Clear();
        if (current is null)
        {
            return;
        }
        foreach (var item in current)
        {
            if (_observedItemSet.Add(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
    }

    private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _observedItems))
        {
            ObserveItemProperties();
        }
        OnDataChanged();
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContextWindowItem.TokensProperty || e.Property == ContextWindowItem.CategoryProperty)
        {
            OnDataChanged();
        }
    }

    /// <summary>A change from outside (the app edited a list or an item): show it with an insert wave, keep the status.</summary>
    private void OnDataChanged()
    {
        if (_writing)
        {
            return;
        }
        var previousModel = _model;
        ReadData();
        if (!_isBuilt)
        {
            return;
        }
        BuildMenu();
        UpdatePill();
        Rebuild(ReferenceEquals(previousModel, _model) ? ContextWindowWave.Insert : ContextWindowWave.Relayout);
        if (_statusIsIdle)
        {
            ShowIdleStatus(announce: false);
        }
    }

    private int InsertItem(ContextWindowItem item)
    {
        var index = _items.Count;
        if (item.Category != ContextWindowCategory.Messages)
        {
            index = 0;
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Category <= item.Category)
                {
                    index = i + 1;
                }
            }
        }
        _items.Insert(index, item);
        if (!_usesSamples)
        {
            WriteItems(list => list.Insert(Math.Min(index, list.Count), item));
        }
        return index;
    }

    private void ReplaceItems(IReadOnlyList<ContextWindowItem> items)
    {
        _items = items.ToList();
        if (_usesSamples)
        {
            _sampleItems = _items;
            return;
        }
        WriteItems(list =>
        {
            list.Clear();
            list.AddRange(items);
        });
    }

    /// <summary>Writes the card's own change into <see cref="Items"/> without treating it as a change from outside.</summary>
    private void WriteItems(Action<AvaloniaList<ContextWindowItem>> write)
    {
        var list = Items;
        if (list is null)
        {
            return;
        }
        _writing = true;
        try
        {
            write(list);
        }
        finally
        {
            _writing = false;
        }
        ObserveItemProperties();
    }

    private void RaiseItemsChanged(ContextWindowChange change, ContextWindowItem? item) =>
        RaiseEvent(new ContextWindowItemsChangedEventArgs(ItemsChangedEvent, _items.ToArray(), change, item));

    #endregion

    #region Layout and numbers

    private void Rebuild(ContextWindowWave wave)
    {
        EnsureData();
        var window = _model?.Window ?? 0;
        var reserve = _model?.Reserve ?? 0;
        var layout = ContextWindowBudget.Compute(window, reserve, Threshold, ContextWindowBudget.Sum(_items));
        var previous = _layout;
        _layout = layout;
        if (_grid is not null)
        {
            var start = wave == ContextWindowWave.Relayout ? 0 : ContextWindowBudget.FirstChange(previous?.Kinds, layout.Kinds);
            var gridWave = previous is null || start < 0 ? ContextWindowWave.None : wave;
            _grid.Apply(layout.Kinds, layout.ThresholdRow, gridWave, Math.Max(0, start));
        }
        UpdateNumbers(previous is not null && wave != ContextWindowWave.None);
        UpdateLayoutTexts();
        UpdateState();
    }

    private void UpdateNumbers(bool animate)
    {
        var layout = _layout;
        if (layout is null)
        {
            return;
        }
        var roll = animate && ArcMotion.IsFull(this) && _numbers is not null;
        var now = _numbersNow;
        if (roll)
        {
            _usedValue.Tween(layout.Used, now, NumberDuration, s_numberEasing);
            for (var p = 0; p < _partValues.Length; p++)
            {
                _partValues[p].Tween(layout.TokensOf((ContextWindowPart)p), now, NumberDuration, s_numberEasing);
            }
            _numbers!.Start();
        }
        else
        {
            _usedValue.Jump(layout.Used);
            for (var p = 0; p < _partValues.Length; p++)
            {
                _partValues[p].Jump(layout.TokensOf((ContextWindowPart)p));
            }
        }
        // The percentages follow the new window at once and then roll with the tokens.
        _shownWindow = layout.Window;
        PushNumbers();
    }

    private bool StepNumbers(double dt)
    {
        _numbersNow += dt;
        PushNumbers();
        if (_usedValue.IsMoving(_numbersNow))
        {
            return true;
        }
        foreach (var value in _partValues)
        {
            if (value.IsMoving(_numbersNow))
            {
                return true;
            }
        }
        return false;
    }

    private void PushNumbers()
    {
        var now = _numbersNow;
        _readout?.Show(_usedValue.Value(now), _shownWindow);
        for (var p = 0; p < _rows.Length; p++)
        {
            _rows[p]?.Show(_partValues[p].Value(now), _shownWindow);
        }
    }

    private void SettleNumbers()
    {
        _usedValue.Jump(_usedValue.Target);
        for (var p = 0; p < _partValues.Length; p++)
        {
            _partValues[p].Jump(_partValues[p].Target);
        }
        PushNumbers();
    }

    /// <summary>Advances the number roll without frames (tests).</summary>
    internal void AdvanceNumbers(double seconds)
    {
        _numbersNow += seconds;
        PushNumbers();
    }

    private void UpdateState()
    {
        var layout = _layout;
        if (layout is null)
        {
            return;
        }
        UsedTokens = layout.Used;
        FreeTokens = layout.Free;
        var idle = _phase == ContextWindowPhase.Idle;
        CanCompact = idle && ContextWindowBudget.FoldableCount(_items, KeepRecentMessages) > 0;
        PseudoClasses.Set(SummarizingPseudoClass, !idle);
        PseudoClasses.Set(WarningPseudoClass, layout.IsWarning);
        PseudoClasses.Set(OverPseudoClass, layout.IsOver);
        if (_send is not null)
        {
            _send.IsVisible = _turns.Count > 0;
            _send.IsEnabled = idle;
        }
        if (_attach is not null)
        {
            _attach.IsVisible = _files.Count > 0;
            _attach.IsEnabled = idle;
        }
        if (_compact is not null)
        {
            _compact.IsEnabled = CanCompact;
        }
        if (_pill is not null)
        {
            _pill.IsVisible = _model is not null;
            _pill.IsEnabled = idle;
        }
    }

    #endregion

    #region Texts

    /// <summary>Words that change only with the labels: buttons, legend names and the region.</summary>
    private void UpdateStaticTexts()
    {
        var labels = Labels;
        SetText(_sendText, _send, labels.Send);
        SetText(_attachText, _attach, labels.Attach);
        SetText(_compactText, _compact, labels.Compact);
        if (_legendHost is not null)
        {
            AutomationProperties.SetName(_legendHost, labels.Legend);
        }
        for (var p = 0; p < _rows.Length; p++)
        {
            if (_rows[p] is { } row)
            {
                row.Label = PartName((ContextWindowPart)p);
            }
        }

        static void SetText(TextBlock? text, Control? owner, string value)
        {
            if (text is not null)
            {
                text.Text = value;
            }
            if (owner is not null)
            {
                AutomationProperties.SetName(owner, value);
            }
        }
    }

    private string PartName(ContextWindowPart part)
    {
        var labels = Labels;
        return part switch
        {
            ContextWindowPart.System => labels.SystemPrompt,
            ContextWindowPart.Tools => labels.Tools,
            ContextWindowPart.Files => labels.Files,
            ContextWindowPart.Summary => labels.Summary,
            ContextWindowPart.Messages => labels.Messages,
            ContextWindowPart.Free => labels.Free,
            _ => labels.Reserve
        };
    }

    /// <summary>Words that follow the arrangement (they change at once, never roll).</summary>
    private void UpdateLayoutTexts()
    {
        var layout = _layout;
        if (layout is null)
        {
            return;
        }
        var labels = Labels;
        var percent = ThresholdPercent;
        var windowText = ContextWindowFormat.Tokens(layout.Window);
        var usedText = ContextWindowFormat.Tokens(layout.Used);
        var ofText = labels.OfTokens(windowText);
        var freeText = labels.FreeTokens(ContextWindowFormat.Tokens(layout.Free));
        if (_readout is not null)
        {
            _readout.SetTexts(ofText, labels.Used, freeText);
            AutomationProperties.SetName(_readout,
                $"{usedText} {ofText}, {ContextWindowFormat.HeaderPercentText(layout.Used, layout.Window)} {labels.Used}, {freeText}");
        }
        if (_cellCaption is not null)
        {
            _cellCaption.Text = labels.CellSize(ContextWindowFormat.Tokens(layout.PerCell));
        }
        if (_thresholdCaption is not null)
        {
            _thresholdCaption.Text = labels.CompactsAt(percent);
        }
        var parts = new string[_rows.Length];
        for (var p = 0; p < _rows.Length; p++)
        {
            var part = (ContextWindowPart)p;
            var tokens = layout.TokensOf(part);
            var name = PartName(part);
            var tokensText = ContextWindowFormat.Tokens(tokens);
            parts[p] = labels.GridPart(name, tokensText);
            if (_rows[p] is not { } row)
            {
                continue;
            }
            string? placeholder = null;
            if (tokens <= 0)
            {
                placeholder = part == ContextWindowPart.Summary ? labels.NoSummary : tokensText;
            }
            row.SetPlaceholder(placeholder);
            AutomationProperties.SetName(row, labels.LegendRow(name, placeholder ?? tokensText,
                placeholder is null ? ContextWindowFormat.LegendPercent(tokens, layout.Window) : string.Empty));
        }
        if (_grid is not null)
        {
            AutomationProperties.SetName(_grid, labels.GridDescription(_model?.Name ?? string.Empty, usedText, windowText,
                string.Join(", ", parts), percent));
        }
        AutomationProperties.SetName(this, labels.Region);
    }

    private void SetStatus(string text, ContextWindowTone tone, bool announce = true, bool idle = false)
    {
        _statusText = text;
        _statusTone = tone;
        _statusIsIdle = idle;
        _status?.Show(text, tone, animate: _isBuilt && ArcMotion.IsFull(this));
        if (_focusHolder is not null)
        {
            AutomationProperties.SetName(_focusHolder, text);
        }
        if (announce)
        {
            _announcer?.Announce(text);
        }
    }

    private void ShowIdleStatus(bool announce) => SetStatus(Labels.Idle(ThresholdPercent), ContextWindowTone.Info, announce, idle: true);

    private void ReportAddition(ContextWindowItem item, string text, ContextWindowChange change)
    {
        var layout = _layout!;
        var percent = ThresholdPercent;
        if (layout.IsOver)
        {
            SetStatus(text + Labels.PastThreshold(percent), ContextWindowTone.Warning);
            if (AutoCompact)
            {
                ScheduleCompaction(ContextWindowCompactReason.Threshold, ThresholdCompactionDelay);
            }
        }
        else if (layout.IsWarning)
        {
            SetStatus(text + Labels.NearThreshold(percent), ContextWindowTone.Warning);
        }
        else
        {
            SetStatus(text, ContextWindowTone.Info);
        }
        RaiseItemsChanged(change, item);
    }

    #endregion

    #region Compaction

    private void ScheduleCompaction(ContextWindowCompactReason reason, TimeSpan delay)
    {
        CancelPendingCompaction();
        _pendingCompaction = Timers.Start(ArcMotion.IsFull(this) ? delay : TimeSpan.Zero, () =>
        {
            _pendingCompaction = null;
            RunAutomaticCompaction(reason);
        });
    }

    private void CancelPendingCompaction()
    {
        _pendingCompaction?.Dispose();
        _pendingCompaction = null;
    }

    private void RunAutomaticCompaction(ContextWindowCompactReason reason)
    {
        if (_phase != ContextWindowPhase.Idle || _layout is not { IsOver: true })
        {
            return;
        }
        if (ContextWindowBudget.FoldableCount(_items, KeepRecentMessages) <= 0)
        {
            SetStatus(Labels.NothingToFold, ContextWindowTone.Info);
            return;
        }
        BeginSummarizing(reason);
    }

    private void BeginSummarizing(ContextWindowCompactReason reason)
    {
        var count = ContextWindowBudget.FoldableCount(_items, KeepRecentMessages);
        if (count <= 0)
        {
            return;
        }
        _summarizingReason = reason;
        _menu?.Close(returnFocus: false);
        HoldFocus();
        Phase = ContextWindowPhase.Summarizing;
        UpdateState();
        if (_layout is { } layout)
        {
            _grid?.BeginFold(layout.MessageStart, layout.MessageCells);
        }
        var labels = Labels;
        var text = reason switch
        {
            ContextWindowCompactReason.Threshold => labels.FoldingPastThreshold(ThresholdPercent, count),
            ContextWindowCompactReason.ModelSwitch => labels.FoldingForModel(_model?.Name ?? string.Empty, count),
            _ => labels.Folding(count)
        };
        SetStatus(text, ContextWindowTone.Working);
        _pendingLanding?.Dispose();
        _pendingLanding = Timers.Start(ArcMotion.IsFull(this) ? SummarizingDuration : TimeSpan.Zero, () =>
        {
            _pendingLanding = null;
            Land();
        });
    }

    private void Land()
    {
        if (_phase != ContextWindowPhase.Summarizing)
        {
            return;
        }
        var result = ContextWindowBudget.Compact(_items, KeepRecentMessages, Summarizer, Labels.SummaryItem);
        _grid?.EndFold();
        Phase = ContextWindowPhase.Idle;
        if (result is null)
        {
            UpdateState();
            ReleaseFocus();
            return;
        }
        ReplaceItems(result.Items);
        Rebuild(ContextWindowWave.Insert);
        var layout = _layout!;
        var folded = ContextWindowFormat.Tokens(result.FoldedTokens);
        if (layout.IsOver)
        {
            SetStatus(Labels.StillOver(folded, ThresholdPercent), ContextWindowTone.Warning);
        }
        else
        {
            SetStatus(Labels.Folded(folded, ContextWindowFormat.Tokens(result.SummaryTokens)), ContextWindowTone.Success);
        }
        ReleaseFocus();
        RaiseEvent(new ContextWindowCompactedEventArgs(CompactedEvent, result.Folded, result.FoldedTokens, result.SummaryTokens,
            result.Before, result.After, _summarizingReason));
        RaiseItemsChanged(ContextWindowChange.Compacted, result.Summary);
    }

    /// <summary>Lands a compaction in flight now (tests).</summary>
    internal void LandNow()
    {
        _pendingLanding?.Dispose();
        _pendingLanding = null;
        Land();
    }

    /// <summary>
    /// Every action turns off while summarizing. Arc lets keyboard focus fall to the page here; this card keeps it on a
    /// hidden holder inside itself (named by the status) and hands it back when the compaction lands.
    /// </summary>
    private void HoldFocus()
    {
        if (_focusHolder is null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Control focused)
        {
            return;
        }
        if (!ReferenceEquals(focused, _pill) && !ReferenceEquals(focused, _send) && !ReferenceEquals(focused, _attach) &&
            !ReferenceEquals(focused, _compact))
        {
            return;
        }
        _restoreFocus = focused;
        _restoreKeyboard = focused.Classes.Contains(":focus-visible");
        _focusHolder.Focus(NavigationMethod.Unspecified);
    }

    private void ReleaseFocus()
    {
        var restore = _restoreFocus;
        _restoreFocus = null;
        if (_focusHolder is null || !_focusHolder.IsFocused)
        {
            return;
        }
        Control? target = restore is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } ? restore : null;
        target ??= FirstAvailable(_send, _attach, _compact, _pill);
        target?.Focus(_restoreKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);

        static Control? FirstAvailable(params Control?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (candidate is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
                {
                    return candidate;
                }
            }
            return null;
        }
    }

    #endregion

    #region Models

    private void OnSelectedModelChanged()
    {
        if (!_dataRead)
        {
            return;
        }
        var previous = _model;
        ReadModels();
        if (ReferenceEquals(previous, _model))
        {
            UpdateMenuChecks();
            return;
        }
        if (!_isBuilt)
        {
            return;
        }
        BuildMenu();
        UpdatePill();
        Rebuild(ContextWindowWave.Relayout);
        if (_phase == ContextWindowPhase.Idle && _model is { } model)
        {
            var layout = _layout!;
            if (layout.IsOver)
            {
                SetStatus(Labels.OverModel(ContextWindowFormat.Tokens(layout.Used), ThresholdPercent, model.Name), ContextWindowTone.Warning);
                if (AutoCompact)
                {
                    ScheduleCompaction(ContextWindowCompactReason.ModelSwitch, ModelCompactionDelay);
                }
            }
            else
            {
                SetStatus(Labels.Switched(model.Name, ContextWindowFormat.Tokens(model.Window)), ContextWindowTone.Info);
            }
        }
        RaiseEvent(new ContextWindowModelChangedEventArgs(ModelChangedEvent, previous, _model));
    }

    private void UpdatePill()
    {
        if (_pill is null)
        {
            return;
        }
        var model = _model;
        _pill.ModelName = model?.Name;
        _pill.WindowText = model is null ? null : ContextWindowFormat.Tokens(model.Window);
        _pill.Mark = model?.Mark;
        _pill.MarkBrush = model?.MarkBrush;
        AutomationProperties.SetName(_pill, model is null ? string.Empty : Labels.ModelPill(model.Name, _pill.WindowText!));
        _pill.IsVisible = model is not null;
    }

    private void EnsureMenu()
    {
        if (_menu is not null)
        {
            return;
        }
        _menu = new MenuSurface
        {
            Variant = MenuSurfaceVariant.Dropdown,
            Side = MenuSide.Bottom,
            Alignment = MenuAlignment.End,
            AnchorGap = 6
        };
        _menu.ItemClicked += OnMenuItemClicked;
        _menu.Opened += OnMenuOpened;
        _menu.Closed += OnMenuClosed;
    }

    private void BuildMenu()
    {
        if (_menu is null)
        {
            return;
        }
        AutomationProperties.SetName(_menu, Labels.ModelMenu);
        _menu.Items.Clear();
        foreach (var model in _models)
        {
            _menu.Items.Add(CreateEntry(model));
        }
    }

    private void UpdateMenuChecks()
    {
        if (_menu is null)
        {
            return;
        }
        foreach (var entry in _menu.Items.OfType<MenuEntry>())
        {
            var selected = ReferenceEquals(entry.Tag, _model);
            entry.IsChecked = selected;
            if (entry.Header is Control header)
            {
                header.Margin = new Thickness(0, 0, selected ? 0 : MenuCheckSlot, 0);
            }
        }
    }

    private MenuEntry CreateEntry(ContextWindowModel model)
    {
        var selected = ReferenceEquals(model, _model);
        var windowText = ContextWindowFormat.Tokens(model.Window);
        var name = new TextBlock
        {
            Text = model.Name,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var window = new TextBlock
        {
            Text = windowText,
            FontFeatures = ContextWindowTextLayouts.Tabular,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(window, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(window, 1);
        // Unchecked rows leave room for the check (its 16px glyph and 16px gap) so the windows line up.
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = MenuRowInset,
            Margin = new Thickness(0, 0, selected ? 0 : MenuCheckSlot, 0),
            Children = { name, window }
        };
        Control mark;
        if (model.Mark is { } geometry)
        {
            var glyph = new ArcGlyph { Data = geometry, Size = 16, StrokeThickness = 1.75 };
            if (model.MarkBrush is { } brush)
            {
                glyph.Foreground = brush;
            }
            else
            {
                ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
            }
            mark = glyph;
        }
        else
        {
            mark = new Panel { Width = 16, Height = 16 };
        }
        var entry = new MenuEntry
        {
            Header = header,
            Icon = mark,
            ToggleType = MenuEntryToggleType.Radio,
            GroupName = ModelGroup,
            IsChecked = selected,
            Tag = model,
            MinHeight = MenuRowHeight,
            Padding = new Thickness(MenuRowInset, 0)
        };
        AutomationProperties.SetName(entry, Labels.ModelOption(model.Name, windowText));
        return entry;
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is ContextWindowModel model && _phase == ContextWindowPhase.Idle)
        {
            SetCurrentValue(SelectedModelProperty, model);
            UpdateMenuChecks();
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        var keyboard = _pill?.IsKeyboardActivation ?? false;
        if (_pill is not null)
        {
            _pill.IsKeyboardActivation = false;
        }
        ToggleMenu(keyboard);
    }

    private void OnPillKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                if (_pill is not null)
                {
                    _pill.IsKeyboardActivation = true;
                }
                break;
            case Key.Down:
            case Key.Up:
                if (_menu is { IsOpen: false })
                {
                    e.Handled = true;
                    ToggleMenu(keyboard: true);
                }
                break;
        }
    }

    /// <summary>Opens the model menu (on the selected model when a key opened it), or closes it.</summary>
    internal void ToggleMenu(bool keyboard)
    {
        if (_menu is null || _pill is null || _phase != ContextWindowPhase.Idle || _models.Count == 0)
        {
            return;
        }
        if (_menu.IsOpen)
        {
            _menu.Close(returnFocus: keyboard);
            return;
        }
        _focusSelectedOnOpen = keyboard;
        _menu.Open(_pill, keyboard ? MenuOpenFocus.First : MenuOpenFocus.Surface);
    }

    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_pill is not null)
        {
            _pill.IsOpen = true;
        }
        if (_focusSelectedOnOpen)
        {
            _focusSelectedOnOpen = false;
            // After the menu put its highlight on the first row (same priority, queued after it).
            Dispatcher.UIThread.Post(FocusSelectedEntry, DispatcherPriority.Loaded);
        }
    }

    private void FocusSelectedEntry()
    {
        if (_menu is not { IsOpen: true })
        {
            return;
        }
        var entry = _menu.Items.OfType<MenuEntry>().FirstOrDefault(m => ReferenceEquals(m.Tag, _model));
        entry?.Focus(NavigationMethod.Directional);
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (_pill is not null)
        {
            _pill.IsOpen = false;
        }
    }

    #endregion

    #region Legend

    private void BuildLegend()
    {
        if (_legendHost is null)
        {
            return;
        }
        _legendHost.Children.Clear();
        for (var p = 0; p < _rows.Length; p++)
        {
            var part = (ContextWindowPart)p;
            var row = new ContextWindowLegendRow(part) { Label = PartName(part) };
            row.SetDivider(strong: part == ContextWindowPart.Messages, last: part == ContextWindowPart.Reserve);
            row.PointerEntered += OnRowPointerEntered;
            row.PointerExited += OnRowPointerExited;
            _rows[p] = row;
            _legendHost.Children.Add(row);
        }
        OnPaletteChanged();
        PushNumbers();
        _hovered = null;
    }

    private void OnPaletteChanged()
    {
        if (_grid is null)
        {
            return;
        }
        for (var p = 0; p < _rows.Length; p++)
        {
            if (_rows[p] is not { } row)
            {
                continue;
            }
            var part = (ContextWindowPart)p;
            if (part == ContextWindowPart.Reserve)
            {
                row.SwatchFill = Brushes.Transparent;
                row.SwatchOutline = new ImmutableSolidColorBrush(_grid.OutlineColor);
            }
            else
            {
                var kind = part == ContextWindowPart.Free ? ContextWindowCellKind.Free : (ContextWindowCellKind)p;
                row.SwatchFill = new ImmutableSolidColorBrush(_grid.ColorOf(kind));
                row.SwatchOutline = null;
            }
        }
    }

    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is ContextWindowLegendRow row && e.Pointer.Type != PointerType.Touch)
        {
            SetHover(row.Part);
        }
    }

    private void OnRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is ContextWindowLegendRow row && _hovered == row.Part)
        {
            SetHover(null);
        }
    }

    /// <summary>Lights one legend row's cells and quiets the other cells (0.28) and rows (0.45); null clears it.</summary>
    internal void SetHover(ContextWindowPart? part)
    {
        _hovered = part;
        _grid?.SetHover(part);
        foreach (var row in _rows)
        {
            if (row is not null)
            {
                row.IsDimmed = part is not null && row.Part != part;
            }
        }
    }

    #endregion
}

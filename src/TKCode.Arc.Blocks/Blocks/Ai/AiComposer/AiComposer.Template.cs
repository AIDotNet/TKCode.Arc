using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(TrayPart, typeof(AiAttachmentTray))]
[TemplatePart(EditorPart, typeof(TokenTextEditor))]
[TemplatePart(ToolbarPart, typeof(Grid))]
[TemplatePart(PlusPart, typeof(AiComposerIconButton))]
[TemplatePart(ToolsPart, typeof(Panel))]
[TemplatePart(CounterPart, typeof(AiCounter))]
[TemplatePart(ModelPillPart, typeof(ModelPill))]
[TemplatePart(SendPart, typeof(AiSendButton))]
[TemplatePart(DropHostPart, typeof(PresenceHost))]
[TemplatePart(DropTitlePart, typeof(TextBlock))]
[TemplatePart(DropHintPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(DraggingPseudoClass, ReadyPseudoClass, BlockedPseudoClass, GeneratingPseudoClass, OverLimitPseudoClass,
    CompactPseudoClass, NarrowPseudoClass, TinyPseudoClass, InlinePseudoClass)]
public partial class AiComposer
{
    public const string HostPart = "PART_Host";
    public const string SurfacePart = "PART_Surface";
    public const string TrayPart = "PART_Tray";
    public const string EditorPart = "PART_Editor";
    public const string ToolbarPart = "PART_Toolbar";
    public const string PlusPart = "PART_Plus";
    public const string ToolsPart = "PART_Tools";
    public const string CounterPart = "PART_Counter";
    public const string ModelPillPart = "PART_ModelPill";
    public const string SendPart = "PART_Send";
    public const string DropHostPart = "PART_DropHost";
    public const string DropTitlePart = "PART_DropTitle";
    public const string DropHintPart = "PART_DropHint";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while files are dragged over the composer: the "Drop to attach" cover shows.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while <see cref="SendState"/> is <see cref="AiSendState.Ready"/>.</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Set while <see cref="SendState"/> is <see cref="AiSendState.Blocked"/> (an upload runs, or the text is over the limit).</summary>
    public const string BlockedPseudoClass = ":blocked";

    /// <summary>Set while <see cref="IsGenerating"/>.</summary>
    public const string GeneratingPseudoClass = ":generating";

    /// <summary>Set while the text is longer than <see cref="MaxLength"/>.</summary>
    public const string OverLimitPseudoClass = ":over-limit";

    /// <summary>Set while the composer is narrower than 680: the tool toggles keep only their icons.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the composer is narrower than 560: the model pill drops the effort.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the composer is narrower than 400: tighter padding, the model pill shows its mark only, the counter its ring only.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while <see cref="Layout"/> is <see cref="AiComposerLayout.Inline"/> (the AI chat's single row).</summary>
    public const string InlinePseudoClass = ":inline";

    #region Token-driven internals

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(CompactWidth), 680);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(NarrowWidth), 560);

    internal static readonly StyledProperty<double> TinyWidthProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(TinyWidth), 400);

    internal static readonly StyledProperty<double> SuggestionShiftProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(SuggestionShift), -12);

    internal static readonly StyledProperty<double> MenuGapProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(MenuGap), 7.5);

    internal static readonly StyledProperty<TimeSpan> UploadDoneHoldProperty =
        AvaloniaProperty.Register<AiComposer, TimeSpan>(nameof(UploadDoneHold), AiFileIntake.DoneHold);

    internal static readonly StyledProperty<int> InlineMaxLinesProperty =
        AvaloniaProperty.Register<AiComposer, int>(nameof(InlineMaxLines), 7);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double TinyWidth { get => GetValue(TinyWidthProperty); set => SetValue(TinyWidthProperty, value); }
    internal double SuggestionShift { get => GetValue(SuggestionShiftProperty); set => SetValue(SuggestionShiftProperty, value); }
    internal double MenuGap { get => GetValue(MenuGapProperty); set => SetValue(MenuGapProperty, value); }
    internal TimeSpan UploadDoneHold { get => GetValue(UploadDoneHoldProperty); set => SetValue(UploadDoneHoldProperty, value); }
    internal int InlineMaxLines { get => GetValue(InlineMaxLinesProperty); set => SetValue(InlineMaxLinesProperty, value); }

    #endregion

    #region Seams (tests)

    /// <summary>Waits while a finished upload shows its check (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = AiFileIntake.DelayAsync;

    /// <summary>Opens the system file picker; null uses the top level's storage provider (tests replace it).</summary>
    internal Func<FilePickerOpenOptions, Task<IReadOnlyList<IStorageFile>>>? PickFilesAsync { get; set; }

    #endregion

    private Panel? _host;
    private Border? _surface;
    private AiAttachmentTray? _tray;
    private TokenTextEditor? _editor;
    private Grid? _toolbar;
    private AiComposerIconButton? _plus;
    private Panel? _toolsPanel;
    private AiCounter? _counter;
    private ModelPill? _modelPill;
    private AiSendButton? _send;
    private PresenceHost? _dropHost;
    private TextBlock? _dropTitle;
    private TextBlock? _dropHint;
    private AiAnnouncer? _announcer;

    private bool _isCompact;
    private bool _isNarrow;
    private bool _isTiny;
    private bool _keyboardMode;
    private bool _refocusInput;

    static AiComposer()
    {
        FocusableProperty.OverrideDefaultValue<AiComposer>(false);
    }

    /// <summary>Creates an empty composer.</summary>
    public AiComposer()
    {
        // Where the last input came from decides whether a menu opened from a button starts on its first row.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        Commands.CollectionChanged += OnSuggestionSourceChanged;
        ContextItems.CollectionChanged += OnSuggestionSourceChanged;
        MentionGroups.CollectionChanged += OnSuggestionSourceChanged;
        Attachments.CollectionChanged += OnAttachmentsChanged;
        Tools.CollectionChanged += OnToolsChanged;
        Models.CollectionChanged += OnModelsChanged;
        Providers.CollectionChanged += OnProvidersChanged;
        InitializeDrop();
        InitializeModelMenu();
    }

    #region Test surface

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    internal TokenTextEditor? Editor => _editor;
    internal Border? Surface => _surface;
    internal Grid? Toolbar => _toolbar;
    internal AiAttachmentTray? Tray => _tray;
    internal AiComposerIconButton? PlusButton => _plus;
    internal Panel? ToolsPanel => _toolsPanel;
    internal AiCounter? Counter => _counter;
    internal ModelPill? ModelPillControl => _modelPill;
    internal AiSendButton? SendButton => _send;
    internal PresenceHost? DropHost => _dropHost;
    internal TextBlock? DropTitle => _dropTitle;
    internal TextBlock? DropHint => _dropHint;
    internal bool IsCompact => _isCompact;
    internal bool IsNarrow => _isNarrow;
    internal bool IsTiny => _isTiny;

    /// <summary>Whether the chat's single row is shown.</summary>
    internal bool IsInline => Layout == AiComposerLayout.Inline;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        // A swap of layouts rebuilds the parts: the focus in the old text area moves to the new one.
        var refocus = _refocusInput || _editor is { IsKeyboardFocusWithin: true };
        _refocusInput = false;
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _host = scope.Find<Panel>(HostPart);
        _surface = scope.Find<Border>(SurfacePart);
        _tray = scope.Find<AiAttachmentTray>(TrayPart);
        _editor = scope.Find<TokenTextEditor>(EditorPart);
        _toolbar = scope.Find<Grid>(ToolbarPart);
        _plus = scope.Find<AiComposerIconButton>(PlusPart);
        _toolsPanel = scope.Find<Panel>(ToolsPart);
        _counter = scope.Find<AiCounter>(CounterPart);
        _modelPill = scope.Find<ModelPill>(ModelPillPart);
        _send = scope.Find<AiSendButton>(SendPart);
        _dropHost = scope.Find<PresenceHost>(DropHostPart);
        _dropTitle = scope.Find<TextBlock>(DropTitlePart);
        _dropHint = scope.Find<TextBlock>(DropHintPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        SetUpEditor();
        if (_tray is not null)
        {
            _tray.ItemsSource = Attachments;
        }
        AttachModelMenu();
        RebuildTools();
        ApplyLabels();
        UpdateTriggers();
        UpdateEditorLimits();
        UpdateCounter();
        UpdateAttachmentParts();
        UpdateSendState();
        UpdateModelParts();
        ApplyBreakpoints();
        UpdateBreakpoints(Bounds.Width);
        if (refocus)
        {
            Post(() => FocusInput(FocusMethod));
        }
    }

    private void HookParts()
    {
        if (_surface is not null)
        {
            _surface.AddHandler(PointerPressedEvent, OnSurfacePointerPressed);
            _surface.SizeChanged += OnSurfaceSizeChanged;
        }
        if (_plus is not null)
        {
            _plus.Click += OnPlusClick;
            _plus.AddHandler(KeyDownEvent, OnPlusKeyDown);
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_tray is not null)
        {
            _tray.RemoveRequested += OnChipRemoveRequested;
            _tray.CancelRequested += OnChipRemoveRequested;
        }
        HookEditor();
        HookModelPill();
    }

    private void UnhookParts()
    {
        if (_surface is not null)
        {
            _surface.RemoveHandler(PointerPressedEvent, OnSurfacePointerPressed);
            _surface.SizeChanged -= OnSurfaceSizeChanged;
        }
        if (_plus is not null)
        {
            _plus.Click -= OnPlusClick;
            _plus.RemoveHandler(KeyDownEvent, OnPlusKeyDown);
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_tray is not null)
        {
            _tray.RemoveRequested -= OnChipRemoveRequested;
            _tray.CancelRequested -= OnChipRemoveRequested;
            _tray.ItemsSource = null;
        }
        UnhookEditor();
        UnhookModelPill();
        DetachModelMenu();
        ClearToolViews();
        CloseSuggestions(dismiss: false, immediate: true);
        _attachMenu?.Close(returnFocus: false);
    }

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        if (_editor is not null)
        {
            _editor.Placeholder = Placeholder ?? labels.Placeholder;
            AutomationProperties.SetName(_editor, AccessibleName ?? labels.Message);
            AutomationProperties.SetHelpText(_editor, labels.KeyHint);
        }
        if (_plus is not null)
        {
            AutomationProperties.SetName(_plus, labels.Attach);
            Tooltip.SetTip(_plus, labels.Attach);
        }
        if (_tray is not null)
        {
            AutomationProperties.SetName(_tray, labels.Attachments);
            _tray.RemoveText = labels.RemoveAttachment;
            _tray.CancelText = labels.CancelUpload;
            _tray.UploadingText = labels.Uploading;
            _tray.ErrorText = labels.UploadFailed;
            _tray.ImageText = labels.ImageType;
            _tray.LinesText = labels.PastedLines;
        }
        if (_counter is not null)
        {
            _counter.LeftText = labels.CharactersLeft;
            _counter.OverText = labels.CharactersOver;
            _counter.Tip = labels.CounterTip;
        }
        if (_dropTitle is not null)
        {
            _dropTitle.Text = labels.DropTitle;
        }
        if (_dropHint is not null)
        {
            _dropHint.Text = labels.DropHint(MaxFileSize ?? 0);
        }
        if (_suggest is not null)
        {
            _suggest.Pop.CommandListName = labels.CommandList;
            _suggest.Pop.MentionListName = labels.MentionList;
        }
        UpdateSendNames();
        UpdateModelLabels();
    }

    /// <summary>
    /// The theme swaps the template on <c>:inline</c>; the parts of the new one are wired in <see cref="OnApplyTemplate"/>.
    /// Menus and lists the old layout had open close, and the triggers and the line limit follow the layout.
    /// </summary>
    private void OnLayoutChanged()
    {
        // The old text area leaves the tree (and loses the focus) before the new template is applied.
        _refocusInput = _editor is { IsKeyboardFocusWithin: true };
        PseudoClasses.Set(InlinePseudoClass, IsInline);
        CloseSuggestions(dismiss: false, immediate: true);
        _attachMenu?.Close(returnFocus: false);
        _menu.Close(returnFocus: false);
        UpdateTriggers();
        UpdateEditorLimits();
    }

    #endregion

    #region Lifetime and size

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchTools(true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchTools(false);
        CloseSuggestions(dismiss: false, immediate: true);
        _attachMenu?.Close(returnFocus: false);
        _menu.Close(returnFocus: false);
        SetDragging(false);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateBreakpoints(e.NewSize.Width);
        }
    }

    /// <summary>The breakpoints follow the composer's own width (a side panel or a narrow column), never the window's.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width < CompactWidth - 0.01;
        var narrow = width < NarrowWidth - 0.01;
        var tiny = width < TinyWidth - 0.01;
        if (compact == _isCompact && narrow == _isNarrow && tiny == _isTiny)
        {
            return;
        }
        _isCompact = compact;
        _isNarrow = narrow;
        _isTiny = tiny;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(TinyPseudoClass, tiny);
        ApplyBreakpoints();
    }

    /// <summary>
    /// Switches the parts at once (no width springs on a breakpoint): icon-only tool toggles, the model pill without its
    /// effort or text, the counter without its text.
    /// </summary>
    private void ApplyBreakpoints()
    {
        foreach (var view in _toolViews)
        {
            view.Toggle.IsCompact = _isCompact;
        }
        if (_modelPill is not null)
        {
            _modelPill.ShowEffort = !_isNarrow;
            _modelPill.ShowText = !_isTiny;
        }
        if (_counter is not null)
        {
            _counter.IsTiny = _isTiny;
        }
    }

    #endregion

    #region Input source and focus

    /// <summary>How a focus move shows: with the keyboard ring after a key, without it after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    /// <summary>
    /// A press on the surface's own padding (around the text, not on the toolbar or a chip) puts the caret in the text,
    /// as clicking anywhere in Arc's prompt box does.
    /// </summary>
    private void OnSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _surface) || _editor is null)
        {
            return;
        }
        if (e.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            FocusInput(NavigationMethod.Pointer);
        }
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    private void Announce(string? text) => _announcer?.Announce(text);

    /// <summary>Runs <paramref name="action"/> once the current input has settled.</summary>
    private static void Post(Action action) => Dispatcher.UIThread.Post(action);

    #endregion

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty)
        {
            OnTextChanged(change.GetNewValue<string>() ?? string.Empty);
        }
        else if (property == ValueProperty)
        {
            OnValueChanged(change.GetNewValue<AiComposerValue>() ?? AiComposerValue.Empty);
        }
        else if (property == PlaceholderProperty || property == AccessibleNameProperty || property == LabelsProperty)
        {
            ApplyLabels();
            if (property == LabelsProperty)
            {
                RefreshSuggestions();
            }
        }
        else if (property == MinLinesProperty || property == MaxLinesProperty)
        {
            UpdateEditorLimits();
        }
        else if (property == MaxLengthProperty || property == CounterThresholdProperty || property == WarningThresholdProperty)
        {
            UpdateCounter();
            UpdateSendState();
        }
        else if (property == MaxFileSizeProperty)
        {
            ApplyLabels();
        }
        else if (property == IsAttachmentEnabledProperty)
        {
            UpdateAttachmentParts();
        }
        else if (property == LayoutProperty)
        {
            OnLayoutChanged();
        }
        else if (property == InlineMaxLinesProperty)
        {
            UpdateEditorLimits();
        }
        else if (property == SelectedModelProperty)
        {
            OnSelectedModelChanged(change.GetOldValue<AiModel?>(), change.GetNewValue<AiModel?>());
        }
        else if (property == EffortProperty)
        {
            OnEffortChanged(change.GetOldValue<ModelEffort?>(), change.GetNewValue<ModelEffort?>());
        }
        else if (property == IsGeneratingProperty)
        {
            UpdateSendState();
        }
        else if (property == GenerationProgressProperty)
        {
            if (_send is not null)
            {
                _send.Progress = change.GetNewValue<double>();
            }
        }
        else if (property == CompactWidthProperty || property == NarrowWidthProperty || property == TinyWidthProperty)
        {
            _isCompact = _isNarrow = _isTiny = false;
            PseudoClasses.Set(CompactPseudoClass, false);
            PseudoClasses.Set(NarrowPseudoClass, false);
            PseudoClasses.Set(TinyPseudoClass, false);
            ApplyBreakpoints();
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == SuggestionShiftProperty && _suggest is not null)
        {
            _suggest.HorizontalShift = change.GetNewValue<double>();
        }
        else if (property == MenuGapProperty && _attachMenu is not null)
        {
            _attachMenu.AnchorGap = change.GetNewValue<double>();
        }
    }

    #endregion
}

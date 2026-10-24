using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

[TemplatePart(HeaderPart, typeof(Border))]
[TemplatePart(TitlePillPart, typeof(AiConversationMenuButton))]
[TemplatePart(ModelPillPart, typeof(ModelPill))]
[TemplatePart(ModelMenuPart, typeof(ModelMenu))]
[TemplatePart(NewChatPart, typeof(AiChatIconButton))]
[TemplatePart(PanePart, typeof(Control))]
[TemplatePart(ScrollerPart, typeof(AiFollowScroller))]
[TemplatePart(ThreadPart, typeof(Border))]
[TemplatePart(TurnsPart, typeof(AiChatThreadPanel))]
[TemplatePart(EmptyPart, typeof(AiChatEmptyState))]
[TemplatePart(DockPart, typeof(Border))]
[TemplatePart(ComposerPart, typeof(AiComposer))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(EmptyPseudoClass, BusyPseudoClass, NarrowPseudoClass, CompactPseudoClass)]
public partial class AiChat
{
    public const string HeaderPart = "PART_Header";
    public const string TitlePillPart = "PART_TitlePill";
    public const string ModelPillPart = "PART_ModelPill";
    public const string ModelMenuPart = "PART_ModelMenu";
    public const string NewChatPart = "PART_NewChat";
    public const string PanePart = "PART_Pane";
    public const string ScrollerPart = "PART_Scroller";
    public const string ThreadPart = "PART_Thread";
    public const string TurnsPart = "PART_Turns";
    public const string EmptyPart = "PART_Empty";
    public const string DockPart = "PART_Dock";
    public const string ComposerPart = "PART_Composer";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the shown conversation has no turns (or there is none): the greeting and the suggestions show.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while <see cref="IsBusy"/>.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while the chat is 480 wide or less: tighter thread, wider bubbles, source rows without domains.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the chat is 360 wide or less: a tighter model pill.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(NarrowWidth), 480);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(CompactWidth), 360);

    internal static readonly StyledProperty<double> ThreadSideRatioProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ThreadSideRatio), 0.045);

    internal static readonly StyledProperty<double> ThreadSideMinProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ThreadSideMin), 16);

    internal static readonly StyledProperty<double> ThreadSideMaxProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ThreadSideMax), 28);

    internal static readonly StyledProperty<double> ThreadTopPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ThreadTopPadding), 24);

    internal static readonly StyledProperty<double> NarrowThreadTopPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(NarrowThreadTopPadding), 18);

    internal static readonly StyledProperty<double> ThreadBottomPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ThreadBottomPadding), 12);

    internal static readonly StyledProperty<double> HeaderSideOffsetProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(HeaderSideOffset), 10);

    internal static readonly StyledProperty<double> HeaderSideMinProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(HeaderSideMin), 6);

    internal static readonly StyledProperty<double> HeaderEndPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(HeaderEndPadding), 10);

    internal static readonly StyledProperty<double> DockSideOffsetProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(DockSideOffset), 8);

    internal static readonly StyledProperty<double> DockSideMinProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(DockSideMin), 8);

    internal static readonly StyledProperty<double> DockTopPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(DockTopPadding), 6);

    internal static readonly StyledProperty<double> DockBottomPaddingProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(DockBottomPadding), 12);

    internal static readonly StyledProperty<double> PaneEnterRiseProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(PaneEnterRise), 4);

    internal static readonly StyledProperty<int> InitialTurnsProperty =
        AvaloniaProperty.Register<AiChat, int>(nameof(InitialTurns), 30);

    internal static readonly StyledProperty<int> EarlierTurnsProperty =
        AvaloniaProperty.Register<AiChat, int>(nameof(EarlierTurns), 20);

    internal static readonly StyledProperty<double> TurnGapProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(TurnGap), 28);

    internal static readonly StyledProperty<double> NarrowTurnGapProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(NarrowTurnGap), 24);

    internal static readonly StyledProperty<Thickness> CompactModelPillPaddingProperty =
        AvaloniaProperty.Register<AiChat, Thickness>(nameof(CompactModelPillPadding), new Thickness(8, 0, 6, 0));

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double ThreadSideRatio { get => GetValue(ThreadSideRatioProperty); set => SetValue(ThreadSideRatioProperty, value); }
    internal double ThreadSideMin { get => GetValue(ThreadSideMinProperty); set => SetValue(ThreadSideMinProperty, value); }
    internal double ThreadSideMax { get => GetValue(ThreadSideMaxProperty); set => SetValue(ThreadSideMaxProperty, value); }
    internal double ThreadTopPadding { get => GetValue(ThreadTopPaddingProperty); set => SetValue(ThreadTopPaddingProperty, value); }
    internal double NarrowThreadTopPadding { get => GetValue(NarrowThreadTopPaddingProperty); set => SetValue(NarrowThreadTopPaddingProperty, value); }
    internal double ThreadBottomPadding { get => GetValue(ThreadBottomPaddingProperty); set => SetValue(ThreadBottomPaddingProperty, value); }
    internal double HeaderSideOffset { get => GetValue(HeaderSideOffsetProperty); set => SetValue(HeaderSideOffsetProperty, value); }
    internal double HeaderSideMin { get => GetValue(HeaderSideMinProperty); set => SetValue(HeaderSideMinProperty, value); }
    internal double HeaderEndPadding { get => GetValue(HeaderEndPaddingProperty); set => SetValue(HeaderEndPaddingProperty, value); }
    internal double DockSideOffset { get => GetValue(DockSideOffsetProperty); set => SetValue(DockSideOffsetProperty, value); }
    internal double DockSideMin { get => GetValue(DockSideMinProperty); set => SetValue(DockSideMinProperty, value); }
    internal double DockTopPadding { get => GetValue(DockTopPaddingProperty); set => SetValue(DockTopPaddingProperty, value); }
    internal double DockBottomPadding { get => GetValue(DockBottomPaddingProperty); set => SetValue(DockBottomPaddingProperty, value); }
    internal double PaneEnterRise { get => GetValue(PaneEnterRiseProperty); set => SetValue(PaneEnterRiseProperty, value); }
    internal int InitialTurns { get => GetValue(InitialTurnsProperty); set => SetValue(InitialTurnsProperty, value); }
    internal int EarlierTurns { get => GetValue(EarlierTurnsProperty); set => SetValue(EarlierTurnsProperty, value); }
    internal double TurnGap { get => GetValue(TurnGapProperty); set => SetValue(TurnGapProperty, value); }
    internal double NarrowTurnGap { get => GetValue(NarrowTurnGapProperty); set => SetValue(NarrowTurnGapProperty, value); }
    internal Thickness CompactModelPillPadding { get => GetValue(CompactModelPillPaddingProperty); set => SetValue(CompactModelPillPaddingProperty, value); }

    #endregion

    #region Seams (tests)

    /// <summary>The clock of the measured thinking time (tests give their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>Writes copied text to the clipboard (tests replace it).</summary>
    internal Func<string, Task> CopyTextAsync { get; set; }

    /// <summary>Opens a source nobody handled (tests replace it); the default uses the top level's launcher.</summary>
    internal Action<Uri> OpenUri { get; set; }

    #endregion

    private Border? _header;
    private AiConversationMenuButton? _titlePill;
    private ModelPill? _modelPill;
    private ModelMenu? _modelMenu;
    private AiChatIconButton? _newChat;
    private Control? _pane;
    private AiFollowScroller? _scroller;
    private Border? _threadHost;
    private AiChatThreadPanel? _turns;
    private AiChatEmptyState? _empty;
    private Border? _dock;
    private AiComposer? _composer;
    private AiAnnouncer? _announcer;
    private AiFadePresence? _paneFade;
    private bool _isNarrow;
    private bool _isCompact;
    private bool _syncingModel;
    private bool _keyboardMode;
    private bool _isTouch;

    static AiChat()
    {
        FocusableProperty.OverrideDefaultValue<AiChat>(false);
    }

    /// <summary>Creates an empty chat.</summary>
    public AiChat()
    {
        CopyTextAsync = text => ArcClipboard.CopyTextAsync(this, text);
        OpenUri = uri =>
        {
            if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(uri);
            }
        };
        // Where the last input came from decides whether a focus move shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            _keyboardMode = false;
            SetTouch(e.Pointer.Type == PointerType.Touch);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (e.Pointer.Type == PointerType.Mouse)
            {
                SetTouch(false);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        Models.CollectionChanged += OnModelsChanged;
        Providers.CollectionChanged += OnProvidersChanged;
        Suggestions.CollectionChanged += OnSuggestionsChanged;
        Conversations.CollectionChanged += OnConversationsChanged;
        AddHandler(AiReplyText.RevealCompletedEvent, OnRevealCompleted);
        AddHandler(AiReplyText.CitationInvokedEvent, OnReplyCitationInvoked);
        AddHandler(AiToolDetail.SourceInvokedEvent, OnToolSourceInvoked);
        AddHandler(AiMessageActions.CopyRequestedEvent, OnReplyCopyRequested);
        AddHandler(AiMessageActions.CopiedEvent, OnReplyCopied);
        AddHandler(AiMessageActions.RetryRequestedEvent, OnRetryRequested);
        AddHandler(AiMessageActions.FeedbackRequestedEvent, OnFeedbackRequested);
        AddHandler(AiStatusNote.ActionRequestedEvent, OnStatusActionRequested);
        AddHandler(AiVersionPager.IndexChangedEvent, OnVersionPagerIndexChanged);
        AddHandler(AiUserBubble.CopyRequestedEvent, OnUserCopyRequested);
        AddHandler(AiUserBubble.EditSubmittedEvent, OnEditSubmitted);
    }

    #region Test surface

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    internal Border? Header => _header;
    internal AiConversationMenuButton? TitlePill => _titlePill;
    internal ModelPill? ModelPillControl => _modelPill;
    internal ModelMenu? ModelMenuControl => _modelMenu;
    internal AiChatIconButton? NewChatButton => _newChat;
    internal Control? Pane => _pane;
    internal AiFadePresence? PaneFade => _paneFade;
    internal AiFollowScroller? Scroller => _scroller;
    internal Border? ThreadHost => _threadHost;
    internal AiChatThreadPanel? TurnsPanel => _turns;
    internal AiChatEmptyState? EmptyState => _empty;
    internal Border? Dock => _dock;
    internal AiComposer? Composer => _composer;

    /// <summary>Whether the chat is narrow now (480 or less).</summary>
    internal bool IsNarrowChat => _isNarrow;

    /// <summary>Whether the chat is compact now (360 or less).</summary>
    internal bool IsCompactChat => _isCompact;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _header = scope.Find<Border>(HeaderPart);
        _titlePill = scope.Find<AiConversationMenuButton>(TitlePillPart);
        _modelPill = scope.Find<ModelPill>(ModelPillPart);
        _modelMenu = scope.Find<ModelMenu>(ModelMenuPart);
        _newChat = scope.Find<AiChatIconButton>(NewChatPart);
        _pane = scope.Find<Control>(PanePart);
        _scroller = scope.Find<AiFollowScroller>(ScrollerPart);
        _threadHost = scope.Find<Border>(ThreadPart);
        _turns = scope.Find<AiChatThreadPanel>(TurnsPart);
        _empty = scope.Find<AiChatEmptyState>(EmptyPart);
        _dock = scope.Find<Border>(DockPart);
        _composer = scope.Find<AiComposer>(ComposerPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        ApplyLabels();
        SyncModelParts();
        if (_empty is not null)
        {
            _empty.Suggestions = Suggestions;
        }
        if (_composer is not null)
        {
            _composer.IsAttachmentEnabled = IsAttachmentEnabled;
            _composer.AcceptedFileTypes = AcceptedFileTypes;
        }
        UpdateBreakpoints(Bounds.Width);
        ApplyBreakpointParts();
        ApplyMetrics(Bounds.Width);
        ShowConversationNow();
    }

    private void HookParts()
    {
        if (_titlePill is not null)
        {
            _titlePill.Conversations = Conversations;
            _titlePill.Current = CurrentConversation;
            _titlePill.ConversationChosen += OnConversationChosen;
            _titlePill.NewChatRequested += OnMenuNewChatRequested;
        }
        if (_modelPill is not null)
        {
            _modelPill.OpenRequested += OnModelPillOpenRequested;
        }
        if (_modelMenu is not null)
        {
            _modelMenu.ModelsSource = Models;
            _modelMenu.PlacementTarget = _modelPill;
            _modelMenu.ModelChosen += OnModelChosen;
            _modelMenu.PropertyChanged += OnModelMenuPropertyChanged;
        }
        if (_newChat is not null)
        {
            _newChat.Click += OnNewChatClick;
        }
        if (_scroller is not null)
        {
            _scroller.FollowingChanged += OnFollowingChanged;
            if (_scroller.Scroll is null)
            {
                _scroller.ApplyTemplate();
            }
            if (_scroller.Scroll is { } scroll)
            {
                scroll.ScrollChanged += OnThreadScrollChanged;
            }
        }
        if (_empty is not null)
        {
            _empty.SuggestionInvoked += OnSuggestionInvoked;
        }
        if (_composer is not null)
        {
            _composer.Sent += OnComposerSent;
            _composer.StopRequested += OnComposerStopRequested;
        }
        if (_pane is not null)
        {
            _paneFade = new AiFadePresence(_pane, shown: true, PaneEnterRise);
            _paneFade.Exited += OnPaneExited;
        }
    }

    private void UnhookParts()
    {
        if (_titlePill is not null)
        {
            _titlePill.ConversationChosen -= OnConversationChosen;
            _titlePill.NewChatRequested -= OnMenuNewChatRequested;
            _titlePill.Conversations = null;
        }
        if (_modelPill is not null)
        {
            _modelPill.OpenRequested -= OnModelPillOpenRequested;
        }
        if (_modelMenu is not null)
        {
            _modelMenu.ModelChosen -= OnModelChosen;
            _modelMenu.PropertyChanged -= OnModelMenuPropertyChanged;
            _modelMenu.Close(returnFocus: false);
            _modelMenu.ModelsSource = null;
        }
        if (_newChat is not null)
        {
            _newChat.Click -= OnNewChatClick;
        }
        if (_scroller is not null)
        {
            _scroller.FollowingChanged -= OnFollowingChanged;
            if (_scroller.Scroll is { } scroll)
            {
                scroll.ScrollChanged -= OnThreadScrollChanged;
            }
        }
        if (_empty is not null)
        {
            _empty.SuggestionInvoked -= OnSuggestionInvoked;
        }
        if (_composer is not null)
        {
            _composer.Sent -= OnComposerSent;
            _composer.StopRequested -= OnComposerStopRequested;
        }
        if (_paneFade is not null)
        {
            _paneFade.Exited -= OnPaneExited;
            _paneFade.Dispose();
            _paneFade = null;
        }
        ClearThread();
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    private void Announce(string? text) => _announcer?.Announce(text);

    /// <summary>
    /// Touch has no hover (Arc's <c>(hover: none)</c>): after a touch the message actions show on every turn; a mouse moving
    /// over the chat brings the hover rule back.
    /// </summary>
    internal void SetTouch(bool touch)
    {
        if (touch == _isTouch)
        {
            return;
        }
        _isTouch = touch;
        foreach (var view in _views.Values)
        {
            view.IsTouch = touch;
        }
    }

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.Chat);
        if (_titlePill is not null)
        {
            _titlePill.NewChatText = labels.NewChat;
            _titlePill.UntitledText = labels.UntitledChat;
            _titlePill.NameFormat = labels.Chats;
        }
        if (_modelPill is not null)
        {
            _modelPill.Labels = new ModelPillLabels
            {
                AutomationName = (model, _) => labels.Model(model.Name),
                AutomationNameWithoutEffort = model => labels.Model(model.Name),
                AutomationNameEmpty = text => text
            };
        }
        if (_newChat is not null)
        {
            AutomationProperties.SetName(_newChat, labels.NewChat);
            Tooltip.SetTip(_newChat, labels.NewChat);
        }
        if (_scroller is not null)
        {
            _scroller.JumpButtonName = labels.ScrollToLatest;
        }
        if (_turns is not null)
        {
            AutomationProperties.SetName(_turns, labels.Messages);
        }
        if (_empty is not null)
        {
            _empty.Greeting = GreetingText ?? labels.Greeting;
        }
        if (_composer is not null)
        {
            _composer.Placeholder = PlaceholderText ?? labels.Placeholder;
            _composer.AccessibleName = labels.Message;
            _composer.Labels = ComposerLabels(labels);
        }
        UpdateTitle();
        foreach (var view in _views.Values)
        {
            view.ApplyLabels();
        }
    }

    /// <summary>
    /// The message box's words: the chat's own for what the chat shows (placeholder, name, send, stop, attach, remove), the
    /// rest from <see cref="AiChatLabels.Composer"/>. The box's own "sent" and "stopped" announcements are silenced: the
    /// chat announces the reply instead.
    /// </summary>
    private static AiComposerLabels ComposerLabels(AiChatLabels labels)
    {
        var b = labels.Composer ?? new AiComposerLabels();
        return new AiComposerLabels
        {
            Placeholder = labels.Placeholder,
            Message = labels.Message,
            Send = labels.SendMessage,
            Stop = labels.StopReply,
            SendEmpty = labels.SendMessage,
            SendUploading = b.SendUploading,
            SendOverLimit = b.SendOverLimit,
            Attach = labels.AttachFiles,
            UploadFile = b.UploadFile,
            MentionEntry = b.MentionEntry,
            PickerTitle = labels.AttachFiles,
            Attachments = labels.Attachments,
            Uploading = b.Uploading,
            CancelUpload = b.CancelUpload,
            RemoveAttachment = labels.Remove,
            TooLarge = b.TooLarge,
            TypeNotAccepted = b.TypeNotAccepted,
            UploadFailed = b.UploadFailed,
            PastedText = b.PastedText,
            PastedLines = b.PastedLines,
            DropTitle = b.DropTitle,
            DropHint = b.DropHint,
            CharactersLeft = b.CharactersLeft,
            CharactersOver = b.CharactersOver,
            CounterTip = b.CounterTip,
            ModelName = b.ModelName,
            EffortName = b.EffortName,
            ModelMenu = b.ModelMenu,
            CommandList = b.CommandList,
            MentionList = b.MentionList,
            FilesGroup = b.FilesGroup,
            DocsGroup = b.DocsGroup,
            KeyHint = labels.KeyHint,
            ImageType = b.ImageType,
            AnnounceTool = b.AnnounceTool,
            AnnounceModel = b.AnnounceModel,
            AnnounceEffort = b.AnnounceEffort,
            AnnounceUploaded = b.AnnounceUploaded,
            AnnounceUploadFailed = b.AnnounceUploadFailed,
            AnnounceRemoved = b.AnnounceRemoved,
            AnnouncePasted = b.AnnouncePasted,
            AnnounceSuggestions = b.AnnounceSuggestions,
            AnnounceStopped = string.Empty,
            AnnounceSent = string.Empty
        };
    }

    /// <summary>The title pill: the conversation's title, its first six words, "Untitled chat", or "New chat" without one.</summary>
    private void UpdateTitle()
    {
        if (_titlePill is null)
        {
            return;
        }
        var conversation = CurrentConversation;
        _titlePill.Title = conversation is null ? Labels.NewChat : AiConversationTitle.Of(conversation, Labels.UntitledChat);
    }

    /// <summary>The title of <paramref name="conversation"/> as the menu and the announcements say it.</summary>
    private string TitleOf(AiChatConversation conversation) => AiConversationTitle.Of(conversation, Labels.UntitledChat);

    #endregion

    #region Size

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateBreakpoints(e.NewSize.Width);
            ApplyMetrics(e.NewSize.Width);
        }
    }

    /// <summary>The breakpoints follow the chat's own width (a side panel, a card), never the window's.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var narrow = width <= NarrowWidth + 0.01;
        var compact = width <= CompactWidth + 0.01;
        if (narrow == _isNarrow && compact == _isCompact)
        {
            return;
        }
        _isNarrow = narrow;
        _isCompact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var view in _views.Values)
        {
            view.IsNarrow = narrow;
        }
        ApplyBreakpointParts();
    }

    /// <summary>The turn gap (24 when narrow) and the compact model pill's padding (0 6 0 8).</summary>
    private void ApplyBreakpointParts()
    {
        if (_turns is not null)
        {
            _turns.Spacing = _isNarrow ? NarrowTurnGap : TurnGap;
        }
        if (_modelPill is not null)
        {
            if (_isCompact)
            {
                _modelPill.Padding = CompactModelPillPadding;
            }
            else
            {
                _modelPill.ClearValue(PaddingProperty);
            }
        }
    }

    /// <summary>
    /// The side paddings follow the width continuously (Arc 7): the thread's is 4.5% of the chat, between 16 and 28; the
    /// header's left is 10 less (at least 6), the composer dock's 8 less (at least 8). The thread is centered and at most
    /// <see cref="ColumnWidth"/> plus its paddings wide; so is the dock.
    /// </summary>
    private void ApplyMetrics(double width)
    {
        var side = Math.Clamp(width > 0 ? width * ThreadSideRatio : ThreadSideMax, ThreadSideMin, ThreadSideMax);
        var maxWidth = ColumnWidth + 2 * side;
        if (_threadHost is not null)
        {
            var top = _isNarrow ? NarrowThreadTopPadding : ThreadTopPadding;
            _threadHost.Padding = new Thickness(side, top, side, ThreadBottomPadding);
            _threadHost.MaxWidth = maxWidth;
        }
        if (_header is not null)
        {
            _header.Padding = new Thickness(Math.Max(HeaderSideMin, side - HeaderSideOffset), 0, HeaderEndPadding, 0);
        }
        if (_dock is not null)
        {
            var dockSide = Math.Max(DockSideMin, side - DockSideOffset);
            _dock.Padding = new Thickness(dockSide, DockTopPadding, dockSide, DockBottomPadding);
            _dock.MaxWidth = maxWidth;
        }
    }

    #endregion

    #region Lifetime

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A chat that leaves the window stops its replies (Arc's unmount aborts the stream).
        CancelAllRuns();
        _modelMenu?.Close(returnFocus: false);
        // The conversations are app data that can outlive the chat: they are followed only while it is in a window.
        Unwatch();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_turns is not null)
        {
            // Catch up with what changed while away (without motion) and follow the data again.
            RefreshBranch(animate: false);
            UpdateTitle();
            UpdateBusy();
        }
        // Replies asked for while away start once the chat and its views are in the window.
        if (_pendingReplies.Count > 0)
        {
            Dispatcher.UIThread.Post(StartPendingReplies, DispatcherPriority.Loaded);
        }
    }

    #endregion

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CurrentConversationProperty)
        {
            OnCurrentConversationChanged(change.GetOldValue<AiChatConversation?>(), change.GetNewValue<AiChatConversation?>());
        }
        else if (property == LabelsProperty || property == GreetingTextProperty || property == PlaceholderTextProperty)
        {
            ApplyLabels();
        }
        else if (property == SelectedModelProperty)
        {
            SyncModelParts();
        }
        else if (property == IsAttachmentEnabledProperty)
        {
            if (_composer is not null)
            {
                _composer.IsAttachmentEnabled = change.GetNewValue<bool>();
            }
        }
        else if (property == AcceptedFileTypesProperty)
        {
            if (_composer is not null)
            {
                _composer.AcceptedFileTypes = change.GetNewValue<string?>();
            }
        }
        else if (property == ColumnWidthProperty || property == ThreadSideRatioProperty || property == ThreadSideMinProperty ||
                 property == ThreadSideMaxProperty || property == ThreadTopPaddingProperty || property == NarrowThreadTopPaddingProperty ||
                 property == ThreadBottomPaddingProperty || property == HeaderSideOffsetProperty || property == HeaderSideMinProperty ||
                 property == HeaderEndPaddingProperty || property == DockSideOffsetProperty || property == DockSideMinProperty ||
                 property == DockTopPaddingProperty || property == DockBottomPaddingProperty)
        {
            ApplyMetrics(Bounds.Width);
        }
        else if (property == TurnGapProperty || property == NarrowTurnGapProperty || property == CompactModelPillPaddingProperty)
        {
            ApplyBreakpointParts();
        }
        else if (property == NarrowWidthProperty || property == CompactWidthProperty)
        {
            _isNarrow = _isCompact = false;
            PseudoClasses.Set(NarrowPseudoClass, false);
            PseudoClasses.Set(CompactPseudoClass, false);
            UpdateBreakpoints(Bounds.Width);
            ApplyMetrics(Bounds.Width);
        }
        else if (property == RevealIntervalProperty || property == WordsPerStepProperty)
        {
            foreach (var view in _views.Values)
            {
                view.ReplyView?.ApplyTiming();
            }
        }
        else if (property == ToolDetailTemplateProperty)
        {
            // New tool panels take it; open ones keep the template they were made with.
        }
    }

    #endregion

    #region Models

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The selected model left the list: the first one takes its place (none when the list is empty).
        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset &&
            SelectedModel is { } selected && !Models.Contains(selected))
        {
            SetCurrentValue(SelectedModelProperty, Models.Count > 0 ? Models[0] : null);
        }
        SyncModelParts();
        foreach (var view in _views.Values)
        {
            view.ReplyView?.UpdateMark();
        }
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_modelMenu is not null)
        {
            _modelMenu.Providers.Clear();
            _modelMenu.Providers.AddRange(Providers);
        }
        SyncModelParts();
        foreach (var view in _views.Values)
        {
            view.ReplyView?.UpdateMark();
        }
    }

    /// <summary>The model pill and menu follow <see cref="SelectedModel"/>; null picks the first model once; no models hide the pill.</summary>
    private void SyncModelParts()
    {
        if (SelectedModel is null && Models.Count > 0)
        {
            SetCurrentValue(SelectedModelProperty, Models[0]);
            return;
        }
        if (_modelPill is not null)
        {
            _modelPill.Model = SelectedModel;
            _modelPill.Providers = Providers;
            _modelPill.IsVisible = Models.Count > 0;
        }
        if (_modelMenu is not null && !ReferenceEquals(_modelMenu.SelectedModel, SelectedModel))
        {
            _syncingModel = true;
            try
            {
                _modelMenu.SelectedModel = SelectedModel;
            }
            finally
            {
                _syncingModel = false;
            }
        }
        if (_modelMenu is not null && _modelMenu.Providers.Count != Providers.Count)
        {
            _modelMenu.Providers.Clear();
            _modelMenu.Providers.AddRange(Providers);
        }
    }

    /// <summary>The model with <paramref name="id"/>, or null.</summary>
    internal AiModel? FindModel(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        foreach (var model in Models)
        {
            if (model.Id == id)
            {
                return model;
            }
        }
        return null;
    }

    private void OnModelPillOpenRequested(object? sender, ModelPillOpenRequestedEventArgs e)
    {
        if (_modelMenu is null || _modelPill is null)
        {
            return;
        }
        // Arc's menus open with the first row highlighted from the keyboard and none from the pointer.
        var focus = e.Focus == ModelMenuOpenFocus.Surface ? ModelMenuOpenFocus.Surface : ModelMenuOpenFocus.First;
        _modelMenu.Toggle(_modelPill, focus);
    }

    private void OnModelMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_modelMenu is null)
        {
            return;
        }
        if (e.Property == ModelMenu.SelectedModelProperty && !_syncingModel && _modelMenu.SelectedModel is { } model &&
            !ReferenceEquals(model, SelectedModel))
        {
            SetCurrentValue(SelectedModelProperty, model);
        }
        else if (e.Property == ModelMenu.IsOpenProperty && _modelPill is not null)
        {
            _modelPill.IsOpen = _modelMenu.IsOpen;
        }
    }

    /// <summary>The person picked a model: new replies use it (old ones keep their marks).</summary>
    private void OnModelChosen(object? sender, ModelMenuModelEventArgs e)
    {
        if (!e.IsChange)
        {
            return;
        }
        var previous = e.PreviousModel;
        SetCurrentValue(SelectedModelProperty, e.Model);
        RaiseEvent(new ModelPickerModelChangedEventArgs(ModelChangedEvent, this, previous, e.Model));
        Announce(Labels.ModelSelected(e.Model.Name));
    }

    #endregion

    #region Composer

    private void OnComposerSent(object? sender, AiComposerSentEventArgs e)
    {
        e.Handled = true;
        Send(e.Message.Text, e.Message.Attachments);
    }

    private void OnComposerStopRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        StopReply();
    }

    private void OnSuggestionInvoked(object? sender, AiChatSuggestionEventArgs e)
    {
        e.Handled = true;
        Send(e.Prompt, Array.Empty<AiAttachment>());
    }

    private void OnSuggestionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The empty state follows the collection itself.
    }

    private void OnConversationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The title menu follows the collection itself; the shown conversation stays even when it leaves the list.
    }

    #endregion

    #region Public methods

    /// <summary>Follows the newest message again and scrolls to it.</summary>
    public void ScrollToLatest() => _scroller?.ScrollToLatest();

    /// <summary>Puts the keyboard focus in the message box.</summary>
    public void FocusComposer() => _composer?.FocusInput();

    /// <summary>Starts a new chat as "New chat" does when nobody handles <see cref="NewChatRequested"/>: the empty chat shows and the message box takes the focus.</summary>
    public void NewChat()
    {
        SetCurrentValue(CurrentConversationProperty, null);
        Announce(Labels.NewChat);
        // The box sits outside the pane that fades: it takes the focus at once.
        _composer?.FocusInput(_keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    #endregion

    #region New chat

    private void OnNewChatClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RequestNewChat();
    }

    private void OnMenuNewChatRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RequestNewChat();
    }

    /// <summary>The person asked for a new chat: the host may handle it; otherwise the chat shows the empty state.</summary>
    private void RequestNewChat()
    {
        var args = new RoutedEventArgs(NewChatRequestedEvent, this);
        RaiseEvent(args);
        if (NewChatCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
        if (!args.Handled)
        {
            NewChat();
        }
    }

    #endregion
}

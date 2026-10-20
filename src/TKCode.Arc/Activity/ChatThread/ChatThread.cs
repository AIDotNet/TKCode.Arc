using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Plugins;

namespace TKCode.Arc;

/// <summary>
/// A chat or support thread (Arc <c>ChatThread</c>): messages grouped by person and time with day separators,
/// reactions, read receipts that glide to the latest read message, a typing indicator and a composer with attachments.
/// The list stays pinned to the newest message and offers a pill when new messages land out of view.
/// </summary>
/// <remarks>
/// <para>Messages are the items (<see cref="ItemsControl.ItemsSource"/> or inline <see cref="ChatMessage"/> items), oldest
/// first. Without an <see cref="ItemsControl.ItemsSource"/> the thread keeps its own list: sent messages are appended
/// and reactions toggle in place, as Arc's uncontrolled mode does. With one, it raises <see cref="SendRequested"/> and
/// <see cref="ReactionToggled"/> and the app updates its messages (they raise <c>PropertyChanged</c>).</para>
/// <para>Bodies render with <see cref="Markdown"/> by default (<see cref="MessageFormat"/>); stream a reply into a message
/// with <see cref="AppendToMessage"/> / <see cref="CompleteMessage"/>, which feed the bubble's Markdown incrementally.
/// Rows are virtualized (a recycling <see cref="VirtualizingStackPanel"/>), so long histories stay fast. The thread fills
/// the height it is given: give it a height (or a parent that does).</para>
/// </remarks>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(ChatThreadList))]
[TemplatePart(ComposerPart, typeof(ChatComposer))]
[TemplatePart(PillHostPart, typeof(PresenceHost))]
[TemplatePart(PillPart, typeof(ChatThreadButton))]
[TemplatePart(TypingRegionPart, typeof(ActivityLiveRegion))]
[TemplatePart(LogRegionPart, typeof(ActivityLiveRegion))]
[PseudoClasses(NarrowPseudoClass, PinnedPseudoClass)]
public class ChatThread : ItemsControl
{
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string ListPart = "PART_List";
    public const string ComposerPart = "PART_Composer";
    public const string PillHostPart = "PART_PillHost";
    public const string PillPart = "PART_Pill";
    public const string TypingRegionPart = "PART_TypingRegion";
    public const string LogRegionPart = "PART_LogRegion";

    /// <summary>Set while the thread is narrower than 420px (tighter padding, 84% bubbles).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the list follows the newest message.</summary>
    public const string PinnedPseudoClass = ":pinned";

    private static readonly IReadOnlyList<string> s_defaultReactions = ["👍", "❤️", "😂", "🎉", "👀", "🙏"];
    private const double StickDistance = 48;
    private const double FarDistance = 180;
    private static readonly SpringSpec s_settle = SpringSpec.FromVisual(0.44, 0);
    private static readonly long s_growthWindow = (long)(0.7 * Stopwatch.Frequency);

    #region Properties

    /// <summary>The people in the conversation (Arc <c>participants</c>).</summary>
    public static readonly StyledProperty<IEnumerable<ChatParticipant>?> ParticipantsProperty =
        AvaloniaProperty.Register<ChatThread, IEnumerable<ChatParticipant>?>(nameof(Participants));

    /// <summary>The current user (Arc <c>currentUserId</c>): their messages sit on the right in the accent color.</summary>
    public static readonly StyledProperty<string?> CurrentUserIdProperty =
        AvaloniaProperty.Register<ChatThread, string?>(nameof(CurrentUserId));

    /// <summary>Participant ids currently typing (Arc <c>typing</c>); an observable collection is followed live.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> TypingParticipantIdsProperty =
        AvaloniaProperty.Register<ChatThread, IEnumerable<string>?>(nameof(TypingParticipantIds));

    /// <summary>
    /// The last message each participant has read, by participant id (Arc <c>readBy</c>). Their portrait sits under
    /// that message and glides when it moves. Assign a new dictionary to update.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, string>?> ReadByProperty =
        AvaloniaProperty.Register<ChatThread, IReadOnlyDictionary<string, string>?>(nameof(ReadBy));

    /// <summary>Emoji offered by the reaction picker (Arc default 👍 ❤️ 😂 🎉 👀 🙏).</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ReactionsProperty =
        AvaloniaProperty.Register<ChatThread, IReadOnlyList<string>>(nameof(Reactions), s_defaultReactions);

    /// <summary>The composer's placeholder and accessible name ("Message").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ChatThread, string?>(nameof(Placeholder), "Message");

    /// <summary>Shows the composer (Arc <c>composer</c>, default true).</summary>
    public static readonly StyledProperty<bool> IsComposerVisibleProperty =
        AvaloniaProperty.Register<ChatThread, bool>(nameof(IsComposerVisible), true);

    /// <summary>Lets the composer take files by button, paste or drop (Arc <c>allowAttachments</c>, default true).</summary>
    public static readonly StyledProperty<bool> IsAttachmentEnabledProperty =
        AvaloniaProperty.Register<ChatThread, bool>(nameof(IsAttachmentEnabled), true);

    /// <summary>File types the attach button offers (Arc <c>accept</c>); null offers everything.</summary>
    public static readonly StyledProperty<IReadOnlyList<FilePickerFileType>?> AcceptedFileTypesProperty =
        AvaloniaProperty.Register<ChatThread, IReadOnlyList<FilePickerFileType>?>(nameof(AcceptedFileTypes));

    /// <summary>Messages from one person closer together than this share a group (Arc <c>groupWindow</c>, 5 minutes).</summary>
    public static readonly StyledProperty<TimeSpan> GroupWindowProperty =
        AvaloniaProperty.Register<ChatThread, TimeSpan>(nameof(GroupWindow), TimeSpan.FromMinutes(5));

    /// <summary>Formatting culture of times and day separators (Arc <c>locale</c>); null uses en-US.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<ChatThread, CultureInfo?>(nameof(Culture));

    /// <summary>Time zone of times and day separators; null uses the local zone (as Arc's browser does).</summary>
    public static readonly StyledProperty<TimeZoneInfo?> TimeZoneProperty =
        AvaloniaProperty.Register<ChatThread, TimeZoneInfo?>(nameof(TimeZone));

    /// <summary>Accessible name of the message log (Arc <c>label</c>, "Conversation").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ChatThread, string>(nameof(Label), "Conversation");

    /// <summary>Every word the thread shows or announces (Arc <c>labels</c>).</summary>
    public static readonly StyledProperty<ChatThreadLabels> LabelsProperty =
        AvaloniaProperty.Register<ChatThread, ChatThreadLabels>(nameof(Labels), new ChatThreadLabels());

    /// <summary>How message text renders unless a message sets <see cref="ChatMessage.Format"/> (default Markdown).</summary>
    public static readonly StyledProperty<ChatMessageFormat> MessageFormatProperty =
        AvaloniaProperty.Register<ChatThread, ChatMessageFormat>(nameof(MessageFormat), ChatMessageFormat.Markdown);

    /// <summary>StreamMark services for Markdown bodies; null uses <see cref="DefaultMarkdownServices"/>.</summary>
    public static readonly StyledProperty<MarkdownServices?> MarkdownServicesProperty =
        AvaloniaProperty.Register<ChatThread, MarkdownServices?>(nameof(MarkdownServices));

    /// <summary>Invoked with the failed <see cref="ChatMessage"/> when Retry is clicked; Retry shows when this or a handler is set.</summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<ChatThread, ICommand?>(nameof(RetryCommand));

    /// <summary>Whether the list follows the newest message (the reader is within 48px of the end).</summary>
    public static readonly DirectProperty<ChatThread, bool> IsPinnedProperty =
        AvaloniaProperty.RegisterDirect<ChatThread, bool>(nameof(IsPinned), o => o.IsPinned);

    /// <summary>Messages from other people that arrived while the reader was scrolled up.</summary>
    public static readonly DirectProperty<ChatThread, int> UnreadCountProperty =
        AvaloniaProperty.RegisterDirect<ChatThread, int>(nameof(UnreadCount), o => o.UnreadCount);

    // Theme inputs for parts built in code (ChatThreadToken / ArcDesign tokens).
    internal static readonly StyledProperty<double> BubbleLineHeightProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(BubbleLineHeight), 19.6);

    internal static readonly StyledProperty<double> BubbleBlockSpacingProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(BubbleBlockSpacing), 8);

    internal static readonly StyledProperty<double> GalleryRadiusProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(GalleryRadius), 16);

    internal static readonly StyledProperty<double> GalleryTileRadiusProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(GalleryTileRadius), 6);

    internal static readonly StyledProperty<double> FileGapProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(FileGap), 10);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(NarrowWidth), 420);

    internal static readonly StyledProperty<double> StackMaxRatioWideProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(StackMaxRatioWide), 0.78);

    internal static readonly StyledProperty<double> StackMaxRatioNarrowProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(StackMaxRatioNarrow), 0.84);

    internal static readonly StyledProperty<double> PickerOffsetProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(PickerOffset), 6);

    internal static readonly StyledProperty<double> PickerGapProperty =
        AvaloniaProperty.Register<ChatThread, double>(nameof(PickerGap), 2);

    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ChatThread, IBrush?>(nameof(AccentBrush));

    internal static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty =
        AvaloniaProperty.Register<ChatThread, IBrush?>(nameof(AccentForegroundBrush));

    public IEnumerable<ChatParticipant>? Participants { get => GetValue(ParticipantsProperty); set => SetValue(ParticipantsProperty, value); }
    public string? CurrentUserId { get => GetValue(CurrentUserIdProperty); set => SetValue(CurrentUserIdProperty, value); }
    public IEnumerable<string>? TypingParticipantIds { get => GetValue(TypingParticipantIdsProperty); set => SetValue(TypingParticipantIdsProperty, value); }
    public IReadOnlyDictionary<string, string>? ReadBy { get => GetValue(ReadByProperty); set => SetValue(ReadByProperty, value); }
    public IReadOnlyList<string> Reactions { get => GetValue(ReactionsProperty); set => SetValue(ReactionsProperty, value); }
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsComposerVisible { get => GetValue(IsComposerVisibleProperty); set => SetValue(IsComposerVisibleProperty, value); }
    public bool IsAttachmentEnabled { get => GetValue(IsAttachmentEnabledProperty); set => SetValue(IsAttachmentEnabledProperty, value); }
    public IReadOnlyList<FilePickerFileType>? AcceptedFileTypes { get => GetValue(AcceptedFileTypesProperty); set => SetValue(AcceptedFileTypesProperty, value); }
    public TimeSpan GroupWindow { get => GetValue(GroupWindowProperty); set => SetValue(GroupWindowProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public TimeZoneInfo? TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ChatThreadLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public ChatMessageFormat MessageFormat { get => GetValue(MessageFormatProperty); set => SetValue(MessageFormatProperty, value); }
    public MarkdownServices? MarkdownServices { get => GetValue(MarkdownServicesProperty); set => SetValue(MarkdownServicesProperty, value); }
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }

    internal double BubbleLineHeight { get => GetValue(BubbleLineHeightProperty); set => SetValue(BubbleLineHeightProperty, value); }
    internal double BubbleBlockSpacing { get => GetValue(BubbleBlockSpacingProperty); set => SetValue(BubbleBlockSpacingProperty, value); }
    internal double GalleryRadius { get => GetValue(GalleryRadiusProperty); set => SetValue(GalleryRadiusProperty, value); }
    internal double GalleryTileRadius { get => GetValue(GalleryTileRadiusProperty); set => SetValue(GalleryTileRadiusProperty, value); }
    internal double FileGap { get => GetValue(FileGapProperty); set => SetValue(FileGapProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double StackMaxRatioWide { get => GetValue(StackMaxRatioWideProperty); set => SetValue(StackMaxRatioWideProperty, value); }
    internal double StackMaxRatioNarrow { get => GetValue(StackMaxRatioNarrowProperty); set => SetValue(StackMaxRatioNarrowProperty, value); }
    internal double PickerOffset { get => GetValue(PickerOffsetProperty); set => SetValue(PickerOffsetProperty, value); }
    internal double PickerGap { get => GetValue(PickerGapProperty); set => SetValue(PickerGapProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? AccentForegroundBrush { get => GetValue(AccentForegroundBrushProperty); set => SetValue(AccentForegroundBrushProperty, value); }

    private bool _isPinned = true;

    /// <summary>Whether the list follows the newest message.</summary>
    public bool IsPinned { get => _isPinned; private set => SetAndRaise(IsPinnedProperty, ref _isPinned, value); }

    private int _unreadCount;

    /// <summary>Messages from other people that arrived while the reader was scrolled up.</summary>
    public int UnreadCount { get => _unreadCount; private set => SetAndRaise(UnreadCountProperty, ref _unreadCount, value); }

    /// <summary>Your message from the composer (Arc <c>onSend</c>). Without an ItemsSource the thread also appends it.</summary>
    public static readonly RoutedEvent<ChatSendEventArgs> SendRequestedEvent =
        RoutedEvent.Register<ChatThread, ChatSendEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    /// <summary>A reaction was picked or a chip clicked (Arc <c>onReact</c>). Without an ItemsSource the thread also toggles it.</summary>
    public static readonly RoutedEvent<ChatReactionToggledEventArgs> ReactionToggledEvent =
        RoutedEvent.Register<ChatThread, ChatReactionToggledEventArgs>(nameof(ReactionToggled), RoutingStrategies.Bubble);

    /// <summary>Retry was clicked on your last, failed message (Arc <c>onRetry</c>).</summary>
    public static readonly RoutedEvent<ChatRetryEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ChatThread, ChatRetryEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>A picture or file was clicked; mark it handled to stop the thread from opening it.</summary>
    public static readonly RoutedEvent<ChatAttachmentEventArgs> AttachmentClickedEvent =
        RoutedEvent.Register<ChatThread, ChatAttachmentEventArgs>(nameof(AttachmentClicked), RoutingStrategies.Bubble);

    /// <summary>Your message from the composer.</summary>
    public event EventHandler<ChatSendEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    /// <summary>A reaction was picked or a chip clicked.</summary>
    public event EventHandler<ChatReactionToggledEventArgs>? ReactionToggled
    {
        add => AddHandler(ReactionToggledEvent, value);
        remove => RemoveHandler(ReactionToggledEvent, value);
    }

    /// <summary>Retry was clicked. Retry shows only while a handler or <see cref="RetryCommand"/> is set.</summary>
    public event EventHandler<ChatRetryEventArgs>? RetryRequested
    {
        add
        {
            _retryHandlers++;
            AddHandler(RetryRequestedEvent, value);
            RefreshRealized();
        }
        remove
        {
            _retryHandlers = Math.Max(0, _retryHandlers - 1);
            RemoveHandler(RetryRequestedEvent, value);
            RefreshRealized();
        }
    }

    /// <summary>A picture or file was clicked.</summary>
    public event EventHandler<ChatAttachmentEventArgs>? AttachmentClicked
    {
        add => AddHandler(AttachmentClickedEvent, value);
        remove => RemoveHandler(AttachmentClickedEvent, value);
    }

    #endregion

    /// <summary>Arc's reaction picker emoji.</summary>
    public static IReadOnlyList<string> DefaultReactions => s_defaultReactions;

    /// <summary>
    /// The Markdown services of message bodies unless <see cref="MarkdownServices"/> is set: Arc-palette highlighting,
    /// chat line breaks (a single newline stays a line break) and Arc's code block wording.
    /// </summary>
    public static MarkdownServices DefaultMarkdownServices { get; } = new()
    {
        Plugins = [new MarkdownHighlightPlugin(), new ChatThreadLineBreakPlugin()],
        CodeBlocks = new CodeBlockOptions
        {
            ShowDownload = false,
            CollapsedLines = 12,
            CollapseText = static () => "Show fewer lines"
        }
    };

    private readonly ObservableCollection<ChatThreadRow> _rows = new();
    private readonly Dictionary<string, ChatThreadRow> _rowsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ChatMessage, ChatThreadMessageRow> _messageRows = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ChatMessage> _subscribed = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private readonly List<ChatMessage> _messages = new();
    private readonly ChatThreadBodies _bodies;
    private ChatThreadPicker? _picker;
    private ScrollViewer? _scroller;
    private ChatThreadList? _list;
    private ChatComposer? _composer;
    private PresenceHost? _pillHost;
    private ChatThreadButton? _pill;
    private ChatThreadPillLabel? _pillLabel;
    private ActivityLiveRegion? _typingRegion;
    private ActivityLiveRegion? _logRegion;
    private INotifyCollectionChanged? _typingSource;
    private readonly TranslateTransform _contentTranslate = new();
    private MotionValue<double>? _contentY;
    private MotionValue<double>? _scrollMotion;
    private bool _smoothScrolling;
    private long _growthArmedUntil;
    private bool _shown;
    private bool _far;
    private int _seen;
    private int _retryHandlers;
    private int _counter;
    private int _lastMine = -1;
    private int _readIndex = -1;
    private string _typingAnnouncement = string.Empty;
    private ChatThreadTypingRow? _typingRow;
    private bool _rebuildQueued;
    private IBrush? _mineCodeBrush;
    private IBrush? _mineSelectionBrush;

    /// <summary>Clock for day separators (tests replace it).</summary>
    internal Func<DateTimeOffset> Now { get; set; } = static () => DateTimeOffset.Now;

    static ChatThread()
    {
        FocusableProperty.OverrideDefaultValue<ChatThread>(false);
    }

    public ChatThread()
    {
        _bodies = new ChatThreadBodies(this);
        ItemsView.CollectionChanged += OnMessagesChanged;
    }

    #region Public API

    /// <summary>Scrolls to the newest message and follows it again (Arc <c>scrollToBottom</c>); immediate under reduced motion.</summary>
    public void ScrollToBottom(bool smooth = true)
    {
        SetPinned(true);
        if (_scroller is null)
        {
            return;
        }
        var end = EndOffset();
        if (!smooth || !IsLoaded || !ArcMotion.IsFull(this))
        {
            StopSmoothScroll();
            SetOffset(end);
            UpdatePill();
            return;
        }
        _scrollMotion ??= CreateScrollMotion();
        _scrollMotion.Jump(_scroller.Offset.Y);
        _smoothScrolling = true;
        _ = RunSmoothScroll(end);
    }

    /// <summary>Moves keyboard focus into the composer (Arc <c>focusComposer</c>).</summary>
    public void FocusComposer() => _composer?.FocusInput();

    /// <summary>
    /// Streams <paramref name="text"/> onto the end of a message (an assistant reply arriving in chunks). The bubble's
    /// <see cref="Markdown"/> receives only the new text (<see cref="Markdown.Append"/>): nothing is parsed again from the
    /// start, and the message's <see cref="ChatMessage.Text"/> grows with it so a recycled bubble still shows all of it.
    /// Returns false when no message has <paramref name="messageId"/>.
    /// </summary>
    public bool AppendToMessage(string messageId, string text)
    {
        if (FindMessage(messageId) is not { } message)
        {
            return false;
        }
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        _bodies.Append(message, text);
        ArmGrowth();
        return true;
    }

    /// <summary>Ends a reply streamed with <see cref="AppendToMessage"/>. Returns false when no message has the id.</summary>
    public bool CompleteMessage(string messageId)
    {
        if (FindMessage(messageId) is not { } message)
        {
            return false;
        }
        _bodies.Complete(message);
        return true;
    }

    /// <summary>Whether a message is being streamed with <see cref="AppendToMessage"/>.</summary>
    public bool IsMessageStreaming(string messageId) => FindMessage(messageId) is { } message && _bodies.IsStreaming(message);

    #endregion

    #region Template and lifecycle

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_composer is not null)
        {
            _composer.Sent -= OnComposerSent;
        }
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _list = e.NameScope.Find<ChatThreadList>(ListPart);
        _composer = e.NameScope.Find<ChatComposer>(ComposerPart);
        _pillHost = e.NameScope.Find<PresenceHost>(PillHostPart);
        _pill = e.NameScope.Find<ChatThreadButton>(PillPart);
        _typingRegion = e.NameScope.Find<ActivityLiveRegion>(TypingRegionPart);
        _logRegion = e.NameScope.Find<ActivityLiveRegion>(LogRegionPart);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (_list is not null)
        {
            _list.Owner = this;
            _list.ItemsSource = _rows;
            AutomationProperties.SetName(_list, Label);
        }
        if (_composer is not null)
        {
            _composer.Sent += OnComposerSent;
        }
        if (_pill is not null)
        {
            _pillLabel ??= new ChatThreadPillLabel();
            _pill.Content = _pillLabel;
            _pill.Click += OnPillClick;
        }
        if (_pillHost is not null)
        {
            _pillHost.SetCurrentValue(PresenceHost.IsPresentProperty, false);
        }
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ArcToday.Changed += OnTodayChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ArcToday.Changed -= OnTodayChanged;
        _picker?.Close(restoreFocus: false);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _shown = true;
        foreach (var message in _messages)
        {
            _known.Add(message.Id);
        }
        _seen = _messages.Count;
        Dispatcher.UIThread.Post(() => PinToEnd(), DispatcherPriority.Loaded);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        if (PseudoClasses.Contains(NarrowPseudoClass) != narrow)
        {
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            RefreshRealized();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ParticipantsProperty || change.Property == CurrentUserIdProperty || change.Property == GroupWindowProperty ||
            change.Property == CultureProperty || change.Property == TimeZoneProperty || change.Property == ReadByProperty ||
            change.Property == LabelsProperty || change.Property == MessageFormatProperty)
        {
            if (change.Property == ReadByProperty)
            {
                CaptureReceipts();
            }
            if (change.Property == CurrentUserIdProperty || change.Property == MessageFormatProperty)
            {
                _bodies.Clear();
            }
            Rebuild();
        }
        else if (change.Property == TypingParticipantIdsProperty)
        {
            if (_typingSource is not null)
            {
                _typingSource.CollectionChanged -= OnTypingChanged;
            }
            _typingSource = change.GetNewValue<IEnumerable<string>?>() as INotifyCollectionChanged;
            if (_typingSource is not null)
            {
                _typingSource.CollectionChanged += OnTypingChanged;
            }
            Rebuild();
        }
        else if (change.Property == LabelProperty && _list is not null)
        {
            AutomationProperties.SetName(_list, Label);
        }
        else if (change.Property == MarkdownServicesProperty)
        {
            _bodies.Clear();
            RefreshRealized();
        }
        else if (change.Property == AccentBrushProperty || change.Property == AccentForegroundBrushProperty)
        {
            _mineCodeBrush = null;
            _mineSelectionBrush = null;
            _bodies.RefreshColors();
        }
        else if (change.Property == ReactionsProperty)
        {
            _picker?.Close(restoreFocus: false);
        }
        else if (change.Property == RetryCommandProperty)
        {
            RefreshRealized();
        }
    }

    #endregion

    #region Messages and rows

    internal IReadOnlyList<ChatThreadRow> Rows => _rows;

    internal ChatThreadList? List => _list;

    internal ScrollViewer? Scroller => _scroller;

    internal ChatComposer? Composer => _composer;

    internal ChatThreadPicker? Picker => _picker;

    internal ChatThreadBodies Bodies => _bodies;

    internal ActivityLiveRegion? TypingRegion => _typingRegion;

    internal ActivityLiveRegion? LogRegion => _logRegion;

    internal PresenceHost? PillHost => _pillHost;

    internal ChatThreadPillLabel? PillLabel => _pillLabel;

    internal double ContentOffset => _contentY?.Value ?? 0;

    internal bool HasRetryHandler => _retryHandlers > 0 || RetryCommand is not null;

    internal CultureInfo EffectiveCulture => Culture ?? ActivityTime.DefaultCulture;

    internal TimeZoneInfo EffectiveTimeZone => TimeZone ?? TimeZoneInfo.Local;

    internal double StackMaxRatio => PseudoClasses.Contains(NarrowPseudoClass) ? StackMaxRatioNarrow : StackMaxRatioWide;

    internal MarkdownServices EffectiveMarkdownServices => MarkdownServices ?? DefaultMarkdownServices;

    private ChatMessage? FindMessage(string id)
    {
        foreach (var message in _messages)
        {
            if (message.Id == id)
            {
                return message;
            }
        }
        return null;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnTypingChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatMessage message)
        {
            return;
        }
        switch (e.PropertyName)
        {
            case nameof(ChatMessage.AuthorId):
            case nameof(ChatMessage.CreatedAt):
            case nameof(ChatMessage.Id):
                QueueRebuild();
                break;
            case nameof(ChatMessage.Status):
                // Status moves the "read" state and the dimmed bubble; the rows' derived status comes from a rebuild.
                ArmGrowth();
                QueueRebuild();
                break;
            case nameof(ChatMessage.Text):
                _bodies.OnTextChanged(message);
                ArmGrowth();
                RefreshMessage(message);
                break;
            default:
                ArmGrowth();
                RefreshMessage(message);
                break;
        }
    }

    private void QueueRebuild()
    {
        if (_rebuildQueued)
        {
            return;
        }
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildQueued)
            {
                Rebuild();
            }
        }, DispatcherPriority.Background);
    }

    private void RefreshMessage(ChatMessage message)
    {
        if (_list is null)
        {
            return;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is ChatThreadMessageView { Row: { } row } view && ReferenceEquals(row.Message, message))
            {
                view.Refresh();
            }
        }
    }

    internal void RefreshRealized()
    {
        if (_list is null)
        {
            return;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            switch (container)
            {
                case ChatThreadMessageView view:
                    view.Refresh();
                    break;
                case ChatThreadDayView day:
                    day.Refresh();
                    break;
                case ChatThreadTypingView typing:
                    typing.Refresh();
                    break;
            }
        }
    }

    /// <summary>
    /// Rebuilds the row list from the messages (Arc's <c>rows</c> memo): day separators, groups, receipts, the last own
    /// message's status and the typing row. Row models are kept by key and the observable row list is updated with
    /// the fewest inserts, moves and removals, so realized containers stay and only rows that changed refresh.
    /// </summary>
    internal void Rebuild()
    {
        _rebuildQueued = false;
        _messages.Clear();
        foreach (var item in ItemsView)
        {
            if (item is ChatMessage message)
            {
                _messages.Add(message);
            }
        }
        // Keep PropertyChanged subscriptions in step with the messages.
        var current = new HashSet<ChatMessage>(_messages, ReferenceEqualityComparer.Instance);
        foreach (var gone in _subscribed.Where(m => !current.Contains(m)).ToList())
        {
            gone.PropertyChanged -= OnMessagePropertyChanged;
            _subscribed.Remove(gone);
            _messageRows.Remove(gone);
            _bodies.Forget(gone);
        }
        foreach (var message in _messages)
        {
            if (_subscribed.Add(message))
            {
                message.PropertyChanged += OnMessagePropertyChanged;
            }
        }

        var people = new Dictionary<string, ChatParticipant>(StringComparer.Ordinal);
        if (Participants is { } participants)
        {
            foreach (var person in participants)
            {
                people[person.Id] = person;
            }
        }
        var me = CurrentUserId;
        var zone = EffectiveTimeZone;
        var window = GroupWindow;

        // Receipts: a reader's avatar under their own message says nothing new, so it waits until they read past it.
        var receipts = new Dictionary<string, List<ChatParticipant>>(StringComparer.Ordinal);
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _messages.Count; i++)
        {
            indexById.TryAdd(_messages[i].Id, i);
        }
        if (ReadBy is { } readBy)
        {
            foreach (var (personId, messageId) in readBy)
            {
                if (!people.TryGetValue(personId, out var who) || personId == me || !indexById.TryGetValue(messageId, out var at) ||
                    _messages[at].AuthorId == personId)
                {
                    continue;
                }
                if (!receipts.TryGetValue(messageId, out var list))
                {
                    list = new List<ChatParticipant>();
                    receipts[messageId] = list;
                }
                list.Add(who);
            }
        }
        _readIndex = -1;
        foreach (var messageId in receipts.Keys)
        {
            _readIndex = Math.Max(_readIndex, indexById[messageId]);
        }
        _lastMine = -1;
        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            if (_messages[i].AuthorId == me)
            {
                _lastMine = i;
                break;
            }
        }

        var next = new List<ChatThreadRow>(_messages.Count + 8);
        var today = ActivityTime.DayOf(Now(), zone);
        DateOnly? lastDay = null;
        var added = new List<ChatMessage>();
        for (var i = 0; i < _messages.Count; i++)
        {
            var message = _messages[i];
            var day = ActivityTime.DayOf(message.CreatedAt, zone);
            var afterDay = false;
            if (day != lastDay)
            {
                var key = "day-" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!_rowsByKey.TryGetValue(key, out var existing) || existing is not ChatThreadDayRow dayRow)
                {
                    dayRow = new ChatThreadDayRow(day);
                }
                dayRow.Sample = message.CreatedAt;
                dayRow.Label = DayLabel(day, today, message.CreatedAt);
                dayRow.IsFirst = next.Count == 0;
                next.Add(dayRow);
                lastDay = day;
                afterDay = true;
            }
            if (!_messageRows.TryGetValue(message, out var row))
            {
                row = new ChatThreadMessageRow(message);
                _messageRows[message] = row;
            }
            if (_shown && _known.Add(message.Id))
            {
                row.Fresh = true;
                added.Add(message);
            }
            row.Author = people.TryGetValue(message.AuthorId, out var author)
                ? author
                : new ChatParticipant(message.AuthorId, Labels.UnknownAuthor);
            row.Mine = message.AuthorId == me;
            row.Index = i;
            row.AfterDay = afterDay;
            row.First = !Joins(i - 1, message, day, window, zone);
            row.Last = !Joins(i + 1, message, day, window, zone);
            row.Readers = receipts.TryGetValue(message.Id, out var readers) ? readers : Array.Empty<ChatParticipant>();
            row.ShowsStatus = row.Mine && i == _lastMine;
            row.Status = message.Status switch
            {
                ChatMessageStatus.Failed => ChatMessageStatus.Failed,
                ChatMessageStatus.Sending => ChatMessageStatus.Sending,
                _ when _readIndex >= i => ChatMessageStatus.Read,
                { } status => status,
                _ => ChatMessageStatus.Sent
            };
            next.Add(row);
        }

        // Typing row (the first typer who is not the current user).
        var typers = new List<ChatParticipant>();
        if (TypingParticipantIds is { } typing)
        {
            foreach (var id in typing)
            {
                if (id != me && people.TryGetValue(id, out var person) && !typers.Contains(person))
                {
                    typers.Add(person);
                }
            }
        }
        if (typers.Count > 0)
        {
            if (_typingRow is null || _typingRow.Leaving)
            {
                var fresh = _typingRow is null;
                _typingRow ??= new ChatThreadTypingRow();
                _typingRow.Leaving = false;
                _typingRow.Fresh = _shown && fresh;
            }
            _typingRow.Typer = typers[0];
            next.Add(_typingRow);
        }
        else if (_typingRow is not null)
        {
            if (TypingViewFor(_typingRow) is { } view && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
            {
                if (!_typingRow.Leaving)
                {
                    _typingRow.Leaving = true;
                    _ = RemoveTypingRowAsync(_typingRow, view);
                }
                next.Add(_typingRow);
            }
            else
            {
                _typingRow = null;
            }
        }

        if (added.Count > 0 || (_typingRow?.Fresh ?? false))
        {
            ArmGrowth();
        }
        Sync(next);
        RefreshRealized();
        AnnounceTyping(typers);
        AnnounceAdded(added, people);
        if (added.Any(m => m.AuthorId == me))
        {
            // Your own message always brings the thread back to the bottom.
            SetPinned(true);
        }
        if (IsPinned)
        {
            _seen = _messages.Count;
        }
        UpdatePill();
        if (_shown && added.Count > 0)
        {
            // Rows that arrived out of view do not play their arrival later, when the reader scrolls to them.
            Dispatcher.UIThread.Post(ClearFresh, DispatcherPriority.Background);
        }
        FlyReceiptsLater();
    }

    private bool Joins(int other, ChatMessage message, DateOnly day, TimeSpan window, TimeZoneInfo zone)
    {
        if (other < 0 || other >= _messages.Count)
        {
            return false;
        }
        var neighbor = _messages[other];
        return neighbor.AuthorId == message.AuthorId && ActivityTime.DayOf(neighbor.CreatedAt, zone) == day &&
               (neighbor.CreatedAt - message.CreatedAt).Duration() <= window;
    }

    private string DayLabel(DateOnly day, DateOnly today, DateTimeOffset sample)
    {
        var labels = Labels;
        var days = ActivityTime.DaysBefore(day, today);
        return days switch
        {
            0 => labels.Today,
            1 => labels.Yesterday,
            > 1 and < 7 => ActivityTime.Weekday(sample, EffectiveTimeZone, EffectiveCulture),
            _ => ActivityTime.ShortDate(sample, EffectiveTimeZone, EffectiveCulture, withYear: day.Year != today.Year)
        };
    }

    private void OnTodayChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshDayLabels);

    /// <summary>Day labels follow the calendar ("Today" becomes "Yesterday" at midnight) without rebuilding rows.</summary>
    internal void RefreshDayLabels()
    {
        var today = ActivityTime.DayOf(Now(), EffectiveTimeZone);
        foreach (var row in _rows)
        {
            if (row is ChatThreadDayRow day)
            {
                day.Label = DayLabel(day.Day, today, day.Sample);
            }
        }
        if (_list is null)
        {
            return;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            (container as ChatThreadDayView)?.Refresh();
        }
    }

    private void ClearFresh()
    {
        foreach (var row in _rows)
        {
            if (row is ChatThreadMessageRow { Fresh: true } message)
            {
                message.Fresh = false;
            }
        }
    }

    /// <summary>Brings the observable row list in line with <paramref name="next"/> using the fewest changes.</summary>
    private void Sync(List<ChatThreadRow> next)
    {
        var wanted = new HashSet<ChatThreadRow>(next, ReferenceEqualityComparer.Instance);
        for (var i = _rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(_rows[i]))
            {
                _rows.RemoveAt(i);
            }
        }
        for (var i = 0; i < next.Count; i++)
        {
            var row = next[i];
            if (i < _rows.Count && ReferenceEquals(_rows[i], row))
            {
                continue;
            }
            var at = -1;
            for (var j = i + 1; j < _rows.Count; j++)
            {
                if (ReferenceEquals(_rows[j], row))
                {
                    at = j;
                    break;
                }
            }
            if (at >= 0)
            {
                _rows.Move(at, i);
            }
            else
            {
                _rows.Insert(i, row);
            }
        }
        while (_rows.Count > next.Count)
        {
            _rows.RemoveAt(_rows.Count - 1);
        }
        _rowsByKey.Clear();
        foreach (var row in _rows)
        {
            _rowsByKey[row.Key] = row;
        }
    }

    private ChatThreadTypingView? TypingViewFor(ChatThreadTypingRow row)
    {
        if (_list is null)
        {
            return null;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is ChatThreadTypingView view && ReferenceEquals(view.Row, row))
            {
                return view;
            }
        }
        return null;
    }

    private async Task RemoveTypingRowAsync(ChatThreadTypingRow row, ChatThreadTypingView view)
    {
        await view.Motion.LeaveAsync();
        if (row.Leaving && ReferenceEquals(_typingRow, row))
        {
            _typingRow = null;
            Rebuild();
        }
    }

    internal ChatThreadMessageView? ViewFor(ChatMessage message)
    {
        if (_list is null)
        {
            return null;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is ChatThreadMessageView { Row: { } row } view && ReferenceEquals(row.Message, message))
            {
                return view;
            }
        }
        return null;
    }

    #endregion

    #region Announcements

    private void AnnounceTyping(List<ChatParticipant> typers)
    {
        var text = typers.Count > 0 ? Labels.Typing(typers.Select(t => t.Name).ToArray()) : string.Empty;
        if (text == _typingAnnouncement)
        {
            return;
        }
        _typingAnnouncement = text;
        _typingRegion?.Announce(text);
    }

    private void AnnounceAdded(List<ChatMessage> added, Dictionary<string, ChatParticipant> people)
    {
        if (added.Count == 0 || _logRegion is null)
        {
            return;
        }
        var parts = added.Select(m =>
        {
            var name = people.TryGetValue(m.AuthorId, out var who) ? who.Name : Labels.UnknownAuthor;
            return string.IsNullOrEmpty(m.Text) ? name : $"{name}: {m.Text}";
        });
        _logRegion.Announce(string.Join(". ", parts));
    }

    #endregion

    #region Pinning, growth spring and the pill

    private double EndOffset() => _scroller is null ? 0 : Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);

    private double Distance() => _scroller is null ? 0 : _scroller.Extent.Height - _scroller.Offset.Y - _scroller.Viewport.Height;

    /// <summary>Content growth during the next 700ms (a message, reactions, streamed text) springs while pinned.</summary>
    internal void ArmGrowth() => _growthArmedUntil = Stopwatch.GetTimestamp() + s_growthWindow;

    private double _ownOffset = double.NaN;

    private void SetPinned(bool value)
    {
        IsPinned = value;
        PseudoClasses.Set(PinnedPseudoClass, value);
        if (value)
        {
            _seen = _messages.Count;
        }
    }

    private void PinToEnd()
    {
        if (_scroller is null || !IsPinned)
        {
            return;
        }
        var end = EndOffset();
        if (Math.Abs(_scroller.Offset.Y - end) > 0.5)
        {
            SetOffset(end);
        }
    }

    /// <summary>Scrolls on the thread's own behalf; <see cref="OnScrollChanged"/> tells these apart from the user's scrolls.</summary>
    private void SetOffset(double y)
    {
        if (_scroller is null)
        {
            return;
        }
        _ownOffset = y;
        _scroller.Offset = new Vector(_scroller.Offset.X, y);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null)
        {
            return;
        }
        var layoutChanged = Math.Abs(e.ExtentDelta.Y) > 0.01 || Math.Abs(e.ViewportDelta.Y) > 0.01;
        // A scroll the thread did not make (wheel, scrollbar, keys, a clamp) decides pinning first, even when the extent
        // changed in the same pass: virtualized rows re-estimate their heights as they come into view, and treating that
        // as growth would snap the reader back to the bottom.
        if (Math.Abs(e.OffsetDelta.Y) > 0.01 && !_smoothScrolling && Math.Abs(_scroller.Offset.Y - _ownOffset) > 0.5)
        {
            _ownOffset = double.NaN;
            SetPinned(Distance() < StickDistance);
        }
        if (layoutChanged && IsPinned)
        {
            var growth = e.ExtentDelta.Y;
            if (_smoothScrolling)
            {
                _ = RunSmoothScroll(EndOffset());
            }
            else
            {
                PinToEnd();
                if (growth > 0.5 && growth < _scroller.Viewport.Height && Stopwatch.GetTimestamp() < _growthArmedUntil &&
                    IsLoaded && ArcMotion.IsFull(this))
                {
                    Settle(growth);
                }
            }
        }
        _far = Distance() > FarDistance;
        UpdatePill();
    }

    /// <summary>P30: the content starts where it was before it grew and springs up into place.</summary>
    private void Settle(double growth)
    {
        if (_list?.ItemsPanelRoot is not { } panel)
        {
            return;
        }
        if (!ReferenceEquals(panel.RenderTransform, _contentTranslate))
        {
            panel.RenderTransform = _contentTranslate;
        }
        _contentY ??= CreateContentY();
        _contentY.Jump(_contentY.Value + growth);
        _ = _contentY.AnimateTo(0, MotionSpec.Spring(s_settle));
    }

    private MotionValue<double> CreateContentY()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += y => _contentTranslate.Y = y;
        return value;
    }

    private MotionValue<double> CreateScrollMotion()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += y =>
        {
            if (_scroller is not null && _smoothScrolling)
            {
                SetOffset(Math.Min(y, EndOffset()));
            }
        };
        return value;
    }

    private async Task RunSmoothScroll(double target)
    {
        if (_scrollMotion is null)
        {
            return;
        }
        var done = await _scrollMotion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        if (done)
        {
            _smoothScrolling = false;
            SetPinned(true);
            PinToEnd();
            UpdatePill();
        }
    }

    private void StopSmoothScroll()
    {
        _smoothScrolling = false;
        _scrollMotion?.Stop();
    }

    private void UpdatePill()
    {
        var unread = 0;
        if (!IsPinned)
        {
            var me = CurrentUserId;
            for (var i = Math.Min(_seen, _messages.Count); i < _messages.Count; i++)
            {
                if (_messages[i].AuthorId != me)
                {
                    unread++;
                }
            }
        }
        UnreadCount = unread;
        var show = !IsPinned && (unread > 0 || _far);
        if (_pillLabel is not null)
        {
            _pillLabel.Update(unread, Labels);
        }
        if (_pill is not null)
        {
            AutomationProperties.SetName(_pill, unread > 0 ? Labels.NewMessages(unread) : Labels.JumpToLatest);
        }
        if (_pillHost is not null && _pillHost.IsPresent != show)
        {
            _pillHost.IsPresent = show;
        }
    }

    private void OnPillClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ScrollToBottom();
    }

    #endregion

    #region Receipts flight (P8)

    private Dictionary<string, Point>? _receiptOrigins;

    /// <summary>Remembers where each reader's portrait is drawn before the receipts move.</summary>
    private void CaptureReceipts()
    {
        if (_list?.ItemsPanelRoot is not { } panel || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        var origins = new Dictionary<string, Point>(StringComparer.Ordinal);
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is not ChatThreadMessageView { Readers: { } readers })
            {
                continue;
            }
            foreach (var avatar in readers.Children.OfType<Avatar>())
            {
                if (avatar.Tag is string id && avatar.TranslatePoint(default, panel) is { } point)
                {
                    origins[id] = point - new Point(GetFlightOffset(avatar).X, GetFlightOffset(avatar).Y);
                }
            }
        }
        _receiptOrigins = origins.Count > 0 ? origins : null;
    }

    private static Vector GetFlightOffset(Visual avatar) =>
        avatar.RenderTransform is TranslateTransform t ? new Vector(-t.X, -t.Y) : default;

    private void FlyReceiptsLater()
    {
        if (_receiptOrigins is null)
        {
            return;
        }
        var origins = _receiptOrigins;
        _receiptOrigins = null;
        Dispatcher.UIThread.Post(() => FlyReceipts(origins), DispatcherPriority.Loaded);
    }

    /// <summary>Each reader's portrait glides from where it was to its new message on Arc's SETTLE spring.</summary>
    private void FlyReceipts(Dictionary<string, Point> origins)
    {
        if (_list?.ItemsPanelRoot is not { } panel)
        {
            return;
        }
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container is not ChatThreadMessageView { Readers: { } readers })
            {
                continue;
            }
            foreach (var avatar in readers.Children.OfType<Avatar>())
            {
                if (avatar.Tag is not string id || !origins.TryGetValue(id, out var from) || avatar.TranslatePoint(default, panel) is not { } to)
                {
                    continue;
                }
                var delta = from - to;
                if (Math.Abs(delta.X) < 0.5 && Math.Abs(delta.Y) < 0.5)
                {
                    continue;
                }
                var translate = avatar.RenderTransform as TranslateTransform ?? new TranslateTransform();
                avatar.RenderTransform = translate;
                var motion = new MotionValue<Vector>(avatar, new Vector(delta.X, delta.Y), MotionVectors.Vector);
                motion.Changed += v =>
                {
                    translate.X = v.X;
                    translate.Y = v.Y;
                };
                translate.X = delta.X;
                translate.Y = delta.Y;
                _ = FlyAsync(motion);
            }
        }
    }

    private static async Task FlyAsync(MotionValue<Vector> motion)
    {
        await motion.AnimateTo(default, MotionSpec.Spring(s_settle));
        motion.Dispose();
    }

    #endregion

    #region Reactions, retry, attachments, sending

    internal bool IsPickerOpenFor(ChatMessage message) => _picker is { IsOpen: true } picker && ReferenceEquals(picker.Message, message);

    internal void TogglePicker(ChatThreadMessageView view, ChatMessage message)
    {
        _picker ??= new ChatThreadPicker(this);
        if (IsPickerOpenFor(message))
        {
            _picker.Close(restoreFocus: false);
            return;
        }
        if (_picker.IsOpen)
        {
            _picker.Close(restoreFocus: false);
        }
        // The picker opens above the bubble unless that would run past the top of the thread.
        var below = false;
        if (view.Line is { } line && _scroller is not null && line.TranslatePoint(default, _scroller) is { } top)
        {
            below = top.Y < 56;
        }
        _picker.Open(view, message, view.Row?.Mine ?? false, below);
        RefreshMessage(message);
    }

    internal void OnPickerClosed() => RefreshRealized();

    internal void React(ChatMessage message, string emoji)
    {
        _picker?.Close(restoreFocus: false);
        RaiseEvent(new ChatReactionToggledEventArgs(ReactionToggledEvent, message, emoji));
        if (ItemsSource is not null)
        {
            return;
        }
        var list = new List<ChatReaction>(message.Reactions ?? Array.Empty<ChatReaction>());
        var at = list.FindIndex(r => r.Emoji == emoji);
        if (at < 0)
        {
            list.Add(new ChatReaction(emoji, 1, true));
        }
        else if (list[at].Mine)
        {
            list[at] = list[at] with { Count = list[at].Count - 1, Mine = false };
        }
        else
        {
            list[at] = list[at] with { Count = list[at].Count + 1, Mine = true };
        }
        message.Reactions = list.Where(r => r.Count > 0).ToArray();
    }

    internal void RequestRetry(ChatMessage message)
    {
        RaiseEvent(new ChatRetryEventArgs(RetryRequestedEvent, message));
        if (RetryCommand is { } command && command.CanExecute(message))
        {
            command.Execute(message);
        }
    }

    internal void OpenAttachment(ChatAttachment attachment)
    {
        var args = new ChatAttachmentEventArgs(AttachmentClickedEvent, attachment);
        RaiseEvent(args);
        if (args.Handled || TopLevel.GetTopLevel(this)?.Launcher is not { } launcher)
        {
            return;
        }
        if (attachment.File is { } file)
        {
            _ = launcher.LaunchFileAsync(file);
        }
        else if (Uri.TryCreate(attachment.Url, UriKind.Absolute, out var uri))
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    private void OnComposerSent(object? sender, ChatSendEventArgs e)
    {
        e.Handled = true;
        Send(e.Draft);
    }

    /// <summary>Sends a draft as if from the composer: pins to the bottom, raises <see cref="SendRequested"/> and, without an ItemsSource, appends it.</summary>
    internal void Send(ChatDraft draft)
    {
        SetPinned(true);
        RaiseEvent(new ChatSendEventArgs(SendRequestedEvent, draft));
        if (ItemsSource is not null)
        {
            return;
        }
        _counter++;
        var now = Now();
        Items.Add(new ChatMessage($"local-{now.ToUnixTimeMilliseconds()}-{_counter}", CurrentUserId ?? string.Empty,
            string.IsNullOrEmpty(draft.Text) ? null : draft.Text, now)
        {
            Attachments = draft.Attachments.Count > 0 ? draft.Attachments : null,
            Status = ChatMessageStatus.Sent
        });
    }

    #endregion

    #region Markdown bodies

    internal bool UsesMarkdown(ChatMessage message)
    {
        if (_bodies.IsStreaming(message))
        {
            return true;
        }
        var format = message.Format ?? MessageFormat;
        return format == ChatMessageFormat.Markdown && ChatThreadMarkdownSniffer.HasMarkdown(message.Text);
    }

    internal Markdown AcquireMarkdown(ChatMessage message, bool mine) => _bodies.Acquire(message, mine);

    /// <summary>Inline code on your own (accent) bubbles: the accent foreground at 18% over the accent.</summary>
    internal IBrush MineCodeBrush => _mineCodeBrush ??= Mix(0.18);

    /// <summary>Selected text on your own bubbles.</summary>
    internal IBrush MineSelectionBrush => _mineSelectionBrush ??= Mix(0.32);

    private IBrush Mix(double amount)
    {
        var accent = ColorOf(AccentBrush);
        var foreground = ColorOf(AccentForegroundBrush);
        return new ImmutableSolidColorBrush(Oklch.MixOklab(accent, foreground, amount));
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ChatThreadGroupPeer(this);
}

/// <summary>Arguments of <see cref="ChatThread.AttachmentClicked"/>.</summary>
public sealed class ChatAttachmentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChatAttachmentEventArgs(RoutedEvent routedEvent, ChatAttachment attachment)
        : base(routedEvent)
    {
        Attachment = attachment;
    }

    /// <summary>The picture or file clicked.</summary>
    public ChatAttachment Attachment { get; }
}

/// <summary>
/// The thread's message list: a virtualizing, recycling list of rows (day separators, messages, typing). Exposed to
/// assistive technology as a polite log named by <see cref="ChatThread.Label"/>.
/// </summary>
internal sealed class ChatThreadList : ItemsControl
{
    public ChatThread? Owner { get; set; }

    protected override Type StyleKeyOverride => typeof(ChatThreadList);

    public ChatThreadList()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = item?.GetType();
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => item switch
    {
        ChatThreadDayRow => new ChatThreadDayView(),
        ChatThreadTypingRow => new ChatThreadTypingView(),
        _ => new ChatThreadMessageView()
    };

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        switch (container)
        {
            case ChatThreadMessageView view when item is ChatThreadMessageRow row && Owner is { } owner:
                view.Bind(owner, row);
                break;
            case ChatThreadDayView day when item is ChatThreadDayRow dayRow:
                day.Bind(dayRow);
                break;
            case ChatThreadTypingView typing when item is ChatThreadTypingRow typingRow:
                typing.Bind(typingRow);
                break;
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        switch (container)
        {
            case ChatThreadMessageView view:
                view.Unbind();
                break;
            case ChatThreadDayView day:
                day.Unbind();
                break;
            case ChatThreadTypingView typing:
                typing.Unbind();
                break;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChatThreadLogPeer(this);

    private sealed class ChatThreadLogPeer(ChatThreadList owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>Chat line breaks: a single newline in a message stays a line break (Markdig's soft-line-as-hard-line).</summary>
internal sealed class ChatThreadLineBreakPlugin : IMarkdownPlugin
{
    public string Name => "chat-line-breaks";

    public void Configure(MarkdownPluginBuilder builder) =>
        builder.Profile.AddMarkdigExtension(new Markdig.Extensions.Hardlines.SoftlineBreakAsHardlineExtension());
}

/// <summary>
/// The jump pill's label: an arrow and "N new messages" with the number rolling (Core <see cref="RollingDigits"/>), or
/// "Jump to latest"; the text swaps with a subtle blur and the pill's width follows on the morph spring.
/// </summary>
internal sealed class ChatThreadPillLabel : StackPanel
{
    private readonly TextBlock _before = new();
    private readonly RollingDigits _count = new() { UseGrouping = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly TextBlock _after = new();
    private readonly ArcContentSwap _swap = new() { Mode = ContentSwapMode.BlurSwap };
    private readonly StackPanel _newer = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly TextBlock _latest = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private bool? _showsCount;

    public ChatThreadPillLabel()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
        Spacing = 6;
        var icon = new ArcGlyph { Data = ArcIcons.ArrowDown, Size = 14, StrokeThickness = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _before.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        _after.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        _newer.Children.Add(_before);
        _newer.Children.Add(_count);
        _newer.Children.Add(_after);
        Children.Add(icon);
        Children.Add(new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Spring = ArcSpring.Morph, Child = _swap });
        ArcTokens.Bind(_count, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_count, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_count, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
    }

    /// <summary>The visible text ("3 new messages" or "Jump to latest").</summary>
    public string Text => _showsCount == true ? $"{_before.Text}{_count.DisplayText}{_after.Text}" : _latest.Text ?? string.Empty;

    public void Update(int unread, ChatThreadLabels labels)
    {
        var showsCount = unread > 0;
        if (showsCount)
        {
            // Put the rolling number wherever the translated sentence puts it (Arc's withCount).
            var text = labels.NewMessages(unread);
            var digits = unread.ToString(CultureInfo.InvariantCulture);
            var at = text.IndexOf(digits, StringComparison.Ordinal);
            _before.Text = at < 0 ? text : text[..at];
            _count.IsVisible = at >= 0;
            _count.Value = unread;
            _after.Text = at < 0 ? string.Empty : text[(at + digits.Length)..];
        }
        else
        {
            _latest.Text = labels.JumpToLatest;
        }
        if (_showsCount != showsCount)
        {
            _showsCount = showsCount;
            _swap.Content = showsCount ? _newer : _latest;
        }
    }
}

/// <summary>
/// Markdown bodies of a thread, one <see cref="Markdown"/> per message, kept for the last 64 messages shown so a
/// bubble scrolled back into view keeps its parsed document (no re-parse, correct height at once). A streaming message
/// keeps its control for as long as it streams: <see cref="Append"/> feeds only the new text to it.
/// </summary>
internal sealed class ChatThreadBodies(ChatThread thread)
{
    private const int Capacity = 64;

    private readonly Dictionary<ChatMessage, Markdown> _bodies = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<ChatMessage> _recent = new();
    private readonly Dictionary<ChatMessage, LinkedListNode<ChatMessage>> _nodes = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ChatMessage> _streaming = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ChatMessage, bool> _mine = new(ReferenceEqualityComparer.Instance);
    private ChatMessage? _appending;

    public int Count => _bodies.Count;

    public bool IsStreaming(ChatMessage message) => _streaming.Contains(message);

    public Markdown? Peek(ChatMessage message) => _bodies.GetValueOrDefault(message);

    public Markdown Acquire(ChatMessage message, bool mine)
    {
        if (!_bodies.TryGetValue(message, out var markdown))
        {
            markdown = Create();
            markdown.Text = message.Text;
            _bodies[message] = markdown;
        }
        Touch(message);
        if (!_mine.TryGetValue(message, out var was) || was != mine)
        {
            _mine[message] = mine;
            ApplyColors(markdown, mine);
        }
        Trim();
        return markdown;
    }

    /// <summary>Streams <paramref name="text"/> onto <paramref name="message"/>.</summary>
    public void Append(ChatMessage message, string text)
    {
        var before = message.Text ?? string.Empty;
        var markdown = Acquire(message, message.AuthorId == thread.CurrentUserId);
        if (!_streaming.Contains(message) || !markdown.IsStreaming)
        {
            // A new reply starts from what the message already says.
            _streaming.Add(message);
            markdown.Append(before + text);
        }
        else
        {
            markdown.Append(text);
        }
        _appending = message;
        try
        {
            message.Text = before + text;
        }
        finally
        {
            _appending = null;
        }
    }

    public void Complete(ChatMessage message)
    {
        if (_bodies.TryGetValue(message, out var markdown) && _streaming.Remove(message))
        {
            markdown.Complete();
        }
    }

    /// <summary>The message's text changed outside <see cref="Append"/>: a static body shows the new text.</summary>
    public void OnTextChanged(ChatMessage message)
    {
        if (ReferenceEquals(_appending, message))
        {
            return;
        }
        if (_streaming.Remove(message) && _bodies.TryGetValue(message, out var streamed))
        {
            streamed.Text = message.Text;
            return;
        }
        if (_bodies.TryGetValue(message, out var markdown) && markdown.Text != message.Text)
        {
            markdown.Text = message.Text;
        }
    }

    public void Forget(ChatMessage message)
    {
        if (_bodies.Remove(message, out var markdown) && markdown.Parent is Decorator host)
        {
            host.Child = null;
        }
        _streaming.Remove(message);
        _mine.Remove(message);
        if (_nodes.Remove(message, out var node))
        {
            _recent.Remove(node);
        }
    }

    public void Clear()
    {
        foreach (var message in _bodies.Keys.ToList())
        {
            if (!_streaming.Contains(message))
            {
                Forget(message);
            }
        }
    }

    public void RefreshColors()
    {
        foreach (var (message, markdown) in _bodies)
        {
            ApplyColors(markdown, _mine.GetValueOrDefault(message));
        }
    }

    private void Touch(ChatMessage message)
    {
        if (_nodes.TryGetValue(message, out var node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
        }
        else
        {
            _nodes[message] = _recent.AddFirst(message);
        }
    }

    private void Trim()
    {
        var node = _recent.Last;
        while (_bodies.Count > Capacity && node is not null)
        {
            var previous = node.Previous;
            var message = node.Value;
            if (!_streaming.Contains(message) && _bodies.TryGetValue(message, out var markdown) && markdown.Parent is null)
            {
                Forget(message);
            }
            node = previous;
        }
    }

    private Markdown Create()
    {
        var markdown = new Markdown
        {
            LineHeight = thread.BubbleLineHeight,
            BlockSpacing = thread.BubbleBlockSpacing,
            Services = thread.EffectiveMarkdownServices,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        ArcTokens.Bind(markdown, Markdown.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        return markdown;
    }

    private void ApplyColors(Markdown markdown, bool mine)
    {
        var resources = markdown.Resources;
        string[] onAccent =
        [
            MarkdownResourceKeys.Foreground, MarkdownResourceKeys.SecondaryForeground, MarkdownResourceKeys.LinkForeground,
            MarkdownResourceKeys.ListMarkerForeground, MarkdownResourceKeys.InlineCodeForeground, MarkdownResourceKeys.QuoteBorderBrush
        ];
        if (mine)
        {
            foreach (var key in onAccent)
            {
                resources[key] = new MarkdownResourceAlias(ArcDesignTokenKind.AccentForeground);
            }
            resources[MarkdownResourceKeys.InlineCodeBackground] = thread.MineCodeBrush;
            resources[MarkdownResourceKeys.SelectionBackground] = thread.MineSelectionBrush;
        }
        else
        {
            foreach (var key in onAccent)
            {
                resources.Remove(key);
            }
            resources.Remove(MarkdownResourceKeys.InlineCodeBackground);
            resources.Remove(MarkdownResourceKeys.SelectionBackground);
        }
    }
}

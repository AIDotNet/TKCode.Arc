using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A message composer that unfurls links (Arc Link unfurl, design reference). Paste an address, or type one and a space,
/// and it becomes an inline link that shows its address under a soft loading sweep; once the preview resolves the
/// favicon and title roll in and the link unrolls into a card under the message (site, title, description and image) in
/// one continuous morph: the card's corner grows out of the link, width first and height after, lifting on a shadow
/// while it travels, and the toolbar glides down with it. Click the link, press the card's collapse button, press
/// Alt+Enter after the link or drag the card up to fold it back into the text; the remove button takes the link out of
/// the message.
/// <para>
/// Give it a <see cref="Resolver"/> that asks your own unfurl endpoint (Open Graph, oEmbed) for a
/// <see cref="LinkUnfurlPreview"/>; without one, <see cref="Samples"/> resolve by address and any other address gets a
/// plain preview built from it after a simulated 1.15s fetch. Enter or the send button raises
/// <see cref="SendRequested"/> (and calls <see cref="SendHandler"/>) with the text, links written out as addresses, and
/// the resolved previews.
/// </para>
/// </summary>
[TemplatePart(ComposerPart, typeof(Border))]
[TemplatePart(BodyPart, typeof(LinkUnfurlHeightHost))]
[TemplatePart(ContentPart, typeof(Control))]
[TemplatePart(EditorPart, typeof(TokenTextEditor))]
[TemplatePart(LanePart, typeof(LinkUnfurlLane))]
[TemplatePart(SamplesPart, typeof(Panel))]
[TemplatePart(TryPart, typeof(TextBlock))]
[TemplatePart(SendPart, typeof(LinkUnfurlSendButton))]
[TemplatePart(ErrorPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(CompactPseudoClass, TightPseudoClass, SendingPseudoClass, DraggingPseudoClass, ErrorPseudoClass)]
public sealed partial class LinkUnfurl : TemplatedControl
{
    internal const string ComposerPart = "PART_Composer";
    internal const string BodyPart = "PART_Body";
    internal const string ContentPart = "PART_Content";
    internal const string EditorPart = "PART_Editor";
    internal const string LanePart = "PART_Lane";
    internal const string SamplesPart = "PART_Samples";
    internal const string TryPart = "PART_Try";
    internal const string SendPart = "PART_Send";
    internal const string ErrorPart = "PART_Error";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string CompactPseudoClass = ":compact";
    internal const string TightPseudoClass = ":tight";
    internal const string SendingPseudoClass = ":sending";
    internal const string DraggingPseudoClass = ":dragging";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>The message field's accessible name (Arc's <c>label</c>), such as "Message #trail-club".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LinkUnfurl, string?>(nameof(Label), "Message");

    /// <summary>The text shown while the message is empty.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<LinkUnfurl, string?>(nameof(Placeholder), "Write a message");

    /// <summary>The message text; links stand in it as their addresses. Two-way: typing writes it; setting it replaces the message.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LinkUnfurl, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => value ?? string.Empty);

    /// <summary>
    /// Sample links offered as buttons under the message; the built-in resolver answers their addresses with them. Once a
    /// sample is in the message its button shows or folds its card. Null or empty hides the buttons.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<LinkUnfurlPreview>?> SamplesProperty =
        AvaloniaProperty.Register<LinkUnfurl, IReadOnlyList<LinkUnfurlPreview>?>(nameof(Samples));

    /// <summary>
    /// Fetches the preview of a pasted address (usually from your own unfurl endpoint); return null when there is none:
    /// the link then stays in the text without a card and is fetched again when clicked. The token is cancelled when the
    /// link is removed or the message is sent. Null uses the samples and plain previews after a simulated 1.15s fetch.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<LinkUnfurlPreview?>>?> ResolverProperty =
        AvaloniaProperty.Register<LinkUnfurl, Func<string, CancellationToken, Task<LinkUnfurlPreview?>>?>(nameof(Resolver));

    /// <summary>Unfurls a card as soon as its preview resolves (default). When false the link only gains its favicon and title, and opens on click.</summary>
    public static readonly StyledProperty<bool> AutoExpandProperty =
        AvaloniaProperty.Register<LinkUnfurl, bool>(nameof(AutoExpand), true);

    /// <summary>Pastes the first sample 1.1s after the composer first scrolls into view, unless someone has already interacted with it.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<LinkUnfurl, bool>(nameof(AutoPlay), true);

    /// <summary>Multiplier for the morph and the simulated fetch (1 = as measured; 0.1 to 10).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<LinkUnfurl, double>(nameof(Speed), 1.0, coerce: static (_, v) => double.IsNaN(v) ? 1 : Math.Clamp(v, 0.1, 10));

    /// <summary>Opens and folds cards instantly without the morph, shows a still tint instead of the sweep and turns autoplay off.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<LinkUnfurl, bool>(nameof(IsPaused));

    /// <summary>The color of an unfurled link and its tie to its card; null uses the accent.</summary>
    public static readonly StyledProperty<IBrush?> LinkAccentProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>(nameof(LinkAccent));

    /// <summary>Every word the composer shows or announces; replace to translate or reword.</summary>
    public static readonly StyledProperty<LinkUnfurlLabels> LabelsProperty =
        AvaloniaProperty.Register<LinkUnfurl, LinkUnfurlLabels>(nameof(Labels), new LinkUnfurlLabels());

    /// <summary>
    /// Sends a message after <see cref="SendRequested"/>: the composer holds while the task runs, then clears and
    /// confirms; a failed task shows its message under the composer and keeps the message.
    /// </summary>
    public static readonly StyledProperty<Func<LinkUnfurlMessage, Task>?> SendHandlerProperty =
        AvaloniaProperty.Register<LinkUnfurl, Func<LinkUnfurlMessage, Task>?>(nameof(SendHandler));

    /// <summary>True while a send is pending: the message is read-only and the send button holds.</summary>
    public static readonly DirectProperty<LinkUnfurl, bool> IsSendingProperty =
        AvaloniaProperty.RegisterDirect<LinkUnfurl, bool>(nameof(IsSending), o => o.IsSending);

    /// <summary>The message of the last failed send, shown under the composer as an alert; null when there is none.</summary>
    public static readonly DirectProperty<LinkUnfurl, string?> SendErrorProperty =
        AvaloniaProperty.RegisterDirect<LinkUnfurl, string?>(nameof(SendError), o => o.SendError);

    /// <summary>Raised on Enter or the send button with the message; set <see cref="LinkUnfurlSendRequestedEventArgs.Pending"/> to hold the composer.</summary>
    public static readonly RoutedEvent<LinkUnfurlSendRequestedEventArgs> SendRequestedEvent =
        RoutedEvent.Register<LinkUnfurl, LinkUnfurlSendRequestedEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when an address became a link (pasted, typed with a space after it, or from a sample).</summary>
    public static readonly RoutedEvent<LinkUnfurlLinkEventArgs> LinkAddedEvent =
        RoutedEvent.Register<LinkUnfurl, LinkUnfurlLinkEventArgs>(nameof(LinkAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when a link's fetch finished: its status is then ready (with a preview) or failed.</summary>
    public static readonly RoutedEvent<LinkUnfurlLinkEventArgs> LinkResolvedEvent =
        RoutedEvent.Register<LinkUnfurl, LinkUnfurlLinkEventArgs>(nameof(LinkResolved), RoutingStrategies.Bubble);

    /// <summary>Raised when a link left the message (the remove button, Backspace or Delete, a send or a clear).</summary>
    public static readonly RoutedEvent<LinkUnfurlLinkEventArgs> LinkRemovedEvent =
        RoutedEvent.Register<LinkUnfurl, LinkUnfurlLinkEventArgs>(nameof(LinkRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when a link's card opened or folded (<see cref="LinkUnfurlLink.IsExpanded"/> says which).</summary>
    public static readonly RoutedEvent<LinkUnfurlLinkEventArgs> PreviewToggledEvent =
        RoutedEvent.Register<LinkUnfurl, LinkUnfurlLinkEventArgs>(nameof(PreviewToggled), RoutingStrategies.Bubble);

    // Theme inks for the chips (the chips are painted by the editor's mirror, so their colors come in as properties).
    internal static readonly StyledProperty<IBrush?> ChipFillInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipFillInk");

    internal static readonly StyledProperty<IBrush?> ChipRingInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipRingInk");

    internal static readonly StyledProperty<IBrush?> ChipTextInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipTextInk");

    internal static readonly StyledProperty<IBrush?> ChipMutedInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipMutedInk");

    internal static readonly StyledProperty<IBrush?> ChipStrongInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipStrongInk");

    internal static readonly StyledProperty<IBrush?> ChipSurfaceInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("ChipSurfaceInk");

    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("AccentInk");

    internal static readonly StyledProperty<IBrush?> SweepInkProperty =
        AvaloniaProperty.Register<LinkUnfurl, IBrush?>("SweepInk");

    internal static readonly StyledProperty<double> AccentFillAmountProperty =
        AvaloniaProperty.Register<LinkUnfurl, double>("AccentFillAmount", 0.11);

    internal static readonly StyledProperty<double> AccentRingAmountProperty =
        AvaloniaProperty.Register<LinkUnfurl, double>("AccentRingAmount", 0.34);

    /// <summary>The wait before autoplay pastes the first sample (Arc 1097ms after the composer enters the view).</summary>
    internal static readonly TimeSpan AutoPlayDelay = TimeSpan.FromMilliseconds(1100);

    /// <summary>The simulated fetch (1.15s) and the extra time a sample's image takes to decode (≈110ms).</summary>
    internal static readonly TimeSpan SimulatedFetch = TimeSpan.FromMilliseconds(1150);

    internal static readonly TimeSpan ImageDecode = TimeSpan.FromMilliseconds(110);

    /// <summary>How long the send button shows its check (Arc ≈1.6s).</summary>
    internal static readonly TimeSpan SentHold = TimeSpan.FromMilliseconds(1600);

    private readonly List<LinkUnfurlEntry> _entries = new();
    private readonly List<LinkUnfurlLink> _links = new();
    private readonly LinkUnfurlChipRenderer _renderer;
    private readonly LinkUnfurlScheduler _scheduler;
    private readonly TranslateTransform _contentShift = new();
    private Border? _composer;
    private LinkUnfurlHeightHost? _body;
    private Control? _content;
    private TokenTextEditor? _editor;
    private LinkUnfurlLane? _lane;
    private Panel? _samples;
    private TextBlock? _try;
    private LinkUnfurlSendButton? _send;
    private TextBlock? _error;
    private ExplainerAnnouncer? _announcer;
    private ViewportVisibility? _viewport;
    private CancellationTokenSource? _autoPlay;
    private int _counter;
    private bool _converting;
    private bool _interacted;
    private bool _autoPlayed;
    private bool _isSending;
    private string? _sendError;
    private string? _startingText;
    private TokenSpan? _pressedToken;
    private double _dragStart;
    private double _dragHeight;
    private int _sendGeneration;
    private MotionValue<double>? _exit;
    private MotionValue<double>? _settle;

    /// <summary>Creates a composer with no samples.</summary>
    public LinkUnfurl()
    {
        _renderer = new LinkUnfurlChipRenderer(this);
        _scheduler = new LinkUnfurlScheduler(this);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _interacted = true, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    #region Properties

    /// <summary>The message field's accessible name (Arc's <c>label</c>), such as "Message #trail-club".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The text shown while the message is empty.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>The message text; links stand in it as their addresses. Two-way: typing writes it; setting it replaces the message.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Sample links offered as buttons under the message; the built-in resolver answers their addresses with them. Once a
    /// sample is in the message its button shows or folds its card. Null or empty hides the buttons.
    /// </summary>
    public IReadOnlyList<LinkUnfurlPreview>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    /// <summary>
    /// Fetches the preview of a pasted address (usually from your own unfurl endpoint); return null when there is none:
    /// the link then stays in the text without a card and is fetched again when clicked. The token is cancelled when the
    /// link is removed or the message is sent. Null uses the samples and plain previews after a simulated 1.15s fetch.
    /// </summary>
    public Func<string, CancellationToken, Task<LinkUnfurlPreview?>>? Resolver
    {
        get => GetValue(ResolverProperty);
        set => SetValue(ResolverProperty, value);
    }

    /// <summary>Unfurls a card as soon as its preview resolves (default). When false the link only gains its favicon and title, and opens on click.</summary>
    public bool AutoExpand
    {
        get => GetValue(AutoExpandProperty);
        set => SetValue(AutoExpandProperty, value);
    }

    /// <summary>Pastes the first sample 1.1s after the composer first scrolls into view, unless someone has already interacted with it.</summary>
    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    /// <summary>Multiplier for the morph and the simulated fetch (1 = as measured; 0.1 to 10).</summary>
    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <summary>Opens and folds cards instantly without the morph, shows a still tint instead of the sweep and turns autoplay off.</summary>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <summary>The color of an unfurled link and its tie to its card; null uses the accent.</summary>
    public IBrush? LinkAccent
    {
        get => GetValue(LinkAccentProperty);
        set => SetValue(LinkAccentProperty, value);
    }

    /// <summary>Every word the composer shows or announces; replace to translate or reword.</summary>
    public LinkUnfurlLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>
    /// Sends a message after <see cref="SendRequested"/>: the composer holds while the task runs, then clears and
    /// confirms; a failed task shows its message under the composer and keeps the message.
    /// </summary>
    public Func<LinkUnfurlMessage, Task>? SendHandler
    {
        get => GetValue(SendHandlerProperty);
        set => SetValue(SendHandlerProperty, value);
    }

    /// <summary>True while a send is pending: the message is read-only and the send button holds.</summary>
    public bool IsSending
    {
        get => _isSending;
        private set => SetAndRaise(IsSendingProperty, ref _isSending, value);
    }

    /// <summary>The message of the last failed send, shown under the composer as an alert; null when there is none.</summary>
    public string? SendError
    {
        get => _sendError;
        private set => SetAndRaise(SendErrorProperty, ref _sendError, value);
    }

    /// <summary>The links in the message, in the order they appear.</summary>
    public IReadOnlyList<LinkUnfurlLink> Links => _links;

    /// <summary>Raised on Enter or the send button with the message; set <see cref="LinkUnfurlSendRequestedEventArgs.Pending"/> to hold the composer.</summary>
    public event EventHandler<LinkUnfurlSendRequestedEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    /// <summary>Raised when an address became a link (pasted, typed with a space after it, or from a sample).</summary>
    public event EventHandler<LinkUnfurlLinkEventArgs>? LinkAdded
    {
        add => AddHandler(LinkAddedEvent, value);
        remove => RemoveHandler(LinkAddedEvent, value);
    }

    /// <summary>Raised when a link's fetch finished: its status is then ready (with a preview) or failed.</summary>
    public event EventHandler<LinkUnfurlLinkEventArgs>? LinkResolved
    {
        add => AddHandler(LinkResolvedEvent, value);
        remove => RemoveHandler(LinkResolvedEvent, value);
    }

    /// <summary>Raised when a link left the message (the remove button, Backspace or Delete, a send or a clear).</summary>
    public event EventHandler<LinkUnfurlLinkEventArgs>? LinkRemoved
    {
        add => AddHandler(LinkRemovedEvent, value);
        remove => RemoveHandler(LinkRemovedEvent, value);
    }

    /// <summary>Raised when a link's card opened or folded (<see cref="LinkUnfurlLink.IsExpanded"/> says which).</summary>
    public event EventHandler<LinkUnfurlLinkEventArgs>? PreviewToggled
    {
        add => AddHandler(PreviewToggledEvent, value);
        remove => RemoveHandler(PreviewToggledEvent, value);
    }

    #endregion

    #region Internals for parts and tests

    internal IBrush? ChipFillInk
    {
        get => GetValue(ChipFillInkProperty);
        set => SetValue(ChipFillInkProperty, value);
    }

    internal IBrush? ChipRingInk
    {
        get => GetValue(ChipRingInkProperty);
        set => SetValue(ChipRingInkProperty, value);
    }

    internal IBrush? ChipTextInk
    {
        get => GetValue(ChipTextInkProperty);
        set => SetValue(ChipTextInkProperty, value);
    }

    internal IBrush? ChipMutedInk
    {
        get => GetValue(ChipMutedInkProperty);
        set => SetValue(ChipMutedInkProperty, value);
    }

    internal IBrush? ChipStrongInk
    {
        get => GetValue(ChipStrongInkProperty);
        set => SetValue(ChipStrongInkProperty, value);
    }

    internal IBrush? ChipSurfaceInk
    {
        get => GetValue(ChipSurfaceInkProperty);
        set => SetValue(ChipSurfaceInkProperty, value);
    }

    internal IBrush? AccentInk
    {
        get => GetValue(AccentInkProperty);
        set => SetValue(AccentInkProperty, value);
    }

    internal IBrush? SweepInk
    {
        get => GetValue(SweepInkProperty);
        set => SetValue(SweepInkProperty, value);
    }

    internal double AccentFillAmount
    {
        get => GetValue(AccentFillAmountProperty);
        set => SetValue(AccentFillAmountProperty, value);
    }

    internal double AccentRingAmount
    {
        get => GetValue(AccentRingAmountProperty);
        set => SetValue(AccentRingAmountProperty, value);
    }


    internal TokenTextEditor? Editor => _editor;

    internal LinkUnfurlLane? Lane => _lane;

    internal LinkUnfurlSendButton? SendButton => _send;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal LinkUnfurlChipRenderer ChipRenderer => _renderer;

    internal LinkUnfurlScheduler Scheduler => _scheduler;

    internal Panel? SamplesPanel => _samples;

    internal TextBlock? TryText => _try;

    internal TextBlock? ErrorText => _error;

    internal Control? ContentLayer => _content;

    internal LinkUnfurlHeightHost? Body => _body;

    internal IReadOnlyList<LinkUnfurlEntry> Entries => _entries;

    /// <summary>Someone pressed, typed or pasted in the composer (autoplay then stays off).</summary>
    internal bool HasInteracted => _interacted;

    /// <summary>The link with <paramref name="id"/> still in the message, or null.</summary>
    internal LinkUnfurlEntry? EntryFor(string id)
    {
        foreach (var entry in _entries)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>Morph and fetch run instantly: paused, or motion below full.</summary>
    internal bool IsInstant => IsPaused || !ArcMotion.IsFull(this);

    #endregion

    /// <summary>A plain preview built from an address alone: site = host, title from the last path segment, description = the address without its scheme.</summary>
    public static LinkUnfurlPreview CreatePlainPreview(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return LinkUnfurlAddress.PlainPreview(LinkUnfurlAddress.TryParse(address, out var url) ? url : address);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_editor is not null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.SubmitRequested -= OnEditorSubmit;
            _editor.RemoveHandler(PointerPressedEvent, OnEditorPointerPressed);
            _editor.RemoveHandler(PointerReleasedEvent, OnEditorPointerReleased);
            _editor.TokenRenderer = null;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_lane is not null)
        {
            _lane.Owner = null;
            _lane.Children.Clear();
        }
        _composer = e.NameScope.Find<Border>(ComposerPart);
        _body = e.NameScope.Find<LinkUnfurlHeightHost>(BodyPart);
        _content = e.NameScope.Find<Control>(ContentPart);
        _editor = e.NameScope.Find<TokenTextEditor>(EditorPart);
        _lane = e.NameScope.Find<LinkUnfurlLane>(LanePart);
        _samples = e.NameScope.Find<Panel>(SamplesPart);
        _try = e.NameScope.Find<TextBlock>(TryPart);
        _send = e.NameScope.Find<LinkUnfurlSendButton>(SendPart);
        _error = e.NameScope.Find<TextBlock>(ErrorPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_content is not null)
        {
            _content.RenderTransform = _contentShift;
        }
        if (_editor is not null)
        {
            _editor.TokenRenderer = _renderer;
            _converting = true;
            try
            {
                _editor.Text = Text;
            }
            finally
            {
                _converting = false;
            }
            _editor.TextChanged += OnEditorTextChanged;
            _editor.SubmitRequested += OnEditorSubmit;
            _editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _editor.AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
        if (_lane is not null)
        {
            _lane.Owner = this;
            foreach (var entry in _entries)
            {
                if (entry.Card is { } card)
                {
                    _lane.Children.Add(card);
                }
            }
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        UpdateNames();
        RebuildSamples();
        UpdateError();
        UpdateChipColors();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _startingText ??= Text;
        _viewport?.Dispose();
        _viewport = new ViewportVisibility(this, 0.5, once: true);
        _viewport.VisibilityChanged += OnViewportVisibilityChanged;
        if (_viewport.IsVisible)
        {
            OnViewportVisibilityChanged(this, true);
        }
        UpdateSweep();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibilityChanged;
            _viewport.Dispose();
            _viewport = null;
        }
        // Offscreen, cards land at once (Arc settles them when the composer leaves the view).
        foreach (var entry in _entries)
        {
            entry.Progress.Jump(entry.Link.IsExpanded ? 1 : 0);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var width = Math.Min(e.NewSize.Width, 576);
        PseudoClasses.Set(CompactPseudoClass, width < 560);
        PseudoClasses.Set(TightPseudoClass, width < 360);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (_editor is not null && !string.Equals(_editor.Text, Text, StringComparison.Ordinal))
            {
                _editor.Text = Text;
            }
        }
        else if (change.Property == LabelProperty || change.Property == LabelsProperty || change.Property == PlaceholderProperty)
        {
            UpdateNames();
            UpdateSampleButtons();
        }
        else if (change.Property == SamplesProperty)
        {
            RebuildSamples();
        }
        else if (change.Property == IsPausedProperty)
        {
            UpdateSweep();
            if (IsPaused)
            {
                _autoPlay?.Cancel();
                foreach (var entry in _entries)
                {
                    entry.Progress.Jump(entry.Link.IsExpanded ? 1 : 0);
                }
            }
        }
        else if (change.Property == ChipFillInkProperty || change.Property == ChipRingInkProperty || change.Property == ChipTextInkProperty ||
                 change.Property == ChipMutedInkProperty || change.Property == ChipStrongInkProperty || change.Property == ChipSurfaceInkProperty ||
                 change.Property == AccentInkProperty || change.Property == SweepInkProperty || change.Property == LinkAccentProperty ||
                 change.Property == AccentFillAmountProperty || change.Property == AccentRingAmountProperty)
        {
            UpdateChipColors();
        }
        else if (change.Property == IsSendingProperty)
        {
            PseudoClasses.Set(SendingPseudoClass, IsSending);
        }
        else if (change.Property == SendErrorProperty)
        {
            UpdateError();
        }
    }

    private void UpdateNames()
    {
        if (_editor is not null)
        {
            AutomationProperties.SetName(_editor, Label);
            AutomationProperties.SetHelpText(_editor, Labels.KeyHint);
            _editor.Placeholder = Placeholder;
        }
        if (_samples is not null)
        {
            AutomationProperties.SetName(_samples, Labels.SamplesGroup);
        }
        if (_try is not null)
        {
            _try.Text = Labels.Try;
        }
        if (_send is not null)
        {
            AutomationProperties.SetName(_send, IsSending ? Labels.Sending : Labels.Send);
        }
        foreach (var entry in _entries)
        {
            if (entry.Card is { } card && entry.Link.Preview is { } preview)
            {
                card.UpdateNames(preview, Labels);
            }
        }
        UpdateEditorStatus();
    }

    private void UpdateError()
    {
        PseudoClasses.Set(ErrorPseudoClass, SendError is not null);
        if (_error is not null)
        {
            _error.Text = SendError;
            _error.IsVisible = SendError is not null;
            AutomationProperties.SetName(_error, SendError);
        }
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? Oklch.WithAlpha(solid.Color, solid.Color.A / 255.0 * solid.Opacity) : Colors.Transparent;

    private void UpdateChipColors()
    {
        var accent = ColorOf(LinkAccent ?? GetValue(AccentInkProperty));
        _renderer.SetColors(new LinkUnfurlChipColors(
            ColorOf(GetValue(ChipFillInkProperty)),
            ColorOf(GetValue(ChipRingInkProperty)),
            ColorOf(GetValue(ChipTextInkProperty)),
            ColorOf(GetValue(ChipMutedInkProperty)),
            ColorOf(GetValue(ChipStrongInkProperty)),
            ColorOf(GetValue(ChipSurfaceInkProperty)),
            accent,
            ColorOf(GetValue(SweepInkProperty)),
            GetValue(AccentFillAmountProperty),
            GetValue(AccentRingAmountProperty)));
    }

    /// <summary>The sweep runs while a link loads, at full motion and not paused (LiveLoop checks the rest).</summary>
    private void UpdateSweep()
    {
        var loading = false;
        foreach (var entry in _entries)
        {
            if (entry.Link.Status == LinkUnfurlStatus.Loading)
            {
                loading = true;
                break;
            }
        }
        _renderer.UpdateSweep(loading && !IsPaused);
    }

    /// <summary>Moves keyboard focus to the message (a folding card gives it back here).</summary>
    internal void FocusEditor()
    {
        if (_editor is { } editor)
        {
            editor.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>Moves keyboard focus into the message.</summary>
    public void FocusMessage() => _editor?.Focus();

    #region Autoplay

    private void OnViewportVisibilityChanged(object? sender, bool visible)
    {
        if (visible)
        {
            StartAutoPlay();
        }
    }

    private async void StartAutoPlay()
    {
        if (_autoPlayed || !AutoPlay || IsPaused || _interacted || Samples is not { Count: > 0 })
        {
            return;
        }
        _autoPlayed = true;
        _autoPlay?.Cancel();
        var cts = new CancellationTokenSource();
        _autoPlay = cts;
        try
        {
            await _scheduler.Delay(AutoPlayDelay, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (cts.IsCancellationRequested || _interacted || IsPaused || !AutoPlay || Samples is not { Count: > 0 } samples)
        {
            return;
        }
        if (FindByAddress(samples[0].Url) is null)
        {
            AddLink(samples[0].Url, atEnd: true);
        }
    }

    /// <summary>
    /// Restores the starting message, removes every link and plays autoplay again (after 1.1s) as if the composer had
    /// just come into view.
    /// </summary>
    public void Replay()
    {
        _autoPlay?.Cancel();
        ClearAll(removeEvents: true);
        _converting = true;
        try
        {
            _editor?.SetTextWithTokens(_startingText ?? string.Empty, Array.Empty<TokenSpan>());
            if (_editor is null)
            {
                SetCurrentValue(TextProperty, _startingText ?? string.Empty);
            }
        }
        finally
        {
            _converting = false;
        }
        _interacted = false;
        _autoPlayed = false;
        SendError = null;
        StartAutoPlay();
    }

    #endregion

    #region Input

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        _interacted = true;
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && _editor is { } editor && editor.IsKeyboardFocusWithin &&
            !editor.IsComposing)
        {
            e.Handled = true;
            ToggleLinkBeforeCaret();
        }
    }

    /// <summary>Alt+Enter: shows or folds the card of the nearest link before the caret.</summary>
    private void ToggleLinkBeforeCaret()
    {
        if (_editor is not { } editor)
        {
            return;
        }
        var caret = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        LinkUnfurlEntry? found = null;
        var best = -1;
        foreach (var token in editor.Tokens)
        {
            if (token.Kind == LinkUnfurlChipRenderer.Kind && token.End <= caret && token.End > best && EntryFor(token.Id) is { } entry)
            {
                best = token.End;
                found = entry;
            }
        }
        if (found is not null)
        {
            Activate(found);
        }
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedToken = null;
        if (_editor is { } editor && e.GetCurrentPoint(editor).Properties.IsLeftButtonPressed)
        {
            _pressedToken = editor.HitTestToken(e.GetPosition(editor));
        }
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressedToken;
        _pressedToken = null;
        if (pressed is null || _editor is not { } editor || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }
        if (editor.HitTestToken(e.GetPosition(editor)) is { } token && token.Id == pressed.Id && EntryFor(token.Id) is { } entry)
        {
            Activate(entry);
        }
    }

    /// <summary>A link was clicked (or Alt+Enter, or its sample button): toggle its card, or fetch a failed one again.</summary>
    private void Activate(LinkUnfurlEntry entry)
    {
        switch (entry.Link.Status)
        {
            case LinkUnfurlStatus.Ready:
                TogglePreview(entry.Id);
                break;
            case LinkUnfurlStatus.Failed:
                Retry(entry.Id);
                break;
        }
    }

    #endregion
}

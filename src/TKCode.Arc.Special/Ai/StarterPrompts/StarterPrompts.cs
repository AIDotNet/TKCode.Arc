using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The empty state of an AI app (Arc Starter prompts, design reference): a greeting, starter cards filtered by category
/// tabs, a Shuffle that swaps the cards with a short staggered crossfade, and a composer the cards fill. Picking a card
/// places its full prompt in the composer, focuses it and raises <see cref="PromptSelected"/>; Enter sends and raises
/// <see cref="SubmitRequested"/>. Cards that use a connected app show its mark and name.
/// <para>
/// With no <see cref="Prompts"/> it shows the greeting, the All tab and the composer with no cards, and with no handler
/// that supplies <see cref="StarterPromptsSubmitEventArgs.Sending"/> it simulates the send. Set <see cref="ShowsComposer"/> to
/// false to pair the cards with a composer of your own: picks still set <see cref="Text"/> and raise the event.
/// </para>
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(TabsPart, typeof(Tabs))]
[TemplatePart(ShufflePart, typeof(AvaloniaButton))]
[TemplatePart(GridPart, typeof(Panel))]
[TemplatePart(InputPart, typeof(TextBox))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(SendPart, typeof(AvaloniaButton))]
[TemplatePart(ModelPart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(LoadingPseudoClass, SendingPseudoClass, CompactPseudoClass, NarrowPseudoClass, NoDescriptionPseudoClass)]
public class StarterPrompts : TemplatedControl
{
    internal const string SurfacePart = "PART_Surface";
    internal const string TabsPart = "PART_Tabs";
    internal const string ShufflePart = "PART_Shuffle";
    internal const string GridPart = "PART_Grid";
    internal const string InputPart = "PART_Input";
    internal const string StatusPart = "PART_Status";
    internal const string SendPart = "PART_Send";
    internal const string ModelPart = "PART_Model";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string LoadingPseudoClass = ":loading";
    internal const string SendingPseudoClass = ":sending";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string NoDescriptionPseudoClass = ":no-description";

    /// <summary>The new text's fade in after a pick (Arc: 160ms).</summary>
    internal static readonly TimeSpan TextFadeDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>The heading.</summary>
    public static readonly StyledProperty<string?> GreetingProperty =
        AvaloniaProperty.Register<StarterPrompts, string?>(nameof(Greeting));

    /// <summary>One quiet line under the heading; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<StarterPrompts, string?>(nameof(Description));

    /// <summary>The id of the selected tab (two-way): <c>"all"</c> or a <see cref="StarterPromptCategory.Id"/>.</summary>
    public static readonly StyledProperty<string> SelectedCategoryProperty =
        AvaloniaProperty.Register<StarterPrompts, string>(nameof(SelectedCategory), StarterPromptsDeck.AllId,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cards on screen at once. The grid keeps this many slots so nothing jumps.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<StarterPrompts, int>(nameof(Count), 4, coerce: (_, value) => Math.Clamp(value, 1, 12));

    /// <summary>Shows placeholder cards with the same size and rhythm while the starters load.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<StarterPrompts, bool>(nameof(IsLoading));

    /// <summary>The composer's text (two-way); changes from typing and from a picked card.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StarterPrompts, string?>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The composer's placeholder; null (the default) shows none.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<StarterPrompts, string?>(nameof(Placeholder));

    /// <summary>The model named in the composer's footer; null (the default) shows the mark alone.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<StarterPrompts, string?>(nameof(ModelName));

    /// <summary>The model's mark beside its name (a 24 × 24 stroked glyph); the sparkle by default, null hides it.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<StarterPrompts, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>Shows the built-in composer. Turn it off to pair the cards with a composer of your own.</summary>
    public static readonly StyledProperty<bool> ShowsComposerProperty =
        AvaloniaProperty.Register<StarterPrompts, bool>(nameof(ShowsComposer), true);

    /// <summary>Every word the control shows or says, beyond the greeting, description and placeholder.</summary>
    public static readonly StyledProperty<StarterPromptsLabels> LabelsProperty =
        AvaloniaProperty.Register<StarterPrompts, StarterPromptsLabels>(nameof(Labels), StarterPromptsLabels.Default);

    /// <summary>Where the composer is with its message: idle, sending, sent, stopped or failed.</summary>
    public static readonly DirectProperty<StarterPrompts, StarterPromptsSendStatus> SendStatusProperty =
        AvaloniaProperty.RegisterDirect<StarterPrompts, StarterPromptsSendStatus>(nameof(SendStatus), o => o.SendStatus);

    /// <summary>The message of the last failed send, or null.</summary>
    public static readonly DirectProperty<StarterPrompts, string?> SendErrorProperty =
        AvaloniaProperty.RegisterDirect<StarterPrompts, string?>(nameof(SendError), o => o.SendError);

    /// <summary>Raised when a card is picked, after its prompt fills the composer. Bubbles.</summary>
    public static readonly RoutedEvent<StarterPromptSelectedEventArgs> PromptSelectedEvent =
        RoutedEvent.Register<StarterPrompts, StarterPromptSelectedEventArgs>(nameof(PromptSelected), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when the composer sends (Enter or the send button). Bubbles. Put your request's task in
    /// <see cref="StarterPromptsSubmitEventArgs.Sending"/> and honor its cancellation token; without it the send is simulated.
    /// </summary>
    public static readonly RoutedEvent<StarterPromptsSubmitEventArgs> SubmitRequestedEvent =
        RoutedEvent.Register<StarterPrompts, StarterPromptsSubmitEventArgs>(nameof(SubmitRequested), RoutingStrategies.Bubble);

    private readonly AvaloniaList<StarterPrompt> _prompts = [];
    private readonly AvaloniaList<StarterPromptCategory> _categories = [];
    private readonly StarterPromptsDeck _deck;
    private readonly List<StarterPromptCard> _cards = [];
    private readonly List<string> _tabIds = [];
    private StarterPromptsSendStatus _sendStatus;
    private string? _sendError;
    private bool _sentSimulated;
    private Border? _surface;
    private Tabs? _tabs;
    private AvaloniaButton? _shuffle;
    private StarterPromptsGrid? _grid;
    private StarterPromptsInput? _input;
    private ArcContentSwap? _status;
    private StarterPromptsSendButton? _send;
    private Control? _model;
    private ExplainerAnnouncer? _announcer;
    private MotionValue<double>? _textFade;
    private CancellationTokenSource? _sending;
    private int _sendVersion;
    private bool _syncingTabs;
    private bool _settingText;
    private bool _deckDirty = true;
    private StarterPrompt? _picked;

    /// <summary>The tab's icon and the gap to its label (the headers are built here, inside the Tabs' own template).</summary>
    private const double TabIconSize = 14;
    private const double TabIconGap = 6;

    /// <summary>Creates the control.</summary>
    public StarterPrompts() : this(null)
    {
    }

    /// <summary>Creates the control with a fixed shuffle order (tests).</summary>
    internal StarterPrompts(int? seed)
    {
        _deck = new StarterPromptsDeck(seed);
        _prompts.CollectionChanged += OnDataChanged;
        _categories.CollectionChanged += OnDataChanged;
    }

    /// <inheritdoc cref="GreetingProperty"/>
    public string? Greeting { get => GetValue(GreetingProperty); set => SetValue(GreetingProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SelectedCategoryProperty"/>
    public string SelectedCategory { get => GetValue(SelectedCategoryProperty); set => SetValue(SelectedCategoryProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="ShowsComposerProperty"/>
    public bool ShowsComposer { get => GetValue(ShowsComposerProperty); set => SetValue(ShowsComposerProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public StarterPromptsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SendStatusProperty"/>
    public StarterPromptsSendStatus SendStatus { get => _sendStatus; private set => SetAndRaise(SendStatusProperty, ref _sendStatus, value); }

    /// <inheritdoc cref="SendErrorProperty"/>
    public string? SendError { get => _sendError; private set => SetAndRaise(SendErrorProperty, ref _sendError, value); }

    /// <summary>The starters: <c>{ Id, Category, Title, Description, Prompt, App }</c>. Empty shows no cards.</summary>
    [Content]
    public AvaloniaList<StarterPrompt> Prompts => _prompts;

    /// <summary>The tabs in order. All is added in front and tabs with no starters are hidden. Empty shows All alone.</summary>
    public AvaloniaList<StarterPromptCategory> Categories => _categories;

    /// <inheritdoc cref="PromptSelectedEvent"/>
    public event EventHandler<StarterPromptSelectedEventArgs>? PromptSelected
    {
        add => AddHandler(PromptSelectedEvent, value);
        remove => RemoveHandler(PromptSelectedEvent, value);
    }

    /// <inheritdoc cref="SubmitRequestedEvent"/>
    public event EventHandler<StarterPromptsSubmitEventArgs>? SubmitRequested
    {
        add => AddHandler(SubmitRequestedEvent, value);
        remove => RemoveHandler(SubmitRequestedEvent, value);
    }

    /// <summary>How long a simulated send takes.</summary>
    internal TimeSpan SimulatedSendDuration { get; set; } = TimeSpan.FromSeconds(1.4);

    /// <summary>The starters in use.</summary>
    internal IReadOnlyList<StarterPrompt> DisplayedPrompts => _prompts;

    /// <summary>The categories in use.</summary>
    internal IReadOnlyList<StarterPromptCategory> DisplayedCategories => _categories;

    /// <summary>The ids of the tabs on screen, All first.</summary>
    internal IReadOnlyList<string> TabIds => _tabIds;

    /// <summary>The cards (slots), in order.</summary>
    internal IReadOnlyList<StarterPromptCard> Cards => _cards;

    /// <summary>The starters the cards are showing or moving to.</summary>
    internal IReadOnlyList<StarterPrompt?> VisiblePrompts => [.. _cards.Select(c => c.Target)];

    /// <summary>The last announcement of the live region.</summary>
    internal string? Announcement => _announcer?.Announcement;

    internal Tabs? TabList => _tabs;

    internal AvaloniaButton? ShuffleButton => _shuffle;

    internal StarterPromptsInput? Input => _input;

    internal StarterPromptsSendButton? SendButton => _send;

    internal StarterPromptsGrid? Grid => _grid;

    internal ArcContentSwap? StatusSwap => _status;

    /// <summary>Shows the next starters of the selected tab with the staggered crossfade.</summary>
    public void Shuffle()
    {
        EnsureDeck();
        var tab = CurrentTab();
        if (IsLoading || !_deck.CanShuffle(tab, Count))
        {
            return;
        }
        var page = _deck.Shuffle(tab, Count);
        ShowPage(page, animate: true);
        Announce(Labels.Shuffled(page.Count));
    }

    /// <summary>
    /// Fills the composer with <paramref name="prompt"/>'s full prompt, focuses it and raises <see cref="PromptSelected"/>,
    /// as picking its card does.
    /// </summary>
    public void Pick(StarterPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (SendStatus == StarterPromptsSendStatus.Sending)
        {
            return;
        }
        _picked = prompt;
        SetText(prompt.Prompt, resetStatus: true);
        if (ShowsComposer && _input is not null)
        {
            _input.Focus(NavigationMethod.Tab);
            _input.CaretIndex = _input.Text?.Length ?? 0;
            FadeTextIn();
        }
        Announce(Labels.Picked(prompt.Title));
        RaiseEvent(new StarterPromptSelectedEventArgs(PromptSelectedEvent, prompt));
    }

    /// <summary>Sends the composer's text, as Enter does. Does nothing while a send is in flight or the text is blank.</summary>
    public void Submit()
    {
        if (SendStatus == StarterPromptsSendStatus.Sending)
        {
            return;
        }
        var message = (Text ?? string.Empty).Trim();
        if (message.Length == 0)
        {
            return;
        }
        _sending?.Dispose();
        _sending = new CancellationTokenSource();
        var version = ++_sendVersion;
        var picked = _picked is not null && message.Contains(_picked.Prompt.Trim(), StringComparison.Ordinal) ? _picked : null;
        var args = new StarterPromptsSubmitEventArgs(SubmitRequestedEvent, message, picked, _sending.Token);
        SetStatus(StarterPromptsSendStatus.Sending, null);
        Announce(Labels.SendingAnnouncement);
        RaiseEvent(args);
        var simulated = args.Sending is null;
        var task = args.Sending ?? Simulate(_sending.Token);
        _ = Await(task, version, simulated);
    }

    /// <summary>Stops a send in flight, as Stop and Escape do: its token is cancelled and the text is kept.</summary>
    public void StopSending()
    {
        if (SendStatus != StarterPromptsSendStatus.Sending)
        {
            return;
        }
        _sendVersion++;
        _sending?.Cancel();
        SetStatus(StarterPromptsSendStatus.Stopped, null);
        Announce(Labels.StoppedAnnouncement);
        _input?.Focus(NavigationMethod.Tab);
    }

    /// <summary>Moves keyboard focus to the composer.</summary>
    public void FocusComposer() => _input?.Focus(NavigationMethod.Tab);

    private Task Simulate(CancellationToken token) =>
        SimulatedSendDuration <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(SimulatedSendDuration, token);

    private async Task Await(Task task, int version, bool simulated)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            if (version != _sendVersion)
            {
                return;
            }
            var message = string.IsNullOrWhiteSpace(error.Message) ? Labels.Failed : error.Message;
            SetStatus(StarterPromptsSendStatus.Failed, message);
            Announce(Labels.FailedAnnouncement(message));
            return;
        }
        if (version != _sendVersion || SendStatus != StarterPromptsSendStatus.Sending)
        {
            return;
        }
        _picked = null;
        _sentSimulated = simulated;
        SetText(string.Empty, resetStatus: false);
        SetStatus(StarterPromptsSendStatus.Sent, null);
        Announce(simulated ? Labels.SentSimulatedAnnouncement : Labels.SentAnnouncement);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _tabs = e.NameScope.Find<Tabs>(TabsPart);
        _shuffle = e.NameScope.Find<AvaloniaButton>(ShufflePart);
        _grid = e.NameScope.Find<StarterPromptsGrid>(GridPart);
        _input = e.NameScope.Find<StarterPromptsInput>(InputPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _send = e.NameScope.Find<StarterPromptsSendButton>(SendPart);
        _model = e.NameScope.Find<Control>(ModelPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _textFade = null;
        if (_tabs is not null)
        {
            _tabs.SelectionChanged += OnTabSelectionChanged;
            _tabs.TemplateApplied += OnTabsTemplateApplied;
        }
        if (_shuffle is not null)
        {
            _shuffle.Click += OnShuffleClick;
        }
        if (e.NameScope.Find<ArcGlyph>("PART_ShuffleGlyph") is { } shuffleGlyph)
        {
            shuffleGlyph.Data = StarterPromptsIcons.Shuffle;
        }
        if (_input is not null)
        {
            _input.Text = Text;
            _input.TextChanged += OnInputTextChanged;
            _input.SendRequested += OnInputSend;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        _cards.Clear();
        _grid?.Children.Clear();
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        PseudoClasses.Set(NoDescriptionPseudoClass, string.IsNullOrEmpty(Description));
        _deckDirty = true;
        RebuildAll();
        UpdateComposer();
        UpdateStatusView();
    }

    private void Unhook()
    {
        if (_tabs is not null)
        {
            _tabs.SelectionChanged -= OnTabSelectionChanged;
            _tabs.TemplateApplied -= OnTabsTemplateApplied;
        }
        if (_shuffle is not null)
        {
            _shuffle.Click -= OnShuffleClick;
        }
        if (_input is not null)
        {
            _input.TextChanged -= OnInputTextChanged;
            _input.SendRequested -= OnInputSend;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        foreach (var card in _cards)
        {
            card.Click -= OnCardClick;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedCategoryProperty)
        {
            if (!_syncingTabs)
            {
                SyncTabSelection();
                ShowPage(animate: true);
            }
        }
        else if (change.Property == CountProperty)
        {
            _deckDirty = true;
            RebuildAll();
        }
        else if (change.Property == IsLoadingProperty)
        {
            PseudoClasses.Set(LoadingPseudoClass, IsLoading);
            foreach (var card in _cards)
            {
                card.SetLoading(IsLoading);
            }
            if (!IsLoading)
            {
                // The starters arrive: their content fades in, staggered, inside the frames the placeholders held.
                ShowPage(animate: true, fromEmpty: true);
            }
            UpdateShuffle();
        }
        else if (change.Property == TextProperty)
        {
            if (_input is not null && _input.Text != Text)
            {
                _input.Text = Text;
            }
            if (string.IsNullOrEmpty(Text))
            {
                _picked = null;
            }
            if (!_settingText && SendStatus is StarterPromptsSendStatus.Sent or StarterPromptsSendStatus.Stopped or StarterPromptsSendStatus.Failed
                && !string.IsNullOrEmpty(Text))
            {
                SetStatus(StarterPromptsSendStatus.Idle, null);
            }
            UpdateComposer();
        }
        else if (change.Property == LabelsProperty)
        {
            RebuildAll();
            UpdateComposer();
            UpdateStatusView();
        }
        else if (change.Property == DescriptionProperty)
        {
            PseudoClasses.Set(NoDescriptionPseudoClass, string.IsNullOrEmpty(Description));
        }
        else if (change.Property == ModelNameProperty || change.Property == ModelMarkProperty)
        {
            UpdateComposer();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the window aborts a send in flight (Arc: the signal aborts on unmount).
        if (SendStatus == StarterPromptsSendStatus.Sending)
        {
            _sendVersion++;
            _sending?.Cancel();
            SetStatus(StarterPromptsSendStatus.Stopped, null);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SendStatus == StarterPromptsSendStatus.Sending)
        {
            StopSending();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>The surface's width steps the layout down: Shuffle as an icon, then smaller padding and radius.</summary>
    internal void OnSurfaceWidth(double width, double compactWidth, double narrowWidth)
    {
        PseudoClasses.Set(CompactPseudoClass, width < compactWidth);
        PseudoClasses.Set(NarrowPseudoClass, width < narrowWidth);
    }

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _deckDirty = true;
        RebuildAll();
    }

    private void OnTabsTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // The tabs here only filter the cards: their panel area stays empty, so it is taken out of layout and focus.
        if (e.NameScope.Find<Control>(Tabs.ContentHostPart) is { } host)
        {
            host.IsVisible = false;
        }
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingTabs || _tabs is null)
        {
            return;
        }
        e.Handled = true;
        var index = _tabs.SelectedIndex;
        if (index >= 0 && index < _tabIds.Count && _tabIds[index] != SelectedCategory)
        {
            SetCurrentValue(SelectedCategoryProperty, _tabIds[index]);
        }
    }

    private void OnShuffleClick(object? sender, RoutedEventArgs e) => Shuffle();

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is StarterPromptCard { Target: { } prompt } && !IsLoading)
        {
            Pick(prompt);
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_input is not null && _input.Text != Text)
        {
            SetCurrentValue(TextProperty, _input.Text);
        }
    }

    private void OnInputSend(object? sender, EventArgs e) => Submit();

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (SendStatus == StarterPromptsSendStatus.Sending)
        {
            StopSending();
        }
        else
        {
            Submit();
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => Submit();

    private void EnsureDeck()
    {
        if (_deckDirty)
        {
            _deck.Reset(DisplayedPrompts, DisplayedCategories);
            _deckDirty = false;
        }
    }

    /// <summary>The selected tab, or All when the selected id names no tab on screen.</summary>
    private string CurrentTab() => _deck.HasTab(SelectedCategory) ? SelectedCategory : StarterPromptsDeck.AllId;

    private void RebuildAll()
    {
        EnsureDeck();
        RebuildTabs();
        EnsureCards();
        ShowPage(animate: false);
    }

    private void RebuildTabs()
    {
        if (_tabs is null)
        {
            return;
        }
        _syncingTabs = true;
        try
        {
            _tabIds.Clear();
            _tabs.Items.Clear();
            _tabIds.Add(StarterPromptsDeck.AllId);
            _tabs.Items.Add(new TabItem { Header = StarterPromptsTabHeader.Create(Labels.All, null, TabIconSize, TabIconGap) });
            foreach (var category in _deck.Tabs)
            {
                _tabIds.Add(category.Id);
                _tabs.Items.Add(new TabItem
                {
                    Header = StarterPromptsTabHeader.Create(category.Label, category.Icon, TabIconSize, TabIconGap),
                    [AutomationProperties.NameProperty] = category.Label,
                });
            }
            _tabs.SelectedIndex = Math.Max(0, _tabIds.IndexOf(CurrentTab()));
        }
        finally
        {
            _syncingTabs = false;
        }
    }

    private void SyncTabSelection()
    {
        if (_tabs is null)
        {
            return;
        }
        EnsureDeck();
        _syncingTabs = true;
        try
        {
            _tabs.SelectedIndex = Math.Max(0, _tabIds.IndexOf(CurrentTab()));
        }
        finally
        {
            _syncingTabs = false;
        }
    }

    private void EnsureCards()
    {
        if (_grid is null)
        {
            return;
        }
        var count = Count;
        while (_cards.Count > count)
        {
            var last = _cards[^1];
            last.Click -= OnCardClick;
            _cards.RemoveAt(_cards.Count - 1);
            _grid.Children.Remove(last);
        }
        while (_cards.Count < count)
        {
            var card = new StarterPromptCard();
            card.Click += OnCardClick;
            card.SetLoading(IsLoading);
            _cards.Add(card);
            _grid.Children.Add(card);
        }
    }

    private void ShowPage(bool animate, bool fromEmpty = false)
    {
        EnsureDeck();
        ShowPage(_deck.Page(CurrentTab(), Count), animate, fromEmpty);
    }

    /// <summary>
    /// Puts <paramref name="page"/> on the cards. Animated, each card crossfades inside its frame 35ms after the one before
    /// it (Arc's item stagger), so four cards take about 100ms to start.
    /// </summary>
    private void ShowPage(IReadOnlyList<StarterPrompt> page, bool animate, bool fromEmpty = false)
    {
        var labels = Labels;
        for (var i = 0; i < _cards.Count; i++)
        {
            var prompt = i < page.Count ? page[i] : null;
            var category = prompt is null ? null : _deck.CategoryOf(prompt);
            var delay = TimeSpan.FromTicks(ArcMotionPresets.StaggerItem.Ticks * i);
            if (fromEmpty)
            {
                _cards[i].SwapTo(null, null, labels, TimeSpan.Zero, animate: false);
            }
            _cards[i].SwapTo(prompt, category, labels, delay, animate && !IsLoading);
        }
        UpdateShuffle();
    }

    private void UpdateShuffle()
    {
        if (_shuffle is null)
        {
            return;
        }
        EnsureDeck();
        _shuffle.IsEnabled = !IsLoading && _deck.CanShuffle(CurrentTab(), Count);
        AutomationProperties.SetName(_shuffle, Labels.ShuffleName);
        if (_shuffle.Content is Panel panel && panel.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
        {
            label.Text = Labels.Shuffle;
        }
        if (_grid is not null)
        {
            AutomationProperties.SetName(_grid, IsLoading ? Labels.Loading : null);
        }
    }

    private void SetText(string text, bool resetStatus)
    {
        _settingText = true;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _settingText = false;
        }
        if (resetStatus && SendStatus != StarterPromptsSendStatus.Sending)
        {
            SetStatus(StarterPromptsSendStatus.Idle, null);
        }
    }

    /// <summary>
    /// The picked prompt fades in over 160ms (full motion). Code-driven because it plays on a pick, not on a state a
    /// ControlTheme could select; reduced motion and Off show the text at once.
    /// </summary>
    private void FadeTextIn()
    {
        if (_input is null || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        if (_textFade is null)
        {
            var input = _input;
            _textFade = new MotionValue<double>(input, 1, MotionVectors.Double);
            _textFade.Changed += v => input.Opacity = v;
        }
        _textFade.Jump(0);
        _ = _textFade.AnimateTo(1, MotionSpec.Tween(TextFadeDuration, ArcEasings.Standard));
    }

    private void SetStatus(StarterPromptsSendStatus status, string? error)
    {
        SendStatus = status;
        SendError = status == StarterPromptsSendStatus.Failed ? error : null;
        PseudoClasses.Set(SendingPseudoClass, status == StarterPromptsSendStatus.Sending);
        if (_input is not null)
        {
            _input.IsReadOnly = status == StarterPromptsSendStatus.Sending;
        }
        UpdateComposer();
        UpdateStatusView();
    }

    private void UpdateComposer()
    {
        var labels = Labels;
        if (_input is not null)
        {
            AutomationProperties.SetName(_input, labels.Composer);
        }
        if (_send is not null)
        {
            var sending = SendStatus == StarterPromptsSendStatus.Sending;
            _send.IsSending = sending;
            _send.IsEnabled = sending || !string.IsNullOrWhiteSpace(Text);
            AutomationProperties.SetName(_send, sending ? labels.StopSending : labels.Send);
        }
        if (_model is not null)
        {
            AutomationProperties.SetName(_model, string.IsNullOrEmpty(ModelName) ? null : labels.ModelName(ModelName));
        }
    }

    /// <summary>The footer status: nothing, Sending with a spinner, Sent with a check, Stopped, or the error with Retry.</summary>
    private void UpdateStatusView()
    {
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        _status.Content = SendStatus switch
        {
            StarterPromptsSendStatus.Sending => StatusLine(null, labels.Sending, ArcDesignTokenKind.TextMuted, spinner: true),
            StarterPromptsSendStatus.Sent => StatusLine(ArcIcons.Check, _sentSimulated ? labels.SentSimulated : labels.Sent, ArcDesignTokenKind.Success),
            StarterPromptsSendStatus.Stopped => StatusLine(ArcIcons.Square, labels.Stopped, ArcDesignTokenKind.TextMuted),
            StarterPromptsSendStatus.Failed => FailedLine(SendError ?? labels.Failed, labels.Retry),
            _ => null,
        };
    }

    private static Control StatusLine(Geometry? glyph, string text, ArcDesignTokenKind tone, bool spinner = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (spinner)
        {
            var ring = new ArcSpinner { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(ring, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            panel.Children.Add(ring);
        }
        else if (glyph is not null)
        {
            var icon = new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, tone);
            panel.Children.Add(icon);
        }
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        panel.Children.Add(label);
        return panel;
    }

    private Control FailedLine(string message, string retry)
    {
        var dock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var button = new Button { Content = retry, Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Margin = new Thickness(6, 0, 0, 0) };
        button.Classes.Add("starter-retry");
        button.Click += OnRetryClick;
        DockPanel.SetDock(button, Dock.Right);
        dock.Children.Add(button);
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var icon = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 14, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        line.Children.Add(icon);
        var text = new TextBlock { Text = message, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        line.Children.Add(text);
        dock.Children.Add(line);
        return dock;
    }

    private void Announce(string text)
    {
        if (_announcer is not null && text != _announcer.Announcement)
        {
            _announcer.Announce(text);
        }
    }
}

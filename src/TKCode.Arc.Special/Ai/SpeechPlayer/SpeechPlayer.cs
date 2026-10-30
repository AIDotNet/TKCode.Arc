using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Read aloud for an AI reply (Arc Speech player, design reference). The reply sits above an action row with Read aloud,
/// Copy, Good response and Bad response. Read aloud crossfades the row into a compact player of the same height: play and
/// pause, the elapsed time, a thin scrubbable progress bar, the total time, a speed button (1×, 1.5×, 2×), a voice picker
/// and Stop reading. As it plays, one rounded highlight glides from word to word along a line and cuts to the next line,
/// while the words still to come stay dimmed; a click on a word jumps there.
/// <para>
/// Give it the reply in <see cref="Text"/> and your text to speech service in <see cref="Synthesize"/>: it receives a
/// <see cref="SpeechRequest"/> (abort when its token is cancelled) and returns a <see cref="SpeechAudio"/> with the playback
/// (<see cref="ISpeechAudio"/>, whose position becomes the player's clock), the word timings and the length. Without
/// <see cref="Synthesize"/> the player is a timed simulation that plays no sound. With no text, a
/// <see cref="Simulation"/> is read once the player is in view and loops through two voices until someone interacts; with
/// neither, the action row waits with Read aloud disabled.
/// </para>
/// </summary>
[TemplatePart(AuthorPart, typeof(Panel))]
[TemplatePart(AuthorMarkPart, typeof(ArcGlyph))]
[TemplatePart(AuthorNamePart, typeof(TextBlock))]
[TemplatePart(ParagraphsPart, typeof(StackPanel))]
[TemplatePart(DockPart, typeof(ArcContentSwap))]
[TemplatePart(StatusPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(OpenPseudoClass, PlainPseudoClass, TouchPseudoClass)]
public partial class SpeechPlayer : TemplatedControl
{
    internal const string AuthorPart = "PART_Author";
    internal const string AuthorMarkPart = "PART_AuthorMark";
    internal const string AuthorNamePart = "PART_AuthorName";
    internal const string ParagraphsPart = "PART_Paragraphs";
    internal const string DockPart = "PART_Dock";
    internal const string StatusPart = "PART_Status";

    internal const string OpenPseudoClass = ":open";
    internal const string PlainPseudoClass = ":plain";
    internal const string TouchPseudoClass = ":touch";

    /// <summary>The reply to read. Blank lines start new paragraphs; each whitespace separated word is one highlight step. Null reads the <see cref="Simulation"/>, if any.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SpeechPlayer, string?>(nameof(Text));

    /// <summary>
    /// Fetches speech from your text to speech service. Return the playback, the word timings (one start and end per word,
    /// from the provider's alignment data) and the length; missing timings are estimated and fitted to the length.
    /// Throw with a short, human message to show it next to "Couldn't generate audio". Null: a simulation with no audio.
    /// </summary>
    public static readonly StyledProperty<Func<SpeechRequest, Task<SpeechAudio>>?> SynthesizeProperty =
        AvaloniaProperty.Register<SpeechPlayer, Func<SpeechRequest, Task<SpeechAudio>>?>(nameof(Synthesize));

    /// <summary>The voices in the picker. Null or empty offers the playing simulation's voices, else one default voice with an empty id (your service's default).</summary>
    public static readonly StyledProperty<IReadOnlyList<SpeechVoice>?> VoicesProperty =
        AvaloniaProperty.Register<SpeechPlayer, IReadOnlyList<SpeechVoice>?>(nameof(Voices));

    /// <summary>The selected voice's id (two-way). Null or an unknown id uses the first voice. A new voice while reading resumes from the word being spoken.</summary>
    public static readonly StyledProperty<string?> SelectedVoiceIdProperty =
        AvaloniaProperty.Register<SpeechPlayer, string?>(nameof(SelectedVoiceId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The playback speed (two-way). The speed button cycles 1×, 1.5×, 2×.</summary>
    public static readonly StyledProperty<SpeechSpeed> SpeedProperty =
        AvaloniaProperty.Register<SpeechPlayer, SpeechSpeed>(nameof(Speed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Who wrote the reply, shown above it with a 16px mark. Null shows no author line (a playing simulation shows its own).</summary>
    public static readonly StyledProperty<string?> AuthorNameProperty =
        AvaloniaProperty.Register<SpeechPlayer, string?>(nameof(AuthorName));

    /// <summary>The author's mark: a 24 × 24 stroked glyph drawn at 16px (the sparkle by default).</summary>
    public static readonly StyledProperty<Geometry?> AuthorMarkProperty =
        AvaloniaProperty.Register<SpeechPlayer, Geometry?>(nameof(AuthorMark), ArcIcons.Sparkles);

    /// <summary>The author mark's color; the accent by default.</summary>
    public static readonly StyledProperty<IBrush?> AuthorMarkBrushProperty =
        AvaloniaProperty.Register<SpeechPlayer, IBrush?>(nameof(AuthorMarkBrush));

    /// <summary>Buttons after Read aloud in the action row, in place of the default Copy, Good response and Bad response.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<SpeechPlayer, object?>(nameof(Actions));

    /// <summary>Shows Copy, Good response and Bad response after Read aloud when <see cref="Actions"/> is null (feedback stays local and is announced as simulated). False shows Read aloud alone.</summary>
    public static readonly StyledProperty<bool> ShowsDefaultActionsProperty =
        AvaloniaProperty.Register<SpeechPlayer, bool>(nameof(ShowsDefaultActions), true);

    /// <summary>Starts reading once the player is in view. Null: on while a <see cref="Simulation"/> plays, off when <see cref="Text"/> is set.</summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<SpeechPlayer, bool?>(nameof(AutoPlay));

    /// <summary>
    /// A self-playing reading to show while <see cref="Text"/> is null: its reply, author and voices, read once the player
    /// is in view and looped through two voices until someone interacts. Null (the default) leaves the player empty. Give
    /// each player its own simulation.
    /// </summary>
    public static readonly StyledProperty<SpeechSimulation?> SimulationProperty =
        AvaloniaProperty.Register<SpeechPlayer, SpeechSimulation?>(nameof(Simulation));

    /// <summary>Notes in the voice menu and the announcements that no audio plays. Null: on when <see cref="Synthesize"/> is not set.</summary>
    public static readonly StyledProperty<bool?> IsSimulatedProperty =
        AvaloniaProperty.Register<SpeechPlayer, bool?>(nameof(IsSimulated));

    /// <summary>A bordered card, or plain to sit inside a chat column.</summary>
    public static readonly StyledProperty<SpeechPlayerVariant> VariantProperty =
        AvaloniaProperty.Register<SpeechPlayer, SpeechPlayerVariant>(nameof(Variant));

    /// <summary>Every word the player shows or says.</summary>
    public static readonly StyledProperty<SpeechPlayerLabels> LabelsProperty =
        AvaloniaProperty.Register<SpeechPlayer, SpeechPlayerLabels>(nameof(Labels), SpeechPlayerLabels.Default);

    /// <summary>Where the player is: the action row, preparing, playing, paused, finished or failed.</summary>
    public static readonly DirectProperty<SpeechPlayer, SpeechPlayerState> StateProperty =
        AvaloniaProperty.RegisterDirect<SpeechPlayer, SpeechPlayerState>(nameof(State), o => o.State);

    /// <summary>Whether the player shows in place of the action row.</summary>
    public static readonly DirectProperty<SpeechPlayer, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<SpeechPlayer, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>The index of the word being spoken (−1 before the first).</summary>
    public static readonly DirectProperty<SpeechPlayer, int> CurrentWordIndexProperty =
        AvaloniaProperty.RegisterDirect<SpeechPlayer, int>(nameof(CurrentWordIndex), o => o.CurrentWordIndex);

    /// <summary>The last sentence said through the polite status region.</summary>
    public static readonly DirectProperty<SpeechPlayer, string?> AnnouncementProperty =
        AvaloniaProperty.RegisterDirect<SpeechPlayer, string?>(nameof(Announcement), o => o.Announcement);

    /// <summary>Playback started or resumed. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechPlaybackEventArgs> PlayedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechPlaybackEventArgs>(nameof(Played), RoutingStrategies.Bubble);

    /// <summary>Playback paused. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechPlaybackEventArgs> PausedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechPlaybackEventArgs>(nameof(Paused), RoutingStrategies.Bubble);

    /// <summary>The last word finished. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechPlaybackEventArgs> EndedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechPlaybackEventArgs>(nameof(Ended), RoutingStrategies.Bubble);

    /// <summary>A scrub, an arrow key or a click on a word moved playback. Raised once when a drag ends, not on every move. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechPlaybackEventArgs> SeekedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechPlaybackEventArgs>(nameof(Seeked), RoutingStrategies.Bubble);

    /// <summary>The spoken word changed; use it to sync a caption or analytics. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechWordEventArgs> WordChangedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechWordEventArgs>(nameof(WordChanged), RoutingStrategies.Bubble);

    /// <summary>A voice was picked. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechVoiceChangedEventArgs> VoiceChangedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechVoiceChangedEventArgs>(nameof(VoiceChanged), RoutingStrategies.Bubble);

    /// <summary>The speed changed. Bubbles.</summary>
    public static readonly RoutedEvent<SpeechSpeedChangedEventArgs> SpeedChangedEvent =
        RoutedEvent.Register<SpeechPlayer, SpeechSpeedChangedEventArgs>(nameof(SpeedChanged), RoutingStrategies.Bubble);

    /// <summary>Stop reading was pressed and the action row returned. Fetched speech is kept, so Read aloud starts again at once. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<SpeechPlayer, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    private SpeechPlayerState _state;
    private bool _isOpen;
    private int _currentWordIndex = -1;
    private string? _announcement;

    private Panel? _author;
    private ArcGlyph? _authorMark;
    private TextBlock? _authorName;
    private StackPanel? _paragraphsPanel;
    private ArcContentSwap? _dock;
    private ExplainerAnnouncer? _status;
    private readonly SpeechPlayerActionRow _actionRow = new();
    private readonly SpeechPlayerControls _controls = new();
    private readonly List<SpeechPlayerParagraph> _paragraphs = [];
    private readonly MenuSurface _voiceMenu = new();
    private SpeechPlayerText _document = SpeechPlayerText.Empty;
    private SpeechVoice? _defaultVoice;
    private bool _narrow;
    private bool _touch;
    private int? _feedback;

    /// <summary>Creates the player.</summary>
    public SpeechPlayer()
    {
        _actionRow.PartsReady += OnActionRowReady;
        _controls.PartsReady += OnControlsReady;
        AutomationProperties.SetName(_controls, Labels.Player);
        _voiceMenu.ItemClicked += OnVoiceItemClicked;
        _voiceMenu.Opened += OnVoiceMenuOpened;
        _voiceMenu.Side = MenuSide.Top;
        _voiceMenu.Alignment = MenuAlignment.End;
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _document = SpeechPlayerText.Parse(string.Empty);
        InitPlayback();
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SynthesizeProperty"/>
    public Func<SpeechRequest, Task<SpeechAudio>>? Synthesize { get => GetValue(SynthesizeProperty); set => SetValue(SynthesizeProperty, value); }

    /// <inheritdoc cref="VoicesProperty"/>
    public IReadOnlyList<SpeechVoice>? Voices { get => GetValue(VoicesProperty); set => SetValue(VoicesProperty, value); }

    /// <inheritdoc cref="SelectedVoiceIdProperty"/>
    public string? SelectedVoiceId { get => GetValue(SelectedVoiceIdProperty); set => SetValue(SelectedVoiceIdProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public SpeechSpeed Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="AuthorNameProperty"/>
    public string? AuthorName { get => GetValue(AuthorNameProperty); set => SetValue(AuthorNameProperty, value); }

    /// <inheritdoc cref="AuthorMarkProperty"/>
    public Geometry? AuthorMark { get => GetValue(AuthorMarkProperty); set => SetValue(AuthorMarkProperty, value); }

    /// <inheritdoc cref="AuthorMarkBrushProperty"/>
    public IBrush? AuthorMarkBrush { get => GetValue(AuthorMarkBrushProperty); set => SetValue(AuthorMarkBrushProperty, value); }

    /// <inheritdoc cref="ActionsProperty"/>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <inheritdoc cref="ShowsDefaultActionsProperty"/>
    public bool ShowsDefaultActions { get => GetValue(ShowsDefaultActionsProperty); set => SetValue(ShowsDefaultActionsProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public SpeechSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool? IsSimulated { get => GetValue(IsSimulatedProperty); set => SetValue(IsSimulatedProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public SpeechPlayerVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SpeechPlayerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public SpeechPlayerState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => _isOpen; private set => SetAndRaise(IsOpenProperty, ref _isOpen, value); }

    /// <inheritdoc cref="CurrentWordIndexProperty"/>
    public int CurrentWordIndex { get => _currentWordIndex; private set => SetAndRaise(CurrentWordIndexProperty, ref _currentWordIndex, value); }

    /// <inheritdoc cref="AnnouncementProperty"/>
    public string? Announcement { get => _announcement; private set => SetAndRaise(AnnouncementProperty, ref _announcement, value); }

    /// <summary>The playback position on the 1× timeline.</summary>
    public TimeSpan Position => TimeSpan.FromSeconds(_time);

    /// <summary>The length of the speech on the 1× timeline (zero until it is prepared).</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_duration);

    /// <summary>The voice that reads: the selected one, or the first (a default voice with an empty id when there are none).</summary>
    public SpeechVoice EffectiveVoice
    {
        get
        {
            var voices = EffectiveVoices;
            var id = SelectedVoiceId;
            foreach (var voice in voices)
            {
                if (voice.Id == id)
                {
                    return voice;
                }
            }
            return voices.Count > 0 ? voices[0] : DefaultVoice;
        }
    }

    /// <inheritdoc cref="PlayedEvent"/>
    public event EventHandler<SpeechPlaybackEventArgs>? Played { add => AddHandler(PlayedEvent, value); remove => RemoveHandler(PlayedEvent, value); }

    /// <inheritdoc cref="PausedEvent"/>
    public event EventHandler<SpeechPlaybackEventArgs>? Paused { add => AddHandler(PausedEvent, value); remove => RemoveHandler(PausedEvent, value); }

    /// <inheritdoc cref="EndedEvent"/>
    public event EventHandler<SpeechPlaybackEventArgs>? Ended { add => AddHandler(EndedEvent, value); remove => RemoveHandler(EndedEvent, value); }

    /// <inheritdoc cref="SeekedEvent"/>
    public event EventHandler<SpeechPlaybackEventArgs>? Seeked { add => AddHandler(SeekedEvent, value); remove => RemoveHandler(SeekedEvent, value); }

    /// <inheritdoc cref="WordChangedEvent"/>
    public event EventHandler<SpeechWordEventArgs>? WordChanged { add => AddHandler(WordChangedEvent, value); remove => RemoveHandler(WordChangedEvent, value); }

    /// <inheritdoc cref="VoiceChangedEvent"/>
    public event EventHandler<SpeechVoiceChangedEventArgs>? VoiceChanged { add => AddHandler(VoiceChangedEvent, value); remove => RemoveHandler(VoiceChangedEvent, value); }

    /// <inheritdoc cref="SpeedChangedEvent"/>
    public event EventHandler<SpeechSpeedChangedEventArgs>? SpeedChanged { add => AddHandler(SpeedChangedEvent, value); remove => RemoveHandler(SpeedChangedEvent, value); }

    /// <inheritdoc cref="ClosedEvent"/>
    public event EventHandler<RoutedEventArgs>? Closed { add => AddHandler(ClosedEvent, value); remove => RemoveHandler(ClosedEvent, value); }

    /// <summary>Whether the simulation is read (no text, and a simulation).</summary>
    internal bool PlaysSimulation => Text is null && Simulation is not null;

    /// <summary>The reply on screen: the text, the playing simulation's, or none.</summary>
    internal string EffectiveText => Text ?? Simulation?.Text ?? string.Empty;

    internal IReadOnlyList<SpeechVoice> EffectiveVoices => Voices is { Count: > 0 } voices ? voices
        : PlaysSimulation && Simulation!.Voices is { Count: > 0 } simulated ? simulated
        : [];

    /// <summary>The voice offered when there are no voices: your service's default, named by <see cref="SpeechPlayerLabels.DefaultVoice"/>.</summary>
    private SpeechVoice DefaultVoice => _defaultVoice ??= new SpeechVoice(string.Empty, Labels.DefaultVoice);

    internal bool EffectiveSimulated => IsSimulated ?? Synthesize is null;

    internal bool EffectiveAutoPlay => AutoPlay ?? PlaysSimulation;

    internal string? EffectiveAuthor => AuthorName ?? (PlaysSimulation ? Simulation!.AuthorName : null);

    internal SpeechPlayerText Document => _document;

    internal IReadOnlyList<SpeechPlayerParagraph> ParagraphViews => _paragraphs;

    internal SpeechPlayerActionRow ActionRow => _actionRow;

    internal SpeechPlayerControls Controls => _controls;

    internal MenuSurface VoiceMenu => _voiceMenu;

    internal ExplainerAnnouncer? StatusText => _status;

    /// <summary>Which default feedback is chosen: 1 good, −1 bad, null none.</summary>
    internal int? Feedback => _feedback;

    internal bool IsNarrow => _narrow;

    internal static double RateOf(SpeechSpeed speed) => speed switch
    {
        SpeechSpeed.OneAndHalf => 1.5,
        SpeechSpeed.Twice => 2,
        _ => 1
    };

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_dock is not null)
        {
            _dock.Content = null;
        }
        _author = e.NameScope.Find<Panel>(AuthorPart);
        _authorMark = e.NameScope.Find<ArcGlyph>(AuthorMarkPart);
        _authorName = e.NameScope.Find<TextBlock>(AuthorNamePart);
        _paragraphsPanel = e.NameScope.Find<StackPanel>(ParagraphsPart);
        _dock = e.NameScope.Find<ArcContentSwap>(DockPart);
        _status = e.NameScope.Find<ExplainerAnnouncer>(StatusPart);
        _status?.Announce(Announcement);
        PseudoClasses.Set(PlainPseudoClass, Variant == SpeechPlayerVariant.Plain);
        PseudoClasses.Set(OpenPseudoClass, IsOpen);
        BuildParagraphs();
        if (_dock is not null)
        {
            _dock.Content = IsOpen ? _controls : _actionRow;
        }
        SyncAuthor();
        SyncActionRow();
        SyncControls();
        SyncWords(animate: false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == SimulationProperty)
        {
            if (change.Property == SimulationProperty && Text is not null)
            {
                // The app's own text wins: a new simulation changes nothing on screen.
                return;
            }
            _document = SpeechPlayerText.Parse(EffectiveText);
            ResetForNewText();
            BuildParagraphs();
            SyncAuthor();
            SyncActionRow();
            BuildVoiceChoices();
            SyncControls();
            TryAutoPlay();
        }
        else if (change.Property == SynthesizeProperty)
        {
            ResetForNewText();
        }
        else if (change.Property == VoicesProperty)
        {
            BuildVoiceChoices();
            SyncControls();
        }
        else if (change.Property == SelectedVoiceIdProperty)
        {
            OnVoiceChanged(change.GetOldValue<string?>());
        }
        else if (change.Property == SpeedProperty)
        {
            OnSpeedChanged();
        }
        else if (change.Property == AuthorNameProperty || change.Property == AuthorMarkProperty || change.Property == AuthorMarkBrushProperty)
        {
            SyncAuthor();
        }
        else if (change.Property == ActionsProperty || change.Property == ShowsDefaultActionsProperty)
        {
            SyncActionRow();
        }
        else if (change.Property == IsSimulatedProperty)
        {
            BuildVoiceChoices();
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(PlainPseudoClass, Variant == SpeechPlayerVariant.Plain);
        }
        else if (change.Property == LabelsProperty)
        {
            _defaultVoice = null;
            AutomationProperties.SetName(_controls, Labels.Player);
            BuildSpeedChoices();
            BuildVoiceChoices();
            SyncActionRow();
            SyncControls();
        }
        else if (change.Property == AutoPlayProperty)
        {
            TryAutoPlay();
        }
    }

    /// <summary>The card fills its container up to its maximum width, so its width never follows the text or the dock.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (double.IsFinite(availableSize.Width))
        {
            var width = Math.Min(availableSize.Width, double.IsFinite(MaxWidth) ? MaxWidth : availableSize.Width);
            return new Size(Math.Max(desired.Width, width), desired.Height);
        }
        return desired;
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < _controls.NarrowWidth;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            SyncControls();
        }
    }

    private void BuildParagraphs()
    {
        if (_paragraphsPanel is null)
        {
            return;
        }
        _paragraphsPanel.Children.Clear();
        _paragraphs.Clear();
        foreach (var paragraph in _document.Paragraphs)
        {
            var view = new SpeechPlayerParagraph { Source = paragraph, TextWrapping = TextWrapping.Wrap, WordPressed = OnWordPressed };
            _paragraphs.Add(view);
            _paragraphsPanel.Children.Add(view);
        }
        SyncWords(animate: false);
    }

    private void SyncAuthor()
    {
        var name = EffectiveAuthor;
        if (_author is not null)
        {
            _author.IsVisible = !string.IsNullOrEmpty(name);
        }
        if (_authorName is not null)
        {
            _authorName.Text = name;
        }
        if (_authorMark is not null)
        {
            _authorMark.Data = AuthorMark;
            _authorMark.IsVisible = AuthorMark is not null;
            if (AuthorMarkBrush is { } brush)
            {
                _authorMark.Foreground = brush;
            }
            else
            {
                _authorMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
    }

    private void OnActionRowReady()
    {
        if (_actionRow.ReadAloud is { } read)
        {
            read.Click -= OnReadAloud;
            read.Click += OnReadAloud;
        }
        if (_actionRow.Good is { } good)
        {
            good.Click -= OnGood;
            good.Click += OnGood;
        }
        if (_actionRow.Bad is { } bad)
        {
            bad.Click -= OnBad;
            bad.Click += OnBad;
        }
        SyncActionRow();
    }

    private void SyncActionRow()
    {
        var labels = Labels;
        if (_actionRow.ReadAloud is { } read)
        {
            read.Content = labels.ReadAloud;
            // Nothing to read: the empty player waits with Read aloud disabled.
            read.IsEnabled = _document.Words.Count > 0;
            AutomationProperties.SetName(read, labels.ReadAloud);
            // Read aloud is described by the reply it reads.
            AutomationProperties.SetHelpText(read, _document.Spoken);
        }
        if (_actionRow.Actions is { } actions)
        {
            actions.Content = Actions;
            actions.IsVisible = Actions is not null;
        }
        if (_actionRow.Defaults is { } defaults)
        {
            defaults.IsVisible = Actions is null && ShowsDefaultActions;
        }
        if (_actionRow.Copy is { } copy)
        {
            copy.Value = _document.Spoken;
            AutomationProperties.SetName(copy, labels.Copy);
            Tooltip.SetTip(copy, labels.Copy);
        }
        if (_actionRow.Good is { } good)
        {
            good.IsActive = _feedback == 1;
            AutomationProperties.SetName(good, labels.GoodResponse);
            Tooltip.SetTip(good, labels.GoodResponse);
        }
        if (_actionRow.Bad is { } bad)
        {
            bad.IsActive = _feedback == -1;
            AutomationProperties.SetName(bad, labels.BadResponse);
            Tooltip.SetTip(bad, labels.BadResponse);
        }
    }

    private void OnGood(object? sender, RoutedEventArgs e) => SetFeedback(1);

    private void OnBad(object? sender, RoutedEventArgs e) => SetFeedback(-1);

    /// <summary>The default feedback buttons are local: pressing one marks it (again clears it) and says so, as simulated.</summary>
    private void SetFeedback(int value)
    {
        _feedback = _feedback == value ? null : value;
        Announce(_feedback is null ? Labels.FeedbackCleared : Labels.FeedbackGiven(value > 0));
        SyncActionRow();
    }

    private void OnControlsReady()
    {
        var c = _controls;
        if (c.Play is { } play)
        {
            play.Click -= OnPlayClick;
            play.Click += OnPlayClick;
        }
        if (c.Speed is { } speed)
        {
            speed.Click -= OnSpeedClick;
            speed.Click += OnSpeedClick;
        }
        if (c.Voice is { } voice)
        {
            DropdownMenu.SetMenu(voice, _voiceMenu);
        }
        if (c.Stop is { } stop)
        {
            stop.Click -= OnStopClick;
            stop.Click += OnStopClick;
        }
        if (c.Progress is { } progress)
        {
            progress.SeekBy = seconds => SeekCore(_time + seconds, dragging: false);
            progress.SeekTo = (fraction, dragging) => SeekCore(fraction * _duration, dragging);
            progress.Toggle = TogglePlayback;
        }
        BuildSpeedChoices();
        BuildVoiceChoices();
        SyncControls();
    }

    /// <summary>One hidden copy of every speed in one cell, so the button is as wide as "1.5×" and never resizes.</summary>
    private void BuildSpeedChoices()
    {
        if (_controls.SpeedLabels is not { } panel)
        {
            return;
        }
        panel.Children.Clear();
        foreach (var speed in Enum.GetValues<SpeechSpeed>())
        {
            panel.Children.Add(new SpeechPlayerChoice { Text = Labels.SpeedText(speed), Tag = speed, IsShown = speed == Speed });
        }
    }

    /// <summary>One hidden copy of every voice name, and the voice menu's rows.</summary>
    private void BuildVoiceChoices()
    {
        var voices = EffectiveVoices;
        var selected = EffectiveVoice;
        if (_controls.VoiceLabels is { } panel)
        {
            panel.Children.Clear();
            foreach (var voice in voices)
            {
                panel.Children.Add(new SpeechPlayerChoice { Text = voice.Name, Tag = voice, IsShown = ReferenceEquals(voice, selected) });
            }
        }
        _voiceMenu.Items.Clear();
        foreach (var voice in voices)
        {
            var name = new TextBlock { Text = voice.Name };
            var header = new StackPanel { Spacing = 1 };
            header.Children.Add(name);
            if (!string.IsNullOrEmpty(voice.Description))
            {
                var description = new TextBlock { Text = voice.Description, FontSize = 12, TextWrapping = TextWrapping.NoWrap };
                ArcTokens.Bind(description, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
                header.Children.Add(description);
            }
            var entry = new MenuEntry
            {
                Header = header,
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "speech-player-voice",
                IsChecked = ReferenceEquals(voice, selected),
                Tag = voice
            };
            TextSearch.SetText(entry, voice.Name);
            AutomationProperties.SetName(entry, string.IsNullOrEmpty(voice.Description) ? voice.Name : voice.Name + ", " + voice.Description);
            _voiceMenu.Items.Add(entry);
        }
        if (EffectiveSimulated)
        {
            _voiceMenu.Items.Add(new MenuEntrySeparator());
            _voiceMenu.Items.Add(new MenuEntryLabel { Text = Labels.SimulatedNote });
        }
        AutomationProperties.SetName(_voiceMenu, Labels.VoiceButton(selected.Name));
    }

    private void OnVoiceItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is SpeechVoice voice)
        {
            _interacted = true;
            SetCurrentValue(SelectedVoiceIdProperty, voice.Id);
        }
    }

    /// <summary>The voice menu opens with the keyboard position on the selected voice.</summary>
    private void OnVoiceMenuOpened(object? sender, RoutedEventArgs e)
    {
        var selected = EffectiveVoice;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var item in _voiceMenu.Items)
            {
                if (item is MenuEntry { Tag: SpeechVoice voice } entry && ReferenceEquals(voice, selected))
                {
                    entry.Focus(NavigationMethod.Directional);
                    break;
                }
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Brings the player's parts in line with the state; parts that already show a value are left alone.</summary>
    private void SyncControls()
    {
        var c = _controls;
        var labels = Labels;
        var state = State;
        c.SetFlags(state == SpeechPlayerState.Preparing, state == SpeechPlayerState.Failed, _narrow, _touch);
        _actionRow.SetTouch(_touch);
        if (c.Play is { } play)
        {
            play.Show(state);
            var name = state switch
            {
                SpeechPlayerState.Playing => labels.Pause,
                SpeechPlayerState.Finished => labels.PlayAgain,
                SpeechPlayerState.Failed => labels.Retry,
                SpeechPlayerState.Preparing => labels.PreparingAudio,
                _ => labels.Play
            };
            AutomationProperties.SetName(play, name);
            Tooltip.SetTip(play, name);
        }
        if (c.Preparing is { } preparing)
        {
            var text = labels.PreparingVoice(EffectiveVoice.Name);
            if (preparing.Text != text)
            {
                preparing.Text = text;
            }
            preparing.IsShimmering = state == SpeechPlayerState.Preparing;
        }
        if (c.Error is { } error)
        {
            var text = string.IsNullOrEmpty(_error) ? labels.Failed : labels.Failed + ". " + _error;
            if (error.Text != text)
            {
                error.Text = text;
            }
        }
        if (c.Speed is { } speedButton)
        {
            AutomationProperties.SetName(speedButton, labels.SpeedButton(labels.SpeedText(Speed)));
            Tooltip.SetTip(speedButton, labels.SpeedButton(labels.SpeedText(Speed)));
        }
        if (c.SpeedLabels is { } speeds)
        {
            foreach (var child in speeds.Children)
            {
                if (child is SpeechPlayerChoice { Tag: SpeechSpeed s } choice)
                {
                    choice.IsShown = s == Speed;
                }
            }
        }
        var voice = EffectiveVoice;
        if (c.Voice is { } voiceButton)
        {
            AutomationProperties.SetName(voiceButton, labels.VoiceButton(voice.Name));
        }
        if (c.VoiceLabels is { } names)
        {
            foreach (var child in names.Children)
            {
                if (child is SpeechPlayerChoice { Tag: SpeechVoice v } choice)
                {
                    choice.IsShown = ReferenceEquals(v, voice);
                }
            }
        }
        foreach (var item in _voiceMenu.Items)
        {
            if (item is MenuEntry { Tag: SpeechVoice v } entry)
            {
                entry.IsChecked = ReferenceEquals(v, voice);
            }
        }
        if (c.Stop is { } stop)
        {
            AutomationProperties.SetName(stop, labels.StopReading);
            Tooltip.SetTip(stop, labels.StopReading);
        }
        if (c.Progress is { } progress)
        {
            AutomationProperties.SetName(progress, labels.Progress);
        }
        SyncTimes(force: true);
    }

    /// <summary>The times and the bar: the bar on every call, the texts only when the whole second changes.</summary>
    private void SyncTimes(bool force)
    {
        var c = _controls;
        if (c.Progress is { } progress)
        {
            progress.Fraction = _duration > 0 ? Math.Clamp(_time / _duration, 0, 1) : 0;
            progress.Seconds = _time;
            progress.TotalSeconds = _duration;
        }
        var whole = (long)Math.Floor(_time + 1e-6);
        if (!force && whole == _shownSecond)
        {
            return;
        }
        _shownSecond = whole;
        var elapsed = SpeechPlayerText.Clock(_time);
        var total = SpeechPlayerText.Clock(_duration);
        if (c.Elapsed is { } e && e.Text != elapsed)
        {
            e.Text = elapsed;
        }
        if (c.Total is { } t && t.Text != total)
        {
            t.Text = total;
        }
        if (c.Progress is { } bar)
        {
            // Updated once a second, not every frame.
            AutomationProperties.SetHelpText(bar, Labels.PositionOf(elapsed, total));
        }
    }

    /// <summary>Says a sentence through the polite status region.</summary>
    private void Announce(string text)
    {
        Announcement = text;
        if (_status is not null && text != _status.Announcement)
        {
            _status.Announce(text);
        }
    }

    /// <summary>The dock's height grows to 36px once a touch has been seen.</summary>
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var touch = e.Pointer.Type == PointerType.Touch;
        if (touch != _touch)
        {
            _touch = touch;
            PseudoClasses.Set(TouchPseudoClass, touch);
            SyncControls();
        }
        _interacted = true;
        StopSimulationLoop();
    }

    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        _interacted = true;
        StopSimulationLoop();
    }
}

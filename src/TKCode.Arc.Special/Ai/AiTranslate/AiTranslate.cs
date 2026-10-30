using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An inline translator for one message or paragraph (Arc AI translate, design reference). A byline with the author and a
/// detected-language chip sits above the text; below it a Translate picker, a Show original toggle and a status line led by
/// the model's mark. Picking a language dims the text on screen and streams the translation in place in fast chunks, each
/// new sentence retiring one sentence of the old text, while the paragraph's height springs to fit. Finished translations
/// are cached per text, so Show original and switching back crossfade at once. A failure puts the previous text back and
/// shows Couldn't translate with Retry.
/// <para>
/// Wire <see cref="Translator"/> to your model: it gets an <see cref="AiTranslateRequest"/> and returns the translation as
/// an async stream of text deltas (yield the whole text once to have the card stream it in chunks itself). Without one the
/// card simulates the stream from <see cref="Translations"/>. Without <see cref="Text"/> it plays a <see cref="Simulation"/>
/// (a message and its translations you pass, toured by itself); with neither it shows an empty card whose picker is off.
/// </para>
/// </summary>
[TemplatePart(TextPart, typeof(AiTranslateParagraph))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[TemplatePart(ChipPart, typeof(Badge))]
[TemplatePart(PickerPart, typeof(AvaloniaButton))]
[TemplatePart(PickerLabelPart, typeof(AiTranslateSlot))]
[TemplatePart(TogglePart, typeof(AvaloniaButton))]
[TemplatePart(ToggleLabelPart, typeof(AiTranslateSlot))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(RetryLabelPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(Control))]
[TemplatePart(StatusLabelPart, typeof(AiTranslateSlot))]
[PseudoClasses(TranslatingPseudoClass, TranslatedPseudoClass, FailedPseudoClass, NoAuthorPseudoClass, NarrowPseudoClass, CompactPseudoClass)]
public class AiTranslate : TemplatedControl
{
    internal const string TextPart = "PART_Text";
    internal const string AvatarPart = "PART_Avatar";
    internal const string NamePart = "PART_Name";
    internal const string MetaPart = "PART_Meta";
    internal const string ChipPart = "PART_Chip";
    internal const string PickerPart = "PART_Picker";
    internal const string PickerLabelPart = "PART_PickerLabel";
    internal const string TogglePart = "PART_Toggle";
    internal const string ToggleLabelPart = "PART_ToggleLabel";
    internal const string RetryPart = "PART_Retry";
    internal const string RetryLabelPart = "PART_RetryLabel";
    internal const string StatusPart = "PART_Status";
    internal const string StatusLabelPart = "PART_StatusLabel";

    internal const string TranslatingPseudoClass = ":translating";
    internal const string TranslatedPseudoClass = ":translated";
    internal const string FailedPseudoClass = ":failed";
    internal const string NoAuthorPseudoClass = ":no-author";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Below this card width the status line drops under the controls.</summary>
    internal const double NarrowWidth = 470;

    /// <summary>Below this card width the chip shows just the language name.</summary>
    internal const double CompactWidth = 380;

    /// <summary>A streamed chunk lands every 34ms.</summary>
    internal const double ChunkInterval = 0.034;

    /// <summary>The simulated model's wait before the first chunk.</summary>
    internal const double SimulatedLatency = 0.32;

    /// <summary>Autoplay: the wait before the tour starts and between its steps.</summary>
    internal const double AutoPlayStart = 1.2;
    internal const double AutoPlayDwell = 2.6;

    /// <summary>
    /// The text to translate, in its original language. Changing it clears the cached translations and shows the new
    /// original. Null plays <see cref="Simulation"/>, or shows the empty card without one.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiTranslate, string?>(nameof(Text));

    /// <summary>The language of <see cref="Text"/>. Null detects it from the text with a light check and the chip reads Detected; set, the chip reads Written in.</summary>
    public static readonly StyledProperty<AiTranslateLanguage?> SourceLanguageProperty =
        AvaloniaProperty.Register<AiTranslate, AiTranslateLanguage?>(nameof(SourceLanguage));

    /// <summary>The languages offered in the picker, in order; null offers all six. The source language is always listed, as Original.</summary>
    public static readonly StyledProperty<IReadOnlyList<AiTranslateLanguage>?> LanguagesProperty =
        AvaloniaProperty.Register<AiTranslate, IReadOnlyList<AiTranslateLanguage>?>(nameof(Languages));

    /// <summary>
    /// The language on screen (two-way); null or the source language means the original. Set it to translate, for example
    /// to a reader's preferred language: a language other than the source translates as soon as the card is shown.
    /// </summary>
    public static readonly StyledProperty<AiTranslateLanguage?> ValueProperty =
        AvaloniaProperty.Register<AiTranslate, AiTranslateLanguage?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Your translation call: returns the translation as an async stream of text deltas. Pass the request's cancellation
    /// token on to your network call; it is cancelled when the reader picks another language, shows the original or the
    /// card leaves the window. A thrown exception's message is announced and kept as the status tooltip. Null simulates the
    /// stream.
    /// </summary>
    public static readonly StyledProperty<Func<AiTranslateRequest, IAsyncEnumerable<string>>?> TranslatorProperty =
        AvaloniaProperty.Register<AiTranslate, Func<AiTranslateRequest, IAsyncEnumerable<string>>?>(nameof(Translator));

    /// <summary>Ready translations by language, streamed by the simulation (content you translated ahead of time). A missing language shows the failure.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<AiTranslateLanguage, string>?> TranslationsProperty =
        AvaloniaProperty.Register<AiTranslate, IReadOnlyDictionary<AiTranslateLanguage, string>?>(nameof(Translations));

    /// <summary>Who wrote the text, shown with an avatar. Null hides the byline (the language chip stays), or shows the <see cref="Simulation"/>'s author while one plays.</summary>
    public static readonly StyledProperty<AiTranslateAuthor?> AuthorProperty =
        AvaloniaProperty.Register<AiTranslate, AiTranslateAuthor?>(nameof(Author));

    /// <summary>Shown after the role in the byline, for example "Oct 27, 09:14".</summary>
    public static readonly StyledProperty<string?> TimestampProperty =
        AvaloniaProperty.Register<AiTranslate, string?>(nameof(Timestamp));

    /// <summary>The model credited in the status line: its name is the idle label. Null credits the <see cref="Simulation"/>'s model while one plays, or none.</summary>
    public static readonly StyledProperty<string?> ModelNameProperty =
        AvaloniaProperty.Register<AiTranslate, string?>(nameof(ModelName));

    /// <summary>The model's mark (a 24 × 24 stroked glyph) that leads every status; a sparkle by default. Pass your provider's mark.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<AiTranslate, Geometry?>(nameof(ModelMark), ArcIcons.Sparkles);

    /// <summary>
    /// Tours the languages (the <see cref="Simulation"/>'s <see cref="AiTranslateSimulation.Tour"/>, or every other language
    /// in the picker), then the original, and loops. It pauses off screen and stops for good on the first pointer or key
    /// press inside the card; with reduced motion it translates once and stops. Null tours only while a
    /// <see cref="Simulation"/> plays.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<AiTranslate, bool?>(nameof(AutoPlay));

    /// <summary>
    /// A message to play while the card has no <see cref="Text"/>: its text, byline, model and ready translations, streamed
    /// as a simulation (nothing is sent). Your own <see cref="Text"/> wins over it; null stops it and shows the empty card.
    /// </summary>
    public static readonly StyledProperty<AiTranslateSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AiTranslate, AiTranslateSimulation?>(nameof(Simulation));

    /// <summary>Adds "Simulated, nothing was sent" to the announcements. Null: true without a <see cref="Translator"/> or while a <see cref="Simulation"/> plays.</summary>
    public static readonly StyledProperty<bool?> IsSimulatedProperty =
        AvaloniaProperty.Register<AiTranslate, bool?>(nameof(IsSimulated));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<AiTranslateLabels> LabelsProperty =
        AvaloniaProperty.Register<AiTranslate, AiTranslateLabels>(nameof(Labels), AiTranslateLabels.Default);

    /// <summary>What the card shows: the original, a translation streaming in, a translation, or a failure.</summary>
    public static readonly DirectProperty<AiTranslate, AiTranslateState> StateProperty =
        AvaloniaProperty.RegisterDirect<AiTranslate, AiTranslateState>(nameof(State), o => o.State);

    /// <summary>The language of the text: <see cref="SourceLanguage"/>, or the one detected from the text.</summary>
    public static readonly DirectProperty<AiTranslate, AiTranslateLanguage> EffectiveSourceLanguageProperty =
        AvaloniaProperty.RegisterDirect<AiTranslate, AiTranslateLanguage>(nameof(EffectiveSourceLanguage), o => o.EffectiveSourceLanguage);

    /// <summary>Raised when the reader picks a language or toggles Show original (not when the app sets <see cref="Value"/> or autoplay moves). Bubbles.</summary>
    public static readonly RoutedEvent<AiTranslateValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<AiTranslate, AiTranslateValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once a translation has fully arrived, before it is cached. Persist it here and pass it back in <see cref="Translations"/>. Bubbles.</summary>
    public static readonly RoutedEvent<AiTranslateCompletedEventArgs> TranslatedEvent =
        RoutedEvent.Register<AiTranslate, AiTranslateCompletedEventArgs>(nameof(Translated), RoutingStrategies.Bubble);

    /// <summary>Raised when a translation fails. Cancelled requests never raise it. Bubbles.</summary>
    public static readonly RoutedEvent<AiTranslateFailedEventArgs> TranslationFailedEvent =
        RoutedEvent.Register<AiTranslate, AiTranslateFailedEventArgs>(nameof(TranslationFailed), RoutingStrategies.Bubble);

    private readonly Dictionary<AiTranslateLanguage, string> _cache = [];
    private readonly List<MenuEntry> _entries = [];
    private readonly ArcGlyph _chipGlyph = new() { Data = ArcIcons.Languages, Size = 13 };
    private AiTranslateState _state;
    private AiTranslateLanguage _source;
    private AiTranslateLanguage _shown;
    private string _shownText = string.Empty;
    private AiTranslateLanguage? _lastTranslated;
    private (AiTranslateLanguage Language, string Message)? _failure;
    private bool _hasTranslated;
    private AiTranslateRun? _run;
    private LiveLoop? _loop;
    private MenuSurface? _menu;
    private int _autoIndex;
    private double _autoWait = AutoPlayStart;
    private bool _autoStopped;
    private bool _narrow;
    private bool _compact;

    private AiTranslateParagraph? _text;
    private Avatar? _avatar;
    private TextBlock? _name;
    private TextBlock? _meta;
    private Badge? _chip;
    private AvaloniaButton? _picker;
    private AiTranslateSlot? _pickerLabel;
    private AvaloniaButton? _toggle;
    private AiTranslateSlot? _toggleLabel;
    private AvaloniaButton? _retry;
    private TextBlock? _retryLabel;
    private Control? _status;
    private AiTranslateSlot? _statusLabel;

    /// <summary>Creates the card.</summary>
    public AiTranslate()
    {
        _source = AiTranslateText.Detect(string.Empty);
        _shown = _source;
        _shownText = string.Empty;
        // The first pointer or key press inside the card stops the autoplay tour for good.
        AddHandler(PointerPressedEvent, (_, _) => StopAutoPlay(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => StopAutoPlay(), RoutingStrategies.Tunnel, handledEventsToo: true);
        SizeChanged += (_, e) => UpdateBreakpoints(e.NewSize.Width);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SourceLanguageProperty"/>
    public AiTranslateLanguage? SourceLanguage { get => GetValue(SourceLanguageProperty); set => SetValue(SourceLanguageProperty, value); }

    /// <inheritdoc cref="LanguagesProperty"/>
    public IReadOnlyList<AiTranslateLanguage>? Languages { get => GetValue(LanguagesProperty); set => SetValue(LanguagesProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public AiTranslateLanguage? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="TranslatorProperty"/>
    public Func<AiTranslateRequest, IAsyncEnumerable<string>>? Translator { get => GetValue(TranslatorProperty); set => SetValue(TranslatorProperty, value); }

    /// <inheritdoc cref="TranslationsProperty"/>
    public IReadOnlyDictionary<AiTranslateLanguage, string>? Translations { get => GetValue(TranslationsProperty); set => SetValue(TranslationsProperty, value); }

    /// <inheritdoc cref="AuthorProperty"/>
    public AiTranslateAuthor? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    /// <inheritdoc cref="TimestampProperty"/>
    public string? Timestamp { get => GetValue(TimestampProperty); set => SetValue(TimestampProperty, value); }

    /// <inheritdoc cref="ModelNameProperty"/>
    public string? ModelName { get => GetValue(ModelNameProperty); set => SetValue(ModelNameProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AiTranslateSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool? IsSimulated { get => GetValue(IsSimulatedProperty); set => SetValue(IsSimulatedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiTranslateLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public AiTranslateState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="EffectiveSourceLanguageProperty"/>
    public AiTranslateLanguage EffectiveSourceLanguage
    {
        get => _source;
        private set => SetAndRaise(EffectiveSourceLanguageProperty, ref _source, value);
    }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<AiTranslateValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="TranslatedEvent"/>
    public event EventHandler<AiTranslateCompletedEventArgs>? Translated
    {
        add => AddHandler(TranslatedEvent, value);
        remove => RemoveHandler(TranslatedEvent, value);
    }

    /// <inheritdoc cref="TranslationFailedEvent"/>
    public event EventHandler<AiTranslateFailedEventArgs>? TranslationFailed
    {
        add => AddHandler(TranslationFailedEvent, value);
        remove => RemoveHandler(TranslationFailedEvent, value);
    }

    /// <summary>Asks for the failed translation again.</summary>
    public void Retry()
    {
        if (_failure is { } failure)
        {
            StartRun(failure.Language);
        }
    }

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Text"/>.</summary>
    internal AiTranslateSimulation? PlayingSimulation => Text is null ? Simulation : null;

    /// <summary>The text in its original language (empty with no data).</summary>
    internal string OriginalText => Text ?? PlayingSimulation?.Text ?? string.Empty;

    /// <summary>Whether there is a text to translate.</summary>
    internal bool HasText => !string.IsNullOrWhiteSpace(OriginalText);

    /// <summary>The language passed for the text: <see cref="SourceLanguage"/>, or the simulation's while it plays.</summary>
    internal AiTranslateLanguage? GivenSourceLanguage => SourceLanguage ?? PlayingSimulation?.SourceLanguage;

    /// <summary>The byline's author: <see cref="Author"/>, or the simulation's while it plays.</summary>
    internal AiTranslateAuthor? EffectiveAuthor => Author ?? PlayingSimulation?.Author;

    /// <summary>The model credited in the status line.</summary>
    internal string EffectiveModelName => ModelName ?? PlayingSimulation?.ModelName ?? string.Empty;

    /// <summary>The language whose text is on screen (the target while one streams in is not on screen yet).</summary>
    internal AiTranslateLanguage ShownLanguage => _shown;

    /// <summary>The text on screen once settled: the original or a translation.</summary>
    internal string ShownText => _shownText;

    /// <summary>The language a stream is bringing in, or null.</summary>
    internal AiTranslateLanguage? TranslatingTo => _run?.To;

    internal AiTranslateLanguage? LastTranslated => _lastTranslated;

    internal string? FailureMessage => _failure?.Message;

    internal bool IsCached(AiTranslateLanguage language) => _cache.ContainsKey(language);

    internal bool EffectiveAutoPlay => !_autoStopped && HasText && (AutoPlay ?? PlayingSimulation is not null);

    internal bool EffectiveSimulated => IsSimulated ?? (Translator is null || PlayingSimulation is not null);

    internal AiTranslateParagraph? Paragraph => _text;

    internal MenuSurface? Menu => _menu;

    internal IReadOnlyList<MenuEntry> MenuEntries => _entries;

    internal AiTranslateSlot? PickerLabel => _pickerLabel;

    internal AiTranslateSlot? ToggleLabel => _toggleLabel;

    internal AiTranslateSlot? StatusLabel => _statusLabel;

    internal AvaloniaButton? PickerButton => _picker;

    internal AvaloniaButton? ToggleButton => _toggle;

    internal AvaloniaButton? RetryButton => _retry;

    internal Badge? Chip => _chip;

    internal Control? StatusPanel => _status;

    /// <summary>The languages of the picker: <see cref="Languages"/> (or all six) with the source language always in.</summary>
    internal List<AiTranslateLanguage> EffectiveLanguages
    {
        get
        {
            var list = (Languages ?? AiTranslateText.All).Distinct().ToList();
            if (!list.Contains(_source))
            {
                list.Insert(0, _source);
            }
            return list;
        }
    }

    /// <summary>The chip's text for a card of this width.</summary>
    internal string ChipText
    {
        get
        {
            var name = Labels.LanguageName(_source);
            if (_compact)
            {
                return name;
            }
            return GivenSourceLanguage is null ? Labels.Detected(name) : Labels.WrittenIn(name);
        }
    }

    /// <summary>The status label: the model's name, Translating to, Translated from, Showing original or Couldn't translate.</summary>
    internal string StatusText => State switch
    {
        AiTranslateState.Translating when _run is { } run => Labels.TranslatingTo(Labels.LanguageName(run.To)),
        AiTranslateState.Failed => Labels.Failed,
        AiTranslateState.Translated => Labels.TranslatedFrom(Labels.LanguageName(_source)),
        _ => _hasTranslated ? Labels.ShowingOriginal : EffectiveModelName,
    };

    /// <summary>What the polite status region says: the status, and that nothing was sent while simulated.</summary>
    internal string Announcement
    {
        get
        {
            var text = StatusText;
            if (State == AiTranslateState.Failed && _failure is { } failure && failure.Message.Length > 0)
            {
                text += ". " + failure.Message;
            }
            return EffectiveSimulated && (_hasTranslated || State != AiTranslateState.Original) ? text + ". " + Labels.Simulated : text;
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _text = e.NameScope.Find<AiTranslateParagraph>(TextPart);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        _chip = e.NameScope.Find<Badge>(ChipPart);
        _picker = e.NameScope.Find<AvaloniaButton>(PickerPart);
        _pickerLabel = e.NameScope.Find<AiTranslateSlot>(PickerLabelPart);
        _toggle = e.NameScope.Find<AvaloniaButton>(TogglePart);
        _toggleLabel = e.NameScope.Find<AiTranslateSlot>(ToggleLabelPart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _retryLabel = e.NameScope.Find<TextBlock>(RetryLabelPart);
        _status = e.NameScope.Find<Control>(StatusPart);
        _statusLabel = e.NameScope.Find<AiTranslateSlot>(StatusLabelPart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggle;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetry;
        }
        if (_chip is not null)
        {
            _chip.Icon = _chipGlyph;
        }
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        RebuildMenu();
        if (_picker is not null && _menu is not null)
        {
            DropdownMenu.SetMenu(_picker, _menu);
        }
        // A stream in flight starts again on the new parts.
        if (_run is not null)
        {
            CancelRun();
        }
        _text?.Show(_shownText, crossfade: false);
        Refresh();
        Apply(crossfade: false);
    }

    private void Unhook()
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggle;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetry;
        }
        if (_picker is not null)
        {
            DropdownMenu.SetMenu(_picker, null);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == SourceLanguageProperty || change.Property == SimulationProperty)
        {
            ResetForText();
            UpdateLoop();
        }
        else if (change.Property == ValueProperty)
        {
            Apply(crossfade: true);
        }
        else if (change.Property == LanguagesProperty || change.Property == LabelsProperty)
        {
            RebuildMenu();
            Refresh();
        }
        else if (change.Property == TranslationsProperty || change.Property == TranslatorProperty)
        {
            if (change.Property == TranslatorProperty)
            {
                _cache.Clear();
            }
            Refresh();
        }
        else if (change.Property == AuthorProperty || change.Property == TimestampProperty || change.Property == ModelNameProperty
                 || change.Property == ModelMarkProperty || change.Property == IsSimulatedProperty)
        {
            if (change.Property == ModelNameProperty)
            {
                RebuildMenu();
            }
            Refresh();
        }
        else if (change.Property == AutoPlayProperty)
        {
            UpdateLoop();
        }
        else if (change.Property == StateProperty)
        {
            var state = State;
            PseudoClasses.Set(TranslatingPseudoClass, state == AiTranslateState.Translating);
            PseudoClasses.Set(TranslatedPseudoClass, state == AiTranslateState.Translated);
            PseudoClasses.Set(FailedPseudoClass, state == AiTranslateState.Failed);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_run is null && State != AiTranslateState.Failed)
        {
            Apply(crossfade: false);
        }
        UpdateLoop();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the window cancels the request and its timers; the settled text stays.
        if (_run is not null)
        {
            CancelRun();
            _text?.Show(_shownText, crossfade: false);
            State = _shown == _source ? AiTranslateState.Original : AiTranslateState.Translated;
            Refresh();
        }
    }

    /// <summary>A new text (or source language): the cache is cleared and the new original is shown.</summary>
    private void ResetForText()
    {
        CancelRun();
        _cache.Clear();
        _failure = null;
        _lastTranslated = null;
        _hasTranslated = false;
        EffectiveSourceLanguage = GivenSourceLanguage ?? AiTranslateText.Detect(OriginalText);
        _shown = _source;
        _shownText = OriginalText;
        _text?.Show(_shownText, crossfade: true);
        State = AiTranslateState.Original;
        // An app that sets Text and Value together (XAML) keeps its Value; a new text later shows its original.
        if (IsLoaded && Value is { } value && value != _source)
        {
            SetCurrentValue(ValueProperty, null);
        }
        RebuildMenu();
        Refresh();
    }

    /// <summary>Brings the screen to <see cref="Value"/>: the original, a cached translation (crossfade) or a new stream.</summary>
    private void Apply(bool crossfade)
    {
        var target = Value ?? _source;
        if (_run is { } run && run.To == target)
        {
            return;
        }
        if (_run is not null)
        {
            CancelRun();
            _text?.Show(_shownText, crossfade: false);
        }
        if (target == _source || !HasText)
        {
            ShowSettled(_source, OriginalText, crossfade);
            State = AiTranslateState.Original;
        }
        else if (_cache.TryGetValue(target, out var cached))
        {
            ShowSettled(target, cached, crossfade);
            _lastTranslated = target;
            State = AiTranslateState.Translated;
        }
        else if (State == AiTranslateState.Failed && _failure?.Language == target)
        {
            // The failed language stays picked with the previous text on screen until Retry.
            return;
        }
        else if (_text is not null)
        {
            StartRun(target);
            return;
        }
        Refresh();
    }

    private void ShowSettled(AiTranslateLanguage language, string text, bool crossfade)
    {
        _failure = null;
        _shown = language;
        _shownText = text;
        _text?.Show(text, crossfade && IsLoaded);
    }

    private void StartRun(AiTranslateLanguage target)
    {
        CancelRun();
        if (target == _source)
        {
            return;
        }
        _failure = null;
        _hasTranslated = true;
        var run = new AiTranslateRun(_source, target);
        _run = run;
        _text?.BeginStream(_shownText);
        State = AiTranslateState.Translating;
        var simulation = PlayingSimulation;
        if (Translator is { } translator && simulation is null)
        {
            _ = PumpAsync(run, translator);
        }
        else
        {
            run.Delay = SimulatedLatency;
            string? ready = null;
            if (Translations is { } translations && translations.TryGetValue(target, out var given))
            {
                ready = given;
            }
            else if (simulation is not null)
            {
                ready = simulation.Translation(target);
            }
            run.Simulated = ready;
        }
        Refresh();
        UpdateLoop();
    }

    private async Task PumpAsync(AiTranslateRun run, Func<AiTranslateRequest, IAsyncEnumerable<string>> translator)
    {
        var token = run.Cancellation.Token;
        try
        {
            var request = new AiTranslateRequest(OriginalText, run.From, run.To, token);
            await foreach (var delta in translator(request).WithCancellation(token))
            {
                if (!ReferenceEquals(run, _run))
                {
                    return;
                }
                if (!string.IsNullOrEmpty(delta))
                {
                    run.Pending.Append(delta);
                    run.Received.Append(delta);
                    UpdateLoop();
                }
            }
            if (ReferenceEquals(run, _run))
            {
                run.SourceDone = true;
                UpdateLoop();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Aborted: another language, the original or the card left the window. Never a failure.
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(run, _run))
            {
                Fail(run, exception.Message, exception);
            }
        }
    }

    private void CancelRun()
    {
        if (_run is { } run)
        {
            _run = null;
            run.Cancellation.Cancel();
        }
    }

    /// <summary>One frame of the card's clock: the simulated latency, a chunk every 34ms, completion and autoplay.</summary>
    internal bool Step(double seconds)
    {
        var busy = false;
        if (_run is { } run)
        {
            busy = true;
            if (run.Delay > 0)
            {
                run.Delay -= seconds;
                if (run.Delay <= 0)
                {
                    if (run.Simulated is { } text)
                    {
                        run.Pending.Append(text);
                        run.Received.Append(text);
                        run.SourceDone = true;
                    }
                    else
                    {
                        Fail(run, Labels.Missing(Labels.LanguageName(run.To)), null);
                    }
                }
            }
            else
            {
                run.Since = Math.Min(run.Since + seconds, 0.25);
                while (run.Since >= ChunkInterval && ReferenceEquals(run, _run)
                       && AiTranslateText.TakeChunk(run.Pending, run.SourceDone, run.To == AiTranslateLanguage.Japanese, run.Chunks, out var chunk))
                {
                    run.Chunks++;
                    run.Since -= ChunkInterval;
                    _text?.Append(chunk);
                }
                if (run.SourceDone && run.Pending.Length > 0 && string.IsNullOrWhiteSpace(run.Pending.ToString()))
                {
                    run.Pending.Clear();
                }
                if (ReferenceEquals(run, _run) && run.SourceDone && run.Pending.Length == 0)
                {
                    Finish(run);
                }
            }
        }
        if (StepAutoPlay(seconds))
        {
            busy = true;
        }
        return busy || _run is not null;
    }

    /// <summary>Runs the card's clock for <paramref name="seconds"/> in 60 Hz frames (tests).</summary>
    internal void Advance(double seconds)
    {
        const double frame = 1 / 60.0;
        for (var t = 0.0; t < seconds - 1e-9; t += frame)
        {
            Step(Math.Min(frame, seconds - t));
            _text?.Step(Math.Min(frame, seconds - t));
        }
    }

    private void Finish(AiTranslateRun run)
    {
        _run = null;
        var text = run.Received.ToString().Trim();
        RaiseEvent(new AiTranslateCompletedEventArgs(TranslatedEvent, run.From, run.To, text));
        _cache[run.To] = text;
        _shown = run.To;
        _shownText = text;
        _lastTranslated = run.To;
        _text?.EndStream(text);
        State = AiTranslateState.Translated;
        Refresh();
    }

    private void Fail(AiTranslateRun run, string message, Exception? exception)
    {
        _run = null;
        run.Cancellation.Cancel();
        _failure = (run.To, message);
        // The previous text comes back.
        _text?.Show(_shownText, crossfade: true);
        State = AiTranslateState.Failed;
        Refresh();
        RaiseEvent(new AiTranslateFailedEventArgs(TranslationFailedEvent, run.From, run.To, message, exception));
    }

    /// <summary>The reader picks a language from the menu; picking the language that just failed retries it.</summary>
    internal void Pick(AiTranslateLanguage language)
    {
        if (State == AiTranslateState.Failed && _failure?.Language == language)
        {
            Retry();
            return;
        }
        PickByUser(language);
    }

    private void PickByUser(AiTranslateLanguage language)
    {
        var before = Value ?? _source;
        if (State == AiTranslateState.Failed && language == _shown)
        {
            _failure = null;
            State = _shown == _source ? AiTranslateState.Original : AiTranslateState.Translated;
            Refresh();
        }
        SetCurrentValue(ValueProperty, language);
        if (before != language)
        {
            RaiseEvent(new AiTranslateValueChangedEventArgs(ValueChangedEvent, language, language == _source));
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (_run is not null || _shown != _source)
        {
            if (_shown != _source)
            {
                _lastTranslated = _shown;
            }
            PickByUser(_source);
        }
        else if (_lastTranslated is { } last)
        {
            PickByUser(last);
        }
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => Retry();

    private void StopAutoPlay()
    {
        _autoStopped = true;
    }

    private bool StepAutoPlay(double seconds)
    {
        if (!EffectiveAutoPlay)
        {
            return false;
        }
        if (_run is not null || _text is null)
        {
            return true;
        }
        _autoWait -= seconds;
        if (_autoWait > 0)
        {
            return true;
        }
        var tour = TourLanguages().Where(l => l != _source && EffectiveLanguages.Contains(l)).ToList();
        if (tour.Count == 0)
        {
            _autoStopped = true;
            return false;
        }
        tour.Add(_source);
        var next = tour[_autoIndex % tour.Count];
        _autoIndex++;
        _autoWait = AutoPlayDwell;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            // Reduced motion: translate once and stop.
            _autoStopped = true;
        }
        if (State == AiTranslateState.Failed)
        {
            _failure = null;
        }
        SetCurrentValue(ValueProperty, next);
        return true;
    }

    /// <summary>
    /// The languages the tour picks: the simulation's <see cref="AiTranslateSimulation.Tour"/>; else, while it plays, the
    /// languages it has a translation for; else every language in the picker.
    /// </summary>
    private IEnumerable<AiTranslateLanguage> TourLanguages()
    {
        if (PlayingSimulation is { } simulation)
        {
            return simulation.Tour ?? EffectiveLanguages.Where(l => simulation.Translation(l) is not null || Translations?.ContainsKey(l) == true);
        }
        return EffectiveLanguages;
    }

    private void UpdateLoop()
    {
        if (_run is not null || EffectiveAutoPlay)
        {
            _loop ??= new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off };
            _loop.Start();
        }
        else
        {
            _loop?.Stop();
        }
    }

    private void UpdateBreakpoints(double width)
    {
        var narrow = width > 0 && width < NarrowWidth;
        var compact = width > 0 && width < CompactWidth;
        if (narrow == _narrow && compact == _compact)
        {
            return;
        }
        _narrow = narrow;
        _compact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        Refresh();
    }

    private void RebuildMenu()
    {
        if (_menu is null)
        {
            _menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 6 };
            _menu.ItemClicked += OnMenuItemClicked;
            _menu.Opened += OnMenuOpened;
        }
        var labels = Labels;
        AutomationProperties.SetName(_menu, labels.TranslateTo);
        _menu.Items.Clear();
        _entries.Clear();
        foreach (var language in EffectiveLanguages)
        {
            var entry = new MenuEntry
            {
                Header = labels.NativeName(language),
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "ai-translate",
                Tag = language,
                InputGestureText = language == _source ? labels.Original : null,
            };
            AutomationProperties.SetName(entry, language == _source
                ? labels.NativeName(language) + ", " + labels.Original
                : labels.NativeName(language));
            _entries.Add(entry);
            _menu.Items.Add(entry);
        }
        if (_pickerLabel is not null)
        {
            _pickerLabel.Reserve([labels.Translate, .. EffectiveLanguages.Select(labels.NativeName)]);
        }
        if (_toggleLabel is not null)
        {
            _toggleLabel.Reserve([labels.ShowOriginal, labels.ShowTranslation]);
        }
        if (_statusLabel is not null)
        {
            var languages = EffectiveLanguages;
            _statusLabel.Reserve([
                EffectiveModelName, labels.ShowingOriginal, labels.Failed,
                .. languages.Select(l => labels.TranslatingTo(labels.LanguageName(l))),
                labels.TranslatedFrom(labels.LanguageName(_source)),
            ]);
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is AiTranslateLanguage language)
        {
            StopAutoPlay();
            Pick(language);
        }
    }

    /// <summary>A menu opened from the keyboard starts on the current language (it opens on the first row by itself).</summary>
    private void OnMenuOpened(object? sender, RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_menu is { IsOpen: true, HighlightedEntry: not null } && _entries.FirstOrDefault(entry => entry.IsChecked) is { } current)
            {
                current.Focus(NavigationMethod.Directional);
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void Refresh()
    {
        var labels = Labels;
        var author = EffectiveAuthor;
        var hasText = HasText;
        PseudoClasses.Set(NoAuthorPseudoClass, author is null);
        AutomationProperties.SetName(this, labels.Region(author?.Name));
        if (author is not null)
        {
            if (_avatar is not null)
            {
                _avatar.DisplayName = author.Name;
                _avatar.Source = author.Avatar;
            }
            if (_name is not null)
            {
                _name.Text = author.Name;
            }
            if (_meta is not null)
            {
                var timestamp = Timestamp ?? PlayingSimulation?.Timestamp;
                var meta = string.Join(" · ", new[] { author.Role, timestamp }.Where(s => !string.IsNullOrWhiteSpace(s)));
                _meta.Text = meta;
                _meta.IsVisible = meta.Length > 0;
            }
        }
        if (_chip is not null)
        {
            // With no text there is no language to name: the empty card shows no chip.
            _chip.IsVisible = hasText;
            var chip = ChipText;
            if (!Equals(_chip.Content, chip))
            {
                _chip.Content = chip;
            }
        }

        var state = State;
        var target = _run?.To;
        var onScreen = target ?? _shown;
        foreach (var entry in _entries)
        {
            var isCurrent = entry.Tag is AiTranslateLanguage language && language == onScreen;
            if (entry.IsChecked != isCurrent)
            {
                entry.SetCurrentValue(MenuEntry.IsCheckedProperty, isCurrent);
            }
        }
        if (_pickerLabel is not null)
        {
            _pickerLabel.Text = onScreen == _source ? labels.Translate : labels.NativeName(onScreen);
        }
        if (_picker is not null)
        {
            _picker.IsEnabled = hasText;
            AutomationProperties.SetName(_picker, onScreen == _source ? labels.Translate : labels.Translate + ", " + labels.NativeName(onScreen));
        }

        var showsTranslation = _run is not null || _shown != _source;
        if (_toggleLabel is not null)
        {
            _toggleLabel.Text = showsTranslation || _lastTranslated is null ? labels.ShowOriginal : labels.ShowTranslation;
        }
        if (_toggle is not null)
        {
            _toggle.IsEnabled = showsTranslation || _lastTranslated is not null;
            var pressed = !showsTranslation && _lastTranslated is not null;
            AutomationProperties.SetName(_toggle, pressed ? labels.ShowTranslation : labels.ShowOriginal);
            AutomationProperties.SetItemStatus(_toggle, pressed ? "pressed" : null);
        }
        if (_retry is not null)
        {
            _retry.IsVisible = state == AiTranslateState.Failed;
        }
        if (_retryLabel is not null)
        {
            _retryLabel.Text = labels.Retry;
        }

        if (_statusLabel is not null)
        {
            // On a wide card the status keeps to the right edge; dropped under the controls it lines up with the text.
            _statusLabel.ContentAlignment = _narrow ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
            var mark = state == AiTranslateState.Failed ? ArcIcons.TriangleAlert : ModelMark ?? ArcIcons.Sparkles;
            var color = state switch
            {
                AiTranslateState.Failed => ArcDesignTokenKind.Warning,
                AiTranslateState.Translating => ArcDesignTokenKind.Accent,
                _ => ArcDesignTokenKind.TextSecondary,
            };
            _statusLabel.Show(StatusText, mark, color);
        }
        if (_status is not null)
        {
            AutomationProperties.SetName(_status, Announcement);
            Tooltip.SetTip(_status, state == AiTranslateState.Failed && _failure is { Message.Length: > 0 } failure ? failure.Message : null);
        }
        if (_text is not null)
        {
            AutomationProperties.SetItemStatus(_text, state == AiTranslateState.Translating ? "busy" : null);
            AutomationProperties.SetHelpText(_text, AiTranslateText.Code(_run is null ? _shown : _run.To));
        }
    }
}

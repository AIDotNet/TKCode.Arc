using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An article card with a Full, Summary and Key points switch and a length slider (Arc TL;DR toggle, design reference).
/// Switching streams an AI summary in place of the article in chunks of 3 to 8 words while the body's height follows on a
/// bounce-free spring. Key points arrive as a checklist: each point ticks when it is written, and once the list is done
/// its ¶ jump link switches back to the article, scrolls the source paragraph into view, focuses it and highlights it.
/// <para>
/// Results are kept per mode and length, so switching back is instant; Regenerate asks again, Stop or Escape ends a
/// stream and keeps what arrived, and a failure shows the message with Try again. Give it an <see cref="Article"/> with
/// stable paragraph ids and a <see cref="Summarizer"/> that streams <see cref="TldrStreamEvent"/>s; without a summarizer
/// the summaries are simulated from the article's lead sentences. Without an article it plays a <see cref="Simulation"/>
/// (an article and its written summaries you pass, opening the summary by itself once it has been half in view for a
/// second); with neither it shows the quiet empty state.
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[TemplatePart(SourceMarkPart, typeof(ArcGlyph))]
[TemplatePart(MonogramPart, typeof(Border))]
[TemplatePart(MonogramTextPart, typeof(TextBlock))]
[TemplatePart(AuthorRowPart, typeof(Control))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(AuthorPart, typeof(TextBlock))]
[TemplatePart(ModePart, typeof(SegmentedControl))]
[TemplatePart(LengthPart, typeof(Slider))]
[TemplatePart(BodyBoxPart, typeof(AiHeightBox))]
[TemplatePart(BodyPart, typeof(ArcContentSwap))]
[TemplatePart(StatePart, typeof(ArcContentSwap))]
[TemplatePart(ActionsPart, typeof(ArcContentSwap))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(ModelNamePart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, BusyPseudoClass)]
public partial class TldrToggle : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string MetaPart = "PART_Meta";
    internal const string SourceMarkPart = "PART_SourceMark";
    internal const string MonogramPart = "PART_Monogram";
    internal const string MonogramTextPart = "PART_MonogramText";
    internal const string AuthorRowPart = "PART_AuthorRow";
    internal const string AvatarPart = "PART_Avatar";
    internal const string AuthorPart = "PART_Author";
    internal const string ModePart = "PART_Mode";
    internal const string LengthPart = "PART_Length";
    internal const string BodyBoxPart = "PART_BodyBox";
    internal const string BodyPart = "PART_Body";
    internal const string StatePart = "PART_State";
    internal const string ActionsPart = "PART_Actions";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string ModelNamePart = "PART_ModelName";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set below <see cref="NarrowWidth"/>: the slider moves under the switch at full width.</summary>
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>Set below <see cref="CompactWidth"/>: the padding steps from 24px to 16px.</summary>
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Set while a result streams (Arc's aria-busy).</summary>
    internal const string BusyPseudoClass = ":busy";

    /// <summary>The card width below which the slider moves under the switch.</summary>
    internal const double NarrowWidth = 520;

    /// <summary>The card width below which the padding steps down.</summary>
    internal const double CompactWidth = 440;

    /// <summary>The shortest and longest length.</summary>
    internal const int MinLength = 1;

    internal const int MaxLength = 5;

    /// <summary>The reading speed behind a worked-out reading time.</summary>
    internal const int WordsPerMinute = 230;

    /// <summary>How long the card must be half in view before it opens the summary by itself.</summary>
    internal static readonly TimeSpan AutoPlayDelay = TimeSpan.FromSeconds(1);

    private static readonly int[] s_targetWords = [35, 60, 85, 100, 130];

    private static readonly TldrArticle s_noArticle = new();

    private static readonly AgentModel s_noModel = new(string.Empty);

    /// <summary>The article to show and summarize; null plays <see cref="Simulation"/>, or shows the quiet empty state without one.</summary>
    public static readonly StyledProperty<TldrArticle?> ArticleProperty =
        AvaloniaProperty.Register<TldrToggle, TldrArticle?>(nameof(Article));

    /// <summary>
    /// Produces summaries and key points as a stream. Null simulates them from the article's lead sentences
    /// (<see cref="TldrStreams.Simulate"/>), which sends nothing. The request's token is cancelled on Stop, Escape, a mode
    /// or length change, a new article, or when the card leaves the window. A playing <see cref="Simulation"/> does not use it.
    /// </summary>
    public static readonly StyledProperty<TldrSummarizer?> SummarizerProperty =
        AvaloniaProperty.Register<TldrToggle, TldrSummarizer?>(nameof(Summarizer));

    /// <summary>The model credited in the footer, with its 14px mark; null credits the <see cref="Simulation"/>'s model while one plays, or none.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<TldrToggle, AgentModel?>(nameof(Model));

    /// <summary>What the body shows (two-way): the article, a summary or key points.</summary>
    public static readonly StyledProperty<TldrMode> ModeProperty =
        AvaloniaProperty.Register<TldrToggle, TldrMode>(nameof(Mode), TldrMode.Full, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The length from 1 to 5 (two-way): summaries aim for about 35, 60, 85, 100 and 130 words; key points return 3 to 7 items.
    /// </summary>
    public static readonly StyledProperty<int> LengthProperty =
        AvaloniaProperty.Register<TldrToggle, int>(nameof(Length), 2, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => Math.Clamp(value, MinLength, MaxLength));

    /// <summary>
    /// Opens the summary by itself once the card has been half in view for a second, unless the reader has already
    /// touched it. Null turns it on only while a <see cref="Simulation"/> plays.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<TldrToggle, bool?>(nameof(AutoPlay));

    /// <summary>
    /// An article to play while the card has no <see cref="Article"/>: the article, the model and the summaries and key
    /// points written for it, streamed as a simulation (nothing is sent). Your own <see cref="Article"/> wins over it; null
    /// stops it and shows the quiet empty state.
    /// </summary>
    public static readonly StyledProperty<TldrToggleSimulation?> SimulationProperty =
        AvaloniaProperty.Register<TldrToggle, TldrToggleSimulation?>(nameof(Simulation));

    /// <summary>
    /// Adds "Simulated, nothing was sent" to announcements and error text. Null turns it on while a
    /// <see cref="Simulation"/> plays or there is no <see cref="Summarizer"/>; set it when you wrap
    /// <see cref="TldrStreams.Simulate"/> or <see cref="TldrToggleSimulation.Summarize"/> yourself.
    /// </summary>
    public static readonly StyledProperty<bool?> IsSimulatedProperty =
        AvaloniaProperty.Register<TldrToggle, bool?>(nameof(IsSimulated));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<TldrToggleLabels> LabelsProperty =
        AvaloniaProperty.Register<TldrToggle, TldrToggleLabels>(nameof(Labels), TldrToggleLabels.Default);

    /// <summary>Raised when the switch, a jump link or autoplay changes <see cref="Mode"/> (not when the app sets it). Bubbles.</summary>
    public static readonly RoutedEvent<TldrModeChangedEventArgs> ModeChangedEvent =
        RoutedEvent.Register<TldrToggle, TldrModeChangedEventArgs>(nameof(ModeChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a slider drag is released or a key moves it (not on every step of a drag), so each change makes one
    /// request. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<TldrLengthChangedEventArgs> LengthChangedEvent =
        RoutedEvent.Register<TldrToggle, TldrLengthChangedEventArgs>(nameof(LengthChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a result finishes, for analytics or a server-side cache. Stopped and failed runs do not raise it. Bubbles.</summary>
    public static readonly RoutedEvent<TldrCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<TldrToggle, TldrCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Raised when a key point's jump link is used, before the card scrolls to the paragraph. Bubbles.</summary>
    public static readonly RoutedEvent<TldrJumpEventArgs> JumpingEvent =
        RoutedEvent.Register<TldrToggle, TldrJumpEventArgs>(nameof(Jumping), RoutingStrategies.Bubble);

    private TextBlock? _title;
    private TextBlock? _meta;
    private ArcGlyph? _sourceMark;
    private Border? _monogram;
    private TextBlock? _monogramText;
    private Control? _authorRow;
    private Avatar? _avatar;
    private TextBlock? _author;
    private SegmentedControl? _mode;
    private Slider? _length;
    private AiHeightBox? _bodyBox;
    private ArcContentSwap? _body;
    private ArcContentSwap? _state;
    private ArcContentSwap? _actions;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private ExplainerAnnouncer? _announcer;
    private bool _syncing;
    private bool _touched;
    private bool _autoPlayed;
    private ViewportVisibility? _viewport;
    private DispatcherTimer? _autoPlayTimer;

    static TldrToggle()
    {
        SpecialFocusModality.Watch();
        AffectsMeasure<TldrToggle>(MaxWidthProperty);
    }

    /// <summary>Creates the card.</summary>
    public TldrToggle()
    {
        // Any press or key inside the card counts as the reader touching it (autoplay then stays out of the way).
        AddHandler(PointerPressedEvent, (_, _) => _touched = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => _touched = true, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc cref="ArticleProperty"/>
    public TldrArticle? Article { get => GetValue(ArticleProperty); set => SetValue(ArticleProperty, value); }

    /// <inheritdoc cref="SummarizerProperty"/>
    public TldrSummarizer? Summarizer { get => GetValue(SummarizerProperty); set => SetValue(SummarizerProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="ModeProperty"/>
    public TldrMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <inheritdoc cref="LengthProperty"/>
    public int Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public TldrToggleSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool? IsSimulated { get => GetValue(IsSimulatedProperty); set => SetValue(IsSimulatedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TldrToggleLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ModeChangedEvent"/>
    public event EventHandler<TldrModeChangedEventArgs>? ModeChanged
    {
        add => AddHandler(ModeChangedEvent, value);
        remove => RemoveHandler(ModeChangedEvent, value);
    }

    /// <inheritdoc cref="LengthChangedEvent"/>
    public event EventHandler<TldrLengthChangedEventArgs>? LengthChanged
    {
        add => AddHandler(LengthChangedEvent, value);
        remove => RemoveHandler(LengthChangedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<TldrCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <inheritdoc cref="JumpingEvent"/>
    public event EventHandler<TldrJumpEventArgs>? Jumping
    {
        add => AddHandler(JumpingEvent, value);
        remove => RemoveHandler(JumpingEvent, value);
    }

    /// <summary>The words a summary aims for at <paramref name="length"/> (1 to 5): 35, 60, 85, 100 or 130.</summary>
    internal static int TargetWordsFor(int length) => s_targetWords[Math.Clamp(length, MinLength, MaxLength) - 1];

    /// <summary>The key points a list returns at <paramref name="length"/> (1 to 5): 3 to 7.</summary>
    internal static int PointCountFor(int length) => Math.Clamp(length, MinLength, MaxLength) + 2;

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Article"/>.</summary>
    internal TldrToggleSimulation? PlayingSimulation => Article is null ? Simulation : null;

    /// <summary>The article on screen: <see cref="Article"/>, the simulation's, or an empty one (the quiet empty state).</summary>
    internal TldrArticle DisplayedArticle => Article ?? PlayingSimulation?.Article ?? s_noArticle;

    /// <summary>The model credited: <see cref="Model"/>, the simulation's, or none (an empty name).</summary>
    internal AgentModel DisplayedModel => Model ?? PlayingSimulation?.Model ?? s_noModel;

    internal bool EffectiveAutoPlay => AutoPlay ?? PlayingSimulation is not null;

    internal bool EffectiveSimulated => IsSimulated ?? (PlayingSimulation is not null || Summarizer is null);

    /// <summary>The reading time the header shows.</summary>
    internal string ReadingTimeText
    {
        get
        {
            var article = DisplayedArticle;
            if (article.ReadingTime is { } given)
            {
                return given;
            }
            var words = article.Paragraphs.Sum(p => TldrStreams.WordCount(p.Text));
            return Labels.ReadingTime(Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute)));
        }
    }

    internal SegmentedControl? ModeSwitch => _mode;

    internal Slider? LengthSlider => _length;

    internal AiHeightBox? BodyBox => _bodyBox;

    internal ArcContentSwap? Body => _body;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_mode is not null)
        {
            _mode.SelectionChanged -= OnModeSelected;
        }
        if (_length is not null)
        {
            _length.ValueCommitted -= OnLengthCommitted;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        _sourceMark = e.NameScope.Find<ArcGlyph>(SourceMarkPart);
        _monogram = e.NameScope.Find<Border>(MonogramPart);
        _monogramText = e.NameScope.Find<TextBlock>(MonogramTextPart);
        _authorRow = e.NameScope.Find<Control>(AuthorRowPart);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _author = e.NameScope.Find<TextBlock>(AuthorPart);
        _mode = e.NameScope.Find<SegmentedControl>(ModePart);
        _length = e.NameScope.Find<Slider>(LengthPart);
        _bodyBox = e.NameScope.Find<AiHeightBox>(BodyBoxPart);
        if (_bodyBox is not null)
        {
            _bodyBox.HeightSpring = AiGlyphMotion.Settle;
        }
        _body = e.NameScope.Find<ArcContentSwap>(BodyPart);
        _state = e.NameScope.Find<ArcContentSwap>(StatePart);
        _actions = e.NameScope.Find<ArcContentSwap>(ActionsPart);
        _modelMark = e.NameScope.Find<ArcGlyph>(ModelMarkPart);
        _modelName = e.NameScope.Find<TextBlock>(ModelNamePart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_title is not null)
        {
            AutomationProperties.SetHeadingLevel(_title, 2);
        }
        if (_length is not null)
        {
            _length.Minimum = MinLength;
            _length.Maximum = MaxLength;
            _length.SmallChange = 1;
            _length.IsValueVisible = true;
            _length.ValueCommitted += OnLengthCommitted;
        }
        if (_mode is not null)
        {
            _mode.SelectionChanged += OnModeSelected;
        }
        _shownStatus = null;
        _shownActions = null;
        ApplyLabels();
        BuildHeader();
        BuildArticleView();
        _shownKey = null;
        SyncControls();
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            SyncControls();
            Sync();
        }
        else if (change.Property == LengthProperty)
        {
            SyncControls();
            Sync();
        }
        else if (change.Property == ArticleProperty || change.Property == SimulationProperty)
        {
            ResetResults();
            BuildHeader();
            BuildArticleView();
            SyncControls();
            Sync();
            UpdateFooter();
            // A simulation set (or taken away) after the card is shown starts (or stops) the autoplay watch.
            StopAutoPlay();
            if (VisualRoot is not null)
            {
                WatchAutoPlay();
            }
        }
        else if (change.Property == SummarizerProperty)
        {
            ResetResults();
            Sync();
        }
        else if (change.Property == ModelProperty)
        {
            UpdateFooter();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            BuildHeader();
            UpdateFooter();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
        WatchAutoPlay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the window aborts a running request; it runs again when the card comes back.
        if (IsRunning)
        {
            CancelRun();
            _phase = TldrTogglePhase.Idle;
            _shownKey = null;
            PseudoClasses.Set(BusyPseudoClass, false);
        }
        StopAutoPlay();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            SetWidthClasses(Math.Min(availableSize.Width, MaxWidth));
        }
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SetWidthClasses(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsRunning)
        {
            Stop();
            e.Handled = true;
        }
    }

    private void SetWidthClasses(double width)
    {
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        PseudoClasses.Set(CompactPseudoClass, width < CompactWidth);
    }

    /// <summary>The words and names of the switch and the slider.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        if (_mode is not null)
        {
            _syncing = true;
            try
            {
                _mode.ItemsSource = new[]
                {
                    new SegmentedOption(TldrMode.Full, labels.Full),
                    new SegmentedOption(TldrMode.Summary, labels.Summary),
                    new SegmentedOption(TldrMode.KeyPoints, labels.KeyPoints),
                };
                _mode.SelectedValue = Mode;
            }
            finally
            {
                _syncing = false;
            }
            AutomationProperties.SetName(_mode, labels.ModeGroup);
        }
        if (_length is not null)
        {
            _length.Label = labels.Length;
            _length.ValueFormatter = value => Labels.LengthValue(Mode == TldrMode.Full ? TldrMode.Summary : Mode, (int)Math.Round(value));
        }
    }

    /// <summary>Brings the switch and the slider in line with Mode and Length; the slider is disabled in Full mode.</summary>
    private void SyncControls()
    {
        var hasText = DisplayedArticle.Paragraphs.Count > 0;
        if (_mode is not null)
        {
            if (!Equals(_mode.SelectedValue, Mode))
            {
                _syncing = true;
                try
                {
                    _mode.SelectedValue = Mode;
                }
                finally
                {
                    _syncing = false;
                }
            }
            _mode.IsEnabled = hasText;
        }
        if (_length is not null)
        {
            if (Math.Abs(_length.Value - Length) > 0.01)
            {
                _length.Value = Length;
            }
            _length.IsEnabled = hasText && Mode != TldrMode.Full;
            // A new delegate makes the slider format its value again ("~60 words" or "4 points").
            _length.ValueFormatter = value => Labels.LengthValue(Mode == TldrMode.Full ? TldrMode.Summary : Mode, (int)Math.Round(value));
        }
    }

    private void OnModeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _mode?.SelectedValue is not TldrMode mode)
        {
            return;
        }
        SetMode(mode);
    }

    private void OnLengthCommitted(object? sender, SliderValueCommittedEventArgs e)
    {
        var length = Math.Clamp((int)Math.Round(e.Value), MinLength, MaxLength);
        if (length == Length)
        {
            return;
        }
        SetCurrentValue(LengthProperty, length);
        RaiseEvent(new TldrLengthChangedEventArgs(LengthChangedEvent, length));
    }

    /// <summary>Changes the mode from inside the card and raises <see cref="ModeChanged"/>.</summary>
    private void SetMode(TldrMode mode)
    {
        if (Mode == mode)
        {
            return;
        }
        SetCurrentValue(ModeProperty, mode);
        RaiseEvent(new TldrModeChangedEventArgs(ModeChangedEvent, mode));
    }

    /// <summary>The header: the source line (mark or monogram, name, date, reading time), the title and the author.</summary>
    private void BuildHeader()
    {
        var article = DisplayedArticle;
        if (_title is not null)
        {
            _title.Text = article.Title;
        }
        AutomationProperties.SetName(this, article.Title);
        if (_meta is not null)
        {
            var parts = new[] { article.SourceName, article.Date, article.Paragraphs.Count > 0 ? ReadingTimeText : null }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            _meta.Text = string.Join(" · ", parts);
        }
        var hasMark = article.SourceMark is not null;
        var hasSource = !string.IsNullOrWhiteSpace(article.SourceName);
        if (_sourceMark is not null)
        {
            _sourceMark.Data = article.SourceMark;
            _sourceMark.IsVisible = hasMark;
        }
        if (_monogram is not null)
        {
            _monogram.IsVisible = !hasMark && hasSource;
        }
        if (_monogramText is not null)
        {
            _monogramText.Text = hasSource ? article.SourceName!.Trim()[..1].ToUpperInvariant() : null;
        }
        var hasAuthor = !string.IsNullOrWhiteSpace(article.AuthorName);
        if (_authorRow is not null)
        {
            _authorRow.IsVisible = hasAuthor;
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = article.AuthorName;
            _avatar.Source = article.AuthorAvatar;
        }
        if (_author is not null)
        {
            _author.Text = article.AuthorName;
        }
    }

    /// <summary>Starts watching for the card to be half in view for a second (autoplay).</summary>
    private void WatchAutoPlay()
    {
        if (_viewport is not null || _autoPlayed || _touched || !EffectiveAutoPlay || Mode != TldrMode.Full)
        {
            return;
        }
        _viewport = new ViewportVisibility(this, 0.5, once: false);
        _viewport.VisibilityChanged += OnViewportVisibility;
    }

    private void StopAutoPlay()
    {
        _autoPlayTimer?.Stop();
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibility;
            _viewport.Dispose();
            _viewport = null;
        }
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        if (!visible)
        {
            _autoPlayTimer?.Stop();
            return;
        }
        if (_autoPlayTimer is null)
        {
            _autoPlayTimer = new DispatcherTimer { Interval = AutoPlayDelay };
            _autoPlayTimer.Tick += (_, _) =>
            {
                _autoPlayTimer.Stop();
                PlayNow();
            };
        }
        _autoPlayTimer.Stop();
        _autoPlayTimer.Start();
    }

    /// <summary>Autoplay's moment: opens the summary unless the reader got there first.</summary>
    internal void PlayNow()
    {
        if (_autoPlayed || _touched || !EffectiveAutoPlay || Mode != TldrMode.Full || DisplayedArticle.Paragraphs.Count == 0)
        {
            StopAutoPlay();
            return;
        }
        _autoPlayed = true;
        StopAutoPlay();
        SetMode(TldrMode.Summary);
    }

    /// <summary>Whether the reader has pressed or typed inside the card.</summary>
    internal bool IsTouched => _touched;
}

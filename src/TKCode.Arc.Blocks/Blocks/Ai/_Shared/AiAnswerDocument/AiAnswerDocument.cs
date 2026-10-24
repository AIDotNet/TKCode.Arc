using System.Collections.Specialized;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiAnswerDocument.CitationActivated"/>: the citation the person points at or focuses, or none.</summary>
internal sealed class AiAnswerLightEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiAnswerLightEventArgs(RoutedEvent routedEvent, object source, int sourceNumber)
        : base(routedEvent, source)
    {
        SourceNumber = sourceNumber;
    }

    /// <summary>The number of the active citation's source; 0 when no citation is active any more.</summary>
    public int SourceNumber { get; }
}

/// <summary>
/// The text of an AI answer, written like an article: paragraphs, short headings, ordered and bullet lists with bold
/// lead sentences, inline code, a comparison table in a rounded region, fenced code with its file name and a copy
/// button, and numbered citations as small pills. Streamed Markdown is shown in whole words on a fixed clock, each batch
/// fading in; headings, table rows and code lines appear when their whole line has arrived. Citations, the sentences
/// they support and the source cards light together (see <see cref="CitationActivated"/> and <see cref="LitSourceNumber"/>).
/// </summary>
/// <remarks>
/// <para>
/// A renderer of its own for the answer's Markdown subset (<see cref="AiAnswerMarkdown"/>) instead of StreamMark: lighting
/// a sentence needs its per-line rectangles and a background under the glyphs, which StreamMark's views do not expose
/// (they draw their own text and keep their layouts internal); its code block header, bold weight and inline code chip
/// are fixed too (see the M4 reply text notes). Each text is an <see cref="AiAnswerText"/> (a
/// <see cref="DecoratedTextBlock"/>), so sentence backgrounds come from Core's <see cref="TextRangeRenderer"/> and never
/// relayout, and citations are real controls embedded in the text (focus, pointer, accessible names).
/// </para>
/// <para>
/// Pacing: <see cref="Push"/> feeds a Core <see cref="WordPacer"/>; a <see cref="LiveLoop"/> (all motion levels, only
/// while text is pending) releases at most <see cref="WordsPerStep"/> words every <see cref="RevealInterval"/>, and each
/// release parses the text so far again (the blocks that did not change keep their views) and shows the difference.
/// </para>
/// </remarks>
[TemplatePart(BodyPart, typeof(Panel))]
[PseudoClasses(RevealingPseudoClass)]
internal partial class AiAnswerDocument : TemplatedControl
{
    public const string BodyPart = "PART_Body";
    public const string RevealingPseudoClass = ":revealing";

    /// <summary>Share of the accent in a lit sentence's background (Arc: accent at 12%).</summary>
    internal const double SentenceLightAlpha = 0.12;

    /// <summary>Share of the muted surface in a code block's fill; the rest is the page background (Arc 55 / 45).</summary>
    internal const double CodeSurfaceShare = 0.55;

    /// <summary>Opacity of the muted surface under a table's header row (Arc 60%).</summary>
    internal const double TableHeaderAlpha = 0.6;

    #region Properties

    /// <summary>The sources: <c>[n]</c> cites the source whose <see cref="AiSource.Index"/> is n, else the n-th; without one it is a plain number.</summary>
    public static readonly StyledProperty<IList<AiSource>?> SourcesProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IList<AiSource>?>(nameof(Sources));

    /// <summary>The source whose card is active: every sentence citing it and its pills are lit; 0 lights nothing.</summary>
    public static readonly StyledProperty<int> LitSourceNumberProperty =
        AvaloniaProperty.Register<AiAnswerDocument, int>(nameof(LitSourceNumber));

    /// <summary>How often streamed text is released (50 ms: every third frame at 60 Hz).</summary>
    public static readonly StyledProperty<TimeSpan> RevealIntervalProperty =
        AvaloniaProperty.Register<AiAnswerDocument, TimeSpan>(nameof(RevealInterval), TimeSpan.FromMilliseconds(50));

    /// <summary>How many whole words each release shows (4: about 80 words a second); more only while catching up.</summary>
    public static readonly StyledProperty<int> WordsPerStepProperty =
        AvaloniaProperty.Register<AiAnswerDocument, int>(nameof(WordsPerStep), 4);

    /// <summary>The accessible name of a citation from its number, the source's title and domain (the owner passes its labels).</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> CitationNameFormatProperty =
        AvaloniaProperty.Register<AiAnswerDocument, Func<int, string, string?, string>?>(nameof(CitationNameFormat));

    /// <summary>The accessible name of a citation when there is no <see cref="CitationNameFormat"/>: this word, the number and the title.</summary>
    public static readonly StyledProperty<string?> CitationNameProperty =
        AvaloniaProperty.Register<AiAnswerDocument, string?>(nameof(CitationName));

    /// <summary>The accessible name of a table region ("Comparison table").</summary>
    public static readonly StyledProperty<string?> TableNameProperty =
        AvaloniaProperty.Register<AiAnswerDocument, string?>(nameof(TableName));

    /// <summary>The accessible name of a code area from its file name or language ("Code: checks.yml").</summary>
    public static readonly StyledProperty<Func<string, string>?> CodeNameProperty =
        AvaloniaProperty.Register<AiAnswerDocument, Func<string, string>?>(nameof(CodeName));

    /// <summary>The copy button of a code block ("Copy").</summary>
    public static readonly StyledProperty<string?> CopyCodeLabelProperty =
        AvaloniaProperty.Register<AiAnswerDocument, string?>(nameof(CopyCodeLabel));

    /// <summary>What the copy button shows after copying ("Copied").</summary>
    public static readonly StyledProperty<string?> CopiedCodeLabelProperty =
        AvaloniaProperty.Register<AiAnswerDocument, string?>(nameof(CopiedCodeLabel));

    /// <summary>What the copy button shows when copying failed ("Failed").</summary>
    public static readonly StyledProperty<string?> CopyFailedCodeLabelProperty =
        AvaloniaProperty.Register<AiAnswerDocument, string?>(nameof(CopyFailedCodeLabel));

    /// <summary>Whether text is still arriving or being shown (read-only).</summary>
    public static readonly DirectProperty<AiAnswerDocument, bool> IsRevealingProperty =
        AvaloniaProperty.RegisterDirect<AiAnswerDocument, bool>(nameof(IsRevealing), o => o.IsRevealing);

    /// <summary>The accent a lit sentence is tinted with (theme: Accent; drawn at 12%).</summary>
    public static readonly StyledProperty<IBrush?> LightBrushProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IBrush?>(nameof(LightBrush));

    /// <summary>The page background (theme: Background): the code fill's other part and the light / dark test.</summary>
    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IBrush?>(nameof(PageBrush));

    /// <summary>The muted surface (theme: SurfaceMuted): inline code, table header, code fill.</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IBrush?>(nameof(MutedBrush));

    /// <summary>The subtle border (theme: BorderSubtle): the hairline in light themes.</summary>
    public static readonly StyledProperty<IBrush?> SubtleBorderBrushProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IBrush?>(nameof(SubtleBorderBrush));

    /// <summary>The stream cursor's color (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> CursorBrushProperty =
        AvaloniaProperty.Register<AiAnswerDocument, IBrush?>(nameof(CursorBrush));

    /// <inheritdoc cref="SourcesProperty"/>
    public IList<AiSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="LitSourceNumberProperty"/>
    public int LitSourceNumber { get => GetValue(LitSourceNumberProperty); set => SetValue(LitSourceNumberProperty, value); }

    /// <inheritdoc cref="RevealIntervalProperty"/>
    public TimeSpan RevealInterval { get => GetValue(RevealIntervalProperty); set => SetValue(RevealIntervalProperty, value); }

    /// <inheritdoc cref="WordsPerStepProperty"/>
    public int WordsPerStep { get => GetValue(WordsPerStepProperty); set => SetValue(WordsPerStepProperty, value); }

    /// <inheritdoc cref="CitationNameFormatProperty"/>
    public Func<int, string, string?, string>? CitationNameFormat { get => GetValue(CitationNameFormatProperty); set => SetValue(CitationNameFormatProperty, value); }

    /// <inheritdoc cref="CitationNameProperty"/>
    public string? CitationName { get => GetValue(CitationNameProperty); set => SetValue(CitationNameProperty, value); }

    /// <inheritdoc cref="TableNameProperty"/>
    public string? TableName { get => GetValue(TableNameProperty); set => SetValue(TableNameProperty, value); }

    /// <inheritdoc cref="CodeNameProperty"/>
    public Func<string, string>? CodeName { get => GetValue(CodeNameProperty); set => SetValue(CodeNameProperty, value); }

    /// <inheritdoc cref="CopyCodeLabelProperty"/>
    public string? CopyCodeLabel { get => GetValue(CopyCodeLabelProperty); set => SetValue(CopyCodeLabelProperty, value); }

    /// <inheritdoc cref="CopiedCodeLabelProperty"/>
    public string? CopiedCodeLabel { get => GetValue(CopiedCodeLabelProperty); set => SetValue(CopiedCodeLabelProperty, value); }

    /// <inheritdoc cref="CopyFailedCodeLabelProperty"/>
    public string? CopyFailedCodeLabel { get => GetValue(CopyFailedCodeLabelProperty); set => SetValue(CopyFailedCodeLabelProperty, value); }

    /// <inheritdoc cref="IsRevealingProperty"/>
    public bool IsRevealing { get => _isRevealing; private set => SetAndRaise(IsRevealingProperty, ref _isRevealing, value); }

    /// <inheritdoc cref="LightBrushProperty"/>
    public IBrush? LightBrush { get => GetValue(LightBrushProperty); set => SetValue(LightBrushProperty, value); }

    /// <inheritdoc cref="PageBrushProperty"/>
    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    /// <inheritdoc cref="MutedBrushProperty"/>
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <inheritdoc cref="SubtleBorderBrushProperty"/>
    public IBrush? SubtleBorderBrush { get => GetValue(SubtleBorderBrushProperty); set => SetValue(SubtleBorderBrushProperty, value); }

    /// <inheritdoc cref="CursorBrushProperty"/>
    public IBrush? CursorBrush { get => GetValue(CursorBrushProperty); set => SetValue(CursorBrushProperty, value); }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the active citation changes: the pointer enters a citation or it takes focus (its source number), or
    /// neither any more (0). The owner lights the source card with that number.
    /// </summary>
    public static readonly RoutedEvent<AiAnswerLightEventArgs> CitationActivatedEvent =
        RoutedEvent.Register<AiAnswerDocument, AiAnswerLightEventArgs>(nameof(CitationActivated), RoutingStrategies.Bubble);

    /// <summary>Raised when a citation with a source is pressed (pointer or Enter); <c>FromCitation</c> is true. The document opens nothing itself.</summary>
    public static readonly RoutedEvent<AiAnswerSourceEventArgs> CitationInvokedEvent =
        RoutedEvent.Register<AiAnswerDocument, AiAnswerSourceEventArgs>(nameof(CitationInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when everything after <see cref="Complete"/> has been shown (not after <see cref="Stop"/> or <see cref="SetMarkdown"/>).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RevealCompletedEvent =
        RoutedEvent.Register<AiAnswerDocument, RoutedEventArgs>(nameof(RevealCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised after a code block's copy button put its code on the clipboard (<see cref="AiAnswerCopyKind.Code"/>).</summary>
    public static readonly RoutedEvent<AiAnswerCopiedEventArgs> CodeCopiedEvent =
        RoutedEvent.Register<AiAnswerDocument, AiAnswerCopiedEventArgs>(nameof(CodeCopied), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CitationActivatedEvent"/>
    public event EventHandler<AiAnswerLightEventArgs>? CitationActivated { add => AddHandler(CitationActivatedEvent, value); remove => RemoveHandler(CitationActivatedEvent, value); }

    /// <inheritdoc cref="CitationInvokedEvent"/>
    public event EventHandler<AiAnswerSourceEventArgs>? CitationInvoked { add => AddHandler(CitationInvokedEvent, value); remove => RemoveHandler(CitationInvokedEvent, value); }

    /// <inheritdoc cref="RevealCompletedEvent"/>
    public event EventHandler<RoutedEventArgs>? RevealCompleted { add => AddHandler(RevealCompletedEvent, value); remove => RemoveHandler(RevealCompletedEvent, value); }

    /// <inheritdoc cref="CodeCopiedEvent"/>
    public event EventHandler<AiAnswerCopiedEventArgs>? CodeCopied { add => AddHandler(CodeCopiedEvent, value); remove => RemoveHandler(CodeCopiedEvent, value); }

    #endregion

    private readonly StringBuilder _source = new();
    private readonly LiveLoop _loop;
    private readonly LiveLoop _breath;
    private WordPacer _pacer;
    private Panel? _body;
    private Phase _phase;
    private bool _final;
    private bool _finalShown;
    private TimeSpan _clock;
    private TimeSpan _lastAppend;
    private int _releases;
    private bool _isRevealing;
    private INotifyCollectionChanged? _sourcesHook;
    private double _breathTime;
    private AiAnswerText? _cursorText;

    public AiAnswerDocument()
    {
        _pacer = CreatePacer();
        _loop = new LiveLoop(this, Step)
        {
            // Text keeps its cadence at every motion level (only the fades go) and while scrolled away.
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        _breath = new LiveLoop(this, Breathe);
        _breath.RunningChanged += running =>
        {
            if (!running)
            {
                // A still cursor at reduced motion and off (Arc: no breathing).
                CursorOpacity = 1;
                _cursorText?.InvalidateVisual();
            }
        };
    }

    /// <summary>The Markdown released so far (after <see cref="SetMarkdown"/>: the whole answer).</summary>
    internal string RevealedMarkdown => _source.ToString();

    /// <summary>How many batches the clock has released since the last <see cref="Reset"/> (tests).</summary>
    internal int Releases => _releases;

    /// <summary>The pacer of the current answer (tests).</summary>
    internal WordPacer Pacer => _pacer;

    /// <summary>The body panel the blocks are shown in.</summary>
    internal Panel? Body => _body;

    /// <summary>The opacity of the breathing cursor (1 → 0.35 → 1 over 1.2 s at full motion).</summary>
    internal double CursorOpacity { get; private set; } = 1;

    /// <summary>The text that shows the cursor, or null.</summary>
    internal AiAnswerText? CursorText => _cursorText;

    /// <summary>The fill of a lit sentence: the accent at 12%.</summary>
    internal IBrush? SentenceLightBrush { get; private set; }

    /// <summary>
    /// The hairline of inline code, the table's row lines and the code header's line: BorderSubtle in light themes,
    /// Border in dark ones (Arc's two themes map it to different tokens).
    /// </summary>
    internal IBrush? HairlineBrush { get; private set; }

    /// <summary>The fill of a code block: 55% muted surface, 45% page background.</summary>
    internal IBrush? CodeSurfaceBrush { get; private set; }

    /// <summary>Whether the theme is dark (the page background is dark).</summary>
    internal bool IsDarkTheme { get; private set; }

    #region Public API

    /// <summary>Starts a new answer: clears the text, the pacer, the stream and the citations.</summary>
    public void Reset()
    {
        _loop.Stop();
        _pacer = CreatePacer();
        _source.Clear();
        _phase = Phase.Idle;
        _final = false;
        _finalShown = false;
        _releases = 0;
        _lastAppend = _clock;
        ClearBlocks();
        SetRevealing(false);
    }

    /// <summary>Adds streamed Markdown of any size (UI thread). Ignored once the answer is completed, stopped or set whole.</summary>
    public void Push(string markdownDelta)
    {
        if (string.IsNullOrEmpty(markdownDelta) || _phase is Phase.Draining or Phase.Finished)
        {
            return;
        }
        if (_phase == Phase.Idle)
        {
            _phase = Phase.Streaming;
            SetRevealing(true);
        }
        _pacer.Push(markdownDelta);
        _loop.Start();
    }

    /// <summary>No more text: the rest is shown within the pacer's drain (600 ms), then <see cref="RevealCompleted"/> is raised.</summary>
    public void Complete()
    {
        if (_phase is Phase.Draining or Phase.Finished)
        {
            return;
        }
        _phase = Phase.Draining;
        SetRevealing(true);
        _pacer.Complete();
        _loop.Start();
    }

    /// <summary>
    /// Drops the text the clock has not released yet, keeps what it released (the answer stops mid-sentence) and ends
    /// the stream; no <see cref="RevealCompleted"/>. A line held back until its end (a heading, a table row, code) shows
    /// as far as it arrived.
    /// </summary>
    public void Stop()
    {
        if (_phase is Phase.Finished)
        {
            return;
        }
        _loop.Stop();
        _pacer.Reset();
        _phase = Phase.Finished;
        _final = true;
        Rebuild(fade: false);
        SetRevealing(false);
    }

    /// <summary>Shows a finished answer at once: no pacing, no fade, no cursor.</summary>
    public void SetMarkdown(string markdown)
    {
        _loop.Stop();
        _pacer = CreatePacer();
        _source.Clear().Append(markdown ?? string.Empty);
        _phase = Phase.Finished;
        _final = true;
        _finalShown = true;
        ClearBlocks();
        Rebuild(fade: false);
        SetRevealing(false);
    }

    /// <summary>
    /// The answer as plain text for the clipboard: blocks separated by blank lines, list items numbered or dashed, table
    /// rows with cells between " | ", code as written, citations as "[n]".
    /// </summary>
    public string PlainText => BuildPlainText(_blocks);

    #endregion

    #region Pacing

    private WordPacer CreatePacer()
    {
        // Arc releases a fixed number of words per clock step. As a WordPacer that is WordsPerStep words per interval
        // with the batch capped at WordsPerStep. The interval is a minimum checked on frames: 2 ms of slack keeps 50 ms
        // on the third 60 Hz frame (3 × 16.67 ms) instead of rounding to the fourth, and the speed is taken over the
        // shortened interval so every step (the first one too) holds the full WordsPerStep.
        var interval = RevealInterval < TimeSpan.FromMilliseconds(4) ? TimeSpan.FromMilliseconds(4) : RevealInterval;
        var minimum = interval - TimeSpan.FromMilliseconds(2);
        var words = Math.Max(1, WordsPerStep);
        return new WordPacer(new WordPacerOptions
        {
            MinInterval = minimum,
            WordsPerSecond = words / minimum.TotalSeconds,
            MaxWordsPerBatch = words
        });
    }

    private bool Step(double dt)
    {
        _clock += TimeSpan.FromSeconds(dt);
        if (_pacer.Tick(_clock) is { } batch)
        {
            _source.Append(batch);
            _releases++;
            Rebuild(FadesIn());
            _lastAppend = _clock;
            return true;
        }
        if (_phase == Phase.Draining && _pacer.IsFinished)
        {
            if (!_finalShown)
            {
                // The end of the stream: a last line held back (a heading, a row) and open markup settle.
                _finalShown = true;
                _final = true;
                Rebuild(FadesIn());
                return true;
            }
            if (_releases > 0 && _clock - _lastAppend < RevealInterval)
            {
                // The finished state follows one step after the last batch.
                return true;
            }
            Finish();
            return false;
        }
        // Nothing can be released until more text arrives: sleep until the next Push.
        return _pacer.PendingWords > 0;
    }

    private bool FadesIn() => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private void Finish()
    {
        _phase = Phase.Finished;
        SetRevealing(false);
        RaiseEvent(new RoutedEventArgs(RevealCompletedEvent, this));
    }

    private void SetRevealing(bool value)
    {
        IsRevealing = value;
        PseudoClasses.Set(RevealingPseudoClass, value);
        UpdateCursor();
    }

    #endregion

    #region Cursor

    /// <summary>The breathing cursor sits after the last text while the answer is being written (not in code or a table row).</summary>
    private void UpdateCursor()
    {
        var target = IsRevealing && _phase != Phase.Finished ? LastText() : null;
        if (!ReferenceEquals(target, _cursorText))
        {
            if (_cursorText is not null)
            {
                _cursorText.ShowsCursor = false;
            }
            _cursorText = target;
            if (target is not null)
            {
                target.ShowsCursor = true;
            }
        }
        if (_cursorText is not null)
        {
            _breath.Start();
        }
        else
        {
            _breath.Stop();
            _breathTime = 0;
            CursorOpacity = 1;
        }
    }

    private bool Breathe(double dt)
    {
        // Arc's "breathe": opacity 1 → 0.35 → 1 over 1.2 s, ease-in-out (a cosine).
        _breathTime = (_breathTime + dt) % 1.2;
        CursorOpacity = 0.675 + 0.325 * Math.Cos(_breathTime / 1.2 * 2 * Math.PI);
        _cursorText?.InvalidateVisual();
        return _cursorText is not null;
    }

    #endregion

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _body?.Children.Clear();
        _body = e.NameScope.Find<Panel>(BodyPart);
        // The views move to the new body: build them again from the text so far.
        ClearBlocks();
        Rebuild(fade: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            HookSources(this.IsAttachedToVisualTree() ? change.GetNewValue<IList<AiSource>?>() : null);
            OnSourcesChanged();
        }
        else if (change.Property == LitSourceNumberProperty)
        {
            UpdateLighting();
        }
        else if (change.Property == RevealIntervalProperty || change.Property == WordsPerStepProperty)
        {
            // An answer keeps its cadence; one that has not started takes the new one.
            if (_phase == Phase.Idle && !_pacer.HasPending)
            {
                _pacer = CreatePacer();
            }
        }
        else if (change.Property == CitationNameFormatProperty || change.Property == CitationNameProperty)
        {
            foreach (var pill in AllCitations())
            {
                UpdateCitation(pill);
            }
        }
        else if (change.Property == TableNameProperty || change.Property == CodeNameProperty || change.Property == CopyCodeLabelProperty
                 || change.Property == CopiedCodeLabelProperty || change.Property == CopyFailedCodeLabelProperty)
        {
            RefreshLabels();
        }
        else if (change.Property == LightBrushProperty || change.Property == PageBrushProperty || change.Property == MutedBrushProperty
                 || change.Property == SubtleBorderBrushProperty || change.Property == BorderBrushProperty)
        {
            UpdateBrushes();
        }
        else if (change.Property == CursorBrushProperty)
        {
            _cursorText?.InvalidateVisual();
        }
    }

    /// <summary>The colors derived from the theme's: the 12% light, the hairline of the theme's kind, the code fill.</summary>
    private void UpdateBrushes()
    {
        var page = (PageBrush as ISolidColorBrush)?.Color ?? Colors.White;
        IsDarkTheme = Oklch.FromColor(page).L < 0.5;
        SentenceLightBrush = LightBrush is ISolidColorBrush light
            ? new ImmutableSolidColorBrush(light.Color, SentenceLightAlpha * light.Opacity)
            : null;
        HairlineBrush = IsDarkTheme ? BorderBrush : SubtleBorderBrush;
        CodeSurfaceBrush = MutedBrush is ISolidColorBrush muted
            ? new ImmutableSolidColorBrush(Oklch.MixOklab(page, muted.Color, CodeSurfaceShare))
            : null;
        foreach (var text in AllTexts())
        {
            text.RefreshBrushes();
        }
        foreach (var view in _views)
        {
            if (view.Control is AiAnswerTable table)
            {
                table.ApplyBrushes(this);
            }
            else if (view.Control is AiAnswerCodeBlock code)
            {
                code.ApplyBrushes(this);
            }
        }
    }

    /// <summary>The sources are app data that can outlive the answer (a kept version holds them): followed only while it is in a window.</summary>
    private void HookSources(IList<AiSource>? sources)
    {
        var next = sources as INotifyCollectionChanged;
        if (ReferenceEquals(next, _sourcesHook))
        {
            return;
        }
        if (_sourcesHook is not null)
        {
            _sourcesHook.CollectionChanged -= OnSourcesCollectionChanged;
        }
        _sourcesHook = next;
        if (_sourcesHook is not null)
        {
            _sourcesHook.CollectionChanged += OnSourcesCollectionChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        HookSources(Sources);
        // The list may have changed while the answer was away.
        OnSourcesChanged();
    }

    private void OnSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSourcesChanged();

    private void OnSourcesChanged()
    {
        foreach (var pill in AllCitations())
        {
            UpdateCitation(pill);
        }
        RefreshCard();
        UpdateLighting();
    }

    /// <summary>
    /// The source cited as <paramref name="number"/>: the one whose <see cref="AiSource.Index"/> is n, else the n-th when
    /// that one was never numbered (its index is still the default 1); null when there is none.
    /// </summary>
    internal AiSource? FindSource(int number)
    {
        if (number < 1 || Sources is not { } sources)
        {
            return null;
        }
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is { } source && source.Index == number)
            {
                return source;
            }
        }
        return number <= sources.Count && sources[number - 1] is { Index: 1 } unnumbered ? unnumbered : null;
    }

    #endregion

    private enum Phase
    {
        /// <summary>Nothing pushed since the last reset.</summary>
        Idle,

        /// <summary>Text is arriving.</summary>
        Streaming,

        /// <summary>Completed; the backlog is being shown.</summary>
        Draining,

        /// <summary>Everything shown, stopped or set whole.</summary>
        Finished
    }
}

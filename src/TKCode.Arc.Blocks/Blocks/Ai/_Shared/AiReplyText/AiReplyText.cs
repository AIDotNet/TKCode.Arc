using System.Collections.Specialized;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiReplyText.CitationInvoked"/>: the person pressed a citation.</summary>
internal sealed class AiReplyTextSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiReplyTextSourceEventArgs(RoutedEvent routedEvent, object source, AiSource item, int number)
        : base(routedEvent, source)
    {
        Item = item;
        Number = number;
    }

    /// <summary>The cited source.</summary>
    public AiSource Item { get; }

    /// <summary>The number the text cites it with.</summary>
    public int Number { get; }
}

/// <summary>
/// The text of one AI reply: Markdown that arrives in pieces of any size and is shown in whole words on a fixed clock,
/// each batch fading in, with numbered citations (<c>[1]</c>) drawn as small pills that preview their source.
/// </summary>
/// <remarks>
/// <para>
/// Pacing: <see cref="Push"/> feeds a Core <see cref="WordPacer"/>; a <see cref="LiveLoop"/> (all motion levels, only while
/// text is pending) ticks it every frame and appends each released batch to an unpaced <see cref="Markdown"/>, which fades
/// it in (150 ms at full motion, at once otherwise). The pacer releases at most <see cref="WordsPerStep"/> words every
/// <see cref="RevealInterval"/> (its reading speed is that ratio), so a 60 Hz display shows 3 words every third frame like
/// Arc; a backlog is caught up and <see cref="Complete"/> drains the rest within 600 ms.
/// </para>
/// <para>
/// Citations: each released batch goes through <see cref="AiCitationText"/> (after pacing, because the tag has spaces the
/// pacer would split), which turns <c>[n]</c> into <c>&lt;Citation id="n" key="k" /&gt;</c> outside code. This control
/// registers its own renderer for that component, so the pill is Arc's (18 high, pill radius, 11px mono number, muted
/// fill; inverted while hovered or previewed) instead of StreamMark's chip, and hosts its own preview card in an
/// <see cref="ArcPopup"/>. StreamMark's tail fade cannot fade an embedded object and shows everything before one at once,
/// so a batch is split at each citation: the pill is appended only when nothing above it is still fading, as the last
/// thing of its batch, and fades itself in.
/// </para>
/// <para>
/// Code-driven motion (<see cref="MotionValue{T}"/>): the pill is painted inside StreamMark's text layout, which has no
/// properties a ControlTheme could animate, so its fade-in and its 160 ms color inversion are values that repaint the line.
/// </para>
/// </remarks>
[TemplatePart(MarkdownPart, typeof(Markdown))]
[TemplatePart(TargetsPart, typeof(Canvas))]
[PseudoClasses(RevealingPseudoClass)]
internal partial class AiReplyText : TemplatedControl
{
    public const string MarkdownPart = "PART_Markdown";
    public const string TargetsPart = "PART_Citations";
    public const string RevealingPseudoClass = ":revealing";

    #region Properties

    /// <summary>The sources the citations point at: <c>[n]</c> previews the source whose <see cref="AiSource.Index"/> is n; without one it is a plain number.</summary>
    public static readonly StyledProperty<IList<AiSource>?> SourcesProperty =
        AvaloniaProperty.Register<AiReplyText, IList<AiSource>?>(nameof(Sources));

    /// <summary>The shortest time between two steps of streamed text (34 ms; every third frame at 60 Hz).</summary>
    public static readonly StyledProperty<TimeSpan> RevealIntervalProperty =
        AvaloniaProperty.Register<AiReplyText, TimeSpan>(nameof(RevealInterval), TimeSpan.FromMilliseconds(34));

    /// <summary>How many whole words a step shows at most (3); more only while catching up or draining.</summary>
    public static readonly StyledProperty<int> WordsPerStepProperty =
        AvaloniaProperty.Register<AiReplyText, int>(nameof(WordsPerStep), 3);

    /// <summary>The accessible name of a citation from its number, title and domain ("Source 1: Title, domain").</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> CitationNameProperty =
        AvaloniaProperty.Register<AiReplyText, Func<int, string, string?, string>?>(nameof(CitationName));

    /// <summary>Whether text is still arriving or being shown (read-only).</summary>
    public static readonly DirectProperty<AiReplyText, bool> IsRevealingProperty =
        AvaloniaProperty.RegisterDirect<AiReplyText, bool>(nameof(IsRevealing), o => o.IsRevealing);

    /// <summary>Fill of a pill at rest (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> CitationBackgroundProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CitationBackground));

    /// <summary>Number of a pill at rest and of a plain citation (theme: TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> CitationForegroundProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CitationForeground));

    /// <summary>Fill of a hovered or previewed pill (theme: Foreground).</summary>
    public static readonly StyledProperty<IBrush?> CitationActiveBackgroundProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CitationActiveBackground));

    /// <summary>Number of a hovered or previewed pill (theme: Background).</summary>
    public static readonly StyledProperty<IBrush?> CitationActiveForegroundProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CitationActiveForeground));

    /// <summary>Keyboard focus ring of a pill (theme: FocusRing).</summary>
    public static readonly StyledProperty<IBrush?> CitationFocusBrushProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CitationFocusBrush));

    /// <summary>Font of the pill numbers (theme: MonoFontFamily).</summary>
    public static readonly StyledProperty<FontFamily?> CitationFontFamilyProperty =
        AvaloniaProperty.Register<AiReplyText, FontFamily?>(nameof(CitationFontFamily));

    /// <summary>Code block fill, mixed 70 / 30 with <see cref="CodeSurfaceProperty"/> (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> CodeSurfaceMutedProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CodeSurfaceMuted));

    /// <summary>Code block fill, the 30 % part (theme: Surface).</summary>
    public static readonly StyledProperty<IBrush?> CodeSurfaceProperty =
        AvaloniaProperty.Register<AiReplyText, IBrush?>(nameof(CodeSurface));

    /// <summary>Corner radius of code blocks and tables (theme: 16).</summary>
    public static readonly StyledProperty<CornerRadius> CodeCornerRadiusProperty =
        AvaloniaProperty.Register<AiReplyText, CornerRadius>(nameof(CodeCornerRadius), new CornerRadius(16));

    /// <inheritdoc cref="SourcesProperty"/>
    public IList<AiSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="RevealIntervalProperty"/>
    public TimeSpan RevealInterval { get => GetValue(RevealIntervalProperty); set => SetValue(RevealIntervalProperty, value); }

    /// <inheritdoc cref="WordsPerStepProperty"/>
    public int WordsPerStep { get => GetValue(WordsPerStepProperty); set => SetValue(WordsPerStepProperty, value); }

    /// <inheritdoc cref="CitationNameProperty"/>
    public Func<int, string, string?, string>? CitationName { get => GetValue(CitationNameProperty); set => SetValue(CitationNameProperty, value); }

    /// <inheritdoc cref="IsRevealingProperty"/>
    public bool IsRevealing { get => _isRevealing; private set => SetAndRaise(IsRevealingProperty, ref _isRevealing, value); }

    /// <inheritdoc cref="CitationBackgroundProperty"/>
    public IBrush? CitationBackground { get => GetValue(CitationBackgroundProperty); set => SetValue(CitationBackgroundProperty, value); }

    /// <inheritdoc cref="CitationForegroundProperty"/>
    public IBrush? CitationForeground { get => GetValue(CitationForegroundProperty); set => SetValue(CitationForegroundProperty, value); }

    /// <inheritdoc cref="CitationActiveBackgroundProperty"/>
    public IBrush? CitationActiveBackground { get => GetValue(CitationActiveBackgroundProperty); set => SetValue(CitationActiveBackgroundProperty, value); }

    /// <inheritdoc cref="CitationActiveForegroundProperty"/>
    public IBrush? CitationActiveForeground { get => GetValue(CitationActiveForegroundProperty); set => SetValue(CitationActiveForegroundProperty, value); }

    /// <inheritdoc cref="CitationFocusBrushProperty"/>
    public IBrush? CitationFocusBrush { get => GetValue(CitationFocusBrushProperty); set => SetValue(CitationFocusBrushProperty, value); }

    /// <inheritdoc cref="CitationFontFamilyProperty"/>
    public FontFamily? CitationFontFamily { get => GetValue(CitationFontFamilyProperty); set => SetValue(CitationFontFamilyProperty, value); }

    /// <inheritdoc cref="CodeSurfaceMutedProperty"/>
    public IBrush? CodeSurfaceMuted { get => GetValue(CodeSurfaceMutedProperty); set => SetValue(CodeSurfaceMutedProperty, value); }

    /// <inheritdoc cref="CodeSurfaceProperty"/>
    public IBrush? CodeSurface { get => GetValue(CodeSurfaceProperty); set => SetValue(CodeSurfaceProperty, value); }

    /// <inheritdoc cref="CodeCornerRadiusProperty"/>
    public CornerRadius CodeCornerRadius { get => GetValue(CodeCornerRadiusProperty); set => SetValue(CodeCornerRadiusProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when everything after <see cref="Complete"/> has been shown (not after <see cref="Stop"/> or <see cref="SetText"/>).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RevealCompletedEvent =
        RoutedEvent.Register<AiReplyText, RoutedEventArgs>(nameof(RevealCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses a citation that has a source (pointer, Enter or Space).</summary>
    public static readonly RoutedEvent<AiReplyTextSourceEventArgs> CitationInvokedEvent =
        RoutedEvent.Register<AiReplyText, AiReplyTextSourceEventArgs>(nameof(CitationInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RevealCompletedEvent"/>
    public event EventHandler<RoutedEventArgs>? RevealCompleted { add => AddHandler(RevealCompletedEvent, value); remove => RemoveHandler(RevealCompletedEvent, value); }

    /// <inheritdoc cref="CitationInvokedEvent"/>
    public event EventHandler<AiReplyTextSourceEventArgs>? CitationInvoked { add => AddHandler(CitationInvokedEvent, value); remove => RemoveHandler(CitationInvokedEvent, value); }

    #endregion

    /// <summary>Code blocks of a reply: copy only, never folded (Arc's chat shows them whole).</summary>
    private static readonly CodeBlockOptions s_codeBlocks = new() { ShowDownload = false, CollapseAfterLines = 0 };

    private static readonly MarkdownHighlightPlugin s_highlight = new();

    private readonly MarkdownServices _services;
    private readonly AiCitationText.Scanner _scanner = new() { EmitsKeys = true };
    private readonly Queue<Piece> _queue = new();
    private readonly List<string> _pieces = [];
    private readonly StringBuilder _shownText = new();
    private readonly StringBuilder _revealedSource = new();
    private readonly LiveLoop _loop;
    private WordPacer _pacer;
    private Markdown? _markdown;
    private Phase _phase;
    private TimeSpan _clock;
    private TimeSpan _lastAppend;
    private string? _staticText;
    private string? _staticSource;
    private bool _isRevealing;
    private INotifyCollectionChanged? _sourcesHook;

    public AiReplyText()
    {
        _services = new MarkdownServices
        {
            Plugins = [s_highlight, new AiCitationPlugin(this)],
            CodeBlocks = s_codeBlocks
        };
        _pacer = CreatePacer();
        _loop = new LiveLoop(this, Step)
        {
            // Text keeps its cadence at every motion level (only the fades go) and while scrolled away.
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        AddHandler(PointerMovedEvent, OnPointerMovedInside, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerPressedInside, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedInside, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(TextSegmentView.ComponentClickedEvent, OnComponentClicked);
    }

    /// <summary>Every piece appended to the Markdown so far, in order (tests).</summary>
    internal IReadOnlyList<string> AppendedPieces => _pieces;

    /// <summary>The Markdown part (tests).</summary>
    internal Markdown? Body => _markdown;

    /// <summary>The pacer of the current reply (tests).</summary>
    internal WordPacer Pacer => _pacer;

    /// <summary>Whether <see cref="Push"/> would be ignored until <see cref="Reset"/>.</summary>
    internal bool IsFinished => _phase == Phase.Finished;

    /// <summary>
    /// The pushed text the clock has released so far, as it was pushed (citations as <c>[n]</c>): after <see cref="Stop"/>
    /// exactly what the reply shows, after <see cref="SetText"/> the text set.
    /// </summary>
    internal string RevealedSource => _staticSource ?? _revealedSource.ToString();

    /// <summary>Whether any text has been shown since the last <see cref="Reset"/>.</summary>
    internal bool HasText => _staticText is { Length: > 0 } || _shownText.Length > 0;

    #region Public API

    /// <summary>Starts a new reply: clears the text, the pacer, the stream and the citations.</summary>
    public void Reset()
    {
        _loop.Stop();
        _pacer = CreatePacer();
        _scanner.Reset();
        _queue.Clear();
        _pieces.Clear();
        _shownText.Clear();
        _revealedSource.Clear();
        _staticText = null;
        _staticSource = null;
        _lastAppend = _clock;
        ClearCitations();
        _phase = Phase.Idle;
        SetRevealing(false);
        if (_markdown is { } markdown)
        {
            markdown.SetCurrentValue(Markdown.TextProperty, null);
            if (markdown.Stream is not null)
            {
                markdown.SetCurrentValue(Markdown.StreamProperty, null);
            }
        }
    }

    /// <summary>Adds streamed text of any size (UI thread). Ignored once the reply is completed, stopped or set whole.</summary>
    public void Push(string delta)
    {
        if (string.IsNullOrEmpty(delta) || _phase is Phase.Draining or Phase.Finished)
        {
            return;
        }
        if (_phase == Phase.Idle)
        {
            _phase = Phase.Streaming;
            SetRevealing(true);
        }
        _pacer.Push(delta);
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
    /// Drops the text the clock has not released yet, keeps what it released and ends the stream (no
    /// <see cref="RevealCompleted"/>). Words already released but still waiting for a fade (a citation held back, a
    /// partial tag) are shown at once, so the reply ends exactly at <see cref="RevealedSource"/>.
    /// </summary>
    public void Stop()
    {
        if (_phase is Phase.Finished)
        {
            return;
        }
        _loop.Stop();
        _pacer.Reset();
        if (_markdown is not null)
        {
            Enqueue(_scanner.Flush());
            var rest = new StringBuilder();
            while (_queue.Count > 0)
            {
                var piece = _queue.Dequeue();
                rest.Append(piece.Text);
                if (piece.Key >= 0)
                {
                    OnCitationShown(piece.Number, piece.Key, fade: false);
                }
            }
            AppendText(rest.ToString());
        }
        _queue.Clear();
        _scanner.Reset();
        _phase = Phase.Finished;
        SetRevealing(false);
        _markdown?.Complete();
    }

    /// <summary>Shows a finished reply at once: no pacing, no fade (history, switching conversations).</summary>
    public void SetText(string text)
    {
        _loop.Stop();
        _pacer = CreatePacer();
        _queue.Clear();
        _pieces.Clear();
        _shownText.Clear();
        _revealedSource.Clear();
        ClearCitations();
        _scanner.Reset();
        var converted = _scanner.Push(text ?? string.Empty) + _scanner.Flush();
        _staticText = converted;
        _staticSource = text ?? string.Empty;
        foreach (var (number, key) in _scanner.Citations)
        {
            OnCitationShown(number, key, fade: false);
        }
        _phase = Phase.Finished;
        SetRevealing(false);
        if (_markdown is { } markdown)
        {
            if (markdown.Stream is not null)
            {
                markdown.SetCurrentValue(Markdown.StreamProperty, null);
            }
            markdown.SetCurrentValue(Markdown.TextProperty, converted);
        }
    }

    #endregion

    #region Pacing

    private WordPacer CreatePacer()
    {
        // Arc releases a fixed number of words per clock step; as a WordPacer that is a speed of WordsPerStep per
        // RevealInterval with the batch capped at WordsPerStep, so a step shows exactly that many at any refresh rate.
        var interval = RevealInterval < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : RevealInterval;
        var words = Math.Max(1, WordsPerStep);
        return new WordPacer(new WordPacerOptions
        {
            MinInterval = interval,
            WordsPerSecond = words / interval.TotalSeconds,
            MaxWordsPerBatch = words
        });
    }

    private bool Step(double dt)
    {
        _clock += TimeSpan.FromSeconds(dt);
        if (_markdown is null)
        {
            return true;
        }
        if (_queue.Count > 0)
        {
            AppendQueued();
            return true;
        }
        if (_pacer.Tick(_clock) is { } batch)
        {
            _revealedSource.Append(batch);
            Enqueue(_scanner.Push(batch));
            AppendQueued();
            return true;
        }
        if (_phase == Phase.Draining && _pacer.IsFinished)
        {
            var tail = _scanner.Flush();
            if (tail.Length > 0)
            {
                Enqueue(tail);
                AppendQueued();
                return true;
            }
            if (_pieces.Count > 0 && _clock - _lastAppend < RevealInterval)
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

    private void Enqueue(string converted)
    {
        var from = 0;
        while (AiCitationText.FindKeyedTag(converted, from, out var start, out var length, out var number, out var key))
        {
            if (start > from)
            {
                _queue.Enqueue(new Piece(converted[from..start], 0, -1));
            }
            _queue.Enqueue(new Piece(converted.Substring(start, length), number, key));
            from = start + length;
        }
        if (from < converted.Length)
        {
            _queue.Enqueue(new Piece(converted[from..], 0, -1));
        }
    }

    /// <summary>
    /// Appends queued pieces. Without a fade the whole batch goes at once. With one, a piece per frame, and a citation only
    /// when nothing above it is still fading: StreamMark's tail fade shows everything before an embedded object at once,
    /// so a pill that arrived while earlier words were fading would cut their fade short.
    /// </summary>
    private void AppendQueued()
    {
        if (!FadesIn())
        {
            var joined = new StringBuilder();
            while (_queue.Count > 0)
            {
                var piece = _queue.Dequeue();
                joined.Append(piece.Text);
                if (piece.Key >= 0)
                {
                    OnCitationShown(piece.Number, piece.Key, fade: false);
                }
            }
            AppendText(joined.ToString());
            return;
        }
        var next = _queue.Peek();
        if (next.Key >= 0 && IsTailFading())
        {
            return;
        }
        _queue.Dequeue();
        AppendText(next.Text);
        if (next.Key >= 0)
        {
            OnCitationShown(next.Number, next.Key, fade: true);
        }
    }

    private void AppendText(string text)
    {
        if (text.Length == 0 || _markdown is null)
        {
            return;
        }
        _markdown.Append(text);
        _pieces.Add(text);
        _shownText.Append(text);
        _lastAppend = _clock;
    }

    private bool FadesIn() => _markdown?.Stream is { } stream
        ? stream.State.FadeDuration > TimeSpan.Zero
        : ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private TimeSpan FadeDuration() => _markdown?.Stream?.State.FadeDuration ?? TimeSpan.Zero;

    private bool IsTailFading() => _markdown?.Stream?.State is { } state && state.GetFadingTail(StreamClock.Now).Count > 0;

    private void Finish()
    {
        _phase = Phase.Finished;
        SetRevealing(false);
        _markdown?.Complete();
        RaiseEvent(new RoutedEventArgs(RevealCompletedEvent, this));
    }

    private void SetRevealing(bool value)
    {
        IsRevealing = value;
        PseudoClasses.Set(RevealingPseudoClass, value);
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_markdown is not null)
        {
            _markdown.TemplateApplied -= OnMarkdownTemplateApplied;
        }
        _markdown = e.NameScope.Find<Markdown>(MarkdownPart);
        // The stops move to the new template's layer.
        _targets?.Children.Clear();
        _targets = e.NameScope.Find<Canvas>(TargetsPart);
        if (_markdown is { } markdown)
        {
            markdown.TemplateApplied += OnMarkdownTemplateApplied;
            markdown.Services = _services;
            ApplyMarkdownResources();
            if (_staticText is not null)
            {
                markdown.SetCurrentValue(Markdown.TextProperty, _staticText);
            }
            else if (_shownText.Length > 0)
            {
                // A new template mid-reply: what was shown comes back as one piece.
                markdown.Append(_shownText.ToString());
                if (_phase == Phase.Finished)
                {
                    markdown.Complete();
                }
            }
        }
        RebuildTargets();
    }

    private static void OnMarkdownTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        // The reply text is selectable with the pointer but is not a Tab stop (Arc: only its links are).
        if (e.NameScope.Find<Control>(Markdown.ViewPart) is { } view)
        {
            KeyboardNavigation.SetIsTabStop(view, false);
        }
    }

    /// <summary>Arc's code block look as far as StreamMark's resources reach: radius 16, BorderSubtle, 70 % SurfaceMuted + 30 % Surface.</summary>
    private void ApplyMarkdownResources()
    {
        if (_markdown is not { } markdown)
        {
            return;
        }
        var resources = markdown.Resources;
        SetResource(resources, MarkdownResourceKeys.CodeBorderBrush, new MarkdownResourceAlias(ArcDesignTokenKind.BorderSubtle));
        SetResource(resources, MarkdownResourceKeys.CornerRadius, CodeCornerRadius);
        if (CodeSurfaceMuted is ISolidColorBrush muted && CodeSurface is ISolidColorBrush surface)
        {
            SetResource(resources, MarkdownResourceKeys.CodeBackground, Oklch.MixOklab(muted.Color, surface.Color, 0.3));
        }
    }

    private static void SetResource(IResourceDictionary resources, string key, object value)
    {
        // Every write raises ResourcesChanged and restyles the document, so only real changes are written.
        if (!resources.TryGetValue(key, out var current) || !Equals(current, value))
        {
            resources[key] = value;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            HookSources(change.GetNewValue<IList<AiSource>?>());
            OnSourcesChanged();
        }
        else if (change.Property == RevealIntervalProperty || change.Property == WordsPerStepProperty)
        {
            // A reply keeps its cadence; a reply that has not started takes the new one.
            if (_phase == Phase.Idle && !_pacer.HasPending)
            {
                _pacer = CreatePacer();
            }
        }
        else if (change.Property == CitationNameProperty)
        {
            UpdateTargetNames();
        }
        else if (change.Property == CitationBackgroundProperty || change.Property == CitationForegroundProperty
                 || change.Property == CitationActiveBackgroundProperty || change.Property == CitationActiveForegroundProperty
                 || change.Property == CitationFocusBrushProperty || change.Property == CitationFontFamilyProperty)
        {
            ClearLabels();
            InvalidateAllCitations();
        }
        else if (change.Property == CodeSurfaceMutedProperty || change.Property == CodeSurfaceProperty || change.Property == CodeCornerRadiusProperty)
        {
            ApplyMarkdownResources();
        }
    }

    private void HookSources(IList<AiSource>? sources)
    {
        if (_sourcesHook is not null)
        {
            _sourcesHook.CollectionChanged -= OnSourcesCollectionChanged;
        }
        _sourcesHook = sources as INotifyCollectionChanged;
        if (_sourcesHook is not null)
        {
            _sourcesHook.CollectionChanged += OnSourcesCollectionChanged;
        }
    }

    private void OnSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSourcesChanged();

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

    /// <summary>A piece of converted text to append: plain text (<see cref="Key"/> −1) or one citation tag.</summary>
    private readonly record struct Piece(string Text, int Number, int Key);
}

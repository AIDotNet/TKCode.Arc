using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;

namespace TKCode.Arc;

/// <summary>
/// An Arc-styled Markdown document rendered by StreamMark: headings, lists, tables, quotes, links, inline code and
/// fenced code highlighted in Arc's syntax palette. Show static text with <see cref="Text"/>, or stream a reply with
/// <see cref="Append"/> / <see cref="Complete"/> (or your own <see cref="Stream"/>). The document grows with its
/// content (no internal scrolling); put long documents in a <see cref="ScrollViewer"/>.
/// </summary>
/// <remarks>
/// Colors, fonts and radii follow the ArcDesign tokens (light/dark and accent changes included) through StreamMark's
/// <c>Md.*</c> resources, set on this control. Add <c>Md.*</c> keys to <see cref="StyledElement.Resources"/> to
/// override single values.
/// <para>
/// A host that builds its own chrome around the document (an outline sidebar, a find bar, a copy action) uses
/// <see cref="GetOutline"/>, <see cref="ScrollToPoint"/>, <see cref="ScrollToAnchor"/>, <see cref="Find"/> and
/// <see cref="GetSelectedText"/> instead of walking the template. Parsing happens on a worker thread: the outline is
/// available as soon as the document has a parsed snapshot, and <see cref="IsDocumentReady"/> turns true when that
/// snapshot is applied to the view. Before the template is applied there is no view, so the outline is empty, the
/// scroll methods return <c>false</c>, <see cref="Find"/> returns 0 and the selection is empty; a new template creates
/// a new view, so a selection does not survive retemplating (the find query is run again when the new view is ready).
/// </para>
/// </remarks>
[TemplatePart(ViewPart, typeof(MarkdownView))]
[PseudoClasses(StreamingPseudoClass)]
public class Markdown : TemplatedControl
{
    public const string ViewPart = "PART_View";
    public const string StreamingPseudoClass = ":streaming";

    #region Properties

    /// <summary>The Markdown source to show. Setting it ends a stream started with <see cref="Append"/>.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Markdown, string?>(nameof(Text));

    /// <summary>
    /// A streamed reply to show instead of <see cref="Text"/>. <see cref="Append"/> creates one when none is set; you
    /// can also supply your own (for example one fed from a worker thread with <see cref="MarkdownStream.Push"/>).
    /// </summary>
    public static readonly StyledProperty<MarkdownStream?> StreamProperty =
        AvaloniaProperty.Register<Markdown, MarkdownStream?>(nameof(Stream));

    /// <summary>
    /// StreamMark services (plugins, code block options, link policy). Null uses services set on an ancestor with
    /// <c>MarkdownServices.SetServices</c>, else <see cref="DefaultServices"/>.
    /// </summary>
    public static readonly StyledProperty<MarkdownServices?> ServicesProperty =
        AvaloniaProperty.Register<Markdown, MarkdownServices?>(nameof(Services));

    /// <summary>Body line height in pixels; NaN (default) uses the font size times Arc's prose leading (1.65).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(LineHeight), double.NaN);

    /// <summary>Vertical space between blocks (paragraphs, lists, code, tables).</summary>
    public static readonly StyledProperty<double> BlockSpacingProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(BlockSpacing), 12);

    /// <summary>
    /// Opens allowed absolute links (http, https, mailto) with the system launcher when no <see cref="LinkClicked"/>
    /// handler marks the event handled and the link policy does not ask for confirmation.
    /// </summary>
    public static readonly StyledProperty<bool> OpensLinksProperty =
        AvaloniaProperty.Register<Markdown, bool>(nameof(OpensLinks), true);

    /// <summary>
    /// How long each batch of text appended with <see cref="Append"/> fades in at full motion; null uses Arc's standard
    /// duration. A host that paces the text itself usually sets 150 ms.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> RevealFadeDurationProperty =
        AvaloniaProperty.Register<Markdown, TimeSpan?>(nameof(RevealFadeDuration));

    /// <summary>
    /// Whether appended text still fades in at reduced motion (a short opacity fade). False shows it at once, which suits
    /// a reply that grows continuously: there is no whole to fade.
    /// </summary>
    public static readonly StyledProperty<bool> RevealFadeInReducedMotionProperty =
        AvaloniaProperty.Register<Markdown, bool>(nameof(RevealFadeInReducedMotion), true);

    /// <summary>
    /// Whether <see cref="Append"/> paces the text (true, the default: it is released in small steps that follow the
    /// arrival rate). False shows every appended piece as one batch at once: the host has already paced it, for example
    /// in whole words on a fixed clock.
    /// </summary>
    public static readonly StyledProperty<bool> IsPacedProperty =
        AvaloniaProperty.Register<Markdown, bool>(nameof(IsPaced), true);

    /// <summary>Whether a stream is attached and has not completed yet.</summary>
    public static readonly DirectProperty<Markdown, bool> IsStreamingProperty =
        AvaloniaProperty.RegisterDirect<Markdown, bool>(nameof(IsStreaming), o => o.IsStreaming);

    /// <summary>
    /// True when the current document has been parsed and the result is applied to the view, so the outline, the search
    /// matches and the scroll targets are final. A static <see cref="Text"/> becomes ready once; a stream becomes ready
    /// when it completes. False before the template is applied and while a new source is being parsed.
    /// </summary>
    public static readonly DirectProperty<Markdown, bool> IsDocumentReadyProperty =
        AvaloniaProperty.RegisterDirect<Markdown, bool>(nameof(IsDocumentReady), o => o.IsDocumentReady);

    /// <summary>The number of matches of the last <see cref="Find"/>, 0 when nothing is searched.</summary>
    public static readonly DirectProperty<Markdown, int> MatchCountProperty =
        AvaloniaProperty.RegisterDirect<Markdown, int>(nameof(MatchCount), o => o.MatchCount);

    /// <summary>The 0-based index of the current match, or -1 when the search has no match.</summary>
    public static readonly DirectProperty<Markdown, int> CurrentMatchIndexProperty =
        AvaloniaProperty.RegisterDirect<Markdown, int>(nameof(CurrentMatchIndex), o => o.CurrentMatchIndex);

    /// <summary>True when the document has a selection, so a copy action can be enabled.</summary>
    public static readonly DirectProperty<Markdown, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<Markdown, bool>(nameof(HasSelection), o => o.HasSelection);

    /// <summary>A link was clicked (anchors inside the document scroll by themselves and do not arrive here).</summary>
    public static readonly RoutedEvent<LinkClickedEventArgs> LinkClickedEvent = MarkdownView.LinkClickedEvent;

    /// <summary>Prose leading, set by the theme from <see cref="MarkdownToken"/>.</summary>
    internal static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<Markdown, double>(nameof(Leading), 1.65);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="RevealFadeDurationProperty"/>
    public TimeSpan? RevealFadeDuration { get => GetValue(RevealFadeDurationProperty); set => SetValue(RevealFadeDurationProperty, value); }

    /// <inheritdoc cref="RevealFadeInReducedMotionProperty"/>
    public bool RevealFadeInReducedMotion { get => GetValue(RevealFadeInReducedMotionProperty); set => SetValue(RevealFadeInReducedMotionProperty, value); }

    /// <inheritdoc cref="IsPacedProperty"/>
    public bool IsPaced { get => GetValue(IsPacedProperty); set => SetValue(IsPacedProperty, value); }
    public MarkdownStream? Stream { get => GetValue(StreamProperty); set => SetValue(StreamProperty, value); }
    public MarkdownServices? Services { get => GetValue(ServicesProperty); set => SetValue(ServicesProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double BlockSpacing { get => GetValue(BlockSpacingProperty); set => SetValue(BlockSpacingProperty, value); }
    public bool OpensLinks { get => GetValue(OpensLinksProperty); set => SetValue(OpensLinksProperty, value); }
    internal double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    private bool _isStreaming;
    private bool _isDocumentReady;
    private int _matchCount;
    private int _currentMatchIndex = -1;
    private bool _hasSelection;

    /// <summary>Whether a stream is attached and has not completed yet.</summary>
    public bool IsStreaming { get => _isStreaming; private set => SetAndRaise(IsStreamingProperty, ref _isStreaming, value); }

    /// <inheritdoc cref="IsDocumentReadyProperty"/>
    public bool IsDocumentReady { get => _isDocumentReady; private set => SetAndRaise(IsDocumentReadyProperty, ref _isDocumentReady, value); }

    /// <inheritdoc cref="MatchCountProperty"/>
    public int MatchCount { get => _matchCount; private set => SetAndRaise(MatchCountProperty, ref _matchCount, value); }

    /// <inheritdoc cref="CurrentMatchIndexProperty"/>
    public int CurrentMatchIndex { get => _currentMatchIndex; private set => SetAndRaise(CurrentMatchIndexProperty, ref _currentMatchIndex, value); }

    /// <inheritdoc cref="HasSelectionProperty"/>
    public bool HasSelection { get => _hasSelection; private set => SetAndRaise(HasSelectionProperty, ref _hasSelection, value); }

    /// <summary>A link was clicked. Mark it handled to stop <see cref="OpensLinks"/> from opening it.</summary>
    public event EventHandler<LinkClickedEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    /// <summary>
    /// The current document is parsed and applied, so <see cref="GetOutline"/> and the scroll methods have their final
    /// targets. Raised again when a new <see cref="Text"/> or <see cref="Stream"/> becomes ready, and after a new
    /// template is applied. Runs on the UI thread.
    /// </summary>
    public event EventHandler? DocumentReady;

    /// <summary>
    /// The matches or the current match of <see cref="Find"/> changed (including when the document changes under a
    /// search and the query is run again). Runs on the UI thread.
    /// </summary>
    public event EventHandler? SearchChanged;

    /// <summary>The selection of the document view changed; <see cref="HasSelection"/> and the copy results follow.</summary>
    public event EventHandler? SelectionChanged;

    #endregion

    /// <summary>
    /// The services used when neither <see cref="Services"/> nor an ancestor sets any: Arc-palette TextMate
    /// highlighting (<see cref="MarkdownHighlightPlugin"/>) and Arc's code block wording (copy only, no download,
    /// long blocks fold to 12 lines with "Show all N lines").
    /// </summary>
    public static MarkdownServices DefaultServices { get; } = new()
    {
        Plugins = [new MarkdownHighlightPlugin()],
        CodeBlocks = new CodeBlockOptions
        {
            ShowDownload = false,
            CollapsedLines = 12,
            CollapseText = static () => "Show fewer lines"
        }
    };

    private readonly ResourceDictionary _resources = CreateResources();
    private MarkdownView? _view;
    private StreamingDocument? _document;
    private MarkdownProfile? _documentProfile;
    private StreamingDocument? _watched;
    private DocumentSnapshot? _readySnapshot;
    private string _query = string.Empty;
    private bool _matchCase;
    private int _applyPending;
    private MarkdownStream? _subscribed;
    private MarkdownStream? _ownStream;

    public Markdown()
    {
        Resources.MergedDictionaries.Add(_resources);
        UpdateTypography();
        AddHandler(LinkClickedEvent, OnLinkClicked);
    }

    internal MarkdownView? View => _view;

    /// <summary>
    /// Appends streamed text (from the UI thread). Starts a new reply when no stream is running, replacing what is
    /// shown (<see cref="Text"/> becomes null); the text is paced and fades in at the end of the document.
    /// </summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        // A stream that ended while the control was detached (not listening for Completed) has ended all the same.
        if (Stream is null || !IsStreaming || Stream.Document.IsComplete)
        {
            // The reply replaces the static text (Text reads null while a reply is shown).
            SetCurrentValue(TextProperty, null);
            _ownStream = new MarkdownStream(smooth: IsPaced, fadeDuration: FadeDuration(), profile: EffectiveServices().Profile);
            SetCurrentValue(StreamProperty, _ownStream);
        }
        Stream!.Push(text);
    }

    /// <summary>Ends the current stream; <see cref="IsStreaming"/> turns false once the paced backlog is shown.</summary>
    public void Complete() => Stream?.Complete();

    #region Host integration

    /// <summary>
    /// The headings of the current document in reading order, from the latest parsed snapshot, so it also works while a
    /// stream is still growing and before <see cref="IsDocumentReady"/> turns true. Empty before the template is applied
    /// or while the first parse runs.
    /// </summary>
    public IReadOnlyList<OutlineItem> GetOutline()
    {
        // Without a view the targets point nowhere. The view check also keeps the answer deterministic: the worker
        // publishes snapshots on its own thread, so a parse started by setting Text can land at any moment.
        if (_view is null || _watched?.Latest is not { } snapshot)
        {
            return Array.Empty<OutlineItem>();
        }
        return DocumentNavigation.Outline(snapshot, 0);
    }

    /// <summary>
    /// Scrolls the document so <paramref name="point"/> (a <see cref="OutlineItem.Target"/> or a selection point) is in
    /// view; the enclosing <see cref="ScrollViewer"/> does the scrolling. Returns false when the view has not laid the
    /// target out yet: call it after <see cref="DocumentReady"/>, or wait for it before scrolling from a new source.
    /// </summary>
    public bool ScrollToPoint(TextPoint point)
    {
        if (_view is null)
        {
            return false;
        }
        ApplyLatest();
        return _view.ScrollToPoint(point);
    }

    /// <summary>Scrolls to the start of the block with <paramref name="blockKey"/> (the <c>BlockKey</c> of a <see cref="TextPoint"/>).</summary>
    public bool ScrollToBlock(int blockKey) => ScrollToPoint(new TextPoint(0, blockKey, 0, 0));

    /// <summary>
    /// Scrolls to the heading a link points at, such as <c>#release-notes</c> or <c>release-notes</c>; the anchor matches
    /// the heading id or its GitHub-style slug. Returns false when the document has no such heading yet.
    /// </summary>
    public bool ScrollToAnchor(string? anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor) || _watched?.Latest is not { } snapshot)
        {
            return false;
        }
        var url = anchor[0] == '#' ? anchor : "#" + anchor;
        return DocumentNavigation.ResolveAnchor(snapshot, 0, url) is { } target && ScrollToPoint(target);
    }

    /// <summary>
    /// Finds <paramref name="query"/> in the document, highlights every match and makes the first one current; returns
    /// the number of matches. The query is kept: when the text or the stream changes, or the template is applied again,
    /// it runs on the new document. An empty query clears the search. Matching is case-insensitive unless
    /// <paramref name="matchCase"/> is true.
    /// </summary>
    public int Find(string? query, bool matchCase = false)
    {
        _query = query ?? string.Empty;
        _matchCase = matchCase;
        return RunSearch();
    }

    /// <summary>Moves to the next match, wrapping around, and scrolls to it.</summary>
    public void FindNext() => _view?.Search.Next();

    /// <summary>Moves to the previous match, wrapping around, and scrolls to it.</summary>
    public void FindPrevious() => _view?.Search.Previous();

    /// <summary>Moves to the match at <paramref name="index"/> and scrolls to it; out-of-range indexes are ignored.</summary>
    public void FindAt(int index) => _view?.Search.MoveTo(index);

    /// <summary>Clears the search and its highlights; <see cref="MatchCount"/> becomes 0 and <see cref="CurrentMatchIndex"/> -1.</summary>
    public void ClearSearch() => Find(null);

    /// <summary>The selected text, with formulas as <c>$latex$</c> and table cells separated by tabs; empty without a selection.</summary>
    public string GetSelectedText() => _view?.SelectionController.GetSelectedText() ?? string.Empty;

    /// <summary>The Markdown source of the blocks the selection covers; empty without a selection.</summary>
    public string GetSelectedMarkdown() => _view?.SelectionController.GetSelectedMarkdown() ?? string.Empty;

    /// <summary>Selects the whole document.</summary>
    public void SelectAll() => _view?.SelectionController.SelectAll();

    /// <summary>Copies the selection to the clipboard as plain text, or as Markdown when <paramref name="asMarkdown"/> is true.</summary>
    public Task CopySelectionAsync(bool asMarkdown = false)
        => _view is { } view ? view.SelectionController.CopyAsync(asMarkdown) : Task.CompletedTask;

    /// <summary>Applies the latest parsed snapshot now instead of waiting for the next frame; a no-op without a view.</summary>
    private void ApplyLatest()
    {
        if (_view is { } view && _watched is { } document && TopLevel.GetTopLevel(view) is not null
            && !ReferenceEquals(view.Snapshot, document.Latest))
        {
            view.ApplyLatest();
            UpdateReady();
        }
    }

    private int RunSearch()
    {
        if (_view is null)
        {
            MatchCount = 0;
            CurrentMatchIndex = -1;
            return 0;
        }
        ApplyLatest();
        var count = _view.Search.Find(_query, _matchCase);
        OnSearchChanged();
        return count;
    }

    private void OnSearchChanged()
    {
        MatchCount = _view?.Search.Count ?? 0;
        CurrentMatchIndex = _view?.Search.CurrentIndex ?? -1;
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged()
    {
        HasSelection = _view is { } view && !view.Selection.IsEmpty;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="DocumentReady"/> once per applied complete snapshot, and drops readiness while a new source
    /// parses. A search that is still set runs again on the new document.
    /// </summary>
    private void UpdateReady()
    {
        var latest = _watched?.Latest;
        var ready = latest is { IsComplete: true } && ReferenceEquals(_view?.Snapshot, latest);
        if (ready)
        {
            if (ReferenceEquals(latest, _readySnapshot))
            {
                return;
            }
            _readySnapshot = latest;
            IsDocumentReady = true;
            if (_query.Length > 0)
            {
                RunSearch();
            }
            DocumentReady?.Invoke(this, EventArgs.Empty);
            return;
        }
        _readySnapshot = null;
        IsDocumentReady = false;
    }

    private void DetachView()
    {
        if (_view is { } view)
        {
            view.Search.Changed -= OnSearchChanged;
            view.Selection.Changed -= OnSelectionChanged;
        }
        HasSelection = false;
    }

    private void AttachView()
    {
        if (_view is { } view)
        {
            view.Search.Changed += OnSearchChanged;
            view.Selection.Changed += OnSelectionChanged;
        }
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachView();
        _view = e.NameScope.Find<MarkdownView>(ViewPart);
        AttachView();
        UpdateServices();
        if (_watched is { } document)
        {
            // A new template means a new view: give it the document and let it lay the content out again.
            AssignToView(document);
            _readySnapshot = null;
            IsDocumentReady = false;
            _view?.ApplyLatest();
            UpdateReady();
        }
        else
        {
            UpdateSource();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (Stream is not null)
            {
                // Text wins over a stream (OnPropertyChanged runs again for Stream and updates the view).
                SetCurrentValue(StreamProperty, null);
            }
            else
            {
                UpdateSource();
            }
        }
        else if (change.Property == StreamProperty)
        {
            Subscribe(change.GetNewValue<MarkdownStream?>());
            UpdateSource();
        }
        else if (change.Property == ServicesProperty || change.Property == MarkdownServices.ServicesProperty)
        {
            UpdateServices();
        }
        else if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty || change.Property == LineHeightProperty
                 || change.Property == LeadingProperty || change.Property == BlockSpacingProperty)
        {
            UpdateTypography();
        }
    }

    private MarkdownServices EffectiveServices()
    {
        if (Services is { } services)
        {
            return services;
        }
        var inherited = GetValue(MarkdownServices.ServicesProperty);
        return ReferenceEquals(inherited, MarkdownServices.Default) ? DefaultServices : inherited;
    }

    private void UpdateServices()
    {
        if (_view is null)
        {
            return;
        }
        var services = EffectiveServices();
        MarkdownServices.SetServices(_view, services);
        if (_document is not null && !ReferenceEquals(_documentProfile, services.Profile))
        {
            // Plugins can change the Markdown dialect: parse the static text again with the new profile.
            _document = null;
            UpdateSource();
        }
    }

    /// <summary>
    /// Static text goes through a document owned by this control (parsed on StreamMark's worker with the services'
    /// profile); a stream brings its own document. Either way the view shows it.
    /// </summary>
    private void UpdateSource()
    {
        StreamingDocument document;
        if (Stream is { } stream)
        {
            document = stream.Document;
        }
        else
        {
            if (_document is null)
            {
                var services = EffectiveServices();
                _documentProfile = services.Profile;
                _document = new StreamingDocument(_documentProfile);
            }
            document = _document;
            document.Load(Text ?? string.Empty);
        }
        // The old snapshot's targets belong to the old source: the document is not ready until the new parse lands.
        _readySnapshot = null;
        IsDocumentReady = false;
        AssignToView(document);
        Watch(document);
    }

    private void AssignToView(StreamingDocument document)
    {
        if (_view is not { } view)
        {
            return;
        }
        if (Stream is { } stream)
        {
            view.Document = null;
            view.Stream = stream;
        }
        else
        {
            view.Stream = null;
            view.Document = document;
        }
    }

    private void Watch(StreamingDocument? document)
    {
        if (ReferenceEquals(document, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.SnapshotReady -= OnSnapshotReady;
        }
        _watched = document;
        if (document is not null && IsAttached)
        {
            document.SnapshotReady += OnSnapshotReady;
        }
        OnSnapshotReady(null);
    }

    /// <summary>
    /// Safety net (worker thread). MarkdownView applies snapshots on its next animation frame and keeps one pending
    /// frame request; should that frame callback be lost, the document would stay empty. After each snapshot a
    /// background-priority check applies it if the view has not already (normally it has, and this does nothing).
    /// </summary>
    private void OnSnapshotReady(DocumentSnapshot? snapshot)
    {
        if (Interlocked.Exchange(ref _applyPending, 1) == 0)
        {
            // This control's dispatcher: on the worker, Dispatcher.UIThread would make one for the worker when none is current.
            Dispatcher.Post(EnsureApplied, DispatcherPriority.Background);
        }
    }

    private void EnsureApplied()
    {
        Volatile.Write(ref _applyPending, 0);
        if (_view is { } view && _watched is { } document && TopLevel.GetTopLevel(view) is not null
            && !ReferenceEquals(view.Snapshot, document.Latest))
        {
            view.ApplyLatest();
        }
        UpdateReady();
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_watched is { } document)
        {
            document.SnapshotReady -= OnSnapshotReady;
            document.SnapshotReady += OnSnapshotReady;
            OnSnapshotReady(null);
        }
        ListenTo(Stream);
        if (IsStreaming && Stream is { Document.IsComplete: true })
        {
            // The stream ended while the control was away.
            SetStreaming(false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_watched is { } document)
        {
            document.SnapshotReady -= OnSnapshotReady;
        }
        ListenTo(null);
    }

    private void Subscribe(MarkdownStream? stream)
    {
        ListenTo(IsAttached ? stream : null);
        if (!ReferenceEquals(stream, _ownStream))
        {
            _ownStream = null;
        }
        SetStreaming(stream is not null && !stream.Document.IsComplete);
    }

    /// <summary>A stream may outlive the control: it is listened to only while the control is attached.</summary>
    private void ListenTo(MarkdownStream? stream)
    {
        if (ReferenceEquals(stream, _subscribed))
        {
            return;
        }
        if (_subscribed is not null)
        {
            _subscribed.Completed -= OnStreamCompleted;
        }
        _subscribed = stream;
        if (stream is not null)
        {
            stream.Completed += OnStreamCompleted;
        }
    }

    private void OnStreamCompleted() => Dispatcher.UIThread.Post(() =>
    {
        if (Stream is { } stream && stream.Document.IsComplete)
        {
            SetStreaming(false);
        }
    });

    private void SetStreaming(bool value)
    {
        IsStreaming = value;
        PseudoClasses.Set(StreamingPseudoClass, value);
    }

    /// <summary>
    /// New text fades in over <see cref="RevealFadeDuration"/> (Arc's standard duration by default); Reduced keeps a
    /// shorter fade (opacity only) unless <see cref="RevealFadeInReducedMotion"/> is off, Off none.
    /// </summary>
    private TimeSpan FadeDuration() => ArcMotion.GetEffectiveLevel(this) switch
    {
        ArcMotionLevel.Full => RevealFadeDuration ?? ArcMotionPresets.Standard,
        ArcMotionLevel.Reduced => RevealFadeInReducedMotion ? ArcMotionPresets.Fast : TimeSpan.Zero,
        _ => TimeSpan.Zero
    };

    /// <summary>
    /// Writes the typography StreamMark reads as <c>Md.*</c> resources. Changing them raises ResourcesChanged, which
    /// makes the view resolve its style again (inherited font changes alone would not).
    /// </summary>
    private void UpdateTypography()
    {
        var fontSize = FontSize;
        var lineHeight = double.IsNaN(LineHeight) ? Math.Round(fontSize * Leading) : LineHeight;
        _resources[MarkdownResourceKeys.FontSize] = fontSize;
        _resources[MarkdownResourceKeys.FontFamily] = FontFamily;
        _resources[MarkdownResourceKeys.LineHeight] = lineHeight;
        _resources[MarkdownResourceKeys.BlockSpacing] = BlockSpacing;
    }

    private void OnLinkClicked(object? sender, LinkClickedEventArgs e)
    {
        if (!OpensLinks || e.RequiresConfirmation || !Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "mailto"))
        {
            return;
        }
        // Handlers added by the app run after this one; open only if none of them handled the click.
        Dispatcher.UIThread.Post(() =>
        {
            if (!e.Handled && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            {
                _ = launcher.LaunchUriAsync(uri);
            }
        });
    }

    /// <summary>
    /// StreamMark's colors, fonts and radii as aliases of ArcDesign tokens, resolved from the view so they follow the
    /// theme scope, light/dark and the accent. Values follow Arc's rich text content styles (links in the strong
    /// accent, inline code on the muted surface, quotes with a strong border) and its CodeBlock (surface, border,
    /// mono font).
    /// </summary>
    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary();
        void Alias(string key, ArcDesignTokenKind token) => resources[key] = new MarkdownResourceAlias(token);
        Alias(MarkdownResourceKeys.Foreground, ArcDesignTokenKind.Foreground);
        Alias(MarkdownResourceKeys.SecondaryForeground, ArcDesignTokenKind.TextSecondary);
        Alias(MarkdownResourceKeys.LinkForeground, ArcDesignTokenKind.AccentStrong);
        Alias(MarkdownResourceKeys.SelectionBackground, ArcDesignTokenKind.AccentSubtle);
        Alias(MarkdownResourceKeys.CodeBackground, ArcDesignTokenKind.Surface);
        Alias(MarkdownResourceKeys.CodeBorderBrush, ArcDesignTokenKind.Border);
        Alias(MarkdownResourceKeys.InlineCodeBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.InlineCodeForeground, ArcDesignTokenKind.Foreground);
        Alias(MarkdownResourceKeys.ListMarkerForeground, ArcDesignTokenKind.TextMuted);
        Alias(MarkdownResourceKeys.MarkBackground, ArcDesignTokenKind.WarningSubtle);
        Alias(MarkdownResourceKeys.QuoteBorderBrush, ArcDesignTokenKind.BorderStrong);
        Alias(MarkdownResourceKeys.TableBorderBrush, ArcDesignTokenKind.Border);
        Alias(MarkdownResourceKeys.TableHeaderBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.HoverBackground, ArcDesignTokenKind.SurfaceMuted);
        Alias(MarkdownResourceKeys.MonospaceFontFamily, ArcDesignTokenKind.MonoFontFamily);
        Alias(MarkdownResourceKeys.CornerRadius, ArcDesignTokenKind.RadiusControl);
        return resources;
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class SourceCards
{
    /// <summary>Lucide search-x (a magnifier with a cross), for "No sources found".</summary>
    internal static readonly Geometry SearchX =
        ArcIcons.Parse("M13.5 8.5 8.5 13.5 M8.5 8.5 13.5 13.5 M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 M21 21 16.7 16.7");

    private Grid? _statusRow;
    private Grid? _statusLeft;
    private ArcContentSwap? _statusIcon;
    private ArcContentSwap? _statusWord;
    private SourceCardsCount? _count;
    private ArcContentSwap? _meta;
    private SourceCardsActionButton? _retry;
    private SourceCardsActionButton? _toggle;
    private Panel? _answer;
    private ContentPresenter? _sampleAnswer;
    private SourceCardsGrid? _grid;
    private ExplainerAnnouncer? _announcer;
    private ArcPopup? _popup;
    private SourceCardsPreview? _preview;

    private readonly Dictionary<string, SourceCardsCard> _cards = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SourceCardsSkeleton> _skeletons = [];
    private readonly List<SourceCardsCellHost> _hosts = [];
    private readonly List<SourceCitation> _citations = [];
    private SourceCardsStack? _stack;
    private List<SourceCardsCell> _plan = [];

    // Cached status parts: the same view comes back for the same state, so the swaps only run on real changes.
    private ArcSpinner? _spinnerIcon;
    private ArcGlyph? _globeIcon;
    private ArcGlyph? _alertIcon;
    private ArcGlyph? _emptyIcon;
    private ToolCallText? _searchingWord;
    private TextBlock? _failedWord;
    private TextBlock? _emptyWord;
    private TextBlock? _metaText;
    private string? _announcedKey;
    private DispatcherTimer? _highlightTimer;

    /// <summary>The cells' plan of the last refresh.</summary>
    internal IReadOnlyList<SourceCardsCell> CellPlan => _plan;

    /// <summary>The cell hosts, in cell order.</summary>
    internal IReadOnlyList<SourceCardsCellHost> CellHosts => _hosts;

    internal SourceCardsGrid? GridPanel => _grid;

    internal SourceCardsStack? StackTile => _stack;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal SourceCardsActionButton? ToggleButton => _toggle;

    internal SourceCardsActionButton? RetryButton => _retry;

    internal SourceCardsCount? CountView => _count;

    /// <summary>The card of source <paramref name="number"/> (1-based), when it has been made.</summary>
    internal SourceCardsCard? CardOf(int number)
    {
        var sources = DisplayedSources;
        return number >= 1 && number <= sources.Count && _cards.TryGetValue(sources[number - 1].Key, out var card) ? card : null;
    }

    /// <summary>The status word shown now ("Searching", "Search failed", "No sources found", or empty when done).</summary>
    internal string DisplayedWord => EffectiveStatus switch
    {
        SourceCardsStatus.Searching => Labels.Searching,
        SourceCardsStatus.Error => Labels.Failed,
        _ => IsEmptyResult ? Labels.Empty : string.Empty
    };

    /// <summary>The rolling count shown now ("3 of 7", "7 sources", or empty).</summary>
    internal string DisplayedCount
    {
        get
        {
            var count = DisplayedSources.Count;
            return EffectiveStatus switch
            {
                SourceCardsStatus.Searching => Labels.Progress(count, EffectiveTotal),
                SourceCardsStatus.Done when count > 0 => Labels.Count(count),
                _ => string.Empty
            };
        }
    }

    /// <summary>The muted text after the count: the query while searching or when nothing was found, "· 2.1s" when done,
    /// the error's detail after a failure.</summary>
    internal string DisplayedMeta
    {
        get
        {
            switch (EffectiveStatus)
            {
                case SourceCardsStatus.Searching:
                    return EffectiveQuery ?? string.Empty;
                case SourceCardsStatus.Error:
                    return EffectiveError ?? string.Empty;
                default:
                    if (IsEmptyResult)
                    {
                        return EffectiveQuery ?? string.Empty;
                    }
                    return EffectiveDuration is { } time ? "· " + Labels.Duration(time) : string.Empty;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _statusRow = e.NameScope.Find<Grid>(StatusRowPart);
        _statusLeft = e.NameScope.Find<Grid>(StatusLeftPart);
        _statusIcon = e.NameScope.Find<ArcContentSwap>(StatusIconPart);
        _statusWord = e.NameScope.Find<ArcContentSwap>(StatusWordPart);
        _count = e.NameScope.Find<SourceCardsCount>(CountPart);
        _meta = e.NameScope.Find<ArcContentSwap>(MetaPart);
        _retry = e.NameScope.Find<SourceCardsActionButton>(RetryPart);
        _toggle = e.NameScope.Find<SourceCardsActionButton>(TogglePart);
        _answer = e.NameScope.Find<Panel>(AnswerPart);
        _sampleAnswer = e.NameScope.Find<ContentPresenter>(SampleAnswerPart);
        _grid = e.NameScope.Find<SourceCardsGrid>(GridPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _preview = e.NameScope.Find<SourceCardsPreview>(PreviewPart);
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_toggle is not null)
        {
            _toggle.IsToggle = true;
            _toggle.Click += OnToggleClick;
        }
        // Fresh parts: every cell is rebuilt without motion.
        foreach (var host in _hosts)
        {
            host.Show(null, SourceCardsEnter.Fade);
        }
        _hosts.Clear();
        // The status parts may still sit in the previous template's swaps: new ones are made for the new template.
        _spinnerIcon = null;
        _globeIcon = null;
        _alertIcon = null;
        _emptyIcon = null;
        _searchingWord = null;
        _failedWord = null;
        _emptyWord = null;
        _metaText = null;
        _announcedKey = null;
        if (_sampleAnswer is not null)
        {
            _sampleAnswer.Content = _simulatedText;
        }
        AnnounceSimulated();
        Refresh();
    }

    private void Unhook()
    {
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    internal void Refresh()
    {
        var status = EffectiveStatus;
        var sources = DisplayedSources;
        var empty = IsEmptyResult;
        PseudoClasses.Set(SearchingPseudoClass, status == SourceCardsStatus.Searching);
        PseudoClasses.Set(ErrorPseudoClass, status == SourceCardsStatus.Error);
        PseudoClasses.Set(EmptyPseudoClass, empty);
        PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
        if (_grid is null)
        {
            return;
        }
        RefreshStatus(status, sources.Count, empty);
        RefreshAnswer();
        RefreshCells(status, sources);
        SyncHighlight();
        SyncCitations();
        Announce(status, sources.Count, empty);
    }

    private void RefreshStatus(SourceCardsStatus status, int count, bool empty)
    {
        var labels = Labels;
        if (_statusIcon is not null)
        {
            _statusIcon.Content = status switch
            {
                SourceCardsStatus.Searching => _spinnerIcon ??= Spinner(),
                SourceCardsStatus.Error => _alertIcon ??= StatusGlyph(ArcIcons.CircleAlert, ArcDesignTokenKind.Danger),
                _ when empty => _emptyIcon ??= StatusGlyph(SearchX, ArcDesignTokenKind.TextSecondary),
                _ => _globeIcon ??= StatusGlyph(ArcIcons.Globe, ArcDesignTokenKind.TextSecondary)
            };
        }
        if (_statusWord is not null)
        {
            Control? word = status switch
            {
                SourceCardsStatus.Searching => _searchingWord ??= ShimmeringWord(),
                SourceCardsStatus.Error => _failedWord ??= StatusText(ArcDesignTokenKind.Foreground, word: true),
                _ when empty => _emptyWord ??= StatusText(ArcDesignTokenKind.Foreground, word: true),
                _ => null
            };
            if (_searchingWord is not null)
            {
                _searchingWord.Text = labels.Searching;
            }
            if (_failedWord is not null)
            {
                _failedWord.Text = labels.Failed;
            }
            if (_emptyWord is not null)
            {
                _emptyWord.Text = labels.Empty;
            }
            _statusWord.Content = word;
        }
        if (_count is not null)
        {
            _count.Text = DisplayedCount;
        }
        if (_meta is not null)
        {
            var meta = DisplayedMeta;
            if (meta.Length == 0)
            {
                _meta.Content = null;
            }
            else if (_metaText?.Text != meta || !ReferenceEquals(_meta.Content, _metaText))
            {
                // A new text crossfades in; the same text stays put.
                _metaText = StatusText(ArcDesignTokenKind.TextMuted, word: false);
                _metaText.Text = meta;
                _meta.Content = _metaText;
            }
        }
        var overflow = count > Max;
        var retry = EffectiveAllowsRetry && (status == SourceCardsStatus.Error || empty);
        if (_retry is not null)
        {
            _retry.IsVisible = retry;
            _retry.Content = status == SourceCardsStatus.Error ? labels.Retry : labels.SearchAgain;
        }
        if (_toggle is not null)
        {
            _toggle.IsVisible = overflow && !retry;
            _toggle.IsExpanded = IsExpanded;
            _toggle.Content = IsExpanded ? labels.ShowLess : labels.ShowAll;
        }
        if (_statusLeft is not null)
        {
            _statusLeft.IsVisible = IsStatusVisible;
        }
        if (_statusRow is not null)
        {
            _statusRow.IsVisible = IsStatusVisible || overflow || retry;
        }
    }

    // The status parts are made here, outside the template, so their colors are bound to the tokens directly.

    private static ArcGlyph StatusGlyph(Geometry data, ArcDesignTokenKind color)
    {
        var glyph = new ArcGlyph { Data = data, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    private static ArcSpinner Spinner()
    {
        var spinner = new ArcSpinner { Width = 14, Height = 14, Thickness = 1.75 };
        ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return spinner;
    }

    /// <summary>The status word (Foreground, medium, 5px before the count) or the muted detail (6px after it).</summary>
    private static TextBlock StatusText(ArcDesignTokenKind color, bool word)
    {
        var text = new TextBlock
        {
            LineHeight = 18,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = word ? new Thickness(0, 0, 5, 0) : new Thickness(6, 0, 0, 0)
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
        if (word)
        {
            ArcTokens.Bind(text, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        return text;
    }

    private static ToolCallText ShimmeringWord()
    {
        var text = new ToolCallText { IsShimmering = true, LineHeight = 18, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(text, ToolCallText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(text, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(text, ToolCallText.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        return text;
    }

    private void RefreshAnswer()
    {
        if (_answer is null)
        {
            return;
        }
        var simulating = IsSimulating;
        var hasAnswer = simulating ? _simulatedText?.Inlines?.Count > 0 : Content is not null;
        _answer.Classes.Set("has-answer", hasAnswer);
        if (_sampleAnswer is not null)
        {
            _sampleAnswer.IsVisible = simulating;
        }
    }

    private void RefreshCells(SourceCardsStatus status, IReadOnlyList<SourceCardsSource> sources)
    {
        if (_grid is null)
        {
            return;
        }
        var max = Max;
        var reserved = status == SourceCardsStatus.Searching ? EffectiveTotal ?? 0 : 0;
        var plan = SourceCardsFormat.Plan(sources.Count, reserved, max, IsExpanded);
        _plan = plan;
        _grid.Max = max;
        _grid.CellCount = plan.Count;
        AutomationProperties.SetItemStatus(_grid, status == SourceCardsStatus.Searching ? Labels.Busy : string.Empty);
        AutomationProperties.SetName(_grid, Labels.List);

        // Cards of sources that left the list are forgotten.
        if (_cards.Count > sources.Count)
        {
            var keys = sources.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var key in _cards.Keys.Where(k => !keys.Contains(k)).ToList())
            {
                _cards.Remove(key);
            }
        }
        while (_hosts.Count < plan.Count)
        {
            var host = new SourceCardsCellHost();
            _hosts.Add(host);
            _grid.Children.Add(host);
        }
        for (var i = 0; i < _hosts.Count; i++)
        {
            if (i >= plan.Count)
            {
                _hosts[i].Show(null, SourceCardsEnter.Fade);
                continue;
            }
            var cell = plan[i];
            switch (cell.Kind)
            {
                case SourceCardsCellKind.Card:
                    _hosts[i].Show(CardFor(sources[cell.Index], cell.Index + 1), SourceCardsEnter.Rise);
                    break;
                case SourceCardsCellKind.Skeleton:
                    _hosts[i].Show(SkeletonFor(cell.Index), SourceCardsEnter.Fade);
                    break;
                default:
                    _hosts[i].Show(StackFor(sources, max), SourceCardsEnter.Fade);
                    break;
            }
        }
    }

    private SourceCardsCard CardFor(SourceCardsSource source, int number)
    {
        if (!_cards.TryGetValue(source.Key, out var card))
        {
            card = new SourceCardsCard();
            card.Click += OnCardClick;
            card.PointerEntered += OnCardPointerEntered;
            card.PointerExited += OnCardPointerExited;
            card.GotFocus += OnCardGotFocus;
            card.LostFocus += OnCardLostFocus;
            _cards[source.Key] = card;
        }
        var domain = SourceCardsFormat.Domain(source.Url);
        card.Item = source;
        card.Number = number;
        card.NumberText = SourceCardsFormat.Number(number);
        card.Title = source.Title;
        card.Domain = domain;
        card.Icon = source.Icon;
        card.Letter = SourceCardsFormat.Monogram(domain, source.Site ?? source.Title);
        AutomationProperties.SetName(card, Labels.CardName(number, source.Title, domain, EffectiveOpensLinks));
        return card;
    }

    private SourceCardsSkeleton SkeletonFor(int index)
    {
        if (!_skeletons.TryGetValue(index, out var skeleton))
        {
            skeleton = new SourceCardsSkeleton();
            _skeletons[index] = skeleton;
        }
        return skeleton;
    }

    private SourceCardsStack StackFor(IReadOnlyList<SourceCardsSource> sources, int max)
    {
        if (_stack is null)
        {
            _stack = new SourceCardsStack();
            _stack.Click += OnStackClick;
        }
        var hidden = sources.Skip(max).ToList();
        _stack.MoreText = Labels.More(hidden.Count);
        var sites = SourceCardsFormat.Sites(hidden.Select(SiteOf));
        _stack.Sites = sites;
        _stack.SetMarks(hidden.Select(s =>
        {
            var domain = SourceCardsFormat.Domain(s.Url);
            return (s.Icon, SourceCardsFormat.Monogram(domain, s.Site ?? s.Title));
        }));
        AutomationProperties.SetName(_stack, Labels.StackName(hidden.Count, sites));
        return _stack;
    }

    private static string SiteOf(SourceCardsSource source) =>
        string.IsNullOrWhiteSpace(source.Site) ? SourceCardsFormat.Domain(source.Url) : source.Site;

    // Expanding ----------------------------------------------------------------------------------------------------------

    /// <summary>Opens or closes the stack as the person asked, and says so.</summary>
    private void SetExpanded(bool expanded)
    {
        if (IsExpanded == expanded)
        {
            return;
        }
        SetCurrentValue(IsExpandedProperty, expanded);
        RaiseEvent(new SourceCardsExpandedEventArgs(ExpandedChangedEvent, expanded));
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => SetExpanded(!IsExpanded);

    private void OnStackClick(object? sender, RoutedEventArgs e)
    {
        var keyboard = _stack?.IsKeyboardFocusWithin == true;
        SetExpanded(true);
        if (keyboard)
        {
            // The stack has left its cell: focus moves to the card that took its place.
            var next = CardOf(Max + 1);
            // The stack holds focus after a click too: the ring only when a key pressed it.
            var method = SpecialFocusModality.For(this);
            Dispatcher.UIThread.Post(() => next?.Focus(method), DispatcherPriority.Loaded);
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        ActiveSimulation?.Restart();
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
    }

    // Cards --------------------------------------------------------------------------------------------------------------

    private async void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not SourceCardsCard { Item: { } source } card)
        {
            return;
        }
        HidePreview(immediately: false);
        var args = new SourceCardsSourceEventArgs(SourceInvokedEvent, source, card.Number);
        RaiseEvent(args);
        if (args.Handled || !EffectiveOpensLinks)
        {
            return;
        }
        if (Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(uri);
        }
    }

    // Highlight ----------------------------------------------------------------------------------------------------------

    /// <summary>A citation was pressed: its card is highlighted (the stack opens when the card is in it).</summary>
    internal void OnCitationClicked(SourceCitation citation) => Highlight(citation.Number, fromCitation: true);

    private void Highlight(int number, bool fromCitation)
    {
        if (number < 1)
        {
            return;
        }
        if (number > Max && number <= DisplayedSources.Count && !IsExpanded)
        {
            SetExpanded(true);
        }
        SetCurrentValue(HighlightedProperty, number);
        if (fromCitation)
        {
            RaiseEvent(new SourceCardsHighlightEventArgs(HighlightChangedEvent, number));
        }
        StopHighlightTimer();
        _highlightTimer = new DispatcherTimer { Interval = HighlightFor };
        _highlightTimer.Tick += (_, _) => EndHighlight(number);
        _highlightTimer.Start();
    }

    /// <summary>Ends the timed highlight of <paramref name="number"/> unless the highlight has moved on.</summary>
    internal void EndHighlight(int number)
    {
        StopHighlightTimer();
        if (Highlighted == number)
        {
            SetCurrentValue(HighlightedProperty, null);
            RaiseEvent(new SourceCardsHighlightEventArgs(HighlightChangedEvent, null));
        }
    }

    private void StopHighlightTimer()
    {
        _highlightTimer?.Stop();
        _highlightTimer = null;
    }

    private void SyncHighlight()
    {
        var highlighted = Highlighted;
        var sources = DisplayedSources;
        for (var i = 0; i < sources.Count; i++)
        {
            if (_cards.TryGetValue(sources[i].Key, out var card))
            {
                card.IsHighlighted = highlighted == i + 1;
            }
        }
        if (_stack is not null)
        {
            _stack.IsHighlighted = highlighted is { } n && n > Max && !IsExpanded;
        }
        foreach (var citation in _citations)
        {
            citation.SetHighlighted(highlighted is { } h && h == citation.Number);
        }
    }

    // Citations ----------------------------------------------------------------------------------------------------------

    internal void Register(SourceCitation citation)
    {
        if (!_citations.Contains(citation))
        {
            _citations.Add(citation);
        }
        SyncCitation(citation);
    }

    internal void Unregister(SourceCitation citation) => _citations.Remove(citation);

    /// <summary>Names a citation after its source and shows its highlight.</summary>
    internal void SyncCitation(SourceCitation citation)
    {
        var sources = DisplayedSources;
        var n = citation.Number;
        var source = n >= 1 && n <= sources.Count ? sources[n - 1] : null;
        AutomationProperties.SetName(citation, Labels.CitationName(n, source?.Title, source is null ? null : SourceCardsFormat.Domain(source.Url)));
        citation.SetHighlighted(Highlighted is { } h && h == n);
    }

    private void SyncCitations()
    {
        foreach (var citation in _citations)
        {
            SyncCitation(citation);
        }
    }

    /// <summary>The citations inside the answer now.</summary>
    internal IReadOnlyList<SourceCitation> Citations => _citations;

    // Announcements ------------------------------------------------------------------------------------------------------

    /// <summary>Says each status once (searching for the query, the number found, nothing found, the failure); counts do
    /// not announce on every card.</summary>
    private void Announce(SourceCardsStatus status, int count, bool empty)
    {
        var key = status switch
        {
            SourceCardsStatus.Searching => "searching:" + EffectiveQuery,
            SourceCardsStatus.Error => "error:" + EffectiveError,
            _ => empty ? "empty" : "done"
        };
        if (key == _announcedKey)
        {
            return;
        }
        var first = _announcedKey is null;
        _announcedKey = key;
        if (first && status == SourceCardsStatus.Done && !empty)
        {
            // A finished list shown from the start (history) has nothing to announce.
            return;
        }
        var labels = Labels;
        _announcer?.Announce(status switch
        {
            SourceCardsStatus.Searching => labels.AnnounceSearching(EffectiveQuery),
            SourceCardsStatus.Error => labels.AnnounceFailed(EffectiveError),
            _ => empty ? labels.Empty : labels.AnnounceFound(count)
        });
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class PdfViewer
{
    /// <summary>Set while <see cref="IsSearchOpen"/> is true.</summary>
    public const string SearchOpenPseudoClass = ":search-open";

    private static readonly TimeSpan s_searchDebounce = TimeSpan.FromMilliseconds(300);

    #region Properties

    /// <summary>Shows the search bar (two-way). Closing it ends the search and takes the highlights away; <see cref="SearchText"/> stays, and opening again searches for it.</summary>
    public static readonly StyledProperty<bool> IsSearchOpenProperty =
        AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsSearchOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What to search for (two-way); a search starts 300 ms after the last change. A host with its own search field can set this with the bar closed.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<PdfViewer, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay, coerce: static (_, value) => value ?? string.Empty);

    /// <summary>Matches only text of the same case.</summary>
    public static readonly StyledProperty<bool> IsMatchCaseProperty =
        AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsMatchCase));

    /// <summary>Matches only whole words. There are no word boundaries in Chinese, Japanese and Korean, so it does not apply to them.</summary>
    public static readonly StyledProperty<bool> IsWholeWordProperty =
        AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsWholeWord));

    /// <summary>How many results were found so far; it grows while <see cref="IsSearching"/> is true.</summary>
    public static readonly DirectProperty<PdfViewer, int> SearchMatchCountProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, int>(nameof(SearchMatchCount), o => o.SearchMatchCount);

    /// <summary>The current result (two-way), from 0 in document order, or -1 for none. Setting it scrolls to that result. While a scan wraps around to earlier pages, the same result's number can grow.</summary>
    public static readonly StyledProperty<int> CurrentSearchMatchProperty =
        AvaloniaProperty.Register<PdfViewer, int>(nameof(CurrentSearchMatch), -1, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceCurrentSearchMatch);

    /// <summary>True while pages are being scanned.</summary>
    public static readonly DirectProperty<PdfViewer, bool> IsSearchingProperty =
        AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsSearching), o => o.IsSearching);

    /// <inheritdoc cref="IsSearchOpenProperty"/>
    public bool IsSearchOpen { get => GetValue(IsSearchOpenProperty); set => SetValue(IsSearchOpenProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="IsMatchCaseProperty"/>
    public bool IsMatchCase { get => GetValue(IsMatchCaseProperty); set => SetValue(IsMatchCaseProperty, value); }

    /// <inheritdoc cref="IsWholeWordProperty"/>
    public bool IsWholeWord { get => GetValue(IsWholeWordProperty); set => SetValue(IsWholeWordProperty, value); }

    /// <inheritdoc cref="SearchMatchCountProperty"/>
    public int SearchMatchCount
    {
        get => _searchMatchCount;
        private set => SetAndRaise(SearchMatchCountProperty, ref _searchMatchCount, value);
    }

    /// <inheritdoc cref="CurrentSearchMatchProperty"/>
    public int CurrentSearchMatch { get => GetValue(CurrentSearchMatchProperty); set => SetValue(CurrentSearchMatchProperty, value); }

    /// <inheritdoc cref="IsSearchingProperty"/>
    public bool IsSearching
    {
        get => _isSearching;
        private set => SetAndRaise(IsSearchingProperty, ref _isSearching, value);
    }

    #endregion

    private readonly PdfToolbarToggle _searchToggle;
    private readonly PdfSearchResults _results = new();
    private PdfSearchLayer _searchLayer = null!;
    private PresenceHost? _searchHost;
    private PdfSearchBar? _searchBar;
    private PdfPageTextCache? _textCache;
    private PdfSearcher? _searcher;
    private CancellationTokenSource? _searchRun;
    private DispatcherTimer? _searchTimer;
    private int _searchMatchCount;
    private bool _isSearching;
    private bool _renumberingMatch;
    private bool _searchSuspended;
    private bool _resumeSearch;
    private int _searchSession;

    /// <summary>The search bar, once it has been opened (tests).</summary>
    internal PdfSearchBar? SearchBar => _searchBar;

    /// <summary>The results so far.</summary>
    internal PdfSearchResults SearchResults => _results;

    /// <summary>The current result, or null.</summary>
    internal PdfSearchMatch? CurrentMatch => CurrentSearchMatch is >= 0 and var index && index < _results.Count ? _results[index] : null;

    /// <summary>The text changed and the wait before searching is still running.</summary>
    internal bool IsSearchPending => _searchTimer?.IsEnabled == true;

    /// <summary>The cache of page text, for what the tests want to see of it.</summary>
    internal PdfPageTextCache? TextCache => _textCache;

    /// <summary>Goes to the next result, around to the first after the last. With the wait for typing still running, it searches now.</summary>
    public void SearchNext() => StepSearch(1);

    /// <summary>Goes to the previous result, around to the last before the first.</summary>
    public void SearchPrevious() => StepSearch(-1);

    /// <summary>Ends the search and empties <see cref="SearchText"/>; the bar stays as it is.</summary>
    public void ClearSearch()
    {
        _searchTimer?.Stop();
        SetCurrentValue(SearchTextProperty, string.Empty);
        ResetSearch();
    }

    private static int CoerceCurrentSearchMatch(AvaloniaObject target, int value)
    {
        var count = ((PdfViewer)target)._searchMatchCount;
        return count == 0 ? -1 : Math.Clamp(value, -1, count - 1);
    }

    private void StepSearch(int direction)
    {
        if (IsSearchPending)
        {
            StartSearch();
            return;
        }
        var count = SearchMatchCount;
        if (count == 0)
        {
            return;
        }
        var current = CurrentSearchMatch;
        var next = current < 0 ? (direction > 0 ? 0 : count - 1) : (current + direction + count) % count;
        if (next == current)
        {
            ScrollToCurrentMatch();
        }
        else
        {
            SetCurrentValue(CurrentSearchMatchProperty, next);
        }
    }

    private void OnSearchOpenChanged(bool open)
    {
        PseudoClasses.Set(SearchOpenPseudoClass, open);
        _searchToggle.IsActive = open;
        if (open)
        {
            EnsureSearchBar();
            _searchHost!.IsPresent = HasPages;
            if (HasPages)
            {
                _searchBar!.FocusField();
            }
            if (_searchSuspended)
            {
                _searchSuspended = false;
                StartSearch();
            }
            return;
        }
        // Focus goes before the bar does, or it would be lost with it.
        if (_searchBar is { HoldsFocus: true })
        {
            FocusViewport(NavigationMethod.Tab);
        }
        if (_searchHost is { } host)
        {
            host.IsPresent = false;
        }
        _searchTimer?.Stop();
        _searchSuspended = SearchText.Length > 0;
        ResetSearch();
    }

    private void EnsureSearchBar()
    {
        if (_searchHost is not null)
        {
            return;
        }
        _searchBar = new PdfSearchBar(this);
        _searchBar.SetNarrow(_sidebarFloats);
        _searchHost = new PresenceHost
        {
            IsPresent = false,
            Mode = PresenceMode.Drop,
            Offset = 8,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _searchBar
        };
        PlaceSearchBar();
        var overlay = EnsureOverlay();
        // Over the pages and the floating sidebar, under the state cover.
        overlay.Children.Insert(_stateLayer is { } state ? overlay.Children.IndexOf(state) : overlay.Children.Count, _searchHost);
    }

    /// <summary>At the top right; in a narrow viewer across the width.</summary>
    private void PlaceSearchBar()
    {
        if (_searchHost is { } host)
        {
            host.HorizontalAlignment = _sidebarFloats ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
            host.Margin = _sidebarFloats ? new Thickness(8) : new Thickness(0, 8, 16, 0);
            _searchBar?.SetNarrow(_sidebarFloats);
        }
    }

    private void OnSearchTextChanged()
    {
        _searchSuspended = false;
        if (SearchText.Length == 0)
        {
            _searchTimer?.Stop();
            ResetSearch();
            return;
        }
        if (_searchTimer is null)
        {
            _searchTimer = new DispatcherTimer(s_searchDebounce, DispatcherPriority.Normal, (_, _) => StartSearch());
        }
        // Restarting is the debounce.
        _searchTimer.Stop();
        _searchTimer.Start();
        _searchBar?.Sync();
        _searchBar?.Announce();
    }

    /// <summary>Stops a scan and forgets its results.</summary>
    private void ResetSearch()
    {
        ClearResults();
        IsSearching = false;
        _searchBar?.Sync();
        _searchBar?.Announce();
    }

    private void ClearResults()
    {
        StopScan();
        _results.Reset(0);
        SearchMatchCount = 0;
        SetCurrentMatchQuietly(-1);
        _searchLayer.Invalidate();
    }

    private void StopScan()
    {
        _searchSession++;
        _searchRun?.Cancel();
        _searchRun?.Dispose();
        _searchRun = null;
    }

    private void StartSearch()
    {
        _searchTimer?.Stop();
        var query = SearchText;
        var options = new PdfSearchOptions(IsMatchCase, IsWholeWord);
        if (EffectivePageSource is not PdfDocument document || _textCache is null || document.PageCount == 0
            || PdfSearcher.PrepareQuery(query, options.MatchCase).Length == 0)
        {
            ResetSearch();
            return;
        }
        ClearResults();
        _searcher ??= new PdfSearcher(_textCache, document.PageCount);
        var start = Math.Clamp(CurrentPageIndex, 0, document.PageCount - 1);
        _results.Reset(start);
        _searchRun = new CancellationTokenSource();
        IsSearching = true;
        _searchBar?.Sync();
        _searchBar?.Announce();
        _ = ScanAsync(_searcher, query, options, start, _searchSession, _searchRun.Token);
    }

    /// <summary>The scan runs on the thread pool; each result comes back through the dispatcher, in order, and is dropped if another search has begun.</summary>
    private async Task ScanAsync(PdfSearcher searcher, string query, PdfSearchOptions options, int start, int session, CancellationToken token)
    {
        try
        {
            await searcher.ScanAsync(query, options, start, (page, matches) =>
            {
                if (matches.Length > 0)
                {
                    Dispatcher.UIThread.Post(() => OnPageSearched(session, page, matches), DispatcherPriority.Background);
                }
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A new search, or the document is gone.
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Pages that cannot be read count as empty, so this is a scan that broke: what it found so far stands.
        }
        Dispatcher.UIThread.Post(() => OnScanFinished(session), DispatcherPriority.Background);
    }

    private void OnPageSearched(int session, int page, PdfSearchMatch[] matches)
    {
        if (session != _searchSession)
        {
            return;
        }
        var moved = _results.Add(page, matches);
        var wasEmpty = SearchMatchCount == 0;
        SearchMatchCount = _results.Count;
        if (wasEmpty)
        {
            // The first find of a scan is the current result: the view goes there.
            SetCurrentMatchQuietly(0);
            ScrollToCurrentMatch();
        }
        else if (moved > 0 && CurrentSearchMatch >= 0)
        {
            SetCurrentMatchQuietly(CurrentSearchMatch + moved);
        }
        _searchLayer.Invalidate();
        _searchBar?.Sync();
    }

    private void OnScanFinished(int session)
    {
        if (session == _searchSession)
        {
            IsSearching = false;
            _searchBar?.Sync();
            _searchBar?.Announce();
        }
    }

    /// <summary>Changes the number of the current result without going anywhere: the result stays, or there is none.</summary>
    private void SetCurrentMatchQuietly(int index)
    {
        _renumberingMatch = true;
        SetCurrentValue(CurrentSearchMatchProperty, index);
        _renumberingMatch = false;
    }

    private void OnCurrentSearchMatchChanged()
    {
        if (!_renumberingMatch)
        {
            ScrollToCurrentMatch();
            _searchBar?.Announce();
        }
        _searchLayer.Invalidate();
        _searchBar?.Sync();
    }

    private void ScrollToCurrentMatch()
    {
        if (CurrentMatch is not { } match)
        {
            return;
        }
        if (match.Rects.Length == 0)
        {
            GoToPage(match.PageIndex);
            return;
        }
        // With some room around it: the search bar floats over the top of the view.
        var margin = SearchScrollMargin / Math.Max(Zoom, 0.01);
        ScrollIntoView(match.PageIndex, match.Bounds.Inflate(margin));
    }

    private const double SearchScrollMargin = 72;

    /// <summary>The pages changed: the search starts over on the new ones, if there was one.</summary>
    private void ResetForPages()
    {
        _textCache?.Dispose();
        _textCache = EffectivePageSource is PdfDocument document ? new PdfPageTextCache(document) : null;
        _searcher = null;
        _linkLayer.Reset(EffectivePageSource as PdfDocument);
        _selectionLayer.Clear();
        ClearLinkTip();
        _searchToggle.IsEnabled = HasPages;
        _searchToggle.IsVisible = HasPages;
        if (_searchHost is { } host)
        {
            host.IsPresent = IsSearchOpen && HasPages;
        }
        ResetSearch();
        if (SearchText.Length > 0 && !_searchSuspended && _textCache is not null)
        {
            StartSearch();
        }
    }

    /// <summary>A scan does not go on behind a viewer out of the tree; it picks up again on the way back.</summary>
    private void PauseSearch()
    {
        _searchTimer?.Stop();
        if (IsSearching)
        {
            _resumeSearch = true;
            ResetSearch();
        }
    }

    private void ResumeSearch()
    {
        if (_resumeSearch)
        {
            _resumeSearch = false;
            StartSearch();
        }
    }

    private bool HandleSearchKey(KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        var command = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && (modifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) == 0;
        if (command && e.Key == Key.F && Status == PdfViewerStatus.Ready)
        {
            if (IsSearchOpen)
            {
                _searchBar?.FocusField();
            }
            else
            {
                SetCurrentValue(IsSearchOpenProperty, true);
            }
            return true;
        }
        var shiftOnly = modifiers == KeyModifiers.Shift;
        if (e.Key == Key.F3 && (modifiers == KeyModifiers.None || shiftOnly))
        {
            StepSearch(shiftOnly ? -1 : 1);
            return true;
        }
        // Enter belongs to the page box and to buttons; it only means "next" from the field and from the pages.
        if (e.Key == Key.Enter && (modifiers == KeyModifiers.None || shiftOnly) && SearchText.Length > 0
            && IsSearchOpen && !IsTextFieldOutsideSearch(e.Source))
        {
            StepSearch(shiftOnly ? -1 : 1);
            return true;
        }
        return false;
    }

    private bool IsTextFieldOutsideSearch(object? source) =>
        IsInTextField(source) && !(source is Visual visual && _searchBar is { } bar && (ReferenceEquals(visual, bar) || bar.IsVisualAncestorOf(visual)));

    private static bool IsInTextField(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox)
            {
                return true;
            }
        }
        return false;
    }
}

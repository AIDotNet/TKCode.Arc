// Ported from vscode (src/vs/editor/contrib/find/browser/findState.ts, commit b57d5c9e), MIT license.
// Differences:
//  - INewFindReplaceState is the record NewFindReplaceState; a member left null is upstream's undefined ("no change").
//    SearchScope needs three states (no change, clear, set): it counts as given once it is initialized, also to null
//    (SearchScopeIsSet).
//  - the generic filters (used by the notebook find widget) are not ported.
//  - the change event is a class with init-only flags instead of an object literal; the emitter is Core/Emitter.

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>What a change of <see cref="FindReplaceState"/> touched (upstream's FindReplaceStateChangedEvent).</summary>
internal sealed class FindReplaceStateChangedEvent
{
    public bool MoveCursor { get; set; }

    public bool UpdateHistory { get; set; }

    public bool SearchString { get; set; }

    public bool ReplaceString { get; set; }

    public bool IsRevealed { get; set; }

    public bool IsReplaceRevealed { get; set; }

    public bool IsRegex { get; set; }

    public bool WholeWord { get; set; }

    public bool MatchCase { get; set; }

    public bool PreserveCase { get; set; }

    public bool SearchScope { get; set; }

    public bool MatchesPosition { get; set; }

    public bool MatchesCount { get; set; }

    public bool CurrentMatch { get; set; }

    public bool Loop { get; set; }

    public bool IsSearching { get; set; }
}

/// <summary>An override of a find option that wins over the option until anything else changes (upstream's FindOptionOverride).</summary>
internal enum FindOptionOverride
{
    NotSet = 0,
    True = 1,
    False = 2,
}

/// <summary>The members of a <see cref="FindReplaceState"/> to change; null leaves one as it is (upstream's INewFindReplaceState).</summary>
internal sealed record NewFindReplaceState
{
    private readonly IReadOnlyList<TextRange>? _searchScope;

    public string? SearchString { get; init; }

    public string? ReplaceString { get; init; }

    public bool? IsRevealed { get; init; }

    public bool? IsReplaceRevealed { get; init; }

    public bool? IsRegex { get; init; }

    public FindOptionOverride? IsRegexOverride { get; init; }

    public bool? WholeWord { get; init; }

    public FindOptionOverride? WholeWordOverride { get; init; }

    public bool? MatchCase { get; init; }

    public FindOptionOverride? MatchCaseOverride { get; init; }

    public bool? PreserveCase { get; init; }

    public FindOptionOverride? PreserveCaseOverride { get; init; }

    /// <summary>The ranges to search in; setting it (also to null, which searches everywhere) changes the scope.</summary>
    public IReadOnlyList<TextRange>? SearchScope
    {
        get => _searchScope;
        init
        {
            _searchScope = value;
            SearchScopeIsSet = true;
        }
    }

    /// <summary>Whether <see cref="SearchScope"/> was given (upstream: it is not undefined).</summary>
    public bool SearchScopeIsSet { get; private init; }

    public bool? Loop { get; init; }

    public bool? IsSearching { get; init; }
}

/// <summary>
/// The state of find and replace in one editor (upstream's FindReplaceState): the search and replace strings, the
/// options, whether the widget shows, the scope and the match info the find model reports.
/// </summary>
internal sealed class FindReplaceState : IDisposable
{
    private readonly Emitter<FindReplaceStateChangedEvent> _onFindReplaceStateChange = new();
    private string _searchString = string.Empty;
    private string _replaceString = string.Empty;
    private bool _isRevealed;
    private bool _isReplaceRevealed;
    private bool _isRegex;
    private FindOptionOverride _isRegexOverride = FindOptionOverride.NotSet;
    private bool _wholeWord;
    private FindOptionOverride _wholeWordOverride = FindOptionOverride.NotSet;
    private bool _matchCase;
    private FindOptionOverride _matchCaseOverride = FindOptionOverride.NotSet;
    private bool _preserveCase;
    private FindOptionOverride _preserveCaseOverride = FindOptionOverride.NotSet;
    private IReadOnlyList<TextRange>? _searchScope;
    private int _matchesPosition;
    private int _matchesCount;
    private TextRange? _currentMatch;
    private bool _loop = true;
    private bool _isSearching;

    /// <summary>Raised after every change (upstream's onFindReplaceStateChange).</summary>
    public event Action<FindReplaceStateChangedEvent>? FindReplaceStateChange
    {
        add => _onFindReplaceStateChange.Add(value!);
        remove => _onFindReplaceStateChange.Remove(value);
    }

    public string SearchString => _searchString;

    public string ReplaceString => _replaceString;

    public bool IsRevealed => _isRevealed;

    public bool IsReplaceRevealed => _isReplaceRevealed;

    public bool IsRegex => EffectiveOptionValue(_isRegexOverride, _isRegex);

    public bool WholeWord => EffectiveOptionValue(_wholeWordOverride, _wholeWord);

    public bool MatchCase => EffectiveOptionValue(_matchCaseOverride, _matchCase);

    public bool PreserveCase => EffectiveOptionValue(_preserveCaseOverride, _preserveCase);

    public bool ActualIsRegex => _isRegex;

    public bool ActualWholeWord => _wholeWord;

    public bool ActualMatchCase => _matchCase;

    public bool ActualPreserveCase => _preserveCase;

    public IReadOnlyList<TextRange>? SearchScope => _searchScope;

    /// <summary>The 1-based index of the current match, or 0 when the cursor is not on a match.</summary>
    public int MatchesPosition => _matchesPosition;

    public int MatchesCount => _matchesCount;

    public TextRange? CurrentMatch => _currentMatch;

    public bool Loop => _loop;

    public bool IsSearching => _isSearching;

    public void Dispose() => _onFindReplaceStateChange.Clear();

    /// <summary>Sets the match info (upstream's changeMatchInfo); <paramref name="currentMatch"/> null leaves the current match.</summary>
    public void ChangeMatchInfo(int matchesPosition, int matchesCount, TextRange? currentMatch)
    {
        var changeEvent = new FindReplaceStateChangedEvent();
        var somethingChanged = false;

        if (matchesCount == 0)
        {
            matchesPosition = 0;
        }
        if (matchesPosition > matchesCount)
        {
            matchesPosition = matchesCount;
        }

        if (_matchesPosition != matchesPosition)
        {
            _matchesPosition = matchesPosition;
            changeEvent.MatchesPosition = true;
            somethingChanged = true;
        }
        if (_matchesCount != matchesCount)
        {
            _matchesCount = matchesCount;
            changeEvent.MatchesCount = true;
            somethingChanged = true;
        }

        if (currentMatch is { } match)
        {
            if (_currentMatch != match)
            {
                _currentMatch = match;
                changeEvent.CurrentMatch = true;
                somethingChanged = true;
            }
        }

        if (somethingChanged)
        {
            _onFindReplaceStateChange.Fire(changeEvent);
        }
    }

    /// <summary>Changes the given members and raises one event (upstream's change).</summary>
    public void Change(NewFindReplaceState newState, bool moveCursor, bool updateHistory = true)
    {
        var changeEvent = new FindReplaceStateChangedEvent { MoveCursor = moveCursor, UpdateHistory = updateHistory };
        var somethingChanged = false;

        var oldEffectiveIsRegex = IsRegex;
        var oldEffectiveWholeWords = WholeWord;
        var oldEffectiveMatchCase = MatchCase;
        var oldEffectivePreserveCase = PreserveCase;

        if (newState.SearchString is { } searchString)
        {
            if (!string.Equals(_searchString, searchString, StringComparison.Ordinal))
            {
                _searchString = searchString;
                changeEvent.SearchString = true;
                somethingChanged = true;
            }
        }
        if (newState.ReplaceString is { } replaceString)
        {
            if (!string.Equals(_replaceString, replaceString, StringComparison.Ordinal))
            {
                _replaceString = replaceString;
                changeEvent.ReplaceString = true;
                somethingChanged = true;
            }
        }
        if (newState.IsRevealed is { } isRevealed)
        {
            if (_isRevealed != isRevealed)
            {
                _isRevealed = isRevealed;
                changeEvent.IsRevealed = true;
                somethingChanged = true;
            }
        }
        if (newState.IsReplaceRevealed is { } isReplaceRevealed)
        {
            if (_isReplaceRevealed != isReplaceRevealed)
            {
                _isReplaceRevealed = isReplaceRevealed;
                changeEvent.IsReplaceRevealed = true;
                somethingChanged = true;
            }
        }
        if (newState.IsRegex is { } isRegex)
        {
            _isRegex = isRegex;
        }
        if (newState.WholeWord is { } wholeWord)
        {
            _wholeWord = wholeWord;
        }
        if (newState.MatchCase is { } matchCase)
        {
            _matchCase = matchCase;
        }
        if (newState.PreserveCase is { } preserveCase)
        {
            _preserveCase = preserveCase;
        }
        if (newState.SearchScopeIsSet)
        {
            // upstream: !newState.searchScope?.every(n => this._searchScope?.some(e => !Range.equalsRange(e, n)))
            var keep = newState.SearchScope is { } newScopes
                && newScopes.All(newSearchScope => _searchScope is { } existing && existing.Any(e => e != newSearchScope));
            if (!keep)
            {
                _searchScope = newState.SearchScope;
                changeEvent.SearchScope = true;
                somethingChanged = true;
            }
        }
        if (newState.Loop is { } loop)
        {
            if (_loop != loop)
            {
                _loop = loop;
                changeEvent.Loop = true;
                somethingChanged = true;
            }
        }

        if (newState.IsSearching is { } isSearching)
        {
            if (_isSearching != isSearching)
            {
                _isSearching = isSearching;
                changeEvent.IsSearching = true;
                somethingChanged = true;
            }
        }

        // Overrides get set when they explicitly come in and get reset anytime something else changes
        _isRegexOverride = newState.IsRegexOverride ?? FindOptionOverride.NotSet;
        _wholeWordOverride = newState.WholeWordOverride ?? FindOptionOverride.NotSet;
        _matchCaseOverride = newState.MatchCaseOverride ?? FindOptionOverride.NotSet;
        _preserveCaseOverride = newState.PreserveCaseOverride ?? FindOptionOverride.NotSet;

        if (oldEffectiveIsRegex != IsRegex)
        {
            somethingChanged = true;
            changeEvent.IsRegex = true;
        }
        if (oldEffectiveWholeWords != WholeWord)
        {
            somethingChanged = true;
            changeEvent.WholeWord = true;
        }
        if (oldEffectiveMatchCase != MatchCase)
        {
            somethingChanged = true;
            changeEvent.MatchCase = true;
        }

        if (oldEffectivePreserveCase != PreserveCase)
        {
            somethingChanged = true;
            changeEvent.PreserveCase = true;
        }

        if (somethingChanged)
        {
            _onFindReplaceStateChange.Fire(changeEvent);
        }
    }

    public bool CanNavigateBack() => CanNavigateInLoop() || MatchesPosition != 1;

    public bool CanNavigateForward() => CanNavigateInLoop() || MatchesPosition < MatchesCount;

    private bool CanNavigateInLoop() => _loop || MatchesCount >= FindModelBoundToEditorModel.MatchesLimit;

    private static bool EffectiveOptionValue(FindOptionOverride @override, bool value) => @override switch
    {
        FindOptionOverride.True => true,
        FindOptionOverride.False => false,
        _ => value,
    };
}

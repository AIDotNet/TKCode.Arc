// Ported from vscode (src/vs/editor/contrib/find/browser/findState.ts: FindOptionOverride, INewFindReplaceState,
// effectiveOptionValue and the part of FindReplaceState the multi-cursor actions read and write; commit b57d5c9e),
// MIT license.
// Differences:
//  - only the members multicursor.ts uses (search string, revealed, regex / whole word / match case with their
//    overrides, search scope; no replace string, preserve case, loop, match info or filters);
//  - the find controller (CommonFindController) is the interface IMultiCursorFindController, so that the multi-cursor
//    feature works with or without the find widget: an editor without one gets a StandaloneMultiCursorFindController
//    that keeps this state (upstream creates a temporary CommonFindController for the action);
//  - the change event carries only the flags the multi-cursor feature reads.

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>Whether an option is forced on or off for a while (upstream's FindOptionOverride).</summary>
internal enum FindOptionOverride
{
    NotSet = 0,
    True = 1,
    False = 2,
}

/// <summary>The parts of the find state a change sets (upstream's INewFindReplaceState); null leaves a part as it is.</summary>
internal sealed record NewFindState
{
    public string? SearchString { get; init; }

    public bool? IsRevealed { get; init; }

    public bool? IsRegex { get; init; }

    public FindOptionOverride? IsRegexOverride { get; init; }

    public bool? WholeWord { get; init; }

    public FindOptionOverride? WholeWordOverride { get; init; }

    public bool? MatchCase { get; init; }

    public FindOptionOverride? MatchCaseOverride { get; init; }

    /// <summary>The search scope; set <see cref="SetSearchScope"/> to change it (null clears it).</summary>
    public IReadOnlyList<TextRange>? SearchScope { get; init; }

    /// <summary>Whether <see cref="SearchScope"/> is part of the change.</summary>
    public bool SetSearchScope { get; init; }
}

/// <summary>What a find state change changed (upstream's FindReplaceStateChangedEvent, reduced).</summary>
internal sealed record FindStateChangedEvent(bool MoveCursor, bool SearchString, bool IsRevealed, bool IsRegex, bool WholeWord, bool MatchCase, bool SearchScope);

/// <summary>The find widget's state as far as the multi-cursor actions use it (upstream's FindReplaceState).</summary>
internal sealed class MultiCursorFindState
{
    private string _searchString = string.Empty;
    private bool _isRevealed;
    private bool _isRegex;
    private FindOptionOverride _isRegexOverride;
    private bool _wholeWord;
    private FindOptionOverride _wholeWordOverride;
    private bool _matchCase;
    private FindOptionOverride _matchCaseOverride;
    private IReadOnlyList<TextRange>? _searchScope;

    public string SearchString => _searchString;

    public bool IsRevealed => _isRevealed;

    public bool IsRegex => EffectiveOptionValue(_isRegexOverride, _isRegex);

    public bool WholeWord => EffectiveOptionValue(_wholeWordOverride, _wholeWord);

    public bool MatchCase => EffectiveOptionValue(_matchCaseOverride, _matchCase);

    public bool ActualIsRegex => _isRegex;

    public bool ActualWholeWord => _wholeWord;

    public bool ActualMatchCase => _matchCase;

    public IReadOnlyList<TextRange>? SearchScope => _searchScope;

    /// <summary>Raised after a change that changed something (upstream's onFindReplaceStateChange).</summary>
    public event Action<FindStateChangedEvent>? FindReplaceStateChange;

    private static bool EffectiveOptionValue(FindOptionOverride @override, bool value) => @override switch
    {
        FindOptionOverride.True => true,
        FindOptionOverride.False => false,
        _ => value,
    };

    /// <summary>Applies a change (upstream's change); overrides not in it are reset.</summary>
    public void Change(NewFindState newState, bool moveCursor)
    {
        var searchStringChanged = false;
        var isRevealedChanged = false;
        var searchScopeChanged = false;
        var somethingChanged = false;

        var oldEffectiveIsRegex = IsRegex;
        var oldEffectiveWholeWords = WholeWord;
        var oldEffectiveMatchCase = MatchCase;

        if (newState.SearchString is { } searchString && _searchString != searchString)
        {
            _searchString = searchString;
            searchStringChanged = true;
            somethingChanged = true;
        }

        if (newState.IsRevealed is { } isRevealed && _isRevealed != isRevealed)
        {
            _isRevealed = isRevealed;
            isRevealedChanged = true;
            somethingChanged = true;
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

        if (newState.SetSearchScope)
        {
            var same = newState.SearchScope is { } a && _searchScope is { } b && a.Count == b.Count && a.SequenceEqual(b);
            if (!same)
            {
                _searchScope = newState.SearchScope;
                searchScopeChanged = true;
                somethingChanged = true;
            }
        }

        // Overrides get set when they explicitly come in and get reset anytime something else changes
        _isRegexOverride = newState.IsRegexOverride ?? FindOptionOverride.NotSet;
        _wholeWordOverride = newState.WholeWordOverride ?? FindOptionOverride.NotSet;
        _matchCaseOverride = newState.MatchCaseOverride ?? FindOptionOverride.NotSet;

        var isRegexChanged = oldEffectiveIsRegex != IsRegex;
        var wholeWordChanged = oldEffectiveWholeWords != WholeWord;
        var matchCaseChanged = oldEffectiveMatchCase != MatchCase;
        somethingChanged = somethingChanged || isRegexChanged || wholeWordChanged || matchCaseChanged;

        if (somethingChanged)
        {
            FindReplaceStateChange?.Invoke(new FindStateChangedEvent(
                moveCursor, searchStringChanged, isRevealedChanged, isRegexChanged, wholeWordChanged, matchCaseChanged, searchScopeChanged));
        }
    }
}

/// <summary>
/// What the multi-cursor actions need from the find controller (upstream's CommonFindController): its state, and a
/// way to point the user at the find options that apply.
/// </summary>
internal interface IMultiCursorFindController
{
    /// <summary>The find state (upstream's getState).</summary>
    MultiCursorFindState GetState();

    /// <summary>Shows the find options briefly, when the find widget is hidden (upstream's highlightFindOptions).</summary>
    void HighlightFindOptions();
}

/// <summary>A find controller without a widget: the state only (an editor without the find feature).</summary>
internal sealed class StandaloneMultiCursorFindController : IMultiCursorFindController
{
    private readonly MultiCursorFindState _state = new();

    /// <summary>How many times <see cref="HighlightFindOptions"/> was called (tests: upstream spies on it).</summary>
    public int HighlightFindOptionsCount { get; private set; }

    public MultiCursorFindState GetState() => _state;

    public void HighlightFindOptions() => HighlightFindOptionsCount++;
}

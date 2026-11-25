// Ported from vscode (src/vs/editor/contrib/find/browser/findDecorations.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the decoration options are recognized by their description (upstream compares the registered option objects by
//    reference; the decorations tree here may hand out an equal copy);
//  - the overview ruler and minimap colors are theme color ids (editorOverviewRuler.findMatchForeground,
//    minimap.findMatchHighlight) that the view resolves;
//  - the deprecated getFindScope is kept (the find model still uses it, as upstream).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Find;

/// <summary>The decorations of find: every match, the current one, its line and the scope (upstream's FindDecorations).</summary>
internal sealed class FindDecorations : IDisposable
{
    /// <summary>The theme color of matches in the overview ruler (upstream's overviewRulerFindMatchForeground).</summary>
    public const string OverviewRulerFindMatchForeground = "editorOverviewRuler.findMatchForeground";

    /// <summary>The theme color of matches in the minimap (upstream's minimapFindMatch).</summary>
    public const string MinimapFindMatch = "minimap.findMatchHighlight";

    public static readonly ModelDecorationOptions CurrentFindMatchDecoration = new("current-find-match")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ZIndex = 13,
        ClassName = "currentFindMatch",
        InlineClassName = "currentFindMatchInline",
        ShowIfCollapsed = true,
        OverviewRuler = new ModelDecorationOverviewRulerOptions
        {
            Color = DecorationColor.FromTheme(OverviewRulerFindMatchForeground),
            Position = OverviewRulerLane.Center,
        },
        Minimap = new ModelDecorationMinimapOptions
        {
            Color = DecorationColor.FromTheme(MinimapFindMatch),
            Position = MinimapPosition.Inline,
        },
    };

    public static readonly ModelDecorationOptions FindMatchDecoration = new("find-match")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ZIndex = 10,
        ClassName = "findMatch",
        InlineClassName = "findMatchInline",
        ShowIfCollapsed = true,
        OverviewRuler = new ModelDecorationOverviewRulerOptions
        {
            Color = DecorationColor.FromTheme(OverviewRulerFindMatchForeground),
            Position = OverviewRulerLane.Center,
        },
        Minimap = new ModelDecorationMinimapOptions
        {
            Color = DecorationColor.FromTheme(MinimapFindMatch),
            Position = MinimapPosition.Inline,
        },
    };

    public static readonly ModelDecorationOptions FindMatchNoOverviewDecoration = new("find-match-no-overview")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = "findMatch",
        ShowIfCollapsed = true,
    };

    private static readonly ModelDecorationOptions FindMatchOnlyOverviewDecoration = new("find-match-only-overview")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        OverviewRuler = new ModelDecorationOverviewRulerOptions
        {
            Color = DecorationColor.FromTheme(OverviewRulerFindMatchForeground),
            Position = OverviewRulerLane.Center,
        },
    };

    private static readonly ModelDecorationOptions RangeHighlightDecoration = new("find-range-highlight")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = "rangeHighlight",
        IsWholeLine = true,
    };

    private static readonly ModelDecorationOptions FindScopeDecoration = new("find-scope")
    {
        ClassName = "findScope",
        IsWholeLine = true,
    };

    private readonly IFindEditor _editor;
    private string[] _decorations = [];
    private string[] _overviewRulerApproximateDecorations = [];
    private string[] _findScopeDecorationIds = [];
    private string? _rangeHighlightDecorationId;
    private string? _highlightedDecorationId;
    private TextPosition _startPosition;

    public FindDecorations(IFindEditor editor)
    {
        _editor = editor;
        _startPosition = _editor.GetPosition();
    }

    private TextModel Model => _editor.GetModel()!;

    public void Dispose()
    {
        _editor.RemoveDecorations(AllDecorations());

        _decorations = [];
        _overviewRulerApproximateDecorations = [];
        _findScopeDecorationIds = [];
        _rangeHighlightDecorationId = null;
        _highlightedDecorationId = null;
    }

    public void Reset()
    {
        _decorations = [];
        _overviewRulerApproximateDecorations = [];
        _findScopeDecorationIds = [];
        _rangeHighlightDecorationId = null;
        _highlightedDecorationId = null;
    }

    public int GetCount() => _decorations.Length;

    /// <summary>The first find scope (deprecated upstream: use <see cref="GetFindScopes"/>).</summary>
    public TextRange? GetFindScope()
    {
        if (_findScopeDecorationIds.Length > 0)
        {
            return Model.GetDecorationRange(_findScopeDecorationIds[0]);
        }

        return null;
    }

    public IReadOnlyList<TextRange>? GetFindScopes()
    {
        if (_findScopeDecorationIds.Length > 0)
        {
            var scopes = new List<TextRange>();
            foreach (var id in _findScopeDecorationIds)
            {
                if (Model.GetDecorationRange(id) is { } range)
                {
                    scopes.Add(range);
                }
            }

            if (scopes.Count > 0)
            {
                return scopes;
            }
        }

        return null;
    }

    public TextPosition GetStartPosition() => _startPosition;

    public void SetStartPosition(TextPosition newStartPosition)
    {
        _startPosition = newStartPosition;
        SetCurrentFindMatch(null);
    }

    private int GetDecorationIndex(string decorationId)
    {
        var index = Array.IndexOf(_decorations, decorationId);
        if (index >= 0)
        {
            return index + 1;
        }

        return 1;
    }

    public TextRange? GetDecorationRangeAt(int index)
    {
        var decorationId = index >= 0 && index < _decorations.Length ? _decorations[index] : null;
        if (decorationId is not null)
        {
            return Model.GetDecorationRange(decorationId);
        }

        return null;
    }

    public int GetCurrentMatchesPosition(TextRange desiredRange)
    {
        var candidates = Model.GetDecorationsInRange(desiredRange);
        foreach (var candidate in candidates)
        {
            if (IsMatchDecoration(candidate.Options))
            {
                return GetDecorationIndex(candidate.Id);
            }
        }

        // We don't know the current match position, so returns zero to show '?' in find widget
        return 0;
    }

    private static bool IsMatchDecoration(ModelDecorationOptions options) =>
        ReferenceEquals(options, FindMatchDecoration)
        || ReferenceEquals(options, FindMatchNoOverviewDecoration)
        || ReferenceEquals(options, CurrentFindMatchDecoration)
        || options.Description is "find-match" or "find-match-no-overview" or "current-find-match";

    public int SetCurrentFindMatch(TextRange? nextMatch)
    {
        string? newCurrentDecorationId = null;
        var matchPosition = 0;
        if (nextMatch is { } next)
        {
            for (var i = 0; i < _decorations.Length; i++)
            {
                var range = Model.GetDecorationRange(_decorations[i]);
                if (range == next)
                {
                    newCurrentDecorationId = _decorations[i];
                    matchPosition = i + 1;
                    break;
                }
            }
        }

        if (_highlightedDecorationId is not null || newCurrentDecorationId is not null)
        {
            _editor.ChangeDecorations(changeAccessor =>
            {
                if (_highlightedDecorationId is not null)
                {
                    changeAccessor.ChangeDecorationOptions(_highlightedDecorationId, FindMatchDecoration);
                    _highlightedDecorationId = null;
                }
                if (newCurrentDecorationId is not null)
                {
                    _highlightedDecorationId = newCurrentDecorationId;
                    changeAccessor.ChangeDecorationOptions(_highlightedDecorationId, CurrentFindMatchDecoration);
                }
                if (_rangeHighlightDecorationId is not null)
                {
                    changeAccessor.RemoveDecoration(_rangeHighlightDecorationId);
                    _rangeHighlightDecorationId = null;
                }
                if (newCurrentDecorationId is not null)
                {
                    var rng = Model.GetDecorationRange(newCurrentDecorationId)!.Value;
                    if (rng.StartLineNumber != rng.EndLineNumber && rng.EndColumn == 1)
                    {
                        var lineBeforeEnd = rng.EndLineNumber - 1;
                        var lineBeforeEndMaxColumn = Model.GetLineMaxColumn(lineBeforeEnd);
                        rng = new TextRange(rng.StartLineNumber, rng.StartColumn, lineBeforeEnd, lineBeforeEndMaxColumn);
                    }

                    _rangeHighlightDecorationId = changeAccessor.AddDecoration(rng, RangeHighlightDecoration);
                }

                return true;
            });
        }

        return matchPosition;
    }

    public void Set(IReadOnlyList<FindMatch> findMatches, IReadOnlyList<TextRange>? findScopes)
    {
        _editor.ChangeDecorations(accessor =>
        {
            var findMatchesOptions = FindMatchDecoration;
            var newOverviewRulerApproximateDecorations = new List<ModelDeltaDecoration>();

            if (findMatches.Count > 1000)
            {
                // we go into a mode where the overview ruler gets "approximate" decorations
                // the reason is that the overview ruler paints all the decorations in the file and we don't want to cause freezes
                findMatchesOptions = FindMatchNoOverviewDecoration;

                // approximate a distance in lines where matches should be merged
                var lineCount = Model.GetLineCount();
                var height = _editor.GetLayoutHeight();
                var approxPixelsPerLine = height / lineCount;
                var mergeLinesDelta = approxPixelsPerLine > 0
                    ? Math.Max(2, (int)Math.Ceiling(3 / approxPixelsPerLine))
                    : int.MaxValue / 2;

                // merge decorations as much as possible
                var prevStartLineNumber = findMatches[0].Range.StartLineNumber;
                var prevEndLineNumber = findMatches[0].Range.EndLineNumber;
                for (var i = 1; i < findMatches.Count; i++)
                {
                    var range = findMatches[i].Range;
                    if (prevEndLineNumber + mergeLinesDelta >= range.StartLineNumber)
                    {
                        if (range.EndLineNumber > prevEndLineNumber)
                        {
                            prevEndLineNumber = range.EndLineNumber;
                        }
                    }
                    else
                    {
                        newOverviewRulerApproximateDecorations.Add(new ModelDeltaDecoration(
                            new TextRange(prevStartLineNumber, 1, prevEndLineNumber, 1), FindMatchOnlyOverviewDecoration));
                        prevStartLineNumber = range.StartLineNumber;
                        prevEndLineNumber = range.EndLineNumber;
                    }
                }

                newOverviewRulerApproximateDecorations.Add(new ModelDeltaDecoration(
                    new TextRange(prevStartLineNumber, 1, prevEndLineNumber, 1), FindMatchOnlyOverviewDecoration));
            }

            // Find matches
            var newFindMatchesDecorations = new ModelDeltaDecoration[findMatches.Count];
            for (var i = 0; i < findMatches.Count; i++)
            {
                newFindMatchesDecorations[i] = new ModelDeltaDecoration(findMatches[i].Range, findMatchesOptions);
            }

            _decorations = accessor.DeltaDecorations(_decorations, newFindMatchesDecorations);

            // Overview ruler approximate decorations
            _overviewRulerApproximateDecorations = accessor.DeltaDecorations(_overviewRulerApproximateDecorations, newOverviewRulerApproximateDecorations);

            // Range highlight
            if (_rangeHighlightDecorationId is not null)
            {
                accessor.RemoveDecoration(_rangeHighlightDecorationId);
                _rangeHighlightDecorationId = null;
            }

            // Find scope
            if (_findScopeDecorationIds.Length > 0)
            {
                foreach (var findScopeDecorationId in _findScopeDecorationIds)
                {
                    accessor.RemoveDecoration(findScopeDecorationId);
                }

                _findScopeDecorationIds = [];
            }
            if (findScopes is { Count: > 0 })
            {
                _findScopeDecorationIds = findScopes.Select(findScope => accessor.AddDecoration(findScope, FindScopeDecoration)).ToArray();
            }

            return true;
        });
    }

    public TextRange? MatchBeforePosition(TextPosition position)
    {
        if (_decorations.Length == 0)
        {
            return null;
        }
        for (var i = _decorations.Length - 1; i >= 0; i--)
        {
            var decorationId = _decorations[i];
            if (Model.GetDecorationRange(decorationId) is not { } r || r.EndLineNumber > position.LineNumber)
            {
                continue;
            }
            if (r.EndLineNumber < position.LineNumber)
            {
                return r;
            }
            if (r.EndColumn > position.Column)
            {
                continue;
            }

            return r;
        }

        return Model.GetDecorationRange(_decorations[^1]);
    }

    public TextRange? MatchAfterPosition(TextPosition position)
    {
        if (_decorations.Length == 0)
        {
            return null;
        }
        for (var i = 0; i < _decorations.Length; i++)
        {
            var decorationId = _decorations[i];
            if (Model.GetDecorationRange(decorationId) is not { } r || r.StartLineNumber < position.LineNumber)
            {
                continue;
            }
            if (r.StartLineNumber > position.LineNumber)
            {
                return r;
            }
            if (r.StartColumn < position.Column)
            {
                continue;
            }

            return r;
        }

        return Model.GetDecorationRange(_decorations[0]);
    }

    private string[] AllDecorations()
    {
        var result = new List<string>();
        result.AddRange(_decorations);
        result.AddRange(_overviewRulerApproximateDecorations);
        result.AddRange(_findScopeDecorationIds);
        if (_rangeHighlightDecorationId is not null)
        {
            result.Add(_rangeHighlightDecorationId);
        }

        return result.ToArray();
    }
}

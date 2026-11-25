// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetSession.ts: OneSnippet, ISnippetSessionInsertOptions,
// ISnippetEdit and SnippetSession, commit b57d5c9e), MIT license.
// Differences:
//  - the editor is an ISnippetEditor (the host's code editor or a test editor); decorations are changed through the
//    model with the editor's owner id, as upstream's editor.changeDecorations does;
//  - placeholder indexes stay int: OneSnippet.Merge computes the fractional indexes of the nested snippet's
//    placeholders in a table and renumbers every placeholder to 1, 2, ... at once (upstream writes the fractions into
//    the placeholders and renormalizes them in the same call, #279349), so the result is the same;
//  - ISnippetEdit is the record SnippetEdit; the insert options are the record SnippetSessionInsertOptions;
//  - the variable services (label, workspace, clock, random) come from the editor (ISnippetEditor.VariableServices);
//  - edits are made with the source "snippet" (upstream's EditSources.snippet()); there is no TextModelEditSource
//    argument;
//  - isSelectionWithinPlaceholders skips an index that a later snippet does not have (upstream would throw spreading
//    undefined), so that selection set counts as not within the placeholders.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>A choice placeholder that is the current tab stop, and where it is.</summary>
internal sealed record ActiveSnippetChoice(Choice Choice, TextRange Range);

/// <summary>One snippet inserted at one cursor: its placeholders, their decorations and the current tab stop (upstream's OneSnippet).</summary>
internal sealed class OneSnippet : IDisposable
{
    private static readonly ModelDecorationOptions s_active = new("snippet-placeholder-1") { Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, ClassName = SnippetDecorationClassNames.Placeholder };
    private static readonly ModelDecorationOptions s_inactive = new("snippet-placeholder-2") { Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, ClassName = SnippetDecorationClassNames.Placeholder };
    private static readonly ModelDecorationOptions s_activeFinal = new("snippet-placeholder-3") { Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, ClassName = SnippetDecorationClassNames.FinalPlaceholder };
    private static readonly ModelDecorationOptions s_inactiveFinal = new("snippet-placeholder-4") { Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, ClassName = SnippetDecorationClassNames.FinalPlaceholder };

    private readonly ISnippetEditor _editor;
    private readonly TextmateSnippet _snippet;
    private readonly string _snippetLineLeadingWhitespace;
    private Dictionary<Placeholder, string>? _placeholderDecorations;
    private List<List<Placeholder>> _placeholderGroups;
    private int _offset = -1;

    public OneSnippet(ISnippetEditor editor, TextmateSnippet snippet, string snippetLineLeadingWhitespace)
    {
        _editor = editor;
        _snippet = snippet;
        _snippetLineLeadingWhitespace = snippetLineLeadingWhitespace;
        _placeholderGroups = GroupBy(snippet.Placeholders);
        PlaceholderGroupsIdx = -1;
    }

    /// <summary>The index of the current tab stop group; -1 before the first move.</summary>
    internal int PlaceholderGroupsIdx { get; private set; }

    /// <summary>Multiplied by 10 for each merge (and back to 1 when the indexes are renumbered).</summary>
    internal int NestingLevel { get; private set; } = 1;

    internal TextmateSnippet Snippet => _snippet;

    /// <summary>Starts the snippet at the offset its text was inserted at.</summary>
    public void Initialize(TextChange textChange)
    {
        _offset = textChange.NewPosition;
    }

    public void Dispose()
    {
        if (_placeholderDecorations is not null)
        {
            _editor.Model.DeltaDecorations(_placeholderDecorations.Values.ToList(), [], _editor.DecorationOwnerId);
        }

        _placeholderGroups.Clear();
    }

    private void InitDecorations()
    {
        if (_offset == -1)
        {
            throw new InvalidOperationException("Snippet not initialized!");
        }

        if (_placeholderDecorations is not null)
        {
            // already initialized
            return;
        }

        _placeholderDecorations = new Dictionary<Placeholder, string>(ReferenceEqualityComparer.Instance);
        var model = _editor.Model;

        model.ChangeDecorations(accessor =>
        {
            // create a decoration for each placeholder
            foreach (var placeholder in _snippet.Placeholders)
            {
                var placeholderOffset = _snippet.Offset(placeholder);
                var placeholderLen = _snippet.FullLen(placeholder);
                var range = FromPositions(
                    model.GetPositionAt(_offset + placeholderOffset),
                    model.GetPositionAt(_offset + placeholderOffset + placeholderLen));
                var options = placeholder.IsFinalTabstop ? s_inactiveFinal : s_inactive;
                var handle = accessor.AddDecoration(range, options);
                _placeholderDecorations[placeholder] = handle;
            }

            return 0;
        }, _editor.DecorationOwnerId);
    }

    /// <summary>
    /// Moves to the next (<paramref name="fwd"/> true) or previous tab stop, or reselects the current one (null), after
    /// running the transforms of the current one; returns the selections of the new tab stop.
    /// </summary>
    public List<TextSelection> Move(bool? fwd)
    {
        InitDecorations();

        var model = _editor.Model;

        // Transform placeholder text if necessary
        if (PlaceholderGroupsIdx >= 0)
        {
            var operations = new List<SingleEditOperation>();

            foreach (var placeholder in _placeholderGroups[PlaceholderGroupsIdx])
            {
                // Check if the placeholder has a transformation
                if (placeholder.Transform is not null)
                {
                    var range = _placeholderDecorations!.TryGetValue(placeholder, out var id) ? model.GetDecorationRange(id) : null;
                    if (range is { } r)
                    {
                        var currentValue = model.GetValueInRange(r);
                        var transformedValueLines = SnippetStrings.SplitLines(placeholder.Transform.Resolve(currentValue));
                        // fix indentation for transformed lines
                        for (var i = 1; i < transformedValueLines.Length; i++)
                        {
                            transformedValueLines[i] = model.NormalizeIndentation(_snippetLineLeadingWhitespace + transformedValueLines[i]);
                        }

                        operations.Add(EditOperation.Replace(r, string.Join(model.GetEOL(), transformedValueLines)));
                    }
                }
            }

            if (operations.Count > 0)
            {
                _editor.ExecuteEdits("snippet.placeholderTransform", operations, static _ => null);
            }
        }

        var couldSkipThisPlaceholder = false;
        if (fwd == true && PlaceholderGroupsIdx < _placeholderGroups.Count - 1)
        {
            PlaceholderGroupsIdx += 1;
            couldSkipThisPlaceholder = true;
        }
        else if (fwd == false && PlaceholderGroupsIdx > 0)
        {
            PlaceholderGroupsIdx -= 1;
            couldSkipThisPlaceholder = true;
        }
        else
        {
            // the selection of the current placeholder might
            // not acurate any more -> simply restore it
        }

        var newSelections = model.ChangeDecorations(accessor =>
        {
            var activePlaceholders = new HashSet<Placeholder>(ReferenceEqualityComparer.Instance);

            // change stickiness to always grow when typing at its edges
            // because these decorations represent the currently active
            // tabstop.
            // Special case #1: reaching the final tabstop
            // Special case #2: placeholders enclosing active placeholders
            var selections = new List<TextSelection>();
            var group = PlaceholderGroupsIdx >= 0 && PlaceholderGroupsIdx < _placeholderGroups.Count
                ? _placeholderGroups[PlaceholderGroupsIdx]
                : [];
            foreach (var placeholder in group)
            {
                var hasId = _placeholderDecorations!.TryGetValue(placeholder, out var id);
                var range = hasId ? model.GetDecorationRange(id!) : null;

                // consider to skip this placeholder index when the decoration
                // range is empty but when the placeholder wasn't. that's a strong
                // hint that the placeholder has been deleted. (all placeholder must match this)
                couldSkipThisPlaceholder = couldSkipThisPlaceholder && HasPlaceholderBeenCollapsed(placeholder);

                if (!hasId || range is not { } r)
                {
                    continue;
                }

                selections.Add(new TextSelection(r.StartLineNumber, r.StartColumn, r.EndLineNumber, r.EndColumn));

                accessor.ChangeDecorationOptions(id!, placeholder.IsFinalTabstop ? s_activeFinal : s_active);
                activePlaceholders.Add(placeholder);

                foreach (var enclosingPlaceholder in _snippet.EnclosingPlaceholders(placeholder))
                {
                    if (_placeholderDecorations.TryGetValue(enclosingPlaceholder, out var enclosingId))
                    {
                        accessor.ChangeDecorationOptions(enclosingId, enclosingPlaceholder.IsFinalTabstop ? s_activeFinal : s_active);
                        activePlaceholders.Add(enclosingPlaceholder);
                    }
                }
            }

            // change stickness to never grow when typing at its edges
            // so that in-active tabstops never grow
            foreach (var (placeholder, id) in _placeholderDecorations!)
            {
                if (!activePlaceholders.Contains(placeholder))
                {
                    accessor.ChangeDecorationOptions(id, placeholder.IsFinalTabstop ? s_inactiveFinal : s_inactive);
                }
            }

            return selections;
        }, _editor.DecorationOwnerId);

        return !couldSkipThisPlaceholder ? newSelections ?? [] : Move(fwd);
    }

    private bool HasPlaceholderBeenCollapsed(Placeholder placeholder)
    {
        // A placeholder is empty when it wasn't empty when authored but
        // when its tracking decoration is empty. This also applies to all
        // potential parent placeholders
        var model = _editor.Model;
        Marker? marker = placeholder;
        while (marker is not null)
        {
            if (marker is Placeholder p)
            {
                var range = _placeholderDecorations!.TryGetValue(p, out var id) ? model.GetDecorationRange(id) : null;
                if ((range is null || range.Value.IsEmpty) && p.ToString().Length > 0)
                {
                    return true;
                }
            }

            marker = marker.Parent;
        }

        return false;
    }

    public bool IsAtFirstPlaceholder => PlaceholderGroupsIdx <= 0 || _placeholderGroups.Count == 0;

    public bool IsAtLastPlaceholder => PlaceholderGroupsIdx == _placeholderGroups.Count - 1;

    public bool HasPlaceholder => _snippet.Placeholders.Count > 0;

    /// <summary>A snippet is trivial when it has no placeholder or only a final placeholder at its very end.</summary>
    public bool IsTrivialSnippet
    {
        get
        {
            if (_snippet.Placeholders.Count == 0)
            {
                return true;
            }

            if (_snippet.Placeholders.Count == 1)
            {
                var placeholder = _snippet.Placeholders[0];
                if (placeholder.IsFinalTabstop)
                {
                    if (ReferenceEquals(_snippet.RightMostDescendant, placeholder))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>The ranges of each tab stop group (the final tab stop not included).</summary>
    public Dictionary<int, List<TextRange>> ComputePossibleSelections()
    {
        var result = new Dictionary<int, List<TextRange>>();
        foreach (var placeholdersWithEqualIndex in _placeholderGroups)
        {
            List<TextRange>? ranges = null;

            foreach (var placeholder in placeholdersWithEqualIndex)
            {
                if (placeholder.IsFinalTabstop)
                {
                    // ignore those
                    break;
                }

                if (ranges is null)
                {
                    ranges = [];
                    result[placeholder.Index] = ranges;
                }

                var range = _placeholderDecorations!.TryGetValue(placeholder, out var id) ? _editor.Model.GetDecorationRange(id) : null;
                if (range is not { } r)
                {
                    // one of the placeholder lost its decoration and
                    // therefore we bail out and pretend the placeholder
                    // (with its mirrors) doesn't exist anymore.
                    result.Remove(placeholder.Index);
                    break;
                }

                ranges.Add(r);
            }
        }

        return result;
    }

    /// <summary>The choice of the current tab stop, if it is a choice placeholder.</summary>
    public ActiveSnippetChoice? ActiveChoice
    {
        get
        {
            if (_placeholderDecorations is null || PlaceholderGroupsIdx < 0 || PlaceholderGroupsIdx >= _placeholderGroups.Count)
            {
                return null;
            }

            var group = _placeholderGroups[PlaceholderGroupsIdx];
            if (group.Count == 0 || group[0].Choice is not { } choice)
            {
                return null;
            }

            if (!_placeholderDecorations.TryGetValue(group[0], out var id))
            {
                return null;
            }

            if (_editor.Model.GetDecorationRange(id) is not { } range)
            {
                return null;
            }

            return new ActiveSnippetChoice(choice, range);
        }
    }

    public bool HasChoice
    {
        get
        {
            var result = false;
            _snippet.Walk(marker =>
            {
                result = marker is Choice;
                return !result;
            });
            return result;
        }
    }

    public int ActivePlaceholderCount =>
        PlaceholderGroupsIdx < 0 ? 0 : _placeholderGroups[PlaceholderGroupsIdx].Count;

    /// <summary>
    /// Puts the nested snippets (one per placeholder of the current tab stop, taken from the start of
    /// <paramref name="others"/>) in place of those placeholders.
    /// </summary>
    public void Merge(List<OneSnippet> others)
    {
        var model = _editor.Model;
        NestingLevel *= 10;

        model.ChangeDecorations(accessor =>
        {
            // the fractional indexes of the nested placeholders (upstream writes them into placeholder.index)
            var fractional = new Dictionary<Placeholder, double>(ReferenceEqualityComparer.Instance);

            // For each active placeholder take one snippet and merge it
            // in that the placeholder (can be many for `$1foo$1foo`). Because
            // everything is sorted by editor selection we can simply remove
            // elements from the beginning of the array
            foreach (var placeholder in _placeholderGroups[PlaceholderGroupsIdx])
            {
                var nested = others[0];
                others.RemoveAt(0);

                // Massage placeholder-indicies of the nested snippet to be
                // sorted right after the insertion point. This ensures we move
                // through the placeholders in the correct order
                var indexLastPlaceholder = nested._snippet.PlaceholderInfo.Last!.Index;
                var outerIndex = fractional.TryGetValue(placeholder, out var outerFraction) ? outerFraction : placeholder.Index;

                foreach (var nestedPlaceholder in nested._snippet.PlaceholderInfo.All)
                {
                    fractional[nestedPlaceholder] = nestedPlaceholder.IsFinalTabstop
                        ? outerIndex + ((indexLastPlaceholder + 1) / (double)NestingLevel)
                        : outerIndex + (nestedPlaceholder.Index / (double)NestingLevel);
                }

                _snippet.Replace(placeholder, nested._snippet.Children);

                // Remove the placeholder at which position are inserting
                // the snippet and also remove its decoration.
                var id = _placeholderDecorations![placeholder];
                accessor.RemoveDecoration(id);
                _placeholderDecorations.Remove(placeholder);

                // For each *new* placeholder we create decoration to monitor
                // how and if it grows/shrinks.
                foreach (var nestedPlaceholder in nested._snippet.Placeholders)
                {
                    var placeholderOffset = nested._snippet.Offset(nestedPlaceholder);
                    var placeholderLen = nested._snippet.FullLen(nestedPlaceholder);
                    var range = FromPositions(
                        model.GetPositionAt(nested._offset + placeholderOffset),
                        model.GetPositionAt(nested._offset + placeholderOffset + placeholderLen));
                    var handle = accessor.AddDecoration(range, s_inactive);
                    _placeholderDecorations[nestedPlaceholder] = handle;
                }
            }

            // Renormalize fractional placeholder indicies back to small integers.
            // Without this, deeply nested merges (~16+ levels) lose floating-point
            // precision so distinct placeholders collapse onto the same index and
            // produce phantom cursors. #279349
            RenormalizePlaceholderIndices(fractional);

            // Last, re-create the placeholder groups by sorting placeholders by their index.
            _placeholderGroups = GroupBy(_snippet.Placeholders);
            return 0;
        }, _editor.DecorationOwnerId);
    }

    private void RenormalizePlaceholderIndices(Dictionary<Placeholder, double> fractional)
    {
        double IndexOf(Placeholder placeholder) => fractional.TryGetValue(placeholder, out var value) ? value : placeholder.Index;

        var placeholders = _snippet.Placeholders;
        var uniqueIndices = new SortedSet<double>();
        foreach (var placeholder in placeholders)
        {
            if (IndexOf(placeholder) != 0)
            {
                uniqueIndices.Add(IndexOf(placeholder));
            }
        }

        var remap = new Dictionary<double, int>();
        var i = 0;
        foreach (var index in uniqueIndices)
        {
            remap[index] = ++i;
        }

        foreach (var placeholder in placeholders)
        {
            var index = IndexOf(placeholder);
            placeholder.Index = index != 0 ? remap[index] : 0;
        }

        NestingLevel = 1;
    }

    /// <summary>The range of all placeholders.</summary>
    public TextRange? GetEnclosingRange()
    {
        TextRange? result = null;
        var model = _editor.Model;
        foreach (var decorationId in _placeholderDecorations!.Values)
        {
            var placeholderRange = model.GetDecorationRange(decorationId);
            if (result is null)
            {
                result = placeholderRange;
            }
            else if (placeholderRange is { } r)
            {
                result = result.Value.PlusRange(r);
            }
        }

        return result;
    }

    internal int Offset => _offset;

    internal bool HasDecorations => _placeholderDecorations is not null;

    // arrays.groupBy: a stable sort by index (the final tab stop last), then runs of equal indexes
    private static List<List<Placeholder>> GroupBy(List<Placeholder> placeholders)
    {
        var result = new List<List<Placeholder>>();
        List<Placeholder>? currentGroup = null;
        foreach (var element in placeholders.Order(Comparer<Placeholder>.Create(Placeholder.CompareByIndex)))
        {
            if (currentGroup is null || Placeholder.CompareByIndex(currentGroup[0], element) != 0)
            {
                currentGroup = [element];
                result.Add(currentGroup);
            }
            else
            {
                currentGroup.Add(element);
            }
        }

        return result;
    }

    internal static TextRange FromPositions(TextPosition start, TextPosition end) =>
        new(start.LineNumber, start.Column, end.LineNumber, end.Column);
}

/// <summary>The class names of the placeholder decorations (upstream's snippetSession.css).</summary>
internal static class SnippetDecorationClassNames
{
    /// <summary>A tab stop with or without default text.</summary>
    public const string Placeholder = "snippet-placeholder";

    /// <summary>The final tab stop <c>$0</c>.</summary>
    public const string FinalPlaceholder = "finish-snippet-placeholder";
}

/// <summary>How a snippet is inserted (upstream's ISnippetSessionInsertOptions).</summary>
internal sealed record SnippetSessionInsertOptions
{
    public static SnippetSessionInsertOptions Default { get; } = new();

    /// <summary>How many characters before the cursor the snippet replaces.</summary>
    public int OverwriteBefore { get; init; }

    /// <summary>How many characters after the cursor the snippet replaces.</summary>
    public int OverwriteAfter { get; init; }

    /// <summary>Indents the snippet's lines like the line it is inserted on.</summary>
    public bool AdjustWhitespace { get; init; } = true;

    /// <summary>The text of the CLIPBOARD variable.</summary>
    public string? ClipboardText { get; init; }

    /// <summary>What the suggest widget overtyped (the SELECTION variable falls back to it).</summary>
    public IOvertypingCapturer? OvertypingCapturer { get; init; }
}

/// <summary>A snippet that replaces a range (upstream's ISnippetEdit).</summary>
/// <param name="Range">The range to replace.</param>
/// <param name="Template">The snippet.</param>
/// <param name="KeepWhitespace">Keep the snippet's indentation as it is (null: the insert options decide).</param>
internal sealed record SnippetEdit(TextRange Range, string Template, bool? KeepWhitespace = null);

/// <summary>
/// The snippets inserted by one insert (one per cursor, or one for a set of edits) and the tab stop navigation over
/// them (upstream's SnippetSession).
/// </summary>
internal sealed partial class SnippetSession : IDisposable
{
    private readonly ISnippetEditor _editor;
    private readonly string? _template;
    private readonly IReadOnlyList<SnippetEdit>? _templateEdits;
    private readonly SnippetSessionInsertOptions _options;
    private readonly List<(int NestingLevel, int PlaceholderGroupsIdx, string Template)> _templateMerges = [];
    private List<OneSnippet> _snippets = [];

    public SnippetSession(ISnippetEditor editor, string template, SnippetSessionInsertOptions? options = null)
    {
        _editor = editor;
        _template = template;
        _options = options ?? SnippetSessionInsertOptions.Default;
    }

    public SnippetSession(ISnippetEditor editor, IReadOnlyList<SnippetEdit> template, SnippetSessionInsertOptions? options = null)
    {
        _editor = editor;
        _templateEdits = template;
        _options = options ?? SnippetSessionInsertOptions.Default;
    }

    public void Dispose()
    {
        foreach (var snippet in _snippets)
        {
            snippet.Dispose();
        }
    }

    internal string LogInfo() =>
        $"template=\"{_template ?? string.Join(", ", _templateEdits?.Select(e => e.Template) ?? [])}\", merged_templates=\"{string.Join(" -> ", _templateMerges)}\"";

    /// <summary>Inserts the snippet and selects its first tab stop.</summary>
    public void Insert()
    {
        // make insert edit and start with first selections
        var (edits, snippets) = _template is not null
            ? CreateEditsAndSnippetsFromSelections(_editor, _template, _options.OverwriteBefore, _options.OverwriteAfter, false, _options.AdjustWhitespace, _options.ClipboardText, _options.OvertypingCapturer)
            : CreateEditsAndSnippetsFromEdits(_editor, _templateEdits!, false, _options.AdjustWhitespace, _options.ClipboardText, _options.OvertypingCapturer);

        _snippets = snippets;

        _editor.ExecuteEdits("snippet", edits, inverseEdits =>
        {
            // Sometimes, the text buffer will remove automatic whitespace when doing any edits,
            // so we need to look only at the undo edits relevant for us.
            // Our edits have an identifier set so that's how we can distinguish them
            var undoEdits = inverseEdits.Where(edit => edit.Identifier is not null).ToList();
            for (var idx = 0; idx < snippets.Count; idx++)
            {
                snippets[idx].Initialize(undoEdits[idx].TextChange);
            }

            if (_snippets[0].HasPlaceholder)
            {
                return MoveCore(true);
            }
            else
            {
                return undoEdits.Select(edit => new TextSelection(edit.Range.End, edit.Range.End)).ToList();
            }
        });
        if (_editor.GetSelections() is { Count: > 0 } selections)
        {
            _editor.RevealRange(selections[0]);
        }
    }

    /// <summary>Inserts a snippet into the current tab stop and continues with its placeholders.</summary>
    public void Merge(string template, SnippetSessionInsertOptions? options = null)
    {
        options ??= SnippetSessionInsertOptions.Default;
        _templateMerges.Add((_snippets[0].NestingLevel, _snippets[0].PlaceholderGroupsIdx, template));
        var (edits, snippets) = CreateEditsAndSnippetsFromSelections(_editor, template, options.OverwriteBefore, options.OverwriteAfter, true, options.AdjustWhitespace, options.ClipboardText, options.OvertypingCapturer);

        _editor.ExecuteEdits("snippet", edits, inverseEdits =>
        {
            // Sometimes, the text buffer will remove automatic whitespace when doing any edits,
            // so we need to look only at the undo edits relevant for us.
            // Our edits have an identifier set so that's how we can distinguish them
            var undoEdits = inverseEdits.Where(edit => edit.Identifier is not null).ToList();
            for (var idx = 0; idx < snippets.Count; idx++)
            {
                snippets[idx].Initialize(undoEdits[idx].TextChange);
            }

            // Trivial snippets have no placeholder or are just the final placeholder. That means they
            // are just text insertions and we don't need to merge the nested snippet into the existing
            // snippet
            var isTrivialSnippet = snippets[0].IsTrivialSnippet;
            // Only merge when each active placeholder occurrence has a matching nested snippet.
            // Cursor normalization or external selection changes can collapse selections, leaving
            // fewer nested snippets than placeholder occurrences and previously crashing the merge.
            var canMergeSnippets = snippets.Count == _snippets.Sum(snippet => snippet.ActivePlaceholderCount);
            if (!isTrivialSnippet && canMergeSnippets)
            {
                foreach (var snippet in _snippets)
                {
                    snippet.Merge(snippets);
                }
            }

            if (_snippets[0].HasPlaceholder && !isTrivialSnippet && canMergeSnippets)
            {
                return MoveCore(null);
            }
            else
            {
                return undoEdits.Select(edit => new TextSelection(edit.Range.End, edit.Range.End)).ToList();
            }
        });
    }

    /// <summary>Selects the next tab stop.</summary>
    public void Next()
    {
        var newSelections = MoveCore(true);
        if (newSelections.Count > 0)
        {
            _editor.SetSelections(newSelections);
            _editor.RevealPositionInCenterIfOutsideViewport(newSelections[0].Position);
        }
    }

    /// <summary>Selects the previous tab stop.</summary>
    public void Prev()
    {
        var newSelections = MoveCore(false);
        if (newSelections.Count > 0)
        {
            _editor.SetSelections(newSelections);
            _editor.RevealPositionInCenterIfOutsideViewport(newSelections[0].Position);
        }
    }

    private List<TextSelection> MoveCore(bool? fwd)
    {
        var selections = new List<TextSelection>();
        foreach (var snippet in _snippets)
        {
            selections.AddRange(snippet.Move(fwd));
        }

        return selections;
    }

    public bool IsAtFirstPlaceholder => _snippets[0].IsAtFirstPlaceholder;

    public bool IsAtLastPlaceholder => _snippets[0].IsAtLastPlaceholder;

    public bool HasPlaceholder => _snippets[0].HasPlaceholder;

    public bool HasChoice => _snippets[0].HasChoice;

    public ActiveSnippetChoice? ActiveChoice => _snippets[0].ActiveChoice;

    /// <summary>Whether every selection lies in the placeholders of one tab stop.</summary>
    public bool IsSelectionWithinPlaceholders()
    {
        if (!HasPlaceholder)
        {
            return false;
        }

        var selections = _editor.GetSelections().Select(s => s.Range).ToList();
        if (selections.Count < _snippets.Count)
        {
            // this means we started snippet mode with N
            // selections and have M (N > M) selections.
            // So one snippet is without selection -> cancel
            return false;
        }

        var allPossibleSelections = new Dictionary<int, List<TextRange>>();
        foreach (var snippet in _snippets)
        {
            var possibleSelections = snippet.ComputePossibleSelections();

            // for the first snippet find the placeholder (and its ranges)
            // that contain at least one selection. for all remaining snippets
            // the same placeholder (and their ranges) must be used.
            if (allPossibleSelections.Count == 0)
            {
                foreach (var (index, ranges) in possibleSelections)
                {
                    ranges.Sort(TextRange.CompareRangesUsingStarts);
                    foreach (var selection in selections)
                    {
                        if (ranges[0].ContainsRange(selection))
                        {
                            allPossibleSelections[index] = [];
                            break;
                        }
                    }
                }
            }

            if (allPossibleSelections.Count == 0)
            {
                // return false if we couldn't associate a selection to
                // this (the first) snippet
                return false;
            }

            // add selections from 'this' snippet so that we know all
            // selections for this placeholder
            foreach (var (index, array) in allPossibleSelections)
            {
                if (possibleSelections.TryGetValue(index, out var ranges))
                {
                    array.AddRange(ranges);
                }
            }
        }

        // sort selections (and later placeholder-ranges). then walk both
        // arrays and make sure the placeholder-ranges contain the corresponding
        // selection
        selections.Sort(TextRange.CompareRangesUsingStarts);

        foreach (var (index, ranges) in allPossibleSelections.ToList())
        {
            if (ranges.Count != selections.Count)
            {
                allPossibleSelections.Remove(index);
                continue;
            }

            ranges.Sort(TextRange.CompareRangesUsingStarts);

            for (var i = 0; i < ranges.Count; i++)
            {
                if (!ranges[i].ContainsRange(selections[i]))
                {
                    allPossibleSelections.Remove(index);
                }
            }
        }

        // from all possible selections we have deleted those
        // that don't match with the current selection. if we don't
        // have any left, we don't have a selection anymore
        return allPossibleSelections.Count > 0;
    }

    /// <summary>The range of the placeholders of all snippets.</summary>
    public TextRange? GetEnclosingRange()
    {
        TextRange? result = null;
        foreach (var snippet in _snippets)
        {
            var snippetRange = snippet.GetEnclosingRange();
            if (result is null)
            {
                result = snippetRange;
            }
            else if (snippetRange is { } r)
            {
                result = result.Value.PlusRange(r);
            }
        }

        return result;
    }
}

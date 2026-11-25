// Ported from vscode (src/vs/editor/common/viewLayout/lineHeights.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - CustomLineHeightData.FromDecorations takes the model-to-view range conversion as a function and the default line
//   height as a number, instead of an ICoordinatesConverter and the editor configuration (neither is ported yet).
// - The pending changes are a small class hierarchy instead of a tagged union; ArrayMap is a private nested class over a
//   Dictionary. "No invalid index" is int.MaxValue instead of Infinity.
// - CustomLine rounds its height half up, as JavaScript's Math.round does (not .NET's default banker's rounding).
// - Reads past the end of the ordered array, which give undefined upstream, are bounds-checked to the same effect.

namespace TKCode.Arc.Editor.ViewLayout;

/// <summary>One line of a decoration that gives its lines a custom height.</summary>
internal sealed class CustomLine
{
    public int Index;
    public int LineNumber;
    public double SpecialHeight;
    public double PrefixSum;
    public double MaximumSpecialHeight;
    public string DecorationId;
    public bool Deleted;

    public CustomLine(string decorationId, int index, int lineNumber, double specialHeight, double prefixSum)
    {
        DecorationId = decorationId;
        Index = index;
        LineNumber = lineNumber;
        SpecialHeight = JsRound(specialHeight);
        PrefixSum = prefixSum;
        MaximumSpecialHeight = JsRound(specialHeight);
        Deleted = false;
    }

    // JavaScript's Math.round: halves round up (towards +Infinity).
    private static double JsRound(double value) => Math.Floor(value + 0.5);
}

/// <summary>
/// Manages line heights in the editor with support for custom line heights from decorations.
/// </summary>
/// <remarks>
/// Lines with a custom height are kept in an array sorted by line number, each a <see cref="CustomLine"/> with its
/// height, the accumulated height of the lines before it (its prefix sum) and its decoration id; a line that several
/// decorations cover takes the largest height. Changes are queued and applied on the next query, which makes reading
/// the accumulated height of any line a binary search. Line insertions and deletions move the custom lines after them
/// and collapse or extend the decorations that span them.
/// </remarks>
internal sealed class LineHeightsManager
{
    private Dictionary<string, List<CustomLine>> _decorationIDToCustomLine = new();
    private List<CustomLine> _orderedCustomLines = new();
    private List<PendingChange> _pendingChanges = new();
    private int _invalidIndex = int.MaxValue;
    private double _defaultLineHeight;
    private bool _hasPending;

    public LineHeightsManager(double defaultLineHeight, IReadOnlyList<CustomLineHeightData> customLineHeightData)
    {
        _defaultLineHeight = defaultLineHeight;
        foreach (var data in customLineHeightData)
        {
            InsertOrChangeCustomLineHeight(data.DecorationId, data.StartLineNumber, data.EndLineNumber, data.LineHeight);
        }
    }

    public double DefaultLineHeight
    {
        get => _defaultLineHeight;
        set => _defaultLineHeight = value;
    }

    public void RemoveCustomLineHeight(string decorationID)
    {
        _pendingChanges.Add(new RemoveChange(decorationID));
        _hasPending = true;
    }

    public void InsertOrChangeCustomLineHeight(string decorationId, int startLineNumber, int endLineNumber, double lineHeight)
    {
        _pendingChanges.Add(new InsertOrChangeChange(decorationId, startLineNumber, endLineNumber, lineHeight));
        _hasPending = true;
    }

    public double HeightForLineNumber(int lineNumber)
    {
        Commit();
        var searchIndex = BinarySearchOverOrderedCustomLinesArray(lineNumber);
        if (searchIndex >= 0)
        {
            return _orderedCustomLines[searchIndex].MaximumSpecialHeight;
        }
        return _defaultLineHeight;
    }

    public double GetAccumulatedLineHeightsIncludingLineNumber(int lineNumber)
    {
        Commit();
        var searchIndex = BinarySearchOverOrderedCustomLinesArray(lineNumber);
        if (searchIndex >= 0)
        {
            return _orderedCustomLines[searchIndex].PrefixSum + _orderedCustomLines[searchIndex].MaximumSpecialHeight;
        }
        if (searchIndex == -1)
        {
            return _defaultLineHeight * lineNumber;
        }
        var modifiedIndex = -(searchIndex + 1);
        var previousSpecialLine = _orderedCustomLines[modifiedIndex - 1];
        return previousSpecialLine.PrefixSum + previousSpecialLine.MaximumSpecialHeight + _defaultLineHeight * (lineNumber - previousSpecialLine.LineNumber);
    }

    public void OnLinesDeleted(int fromLineNumber, int toLineNumber)
    {
        _pendingChanges.Add(new LinesDeletedChange(fromLineNumber, toLineNumber));
        _hasPending = true;
    }

    public void OnLinesInserted(int fromLineNumber, int toLineNumber)
    {
        _pendingChanges.Add(new LinesInsertedChange(fromLineNumber, toLineNumber));
        _hasPending = true;
    }

    private void Commit()
    {
        if (!_hasPending)
        {
            return;
        }
        var changes = _pendingChanges;
        _pendingChanges = new List<PendingChange>();
        _hasPending = false;

        var stagedInserts = new List<CustomLine>();
        var stagedIdMap = new ArrayMap();
        foreach (var change in changes)
        {
            switch (change)
            {
                case RemoveChange remove:
                    DoRemoveCustomLineHeight(remove.DecorationId, stagedIdMap);
                    break;
                case InsertOrChangeChange insert:
                    DoInsertOrChangeCustomLineHeight(insert.DecorationId, insert.StartLineNumber, insert.EndLineNumber, insert.LineHeight, stagedInserts, stagedIdMap);
                    break;
                case LinesDeletedChange deleted:
                    FlushStagedDecorationChanges(stagedInserts, stagedIdMap);
                    DoLinesDeleted(deleted.FromLineNumber, deleted.ToLineNumber);
                    break;
                case LinesInsertedChange inserted:
                    FlushStagedDecorationChanges(stagedInserts, stagedIdMap);
                    DoLinesInserted(inserted.FromLineNumber, inserted.ToLineNumber, stagedInserts, stagedIdMap);
                    break;
            }
        }
        FlushStagedDecorationChanges(stagedInserts, stagedIdMap);
    }

    private void DoRemoveCustomLineHeight(string decorationID, ArrayMap stagedIdMap)
    {
        if (_decorationIDToCustomLine.TryGetValue(decorationID, out var customLines))
        {
            _decorationIDToCustomLine.Remove(decorationID);
            foreach (var customLine in customLines)
            {
                customLine.Deleted = true;
                _invalidIndex = Math.Min(_invalidIndex, customLine.Index);
            }
        }
        var stagedLines = stagedIdMap.Get(decorationID);
        if (stagedLines is not null)
        {
            stagedIdMap.Delete(decorationID);
            foreach (var line in stagedLines)
            {
                line.Deleted = true;
            }
        }
    }

    private void DoInsertOrChangeCustomLineHeight(string decorationId, int startLineNumber, int endLineNumber, double lineHeight, List<CustomLine> stagedInserts, ArrayMap stagedIdMap)
    {
        DoRemoveCustomLineHeight(decorationId, stagedIdMap);
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var customLine = new CustomLine(decorationId, -1, lineNumber, lineHeight, 0);
            stagedInserts.Add(customLine);
            stagedIdMap.Add(decorationId, customLine);
        }
    }

    private void FlushStagedDecorationChanges(List<CustomLine> stagedInserts, ArrayMap stagedIdMap)
    {
        if (stagedInserts.Count == 0 && _invalidIndex == int.MaxValue)
        {
            return;
        }
        foreach (var pendingChange in stagedInserts)
        {
            if (pendingChange.Deleted)
            {
                continue;
            }
            var candidateInsertionIndex = BinarySearchOverOrderedCustomLinesArray(pendingChange.LineNumber);
            var insertionIndex = candidateInsertionIndex >= 0 ? candidateInsertionIndex : -(candidateInsertionIndex + 1);
            _orderedCustomLines.Insert(insertionIndex, pendingChange);
            _invalidIndex = Math.Min(_invalidIndex, insertionIndex);
        }
        stagedInserts.Clear();
        stagedIdMap.Clear();
        if (_invalidIndex == int.MaxValue)
        {
            return;
        }
        var newDecorationIDToSpecialLine = new ArrayMap();
        var newOrderedSpecialLines = new List<CustomLine>();

        for (var i = 0; i < _invalidIndex; i++)
        {
            var customLine = _orderedCustomLines[i];
            newOrderedSpecialLines.Add(customLine);
            newDecorationIDToSpecialLine.Add(customLine.DecorationId, customLine);
        }

        var numberOfDeletions = 0;
        CustomLine? previousSpecialLine = _invalidIndex > 0 ? newOrderedSpecialLines[_invalidIndex - 1] : null;
        for (var i = _invalidIndex; i < _orderedCustomLines.Count; i++)
        {
            var customLine = _orderedCustomLines[i];
            if (customLine.Deleted)
            {
                numberOfDeletions++;
                continue;
            }
            customLine.Index = i - numberOfDeletions;
            if (previousSpecialLine is not null && previousSpecialLine.LineNumber == customLine.LineNumber)
            {
                customLine.MaximumSpecialHeight = previousSpecialLine.MaximumSpecialHeight;
                customLine.PrefixSum = previousSpecialLine.PrefixSum;
            }
            else
            {
                var maximumSpecialHeight = customLine.SpecialHeight;
                for (var j = i; j < _orderedCustomLines.Count; j++)
                {
                    var nextSpecialLine = _orderedCustomLines[j];
                    if (nextSpecialLine.Deleted)
                    {
                        continue;
                    }
                    if (nextSpecialLine.LineNumber != customLine.LineNumber)
                    {
                        break;
                    }
                    maximumSpecialHeight = Math.Max(maximumSpecialHeight, nextSpecialLine.SpecialHeight);
                }
                customLine.MaximumSpecialHeight = maximumSpecialHeight;

                double prefixSum;
                if (previousSpecialLine is not null)
                {
                    prefixSum = previousSpecialLine.PrefixSum + previousSpecialLine.MaximumSpecialHeight + _defaultLineHeight * (customLine.LineNumber - previousSpecialLine.LineNumber - 1);
                }
                else
                {
                    prefixSum = _defaultLineHeight * (customLine.LineNumber - 1);
                }
                customLine.PrefixSum = prefixSum;
            }
            previousSpecialLine = customLine;
            newOrderedSpecialLines.Add(customLine);
            newDecorationIDToSpecialLine.Add(customLine.DecorationId, customLine);
        }
        _orderedCustomLines = newOrderedSpecialLines;
        _decorationIDToCustomLine = newDecorationIDToSpecialLine.Map;
        _invalidIndex = int.MaxValue;
    }

    private void DoLinesDeleted(int fromLineNumber, int toLineNumber)
    {
        var deleteCount = toLineNumber - fromLineNumber + 1;
        var numberOfCustomLines = _orderedCustomLines.Count;
        var candidateStartIndexOfDeletion = BinarySearchOverOrderedCustomLinesArray(fromLineNumber);
        int startIndexOfDeletion;
        if (candidateStartIndexOfDeletion >= 0)
        {
            startIndexOfDeletion = candidateStartIndexOfDeletion;
            for (var i = candidateStartIndexOfDeletion - 1; i >= 0; i--)
            {
                if (_orderedCustomLines[i].LineNumber == fromLineNumber)
                {
                    startIndexOfDeletion--;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            startIndexOfDeletion = candidateStartIndexOfDeletion == -(numberOfCustomLines + 1) && candidateStartIndexOfDeletion != -1 ? numberOfCustomLines - 1 : -(candidateStartIndexOfDeletion + 1);
        }
        var candidateEndIndexOfDeletion = BinarySearchOverOrderedCustomLinesArray(toLineNumber);
        int endIndexOfDeletion;
        if (candidateEndIndexOfDeletion >= 0)
        {
            endIndexOfDeletion = candidateEndIndexOfDeletion;
            for (var i = candidateEndIndexOfDeletion + 1; i < numberOfCustomLines; i++)
            {
                if (_orderedCustomLines[i].LineNumber == toLineNumber)
                {
                    endIndexOfDeletion++;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            endIndexOfDeletion = candidateEndIndexOfDeletion == -(numberOfCustomLines + 1) && candidateEndIndexOfDeletion != -1 ? numberOfCustomLines - 1 : -(candidateEndIndexOfDeletion + 1);
        }
        var isEndIndexBiggerThanStartIndex = endIndexOfDeletion > startIndexOfDeletion;
        var isEndIndexEqualToStartIndexAndCoversCustomLine = endIndexOfDeletion == startIndexOfDeletion
            && startIndexOfDeletion < _orderedCustomLines.Count
            && _orderedCustomLines[startIndexOfDeletion].LineNumber >= fromLineNumber
            && _orderedCustomLines[startIndexOfDeletion].LineNumber <= toLineNumber;

        if (isEndIndexBiggerThanStartIndex || isEndIndexEqualToStartIndexAndCoversCustomLine)
        {
            double maximumSpecialHeightOnDeletedInterval = 0;
            for (var i = startIndexOfDeletion; i <= endIndexOfDeletion; i++)
            {
                maximumSpecialHeightOnDeletedInterval = Math.Max(maximumSpecialHeightOnDeletedInterval, _orderedCustomLines[i].MaximumSpecialHeight);
            }
            double prefixSumOnDeletedInterval;
            if (startIndexOfDeletion > 0)
            {
                var previousSpecialLine = _orderedCustomLines[startIndexOfDeletion - 1];
                prefixSumOnDeletedInterval = previousSpecialLine.PrefixSum + previousSpecialLine.MaximumSpecialHeight + _defaultLineHeight * (fromLineNumber - previousSpecialLine.LineNumber - 1);
            }
            else
            {
                prefixSumOnDeletedInterval = fromLineNumber > 0 ? (fromLineNumber - 1) * _defaultLineHeight : 0;
            }
            var firstSpecialLineDeleted = _orderedCustomLines[startIndexOfDeletion];
            var lastSpecialLineDeleted = _orderedCustomLines[endIndexOfDeletion];
            var firstSpecialLineAfterDeletion = endIndexOfDeletion + 1 < _orderedCustomLines.Count ? _orderedCustomLines[endIndexOfDeletion + 1] : null;
            var heightOfFirstLineAfterDeletion = firstSpecialLineAfterDeletion is not null && firstSpecialLineAfterDeletion.LineNumber == toLineNumber + 1 ? firstSpecialLineAfterDeletion.MaximumSpecialHeight : _defaultLineHeight;
            var totalHeightDeleted = lastSpecialLineDeleted.PrefixSum
                + lastSpecialLineDeleted.MaximumSpecialHeight
                - firstSpecialLineDeleted.PrefixSum
                + _defaultLineHeight * (toLineNumber - lastSpecialLineDeleted.LineNumber)
                + _defaultLineHeight * (firstSpecialLineDeleted.LineNumber - fromLineNumber)
                + heightOfFirstLineAfterDeletion - maximumSpecialHeightOnDeletedInterval;

            var decorationIdsSeen = new HashSet<string>();
            var newOrderedCustomLines = new List<CustomLine>();
            var newDecorationIDToSpecialLine = new ArrayMap();
            var numberOfDeletions = 0;
            for (var i = 0; i < _orderedCustomLines.Count; i++)
            {
                var customLine = _orderedCustomLines[i];
                if (i < startIndexOfDeletion)
                {
                    newOrderedCustomLines.Add(customLine);
                    newDecorationIDToSpecialLine.Add(customLine.DecorationId, customLine);
                }
                else if (i >= startIndexOfDeletion && i <= endIndexOfDeletion)
                {
                    var decorationId = customLine.DecorationId;
                    if (!decorationIdsSeen.Contains(decorationId))
                    {
                        customLine.Index -= numberOfDeletions;
                        customLine.LineNumber = fromLineNumber;
                        customLine.PrefixSum = prefixSumOnDeletedInterval;
                        customLine.MaximumSpecialHeight = maximumSpecialHeightOnDeletedInterval;
                        newOrderedCustomLines.Add(customLine);
                        newDecorationIDToSpecialLine.Add(customLine.DecorationId, customLine);
                    }
                    else
                    {
                        numberOfDeletions++;
                    }
                }
                else if (i > endIndexOfDeletion)
                {
                    customLine.Index -= numberOfDeletions;
                    customLine.LineNumber -= deleteCount;
                    customLine.PrefixSum -= totalHeightDeleted;
                    newOrderedCustomLines.Add(customLine);
                    newDecorationIDToSpecialLine.Add(customLine.DecorationId, customLine);
                }
                decorationIdsSeen.Add(customLine.DecorationId);
            }
            _orderedCustomLines = newOrderedCustomLines;
            _decorationIDToCustomLine = newDecorationIDToSpecialLine.Map;
        }
        else
        {
            var totalHeightDeleted = deleteCount * _defaultLineHeight;
            for (var i = endIndexOfDeletion; i < _orderedCustomLines.Count; i++)
            {
                var customLine = _orderedCustomLines[i];
                if (customLine.LineNumber > toLineNumber)
                {
                    customLine.LineNumber -= deleteCount;
                    customLine.PrefixSum -= totalHeightDeleted;
                }
            }
        }
    }

    private void DoLinesInserted(int fromLineNumber, int toLineNumber, List<CustomLine> stagedInserts, ArrayMap stagedIdMap)
    {
        var insertCount = toLineNumber - fromLineNumber + 1;
        var candidateStartIndexOfInsertion = BinarySearchOverOrderedCustomLinesArray(fromLineNumber);
        int startIndexOfInsertion;
        if (candidateStartIndexOfInsertion >= 0)
        {
            startIndexOfInsertion = candidateStartIndexOfInsertion;
            for (var i = candidateStartIndexOfInsertion - 1; i >= 0; i--)
            {
                if (_orderedCustomLines[i].LineNumber == fromLineNumber)
                {
                    startIndexOfInsertion--;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            startIndexOfInsertion = -(candidateStartIndexOfInsertion + 1);
        }
        var toReAdd = new List<CustomLineHeightData>();
        // Sets that keep the order they were filled in, as upstream's Set does.
        var decorationsImmediatelyAfter = new List<string>();
        var decorationsImmediatelyAfterSet = new HashSet<string>();
        for (var i = startIndexOfInsertion; i < _orderedCustomLines.Count; i++)
        {
            if (_orderedCustomLines[i].LineNumber == fromLineNumber && decorationsImmediatelyAfterSet.Add(_orderedCustomLines[i].DecorationId))
            {
                decorationsImmediatelyAfter.Add(_orderedCustomLines[i].DecorationId);
            }
        }
        var decorationsImmediatelyBefore = new HashSet<string>();
        for (var i = startIndexOfInsertion - 1; i >= 0; i--)
        {
            if (_orderedCustomLines[i].LineNumber == fromLineNumber - 1)
            {
                decorationsImmediatelyBefore.Add(_orderedCustomLines[i].DecorationId);
            }
        }
        // intersection(decorationsImmediatelyBefore, decorationsImmediatelyAfter), in the order of the second set
        var decorationsWithGaps = new List<string>();
        foreach (var decorationId in decorationsImmediatelyAfter)
        {
            if (decorationsImmediatelyBefore.Contains(decorationId))
            {
                decorationsWithGaps.Add(decorationId);
            }
        }
        var prefixSumToAdd = insertCount * _defaultLineHeight;
        for (var i = startIndexOfInsertion; i < _orderedCustomLines.Count; i++)
        {
            _orderedCustomLines[i].LineNumber += insertCount;
            _orderedCustomLines[i].PrefixSum += prefixSumToAdd;
        }

        if (decorationsWithGaps.Count > 0)
        {
            foreach (var decorationId in decorationsWithGaps)
            {
                if (_decorationIDToCustomLine.TryGetValue(decorationId, out var decoration))
                {
                    var startLineNumber = fromLineNumber;
                    var endLineNumber = fromLineNumber;
                    double lineHeight = 0;
                    foreach (var l in decoration)
                    {
                        startLineNumber = Math.Min(startLineNumber, l.LineNumber);
                        endLineNumber = Math.Max(endLineNumber, l.LineNumber);
                        lineHeight = Math.Max(lineHeight, l.SpecialHeight);
                    }
                    toReAdd.Add(new CustomLineHeightData(decorationId, startLineNumber, endLineNumber, lineHeight));
                }
            }

            foreach (var dec in toReAdd)
            {
                DoInsertOrChangeCustomLineHeight(dec.DecorationId, dec.StartLineNumber, dec.EndLineNumber, dec.LineHeight, stagedInserts, stagedIdMap);
            }
        }
    }

    private int BinarySearchOverOrderedCustomLinesArray(int lineNumber)
    {
        var orderedCustomLines = _orderedCustomLines;
        return Arrays.BinarySearch2(orderedCustomLines.Count, index =>
        {
            var line = orderedCustomLines[index];
            if (line.LineNumber == lineNumber)
            {
                return 0;
            }
            else if (line.LineNumber < lineNumber)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        });
    }

    private abstract class PendingChange;

    private sealed class InsertOrChangeChange(string decorationId, int startLineNumber, int endLineNumber, double lineHeight) : PendingChange
    {
        public string DecorationId { get; } = decorationId;
        public int StartLineNumber { get; } = startLineNumber;
        public int EndLineNumber { get; } = endLineNumber;
        public double LineHeight { get; } = lineHeight;
    }

    private sealed class RemoveChange(string decorationId) : PendingChange
    {
        public string DecorationId { get; } = decorationId;
    }

    private sealed class LinesDeletedChange(int fromLineNumber, int toLineNumber) : PendingChange
    {
        public int FromLineNumber { get; } = fromLineNumber;
        public int ToLineNumber { get; } = toLineNumber;
    }

    private sealed class LinesInsertedChange(int fromLineNumber, int toLineNumber) : PendingChange
    {
        public int FromLineNumber { get; } = fromLineNumber;
        public int ToLineNumber { get; } = toLineNumber;
    }

    /// <summary>Several custom lines per decoration id.</summary>
    private sealed class ArrayMap
    {
        public Dictionary<string, List<CustomLine>> Map { get; } = new();

        public void Add(string key, CustomLine value)
        {
            if (!Map.TryGetValue(key, out var array))
            {
                Map[key] = [value];
            }
            else
            {
                array.Add(value);
            }
        }

        public List<CustomLine>? Get(string key) => Map.GetValueOrDefault(key);

        public void Delete(string key) => Map.Remove(key);

        public void Clear() => Map.Clear();
    }
}

/// <summary>A decoration's custom line height over a range of view lines.</summary>
internal sealed class CustomLineHeightData
{
    public CustomLineHeightData(string decorationId, int startLineNumber, int endLineNumber, double lineHeight)
    {
        DecorationId = decorationId;
        StartLineNumber = startLineNumber;
        EndLineNumber = endLineNumber;
        LineHeight = lineHeight;
    }

    public string DecorationId { get; }

    public int StartLineNumber { get; }

    public int EndLineNumber { get; }

    public double LineHeight { get; }

    /// <summary>
    /// The custom line heights of <paramref name="decorations"/>, in view lines. A decoration's line height is a multiplier
    /// of <paramref name="defaultLineHeight"/>; <paramref name="convertModelRangeToViewRange"/> maps its model range to
    /// view lines (upstream's <c>ICoordinatesConverter.convertModelRangeToViewRange</c>).
    /// </summary>
    public static CustomLineHeightData[] FromDecorations(IReadOnlyList<Model.IModelDecoration> decorations, Func<TextRange, TextRange> convertModelRangeToViewRange, double defaultLineHeight)
    {
        var result = new CustomLineHeightData[decorations.Count];
        for (var i = 0; i < decorations.Count; i++)
        {
            var d = decorations[i];
            var viewRange = convertModelRangeToViewRange(d.Range);
            result[i] = new CustomLineHeightData(
                d.Id,
                viewRange.StartLineNumber,
                viewRange.EndLineNumber,
                d.Options.LineHeight is { } lineHeight && lineHeight != 0 ? lineHeight * defaultLineHeight : 0);
        }
        return result;
    }
}

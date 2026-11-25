// Ported from vscode (src/vs/editor/common/viewLayout/linesLayout.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - Upstream prefixes whitespace ids with a letter taken from a static instance counter. Here there is no static state:
//   the letter comes from the constructor's instanceNumber (default 1, upstream's first instance), and the id counter
//   is per layout as upstream's _lastWhitespaceId already is. Hosts with several layouts that mix ids pass distinct numbers.
// - Pixel values are doubles. Upstream's `x | 0` truncations to 32-bit integers are kept (Int32 below), so whitespace
//   heights and min widths are whole numbers and the vertical offsets used for hit testing are truncated, as upstream.
// - PendingChanges and the pending change records are private nested types, and _commitPendingChanges is private.
// - The full rebuild in CommitPendingChanges sorts with a stable sort, as JavaScript's Array.prototype.sort is.
// - getWhitespaceIndexAtOrAfterVerticallOffset keeps upstream's spelling.

namespace TKCode.Arc.Editor.ViewLayout;

/// <summary>A whitespace between lines, with the sum of the heights of the whitespaces up to and including it.</summary>
internal sealed class EditorWhitespace : IEditorWhitespace
{
    public EditorWhitespace(string id, int afterLineNumber, int ordinal, double height, double minWidth)
    {
        Id = id;
        AfterLineNumber = afterLineNumber;
        Ordinal = ordinal;
        Height = height;
        MinWidth = minWidth;
        PrefixSum = 0;
    }

    public string Id { get; set; }

    public int AfterLineNumber { get; set; }

    public int Ordinal { get; set; }

    public double Height { get; set; }

    public double MinWidth { get; set; }

    public double PrefixSum { get; set; }
}

/// <summary>
/// Layouting of objects that take vertical space (by having a height) and push down other objects.
/// </summary>
/// <remarks>
/// These objects are basically either text (lines) or spaces between those lines (whitespaces).
/// This provides commodity operations for working with lines that contain whitespace that pushes lines lower (vertically).
/// </remarks>
internal sealed class LinesLayout
{
    private readonly string _instanceId;
    private readonly PendingChanges _pendingChanges;
    private int _lastWhitespaceId;
    private List<EditorWhitespace> _arr;
    private int _prefixSumValidIndex;
    private double _minWidth;
    private int _lineCount;
    private double _paddingTop;
    private double _paddingBottom;
    private LineHeightsManager _lineHeightsManager;

    public LinesLayout(int lineCount, double defaultLineHeight, double paddingTop, double paddingBottom, IReadOnlyList<CustomLineHeightData> customLineHeightData, int instanceNumber = 1)
    {
        _instanceId = Strings.SingleLetterHash(instanceNumber);
        _pendingChanges = new PendingChanges();
        _lastWhitespaceId = 0;
        _arr = new List<EditorWhitespace>();
        _prefixSumValidIndex = -1;
        _minWidth = -1; /* marker for not being computed */
        _lineCount = lineCount;
        _paddingTop = paddingTop;
        _paddingBottom = paddingBottom;
        _lineHeightsManager = new LineHeightsManager(defaultLineHeight, customLineHeightData);
    }

    /// <summary>
    /// Find the insertion index for a new value inside a sorted array of values.
    /// If the value is already present in the sorted array, the insertion index will be after the already existing value.
    /// </summary>
    public static int FindInsertionIndex(IReadOnlyList<EditorWhitespace> arr, int afterLineNumber, int ordinal)
    {
        var low = 0;
        var high = arr.Count;

        while (low < high)
        {
            var mid = (int)((uint)(low + high) >> 1);

            if (afterLineNumber == arr[mid].AfterLineNumber)
            {
                if (ordinal < arr[mid].Ordinal)
                {
                    high = mid;
                }
                else
                {
                    low = mid + 1;
                }
            }
            else if (afterLineNumber < arr[mid].AfterLineNumber)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    /// <summary>Change the height of a line in pixels.</summary>
    public void SetDefaultLineHeight(double lineHeight)
    {
        _lineHeightsManager.DefaultLineHeight = lineHeight;
    }

    /// <summary>Changes the padding used to calculate vertical offsets.</summary>
    public void SetPadding(double paddingTop, double paddingBottom)
    {
        _paddingTop = paddingTop;
        _paddingBottom = paddingBottom;
    }

    /// <summary>Set the number of lines.</summary>
    /// <param name="lineCount">New number of lines.</param>
    /// <param name="customLineHeightData">The custom line heights of the new lines.</param>
    public void OnFlushed(int lineCount, IReadOnlyList<CustomLineHeightData> customLineHeightData)
    {
        _lineCount = lineCount;
        _lineHeightsManager = new LineHeightsManager(_lineHeightsManager.DefaultLineHeight, customLineHeightData);
    }

    public bool ChangeLineHeights(Action<ILineHeightChangeAccessor> callback)
    {
        var accessor = new LineHeightChangeAccessor(_lineHeightsManager);
        callback(accessor);
        return accessor.HadAChange;
    }

    public bool ChangeWhitespace(Action<IWhitespaceChangeAccessor> callback)
    {
        var accessor = new WhitespaceChangeAccessor(this);
        try
        {
            callback(accessor);
        }
        finally
        {
            _pendingChanges.Commit(this);
        }
        return accessor.HadAChange;
    }

    private void CommitPendingChanges(List<EditorWhitespace> inserts, List<PendingChange> changes, List<PendingRemove> removes)
    {
        if (inserts.Count > 0 || removes.Count > 0)
        {
            _minWidth = -1; /* marker for not being computed */
        }

        if (inserts.Count + changes.Count + removes.Count <= 1)
        {
            // when only one thing happened, handle it "delicately"
            foreach (var insert in inserts)
            {
                InsertWhitespace(insert);
            }
            foreach (var change in changes)
            {
                ChangeOneWhitespace(change.Id, change.NewAfterLineNumber, change.NewHeight);
            }
            foreach (var remove in removes)
            {
                var index = FindWhitespaceIndex(remove.Id);
                if (index == -1)
                {
                    continue;
                }
                RemoveWhitespace(index);
            }
            return;
        }

        // simply rebuild the entire datastructure

        var toRemove = new HashSet<string>();
        foreach (var remove in removes)
        {
            toRemove.Add(remove.Id);
        }

        var toChange = new Dictionary<string, PendingChange>();
        foreach (var change in changes)
        {
            toChange[change.Id] = change;
        }

        List<EditorWhitespace> ApplyRemoveAndChange(List<EditorWhitespace> whitespaces)
        {
            var result = new List<EditorWhitespace>();
            foreach (var whitespace in whitespaces)
            {
                if (toRemove.Contains(whitespace.Id))
                {
                    continue;
                }
                if (toChange.TryGetValue(whitespace.Id, out var change))
                {
                    whitespace.AfterLineNumber = change.NewAfterLineNumber;
                    whitespace.Height = change.NewHeight;
                }
                result.Add(whitespace);
            }
            return result;
        }

        var combined = ApplyRemoveAndChange(_arr);
        combined.AddRange(ApplyRemoveAndChange(inserts));
        // stable, like JavaScript's sort
        _arr = combined.OrderBy(static w => w.AfterLineNumber).ThenBy(static w => w.Ordinal).ToList();
        _prefixSumValidIndex = -1;
    }

    private void InsertWhitespace(EditorWhitespace whitespace)
    {
        var insertIndex = FindInsertionIndex(_arr, whitespace.AfterLineNumber, whitespace.Ordinal);
        _arr.Insert(insertIndex, whitespace);
        _prefixSumValidIndex = Math.Min(_prefixSumValidIndex, insertIndex - 1);
    }

    private int FindWhitespaceIndex(string id)
    {
        var arr = _arr;
        for (int i = 0, len = arr.Count; i < len; i++)
        {
            if (arr[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    private void ChangeOneWhitespace(string id, int newAfterLineNumber, double newHeight)
    {
        var index = FindWhitespaceIndex(id);
        if (index == -1)
        {
            return;
        }
        if (_arr[index].Height != newHeight)
        {
            _arr[index].Height = newHeight;
            _prefixSumValidIndex = Math.Min(_prefixSumValidIndex, index - 1);
        }
        if (_arr[index].AfterLineNumber != newAfterLineNumber)
        {
            // `afterLineNumber` changed for this whitespace

            // Record old whitespace
            var whitespace = _arr[index];

            // Since changing `afterLineNumber` can trigger a reordering, we're gonna remove this whitespace
            RemoveWhitespace(index);

            whitespace.AfterLineNumber = newAfterLineNumber;

            // And add it again
            InsertWhitespace(whitespace);
        }
    }

    private void RemoveWhitespace(int removeIndex)
    {
        _arr.RemoveAt(removeIndex);
        _prefixSumValidIndex = Math.Min(_prefixSumValidIndex, removeIndex - 1);
    }

    /// <summary>Notify the layouter that lines have been deleted (a continuous zone of lines).</summary>
    /// <param name="fromLineNumber">The line number at which the deletion started, inclusive</param>
    /// <param name="toLineNumber">The line number at which the deletion ended, inclusive</param>
    public void OnLinesDeleted(int fromLineNumber, int toLineNumber)
    {
        _lineCount -= toLineNumber - fromLineNumber + 1;
        for (int i = 0, len = _arr.Count; i < len; i++)
        {
            var afterLineNumber = _arr[i].AfterLineNumber;

            if (fromLineNumber <= afterLineNumber && afterLineNumber <= toLineNumber)
            {
                // The line this whitespace was after has been deleted
                //  => move whitespace to before first deleted line
                _arr[i].AfterLineNumber = fromLineNumber - 1;
            }
            else if (afterLineNumber > toLineNumber)
            {
                // The line this whitespace was after has been moved up
                //  => move whitespace up
                _arr[i].AfterLineNumber -= toLineNumber - fromLineNumber + 1;
            }
        }
        _lineHeightsManager.OnLinesDeleted(fromLineNumber, toLineNumber);
    }

    /// <summary>Notify the layouter that lines have been inserted (a continuous zone of lines).</summary>
    /// <param name="fromLineNumber">The line number at which the insertion started, inclusive</param>
    /// <param name="toLineNumber">The line number at which the insertion ended, inclusive.</param>
    public void OnLinesInserted(int fromLineNumber, int toLineNumber)
    {
        _lineCount += toLineNumber - fromLineNumber + 1;
        for (int i = 0, len = _arr.Count; i < len; i++)
        {
            var afterLineNumber = _arr[i].AfterLineNumber;

            if (fromLineNumber <= afterLineNumber)
            {
                _arr[i].AfterLineNumber += toLineNumber - fromLineNumber + 1;
            }
        }
        _lineHeightsManager.OnLinesInserted(fromLineNumber, toLineNumber);
    }

    /// <summary>Get the sum of all the whitespaces.</summary>
    public double GetWhitespacesTotalHeight()
    {
        if (_arr.Count == 0)
        {
            return 0;
        }
        return GetWhitespacesAccumulatedHeight(_arr.Count - 1);
    }

    /// <summary>
    /// Return the sum of the heights of the whitespaces at [0..index].
    /// This includes the whitespace at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the whitespace.</param>
    /// <returns>The sum of the heights of all whitespaces before the one at <paramref name="index"/>, including the one at <paramref name="index"/>.</returns>
    public double GetWhitespacesAccumulatedHeight(int index)
    {
        var startIndex = Math.Max(0, _prefixSumValidIndex + 1);
        if (startIndex == 0)
        {
            _arr[0].PrefixSum = _arr[0].Height;
            startIndex++;
        }

        for (var i = startIndex; i <= index; i++)
        {
            _arr[i].PrefixSum = _arr[i - 1].PrefixSum + _arr[i].Height;
        }
        _prefixSumValidIndex = Math.Max(_prefixSumValidIndex, index);
        return _arr[index].PrefixSum;
    }

    /// <summary>Get the sum of heights for all objects.</summary>
    /// <returns>The sum of heights for all objects.</returns>
    public double GetLinesTotalHeight()
    {
        var linesHeight = _lineHeightsManager.GetAccumulatedLineHeightsIncludingLineNumber(_lineCount);
        var whitespacesHeight = GetWhitespacesTotalHeight();

        return linesHeight + whitespacesHeight + _paddingTop + _paddingBottom;
    }

    /// <summary>Returns the accumulated height of whitespaces before the given line number.</summary>
    /// <param name="lineNumber">The line number</param>
    public double GetWhitespaceAccumulatedHeightBeforeLineNumber(int lineNumber)
    {
        var lastWhitespaceBeforeLineNumber = FindLastWhitespaceBeforeLineNumber(lineNumber);

        if (lastWhitespaceBeforeLineNumber == -1)
        {
            return 0;
        }

        return GetWhitespacesAccumulatedHeight(lastWhitespaceBeforeLineNumber);
    }

    private int FindLastWhitespaceBeforeLineNumber(int lineNumber)
    {
        // Find the whitespace before line number
        var arr = _arr;
        var low = 0;
        var high = arr.Count - 1;

        while (low <= high)
        {
            var delta = high - low;
            var halfDelta = delta / 2;
            var mid = low + halfDelta;

            if (arr[mid].AfterLineNumber < lineNumber)
            {
                if (mid + 1 >= arr.Count || arr[mid + 1].AfterLineNumber >= lineNumber)
                {
                    return mid;
                }
                else
                {
                    low = mid + 1;
                }
            }
            else
            {
                high = mid - 1;
            }
        }

        return -1;
    }

    private int FindFirstWhitespaceAfterLineNumber(int lineNumber)
    {
        var lastWhitespaceBeforeLineNumber = FindLastWhitespaceBeforeLineNumber(lineNumber);
        var firstWhitespaceAfterLineNumber = lastWhitespaceBeforeLineNumber + 1;

        if (firstWhitespaceAfterLineNumber < _arr.Count)
        {
            return firstWhitespaceAfterLineNumber;
        }

        return -1;
    }

    /// <summary>Find the index of the first whitespace which has <c>afterLineNumber</c> &gt;= <paramref name="lineNumber"/>.</summary>
    /// <returns>The index of the first whitespace with <c>afterLineNumber</c> &gt;= <paramref name="lineNumber"/> or -1 if no whitespace is found.</returns>
    public int GetFirstWhitespaceIndexAfterLineNumber(int lineNumber)
    {
        return FindFirstWhitespaceAfterLineNumber(lineNumber);
    }

    /// <summary>Get the vertical offset (the sum of heights for all objects above) a certain line number.</summary>
    /// <param name="lineNumber">The line number</param>
    /// <param name="includeViewZones">When true, the offset is the top of the whitespaces right above the line instead of the top of the line.</param>
    /// <returns>The sum of heights for all objects above <paramref name="lineNumber"/>.</returns>
    public double GetVerticalOffsetForLineNumber(int lineNumber, bool includeViewZones = false)
    {
        double previousLinesHeight;
        if (lineNumber > 1)
        {
            previousLinesHeight = _lineHeightsManager.GetAccumulatedLineHeightsIncludingLineNumber(lineNumber - 1);
        }
        else
        {
            previousLinesHeight = 0;
        }

        var previousWhitespacesHeight = GetWhitespaceAccumulatedHeightBeforeLineNumber(lineNumber - (includeViewZones ? 1 : 0));

        return previousLinesHeight + previousWhitespacesHeight + _paddingTop;
    }

    public double GetLineHeightForLineNumber(int lineNumber)
    {
        return _lineHeightsManager.HeightForLineNumber(lineNumber);
    }

    /// <summary>Get the vertical offset (the sum of heights for all objects above) a certain line number and also the line height of the line.</summary>
    /// <param name="lineNumber">The line number</param>
    /// <param name="includeViewZones">When true, the offset is below the whitespaces right after the line too.</param>
    /// <returns>The sum of heights for all objects above <paramref name="lineNumber"/>.</returns>
    public double GetVerticalOffsetAfterLineNumber(int lineNumber, bool includeViewZones = false)
    {
        var previousLinesHeight = _lineHeightsManager.GetAccumulatedLineHeightsIncludingLineNumber(lineNumber);
        var previousWhitespacesHeight = GetWhitespaceAccumulatedHeightBeforeLineNumber(lineNumber + (includeViewZones ? 1 : 0));
        return previousLinesHeight + previousWhitespacesHeight + _paddingTop;
    }

    /// <summary>Returns if there is any whitespace in the document.</summary>
    public bool HasWhitespace()
    {
        return GetWhitespacesCount() > 0;
    }

    /// <summary>The maximum min width for all whitespaces.</summary>
    public double GetWhitespaceMinWidth()
    {
        if (_minWidth == -1)
        {
            double minWidth = 0;
            for (int i = 0, len = _arr.Count; i < len; i++)
            {
                minWidth = Math.Max(minWidth, _arr[i].MinWidth);
            }
            _minWidth = minWidth;
        }
        return _minWidth;
    }

    /// <summary>Check if <paramref name="verticalOffset"/> is below all lines.</summary>
    public bool IsAfterLines(double verticalOffset)
    {
        var totalHeight = GetLinesTotalHeight();
        return verticalOffset > totalHeight;
    }

    public bool IsInTopPadding(double verticalOffset)
    {
        if (_paddingTop == 0)
        {
            return false;
        }
        return verticalOffset < _paddingTop;
    }

    public bool IsInBottomPadding(double verticalOffset)
    {
        if (_paddingBottom == 0)
        {
            return false;
        }
        var totalHeight = GetLinesTotalHeight();
        return verticalOffset >= totalHeight - _paddingBottom;
    }

    /// <summary>
    /// Find the first line number that is at or after vertical offset <paramref name="verticalOffset"/>.
    /// i.e. if getVerticalOffsetForLine(line) is x and getVerticalOffsetForLine(line + 1) is y, then
    /// getLineNumberAtOrAfterVerticalOffset(i) = line, x &lt;= i &lt; y.
    /// </summary>
    /// <param name="verticalOffset">The vertical offset to search at.</param>
    /// <returns>The line number at or after vertical offset <paramref name="verticalOffset"/>.</returns>
    public int GetLineNumberAtOrAfterVerticalOffset(double verticalOffset)
    {
        verticalOffset = Int32(verticalOffset);

        if (verticalOffset < 0)
        {
            return 1;
        }

        var linesCount = _lineCount;
        var minLineNumber = 1;
        var maxLineNumber = linesCount;

        while (minLineNumber < maxLineNumber)
        {
            var midLineNumber = (minLineNumber + maxLineNumber) / 2;

            var lineHeight = GetLineHeightForLineNumber(midLineNumber);
            var midLineNumberVerticalOffset = Int32(GetVerticalOffsetForLineNumber(midLineNumber));

            if (verticalOffset >= midLineNumberVerticalOffset + lineHeight)
            {
                // vertical offset is after mid line number
                minLineNumber = midLineNumber + 1;
            }
            else if (verticalOffset >= midLineNumberVerticalOffset)
            {
                // Hit
                return midLineNumber;
            }
            else
            {
                // vertical offset is before mid line number, but mid line number could still be what we're searching for
                maxLineNumber = midLineNumber;
            }
        }

        if (minLineNumber > linesCount)
        {
            return linesCount;
        }

        return minLineNumber;
    }

    /// <summary>Get all the lines and their relative vertical offsets that are positioned between <paramref name="verticalOffset1"/> and <paramref name="verticalOffset2"/>.</summary>
    /// <param name="verticalOffset1">The beginning of the viewport.</param>
    /// <param name="verticalOffset2">The end of the viewport.</param>
    /// <returns>A structure describing the lines positioned between <paramref name="verticalOffset1"/> and <paramref name="verticalOffset2"/>.</returns>
    public IPartialViewLinesViewportData GetLinesViewportData(double verticalOffset1, double verticalOffset2)
    {
        verticalOffset1 = Int32(verticalOffset1);
        verticalOffset2 = Int32(verticalOffset2);

        // Find first line number
        // We don't live in a perfect world, so the line number might start before or after verticalOffset1
        var startLineNumber = GetLineNumberAtOrAfterVerticalOffset(verticalOffset1);
        var startLineNumberVerticalOffset = Int32(GetVerticalOffsetForLineNumber(startLineNumber));

        var endLineNumber = _lineCount;

        // Also keep track of what whitespace we've got
        var whitespaceIndex = GetFirstWhitespaceIndexAfterLineNumber(startLineNumber);
        var whitespaceCount = GetWhitespacesCount();
        double currentWhitespaceHeight;
        int currentWhitespaceAfterLineNumber;

        if (whitespaceIndex == -1)
        {
            whitespaceIndex = whitespaceCount;
            currentWhitespaceAfterLineNumber = endLineNumber + 1;
            currentWhitespaceHeight = 0;
        }
        else
        {
            currentWhitespaceAfterLineNumber = GetAfterLineNumberForWhitespaceIndex(whitespaceIndex);
            currentWhitespaceHeight = Int32(GetHeightForWhitespaceIndex(whitespaceIndex));
        }

        var currentVerticalOffset = startLineNumberVerticalOffset;
        var currentLineRelativeOffset = currentVerticalOffset;

        // IE (all versions) cannot handle units above about 1,533,908 px, so every 500k pixels bring numbers down
        const double STEP_SIZE = 500000;
        double bigNumbersDelta = 0;
        if (startLineNumberVerticalOffset >= STEP_SIZE)
        {
            // Compute a delta that guarantees that lines are positioned at `lineHeight` increments
            bigNumbersDelta = Math.Floor(startLineNumberVerticalOffset / STEP_SIZE) * STEP_SIZE;
            bigNumbersDelta = Math.Floor(bigNumbersDelta / _lineHeightsManager.DefaultLineHeight) * _lineHeightsManager.DefaultLineHeight;

            currentLineRelativeOffset -= bigNumbersDelta;
        }

        var linesOffsets = new List<double>();

        var verticalCenter = verticalOffset1 + (verticalOffset2 - verticalOffset1) / 2;
        var centeredLineNumber = -1;

        // Figure out how far the lines go
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var lineHeight = GetLineHeightForLineNumber(lineNumber);
            if (centeredLineNumber == -1)
            {
                var currentLineTop = currentVerticalOffset;
                var currentLineBottom = currentVerticalOffset + lineHeight;
                if ((currentLineTop <= verticalCenter && verticalCenter < currentLineBottom) || currentLineTop > verticalCenter)
                {
                    centeredLineNumber = lineNumber;
                }
            }

            // Count current line height in the vertical offsets
            currentVerticalOffset += lineHeight;
            linesOffsets.Add(currentLineRelativeOffset);

            // Next line starts immediately after this one
            currentLineRelativeOffset += lineHeight;
            while (currentWhitespaceAfterLineNumber == lineNumber)
            {
                // Push down next line with the height of the current whitespace
                currentLineRelativeOffset += currentWhitespaceHeight;

                // Count current whitespace in the vertical offsets
                currentVerticalOffset += currentWhitespaceHeight;
                whitespaceIndex++;

                if (whitespaceIndex >= whitespaceCount)
                {
                    currentWhitespaceAfterLineNumber = endLineNumber + 1;
                }
                else
                {
                    currentWhitespaceAfterLineNumber = GetAfterLineNumberForWhitespaceIndex(whitespaceIndex);
                    currentWhitespaceHeight = Int32(GetHeightForWhitespaceIndex(whitespaceIndex));
                }
            }

            if (currentVerticalOffset >= verticalOffset2)
            {
                // We have covered the entire viewport area, time to stop
                endLineNumber = lineNumber;
                break;
            }
        }

        if (centeredLineNumber == -1)
        {
            centeredLineNumber = endLineNumber;
        }

        var endLineNumberVerticalOffset = Int32(GetVerticalOffsetForLineNumber(endLineNumber));

        var completelyVisibleStartLineNumber = startLineNumber;
        var completelyVisibleEndLineNumber = endLineNumber;

        if (completelyVisibleStartLineNumber < completelyVisibleEndLineNumber)
        {
            if (startLineNumberVerticalOffset < verticalOffset1)
            {
                completelyVisibleStartLineNumber++;
            }
        }
        if (completelyVisibleStartLineNumber < completelyVisibleEndLineNumber)
        {
            var endLineHeight = GetLineHeightForLineNumber(endLineNumber);
            if (endLineNumberVerticalOffset + endLineHeight > verticalOffset2)
            {
                completelyVisibleEndLineNumber--;
            }
        }

        return new PartialViewLinesViewportData(
            BigNumbersDelta: bigNumbersDelta,
            StartLineNumber: startLineNumber,
            EndLineNumber: endLineNumber,
            RelativeVerticalOffset: linesOffsets.ToArray(),
            CenteredLineNumber: centeredLineNumber,
            CompletelyVisibleStartLineNumber: completelyVisibleStartLineNumber,
            CompletelyVisibleEndLineNumber: completelyVisibleEndLineNumber,
            LineHeight: _lineHeightsManager.DefaultLineHeight);
    }

    public double GetVerticalOffsetForWhitespaceIndex(int whitespaceIndex)
    {
        var afterLineNumber = GetAfterLineNumberForWhitespaceIndex(whitespaceIndex);

        double previousLinesHeight;
        if (afterLineNumber >= 1)
        {
            previousLinesHeight = _lineHeightsManager.GetAccumulatedLineHeightsIncludingLineNumber(afterLineNumber);
        }
        else
        {
            previousLinesHeight = 0;
        }

        double previousWhitespacesHeight;
        if (whitespaceIndex > 0)
        {
            previousWhitespacesHeight = GetWhitespacesAccumulatedHeight(whitespaceIndex - 1);
        }
        else
        {
            previousWhitespacesHeight = 0;
        }
        return previousLinesHeight + previousWhitespacesHeight + _paddingTop;
    }

    public int GetWhitespaceIndexAtOrAfterVerticallOffset(double verticalOffset)
    {
        verticalOffset = Int32(verticalOffset);

        var minWhitespaceIndex = 0;
        var maxWhitespaceIndex = GetWhitespacesCount() - 1;

        if (maxWhitespaceIndex < 0)
        {
            return -1;
        }

        // Special case: nothing to be found
        var maxWhitespaceVerticalOffset = GetVerticalOffsetForWhitespaceIndex(maxWhitespaceIndex);
        var maxWhitespaceHeight = GetHeightForWhitespaceIndex(maxWhitespaceIndex);
        if (verticalOffset >= maxWhitespaceVerticalOffset + maxWhitespaceHeight)
        {
            return -1;
        }

        while (minWhitespaceIndex < maxWhitespaceIndex)
        {
            var midWhitespaceIndex = (minWhitespaceIndex + maxWhitespaceIndex) / 2;

            var midWhitespaceVerticalOffset = GetVerticalOffsetForWhitespaceIndex(midWhitespaceIndex);
            var midWhitespaceHeight = GetHeightForWhitespaceIndex(midWhitespaceIndex);

            if (verticalOffset >= midWhitespaceVerticalOffset + midWhitespaceHeight)
            {
                // vertical offset is after whitespace
                minWhitespaceIndex = midWhitespaceIndex + 1;
            }
            else if (verticalOffset >= midWhitespaceVerticalOffset)
            {
                // Hit
                return midWhitespaceIndex;
            }
            else
            {
                // vertical offset is before whitespace, but midWhitespaceIndex might still be what we're searching for
                maxWhitespaceIndex = midWhitespaceIndex;
            }
        }
        return minWhitespaceIndex;
    }

    /// <summary>Get exactly the whitespace that is layouted at <paramref name="verticalOffset"/>.</summary>
    /// <param name="verticalOffset">The vertical offset.</param>
    /// <returns>Precisely the whitespace that is layouted at <paramref name="verticalOffset"/> or null.</returns>
    public IViewWhitespaceViewportData? GetWhitespaceAtVerticalOffset(double verticalOffset)
    {
        verticalOffset = Int32(verticalOffset);

        var candidateIndex = GetWhitespaceIndexAtOrAfterVerticallOffset(verticalOffset);

        if (candidateIndex < 0)
        {
            return null;
        }

        if (candidateIndex >= GetWhitespacesCount())
        {
            return null;
        }

        var candidateTop = GetVerticalOffsetForWhitespaceIndex(candidateIndex);

        if (candidateTop > verticalOffset)
        {
            return null;
        }

        var candidateHeight = GetHeightForWhitespaceIndex(candidateIndex);
        var candidateId = GetIdForWhitespaceIndex(candidateIndex);
        var candidateAfterLineNumber = GetAfterLineNumberForWhitespaceIndex(candidateIndex);

        return new ViewWhitespaceViewportData(candidateId, candidateAfterLineNumber, candidateTop, candidateHeight);
    }

    /// <summary>Get a list of whitespaces that are positioned between <paramref name="verticalOffset1"/> and <paramref name="verticalOffset2"/>.</summary>
    /// <param name="verticalOffset1">The beginning of the viewport.</param>
    /// <param name="verticalOffset2">The end of the viewport.</param>
    /// <returns>An array with all the whitespaces in the viewport. If no whitespace is in viewport, the array is empty.</returns>
    public IViewWhitespaceViewportData[] GetWhitespaceViewportData(double verticalOffset1, double verticalOffset2)
    {
        verticalOffset1 = Int32(verticalOffset1);
        verticalOffset2 = Int32(verticalOffset2);

        var startIndex = GetWhitespaceIndexAtOrAfterVerticallOffset(verticalOffset1);
        var endIndex = GetWhitespacesCount() - 1;

        if (startIndex < 0)
        {
            return [];
        }

        var result = new List<IViewWhitespaceViewportData>();
        for (var i = startIndex; i <= endIndex; i++)
        {
            var top = GetVerticalOffsetForWhitespaceIndex(i);
            var height = GetHeightForWhitespaceIndex(i);
            if (top >= verticalOffset2)
            {
                break;
            }

            result.Add(new ViewWhitespaceViewportData(GetIdForWhitespaceIndex(i), GetAfterLineNumberForWhitespaceIndex(i), top, height));
        }

        return result.ToArray();
    }

    /// <summary>Get all whitespaces.</summary>
    public IEditorWhitespace[] GetWhitespaces()
    {
        return _arr.ToArray<IEditorWhitespace>();
    }

    /// <summary>The number of whitespaces.</summary>
    public int GetWhitespacesCount()
    {
        return _arr.Count;
    }

    /// <summary>Get the <c>id</c> for whitespace at index <paramref name="index"/>.</summary>
    /// <param name="index">The index of the whitespace.</param>
    /// <returns><c>id</c> of whitespace at <paramref name="index"/>.</returns>
    public string GetIdForWhitespaceIndex(int index)
    {
        return _arr[index].Id;
    }

    /// <summary>Get the <c>afterLineNumber</c> for whitespace at index <paramref name="index"/>.</summary>
    /// <param name="index">The index of the whitespace.</param>
    /// <returns><c>afterLineNumber</c> of whitespace at <paramref name="index"/>.</returns>
    public int GetAfterLineNumberForWhitespaceIndex(int index)
    {
        return _arr[index].AfterLineNumber;
    }

    /// <summary>Get the <c>height</c> for whitespace at index <paramref name="index"/>.</summary>
    /// <param name="index">The index of the whitespace.</param>
    /// <returns><c>height</c> of whitespace at <paramref name="index"/>.</returns>
    public double GetHeightForWhitespaceIndex(int index)
    {
        return _arr[index].Height;
    }

    // JavaScript's `value | 0`: the value truncated to a 32-bit integer (NaN and the infinities give 0).
    private static double Int32(double value) => double.IsFinite(value) ? unchecked((int)(long)value) : 0;

    private readonly record struct PendingChange(string Id, int NewAfterLineNumber, double NewHeight);

    private readonly record struct PendingRemove(string Id);

    private sealed class PendingChanges
    {
        private bool _hasPending;
        private List<EditorWhitespace> _inserts = new();
        private List<PendingChange> _changes = new();
        private List<PendingRemove> _removes = new();

        public void Insert(EditorWhitespace x)
        {
            _hasPending = true;
            _inserts.Add(x);
        }

        public void Change(PendingChange x)
        {
            _hasPending = true;
            _changes.Add(x);
        }

        public void Remove(PendingRemove x)
        {
            _hasPending = true;
            _removes.Add(x);
        }

        public void Commit(LinesLayout linesLayout)
        {
            if (!_hasPending)
            {
                return;
            }

            var inserts = _inserts;
            var changes = _changes;
            var removes = _removes;

            _hasPending = false;
            _inserts = new List<EditorWhitespace>();
            _changes = new List<PendingChange>();
            _removes = new List<PendingRemove>();

            linesLayout.CommitPendingChanges(inserts, changes, removes);
        }
    }

    private sealed class LineHeightChangeAccessor(LineHeightsManager lineHeightsManager) : ILineHeightChangeAccessor
    {
        public bool HadAChange { get; private set; }

        public void InsertOrChangeCustomLineHeight(string decorationId, int startLineNumber, int endLineNumber, double lineHeight)
        {
            HadAChange = true;
            lineHeightsManager.InsertOrChangeCustomLineHeight(decorationId, startLineNumber, endLineNumber, lineHeight);
        }

        public void RemoveCustomLineHeight(string decorationId)
        {
            HadAChange = true;
            lineHeightsManager.RemoveCustomLineHeight(decorationId);
        }
    }

    private sealed class WhitespaceChangeAccessor(LinesLayout linesLayout) : IWhitespaceChangeAccessor
    {
        public bool HadAChange { get; private set; }

        public string InsertWhitespace(int afterLineNumber, int ordinal, double heightInPx, double minWidth)
        {
            HadAChange = true;
            heightInPx = Int32(heightInPx);
            minWidth = Int32(minWidth);
            var id = linesLayout._instanceId + (++linesLayout._lastWhitespaceId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            linesLayout._pendingChanges.Insert(new EditorWhitespace(id, afterLineNumber, ordinal, heightInPx, minWidth));
            return id;
        }

        public void ChangeOneWhitespace(string id, int newAfterLineNumber, double newHeight)
        {
            HadAChange = true;
            newHeight = Int32(newHeight);
            linesLayout._pendingChanges.Change(new PendingChange(id, newAfterLineNumber, newHeight));
        }

        public void RemoveWhitespace(string id)
        {
            HadAChange = true;
            linesLayout._pendingChanges.Remove(new PendingRemove(id));
        }
    }
}

namespace TKCode.Arc;

/// <summary>
/// Column geometry of a grid at one width: the visible columns (pinned first), their left edges and widths, the pinned
/// count and edge, the gutter width and the total width. Drawing, hit testing, the selection and the header rows all
/// read the same instance.
/// </summary>
internal sealed class DataGridLayout
{
    public static readonly DataGridLayout Empty = new([], [], [], 0, 0, 0);

    public DataGridLayout(int[] accessors, double[] lefts, double[] widths, int pinCount, double gutter, double total)
    {
        Accessors = accessors;
        Lefts = lefts;
        Widths = widths;
        PinCount = pinCount;
        Gutter = gutter;
        Total = total;
        PinEdge = pinCount > 0 ? lefts[pinCount - 1] + widths[pinCount - 1] : gutter;
    }

    /// <summary>Accessor index of each visible column, in display order.</summary>
    public int[] Accessors { get; }

    public double[] Lefts { get; }

    public double[] Widths { get; }

    public int PinCount { get; }

    /// <summary>Width of the row gutter (row numbers and checkboxes); 0 without one.</summary>
    public double Gutter { get; }

    /// <summary>Right edge of the gutter and the pinned columns.</summary>
    public double PinEdge { get; }

    public double Total { get; }

    public int Count => Accessors.Length;

    public double Right(int column) => Lefts[column] + Widths[column];

    /// <summary>The visible column at content x (−1 left of the first column, Count − 1 past the last).</summary>
    public int ColumnAtContent(double x)
    {
        if (Count == 0 || x < Gutter)
        {
            return -1;
        }
        var lo = 0;
        var hi = Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (x < Right(mid))
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }
        return lo;
    }

    /// <summary>
    /// Builds the geometry. Pinned columns are scaled down to about half the width (never under 96px or their minimum)
    /// so a narrow grid still shows scrolling columns, and the last column absorbs spare width.
    /// </summary>
    public static DataGridLayout Compute(IReadOnlyList<int> accessors, IReadOnlyList<double> widths, IReadOnlyList<double> minWidths,
                                         int pinCount, double gutter, double viewportWidth, double pinnedFloor)
    {
        var count = accessors.Count;
        var w = widths.ToArray();
        var pinnedTotal = 0.0;
        for (var i = 0; i < pinCount; i++)
        {
            pinnedTotal += w[i];
        }
        var cap = viewportWidth > 0 && double.IsFinite(viewportWidth)
            ? Math.Max(pinnedFloor, Math.Floor(viewportWidth * 0.5) - gutter)
            : double.PositiveInfinity;
        if (pinnedTotal > cap)
        {
            for (var i = 0; i < pinCount; i++)
            {
                w[i] = Math.Max(Math.Min(minWidths[i], pinnedFloor), Math.Floor(w[i] * cap / pinnedTotal));
            }
        }
        var sum = gutter;
        foreach (var width in w)
        {
            sum += width;
        }
        if (count > 0 && double.IsFinite(viewportWidth) && viewportWidth > sum)
        {
            w[^1] += Math.Floor(viewportWidth - sum);
        }
        var lefts = new double[count];
        var x = gutter;
        for (var i = 0; i < count; i++)
        {
            lefts[i] = x;
            x += w[i];
        }
        return new DataGridLayout(accessors.ToArray(), lefts, w, Math.Min(pinCount, count), gutter, x);
    }
}

/// <summary>One cell of a change set: where it is and its value before and after.</summary>
internal readonly record struct DataGridCellEdit(object RowKey, int Accessor, object? Before, object? After);

/// <summary>A row removed by a delete, with the index it had in the source.</summary>
internal readonly record struct DataGridDeletedRow(int Index, object Item);

/// <summary>One undoable step: cell writes, or a row delete.</summary>
internal sealed class DataGridChangeSet
{
    public required DataGridChangeReason Reason { get; init; }

    public List<DataGridCellEdit> Cells { get; } = new();

    public List<DataGridDeletedRow> Deleted { get; } = new();

    /// <summary>The range to select and flash when the step is undone or redone.</summary>
    public DataGridCellRange? Range { get; init; }
}

/// <summary>Undo and redo stacks of change sets; a new step clears redo. Holds at most <see cref="Limit"/> steps.</summary>
internal sealed class DataGridUndoStack
{
    private readonly LinkedList<DataGridChangeSet> _past = new();
    private readonly Stack<DataGridChangeSet> _future = new();

    public int Limit { get; set; } = 100;

    public bool CanUndo => _past.Count > 0;

    public bool CanRedo => _future.Count > 0;

    public int PastCount => _past.Count;

    public int FutureCount => _future.Count;

    public void Push(DataGridChangeSet step)
    {
        _future.Clear();
        if (Limit <= 0)
        {
            _past.Clear();
            return;
        }
        _past.AddLast(step);
        Trim();
    }

    public DataGridChangeSet? PopUndo()
    {
        if (_past.Last is not { } last)
        {
            return null;
        }
        _past.RemoveLast();
        _future.Push(last.Value);
        return last.Value;
    }

    public DataGridChangeSet? PopRedo()
    {
        if (_future.Count == 0)
        {
            return null;
        }
        var step = _future.Pop();
        _past.AddLast(step);
        Trim();
        return step;
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
    }

    public void Trim()
    {
        while (_past.Count > Math.Max(0, Limit))
        {
            _past.RemoveFirst();
        }
    }
}

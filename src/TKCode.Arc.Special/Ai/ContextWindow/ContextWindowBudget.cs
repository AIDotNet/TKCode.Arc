namespace TKCode.Arc;

/// <summary>What one of the 200 cells of a <see cref="ContextWindow"/> shows.</summary>
internal enum ContextWindowCellKind : byte
{
    System,
    Tools,
    Files,
    Summary,
    Messages,

    /// <summary>Free space before the threshold line.</summary>
    Free,

    /// <summary>Free space after the threshold line (a lighter fill).</summary>
    FreeAfter,

    /// <summary>The output reserve: an outlined cell.</summary>
    Reserve
}

/// <summary>The seven rows of the legend, in order (also what a hovered row lights in the grid).</summary>
internal enum ContextWindowPart
{
    System,
    Tools,
    Files,
    Summary,
    Messages,
    Free,
    Reserve
}

/// <summary>One arrangement of the grid and the totals behind it (immutable).</summary>
internal sealed class ContextWindowLayout
{
    public ContextWindowLayout(long window, long reserve, double threshold, long[] categoryTokens, ContextWindowCellKind[] kinds,
        int[] categoryCells, int reserveCells, int thresholdRow)
    {
        Window = window;
        Reserve = reserve;
        Threshold = threshold;
        CategoryTokens = categoryTokens;
        Kinds = kinds;
        CategoryCells = categoryCells;
        ReserveCells = reserveCells;
        ThresholdRow = thresholdRow;
        var used = 0L;
        foreach (var tokens in categoryTokens)
        {
            used += tokens;
        }
        Used = used;
        Free = Math.Max(0, window - used - reserve);
        var usedCells = 0;
        foreach (var cells in categoryCells)
        {
            usedCells += cells;
        }
        UsedCells = usedCells;
        MessageCells = categoryCells[(int)ContextWindowCategory.Messages];
        MessageStart = usedCells - MessageCells;
    }

    public long Window { get; }

    public long Reserve { get; }

    public double Threshold { get; }

    /// <summary>Tokens per category (System, Tools, Files, Summary, Messages).</summary>
    public long[] CategoryTokens { get; }

    /// <summary>The 200 cells in reading order.</summary>
    public ContextWindowCellKind[] Kinds { get; }

    /// <summary>Cells per category.</summary>
    public int[] CategoryCells { get; }

    public int ReserveCells { get; }

    public int ReserveStart => ContextWindowBudget.CellCount - ReserveCells;

    /// <summary>Cells filled by the five categories (never past the reserve).</summary>
    public int UsedCells { get; }

    public int FreeCells => ReserveStart - UsedCells;

    /// <summary>The first conversation cell and how many there are (the cells the fold sweep dims).</summary>
    public int MessageStart { get; }

    public int MessageCells { get; }

    /// <summary>The threshold line sits after this many rows (0 to 10).</summary>
    public int ThresholdRow { get; }

    /// <summary>The first cell after the threshold line.</summary>
    public int ThresholdCell => ThresholdRow * ContextWindowBudget.Columns;

    /// <summary>Tokens per cell: the window over 200.</summary>
    public double PerCell => Window / (double)ContextWindowBudget.CellCount;

    /// <summary>Tokens used by every item; it can pass the window.</summary>
    public long Used { get; }

    /// <summary>Tokens left before the reserve, never below 0.</summary>
    public long Free { get; }

    /// <summary>At or past 85% of the threshold: sends and attachments warn.</summary>
    public bool IsWarning => ContextWindowBudget.IsWarning(Used, Window, Threshold);

    /// <summary>At or past the threshold: compaction runs.</summary>
    public bool IsOver => ContextWindowBudget.IsOver(Used, Window, Threshold);

    /// <summary>The tokens a legend row stands for.</summary>
    public long TokensOf(ContextWindowPart part) => part switch
    {
        ContextWindowPart.Free => Free,
        ContextWindowPart.Reserve => Reserve,
        _ => CategoryTokens[(int)part]
    };
}

/// <summary>The outcome of folding older messages into the summary.</summary>
internal sealed record ContextWindowCompaction(
    IReadOnlyList<ContextWindowItem> Items,
    IReadOnlyList<ContextWindowItem> Folded,
    long FoldedTokens,
    long SummaryTokens,
    ContextWindowItem? Summary,
    long Before,
    long After);

/// <summary>
/// The arithmetic of <see cref="ContextWindow"/> (measured from the reference, section 2b.8 of the notes): a fixed grid of
/// 200 cells, each a two-hundredth of the window; categories fill it in order by cumulative rounding (each boundary is the
/// rounded running total, so a category's cells are the difference of two boundaries); the reserve is counted back from
/// the last cell; the threshold line sits on a row boundary; compaction folds every message but the newest few into one
/// summary of about 6% of them, between 4K and 24K.
/// </summary>
internal static class ContextWindowBudget
{
    public const int CellCount = 200;
    public const int Columns = 20;
    public const int Rows = 10;

    /// <summary>The share of the threshold at which sends and attachments start to warn.</summary>
    public const double WarningShare = 0.85;

    /// <summary>The default summary: 6% of the folded tokens.</summary>
    public const double SummaryShare = 0.06;

    public const long SummaryMinimum = 4_000;
    public const long SummaryMaximum = 24_000;

    /// <summary>Tokens per category of <paramref name="items"/> (System, Tools, Files, Summary, Messages).</summary>
    public static long[] Sum(IEnumerable<ContextWindowItem> items)
    {
        var sums = new long[5];
        foreach (var item in items)
        {
            var category = (int)item.Category;
            if (category is >= 0 and < 5)
            {
                sums[category] += item.Tokens;
            }
        }
        return sums;
    }

    /// <summary>The row after which the threshold line sits: the threshold's cell rounded to a whole row.</summary>
    public static int ThresholdRow(double threshold)
    {
        var cell = ContextWindowFormat.Round(Math.Clamp(threshold, 0, 1) * CellCount);
        return (int)Math.Clamp(ContextWindowFormat.Round(cell / (double)Columns), 0, Rows);
    }

    // A hair of tolerance so that a limit such as 0.85 × 0.8 × 1M = 680,000 is reached by exactly 680,000 tokens.
    private const double Tolerance = 1e-6;

    public static bool IsWarning(long used, long window, double threshold) =>
        window > 0 && used >= WarningShare * threshold * window - Tolerance;

    public static bool IsOver(long used, long window, double threshold) => window > 0 && used >= threshold * window - Tolerance;

    /// <summary>The default summary size for <paramref name="foldedTokens"/>: 6%, at least 4K and at most 24K.</summary>
    public static long DefaultSummary(long foldedTokens) =>
        Math.Clamp(ContextWindowFormat.Round(SummaryShare * foldedTokens), SummaryMinimum, SummaryMaximum);

    /// <summary>Lays out the grid.</summary>
    public static ContextWindowLayout Compute(long window, long reserve, double threshold, long[] categoryTokens)
    {
        window = Math.Max(0, window);
        reserve = Math.Clamp(reserve, 0, window);
        var kinds = new ContextWindowCellKind[CellCount];
        var categoryCells = new int[5];
        var perCell = window / (double)CellCount;
        var reserveCells = perCell > 0 ? (int)Math.Clamp(ContextWindowFormat.Round(reserve / perCell), 0, CellCount) : 0;
        var reserveStart = CellCount - reserveCells;
        var thresholdRow = ThresholdRow(threshold);
        var thresholdCell = thresholdRow * Columns;
        var running = 0L;
        var boundary = 0;
        for (var c = 0; c < 5; c++)
        {
            running += categoryTokens[c];
            var next = perCell > 0
                ? (int)Math.Clamp(ContextWindowFormat.Round(running / perCell), 0, reserveStart)
                : 0;
            next = Math.Max(next, boundary);
            for (var i = boundary; i < next; i++)
            {
                kinds[i] = (ContextWindowCellKind)c;
            }
            categoryCells[c] = next - boundary;
            boundary = next;
        }
        for (var i = boundary; i < reserveStart; i++)
        {
            kinds[i] = i >= thresholdCell ? ContextWindowCellKind.FreeAfter : ContextWindowCellKind.Free;
        }
        for (var i = reserveStart; i < CellCount; i++)
        {
            kinds[i] = ContextWindowCellKind.Reserve;
        }
        return new ContextWindowLayout(window, reserve, threshold, (long[])categoryTokens.Clone(), kinds, categoryCells, reserveCells, thresholdRow);
    }

    /// <summary>The first cell whose kind differs between two arrangements (the start of a wave), or -1 when none does.</summary>
    public static int FirstChange(ContextWindowCellKind[]? before, ContextWindowCellKind[] after)
    {
        if (before is null)
        {
            return 0;
        }
        for (var i = 0; i < CellCount; i++)
        {
            if (before[i] != after[i])
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>How many messages a compaction would fold (all but the newest <paramref name="keep"/>).</summary>
    public static int FoldableCount(IEnumerable<ContextWindowItem> items, int keep)
    {
        var messages = 0;
        foreach (var item in items)
        {
            if (item.Category == ContextWindowCategory.Messages)
            {
                messages++;
            }
        }
        return Math.Max(0, messages - Math.Max(0, keep));
    }

    /// <summary>
    /// Folds every message but the newest <paramref name="keep"/> into the summary. The summary adds up: an existing summary
    /// item (the last one) is replaced by one that also holds the new tokens; otherwise a new item goes right before the
    /// first kept message. Returns null when there is nothing to fold. The input list and its items are not changed.
    /// </summary>
    public static ContextWindowCompaction? Compact(IReadOnlyList<ContextWindowItem> items, int keep,
        Func<IReadOnlyList<ContextWindowItem>, long>? summarizer, string summaryLabel)
    {
        var foldCount = FoldableCount(items, keep);
        if (foldCount <= 0)
        {
            return null;
        }
        var folded = new List<ContextWindowItem>(foldCount);
        var before = 0L;
        foreach (var item in items)
        {
            before += item.Tokens;
            if (item.Category == ContextWindowCategory.Messages && folded.Count < foldCount)
            {
                folded.Add(item);
            }
        }
        var foldedTokens = 0L;
        foreach (var item in folded)
        {
            foldedTokens += item.Tokens;
        }
        var summaryTokens = Math.Max(0, summarizer?.Invoke(folded) ?? DefaultSummary(foldedTokens));
        var lastSummary = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Category == ContextWindowCategory.Summary)
            {
                lastSummary = i;
            }
        }
        var result = new List<ContextWindowItem>(items.Count - foldCount + 1);
        ContextWindowItem? summary = null;
        var foldedSet = new HashSet<ContextWindowItem>(folded);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (foldedSet.Contains(item))
            {
                continue;
            }
            if (i == lastSummary)
            {
                summary = new ContextWindowItem(ContextWindowCategory.Summary, item.Label, item.Tokens + summaryTokens, item.Id);
                result.Add(summary);
                continue;
            }
            if (lastSummary < 0 && summary is null && item.Category == ContextWindowCategory.Messages)
            {
                summary = new ContextWindowItem(ContextWindowCategory.Summary, summaryLabel, summaryTokens, "summary");
                result.Add(summary);
            }
            result.Add(item);
        }
        if (summary is null)
        {
            summary = new ContextWindowItem(ContextWindowCategory.Summary, summaryLabel, summaryTokens, "summary");
            result.Add(summary);
        }
        var after = before - foldedTokens + summaryTokens;
        return new ContextWindowCompaction(result, folded, foldedTokens, summaryTokens, summary, before, after);
    }
}

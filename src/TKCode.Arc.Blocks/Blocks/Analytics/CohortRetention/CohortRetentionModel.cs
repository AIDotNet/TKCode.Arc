using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// The numbers behind a <see cref="CohortRetention"/>, computed once per segment: each cohort's rates, the size-weighted
/// average row (a month's average counts only the cohorts that have reached it), the two headline metrics and their
/// "newest n vs oldest n" changes, and the total. Pure: no UI, no animation.
/// </summary>
internal sealed class CohortRetentionModel
{
    /// <summary>An empty model (no cohorts).</summary>
    public static readonly CohortRetentionModel Empty = new([], 0);

    private readonly RetentionCohort?[] _sources;

    /// <summary>Builds the model; <paramref name="months"/> ≤ 0 takes the longest cohort.</summary>
    public CohortRetentionModel(IReadOnlyList<RetentionCohort> cohorts, int months)
    {
        var rows = cohorts.Count;
        var longest = 0;
        foreach (var cohort in cohorts)
        {
            longest = Math.Max(longest, cohort.MeasuredMonths);
        }
        Months = months > 0 ? months : longest;
        Rows = rows;
        _sources = new RetentionCohort?[rows];
        Labels = new string[rows];
        ShortLabels = new string[rows];
        Sizes = new int[rows];
        Retained = new int[rows][];
        Rates = new double[rows][];
        for (var r = 0; r < rows; r++)
        {
            var cohort = cohorts[r];
            _sources[r] = cohort;
            Labels[r] = cohort.Label;
            ShortLabels[r] = cohort.ShortLabel ?? cohort.Label;
            var size = Math.Max(0, cohort.Size);
            Sizes[r] = size;
            var retained = cohort.Retained ?? [];
            var length = Math.Min(retained.Count, Months);
            Retained[r] = new int[length];
            Rates[r] = new double[length];
            for (var m = 0; m < length; m++)
            {
                var kept = Math.Max(0, retained[m]);
                Retained[r][m] = kept;
                Rates[r][m] = size > 0 ? Math.Clamp(kept / (double)size, 0, 1) : 0;
            }
            TotalSize += size;
        }

        AverageRates = new double[Months];
        AverageRetained = new long[Months];
        AverageSizes = new long[Months];
        AverageCoverage = new int[Months];
        for (var m = 0; m < Months; m++)
        {
            for (var r = 0; r < rows; r++)
            {
                if (m < Rates[r].Length && Sizes[r] > 0)
                {
                    AverageRetained[m] += Retained[r][m];
                    AverageSizes[m] += Sizes[r];
                    AverageCoverage[m]++;
                }
            }
            AverageRates[m] = AverageSizes[m] > 0 ? Math.Clamp(AverageRetained[m] / (double)AverageSizes[m], 0, 1) : double.NaN;
        }
        var averageLength = 0;
        for (var m = 0; m < Months; m++)
        {
            if (AverageCoverage[m] > 0)
            {
                averageLength = m + 1;
            }
        }
        AverageLength = averageLength;
    }

    /// <summary>Cohorts (rows of the triangle).</summary>
    public int Rows { get; }

    /// <summary>Columns (months).</summary>
    public int Months { get; }

    /// <summary>Wide row names.</summary>
    public string[] Labels { get; }

    /// <summary>Compact row names.</summary>
    public string[] ShortLabels { get; }

    /// <summary>Cohort sizes.</summary>
    public int[] Sizes { get; }

    /// <summary>Retained counts per cohort, one per measured month.</summary>
    public int[][] Retained { get; }

    /// <summary>Rates (0–1) per cohort, one per measured month.</summary>
    public double[][] Rates { get; }

    /// <summary>Size-weighted average rate per month (NaN where no cohort has reached the month).</summary>
    public double[] AverageRates { get; }

    /// <summary>Retained counts summed over the cohorts that reached each month.</summary>
    public long[] AverageRetained { get; }

    /// <summary>Sizes summed over the cohorts that reached each month.</summary>
    public long[] AverageSizes { get; }

    /// <summary>How many cohorts reached each month.</summary>
    public int[] AverageCoverage { get; }

    /// <summary>Months with an average (the average row's length).</summary>
    public int AverageLength { get; }

    /// <summary>Everyone who started, over every cohort.</summary>
    public long TotalSize { get; }

    /// <summary>True without cohorts or months.</summary>
    public bool IsEmpty => Rows == 0 || Months == 0;

    /// <summary>The source cohort of row <paramref name="row"/>.</summary>
    public RetentionCohort? Source(int row) => row >= 0 && row < _sources.Length ? _sources[row] : null;

    /// <summary>Measured months of row <paramref name="row"/> (the average row's length for the average).</summary>
    public int Length(int row, bool isAverage = false) => isAverage ? AverageLength : row >= 0 && row < Rows ? Rates[row].Length : 0;

    /// <summary>Measured months of the cell's row.</summary>
    public int Length(CohortCell cell) => Length(cell.Row, cell.IsAverage);

    /// <summary>True when the cell exists (measured cohort month, or an average month that some cohort reached).</summary>
    public bool Contains(CohortCell cell)
        => cell.MonthIndex >= 0 && cell.MonthIndex < Length(cell) && (cell.IsAverage ? AverageCoverage[cell.MonthIndex] > 0 : cell.Row >= 0 && cell.Row < Rows);

    /// <summary>The cell's rate (0–1); NaN when it does not exist.</summary>
    public double Rate(CohortCell cell)
    {
        if (!Contains(cell))
        {
            return double.NaN;
        }
        return cell.IsAverage ? AverageRates[cell.MonthIndex] : Rates[cell.Row][cell.MonthIndex];
    }

    /// <summary>The cell's retained count (summed for the average row).</summary>
    public long RetainedAt(CohortCell cell)
        => !Contains(cell) ? 0 : cell.IsAverage ? AverageRetained[cell.MonthIndex] : Retained[cell.Row][cell.MonthIndex];

    /// <summary>The cell's denominator (summed for the average row).</summary>
    public long SizeAt(CohortCell cell) => !Contains(cell) ? 0 : cell.IsAverage ? AverageSizes[cell.MonthIndex] : Sizes[cell.Row];

    /// <summary>The month's average rate, or null when no cohort reached it.</summary>
    public double? AverageAt(int monthIndex)
        => monthIndex >= 0 && monthIndex < Months && AverageCoverage[monthIndex] > 0 ? AverageRates[monthIndex] : null;

    /// <summary>
    /// The change between the newest <paramref name="count"/> and the oldest <paramref name="count"/> cohorts that have
    /// reached the month: mean of the newest rates − mean of the oldest rates (a rate difference; × 100 for points). Null
    /// when fewer than one cohort reached the month.
    /// </summary>
    public double? CompareChange(int monthIndex, int count)
    {
        if (monthIndex < 0 || monthIndex >= Months || count <= 0)
        {
            return null;
        }
        var reached = new List<double>();
        for (var r = 0; r < Rows; r++)
        {
            if (monthIndex < Rates[r].Length && Sizes[r] > 0)
            {
                reached.Add(Rates[r][monthIndex]);
            }
        }
        if (reached.Count == 0)
        {
            return null;
        }
        var n = Math.Min(count, reached.Count);
        double oldest = 0, newest = 0;
        for (var i = 0; i < n; i++)
        {
            oldest += reached[i];
            newest += reached[reached.Count - 1 - i];
        }
        return (newest - oldest) / n;
    }

    /// <summary>
    /// The heat step of a rate: the number of thresholds at or below it (thresholds are closed at the bottom, so 0.20 is
    /// step 1 with the default 0.20 / 0.35 / 0.50 / 0.65 / 0.80, and 0.798 stays step 4 although it shows as "80").
    /// </summary>
    public static int Step(double rate, IReadOnlyList<double> thresholds)
    {
        var step = 0;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (rate >= thresholds[i] - 1e-12)
            {
                step = i + 1;
            }
        }
        return step;
    }

    /// <summary>
    /// The cursor after a key (the measured keyboard model): with no cursor any arrow, Home or End lands on the first cohort's
    /// first month; ←/→ move inside the row and stop at its ends; ↑/↓ move between rows, keep the month when the row has it
    /// and otherwise take the row's last measured month; ↓ from the newest cohort goes to the average row and ↑ from the
    /// average row back to the newest cohort; Home / End jump to the row's first and last measured month. Returns null for
    /// other keys.
    /// </summary>
    public CohortCell? Move(CohortCell? current, Key key, bool hasAverage)
    {
        if (key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End))
        {
            return null;
        }
        if (IsEmpty)
        {
            return null;
        }
        if (current is not { } cell || !Contains(cell) || (cell.IsAverage && !hasAverage))
        {
            return First(hasAverage);
        }
        var length = Length(cell);
        switch (key)
        {
            case Key.Left:
                return cell with { MonthIndex = Math.Max(0, cell.MonthIndex - 1) };
            case Key.Right:
                return cell with { MonthIndex = Math.Min(length - 1, cell.MonthIndex + 1) };
            case Key.Home:
                return cell with { MonthIndex = 0 };
            case Key.End:
                return cell with { MonthIndex = Math.Max(0, length - 1) };
            case Key.Up:
                if (cell.IsAverage)
                {
                    return LastCohortRow() is { } last ? Clamp(new CohortCell(last, cell.MonthIndex)) : cell;
                }
                return PreviousRow(cell.Row) is { } previous ? Clamp(new CohortCell(previous, cell.MonthIndex)) : cell;
            default:
                if (cell.IsAverage)
                {
                    return cell;
                }
                if (NextRow(cell.Row) is { } next)
                {
                    return Clamp(new CohortCell(next, cell.MonthIndex));
                }
                return hasAverage && AverageLength > 0 ? Clamp(CohortCell.Average(cell.MonthIndex)) : cell;
        }
    }

    /// <summary>The first cell the keyboard lands on: the oldest cohort's first month (or the average's when no cohort has one).</summary>
    public CohortCell? First(bool hasAverage)
    {
        for (var r = 0; r < Rows; r++)
        {
            if (Rates[r].Length > 0)
            {
                return new CohortCell(r, 0);
            }
        }
        return hasAverage && AverageLength > 0 ? CohortCell.Average(0) : null;
    }

    private CohortCell Clamp(CohortCell cell) => cell with { MonthIndex = Math.Clamp(cell.MonthIndex, 0, Math.Max(0, Length(cell) - 1)) };

    private int? PreviousRow(int row)
    {
        for (var r = row - 1; r >= 0; r--)
        {
            if (Rates[r].Length > 0)
            {
                return r;
            }
        }
        return null;
    }

    private int? NextRow(int row)
    {
        for (var r = row + 1; r < Rows; r++)
        {
            if (Rates[r].Length > 0)
            {
                return r;
            }
        }
        return null;
    }

    private int? LastCohortRow() => PreviousRow(Rows);

    /// <summary>True when the two models draw the same cells (same rows, months and row lengths): a segment switch can animate.</summary>
    public bool HasSameShape(CohortRetentionModel other)
    {
        if (other.Rows != Rows || other.Months != Months || other.AverageLength != AverageLength)
        {
            return false;
        }
        for (var r = 0; r < Rows; r++)
        {
            if (other.Rates[r].Length != Rates[r].Length)
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// The plot's geometry at one width (the measured svg coordinates): label column, row pitch, pixel-snapped columns with a
/// 2px gap, the average row below a gap, the x axis band and the curves' y scale. Wide: labels 136, pitch 30, cells 28
/// (r 5), average gap 10, height 430 for 12 cohorts, y = 390 − rate·380. Compact (plot narrower than 600): labels 56,
/// pitch 24, cells 22 (r 3), gap 8, height 350, y = 310 − rate·300.
/// </summary>
internal readonly record struct CohortRetentionLayout(
    double Width,
    int Rows,
    int Months,
    bool HasAverage,
    bool IsCompact,
    double LabelWidth,
    double Pitch,
    double CellHeight,
    double CellRadius,
    double AverageGap,
    double AxisBand = 30,
    double PlotTop = 10,
    double PlotInset = 10)
{
    /// <summary>One column's width.</summary>
    public double ColumnWidth => Months > 0 ? Math.Max(0, Width - LabelWidth) / Months : 0;

    /// <summary>Left edge of column <paramref name="i"/>'s cell: <c>round(label + i·colW) + 1</c>.</summary>
    public double X0(int i) => Math.Round(LabelWidth + i * ColumnWidth, MidpointRounding.AwayFromZero) + 1;

    /// <summary>Right edge of column <paramref name="i"/>'s cell: <c>round(label + (i + 1)·colW) − 1</c>.</summary>
    public double X1(int i) => Math.Round(LabelWidth + (i + 1) * ColumnWidth, MidpointRounding.AwayFromZero) - 1;

    /// <summary>Column centre (cells, points, month numbers).</summary>
    public double Cx(int i) => (X0(i) + X1(i)) / 2;

    /// <summary>Top of the band of cohort row <paramref name="row"/> (the cell starts 1 lower).</summary>
    public double RowBand(int row) => row * Pitch;

    /// <summary>Top of the average row's band.</summary>
    public double AverageBand => Rows * Pitch + AverageGap;

    /// <summary>Top of a cell.</summary>
    public double CellTop(int row, bool isAverage) => (isAverage ? AverageBand : RowBand(row)) + (Pitch - CellHeight) / 2;

    /// <summary>Vertical centre of a row.</summary>
    public double RowCenter(int row, bool isAverage) => (isAverage ? AverageBand : RowBand(row)) + Pitch / 2;

    /// <summary>The plot's height: rows, the average row and the axis band.</summary>
    public double Height => Rows * Pitch + (HasAverage ? AverageGap + Pitch : 0) + AxisBand;

    /// <summary>The curves' 0% line (390 wide, 310 compact).</summary>
    public double PlotBottom => Height - AxisBand - PlotInset;

    /// <summary>The axis labels' centre line (421 wide, 341 compact).</summary>
    public double AxisY => Height - 9;

    /// <summary>The y of a rate in the curves view.</summary>
    public double YOf(double rate) => PlotBottom - rate * (PlotBottom - PlotTop);

    /// <summary>Gridline y for tick <paramref name="i"/> of 5 (100% first), half-pixel aligned.</summary>
    public double GridY(int i) => Math.Round(PlotTop + i * (PlotBottom - PlotTop) / 4) + 0.5;

    /// <summary>The cell under a point in the heatmap: the row band and the column band (gaps belong to the cell before).</summary>
    public CohortCell? HitHeatmap(Point p, CohortRetentionModel model)
    {
        if (p.X < LabelWidth || p.X >= Width || p.Y < 0 || Months == 0)
        {
            return null;
        }
        var col = (int)Math.Floor((p.X - LabelWidth) / ColumnWidth);
        if (col < 0 || col >= Months)
        {
            return null;
        }
        CohortCell cell;
        if (p.Y < Rows * Pitch)
        {
            cell = new CohortCell((int)Math.Floor(p.Y / Pitch), col);
        }
        else if (HasAverage && p.Y >= AverageBand && p.Y < AverageBand + Pitch)
        {
            cell = CohortCell.Average(col);
        }
        else
        {
            return null;
        }
        return model.Contains(cell) ? cell : null;
    }

    /// <summary>
    /// The nearest point in the curves view: the month nearest x (<c>round((x − label)/colW − .5)</c>, clamped), then the
    /// cohort (or the average) whose point at that month is nearest y. <paramref name="pointY"/> gives each point's drawn y.
    /// </summary>
    public CohortCell? HitCurves(Point p, CohortRetentionModel model, Func<CohortCell, double> pointY)
    {
        if (p.X < LabelWidth || Months == 0 || model.IsEmpty)
        {
            return null;
        }
        var col = (int)Math.Round((p.X - LabelWidth) / ColumnWidth - 0.5, MidpointRounding.AwayFromZero);
        col = Math.Clamp(col, 0, Months - 1);
        CohortCell? best = null;
        var bestDistance = double.MaxValue;
        for (var r = 0; r < Rows; r++)
        {
            var cell = new CohortCell(r, col);
            if (!model.Contains(cell))
            {
                continue;
            }
            var d = Math.Abs(pointY(cell) - p.Y);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = cell;
            }
        }
        if (HasAverage)
        {
            var average = CohortCell.Average(col);
            if (model.Contains(average))
            {
                var d = Math.Abs(pointY(average) - p.Y);
                if (d < bestDistance)
                {
                    best = average;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Where the hover card goes (measured): beside the cursor box, <paramref name="gap"/> to its right, or to its left when it
    /// would pass the plot's right edge; centred on the box vertically and clamped into the plot.
    /// </summary>
    public static Point CardPosition(Rect box, Size card, double plotWidth, double plotHeight, double gap)
    {
        var x = box.Right + gap;
        if (x + card.Width > plotWidth)
        {
            x = box.Left - gap - card.Width;
        }
        var y = box.Center.Y - card.Height / 2;
        y = plotHeight <= card.Height ? 0 : Math.Clamp(y, 0, plotHeight - card.Height);
        return new Point(x, y);
    }
}

/// <summary>
/// The heatmap ↔ curves morph as one master progress p (0 = heatmap, 1 = curves) driven by a spring (k 72, c 15.3), with
/// every part taking a linear slice of p (the joint fit of the measured frames, rms ≤ .009). The slices move later along the
/// diagonal key r + c (the average row counts as row n), so the change spreads from the top-left corner; going back, p
/// returns to 0 and every slice plays in reverse: the last keys move first, and each cell moves before it grows.
/// </summary>
internal static class CohortRetentionMorph
{
    /// <summary>Shrink from cell to point: [.24, .54] for key 0, 0.012 later per key.</summary>
    public static double Size(double p, int key) => Slice(p, 0.24 + 0.012 * key, 0.54 + 0.012 * key);

    /// <summary>Row centre → data y: [.40, .78] for key 0, start 0.0139 later per key, end 0.0109 later (≤ 1).</summary>
    public static double Y(double p, int key) => Slice(p, 0.40 + 0.0139 * key, Math.Min(1, 0.78 + 0.0109 * key));

    /// <summary>The number inside a cell: gone after the first third of its shrink.</summary>
    public static double TextOpacity(double sizeProgress) => 1 - Slice(sizeProgress, 0, 0.35);

    /// <summary>Row labels and sizes fade out over [.24, .54].</summary>
    public static double Labels(double p) => 1 - Slice(p, 0.24, 0.54);

    /// <summary>The lines fade in over [.56, .92].</summary>
    public static double Curves(double p) => Slice(p, 0.56, 0.92);

    /// <summary>The y grid and its labels fade in over [.72, 1].</summary>
    public static double Grid(double p) => Slice(p, 0.72, 1);

    /// <summary>Linear map of p over [a, b] to [0, 1], clamped.</summary>
    public static double Slice(double p, double a, double b) => b <= a ? (p >= b ? 1 : 0) : Math.Clamp((p - a) / (b - a), 0, 1);

    /// <summary>The diagonal key of a cell (the average row is row <paramref name="rows"/>).</summary>
    public static int Key(CohortCell cell, int rows) => (cell.IsAverage ? rows : cell.Row) + cell.MonthIndex;

    /// <summary>Stagger of the value springs on a segment switch: 9ms per key (8.5–9.5 measured).</summary>
    public const double ValueStaggerSeconds = 0.009;
}

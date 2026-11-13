namespace TKCode.Arc;

/// <summary>One raw wind observation: where the wind comes from and how fast it blows.</summary>
/// <param name="Direction">Degrees clockwise from north that the wind blows from (0 and 360 are north).</param>
/// <param name="Speed">Wind speed, in the unit of the bin minimums (m/s by default).</param>
public readonly record struct WindObservation(double Direction, double Speed);

/// <summary>Raw observations sorted into direction sectors and speed bins by <see cref="WindRoseBinning"/>.</summary>
public sealed class WindRoseData
{
    internal WindRoseData(IReadOnlyList<WindRoseRow> rows, double calm, int total, int skipped)
    {
        Rows = rows;
        Calm = calm;
        Total = total;
        Skipped = skipped;
    }

    /// <summary>One row per sector, from sector 0; its direction is the sector's centre in degrees and its values are counts per bin.</summary>
    public IReadOnlyList<WindRoseRow> Rows { get; }

    /// <summary>How many observations were calm (bind it to <see cref="WindRoseChart.Calm"/>).</summary>
    public double Calm { get; }

    /// <summary>How many observations were counted, calm included.</summary>
    public int Total { get; }

    /// <summary>How many observations were left out: a direction or speed that is not a number, or a negative speed.</summary>
    public int Skipped { get; }
}

/// <summary>
/// Sorts raw wind observations into the sectors and speed bins of a <see cref="WindRoseChart"/>. Sector <c>i</c> covers
/// <c>[i·w − w/2, i·w + w/2)</c> degrees with <c>w = 360 / sectorCount</c>, so sector 0 is centred on north and 360° is
/// north again. An observation slower than the calm threshold is calm; the others go to the last bin whose minimum is not
/// above their speed (speeds under the first minimum go to the first bin).
/// </summary>
public static class WindRoseBinning
{
    /// <summary>The bins a chart without <see cref="WindRoseChart.Bins"/> uses: 0.5, 2, 4, 6, 8 and 10 (m/s).</summary>
    public static IReadOnlyList<double> DefaultBinMinimums { get; } = [0.5, 2, 4, 6, 8, 10];

    /// <summary>Speeds below 0.5 (m/s) are calm by default.</summary>
    public const double DefaultCalmThreshold = 0.5;

    /// <summary>The sector a direction (degrees clockwise from north, any number of turns) falls in, or -1 when it is not a number.</summary>
    public static int SectorOf(double degrees, int sectorCount)
    {
        if (!double.IsFinite(degrees) || sectorCount <= 0)
        {
            return -1;
        }
        var width = 360.0 / sectorCount;
        var turn = ((degrees % 360) + 360) % 360;
        var index = (int)Math.Floor((turn + width / 2) / width);
        return index % sectorCount;
    }

    /// <summary>The bin a speed falls in: the last one whose minimum is not above it, 0 below the first minimum, -1 when it is not a number or negative.</summary>
    public static int BinOf(double speed, IReadOnlyList<double> binMinimums)
    {
        if (!double.IsFinite(speed) || speed < 0 || binMinimums.Count == 0)
        {
            return -1;
        }
        var bin = 0;
        for (var i = 1; i < binMinimums.Count; i++)
        {
            if (speed >= binMinimums[i])
            {
                bin = i;
            }
        }
        return bin;
    }

    /// <summary>Counts observations per sector and speed bin.</summary>
    /// <param name="observations">The raw observations.</param>
    /// <param name="sectorCount">Number of sectors (as <see cref="WindRoseChart.SectorCount"/>).</param>
    /// <param name="binMinimums">Lower bound of each speed bin, ascending; null uses <see cref="DefaultBinMinimums"/>.</param>
    /// <param name="calmThreshold">Speeds below this are calm.</param>
    public static WindRoseData Bin(IEnumerable<WindObservation> observations, int sectorCount, IReadOnlyList<double>? binMinimums = null,
                                   double calmThreshold = DefaultCalmThreshold)
        => Bin(observations, static o => o.Direction, static o => o.Speed, sectorCount, binMinimums, calmThreshold);

    /// <summary>Counts any items per sector and speed bin, reading their direction and speed through the selectors.</summary>
    /// <param name="items">The raw observations.</param>
    /// <param name="direction">Reads an item's direction in degrees clockwise from north.</param>
    /// <param name="speed">Reads an item's speed.</param>
    /// <param name="sectorCount">Number of sectors (as <see cref="WindRoseChart.SectorCount"/>).</param>
    /// <param name="binMinimums">Lower bound of each speed bin, ascending; null uses <see cref="DefaultBinMinimums"/>.</param>
    /// <param name="calmThreshold">Speeds below this are calm.</param>
    public static WindRoseData Bin<T>(IEnumerable<T> items, Func<T, double> direction, Func<T, double> speed, int sectorCount,
                                      IReadOnlyList<double>? binMinimums = null, double calmThreshold = DefaultCalmThreshold)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentNullException.ThrowIfNull(speed);
        if (sectorCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectorCount), sectorCount, "A wind rose needs at least one sector.");
        }
        var minimums = binMinimums is { Count: > 0 } ? binMinimums : DefaultBinMinimums;
        var counts = new double[sectorCount, minimums.Count];
        var calm = 0;
        var total = 0;
        var skipped = 0;
        foreach (var item in items)
        {
            var s = speed(item);
            if (!double.IsFinite(s) || s < 0)
            {
                skipped++;
                continue;
            }
            if (s < calmThreshold)
            {
                // A calm reading has no meaningful direction (stations often report 0 or nothing).
                calm++;
                total++;
                continue;
            }
            var sector = SectorOf(direction(item), sectorCount);
            if (sector < 0)
            {
                skipped++;
                continue;
            }
            counts[sector, BinOf(s, minimums)]++;
            total++;
        }
        var rows = new WindRoseRow[sectorCount];
        for (var i = 0; i < sectorCount; i++)
        {
            var values = new double[minimums.Count];
            for (var b = 0; b < values.Length; b++)
            {
                values[b] = counts[i, b];
            }
            rows[i] = new WindRoseRow(i * 360.0 / sectorCount, values);
        }
        return new WindRoseData(rows, calm, total, skipped);
    }
}

using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A price tier of a <see cref="SeatMap"/> ("Premium", $120).</summary>
public sealed class SeatMapTier
{
    /// <summary>The identifier sections refer to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The tier's name in the legend.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The price of one seat in the tier.</summary>
    public decimal Price { get; set; }

    /// <summary>Which of the theme's four series colors the tier's seats take (1 to 4).</summary>
    public int ColorIndex { get; set; } = 1;
}

/// <summary>
/// A block of seats of a <see cref="SeatMap"/>: rows of equal length placed in the map's own units (the map scales them to
/// fit), optionally bent into an arc toward the stage, with aisles after given seat numbers.
/// </summary>
public sealed class SeatMapSection
{
    /// <summary>Short identifier used in seat ids ("ORC").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The section's name ("Orchestra").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The <see cref="SeatMapTier.Id"/> its seats are sold at.</summary>
    public string TierId { get; set; } = string.Empty;

    /// <summary>How many rows (lettered from <see cref="FirstRow"/>).</summary>
    public int Rows { get; set; } = 1;

    /// <summary>Seats in each row (numbered from 1, left to right).</summary>
    public int SeatsPerRow { get; set; } = 1;

    /// <summary>The letter of the first row.</summary>
    public char FirstRow { get; set; } = 'A';

    /// <summary>Left edge of the section, in map units (one seat is about 1 unit wide).</summary>
    public double X { get; set; }

    /// <summary>Top edge of the section, in map units.</summary>
    public double Y { get; set; }

    /// <summary>How far the middle of each row bows toward the stage, in map units (0 keeps rows straight).</summary>
    public double Curve { get; set; }

    /// <summary>Seat numbers after which an aisle (one seat wide) opens.</summary>
    public IReadOnlyList<int> AisleAfter { get; set; } = [];
}

/// <summary>One seat of a <see cref="SeatMap"/>, as laid out.</summary>
/// <param name="Id">"ORC-C-12": section id, row letter and number.</param>
/// <param name="Section">The section.</param>
/// <param name="Row">The row letter.</param>
/// <param name="Number">The seat number in the row.</param>
/// <param name="Tier">The tier it is sold at (null when the section names an unknown tier).</param>
/// <param name="Position">Its center in map units.</param>
public sealed record SeatMapSeat(string Id, SeatMapSection Section, char Row, int Number, SeatMapTier? Tier, Point Position)
{
    /// <summary>The seat's price (0 without a tier).</summary>
    public decimal Price => Tier?.Price ?? 0;
}

/// <summary>The chosen seats of a <see cref="SeatMap"/> changed, or Continue was pressed.</summary>
public sealed class SeatMapSelectionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SeatMapSelectionEventArgs(RoutedEvent routedEvent, IReadOnlyList<SeatMapSeat> seats, decimal total) : base(routedEvent)
    {
        Seats = seats;
        Total = total;
    }

    /// <summary>The chosen seats, in the order they were chosen.</summary>
    public IReadOnlyList<SeatMapSeat> Seats { get; }

    /// <summary>What they cost with the fees.</summary>
    public decimal Total { get; }
}

/// <summary>Lays out sections into seats and finds seats by position or direction (no UI).</summary>
internal static class SeatMapLayout
{
    /// <summary>Every seat of <paramref name="sections"/>, section by section, row by row, left to right.</summary>
    public static List<SeatMapSeat> Build(IEnumerable<SeatMapSection> sections, IEnumerable<SeatMapTier> tiers)
    {
        var tierById = tiers.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        var seats = new List<SeatMapSeat>();
        foreach (var section in sections)
        {
            var aisles = section.AisleAfter.ToHashSet();
            var columns = new double[section.SeatsPerRow];
            double x = 0;
            for (var n = 1; n <= section.SeatsPerRow; n++)
            {
                columns[n - 1] = x;
                x += aisles.Contains(n) ? 2 : 1;
            }
            var width = Math.Max(1, x - 1);
            for (var r = 0; r < section.Rows; r++)
            {
                for (var n = 1; n <= section.SeatsPerRow; n++)
                {
                    var t = section.SeatsPerRow > 1 ? columns[n - 1] / width * 2 - 1 : 0;
                    var bow = section.Curve * (1 - t * t);
                    var position = new Point(section.X + columns[n - 1] + 0.5, section.Y + r * 1.15 + 0.5 - bow);
                    var row = (char)(section.FirstRow + r);
                    seats.Add(new SeatMapSeat($"{section.Id}-{row}-{n}", section, row, n,
                        tierById.GetValueOrDefault(section.TierId), position));
                }
            }
        }
        return seats;
    }

    /// <summary>The extent of the seats in map units (with half a seat of margin).</summary>
    public static Rect Bounds(IReadOnlyList<SeatMapSeat> seats)
    {
        if (seats.Count == 0)
        {
            return new Rect(0, 0, 1, 1);
        }
        var minX = seats.Min(s => s.Position.X) - 0.6;
        var minY = seats.Min(s => s.Position.Y) - 0.6;
        var maxX = seats.Max(s => s.Position.X) + 0.6;
        var maxY = seats.Max(s => s.Position.Y) + 0.6;
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>The nearest seat from <paramref name="from"/> in a direction (dx, dy), preferring seats in line; −1 when none.</summary>
    public static int Step(IReadOnlyList<SeatMapSeat> seats, int from, int dx, int dy)
    {
        if (from < 0 || from >= seats.Count)
        {
            return seats.Count > 0 ? 0 : -1;
        }
        // Inside a section the keys follow its rows and seat numbers, so a curved row is walked seat by seat.
        var current = seats[from];
        for (var i = 0; i < seats.Count; i++)
        {
            var s = seats[i];
            if (s.Section != current.Section)
            {
                continue;
            }
            if (dy == 0 && s.Row == current.Row && s.Number == current.Number + dx
                || dx == 0 && s.Number == current.Number && s.Row == current.Row + dy)
            {
                return i;
            }
        }
        var origin = current.Position;
        var best = -1;
        var bestScore = double.MaxValue;
        for (var i = 0; i < seats.Count; i++)
        {
            if (i == from)
            {
                continue;
            }
            var d = seats[i].Position - origin;
            var along = d.X * dx + d.Y * dy;
            if (along <= 0.3)
            {
                continue;
            }
            var across = Math.Abs(d.X * dy - d.Y * dx);
            var score = along + across * 3;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }
}

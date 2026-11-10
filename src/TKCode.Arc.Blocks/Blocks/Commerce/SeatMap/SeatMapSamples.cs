namespace TKCode.Arc;

/// <summary>
/// Invented sample venues for the <see cref="SeatMap"/> preview: a small concert hall with three tiers and a narrow-body
/// cabin with one aisle. Sold seats are picked with a fixed seed, so the maps look the same every time. None of it is required.
/// </summary>
public static class SeatMapSamples
{
    /// <summary>Fills <paramref name="map"/> with the concert hall: orchestra, two side boxes and a balcony.</summary>
    public static void ApplyHall(SeatMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Title = "Harbor Hall · Friday, Nov 20, 8 pm";
        map.ServiceFee = 3.50m;
        map.Labels = new SeatMapLabels();
        map.Tiers.Clear();
        map.Tiers.AddRange(
        [
            new SeatMapTier { Id = "premium", Name = "Premium", Price = 120, ColorIndex = 1 },
            new SeatMapTier { Id = "standard", Name = "Standard", Price = 85, ColorIndex = 2 },
            new SeatMapTier { Id = "balcony", Name = "Balcony", Price = 45, ColorIndex = 3 },
        ]);
        map.Sections.Clear();
        map.Sections.AddRange(
        [
            new SeatMapSection { Id = "ORC", Name = "Orchestra", TierId = "premium", Rows = 5, SeatsPerRow = 16, X = 4, Y = 1, Curve = 1.2, AisleAfter = [8] },
            new SeatMapSection { Id = "BXL", Name = "Box left", TierId = "standard", Rows = 5, SeatsPerRow = 3, X = 0, Y = 1.5 },
            new SeatMapSection { Id = "BXR", Name = "Box right", TierId = "standard", Rows = 5, SeatsPerRow = 3, X = 22.5, Y = 1.5 },
            new SeatMapSection { Id = "BAL", Name = "Balcony", TierId = "balcony", Rows = 4, SeatsPerRow = 20, X = 3, Y = 9.4, Curve = 0.9, AisleAfter = [5, 15] },
        ]);
        map.TakenSeats = Taken(map, 0.32, 11);
    }

    /// <summary>
    /// Fills <paramref name="map"/> with a cabin seen from above, nose on the left: business (two seats each side) and economy
    /// (three each side). Each section is one side of the aisle; its rows are seat letters and its seat numbers the cabin rows.
    /// </summary>
    public static void ApplyCabin(SeatMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Title = "Flight LK 214 · Port Wren to Kestrel";
        map.ServiceFee = 0;
        map.Labels = new SeatMapLabels { Stage = "Front", Seat = (section, row, number) => $"{section} · Seat {number}{row}" };
        map.Tiers.Clear();
        map.Tiers.AddRange(
        [
            new SeatMapTier { Id = "business", Name = "Business", Price = 260, ColorIndex = 4 },
            new SeatMapTier { Id = "extra", Name = "Extra legroom", Price = 45, ColorIndex = 2 },
            new SeatMapTier { Id = "economy", Name = "Economy", Price = 0, ColorIndex = 1 },
        ]);
        map.Sections.Clear();
        map.Sections.AddRange(
        [
            new SeatMapSection { Id = "B1", Name = "Business", TierId = "business", Rows = 2, SeatsPerRow = 3, FirstRow = 'A', X = 0, Y = 0, AisleAfter = [1, 2] },
            new SeatMapSection { Id = "B2", Name = "Business", TierId = "business", Rows = 2, SeatsPerRow = 3, FirstRow = 'E', X = 0, Y = 4.6, AisleAfter = [1, 2] },
            new SeatMapSection { Id = "X1", Name = "Extra legroom", TierId = "extra", Rows = 3, SeatsPerRow = 2, FirstRow = 'A', X = 6.5, Y = 0, AisleAfter = [1] },
            new SeatMapSection { Id = "X2", Name = "Extra legroom", TierId = "extra", Rows = 3, SeatsPerRow = 2, FirstRow = 'D', X = 6.5, Y = 4.6, AisleAfter = [1] },
            new SeatMapSection { Id = "E1", Name = "Economy", TierId = "economy", Rows = 3, SeatsPerRow = 12, FirstRow = 'A', X = 10.5, Y = 0 },
            new SeatMapSection { Id = "E2", Name = "Economy", TierId = "economy", Rows = 3, SeatsPerRow = 12, FirstRow = 'D', X = 10.5, Y = 4.6 },
        ]);
        map.TakenSeats = Taken(map, 0.45, 5);
    }

    private static IReadOnlyList<string> Taken(SeatMap map, double share, int seed)
    {
        var random = new Random(seed);
        return SeatMapLayout.Build(map.Sections, map.Tiers).Where(_ => random.NextDouble() < share).Select(s => s.Id).ToList();
    }
}

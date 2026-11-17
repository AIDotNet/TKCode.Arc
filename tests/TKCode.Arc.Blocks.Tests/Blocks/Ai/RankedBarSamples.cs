using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented standings for the ranked bar list and the arena standings (model arena spec 10.5): made-up models with
/// geometric marks; nothing here names a real vendor or model.
/// </summary>
internal static class RankedBarSamples
{
    public static readonly Geometry SunMark = Geometry.Parse(
        "M12 6.5A5.5 5.5 0 1 1 11.99 6.5Z M11 1H13V4.5H11Z M11 19.5H13V23H11Z M1 11H4.5V13H1Z M19.5 11H23V13H19.5Z");

    public static readonly Geometry HexagonMark = Geometry.Parse(
        "M12 2L20.66 7L20.66 17L12 22L3.34 17L3.34 7Z M12 6L6.8 9L6.8 15L12 18L17.2 15L17.2 9Z");

    public static readonly Geometry DiamondMark = Geometry.Parse("M12 2L22 12L12 22L2 12Z");

    public static readonly Geometry TriangleMark = Geometry.Parse("M12 3.5L21 19.5L3 19.5Z");

    public static readonly Geometry LeafMark = Geometry.Parse("M20 4C12 4 5 9 4 20C9 19 14 14 20 4Z");

    public static readonly Geometry WaveMark = Geometry.Parse(
        "M2 9Q7 5 12 9T22 9L22 12Q17 8 12 12T2 12Z M2 15Q7 11 12 15T22 15L22 18Q17 14 12 18T2 18Z");

    public static readonly IBrush Amber = new ImmutableSolidColorBrush(Color.Parse("#D9822B"));
    public static readonly IBrush Teal = new ImmutableSolidColorBrush(Color.Parse("#12A594"));
    public static readonly IBrush Violet = new ImmutableSolidColorBrush(Color.Parse("#8E4EC6"));
    public static readonly IBrush Blue = new ImmutableSolidColorBrush(Color.Parse("#3E63DD"));

    private sealed record Model(string Id, string Name, object Mark, IBrush? Color, int Tone);

    private static readonly Model[] s_models =
    [
        new("lumen-pro", "Lumen 3 Pro", SunMark, Amber, 3),
        new("kite-5", "Kite 5", HexagonMark, Teal, 4),
        new("basalt-ultra", "Basalt 2 Ultra", DiamondMark, null, 1),
        new("vireo-4", "Vireo 4", TriangleMark, Violet, 2),
        new("tern-flash", "Tern 1.2 Flash", LeafMark, Blue, 1),
        new("marlin-xl", "Marlin XL", WaveMark, Violet, 2)
    ];

    /// <summary>Standings from (id, wins, ties, losses), ranked by win rate (ties keep the given order).</summary>
    public static RankedBarItem[] Rank(params (string Id, int Wins, int Ties, int Losses)[] records)
    {
        var rated = records
            .Select(r => (Model: s_models.First(m => m.Id == r.Id), Rate: (r.Wins + 0.5 * r.Ties) / (r.Wins + r.Ties + r.Losses)))
            .OrderByDescending(r => r.Rate)
            .ToArray();
        return rated
            .Select((r, i) => new RankedBarItem(r.Model.Id, r.Model.Name, r.Model.Mark, r.Model.Color, r.Model.Tone, r.Rate,
                $"Rank {i + 1}, {r.Model.Name}, win rate {(int)Math.Round(r.Rate * 100, MidpointRounding.AwayFromZero)}%"))
            .ToArray();
    }

    /// <summary>The demo's preset standings (spec 10.5): Lumen 70%, Kite 50%, Basalt 50%, Vireo 33%, Tern 17%.</summary>
    public static RankedBarItem[] Standings() => Rank(
        ("lumen-pro", 3, 1, 1),
        ("kite-5", 2, 0, 2),
        ("basalt-ultra", 1, 1, 1),
        ("vireo-4", 1, 0, 2),
        ("tern-flash", 0, 1, 2));

    /// <summary>Earlier standings: Basalt 75%, Kite 67%, Lumen 63%, Vireo 33%, Tern 25% (Lumen climbs to first).</summary>
    public static RankedBarItem[] Earlier() => Rank(
        ("basalt-ultra", 1, 1, 0),
        ("kite-5", 2, 0, 1),
        ("lumen-pro", 2, 1, 1),
        ("vireo-4", 1, 0, 2),
        ("tern-flash", 0, 1, 1));

    /// <summary>Six models (one more than the default limit): Marlin last at 0%.</summary>
    public static RankedBarItem[] Six() => Rank(
        ("lumen-pro", 3, 1, 1),
        ("kite-5", 2, 0, 2),
        ("basalt-ultra", 1, 1, 1),
        ("vireo-4", 1, 0, 2),
        ("tern-flash", 0, 1, 2),
        ("marlin-xl", 0, 0, 3));

    /// <summary>A popover's content with the standings and sample texts.</summary>
    public static ArenaStandings Popover(RankedBarItem[]? items = null, Action<ArenaStandings>? configure = null)
    {
        items ??= Standings();
        var standings = new ArenaStandings
        {
            Items = items,
            Title = "Standings",
            VotesText = items.Length == 0 ? "No votes yet" : "15 votes",
            EmptyText = "No votes yet. Judge a round and the models land here, ranked by win rate.",
            FootnoteText = "Ties count as half a win",
            ResetText = "Reset",
            CanReset = items.Length > 0,
            RankFormat = "Rank {0},",
            WinRateSuffix = ", win rate"
        };
        configure?.Invoke(standings);
        return standings;
    }
}

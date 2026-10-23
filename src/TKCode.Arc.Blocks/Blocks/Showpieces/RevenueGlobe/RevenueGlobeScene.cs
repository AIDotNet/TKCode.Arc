namespace TKCode.Arc;

/// <summary>
/// What the globe canvas draws besides land: the cities as unit vectors with their region and current revenue, the
/// HQ, and every city's arc to HQ precomputed once (57 lifted and 57 ground points), so a frame only projects.
/// Built by <see cref="RevenueGlobe"/> whenever cities, regions or HQ change; <see cref="CityRevenue"/> is refreshed
/// as the playhead moves.
/// </summary>
internal sealed class RevenueGlobeScene
{
    public static readonly RevenueGlobeScene Empty = new([], null, []);

    public RevenueGlobeScene(RevenueCity[] cities, RevenueCity? headquarters, string?[] regionKeys)
    {
        Cities = cities;
        Headquarters = headquarters;
        var n = cities.Length;
        CityPoints = new ShowpieceVec3[n];
        CityRegion = new int[n];
        CityRevenue = new double[n];
        CityTangents = new (ShowpieceVec3, ShowpieceVec3)[n];
        ArcLifted = new float[n][];
        ArcGround = new float[n][];
        ArcTheta = new double[n];
        HqIndex = -1;
        for (var i = 0; i < n; i++)
        {
            var city = cities[i];
            CityPoints[i] = ShowpieceProjection.LatLonToUnit(city.Latitude, city.Longitude);
            CityTangents[i] = RevenueGlobeMath.Tangents(CityPoints[i]);
            CityRegion[i] = Array.IndexOf(regionKeys, city.RegionKey);
            if (headquarters is not null && (ReferenceEquals(city, headquarters) ||
                                             (!string.IsNullOrEmpty(city.Key) && city.Key == headquarters.Key)))
            {
                HqIndex = i;
            }
        }
        if (headquarters is not null)
        {
            HqPoint = HqIndex >= 0 ? CityPoints[HqIndex] : ShowpieceProjection.LatLonToUnit(headquarters.Latitude, headquarters.Longitude);
            HqTangents = RevenueGlobeMath.Tangents(HqPoint);
            for (var i = 0; i < n; i++)
            {
                if (i == HqIndex)
                {
                    ArcLifted[i] = [];
                    ArcGround[i] = [];
                    continue;
                }
                var lifted = new float[RevenueGlobeMath.ArcPoints * 3];
                var ground = new float[RevenueGlobeMath.ArcPoints * 3];
                RevenueGlobeMath.BuildArc(CityPoints[i], HqPoint, lifted, ground, out var theta);
                ArcLifted[i] = lifted;
                ArcGround[i] = ground;
                ArcTheta[i] = theta;
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                ArcLifted[i] = [];
                ArcGround[i] = [];
            }
        }
    }

    public RevenueCity[] Cities { get; }

    public RevenueCity? Headquarters { get; }

    public ShowpieceVec3[] CityPoints { get; }

    public (ShowpieceVec3 U, ShowpieceVec3 V)[] CityTangents { get; }

    /// <summary>Index of each city's region in the block's regions (−1 when it has none).</summary>
    public int[] CityRegion { get; }

    /// <summary>Revenue of each city at the playhead (dot sizes, region labels).</summary>
    public double[] CityRevenue { get; }

    public double MaxCityRevenue { get; set; }

    /// <summary>Index of HQ in <see cref="Cities"/> (−1 when HQ is not one of them).</summary>
    public int HqIndex { get; }

    public ShowpieceVec3 HqPoint { get; }

    public (ShowpieceVec3 U, ShowpieceVec3 V) HqTangents { get; }

    public bool HasHq => Headquarters is not null;

    /// <summary>Each city's arc to HQ: 57 lifted points (x, y, z triples; empty for HQ itself).</summary>
    public float[][] ArcLifted { get; }

    /// <summary>Each city's arc on the ground (the great circle).</summary>
    public float[][] ArcGround { get; }

    /// <summary>Angle between each city and HQ.</summary>
    public double[] ArcTheta { get; }

    public bool HasArc(int city) => (uint)city < (uint)ArcLifted.Length && ArcLifted[city].Length > 0;
}

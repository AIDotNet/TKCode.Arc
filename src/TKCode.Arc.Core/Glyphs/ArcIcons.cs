using System.Collections.Concurrent;
using Avalonia.Media;

namespace TKCode.Arc;

public static partial class ArcIcons
{
    private static readonly ConcurrentDictionary<string, Geometry> s_cache = new(StringComparer.Ordinal);

    private static Geometry Get(string name, string data) => s_cache.GetOrAdd(name, static (_, d) => StreamGeometry.Parse(d), data);

    /// <summary>Parses arbitrary 24x24 SVG path data (cached by content).</summary>
    public static Geometry Parse(string data) => s_cache.GetOrAdd(data, static d => StreamGeometry.Parse(d));
}

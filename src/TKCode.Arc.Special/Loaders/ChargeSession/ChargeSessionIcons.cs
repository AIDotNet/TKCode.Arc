using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs <see cref="ChargeSession"/> needs that Core does not carry.</summary>
internal static class ChargeSessionIcons
{
    // Lucide "battery-full" (ISC license), as converted by tools/lucide/generate_lucide.py.
    private const string BatteryFullData =
        "M10 10v4 M14 10v4 M22 14v-4 M6 10v4 M4 6h12a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z";

    /// <summary>A battery with three bars.</summary>
    public static Geometry BatteryFull => ArcIcons.Parse(BatteryFullData);
}

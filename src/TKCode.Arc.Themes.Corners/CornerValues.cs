using System.Globalization;

namespace TKCode.Arc.Themes.Corners;

/// <summary>
/// The value a corner style writes for one radius token, used by the corner presets (<c>ArcCornerPresets</c>). Free of
/// Foundation types so tests can compile it on its own.
/// </summary>
internal static class CornerValues
{
    /// <summary>Radii from this value up are pills or circles: they stay as they are unless the style is square.</summary>
    internal const double PillThreshold = 999;

    /// <summary>The override text of <paramref name="entry"/> under <paramref name="style"/> (not called for the default style).</summary>
    internal static string Value(CornerEntry entry, ArcCornerStyle style)
    {
        if (style.IsSquare)
        {
            return "0";
        }

        // "5", or "19,19,0,0" for a radius with its own corners. Pill radii are not scaled.
        var parts = entry.Default.Split(',');
        for (var i = 0; i < parts.Length; i++)
        {
            var value = double.Parse(parts[i], CultureInfo.InvariantCulture);
            if (value < PillThreshold)
            {
                value = Math.Round(value * style.Factor, 3);
            }
            parts[i] = value.ToString("0.###", CultureInfo.InvariantCulture);
        }
        return string.Join(',', parts);
    }
}

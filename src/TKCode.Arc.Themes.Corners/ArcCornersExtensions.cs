using System.Globalization;
using AtomUI.Theme.Configuration;

namespace TKCode.Arc.Themes.Corners;

/// <summary>
/// Global corner style (design/architecture/theming/corner-style.md). It writes the final value of every radius token of
/// ArcDesign (the four tiers and the radius steps) and of the control packages in the scope, and nothing else.
/// </summary>
public static class ArcCornersExtensions
{
    /// <summary>
    /// Applies a corner style to a <see cref="ThemeConfigBuilder"/>. It uses <c>WithArc</c>, so a second <c>WithArc</c> or
    /// <c>WithArcCorners</c> call replaces what this one wrote; to combine it with an accent or other overrides use
    /// <see cref="Corners"/> inside a single <c>WithArc</c> call.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="style">The corner style; <see cref="ArcCornerStyle.Default"/> writes nothing.</param>
    /// <param name="scope">The control packages the app registers.</param>
    public static ThemeConfigBuilder WithArcCorners(this ThemeConfigBuilder builder, ArcCornerStyle style,
        ArcCornerScope scope = ArcCornerScope.Controls)
        => builder.WithArc(arc => arc.Corners(style, scope));

    /// <summary>Applies a corner style to the overrides being collected by <c>WithArc</c>.</summary>
    public static ArcThemeConfigBuilder Corners(this ArcThemeConfigBuilder arc, ArcCornerStyle style,
        ArcCornerScope scope = ArcCornerScope.Controls)
    {
        if (style.IsDefault)
        {
            return arc;
        }

        foreach (var entry in CornerTable.ArcDesign)
        {
            arc.Token(entry.Token, Value(entry, style));
        }
        Add(arc, scope, ArcCornerScope.Controls, CornerTable.Controls, "TKCode.Arc", style);
        // AtomUI's scroll bar thumb sits in every Arc popup and scrolling surface, so it follows the main package.
        Add(arc, scope, ArcCornerScope.Controls, AtomUiCornerTable.ScrollViewer, AtomUiCornerTable.Catalog, style);
        Add(arc, scope, ArcCornerScope.Blocks, CornerTable.Blocks, "TKCode.Arc.Blocks", style);
        Add(arc, scope, ArcCornerScope.Special, CornerTable.Special, "TKCode.Arc.Special", style);
        Add(arc, scope, ArcCornerScope.AdvancedCharts, CornerTable.AdvancedCharts, "TKCode.Arc.AdvancedCharts", style);
        Add(arc, scope, ArcCornerScope.Workflow, CornerTable.Workflow, "TKCode.Arc.Workflow", style);
        return arc;
    }

    private static void Add(ArcThemeConfigBuilder arc, ArcCornerScope scope, ArcCornerScope flag, CornerEntry[] entries,
        string catalog, ArcCornerStyle style)
    {
        if (!scope.HasFlag(flag))
        {
            return;
        }
        foreach (var entry in entries)
        {
            arc.ControlToken(catalog, entry.Control, entry.Token, Value(entry, style));
        }
    }

    /// <summary>Radii from this value up are pills or circles: they stay as they are unless the style is square.</summary>
    private const double PillThreshold = 999;

    private static string Value(CornerEntry entry, ArcCornerStyle style)
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

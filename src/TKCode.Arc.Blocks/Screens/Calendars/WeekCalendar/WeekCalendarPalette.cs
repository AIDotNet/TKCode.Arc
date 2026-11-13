using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The colors of one calendar in one theme (notes §3.8): an event's bar, fills and text, now, once it is over, and selected.</summary>
internal readonly record struct WeekCalendarColors(
    Color Bar,
    Color Fill,
    Color HoverFill,
    Color Text,
    Color SubText,
    Color PastFill,
    Color PastBar,
    Color PastText,
    Color PastSubText,
    Color SelectedFill,
    Color SelectedText);

/// <summary>
/// The week calendar's calendar colors (measured from Arc in both themes, notes §3.8). Seven preset base colors per theme;
/// every other color is a linear OKLab mix of the base with the theme's surface, foreground or secondary text (CSS
/// <c>color-mix(in oklab, …)</c>). A custom color is the base in both themes; only the mix amounts follow the theme.
/// Accent independent. Pure; no UI.
/// </summary>
internal static class WeekCalendarPalette
{
    /// <summary>The base color of a preset in the light or dark theme (gray for <see cref="WeekCalendarColor.Custom"/>).</summary>
    public static Color Base(WeekCalendarColor color, bool dark) => color switch
    {
        WeekCalendarColor.Blue => dark ? Oklch.ToColor(0.72, 0.13, 256) : Oklch.ToColor(0.57, 0.16, 256),
        WeekCalendarColor.Green => dark ? Oklch.ToColor(0.76, 0.13, 158) : Oklch.ToColor(0.58, 0.13, 158),
        WeekCalendarColor.Violet => dark ? Oklch.ToColor(0.72, 0.14, 292) : Oklch.ToColor(0.55, 0.17, 292),
        WeekCalendarColor.Amber => dark ? Oklch.ToColor(0.80, 0.13, 75) : Oklch.ToColor(0.68, 0.15, 68),
        WeekCalendarColor.Rose => dark ? Oklch.ToColor(0.74, 0.14, 8) : Oklch.ToColor(0.60, 0.18, 8),
        WeekCalendarColor.Teal => dark ? Oklch.ToColor(0.76, 0.10, 200) : Oklch.ToColor(0.60, 0.10, 200),
        _ => dark ? Oklch.ToColor(0.70, 0, 0) : Oklch.ToColor(0.58, 0, 0),
    };

    /// <summary>The base color of a calendar (its custom color when it has one).</summary>
    public static Color BaseOf(WeekCalendarSource? source, bool dark)
    {
        if (source is null)
        {
            return Base(WeekCalendarColor.Neutral, dark);
        }
        if (source.Color == WeekCalendarColor.Custom)
        {
            return source.CustomColor is { } custom ? Color.FromRgb(custom.R, custom.G, custom.B) : Base(WeekCalendarColor.Neutral, dark);
        }
        return Base(source.Color, dark);
    }

    /// <summary>Mixes every color of one calendar from its base and the theme's surface, foreground and secondary text.</summary>
    public static WeekCalendarColors Derive(Color calendar, bool dark, Color surface, Color foreground, Color textSecondary)
    {
        var text = Oklch.MixOklab(foreground, calendar, dark ? 0.38 : 0.45);
        var pastText = Oklch.MixOklab(textSecondary, calendar, 0.35);
        return new WeekCalendarColors(
            Bar: calendar,
            Fill: Oklch.MixOklab(surface, calendar, dark ? 0.20 : 0.12),
            HoverFill: Oklch.MixOklab(surface, calendar, dark ? 0.27 : 0.18),
            Text: text,
            SubText: Oklch.WithAlpha(text, 0.78),
            PastFill: Oklch.MixOklab(surface, calendar, dark ? 0.14 : 0.084),
            PastBar: Oklch.MixOklab(surface, calendar, 0.65),
            PastText: pastText,
            PastSubText: Oklch.WithAlpha(pastText, 0.78),
            // Selected and dragged blocks (notes B §5): a deeper fill (light L .881 for blue) and darker text (.297).
            SelectedFill: Oklch.MixOklab(surface, calendar, dark ? 0.36 : 0.28),
            SelectedText: Oklch.MixOklab(foreground, calendar, dark ? 0.30 : 0.36));
    }

    /// <summary>The colors of a calendar with the neutral theme colors of <see cref="ArcPalette.Neutrals"/>.</summary>
    public static WeekCalendarColors Derive(Color calendar, bool dark)
    {
        var n = ArcPalette.Neutrals(dark);
        return Derive(calendar, dark, n.Surface, n.Foreground, n.TextSecondary);
    }
}

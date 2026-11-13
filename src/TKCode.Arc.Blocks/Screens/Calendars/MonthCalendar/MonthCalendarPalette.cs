using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>The brushes of one calendar in one theme.</summary>
internal sealed class MonthToneBrushes
{
    public MonthToneBrushes(Color tone, ScreenPaint paint)
    {
        var dark = paint.IsDark;
        Tone = new ImmutableSolidColorBrush(tone);
        // Arc Month calendar (design reference, notes §3): bars mix the card with the tone (18% dark, 13% light; hover 25% /
        // 19%), bar text mixes the foreground with the tone half and half; pending invitations are outlined in the tone at 55%
        // and drawn at 35% in the busy strip.
        BarFill = new ImmutableSolidColorBrush(Oklch.MixOklab(paint.Surface, tone, dark ? 0.18 : 0.13));
        BarHover = new ImmutableSolidColorBrush(Oklch.MixOklab(paint.Surface, tone, dark ? 0.25 : 0.19));
        BarText = new ImmutableSolidColorBrush(Oklch.MixOklab(paint.Foreground, tone, 0.5));
        PendingOutline = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(tone, 0.55)), 1,
            new ImmutableDashStyle(new double[] { 3, 2 }, 0));
        Pending = new ImmutableSolidColorBrush(Oklch.WithAlpha(tone, 0.35));
        TonePen = new ImmutablePen(Tone, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    }

    public IImmutableSolidColorBrush Tone { get; }
    public IImmutableSolidColorBrush BarFill { get; }
    public IImmutableSolidColorBrush BarHover { get; }
    public IImmutableSolidColorBrush BarText { get; }
    public IPen PendingOutline { get; }
    public IImmutableSolidColorBrush Pending { get; }
    public IPen TonePen { get; }
}

/// <summary>
/// The calendar colors of <see cref="MonthCalendar"/> (Arc Month calendar, design reference: the sample's tone tokens
/// measured in both themes). Green and gray were not used by Arc's calendars; they follow the same lightness and chroma
/// as the measured tones.
/// </summary>
internal static class MonthCalendarPalette
{
    public static Color Base(MonthCalendarTone tone, bool dark) => tone switch
    {
        MonthCalendarTone.Teal => dark ? Oklch.ToColor(0.79, 0.13, 182) : Oklch.ToColor(0.50, 0.095, 182),
        MonthCalendarTone.Violet => dark ? Oklch.ToColor(0.74, 0.125, 290) : Oklch.ToColor(0.51, 0.16, 290),
        MonthCalendarTone.Amber => dark ? Oklch.ToColor(0.83, 0.12, 78) : Oklch.ToColor(0.55, 0.12, 68),
        MonthCalendarTone.Blue => dark ? Oklch.ToColor(0.79, 0.095, 236) : Oklch.ToColor(0.51, 0.11, 240),
        MonthCalendarTone.Rose => dark ? Oklch.ToColor(0.71, 0.155, 8) : Oklch.ToColor(0.53, 0.18, 10),
        MonthCalendarTone.Green => dark ? Oklch.ToColor(0.79, 0.14, 162) : Oklch.ToColor(0.52, 0.12, 158),
        _ => dark ? Oklch.ToColor(0.72, 0, 0) : Oklch.ToColor(0.52, 0, 0),
    };

    public static Color BaseOf(MonthCalendarSource? source, bool dark)
    {
        if (source is null)
        {
            return Base(MonthCalendarTone.Neutral, dark);
        }
        if (source.Tone == MonthCalendarTone.Custom)
        {
            return source.CustomColor ?? Base(MonthCalendarTone.Neutral, dark);
        }
        return Base(source.Tone, dark);
    }

    /// <summary>The green of a guest who is going.</summary>
    public static Color Going(bool dark) => Base(MonthCalendarTone.Green, dark);
}

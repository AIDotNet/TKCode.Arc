using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Theme.TokenBaseline;

/// <summary>
/// Turns a resolved token value into stable, culture-invariant text for the token baseline. It knows only Avalonia types,
/// so it stays as is when the theme engine is replaced.
/// </summary>
internal static class TokenValueFormatter
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    public static string Format(object? value) => value switch
    {
        null => "null",
        ISolidColorBrush brush => brush.Opacity == 1 ? Format(brush.Color) : $"{Format(brush.Color)} opacity {Number(brush.Opacity)}",
        Color color => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}",
        IGradientBrush gradient => Gradient(gradient),
        double number => Number(number),
        float number => number.ToString("R", s_invariant),
        int number => number.ToString(s_invariant),
        bool flag => flag ? "true" : "false",
        string text => text,
        Thickness t => $"{Number(t.Left)},{Number(t.Top)},{Number(t.Right)},{Number(t.Bottom)}",
        CornerRadius r => $"{Number(r.TopLeft)},{Number(r.TopRight)},{Number(r.BottomRight)},{Number(r.BottomLeft)}",
        BoxShadows shadows => Shadows(shadows),
        BoxShadow shadow => Shadow(shadow),
        FontFamily family => family.ToString(),
        // FontWeight has aliases with the same number (Normal = Regular), so the number is the stable form.
        FontWeight weight => ((int)weight).ToString(s_invariant),
        TimeSpan time => $"{Number(time.TotalMilliseconds)}ms",
        SplineEasing spline => $"cubic-bezier({Number(spline.X1)},{Number(spline.Y1)},{Number(spline.X2)},{Number(spline.Y2)})",
        Easing easing => $"{easing.GetType().Name}[{string.Join(",", Enumerable.Range(0, 11).Select(i => Number(easing.Ease(i / 10.0))))}]",
        Enum kind => kind.ToString(),
        IFormattable formattable => $"{value.GetType().Name}:{formattable.ToString(null, s_invariant)}",
        _ => $"{value.GetType().Name}:{value}"
    };

    private static string Number(double value) => value.ToString("R", s_invariant);

    private static string Shadows(BoxShadows shadows)
    {
        if (shadows.Count == 0)
        {
            return "none";
        }
        var parts = new List<string>(shadows.Count);
        for (var i = 0; i < shadows.Count; i++)
        {
            parts.Add(Shadow(shadows[i]));
        }
        return string.Join(" | ", parts);
    }

    private static string Shadow(BoxShadow shadow)
        => $"{(shadow.IsInset ? "inset " : "")}{Number(shadow.OffsetX)} {Number(shadow.OffsetY)} {Number(shadow.Blur)} {Number(shadow.Spread)} {Format(shadow.Color)}";

    private static string Gradient(IGradientBrush brush)
    {
        var stops = string.Join(" ", brush.GradientStops.Select(s => $"{Format(s.Color)}@{Number(s.Offset)}"));
        return $"{brush.GetType().Name}[{stops}] opacity {Number(brush.Opacity)}";
    }
}

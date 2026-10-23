using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The color of a prompt variable: each variable of the list takes one of the four series colors in turn, and the editor's
/// chips, the variable fields and their dots all use the same one.
/// </summary>
internal static class PromptTone
{
    /// <summary>The tone (1 to 4) of a variable: its index in <paramref name="variables"/> modulo four, counted from 1; a name that is not listed takes the next tone after the last.</summary>
    public static int ToneOf(string name, IReadOnlyList<string> variables)
    {
        for (var i = 0; i < variables.Count; i++)
        {
            if (string.Equals(variables[i], name, StringComparison.Ordinal))
            {
                return i % 4 + 1;
            }
        }

        return variables.Count % 4 + 1;
    }

    /// <summary>The series color of a tone (1 to 4) in the theme the owner is in.</summary>
    public static Color SeriesColor(Control owner, int tone) => ArcTokens.GetColor(owner, tone switch
    {
        1 => ArcDesignTokenKind.Series1,
        2 => ArcDesignTokenKind.Series2,
        3 => ArcDesignTokenKind.Series3,
        _ => ArcDesignTokenKind.Series4
    });

    /// <summary>The color <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>, in sRGB.</summary>
    public static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Lerp(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(Lerp(from.A, to.A), Lerp(from.R, to.R), Lerp(from.G, to.G), Lerp(from.B, to.B));
    }

    /// <summary>The color with its alpha multiplied by <paramref name="opacity"/>.</summary>
    public static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);
}

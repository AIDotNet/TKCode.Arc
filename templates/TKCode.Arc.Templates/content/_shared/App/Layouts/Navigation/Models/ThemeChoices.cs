using Avalonia.Media;
using TKCode.Arc;

namespace ArcStarterApp.Models;

/// <summary>The names the Settings page shows for the theme's appearance and accent.</summary>
public static class ThemeChoices
{
    /// <summary>The accent preset whose seed is <paramref name="seed"/>, or null for a custom accent.</summary>
    public static string? AccentName(Color? seed)
    {
        foreach (var preset in Enum.GetValues<ArcAccentPreset>())
        {
            if (seed == ArcPalette.GetSeed(preset))
            {
                return preset.ToString();
            }
        }
        return null;
    }

    public static bool TryParseAccent(object? value, out ArcAccentPreset preset)
    {
        preset = default;
        return value is string name && Enum.TryParse(name, out preset);
    }

    public static bool TryParseAppearance(object? value, out ArcAppearance appearance)
    {
        appearance = default;
        return value is string name && Enum.TryParse(name, out appearance);
    }
}

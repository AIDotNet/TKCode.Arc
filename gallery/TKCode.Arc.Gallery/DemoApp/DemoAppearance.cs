using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The demo's light, dark or system choice (Settings → Preferences and the account menu). It sets the gallery's own
/// appearance, as uiarc.dev's demo shares its appearance with the Arc library; "system" follows the device's setting
/// while it is chosen.
/// </summary>
public static class DemoAppearance
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string System = "system";

    private static GalleryTheme? s_following;
    private static bool s_listening;

    /// <summary>The last choice made in the demo; null until one is made (the gallery's own light or dark stands).</summary>
    public static string? Mode { get; private set; }

    /// <summary>The choice to show: system while the demo follows the device, otherwise the gallery's light or dark.</summary>
    public static string Current(GalleryTheme theme) => Mode == System ? System : theme.IsDark ? Dark : Light;

    public static Task SetAsync(GalleryTheme theme, string mode)
    {
        Mode = mode;
        if (mode != System)
        {
            s_following = null;
            return theme.SetDarkAsync(mode == Dark);
        }
        s_following = theme;
        Listen();
        return theme.SetDarkAsync(IsDeviceDark());
    }

    private static bool IsDeviceDark()
        => Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;

    private static void Listen()
    {
        if (s_listening || Application.Current?.PlatformSettings is not { } settings)
        {
            return;
        }
        s_listening = true;
        settings.ColorValuesChanged += async (_, values) =>
        {
            if (s_following is { } theme)
            {
                await theme.SetDarkAsync(values.ThemeVariant == PlatformThemeVariant.Dark);
            }
        };
    }
}

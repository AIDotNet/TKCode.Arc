using AtomUI;
using TKCode.Arc.Motion;
using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Holds the gallery's appearance, accent and motion level, and applies them through AtomUI.</summary>
public sealed class GalleryTheme
{
    public bool IsDark { get; private set; }
    public ArcAccentPreset Accent { get; private set; } = ArcAccentPreset.Neutral;
    public bool IsPixel { get; private set; }

    /// <summary>How round the corners are: 0 as shipped, 1 half, 2 square. The Pixel style is always square.</summary>
    public int Corners { get; private set; }
    public ArcMotionLevel MotionLevel { get; private set; } = ArcMotionLevel.Full;

    public event Action? Changed;

    public Task ToggleDarkAsync() => SetDarkAsync(!IsDark);

    public Task SetDarkAsync(bool dark)
    {
        IsDark = dark;
        return ApplyAsync();
    }

    public Task SetAccentAsync(ArcAccentPreset accent)
    {
        Accent = accent;
        return ApplyAsync();
    }

    public Task SetPixelAsync(bool pixel)
    {
        IsPixel = pixel;
        return ApplyAsync();
    }

    public Task SetCornersAsync(int corners)
    {
        Corners = corners;
        return ApplyAsync();
    }

    private ArcCornerStyle CornerStyle => Corners switch
    {
        1 => ArcCornerStyle.Scale(0.5),
        2 => ArcCornerStyle.Square,
        _ => ArcCornerStyle.Default
    };

    public void SetMotion(Control owner, ArcMotionLevel level)
    {
        MotionLevel = level;
        ArcMotion.SetLevel(owner, level);
        Changed?.Invoke();
    }

    private async Task ApplyAsync()
    {
        var manager = Application.Current?.GetThemeManager();
        if (manager is null)
        {
            return;
        }
        if (IsPixel)
        {
            // The pixel style is a compiled theme pair; its config already carries light/dark and the palette.
            await manager.ApplyThemeAsync(ArcPixelTheme.CreateRequest(IsDark, scope: ArcPixelScope.All));
            ArcPixelRendering.ApplyToApplication(true);
            Changed?.Invoke();
            return;
        }
        ArcPixelRendering.ApplyToApplication(false);
        var builder = new ThemeConfigBuilder();
        builder = IsDark
            ? builder.WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
            : builder.WithAlgorithms(ThemeAlgorithm.Default);
        var accent = Accent;
        var corners = CornerStyle;
        builder = builder.WithArc(arc => arc.Accent(accent).Corners(corners, ArcCornerScope.All));
        await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(), ThemeTransitionReason.UserRequest));
        Changed?.Invoke();
    }
}

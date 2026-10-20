using AtomUI;
using TKCode.Arc.Motion;
using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Holds the gallery's appearance, accent and motion level, and applies them through AtomUI.</summary>
public sealed class GalleryTheme
{
    public bool IsDark { get; private set; }
    public ArcAccentPreset Accent { get; private set; } = ArcAccentPreset.Neutral;
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

    public void SetMotion(Window window, ArcMotionLevel level)
    {
        MotionLevel = level;
        ArcMotion.SetLevel(window, level);
        Changed?.Invoke();
    }

    private async Task ApplyAsync()
    {
        var manager = Application.Current?.GetThemeManager();
        if (manager is null)
        {
            return;
        }
        var builder = new ThemeConfigBuilder();
        builder = IsDark
            ? builder.WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
            : builder.WithAlgorithms(ThemeAlgorithm.Default);
        var accent = Accent;
        builder = builder.WithArc(arc => arc.Accent(accent));
        await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(), ThemeTransitionReason.UserRequest));
        Changed?.Invoke();
    }
}

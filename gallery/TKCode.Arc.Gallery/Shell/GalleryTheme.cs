using TKCode.Arc.Motion;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>Holds the gallery's appearance, accent and motion level, and applies them through the Arc theme manager.</summary>
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

    // ArcThemeManager.Apply, not ApplyAsync: computing every token takes a few milliseconds, and callers may wait on these
    // tasks on the UI thread, which ApplyAsync's UI-thread publish cannot complete under (see ArcThemeManager.ApplyAsync).
    private Task ApplyAsync()
    {
        var manager = ArcThemeManager.Current;
        if (manager is null)
        {
            return Task.CompletedTask;
        }
        var dark = IsDark;
        var pixel = IsPixel;
        var accent = Accent;
        var corners = CornerStyle;
        manager.Apply(t =>
        {
            t.Appearance(dark ? ArcAppearance.Dark : ArcAppearance.Light);
            if (pixel)
            {
                // The Pixel style brings its own palette and square corners.
                t.DefaultAccent().RemovePreset(ArcCornerPresets.Slot).Pixel();
            }
            else
            {
                t.Accent(accent).RemovePreset(ArcPixelPresets.Slot).Corners(corners);
            }
        });
        ArcPixelRendering.ApplyToApplication(pixel);
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}

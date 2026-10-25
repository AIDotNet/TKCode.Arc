using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Themes.Pixel;

/// <summary>
/// Render options a token cannot carry. Aliased edges and nearest-neighbour bitmaps keep one-pixel lines and square
/// corners hard; they are inherited, so setting them on a top level covers the whole tree and popups opened from it.
/// </summary>
public static class ArcPixelRendering
{
    /// <summary>Turns hard edges on or off for one visual tree. Off sets both back to unspecified, which is Avalonia's default.</summary>
    /// <param name="root">The root, usually a <see cref="TopLevel"/>.</param>
    /// <param name="on">True for the pixel look.</param>
    public static void Apply(Visual root, bool on)
    {
        if (on)
        {
            RenderOptions.SetEdgeMode(root, EdgeMode.Aliased);
            RenderOptions.SetBitmapInterpolationMode(root, BitmapInterpolationMode.None);
        }
        else
        {
            RenderOptions.SetEdgeMode(root, EdgeMode.Unspecified);
            RenderOptions.SetBitmapInterpolationMode(root, BitmapInterpolationMode.Unspecified);
        }
    }

    /// <summary>Applies <see cref="Apply(Visual, bool)"/> to every window (or the single view's top level) the app has now.</summary>
    /// <param name="on">True for the pixel look.</param>
    public static void ApplyToApplication(bool on)
    {
        switch (Application.Current?.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                foreach (var window in desktop.Windows)
                {
                    Apply(window, on);
                }
                break;
            case ISingleViewApplicationLifetime { MainView: { } view }:
                if (TopLevel.GetTopLevel(view) is { } top)
                {
                    Apply(top, on);
                }
                break;
        }
    }
}

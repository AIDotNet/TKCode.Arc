using Avalonia;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A device the <see cref="PhoneFrame"/> simulates: its screen size in portrait, the room its status bar, notch, rounded
/// corners and gesture bar take (the safe area), and the height of its soft keyboard. The numbers are approximations of
/// typical devices, not measurements of real ones.
/// </summary>
/// <param name="Name">The device's name.</param>
/// <param name="Width">Screen width in portrait, in device-independent pixels.</param>
/// <param name="Height">Screen height in portrait.</param>
/// <param name="PortraitSafeArea">The insets in portrait: the top holds the status bar and notch, the bottom the home indicator.</param>
/// <param name="LandscapeSafeArea">The insets in landscape.</param>
/// <param name="PortraitKeyboardHeight">The soft keyboard's height in portrait.</param>
/// <param name="LandscapeKeyboardHeight">The soft keyboard's height in landscape.</param>
/// <param name="CutoutWidth">Width of the camera cutout drawn at the top of the screen in portrait; 0 for none.</param>
/// <param name="CutoutHeight">Height of the camera cutout.</param>
/// <param name="CutoutTop">Distance between the top of the screen and the cutout.</param>
public sealed record PhoneDevice(
    string Name,
    double Width,
    double Height,
    Thickness PortraitSafeArea,
    Thickness LandscapeSafeArea,
    double PortraitKeyboardHeight,
    double LandscapeKeyboardHeight,
    double CutoutWidth = 0,
    double CutoutHeight = 0,
    double CutoutTop = 0)
{
    /// <summary>An iPhone 15: 390 by 844 with a Dynamic Island, a 59 px top inset and a 34 px home indicator.</summary>
    public static PhoneDevice IPhone15 { get; } = new("iPhone 15", 390, 844,
        new Thickness(0, 59, 0, 34), new Thickness(59, 0, 59, 21), 291, 209, CutoutWidth: 126, CutoutHeight: 37, CutoutTop: 11);

    /// <summary>A Pixel: 412 by 915 with a punch-hole camera and a gesture bar.</summary>
    public static PhoneDevice Pixel { get; } = new("Pixel", 412, 915,
        new Thickness(0, 32, 0, 24), new Thickness(0, 24, 0, 24), 300, 200, CutoutWidth: 16, CutoutHeight: 16, CutoutTop: 8);

    /// <summary>A small phone: 360 by 640, a 24 px status bar and no gesture bar.</summary>
    public static PhoneDevice SmallPhone { get; } = new("Small phone", 360, 640,
        new Thickness(0, 24, 0, 0), new Thickness(0, 24, 0, 0), 250, 180);

    /// <summary>A tablet: 768 by 1024 with a 24 px status bar and a 20 px home indicator.</summary>
    public static PhoneDevice Tablet { get; } = new("Tablet", 768, 1024,
        new Thickness(0, 24, 0, 20), new Thickness(0, 24, 0, 20), 380, 320);

    /// <summary>The presets the frame's switcher offers, in order.</summary>
    public static IReadOnlyList<PhoneDevice> Presets { get; } = [IPhone15, Pixel, SmallPhone, Tablet];

    /// <summary>The screen size: <see cref="Width"/> by <see cref="Height"/>, swapped in landscape.</summary>
    public Size ScreenSize(bool landscape) => landscape ? new Size(Height, Width) : new Size(Width, Height);

    /// <summary>The safe area in the given orientation.</summary>
    public Thickness SafeArea(bool landscape) => landscape ? LandscapeSafeArea : PortraitSafeArea;

    /// <summary>The soft keyboard's height in the given orientation.</summary>
    public double KeyboardHeight(bool landscape) => landscape ? LandscapeKeyboardHeight : PortraitKeyboardHeight;
}

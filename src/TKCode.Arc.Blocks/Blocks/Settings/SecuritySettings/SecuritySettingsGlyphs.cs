using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs that <see cref="ArcIcons"/> does not include (24×24 stroke paths, absolute commands).</summary>
internal static class SecuritySettingsGlyphs
{
    /// <summary>Lucide "tablet".</summary>
    public static Geometry Tablet => ArcIcons.Parse("M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-16a2 2 0 0 1 2 -2z M12 18h.01");

    /// <summary>Lucide "shield-check".</summary>
    public static Geometry ShieldCheck => ArcIcons.Parse(
        "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M9 12l2 2l4-4");

    /// <summary>Lucide "shield-alert".</summary>
    public static Geometry ShieldAlert => ArcIcons.Parse(
        "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z M12 8v4 M12 16h.01");

    /// <summary>The glyph for a kind of device.</summary>
    public static Geometry For(SecurityDeviceKind kind) => kind switch
    {
        SecurityDeviceKind.Desktop => ArcIcons.Monitor,
        SecurityDeviceKind.Laptop => ArcIcons.Laptop,
        SecurityDeviceKind.Phone => ArcIcons.Smartphone,
        SecurityDeviceKind.Tablet => Tablet,
        _ => ArcIcons.Globe
    };
}

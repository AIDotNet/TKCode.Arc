// Ported from Arc's free blocks (MIT, Copyright (c) 2026 Elia Kuratli): the --arc-gradient-* values measured on uiarc.dev.
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's brand gradient (<c>--arc-gradient-from/to</c>) and its soft page wash (<c>--arc-gradient-soft</c>, 135°). The hues
/// are fixed (violet 300, amber 65) and do not follow the accent, so block tokens may hold them.
/// </summary>
internal readonly record struct SiteBrandPalette(Color From, Color To, Color SoftFrom, Color SoftTo)
{
    /// <summary>The light or dark palette.</summary>
    public static SiteBrandPalette For(bool isDark) => isDark
        ? new(Oklch.ToColor(0.38, 0.05, 300), Oklch.ToColor(0.40, 0.045, 60), Oklch.ToColor(0.235, 0.016, 300), Oklch.ToColor(0.24, 0.014, 65))
        : new(Oklch.ToColor(0.90, 0.045, 300), Oklch.ToColor(0.92, 0.045, 65), Oklch.ToColor(0.975, 0.016, 300), Oklch.ToColor(0.98, 0.016, 65));
}

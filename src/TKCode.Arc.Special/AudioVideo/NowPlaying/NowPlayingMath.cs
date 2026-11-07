using System.Globalization;
using Avalonia;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The two layouts of the player (Arc Now playing, design reference, as measured): the 64px mini bar and the 411px full
/// player inside a shell at most 336 wide. Rectangles are in the shell's inner coordinates (inside its 1px border), so
/// the mini artwork sits at (10, 10) and the full artwork at (12, 11). Both layouts are computed at once so the shared
/// parts (artwork, play, next) can fly between them.
/// </summary>
internal readonly record struct NowPlayingLayout(
    double ShellWidth,
    bool IsCompact,
    double MiniHeight,
    double FullHeight,
    Rect MiniArt,
    Rect MiniMain,
    Rect MiniLevels,
    Rect MiniPlay,
    Rect MiniNext,
    Rect MiniProgress,
    Rect FullArt,
    Rect Collapse,
    Rect Info,
    Rect Wave,
    Rect Times,
    Rect Previous,
    Rect FullPlay,
    Rect FullNext)
{
    public const double MaxShellWidth = 336;
    public const double MiniShellHeight = 64;
    public const double MiniPadding = 10;
    public const double MiniGap = 10;
    public const double MiniArtSize = 44;
    public const double MiniButtonSize = 40;
    public const double LevelsWidth = 13;
    public const double LevelsHeight = 14;
    public const double LevelsMarginRight = 4;
    public const double FullPaddingX = 12;
    public const double FullPaddingTop = 11;
    public const double FullPaddingBottom = 14;
    public const double FullRowGap = 12;
    public const double FullArtHeight = 192;
    public const double InfoHeight = 46;
    public const double TextInset = 4;
    public const double WaveHeight = 36;
    public const double TimesGap = 6;
    public const double TimesHeight = 16.8;
    public const double CollapseSize = 34;
    public const double CollapseInset = 10;
    public const double PlaySize = 52;
    public const double SkipSize = 44;
    public const double ControlsGap = 28;
    public const double MiniRadius = 22;
    public const double FullRadius = 30;
    public const double ArtInset = 10;
    public const double Border = 1;

    /// <summary>The shell width for the room available: 336 or less (min(100%, 21rem)).</summary>
    public static double ShellWidthFor(double available) =>
        double.IsFinite(available) ? Math.Max(0, Math.Min(available, MaxShellWidth)) : MaxShellWidth;

    /// <summary>Both layouts for a shell <paramref name="shellWidth"/> wide; compact (a viewport of 360 or less) drops the mini next button.</summary>
    public static NowPlayingLayout Compute(double shellWidth, bool compact)
    {
        var w = Math.Max(0, shellWidth - 2 * Border);

        // Mini: 44 | 1fr | levels (13 + 4) | 40 | 40, gap 10, padding 10; compact drops the last column.
        var miniArt = new Rect(MiniPadding, MiniPadding, MiniArtSize, MiniArtSize);
        var buttonY = MiniPadding + (MiniArtSize - MiniButtonSize) / 2;
        var nextX = w - MiniPadding - MiniButtonSize;
        var playX = compact ? nextX : nextX - MiniGap - MiniButtonSize;
        var miniNext = compact ? default : new Rect(nextX, buttonY, MiniButtonSize, MiniButtonSize);
        var miniPlay = new Rect(playX, buttonY, MiniButtonSize, MiniButtonSize);
        var levelsX = playX - MiniGap - (LevelsWidth + LevelsMarginRight);
        var miniLevels = new Rect(levelsX, MiniPadding + (MiniArtSize - LevelsHeight) / 2, LevelsWidth, LevelsHeight);
        var mainX = miniArt.Right + MiniGap;
        var miniMain = new Rect(mainX, MiniPadding, Math.Max(0, levelsX - MiniGap - mainX), MiniArtSize);
        // The progress line sits at the bottom of the inner box (62 to 64): the shell's border box clips it at rest, so it
        // only shows while the shell is taller than the mini bar during the morph (as measured).
        var miniProgress = new Rect(18, MiniShellHeight - 2, Math.Max(0, w - 36), 2);

        // Full: artwork, info, waveform with times, controls; 12 between rows.
        var artWidth = Math.Max(0, w - 2 * FullPaddingX);
        var fullArt = new Rect(FullPaddingX, FullPaddingTop, artWidth, FullArtHeight);
        var collapse = new Rect(fullArt.Right - CollapseInset - CollapseSize, fullArt.Y + CollapseInset, CollapseSize, CollapseSize);
        var textX = FullPaddingX + TextInset;
        var textWidth = Math.Max(0, artWidth - 2 * TextInset);
        var info = new Rect(textX, fullArt.Bottom + FullRowGap, textWidth, InfoHeight);
        var wave = new Rect(textX, info.Bottom + FullRowGap, textWidth, WaveHeight);
        var times = new Rect(textX, wave.Bottom + TimesGap, textWidth, TimesHeight);
        var rowY = wave.Y + WaveHeight + TimesGap + TimesHeight + FullRowGap;
        var cluster = 2 * SkipSize + PlaySize + 2 * ControlsGap;
        var startX = FullPaddingX + (artWidth - cluster) / 2;
        var skipY = rowY + (PlaySize - SkipSize) / 2;
        var previous = new Rect(startX, skipY, SkipSize, SkipSize);
        var fullPlay = new Rect(previous.Right + ControlsGap, rowY, PlaySize, PlaySize);
        var fullNext = new Rect(fullPlay.Right + ControlsGap, skipY, SkipSize, SkipSize);
        // The shell is as tall as the inner box (410.8 for the measured layout; the bottom border overlaps the padding).
        var fullHeight = Border + rowY + PlaySize + FullPaddingBottom;

        return new NowPlayingLayout(shellWidth, compact, MiniShellHeight, fullHeight,
            miniArt, miniMain, miniLevels, miniPlay, miniNext, miniProgress,
            fullArt, collapse, info, wave, times, previous, fullPlay, fullNext);
    }

    /// <summary>The morph progress for a shell height: 0 at the mini bar, 1 at the full player (overshoot passes through).</summary>
    public double Progress(double height) =>
        FullHeight - MiniHeight is var span && span > 0 ? (height - MiniHeight) / span : 0;

    /// <summary>The shell's corner radius at progress <paramref name="p"/>: 22 to 30.</summary>
    public static double RadiusAt(double p) => MiniRadius + (FullRadius - MiniRadius) * p;

    /// <summary>Linear interpolation of two rectangles (position and size each straight, as a layout projection does).</summary>
    public static Rect Lerp(Rect a, Rect b, double p) =>
        new(a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p,
            Math.Max(0, a.Width + (b.Width - a.Width) * p), Math.Max(0, a.Height + (b.Height - a.Height) * p));
}

/// <summary>The time texts: m:ss without a leading zero on the minutes, the remaining time and the slider's spoken value.</summary>
internal static class NowPlayingFormat
{
    /// <summary>Whole seconds as m:ss ("0:00", "3:34", "61:05").</summary>
    public static string Clock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>The elapsed text: m:ss of the whole seconds played.</summary>
    public static string Elapsed(double position) => Clock(Floor(position));

    /// <summary>The remaining text: the sign then m:ss of the length minus the whole seconds played ("−3:34").</summary>
    public static string Remaining(double position, double duration, string sign) =>
        sign + Clock(Floor(duration) - Floor(position));

    /// <summary>The slider's value text ("1:07 of 3:51").</summary>
    public static string ValueText(double position, double duration, string pattern) =>
        NowPlayingLabels.Format(pattern, Elapsed(position), Clock(Floor(duration)));

    public static int Floor(double seconds) => double.IsFinite(seconds) ? (int)Math.Floor(Math.Max(0, seconds)) : 0;
}

/// <summary>
/// Waveform peaks: a fixed shape per track id (soft ends, a fuller middle, smooth noise; 54 values from 0.16 to 0.98),
/// generated from a 32-bit hash of the id, and resampling of peaks of another length.
/// </summary>
internal static class NowPlayingPeaks
{
    public const int DefaultCount = 54;

    public static double[] Generate(string id, int count = DefaultCount)
    {
        count = Math.Max(1, count);
        var state = Hash(id);
        var random = new double[count];
        for (var i = 0; i < count; i++)
        {
            random[i] = Next(ref state);
        }
        var peaks = new double[count];
        for (var i = 0; i < count; i++)
        {
            var smooth = (random[Math.Max(0, i - 1)] + 2 * random[i] + random[Math.Min(count - 1, i + 1)]) / 4;
            var factor = 0.5 + 1.0 * smooth;
            var envelope = 0.17 + 0.55 * Math.Pow(Math.Sin(Math.PI * (i + 0.5) / count), 0.8);
            peaks[i] = Math.Clamp(envelope * factor, 0.16, 0.98);
        }
        return peaks;
    }

    /// <summary>Fits <paramref name="source"/> to <paramref name="count"/> bars (linear resampling, clamped to 0..1).</summary>
    public static double[] Resample(IReadOnlyList<double> source, int count)
    {
        count = Math.Max(1, count);
        var result = new double[count];
        if (source.Count == 0)
        {
            return result;
        }
        for (var i = 0; i < count; i++)
        {
            var at = count == 1 ? 0 : i * (source.Count - 1) / (double)(count - 1);
            var lo = (int)Math.Floor(at);
            var hi = Math.Min(source.Count - 1, lo + 1);
            var f = at - lo;
            var v = source[lo] + (source[hi] - source[lo]) * f;
            result[i] = double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 0;
        }
        return result;
    }

    /// <summary>FNV-1a over the UTF-16 code units.</summary>
    internal static uint Hash(string id)
    {
        var h = 2166136261u;
        foreach (var c in id ?? string.Empty)
        {
            h ^= c;
            h *= 16777619u;
        }
        return h;
    }

    /// <summary>mulberry32: a small, fast, well-mixed 32-bit generator, so the shape is the same everywhere.</summary>
    private static double Next(ref uint state)
    {
        unchecked
        {
            state += 0x6D2B79F5u;
            var t = state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}

/// <summary>
/// The pull on the full player's artwork (as measured): down follows at 0.6 of the pointer, up rubber-bands
/// (−RubberBand(|dy|, 200, 0.2)); a release past 70 (the pointer about 117 down) or a downward flick of 600 px/s or
/// more closes the player, anything else springs back.
/// </summary>
internal static class NowPlayingPull
{
    public const double Follow = 0.6;
    public const double UpDimension = 200;
    public const double UpFactor = 0.2;
    public const double CloseOffset = 70;

    /// <summary>The downward flick speed of the pointer that closes the player (px/s; measured between 580 and 700).</summary>
    public const double CloseVelocity = 600;

    /// <summary>The spring back to rest (ω 13.1, ζ 0.75; overshoot about 3%).</summary>
    public static readonly SpringSpec ReturnSpring = SpringSpec.FromVisual(0.4, 0.25);

    public static double Offset(double dy) => dy >= 0 ? Follow * dy : -RubberBand.Apply(-dy, UpDimension, UpFactor);

    public static bool ShouldClose(double offset, double velocityY) =>
        offset > CloseOffset || (offset > 0 && velocityY >= CloseVelocity);
}

/// <summary>The springs and tweens of the player as measured (shared by the parts).</summary>
internal static class NowPlayingMotion
{
    /// <summary>The shell opening (ω 11.34, ζ 0.9; 0.15% overshoot).</summary>
    public static readonly SpringSpec Expand = SpringSpec.FromVisual(0.46, 0.1);

    /// <summary>The shell folding (ω 11.3, ζ 1).</summary>
    public static readonly SpringSpec Collapse = SpringSpec.FromVisual(0.46, 0);

    /// <summary>k 400, c 40 (ω 20, ζ 1): the glyph swap's incoming icon and the waveform stretch.</summary>
    public static readonly SpringSpec Firm = SpringSpec.FromPhysical(400, 40);

    /// <summary>CSS ease-out (0, 0, 0.58, 1): the reduced shell tween and the artwork crossfade.</summary>
    public static readonly Avalonia.Animation.Easings.Easing EaseOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);

    /// <summary>CSS ease-in-out (0.42, 0, 0.58, 1): the level bars.</summary>
    public static readonly Avalonia.Animation.Easings.Easing EaseInOut = new Avalonia.Animation.Easings.SplineEasing(0.42, 0, 0.58, 1);

    public static readonly TimeSpan ReducedShell = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan ReducedExit = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan Enter = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan GlyphOut = TimeSpan.FromMilliseconds(120);
}

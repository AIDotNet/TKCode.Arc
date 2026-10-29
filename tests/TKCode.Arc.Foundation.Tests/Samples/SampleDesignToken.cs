using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Foundation.Samples;

/// <summary>Sample density, for the enum token path.</summary>
public enum SampleDensity
{
    /// <summary>Default spacing.</summary>
    Comfortable,

    /// <summary>Tighter spacing.</summary>
    Compact
}

/// <summary>
/// An ArcDesign-like global identity: derives an accent ramp from the seed and switches neutrals by appearance. It covers
/// every token type the generator supports.
/// </summary>
[ControlDesignToken(IsGlobal = true)]
internal sealed class SampleDesignToken : AbstractControlDesignToken
{
    public Color Accent { get; set; }
    public Color AccentStrong { get; set; }
    public Color AccentForeground { get; set; }
    public Color Surface { get; set; }
    public Color Foreground { get; set; }
    public Color ControlTrack { get; set; }
    public CornerRadius RadiusControl { get; set; }
    public CornerRadius RadiusPill { get; set; }
    public BoxShadows ShadowRaised { get; set; }
    public Thickness SpaceInset { get; set; }
    public double ControlSizeMD { get; set; }
    public int StaggerCount { get; set; }
    public bool ReduceTransparency { get; set; }
    public FontFamily BodyFontFamily { get; set; } = FontFamily.Default;
    public FontWeight WeightMedium { get; set; }
    public TimeSpan DurationFast { get; set; }
    public Easing EaseStandard { get; set; } = new LinearEasing();
    public SampleDensity Density { get; set; }
    public string MotionMode { get; set; } = "full";

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var seed = EffectiveGlobalToken.ColorPrimary;
        // Dark mode lifts the accent towards white, a stand-in for ArcPalette's hand-tuned dark ramp.
        Accent = isDarkMode ? Mix(seed, Colors.White, 0.2) : seed;
        AccentStrong = isDarkMode ? Mix(seed, Colors.White, 0.35) : Mix(seed, Colors.Black, 0.15);
        AccentForeground = isDarkMode ? Color.Parse("#0B0B0B") : Colors.White;
        Surface = isDarkMode ? Color.Parse("#1D1D1D") : Color.Parse("#FFFFFF");
        Foreground = isDarkMode ? Color.Parse("#F1F1F1") : Color.Parse("#0B0B0B");
        ControlTrack = isDarkMode ? Color.Parse("#383838") : Color.Parse("#E3E3E3");
        RadiusControl = new CornerRadius(18);
        RadiusPill = new CornerRadius(999);
        ShadowRaised = BoxShadows.Parse(isDarkMode ? "0 2 8 0 #66000000" : "0 2 8 0 #1A000000");
        SpaceInset = new Thickness(16, 8);
        ControlSizeMD = 44;
        StaggerCount = 4;
        ReduceTransparency = false;
        BodyFontFamily = new FontFamily("Inter");
        WeightMedium = FontWeight.Medium;
        DurationFast = TimeSpan.FromMilliseconds(160);
        EaseStandard = new SplineEasing(new KeySpline(0.2, 0, 0, 1));
        Density = SampleDensity.Comfortable;
        MotionMode = EffectiveGlobalToken.MotionLevel switch
        {
            ArcMotionLevel.Off => "off",
            ArcMotionLevel.Reduced => "reduced",
            _ => "full"
        };
    }

    internal static Color Mix(Color a, Color b, double amount)
    {
        byte Lerp(byte x, byte y) => (byte)Math.Round(x + (y - x) * amount);
        return Color.FromArgb(Lerp(a.A, b.A), Lerp(a.R, b.R), Lerp(a.G, b.G), Lerp(a.B, b.B));
    }
}

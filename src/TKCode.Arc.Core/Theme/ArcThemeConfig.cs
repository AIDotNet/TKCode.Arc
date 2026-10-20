using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AtomUI.Theme.Configuration;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Accumulates ArcDesign overrides into a single control config (WithControl replaces per identity).</summary>
public sealed class ArcThemeConfigBuilder
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

    /// <summary>Uses one of Arc's accent presets.</summary>
    public ArcThemeConfigBuilder Accent(ArcAccentPreset preset) => Accent(ArcPalette.GetSeed(preset));

    /// <summary>Uses any brand color as accent seed (derived in OKLCH when it is not a preset).</summary>
    public ArcThemeConfigBuilder Accent(Color seed)
    {
        _tokens["ColorPrimary"] = $"#{seed.R:X2}{seed.G:X2}{seed.B:X2}";
        return this;
    }

    /// <summary>Overrides the final value of an ArcDesign token.</summary>
    public ArcThemeConfigBuilder Token(string name, string value)
    {
        _tokens[name] = value;
        return this;
    }

    public ArcThemeConfigBuilder MotionMode(ArcMotionLevel level)
    {
        _tokens[nameof(ArcDesignTokenKind.MotionMode)] = level switch
        {
            ArcMotionLevel.Off => "off",
            ArcMotionLevel.Reduced => "reduced",
            _ => "full"
        };
        return this;
    }

    internal ControlThemeConfig Build()
    {
        // No WithAlgorithm: the mode stays Disabled so ColorPrimary passes through unchanged in dark mode.
        var builder = new ControlThemeConfigBuilder();
        foreach (var (name, value) in _tokens)
        {
            builder.WithToken(name, value);
        }
        return builder.Build();
    }
}

public static class ArcThemeConfigExtensions
{
    /// <summary>Applies Arc overrides (accent, token values, motion mode) to the ArcDesign identity.</summary>
    public static ThemeConfigBuilder WithArc(this ThemeConfigBuilder builder, Action<ArcThemeConfigBuilder> configure)
    {
        var arc = new ArcThemeConfigBuilder();
        configure(arc);
        return builder.WithControl(ArcDesignTokens.Identity, arc.Build());
    }
}

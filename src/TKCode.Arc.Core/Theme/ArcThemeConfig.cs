using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AtomUI.Theme.Schema;
using AtomUI.Theme.Configuration;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Accumulates ArcDesign overrides into a single control config (WithControl replaces per identity).</summary>
public sealed class ArcThemeConfigBuilder
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Catalog, string Control), Dictionary<string, string>> _controls = new();

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

    /// <summary>
    /// Overrides the final value of a control token (for example <c>Checkbox.BoxRadius</c> in catalog <c>TKCode.Arc</c>).
    /// Overrides of the same control are merged, so helpers that each write some tokens of a control do not replace one
    /// another. Naming a control of a package the app has not registered fails the whole theme change.
    /// </summary>
    public ArcThemeConfigBuilder ControlToken(string catalog, string control, string name, string value)
    {
        if (!_controls.TryGetValue((catalog, control), out var tokens))
        {
            tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            _controls[(catalog, control)] = tokens;
        }
        tokens[name] = value;
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

    internal ControlThemeConfig Build() => Build(_tokens);

    internal ThemeConfigBuilder ApplyTo(ThemeConfigBuilder target)
    {
        target = target.WithControl(ArcDesignTokens.Identity, Build(_tokens));
        foreach (var ((catalog, control), tokens) in _controls)
        {
            target = target.WithControl(new ControlTokenIdentity(catalog, control), Build(tokens));
        }
        return target;
    }

    private static ControlThemeConfig Build(Dictionary<string, string> tokens)
    {
        // No WithAlgorithm: the mode stays Disabled so ColorPrimary passes through unchanged in dark mode.
        var builder = new ControlThemeConfigBuilder();
        foreach (var (name, value) in tokens)
        {
            builder.WithToken(name, value);
        }
        return builder.Build();
    }
}

public static class ArcThemeConfigExtensions
{
    /// <summary>
    /// Applies Arc overrides (accent, token values, motion mode) to the ArcDesign identity, and control token overrides to
    /// their controls. Put every override in one call: a second call replaces the configs the first one wrote.
    /// </summary>
    public static ThemeConfigBuilder WithArc(this ThemeConfigBuilder builder, Action<ArcThemeConfigBuilder> configure)
    {
        var arc = new ArcThemeConfigBuilder();
        configure(arc);
        return arc.ApplyTo(builder);
    }
}

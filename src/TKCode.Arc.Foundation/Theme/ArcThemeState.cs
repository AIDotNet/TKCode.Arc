using System.Collections.Immutable;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Light, dark, or following the operating system.</summary>
public enum ArcAppearance
{
    /// <summary>The light appearance.</summary>
    Light = 0,

    /// <summary>The dark appearance.</summary>
    Dark = 1,

    /// <summary>Follows the platform's light or dark setting and updates when it changes.</summary>
    System = 2
}

/// <summary>
/// Everything a theme is computed from: appearance, accent seed, motion level, theme presets and string token overrides. Immutable;
/// derive a changed copy with <see cref="With"/>.
/// </summary>
public sealed class ArcThemeState
{
    /// <summary>The identity name under which overrides of the global identity are stored when they name no identity.</summary>
    public const string GlobalIdentity = "";

    private static readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> s_noOverrides =
        ImmutableDictionary.Create<string, ImmutableDictionary<string, string>>(StringComparer.Ordinal);

    /// <summary>Light appearance, default accent, full motion, no overrides.</summary>
    public static ArcThemeState Default { get; } = new(ArcAppearance.Light, null, ArcMotionLevel.Full, s_noOverrides,
        ImmutableArray<ArcThemePreset>.Empty);

    internal ArcThemeState(ArcAppearance appearance, Color? accent, ArcMotionLevel motionLevel,
                           ImmutableDictionary<string, ImmutableDictionary<string, string>> overrides,
                           ImmutableArray<ArcThemePreset> presets)
    {
        Appearance = appearance;
        Accent = accent;
        MotionLevel = motionLevel;
        Overrides = overrides;
        Presets = presets;
    }

    /// <summary>The requested appearance.</summary>
    public ArcAppearance Appearance { get; }

    /// <summary>The accent seed, or null for <see cref="ArcSeedToken.DefaultAccent"/>.</summary>
    public Color? Accent { get; }

    /// <summary>The motion level.</summary>
    public ArcMotionLevel MotionLevel { get; }

    /// <summary>
    /// Token overrides: identity → (token name → value text). Overrides of the global identity that name no identity are
    /// stored under <see cref="GlobalIdentity"/>. Overrides are applied after the calculation, as final values.
    /// </summary>
    public IReadOnlyDictionary<string, ImmutableDictionary<string, string>> TokenOverrides => Overrides;

    internal ImmutableDictionary<string, ImmutableDictionary<string, string>> Overrides { get; }

    /// <summary>
    /// The theme presets, at most one per <see cref="ArcThemePreset.Slot"/>, applied in this order after the calculation
    /// and before <see cref="TokenOverrides"/>.
    /// </summary>
    public ImmutableArray<ArcThemePreset> Presets { get; }

    /// <summary>Returns a copy changed by <paramref name="configure"/>; the parts it does not touch are kept.</summary>
    /// <param name="configure">Changes to make.</param>
    public ArcThemeState With(Action<ArcThemeBuilder> configure)
    {
        var builder = new ArcThemeBuilder(this);
        configure(builder);
        return builder.Build();
    }

    /// <summary>Resolves the appearance to light or dark.</summary>
    /// <param name="systemIsDark">Whether the platform is currently dark (used for <see cref="ArcAppearance.System"/>).</param>
    public bool ResolveIsDark(bool systemIsDark) => Appearance switch
    {
        ArcAppearance.Dark => true,
        ArcAppearance.System => systemIsDark,
        _ => false
    };
}

/// <summary>
/// Builds an <see cref="ArcThemeState"/> from an existing one: what is not set keeps the value of the starting state.
/// </summary>
public sealed class ArcThemeBuilder
{
    private ArcAppearance _appearance;
    private Color? _accent;
    private ArcMotionLevel _motionLevel;
    private ImmutableDictionary<string, ImmutableDictionary<string, string>> _overrides;
    private ImmutableArray<ArcThemePreset> _presets;

    /// <summary>Starts from <paramref name="from"/>.</summary>
    /// <param name="from">The state whose values are kept unless changed.</param>
    public ArcThemeBuilder(ArcThemeState from)
    {
        _appearance = from.Appearance;
        _accent = from.Accent;
        _motionLevel = from.MotionLevel;
        _overrides = from.Overrides;
        _presets = from.Presets;
    }

    /// <summary>Sets the appearance.</summary>
    public ArcThemeBuilder Appearance(ArcAppearance appearance)
    {
        _appearance = appearance;
        return this;
    }

    /// <summary>Uses <paramref name="seed"/> as accent seed (the global identity derives its accent ramp from it).</summary>
    public ArcThemeBuilder Accent(Color seed)
    {
        _accent = seed;
        return this;
    }

    /// <summary>Goes back to the default accent seed.</summary>
    public ArcThemeBuilder DefaultAccent()
    {
        _accent = null;
        return this;
    }

    /// <summary>Sets the motion level.</summary>
    public ArcThemeBuilder MotionLevel(ArcMotionLevel level)
    {
        _motionLevel = level;
        return this;
    }

    /// <summary>Overrides the final value of a token of the global identity (Arc's <c>ArcDesign</c>).</summary>
    /// <param name="name">The token name, for example <c>RadiusControl</c>.</param>
    /// <param name="value">The value text, parsed by the token's type when the theme is applied.</param>
    public ArcThemeBuilder Token(string name, string value) => Token(ArcThemeState.GlobalIdentity, name, value);

    /// <summary>Overrides the final value of a token of <paramref name="identity"/>.</summary>
    /// <param name="identity">The identity, for example <c>Button</c>.</param>
    /// <param name="name">The token name.</param>
    /// <param name="value">The value text, parsed by the token's type when the theme is applied.</param>
    public ArcThemeBuilder Token(string identity, string name, string value)
    {
        var tokens = _overrides.TryGetValue(identity, out var existing)
            ? existing
            : ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
        _overrides = _overrides.SetItem(identity, tokens.SetItem(name, value));
        return this;
    }

    /// <summary>Removes every token override.</summary>
    public ArcThemeBuilder ClearTokens()
    {
        _overrides = _overrides.Clear();
        return this;
    }

    /// <summary>
    /// Adds a theme preset. A preset already in the state with the same <see cref="ArcThemePreset.Slot"/> is replaced in
    /// place (a new corner style replaces the old one); otherwise the preset goes last, so it wins over earlier ones.
    /// </summary>
    /// <param name="preset">The preset, for example <c>ArcPixelPresets.Pixel</c>.</param>
    public ArcThemeBuilder Preset(ArcThemePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        for (var i = 0; i < _presets.Length; i++)
        {
            if (string.Equals(_presets[i].Slot, preset.Slot, StringComparison.Ordinal))
            {
                _presets = _presets.SetItem(i, preset);
                return this;
            }
        }
        _presets = _presets.Add(preset);
        return this;
    }

    /// <summary>Removes the preset in <paramref name="slot"/>, if any.</summary>
    /// <param name="slot">The preset slot, for example <c>ArcCornerPresets.Slot</c>.</param>
    public ArcThemeBuilder RemovePreset(string slot)
    {
        _presets = _presets.RemoveAll(p => string.Equals(p.Slot, slot, StringComparison.Ordinal));
        return this;
    }

    /// <summary>Removes every preset.</summary>
    public ArcThemeBuilder ClearPresets()
    {
        _presets = ImmutableArray<ArcThemePreset>.Empty;
        return this;
    }

    /// <summary>Builds the state.</summary>
    public ArcThemeState Build() => new(_appearance, _accent, _motionLevel, _overrides, _presets);
}

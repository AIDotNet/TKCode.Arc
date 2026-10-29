using System.Collections.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A named, immutable bundle of token overrides that a theme package ships (the Pixel style, a corner style): overrides
/// for both appearances plus overrides for light or dark only. Add it to a state with
/// <c>ArcThemeBuilder.Preset</c>; packages make theirs discoverable with <c>ArcPackageBuilder.AddPresets</c>.
/// </summary>
/// <remarks>
/// <para>Overrides are final values, like <c>ArcThemeBuilder.Token</c>. When a state is
/// computed, the presets are applied in order after the token calculation, then the state's own overrides, so the app
/// always wins over a preset and a later preset wins over an earlier one.</para>
/// <para>Unlike the state's own overrides, a preset's overrides of an identity that is not registered are skipped: a preset
/// may cover every Arc package, and an app that did not enable one of them simply does not get those overrides. A token
/// name the registered identity does not have is still an error.</para>
/// <para>This file depends on nothing but the base class library so that tests can compile it on its own.</para>
/// </remarks>
public sealed class ArcThemePreset
{
    /// <summary>The identity name under which overrides of the global identity are stored when they name no identity.</summary>
    public const string GlobalIdentity = "";

    private ArcThemePreset(string name, string slot, ArcPresetOverrides light, ArcPresetOverrides dark)
    {
        Name = name;
        Slot = slot;
        LightTokens = light;
        DarkTokens = dark;
    }

    /// <summary>The unique name, for example <c>ArcPixel</c> or <c>ArcCornersSquare</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// What the preset replaces: a state holds at most one preset per slot, so adding a corner style replaces the previous
    /// corner style while a Pixel preset in another slot stays. Defaults to <see cref="Name"/>.
    /// </summary>
    public string Slot { get; }

    /// <summary>The overrides used when the state resolves to light: identity → (token name → value text).</summary>
    public IReadOnlyDictionary<string, ImmutableDictionary<string, string>> LightTokens { get; }

    /// <summary>The overrides used when the state resolves to dark: identity → (token name → value text).</summary>
    public IReadOnlyDictionary<string, ImmutableDictionary<string, string>> DarkTokens { get; }

    /// <summary>The overrides for one appearance.</summary>
    /// <param name="isDark">Whether the state resolves to dark.</param>
    public IReadOnlyDictionary<string, ImmutableDictionary<string, string>> TokensFor(bool isDark)
        => isDark ? DarkTokens : LightTokens;

    /// <summary>Whether the preset overrides nothing (for example the default corner style).</summary>
    public bool IsEmpty => LightTokens.Count == 0 && DarkTokens.Count == 0;

    /// <summary>Defines a preset.</summary>
    /// <param name="name">The unique name.</param>
    /// <param name="define">Adds the overrides.</param>
    /// <param name="slot">What the preset replaces in a state; null uses <paramref name="name"/>.</param>
    public static ArcThemePreset Create(string name, Action<ArcThemePresetBuilder> define, string? slot = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A preset needs a name.", nameof(name));
        }
        if (slot is { Length: 0 })
        {
            throw new ArgumentException("A preset slot cannot be empty.", nameof(slot));
        }
        var builder = new ArcThemePresetBuilder();
        define(builder);
        return new ArcThemePreset(name, slot ?? name, builder.Light.ToImmutable(), builder.Dark.ToImmutable());
    }

    /// <inheritdoc />
    public override string ToString() => Slot == Name ? Name : $"{Name} ({Slot})";
}

/// <summary>Collects the overrides of an <see cref="ArcThemePreset"/>. Later writes of the same token win.</summary>
public sealed class ArcThemePresetBuilder
{
    private readonly bool _light;
    private readonly bool _dark;

    internal ArcThemePresetBuilder()
        : this(new ArcPresetOverrides.Builder(), new ArcPresetOverrides.Builder(), true, true)
    {
    }

    private ArcThemePresetBuilder(ArcPresetOverrides.Builder light, ArcPresetOverrides.Builder dark, bool toLight, bool toDark)
    {
        Light = light;
        Dark = dark;
        _light = toLight;
        _dark = toDark;
    }

    internal ArcPresetOverrides.Builder Light { get; }

    internal ArcPresetOverrides.Builder Dark { get; }

    /// <summary>Overrides a token of the global identity (Arc's <c>ArcDesign</c>).</summary>
    /// <param name="name">The token name.</param>
    /// <param name="value">The value text, parsed by the token's type when the theme is computed.</param>
    public ArcThemePresetBuilder Token(string name, string value) => Token(ArcThemePreset.GlobalIdentity, name, value);

    /// <summary>Overrides a token of <paramref name="identity"/>.</summary>
    /// <param name="identity">The identity, for example <c>Button</c>.</param>
    /// <param name="name">The token name.</param>
    /// <param name="value">The value text, parsed by the token's type when the theme is computed.</param>
    public ArcThemePresetBuilder Token(string identity, string name, string value)
    {
        if (_light)
        {
            Light.Set(identity, name, value);
        }
        if (_dark)
        {
            Dark.Set(identity, name, value);
        }
        return this;
    }

    /// <summary>Adds overrides that apply only when the state resolves to light.</summary>
    /// <param name="define">Adds the overrides.</param>
    public ArcThemePresetBuilder ForLight(Action<ArcThemePresetBuilder> define)
    {
        if (_light)
        {
            define(new ArcThemePresetBuilder(Light, Dark, true, false));
        }
        return this;
    }

    /// <summary>Adds overrides that apply only when the state resolves to dark.</summary>
    /// <param name="define">Adds the overrides.</param>
    public ArcThemePresetBuilder ForDark(Action<ArcThemePresetBuilder> define)
    {
        if (_dark)
        {
            define(new ArcThemePresetBuilder(Light, Dark, false, true));
        }
        return this;
    }

    /// <summary>
    /// Copies the overrides of <paramref name="preset"/> (its light overrides into light, its dark ones into dark), for
    /// example the Pixel style including the square corner style.
    /// </summary>
    /// <param name="preset">The preset to include.</param>
    public ArcThemePresetBuilder Include(ArcThemePreset preset)
    {
        if (_light)
        {
            Light.SetAll(preset.LightTokens);
        }
        if (_dark)
        {
            Dark.SetAll(preset.DarkTokens);
        }
        return this;
    }
}

/// <summary>The override table of one appearance of a preset.</summary>
internal sealed class ArcPresetOverrides : IReadOnlyDictionary<string, ImmutableDictionary<string, string>>
{
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> _items;

    private ArcPresetOverrides(ImmutableDictionary<string, ImmutableDictionary<string, string>> items) => _items = items;

    public ImmutableDictionary<string, string> this[string key] => _items[key];

    public IEnumerable<string> Keys => _items.Keys;

    public IEnumerable<ImmutableDictionary<string, string>> Values => _items.Values;

    public int Count => _items.Count;

    public bool ContainsKey(string key) => _items.ContainsKey(key);

    public bool TryGetValue(string key, out ImmutableDictionary<string, string> value)
        => _items.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, ImmutableDictionary<string, string>>> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal sealed class Builder
    {
        private readonly Dictionary<string, ImmutableDictionary<string, string>.Builder> _items = new(StringComparer.Ordinal);

        public void Set(string identity, string name, string value)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);
            if (!_items.TryGetValue(identity, out var tokens))
            {
                tokens = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
                _items.Add(identity, tokens);
            }
            tokens[name] = value;
        }

        public void SetAll(IReadOnlyDictionary<string, ImmutableDictionary<string, string>> overrides)
        {
            foreach (var (identity, tokens) in overrides)
            {
                foreach (var (name, value) in tokens)
                {
                    Set(identity, name, value);
                }
            }
        }

        public ArcPresetOverrides ToImmutable()
        {
            var result = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var (identity, tokens) in _items)
            {
                result.Add(identity, tokens.ToImmutable());
            }
            return new ArcPresetOverrides(result.ToImmutable());
        }
    }
}

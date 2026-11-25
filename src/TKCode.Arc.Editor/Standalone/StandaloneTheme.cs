// Ported from vscode (src/vs/editor/standalone/common/standaloneTheme.ts and the theme parts of
// src/vs/editor/standalone/browser/standaloneThemeService.ts: StandaloneTheme, isBuiltinTheme, getBuiltinRules,
// defineTheme, setTheme, setColorMapOverride, commit b57d5c9e), MIT license.
//
// Differences:
// - Only what turns a theme definition into a TokenTheme and publishes its color map: no CSS, no color registry (a color
//   the theme does not define is null instead of the registry's default), no theming participants, no high contrast
//   auto-detection, no icon themes.
// - The service is an instance that publishes the color map to the TokenizationRegistry it is given, as the registry is
//   (Languages/TokenizationRegistry.cs).
// - StandaloneTheme.TokenTheme copies the base theme's rules before adding to them (upstream pushes the
//   editor.foreground / editor.background rule into the built-in theme's own array); and it is computed once under a
//   lock, as tokenizers read it off the UI thread.
// - IColors is IReadOnlyDictionary<string, string>; BuiltinTheme is a string checked by IsBuiltinTheme.

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Standalone;

/// <summary>Whether a theme is light, dark or high contrast (upstream's ColorScheme).</summary>
internal enum ColorScheme
{
    Dark,
    Light,
    HighContrastDark,
    HighContrastLight,
}

/// <summary>A theme definition (upstream's IStandaloneThemeData).</summary>
/// <param name="Base">The built-in theme it starts from: <c>vs</c>, <c>vs-dark</c>, <c>hc-black</c> or <c>hc-light</c>.</param>
/// <param name="Inherit">Whether the base theme's rules and colors apply under this theme's.</param>
/// <param name="Rules">The token rules.</param>
/// <param name="Colors">The workbench colors by id, such as <c>editor.background</c>, as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.</param>
internal sealed record StandaloneThemeData(string Base, bool Inherit, IReadOnlyList<TokenThemeRule> Rules, IReadOnlyDictionary<string, string> Colors)
{

    /// <summary>Colors that take the first ids of the color map, in order (six hex digits each).</summary>
    public IReadOnlyList<string>? EncodedTokensColors { get; init; }
}

/// <summary>A theme the tokenizers read their token colors from (upstream's IStandaloneTheme).</summary>
internal interface IStandaloneTheme
{
    string ThemeName { get; }

    TokenTheme TokenTheme { get; }
}

/// <summary>Where a tokenizer gets the current theme (upstream's IStandaloneThemeService, the part the lexer uses).</summary>
internal interface IStandaloneThemeService
{
    IStandaloneTheme GetColorTheme();
}

/// <summary>A named theme made from a theme definition.</summary>
internal sealed class StandaloneTheme : IStandaloneTheme
{
    public const string VsLightThemeName = "vs";
    public const string VsDarkThemeName = "vs-dark";
    public const string HcBlackThemeName = "hc-black";
    public const string HcLightThemeName = "hc-light";

    private readonly StandaloneThemeData _themeData;
    private readonly SyncLock _gate = new();
    private IReadOnlyDictionary<string, RgbaColor>? _colors;
    private TokenTheme? _tokenTheme;

    public StandaloneTheme(string name, StandaloneThemeData standaloneThemeData)
    {
        _themeData = standaloneThemeData;
        var @base = standaloneThemeData.Base;
        if (name.Length > 0)
        {
            Id = IsBuiltinTheme(name) ? name : @base + " " + name;
            ThemeName = name;
        }
        else
        {
            Id = @base;
            ThemeName = @base;
        }
    }

    public string Id { get; }

    public string ThemeName { get; }

    public string Label => ThemeName;

    public string Base => _themeData.Base;

    public ColorScheme Type => Base switch
    {
        VsLightThemeName => ColorScheme.Light,
        HcBlackThemeName => ColorScheme.HighContrastDark,
        HcLightThemeName => ColorScheme.HighContrastLight,
        _ => ColorScheme.Dark,
    };

    public TokenTheme TokenTheme
    {
        get
        {
            lock (_gate)
            {
                return _tokenTheme ??= CreateTokenTheme();
            }
        }
    }

    /// <summary>Called when the built-in theme this one inherits from was redefined.</summary>
    public void NotifyBaseUpdated()
    {
        if (_themeData.Inherit)
        {
            lock (_gate)
            {
                _colors = null;
                _tokenTheme = null;
            }
        }
    }

    /// <summary>A color the theme (or the base it inherits) defines; null when it defines none.</summary>
    public RgbaColor? GetColor(string colorId) => GetColors().TryGetValue(colorId, out var color) ? color : null;

    /// <summary>A color the theme defines, or else its default for the theme's type (see <see cref="EditorColorRegistry"/>).</summary>
    public RgbaColor? GetColorOrDefault(string colorId) => GetColor(colorId) ?? EditorColorRegistry.GetDefault(colorId, Type);

    public bool Defines(string colorId) => GetColors().ContainsKey(colorId);

    public static bool IsBuiltinTheme(string themeName) =>
        themeName is VsLightThemeName or VsDarkThemeName or HcBlackThemeName or HcLightThemeName;

    public static StandaloneThemeData GetBuiltinRules(string builtinTheme) => builtinTheme switch
    {
        VsLightThemeName => Themes.Vs,
        VsDarkThemeName => Themes.VsDark,
        HcBlackThemeName => Themes.HcBlack,
        HcLightThemeName => Themes.HcLight,
        _ => throw new ArgumentException("Not a built-in theme: " + builtinTheme, nameof(builtinTheme)),
    };

    private IReadOnlyDictionary<string, RgbaColor> GetColors()
    {
        lock (_gate)
        {
            if (_colors is null)
            {
                var colors = new Dictionary<string, RgbaColor>(StringComparer.Ordinal);
                foreach (var (id, value) in _themeData.Colors)
                {
                    colors[id] = RgbaColor.FromHex(value);
                }

                if (_themeData.Inherit)
                {
                    foreach (var (id, value) in GetBuiltinRules(_themeData.Base).Colors)
                    {
                        colors.TryAdd(id, RgbaColor.FromHex(value));
                    }
                }

                _colors = colors;
            }

            return _colors;
        }
    }

    private TokenTheme CreateTokenTheme()
    {
        var rules = new List<TokenThemeRule>();
        IReadOnlyList<string> encodedTokensColors = [];
        if (_themeData.Inherit)
        {
            var baseData = GetBuiltinRules(_themeData.Base);
            rules.AddRange(baseData.Rules);
            if (baseData.EncodedTokensColors is { } baseColors)
            {
                encodedTokensColors = baseColors;
            }
        }

        // Pick up default colors from `editor.foreground` and `editor.background` if available
        _themeData.Colors.TryGetValue("editor.foreground", out var editorForeground);
        _themeData.Colors.TryGetValue("editor.background", out var editorBackground);
        if (!string.IsNullOrEmpty(editorForeground) || !string.IsNullOrEmpty(editorBackground))
        {
            rules.Add(new TokenThemeRule("")
            {
                Foreground = string.IsNullOrEmpty(editorForeground) ? null : editorForeground,
                Background = string.IsNullOrEmpty(editorBackground) ? null : editorBackground,
            });
        }

        rules.AddRange(_themeData.Rules);
        if (_themeData.EncodedTokensColors is { } colors)
        {
            encodedTokensColors = colors;
        }

        return TokenTheme.CreateFromRawTokenTheme(rules, encodedTokensColors);
    }
}

/// <summary>
/// The known themes and the current one; publishes the current theme's color map to a tokenization registry
/// (upstream's StandaloneThemeService without its CSS).
/// </summary>
internal sealed class StandaloneThemeService : IStandaloneThemeService
{
    private readonly SyncLock _gate = new();
    private readonly TokenizationRegistry<ITokenizationSupport>? _tokenizationRegistry;
    private readonly Dictionary<string, StandaloneTheme> _knownThemes = new(StringComparer.Ordinal);
    private StandaloneTheme _theme;
    private RgbaColor[]? _colorMapOverride;

    /// <param name="tokenizationRegistry">The registry whose color map follows the current theme; null for none.</param>
    public StandaloneThemeService(TokenizationRegistry<ITokenizationSupport>? tokenizationRegistry = null)
    {
        _tokenizationRegistry = tokenizationRegistry;
        foreach (var name in new[] { StandaloneTheme.VsLightThemeName, StandaloneTheme.VsDarkThemeName, StandaloneTheme.HcBlackThemeName, StandaloneTheme.HcLightThemeName })
        {
            _knownThemes[name] = new StandaloneTheme(name, StandaloneTheme.GetBuiltinRules(name));
        }

        _theme = _knownThemes[StandaloneTheme.VsLightThemeName];
        UpdateThemeOrColorMap();
    }

    /// <summary>The current theme changed, or its color map was overridden.</summary>
    public event Action<StandaloneTheme>? DidColorThemeChange;

    public IStandaloneTheme GetColorTheme()
    {
        lock (_gate)
        {
            return _theme;
        }
    }

    /// <summary>Defines a theme or replaces one; the name may be a built-in theme's, which redefines it.</summary>
    public void DefineTheme(string themeName, StandaloneThemeData themeData)
    {
        if (themeName.Length == 0 || !themeName.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new ArgumentException("Illegal theme name!", nameof(themeName));
        }

        if (!StandaloneTheme.IsBuiltinTheme(themeData.Base) && !StandaloneTheme.IsBuiltinTheme(themeName))
        {
            throw new ArgumentException("Illegal theme base!", nameof(themeData));
        }

        bool refresh;
        lock (_gate)
        {
            // set or replace theme
            _knownThemes[themeName] = new StandaloneTheme(themeName, themeData);

            if (StandaloneTheme.IsBuiltinTheme(themeName))
            {
                foreach (var theme in _knownThemes.Values)
                {
                    if (theme.Base == themeName)
                    {
                        theme.NotifyBaseUpdated();
                    }
                }
            }

            refresh = _theme.ThemeName == themeName;
        }

        if (refresh)
        {
            SetTheme(themeName); // refresh theme
        }
    }

    /// <summary>Makes a known theme current; an unknown name selects <c>vs</c>.</summary>
    public void SetTheme(string themeName)
    {
        lock (_gate)
        {
            if (!_knownThemes.TryGetValue(themeName, out var theme))
            {
                theme = _knownThemes[StandaloneTheme.VsLightThemeName];
            }

            if (ReferenceEquals(_theme, theme))
            {
                // Nothing to do
                return;
            }

            _theme = theme;
        }

        UpdateThemeOrColorMap();
    }

    /// <summary>Publishes these colors instead of the theme's color map; null to go back to the theme's.</summary>
    public void SetColorMapOverride(RgbaColor[]? colorMapOverride)
    {
        lock (_gate)
        {
            _colorMapOverride = colorMapOverride;
        }

        UpdateThemeOrColorMap();
    }

    /// <summary>The color map that is published: the override, or the current theme's.</summary>
    public RgbaColor[] GetColorMap()
    {
        lock (_gate)
        {
            return _colorMapOverride ?? _theme.TokenTheme.GetColorMap();
        }
    }

    private void UpdateThemeOrColorMap()
    {
        StandaloneTheme theme;
        lock (_gate)
        {
            theme = _theme;
        }

        _tokenizationRegistry?.SetColorMap(GetColorMap());
        DidColorThemeChange?.Invoke(theme);
    }
}

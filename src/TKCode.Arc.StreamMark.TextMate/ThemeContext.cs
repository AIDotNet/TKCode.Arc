using System.Globalization;
using StreamMark.Core.Highlighting;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Types;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace StreamMark.Highlighting.TextMate;

/// <summary>
/// 一个主题对应的 TextMate <see cref="Registry"/> 与已加载语法缓存。
/// </summary>
/// <remarks>
/// TextMateSharp 2.0.4 的 Registry/SyncRegistry 用普通 Dictionary 存语法，Grammar 在分词时惰性编译规则、
/// 修改 _ruleId2desc/_includedGrammars 等内部状态，且主题匹配缓存会在分词中被写入 —— 都不是线程安全的。
/// 因此本类所有对 Registry、Grammar、Theme 的访问都必须持有 <see cref="Gate"/>。
/// 明暗主题各自一个 Registry/锁，彼此可以并行。
/// </remarks>
internal sealed class ThemeContext
{
    private readonly Registry _registry;
    private readonly Theme _theme;
    private readonly Dictionary<string, IGrammar?> _grammars = new(StringComparer.Ordinal);

    // colorId → ARGB（已把默认前景折算成 0）；_colorResolved 标记是否已计算
    private uint[] _colors = new uint[64];
    private bool[] _colorResolved = new bool[64];

    private const int DefaultTokenColorId = 1;

    public ThemeContext(RegistryOptions options, ThemeName themeName, HighlightThemeKind kind)
    {
        _registry = new Registry(new ThemedRegistryOptions(options, themeName));
        _theme = _registry.GetTheme();

        var gui = _theme.GetGuiColorDictionary();
        uint fallbackFg = kind == HighlightThemeKind.Dark ? 0xFFD4D4D4u : 0xFF000000u;
        uint fallbackBg = kind == HighlightThemeKind.Dark ? 0xFF1E1E1Eu : 0xFFFFFFFFu;
        DefaultForeground = gui.TryGetValue("editor.foreground", out string? fg) && TryParseColor(fg, out uint f) ? f : fallbackFg;
        DefaultBackground = gui.TryGetValue("editor.background", out string? bg) && TryParseColor(bg, out uint b) ? b : fallbackBg;
    }

    /// <summary>保护 Registry、其全部 Grammar 与 Theme 的锁。</summary>
    public object Gate { get; } = new();

    public uint DefaultForeground { get; }

    public uint DefaultBackground { get; }

    /// <summary>取（必要时加载）语法；加载失败缓存为 null。调用方必须持有 <see cref="Gate"/>。</summary>
    public IGrammar? GetGrammarLocked(string scopeName)
    {
        if (!_grammars.TryGetValue(scopeName, out IGrammar? grammar))
        {
            try
            {
                grammar = _registry.LoadGrammar(scopeName);
            }
            catch (Exception)
            {
                grammar = null;
            }

            _grammars[scopeName] = grammar;
        }

        return grammar;
    }

    /// <summary>
    /// 主题颜色 id → ARGB；主题默认规则色（无 scope 的规则，缺省为 #000000）或等于编辑器前景色时返回 0。
    /// 调用方必须持有 <see cref="Gate"/>。
    /// </summary>
    /// <remarks>
    /// TextMateSharp 的 Theme.CreateFromRawTheme 用一个全新的 ColorMap 先解析主主题，而解析时第一个登记的颜色就是
    /// 默认前景（ResolveParsedThemeRules 里的 colorMap.GetId(defaultForeground)），因此默认前景的 id 恒为 1。
    /// 未被任何主题规则命中的片段（空白、标点等）都带这个 id —— VS Code 用 editor.foreground 显示它们。
    /// </remarks>
    public uint ResolveForegroundLocked(int colorId)
    {
        if (colorId <= DefaultTokenColorId)
        {
            return 0;
        }

        if (colorId >= _colors.Length)
        {
            int size = Math.Max(colorId + 1, _colors.Length * 2);
            Array.Resize(ref _colors, size);
            Array.Resize(ref _colorResolved, size);
        }

        if (!_colorResolved[colorId])
        {
            uint argb = 0;
            try
            {
                string? text = _theme.GetColor(colorId);
                if (text is not null && TryParseColor(text, out uint parsed) && parsed != DefaultForeground)
                {
                    argb = parsed;
                }
            }
            catch (Exception)
            {
                argb = 0;
            }

            _colors[colorId] = argb;
            _colorResolved[colorId] = true;
        }

        return _colors[colorId];
    }

    /// <summary>解析 "#RGB" / "#RGBA" / "#RRGGBB" / "#RRGGBBAA" 为 0xAARRGGBB。</summary>
    internal static bool TryParseColor(string? text, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrEmpty(text) || text[0] != '#')
        {
            return false;
        }

        ReadOnlySpan<char> hex = text.AsSpan(1);
        if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint v))
        {
            return false;
        }

        switch (hex.Length)
        {
            case 3:
            {
                uint r = (v >> 8) & 0xF, g = (v >> 4) & 0xF, b = v & 0xF;
                argb = 0xFF000000u | (r * 0x11) << 16 | (g * 0x11) << 8 | (b * 0x11);
                return true;
            }
            case 4:
            {
                uint r = (v >> 12) & 0xF, g = (v >> 8) & 0xF, b = (v >> 4) & 0xF, a = v & 0xF;
                argb = (a * 0x11) << 24 | (r * 0x11) << 16 | (g * 0x11) << 8 | (b * 0x11);
                return true;
            }
            case 6:
                argb = 0xFF000000u | v;
                return true;
            case 8:
                argb = (v & 0xFF) << 24 | (v >> 8);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 共享同一个（只读的）<see cref="RegistryOptions"/>，只替换默认主题，避免为每个主题重复解析全部 package.json。
    /// </summary>
    private sealed class ThemedRegistryOptions(RegistryOptions inner, ThemeName theme) : IRegistryOptions
    {
        public IRawTheme GetTheme(string scopeName) => inner.GetTheme(scopeName);

        public IRawGrammar GetGrammar(string scopeName) => inner.GetGrammar(scopeName);

        public ICollection<string> GetInjections(string scopeName) => inner.GetInjections(scopeName);

        public IRawTheme GetDefaultTheme() => inner.LoadTheme(theme);
    }
}

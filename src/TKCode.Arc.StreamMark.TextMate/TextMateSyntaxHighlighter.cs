using System.Collections.Concurrent;
using StreamMark.Core.Highlighting;
using TextMateSharp.Grammars;

namespace StreamMark.Highlighting.TextMate;

/// <summary>
/// 基于 TextMateSharp（VS Code 同款 TextMate 语法与主题）的增量代码高亮器。
/// </summary>
/// <remarks>
/// <para>线程安全：可在任意线程并发调用 <see cref="SupportsLanguage"/>、<see cref="CreateSession"/>、
/// <see cref="GetThemeColors"/> 与 <see cref="Prewarm"/>。每个主题共享一个 TextMate Registry，
/// 语法按 scope 缓存；对 Registry/Grammar 的访问按主题加锁串行化（TextMateSharp 本身不是线程安全的）。</para>
/// <para>单个会话只能在一个线程上顺序使用。语法在会话首次 <c>Update</c> 时加载（C# 冷加载约数百毫秒），
/// 可用 <see cref="Prewarm"/> 提前在后台加载。</para>
/// </remarks>
public sealed class TextMateSyntaxHighlighter : ISyntaxHighlighter
{
    private static readonly Lazy<RegistryOptions> SharedOptions =
        new(() => new RegistryOptions(ThemeName.DarkPlus), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Lazy<ThemeContext> _dark;
    private readonly Lazy<ThemeContext> _light;
    private readonly ConcurrentDictionary<string, string?> _scopeCache = new(StringComparer.Ordinal);

    /// <summary>创建高亮器。</summary>
    /// <param name="darkTheme">暗色主题，默认 VS Code Dark+。</param>
    /// <param name="lightTheme">亮色主题，默认 VS Code Light+。</param>
    public TextMateSyntaxHighlighter(ThemeName darkTheme = ThemeName.DarkPlus, ThemeName lightTheme = ThemeName.LightPlus)
    {
        DarkTheme = darkTheme;
        LightTheme = lightTheme;
        _dark = new Lazy<ThemeContext>(() => new ThemeContext(SharedOptions.Value, darkTheme, HighlightThemeKind.Dark),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _light = new Lazy<ThemeContext>(() => new ThemeContext(SharedOptions.Value, lightTheme, HighlightThemeKind.Light),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>暗色主题。</summary>
    public ThemeName DarkTheme { get; }

    /// <summary>亮色主题。</summary>
    public ThemeName LightTheme { get; }

    /// <inheritdoc />
    public bool SupportsLanguage(string? language) => ResolveScope(language) is not null;

    /// <inheritdoc />
    /// <remarks>不认识的语言返回一个不着色的会话（每行空片段）。</remarks>
    public IHighlightSession CreateSession(string language, HighlightThemeKind theme)
    {
        string? scope = ResolveScope(language);
        return scope is null
            ? new TextMateHighlightSession(null, null)
            : new TextMateHighlightSession(GetContext(theme), scope);
    }

    /// <inheritdoc />
    public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme)
    {
        ThemeContext context = GetContext(theme);
        return (context.DefaultForeground, context.DefaultBackground);
    }

    /// <summary>
    /// 在后台为明暗两个主题预加载这些语言的语法（并编译首行规则），之后创建的会话首次着色不再卡顿。
    /// 未知语言会被忽略。
    /// </summary>
    public void Prewarm(params string[] languages)
    {
        _ = PrewarmAsync(HighlightThemeKind.Dark, languages);
        _ = PrewarmAsync(HighlightThemeKind.Light, languages);
    }

    /// <summary>在后台为指定主题预加载语法；返回的任务在全部加载完成后结束（不会失败）。</summary>
    public Task PrewarmAsync(HighlightThemeKind theme, params IEnumerable<string> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        string[] scopes = languages.Select(ResolveScope).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (scopes.Length == 0)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            ThemeContext context = GetContext(theme);
            foreach (string scope in scopes)
            {
                // 每个语法单独加锁，避免长时间阻塞同主题的其他会话
                lock (context.Gate)
                {
                    try
                    {
                        context.GetGrammarLocked(scope)?.TokenizeLine2("", null, TextMateHighlightSession.LineTimeLimit);
                    }
                    catch (Exception)
                    {
                        // 预热失败不影响正常使用：会话首次着色时会再次尝试
                    }
                }
            }
        });
    }

    /// <summary>把围栏语言名解析为 TextMate scope（如 "source.cs"）；不认识返回 null。</summary>
    internal string? ResolveScope(string? language)
    {
        string? name = LanguageAliases.Normalize(language);
        if (name is null)
        {
            return null;
        }

        if (_scopeCache.TryGetValue(name, out string? cached))
        {
            return cached;
        }

        string? scope = ResolveScopeCore(name);
        _scopeCache.TryAdd(name, scope);
        return scope;
    }

    private static string? ResolveScopeCore(string name)
    {
        if (LanguageAliases.IsPlain(name))
        {
            return null;
        }

        RegistryOptions options = SharedOptions.Value;
        try
        {
            if (LanguageAliases.TryGetLanguageId(name, out string languageId))
            {
                return options.GetScopeByLanguageId(languageId);
            }

            return options.GetScopeByLanguageId(name) ?? options.GetScopeByExtension("." + name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ThemeContext GetContext(HighlightThemeKind theme) =>
        theme == HighlightThemeKind.Light ? _light.Value : _dark.Value;
}

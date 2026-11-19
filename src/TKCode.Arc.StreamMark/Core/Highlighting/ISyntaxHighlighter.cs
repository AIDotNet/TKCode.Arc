using System.Collections.Immutable;

namespace StreamMark.Core.Highlighting;

[Flags]
public enum HighlightFontStyle : byte
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
}

/// <summary>
/// 一行中的一个着色片段。<see cref="Start"/> 相对行首；<see cref="Foreground"/> 为 0xAARRGGBB，0 表示使用默认前景色。
/// </summary>
public readonly record struct HighlightToken(int Start, int Length, uint Foreground, HighlightFontStyle Style = HighlightFontStyle.None);

/// <summary>代码高亮的明暗主题。</summary>
public enum HighlightThemeKind : byte
{
    Dark,
    Light,
}

/// <summary>
/// 代码高亮器。实现必须线程安全地创建会话；单个会话只在一个线程上顺序使用。
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>是否认识这个语言名（Markdown 围栏信息串的第一个词，如 "csharp"、"ts"、"py"）。</summary>
    bool SupportsLanguage(string? language);

    /// <summary>为一个代码块创建增量着色会话。</summary>
    IHighlightSession CreateSession(string language, HighlightThemeKind theme);

    /// <summary>主题的默认前景色和背景色（0xAARRGGBB），用于代码块底色与未着色文本。</summary>
    (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme);
}

/// <summary>
/// 一个代码块的着色会话。流式中代码只在末尾增长：会话应缓存每个完整行结束时的语法状态，
/// 只为新增的完整行和最后一个未完成的行重新着色。
/// </summary>
public interface IHighlightSession : IDisposable
{
    /// <summary>
    /// 用完整的代码文本更新，返回每一行（按 '\n' 切分）的着色片段。
    /// 若新文本不是旧文本的追加（例如被替换），会话应从变化处重新着色。
    /// </summary>
    IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code);
}

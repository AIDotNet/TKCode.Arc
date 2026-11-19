// Ported from vercel/streamdown packages/remend (src/patterns.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.
//
// JS → .NET regex notes: a JS `$` without the m flag means end of input, so it
// becomes `\z` (.NET `$` also matches before a final newline); JS `\s` is spelled
// out as JsText.WhitespaceClass because .NET `\s` differs (U+0085 / U+FEFF).

using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

/// <summary>
/// 以文本末尾为锚点的模式：分隔符只允许作为文本最后一个字符出现在开启标记之后。
/// </summary>
internal sealed record TrailingPattern(string Name, char Delimiter, int MarkerLength, Regex Regex);

internal static partial class Patterns
{
    private const string Ws = JsText.WhitespaceClass;

    [GeneratedRegex(@"(\*\*)([^*]*\*?)\z")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(__)([^_]*?)\z")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"(\*\*\*)([^*]*?)\z")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"(\*)([^*]*?)\z")]
    private static partial Regex SingleAsteriskRegex();

    [GeneratedRegex(@"(_)([^_]*?)\z")]
    private static partial Regex SingleUnderscoreRegex();

    [GeneratedRegex(@"(~~)([^~]*?)\z")]
    private static partial Regex StrikethroughRegex();

    [GeneratedRegex(@"(__)([^_]+)_\z")]
    private static partial Regex HalfCompleteUnderscoreRegex();

    [GeneratedRegex(@"(~~)([^~]+)~\z")]
    private static partial Regex HalfCompleteTildeRegex();

    [GeneratedRegex("^[" + Ws + "_~*`]*\\z")]
    public static partial Regex WhitespaceOrMarkers();

    [GeneratedRegex("^[" + Ws + "]*[-*+][" + Ws + "]+\\z")]
    public static partial Regex ListItem();

    [GeneratedRegex(@"^\*{4,}\z")]
    public static partial Regex FourOrMoreAsterisks();

    public static readonly TrailingPattern Bold = new("boldPattern", '*', 2, BoldRegex());
    public static readonly TrailingPattern Italic = new("italicPattern", '_', 2, ItalicRegex());
    public static readonly TrailingPattern BoldItalic = new("boldItalicPattern", '*', 3, BoldItalicRegex());
    public static readonly TrailingPattern SingleAsterisk = new("singleAsteriskPattern", '*', 1, SingleAsteriskRegex());
    public static readonly TrailingPattern SingleUnderscore = new("singleUnderscorePattern", '_', 1, SingleUnderscoreRegex());
    public static readonly TrailingPattern Strikethrough = new("strikethroughPattern", '~', 2, StrikethroughRegex());
    public static readonly TrailingPattern HalfCompleteUnderscore = new("halfCompleteUnderscorePattern", '_', 2, HalfCompleteUnderscoreRegex());
    public static readonly TrailingPattern HalfCompleteTilde = new("halfCompleteTildePattern", '~', 2, HalfCompleteTildeRegex());

    /// <summary>
    /// 只在最后一个分隔符之后的尾部切片上匹配。
    /// A match's marker ends at or after the last delimiter before the final
    /// character, or at the final character when there is none, so matching from
    /// there finds the same match without scanning the rest of the document.
    /// </summary>
    /// <returns>成功的匹配；没有匹配时返回 null。分组值与 TS 在切片上匹配的结果一致。</returns>
    public static Match? MatchTrailing(string text, TrailingPattern pattern)
    {
        var last = JsText.LastIndexOf(text, pattern.Delimiter, text.Length - 2);
        var markerEnd = last == -1 ? text.Length - 1 : last;
        var start = Math.Max(0, markerEnd - pattern.MarkerLength + 1);
        // None of the patterns has a start anchor or lookbehind, so matching from
        // `start` sees exactly what matching the slice would
        var match = pattern.Regex.Match(text, Math.Min(start, text.Length));
        return match.Success ? match : null;
    }
}

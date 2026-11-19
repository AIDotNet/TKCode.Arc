// Ported from vercel/streamdown packages/remend, Copyright 2023 Vercel, Inc., Apache-2.0; modified.
// (JavaScript string semantics the TypeScript sources rely on.)

namespace StreamMark.Core.Remend;

/// <summary>
/// 以 C# 复刻 TypeScript 源码所依赖的 JavaScript 字符串语义：越界下标返回 undefined、
/// <c>lastIndexOf</c> 的 fromIndex 钳制、<c>substring</c> 的参数交换以及 ECMAScript 的空白字符集。
/// 下标与 JS 一样按 UTF-16 码元计。
/// </summary>
internal static class JsText
{
    /// <summary>越界读取的哨兵值，对应 JS 中的 <c>undefined</c> / 空字符串。</summary>
    public const int None = -1;

    /// <summary>
    /// ECMAScript <c>\s</c> 与 <c>trim()</c> 共用的字符集（WhiteSpace + LineTerminator），
    /// 用于拼接正则字符类。与 .NET 的 <c>\s</c> 不同：包含 U+FEFF，不包含 U+0085。
    /// </summary>
    public const string WhitespaceClass = @"\t\n\v\f\r \u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

    /// <summary>对应 JS 的 <c>text[i]</c>：越界时返回 <see cref="None"/>。</summary>
    public static int At(string text, int index) => (uint)index < (uint)text.Length ? text[index] : None;

    /// <summary>ECMAScript 空白字符（含行终止符）。</summary>
    public static bool IsWhitespace(int c) => c switch
    {
        '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00A0' or '\u1680' or '\u2028' or '\u2029'
            or '\u202F' or '\u205F' or '\u3000' or '\uFEFF' => true,
        >= '\u2000' and <= '\u200A' => true,
        _ => false,
    };

    /// <summary>JS <c>String.prototype.trim</c>。</summary>
    public static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsWhitespace(text[start]))
        {
            start++;
        }
        while (end > start && IsWhitespace(text[end - 1]))
        {
            end--;
        }
        return start == 0 && end == text.Length ? text : text.Substring(start, end - start);
    }

    /// <summary>JS <c>String.prototype.trimEnd</c>。</summary>
    public static string TrimEnd(string text)
    {
        var end = text.Length;
        while (end > 0 && IsWhitespace(text[end - 1]))
        {
            end--;
        }
        return end == text.Length ? text : text[..end];
    }

    /// <summary>JS <c>indexOf(char, from)</c>。</summary>
    public static int IndexOf(string text, char value, int from = 0)
    {
        if (from < 0)
        {
            from = 0;
        }
        return from >= text.Length ? -1 : text.IndexOf(value, from);
    }

    /// <summary>JS <c>indexOf(string, from)</c>（序数比较）。</summary>
    public static int IndexOf(string text, string value, int from = 0)
    {
        if (from < 0)
        {
            from = 0;
        }
        return from > text.Length ? -1 : text.IndexOf(value, from, StringComparison.Ordinal);
    }

    /// <summary>JS <c>lastIndexOf(string)</c>（序数比较）。</summary>
    public static int LastIndexOf(string text, string value) => text.LastIndexOf(value, StringComparison.Ordinal);

    /// <summary>
    /// JS <c>lastIndexOf(char, fromIndex)</c>：匹配起点不超过 fromIndex，负数 fromIndex 钳制为 0。
    /// </summary>
    public static int LastIndexOf(string text, char value, int fromIndex)
    {
        if (text.Length == 0)
        {
            return -1;
        }
        return text.LastIndexOf(value, Math.Clamp(fromIndex, 0, text.Length - 1));
    }

    /// <summary>JS <c>substring(start, end)</c>：参数钳制到 [0, length]，start &gt; end 时交换。</summary>
    public static string Substring(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        if (start > end)
        {
            (start, end) = (end, start);
        }
        return start == 0 && end == text.Length ? text : text.Substring(start, end - start);
    }

    /// <summary>JS <c>substring(start)</c>。</summary>
    public static string Substring(string text, int start) => Substring(text, start, text.Length);

    /// <summary>JS <c>endsWith</c> 的单字符形式。</summary>
    public static bool EndsWith(string text, char value) => text.Length > 0 && text[^1] == value;

    /// <summary>JS <c>endsWith</c>（序数比较）。</summary>
    public static bool EndsWith(string text, string value) => text.EndsWith(value, StringComparison.Ordinal);
}

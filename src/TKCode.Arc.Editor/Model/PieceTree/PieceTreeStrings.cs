// Ported from vscode's src/vs/base/common/strings.ts and charCode.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Only the helpers the piece tree needs are ported.

using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Model;

internal static class PieceTreeStrings
{
    internal const char LineFeed = '\n';
    internal const char CarriageReturn = '\r';

    /// <summary>The UTF-8 byte order mark as a single UTF-16 character.</summary>
    internal const char Utf8BomCharacter = '﻿';

    private static readonly Regex s_splitLines = new(@"\r\n|\r|\n", RegexOptions.Compiled);
    private static readonly Regex s_replaceLines = new(@"\r\n|\r|\n", RegexOptions.Compiled);
    private static readonly Regex s_unusualLineTerminators = new(@"[  ]", RegexOptions.Compiled);

    // Generated using https://github.com/alexdima/unicode-utils/blob/main/rtl-test.js
    private static readonly Regex s_containsRtl = new(
        @"(?:[־׀׃׆א-״؈؋؍؛-ي٭-ٯٱ-ەۥۦۮۯۺ-ܐܒ-ܯݍ-ޥޱ-ߪߴߵߺ߾-ࠕࠚࠤࠨ࠰-ࡘ࡞-ࢎࢠ-ࣉ‏יִײַ-ﬨשׁ-ﴽﵐ-ﷇﷰ-﷼ﹰ-ﻼ]|\uD802[\uDC00-\uDD1B\uDD20-\uDE00\uDE10-\uDE35\uDE40-\uDEE4\uDEEB-\uDF35\uDF40-\uDFFF]|\uD803[\uDC00-\uDD23\uDE80-\uDEA9\uDEAD-\uDF45\uDF51-\uDF81\uDF86-\uDFF6]|\uD83A[\uDC00-\uDCCF\uDD00-\uDD43\uDD4B-\uDFFF]|\uD83B[\uDC00-\uDEBB])",
        RegexOptions.Compiled);

    internal static bool StartsWithUtf8Bom(string value) =>
        value.Length > 0 && value[0] == Utf8BomCharacter;

    internal static bool ContainsRtl(string value) => s_containsRtl.IsMatch(value);

    internal static bool ContainsUnusualLineTerminators(string value) => s_unusualLineTerminators.IsMatch(value);

    internal static string[] SplitLines(string value) => s_splitLines.Split(value);

    internal static string ReplaceLineBreaks(string value, string eol) => s_replaceLines.Replace(value, eol);

    /// <summary>Removes one trailing <c>\r\n</c>, <c>\r</c> or <c>\n</c>, if present.</summary>
    internal static string TrimTrailingLineBreak(string value)
    {
        var trimmed = TrimTrailingLineBreak(value.AsSpan());
        return trimmed.Length == value.Length ? value : value.Substring(0, trimmed.Length);
    }

    /// <summary>Removes one trailing <c>\r\n</c>, <c>\r</c> or <c>\n</c>, if present.</summary>
    internal static ReadOnlySpan<char> TrimTrailingLineBreak(ReadOnlySpan<char> value)
    {
        var length = value.Length;
        if (length == 0)
        {
            return value;
        }

        var last = value[length - 1];
        if (last == LineFeed)
        {
            return value.Slice(0, length >= 2 && value[length - 2] == CarriageReturn ? length - 2 : length - 1);
        }

        return last == CarriageReturn ? value.Slice(0, length - 1) : value;
    }
}

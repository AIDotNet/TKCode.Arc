using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Text;

namespace StreamMark.Core.Parsing;

/// <summary>
/// CommonMark 0.31.2 兼容的 HTML 命名实体与数值字符引用解码器。
/// 常用实体走零分配快路径，其余使用静态只读表 + <see cref="WebUtility.HtmlDecode(string)"/> 回退；NativeAOT 零反射。
/// </summary>
internal static class HtmlEntityTable
{
    private static readonly FrozenDictionary<string, string> s_extraEntities = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["apos"] = "'",
        ["Tab"] = "\t",
        ["NewLine"] = "\n",
        ["excl"] = "!",
        ["quot"] = "\"",
        ["QUOT"] = "\"",
        ["num"] = "#",
        ["dollar"] = "$",
        ["percnt"] = "%",
        ["amp"] = "&",
        ["AMP"] = "&",
        ["lpar"] = "(",
        ["rpar"] = ")",
        ["ast"] = "*",
        ["midast"] = "*",
        ["plus"] = "+",
        ["comma"] = ",",
        ["period"] = ".",
        ["sol"] = "/",
        ["colon"] = ":",
        ["semi"] = ";",
        ["lt"] = "<",
        ["LT"] = "<",
        ["equals"] = "=",
        ["gt"] = ">",
        ["GT"] = ">",
        ["quest"] = "?",
        ["commat"] = "@",
        ["lsqb"] = "[",
        ["lbrack"] = "[",
        ["bsol"] = "\\",
        ["rsqb"] = "]",
        ["rbrack"] = "]",
        ["Hat"] = "^",
        ["lowbar"] = "_",
        ["grave"] = "`",
        ["lcub"] = "{",
        ["lbrace"] = "{",
        ["verbar"] = "|",
        ["vert"] = "|",
        ["VerticalLine"] = "|",
        ["rcub"] = "}",
        ["rbrace"] = "}",
        ["tilde"] = "~",
        ["nbsp"] = "\u00A0",
        ["NonBreakingSpace"] = "\u00A0",
        ["ensp"] = "\u2002",
        ["emsp"] = "\u2003",
        ["thinsp"] = "\u2009",
        ["ZeroWidthSpace"] = "\u200B",
        ["NegativeThinSpace"] = "\u200B",
        ["ndash"] = "\u2013",
        ["mdash"] = "\u2014",
        ["lsquo"] = "\u2018",
        ["rsquo"] = "\u2019",
        ["ldquo"] = "\u201C",
        ["rdquo"] = "\u201D",
        ["bull"] = "\u2022",
        ["hellip"] = "\u2026",
        ["copy"] = "\u00A9",
        ["COPY"] = "\u00A9",
        ["reg"] = "\u00AE",
        ["REG"] = "\u00AE",
        ["trade"] = "\u2122",
        ["TRADE"] = "\u2122",
        ["check"] = "\u2713",
        ["checkmark"] = "\u2713",
        ["cross"] = "\u2717",
        ["star"] = "\u2606",
        ["starf"] = "\u2605",
        ["bigstar"] = "\u2605",
        ["NotEqual"] = "\u2260",
        ["GreaterEqual"] = "\u2265",
        ["LessEqual"] = "\u2264",
        ["PlusMinus"] = "\u00B1",
        ["pm"] = "\u00B1",
        ["mp"] = "\u2213",
        ["RightArrow"] = "\u2192",
        ["LeftArrow"] = "\u2190",
        ["UpArrow"] = "\u2191",
        ["DownArrow"] = "\u2193",
        ["DoubleRightArrow"] = "\u21D2",
        ["DoubleLeftArrow"] = "\u21D0",
        ["Longleftrightarrow"] = "\u27FA",
        ["longrightarrow"] = "\u27F6",
        ["longleftarrow"] = "\u27F5",
        ["mapsto"] = "\u21A6",
        ["cdot"] = "\u00B7",
        ["centerdot"] = "\u00B7",
        ["times"] = "\u00D7",
        ["divide"] = "\u00F7",
        ["div"] = "\u00F7",
        ["approx"] = "\u2248",
        ["neq"] = "\u2260",
        ["leq"] = "\u2264",
        ["geq"] = "\u2265",
        ["infty"] = "\u221E",
        ["subset"] = "\u2282",
        ["supset"] = "\u2283",
        ["subseteq"] = "\u2286",
        ["supseteq"] = "\u2287",
        ["notin"] = "\u2209",
        ["emptyset"] = "\u2205",
        ["therefore"] = "\u2234",
        ["because"] = "\u2235",
        ["deg"] = "\u00B0",
        ["micro"] = "\u00B5",
        ["para"] = "\u00B6",
        ["sect"] = "\u00A7",
        ["laquo"] = "\u00AB",
        ["raquo"] = "\u00BB",
        ["euro"] = "\u20AC",
        ["pound"] = "\u00A3",
        ["yen"] = "\u00A5",
        ["cent"] = "\u00A2",
        ["frac12"] = "\u00BD",
        ["frac14"] = "\u00BC",
        ["frac34"] = "\u00BE",
        ["AElig"] = "\u00C6",
        ["aelig"] = "\u00E6",
        ["Dcaron"] = "\u010E",
        ["ouml"] = "\u00F6",
        ["auml"] = "\u00E4",
        ["uuml"] = "\u00FC",
        ["Ouml"] = "\u00D6",
        ["Auml"] = "\u00C4",
        ["Uuml"] = "\u00DC",
        ["szlig"] = "\u00DF",
        ["ccedil"] = "\u00E7",
        ["eacute"] = "\u00E9",
        ["egrave"] = "\u00E8",
        ["ntilde"] = "\u00F1",
        ["HilbertSpace"] = "\u210B",
        ["DifferentialD"] = "\u2146",
        ["ClockwiseContourIntegral"] = "\u2232",
        ["ngE"] = "\u2267\u0338",
        ["ThickSpace"] = "\u205F\u200A",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// 尝试从 <paramref name="slice"/> 开头（必须以 '&amp;' 开始）解码一个合法的 CommonMark HTML 实体或数值字符引用。
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<char> slice, out string decoded, out int consumed)
    {
        decoded = string.Empty;
        consumed = 0;
        if (slice.Length < 4 || slice[0] != '&')
        {
            return false;
        }
        var semi = slice[1..Math.Min(slice.Length, 36)].IndexOf(';');
        if (semi <= 0)
        {
            return false;
        }
        var body = slice.Slice(1, semi);
        consumed = semi + 2;

        if (body[0] == '#')
        {
            return TryDecodeNumeric(body[1..], out decoded);
        }

        // 命名实体：CommonMark 要求 &[A-Za-z][A-Za-z0-9]{1,31};
        if (body.Length < 2 || !char.IsAsciiLetter(body[0]))
        {
            consumed = 0;
            return false;
        }
        for (var i = 1; i < body.Length; i++)
        {
            if (!char.IsAsciiLetterOrDigit(body[i]))
            {
                consumed = 0;
                return false;
            }
        }

        // 最高频实体零查找快路径
        if (body.Length <= 4)
        {
            if (body.SequenceEqual("amp"))
            {
                decoded = "&";
                return true;
            }
            if (body.SequenceEqual("lt"))
            {
                decoded = "<";
                return true;
            }
            if (body.SequenceEqual("gt"))
            {
                decoded = ">";
                return true;
            }
            if (body.SequenceEqual("quot"))
            {
                decoded = "\"";
                return true;
            }
            if (body.SequenceEqual("apos"))
            {
                decoded = "'";
                return true;
            }
            if (body.SequenceEqual("nbsp"))
            {
                decoded = "\u00A0";
                return true;
            }
        }

#if NET9_0_OR_GREATER
        if (s_extraEntities.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(body, out var found))
        {
            decoded = found;
            return true;
        }
#else
        if (s_extraEntities.TryGetValue(body.ToString(), out var found))
        {
            decoded = found;
            return true;
        }
#endif

        // 回退到 BCL 内置 HTML 命名实体表（零反射、AOT 安全）
        var rawEntity = slice[..consumed].ToString();
        var webDecoded = WebUtility.HtmlDecode(rawEntity);
        if (!string.Equals(webDecoded, rawEntity, StringComparison.Ordinal))
        {
            decoded = webDecoded;
            return true;
        }

        consumed = 0;
        return false;
    }

    private static bool TryDecodeNumeric(ReadOnlySpan<char> digits, out string decoded)
    {
        decoded = string.Empty;
        if (digits.IsEmpty)
        {
            return false;
        }
        int codePoint;
        if (digits[0] is 'x' or 'X')
        {
            var hex = digits[1..];
            // CommonMark 0.31.2: 1..6 hex digits
            if (hex.IsEmpty || hex.Length > 6)
            {
                return false;
            }
            for (var i = 0; i < hex.Length; i++)
            {
                if (!char.IsAsciiHexDigit(hex[i]))
                {
                    return false;
                }
            }
            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codePoint))
            {
                return false;
            }
        }
        else
        {
            // CommonMark 0.31.2: 1..7 decimal digits
            if (digits.Length > 7)
            {
                return false;
            }
            for (var i = 0; i < digits.Length; i++)
            {
                if (!char.IsAsciiDigit(digits[i]))
                {
                    return false;
                }
            }
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out codePoint))
            {
                return false;
            }
        }

        // CommonMark §2.3: 0 或非法/代理码点替换为 U+FFFD
        if (codePoint == 0 || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
        {
            decoded = "\uFFFD";
            return true;
        }
        decoded = codePoint <= 0xFFFF ? ((char)codePoint).ToString() : char.ConvertFromUtf32(codePoint);
        return true;
    }
}

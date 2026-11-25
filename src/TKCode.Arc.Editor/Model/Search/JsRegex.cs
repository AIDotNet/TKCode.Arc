// Adaptation of vscode (src/vs/base/common/strings.ts: createRegExp, escapeRegExpCharacters, commit b57d5c9e),
// MIT license. Maps the JavaScript regular expressions the find box produces (flags g, u, optionally i and m)
// onto System.Text.RegularExpressions.
//
// The mapping:
//  - RegexOptions.ECMAScript makes \d, \D, \w, \W, \b and \B ASCII-based like JavaScript, including inside
//    character classes; it cannot be combined with Singleline, IgnorePatternWhitespace or NonBacktracking.
//  - \s and \S are rewritten to JavaScript's whitespace set (ASCII whitespace plus the Unicode Zs characters,
//    U+FEFF, U+2028 and U+2029); .NET's ECMAScript \s is ASCII-only and .NET's normal \s is Unicode-based but
//    includes U+0085 and excludes U+FEFF, so neither matches.
//  - . becomes an indivisible code point that is not a line terminator (JavaScript's u flag makes . match a
//    surrogate pair as one character; .NET matches one UTF-16 code unit). That costs an alternation per character,
//    so a second expression with a plain class for . is kept for text that is known to be basic ASCII, where a code
//    unit is a code point (GetRegex).
//  - $ without the multiline flag becomes \z, because .NET's $ also matches before a final \n.
//  - \u{...} code point escapes (u flag) become \uXXXX, or a surrogate pair wrapped in an atomic group.
//  - [] (never matches) and [^] (any code point) are rewritten because .NET rejects both.
// Notably not handled: JavaScript's u-flag syntax errors for identity escapes and malformed quantifiers (so a
// pattern JavaScript rejects can still match here), astral code points inside a character class, \p{Script=...}
// long property names (except \p{XID_Start} / \p{XID_Continue}, which become general categories), the order of captures when one pattern mixes named and unnamed groups (.NET numbers the
// unnamed groups first), and JavaScript's simple case folding versus .NET's IgnoreCase. A pathological user pattern
// is bounded by <see cref="MatchTimeout"/>; when it expires the search stops and reports no further matches.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Model;

/// <summary>A JavaScript regular expression (global, unicode, optionally ignore-case and multiline) backed by .NET.</summary>
internal sealed class JsRegex
{
    /// <summary>How long one match may take before the search gives up.</summary>
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    // JavaScript's \s: <TAB> <LF> <VT> <FF> <CR> <SP> <NBSP> and the other Unicode space separators, U+FEFF, LS, PS.
    private const string JsWhitespaceRanges =
        "\\t\\n\\v\\f\\r \\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000\\uFEFF";

    // The complement of JsWhitespaceRanges over the BMP, as ranges that can be used inside another class.
    private const string JsNonWhitespaceRanges =
        "\\u0000-\\u0008\\u000E-\\u001F\\u0021-\\u009F\\u00A1-\\u167F\\u1681-\\u1FFF" +
        "\\u200B-\\u2027\\u202A-\\u202E\\u2030-\\u205E\\u2060-\\u2FFF\\u3001-\\uFEFE\\uFF00-\\uFFFF";

    // A code point that is not a line terminator: a surrogate pair, or one non-pair code unit. The atomic group
    // keeps the alternation from splitting a pair on backtracking, as the u flag's . is indivisible.
    private const string Dot =
        "(?>[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]|[^\\n\\r\\u2028\\u2029])";

    // One UTF-16 code unit that is not a line terminator: what Dot matches in text without surrogate pairs.
    private const string DotCodeUnit = "[^\\n\\r\\u2028\\u2029]";

    // Any code point, line terminators included (JavaScript's [^] under the u flag).
    private const string AnyCodePoint =
        "(?>[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]|[\\s\\S])";

    // JavaScript's empty character class matches nothing; (?!) is the .NET equivalent.
    private const string NeverMatch = "(?!)";

    private readonly Regex _basicAsciiRegex;

    private JsRegex(string source, bool matchCase, bool multiline, Regex regex, Regex basicAsciiRegex)
    {
        Source = source;
        MatchCase = matchCase;
        Multiline = multiline;
        Regex = regex;
        _basicAsciiRegex = basicAsciiRegex;
    }

    /// <summary>The JavaScript source after <c>escapeRegExpCharacters</c>, before the .NET translation.</summary>
    internal string Source { get; }

    /// <summary>Whether the <c>i</c> flag was requested.</summary>
    internal bool MatchCase { get; }

    /// <summary>Whether the <c>m</c> flag was requested.</summary>
    internal bool Multiline { get; }

    /// <summary>The .NET regular expression. It is right for any text.</summary>
    internal Regex Regex { get; }

    /// <summary>
    /// The expression to search a text with. For text that is known to be basic ASCII (a buffer whose
    /// <c>MightContainNonBasicASCII</c> is false) it is one that finds the same matches faster; pass true when unsure.
    /// </summary>
    internal Regex GetRegex(bool mightContainNonBasicAscii) => mightContainNonBasicAscii ? Regex : _basicAsciiRegex;

    /// <summary>
    /// Builds the regular expression the way <c>strings.createRegExp</c> does. Throws <see cref="ArgumentException"/>
    /// when the pattern is empty or invalid; <see cref="SearchParams.ParseSearchRequest"/> turns that into null.
    /// </summary>
    internal static JsRegex Create(string searchString, bool isRegex, bool matchCase, bool multiline)
    {
        if (string.IsNullOrEmpty(searchString))
        {
            throw new ArgumentException("Cannot create regex from empty string", nameof(searchString));
        }

        var source = isRegex ? searchString : SearchHelpers.EscapeRegExpCharacters(searchString);
        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;
        if (!matchCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (multiline)
        {
            options |= RegexOptions.Multiline;
        }

        var regex = new Regex(Translate(source, multiline, Dot, out var usesDot), options, MatchTimeout);
        var basicAsciiRegex = usesDot
            ? new Regex(Translate(source, multiline, DotCodeUnit, out _), options, MatchTimeout)
            : regex;
        return new JsRegex(source, matchCase, multiline, regex, basicAsciiRegex);
    }

    /// <summary>
    /// Translates the JavaScript source of a Monarch rule (compiled by monarchCompile without the g flag, with u only when
    /// the language sets unicode) into a .NET pattern for <see cref="RegexOptions.ECMAScript"/>. Without u, <c>.</c> and
    /// <c>[^]</c> match one UTF-16 code unit and <c>\u{</c> is not a code point escape, as in JavaScript.
    /// </summary>
    internal static string TranslateMonarch(string pattern, bool unicode) =>
        Translate(pattern, multiline: false, unicode ? Dot : DotCodeUnit, out _, unicode);

    /// <summary>
    /// A .NET expression for a JavaScript regular expression literal (<c>/source/flags</c>) of a language definition:
    /// <c>i</c>, <c>m</c> and <c>u</c> are honoured, <c>g</c>, <c>s</c> and <c>y</c> are ignored (the editor never keeps
    /// a lastIndex in them).
    /// </summary>
    internal static Regex FromLiteral(string source, string flags = "")
    {
        var unicode = flags.Contains('u');
        var multiline = flags.Contains('m');
        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant;
        if (flags.Contains('i'))
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (multiline)
        {
            options |= RegexOptions.Multiline;
        }

        return new Regex(Translate(source, multiline, unicode ? Dot : DotCodeUnit, out _, unicode), options, MatchTimeout);
    }

    private static string Translate(string pattern, bool multiline, string dot, out bool usesDot, bool unicode = true)
    {
        var builder = new StringBuilder(pattern.Length + 16);
        var inClass = false;
        usesDot = false;

        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];

            if (ch == '\\')
            {
                if (i + 1 >= pattern.Length)
                {
                    builder.Append('\\');
                    continue;
                }

                var next = pattern[i + 1];
                if (!inClass)
                {
                    switch (next)
                    {
                        case 's':
                            builder.Append('[').Append(JsWhitespaceRanges).Append(']');
                            i++;
                            continue;
                        case 'S':
                            builder.Append("[^").Append(JsWhitespaceRanges).Append(']');
                            i++;
                            continue;
                        case 'u' when unicode && i + 2 < pattern.Length && pattern[i + 2] == '{':
                            i = AppendCodePointEscape(builder, pattern, i, inClass: false);
                            continue;
                        case 'p' or 'P' when TryMapProperty(pattern, i, out var outsideMembers, out var outsideEnd):
                            builder.Append(next == 'p' ? "[" : "[^").Append(outsideMembers).Append(']');
                            i = outsideEnd;
                            continue;
                        default:
                            builder.Append('\\').Append(next);
                            i++;
                            continue;
                    }
                }

                switch (next)
                {
                    case 's':
                        builder.Append(JsWhitespaceRanges);
                        i++;
                        continue;
                    case 'S':
                        builder.Append(JsNonWhitespaceRanges);
                        i++;
                        continue;
                    case 'u' when unicode && i + 2 < pattern.Length && pattern[i + 2] == '{':
                        i = AppendCodePointEscape(builder, pattern, i, inClass: true);
                        continue;
                    case 'p' when TryMapProperty(pattern, i, out var insideMembers, out var insideEnd):
                        builder.Append(insideMembers);
                        i = insideEnd;
                        continue;
                    default:
                        builder.Append('\\').Append(next);
                        i++;
                        continue;
                }
            }

            if (ch == '[' && !inClass)
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                {
                    builder.Append(NeverMatch);
                    i++;
                    continue;
                }

                if (i + 2 < pattern.Length && pattern[i + 1] == '^' && pattern[i + 2] == ']')
                {
                    builder.Append(unicode ? AnyCodePoint : "[\\s\\S]");
                    i += 2;
                    continue;
                }

                inClass = true;
                builder.Append(ch);
                continue;
            }

            if (ch == ']' && inClass)
            {
                inClass = false;
                builder.Append(ch);
                continue;
            }

            if (!inClass)
            {
                if (ch == '.')
                {
                    builder.Append(dot);
                    usesDot = true;
                    continue;
                }

                if (ch == '$' && !multiline)
                {
                    builder.Append("\\z");
                    continue;
                }
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// JavaScript's identifier properties, which .NET does not know, as the general categories they are defined by
    /// (UAX #31; the few Other_ID_Start / Other_ID_Continue code points are left out). Other \p{...} pass through.
    /// </summary>
    private static bool TryMapProperty(string pattern, int escapeStart, out string members, out int end)
    {
        members = string.Empty;
        end = escapeStart;
        if (escapeStart + 2 >= pattern.Length || pattern[escapeStart + 2] != '{')
        {
            return false;
        }

        var close = pattern.IndexOf('}', escapeStart + 3);
        if (close < 0)
        {
            return false;
        }

        switch (pattern.Substring(escapeStart + 3, close - escapeStart - 3))
        {
            case "XID_Start" or "ID_Start":
                members = "\\p{L}\\p{Nl}";
                break;
            case "XID_Continue" or "ID_Continue":
                members = "\\p{L}\\p{Nl}\\p{Mn}\\p{Mc}\\p{Nd}\\p{Pc}";
                break;
            default:
                return false;
        }

        end = close;
        return true;
    }

    /// <summary>Rewrites <c>\u{...}</c>; returns the index of the closing brace.</summary>
    private static int AppendCodePointEscape(StringBuilder builder, string pattern, int escapeStart, bool inClass)
    {
        var close = pattern.IndexOf('}', escapeStart + 2);
        if (close < 0)
        {
            throw new ArgumentException("Unterminated code point escape", nameof(pattern));
        }

        var digits = pattern.Substring(escapeStart + 3, close - escapeStart - 3);
        if (digits.Length == 0
            || !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codePoint)
            || codePoint > 0x10FFFF)
        {
            throw new ArgumentException("Invalid code point escape", nameof(pattern));
        }

        if (codePoint <= 0xFFFF)
        {
            builder.Append("\\u").Append(codePoint.ToString("X4", CultureInfo.InvariantCulture));
            return close;
        }

        var pair = char.ConvertFromUtf32(codePoint);
        var high = ((int)pair[0]).ToString("X4", CultureInfo.InvariantCulture);
        var low = ((int)pair[1]).ToString("X4", CultureInfo.InvariantCulture);
        if (inClass)
        {
            // .NET cannot put a surrogate pair into one class member; this matches either half. See the file header.
            builder.Append("\\u").Append(high).Append("\\u").Append(low);
        }
        else
        {
            builder.Append("(?>\\u").Append(high).Append("\\u").Append(low).Append(')');
        }

        return close;
    }
}

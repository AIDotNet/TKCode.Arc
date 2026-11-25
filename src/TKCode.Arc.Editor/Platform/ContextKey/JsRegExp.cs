// Adaptation of the parts of JavaScript's RegExp that vscode's `=~` context key expressions use (the constructor,
// source, flags and test; src/vs/platform/contextkey/common/contextkey.ts, commit b57d5c9e), MIT license.
//
// The pattern is matched by the editor's JavaScript regex translation, Model/Search/JsRegex.cs (read its header for how
// JavaScript semantics map onto System.Text.RegularExpressions). On top of it:
//  - flags: i and m map onto JsRegex's matchCase and multiline. s (dotAll) is done here by rewriting each unescaped `.`
//    outside a character class to `[^]` before the translation, because JsRegex has no dotAll option. g and y only
//    change lastIndex bookkeeping in JavaScript; a JsRegExp keeps no lastIndex, so Test never depends on earlier calls
//    (JavaScript's test with g or y does; upstream's parser removes both flags anyway). u is accepted and recorded, and
//    JsRegex always applies u-flag semantics (`.` matches a whole code point, \u{...} is a code point escape), with or
//    without it, so a pattern written for non-u mode can differ for astral characters and for `\u{...}`.
//  - an empty pattern is JavaScript's `(?:)`, which JsRegex (built for the find box) would reject.
//  - Source is JavaScript's escaped source (EscapeRegExpPattern as V8 does it: unescaped `/` outside a class and line
//    terminators are escaped), so `/a/b/` serializes as it does upstream. Flags is in JavaScript's canonical order.
//  - a pattern that .NET rejects throws ArgumentException where JavaScript throws SyntaxError; the two do not reject
//    exactly the same patterns (see JsRegex's header).
//  - Test converts its argument with String(value) like JavaScript (so an undefined key tests "undefined"), and a match
//    that runs past JsRegex.MatchTimeout counts as no match.

using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Platform;

/// <summary>A JavaScript regular expression, as <c>new RegExp(pattern, flags)</c> makes it.</summary>
internal sealed class JsRegExp
{
    private readonly Regex _regex;

    /// <summary><c>new RegExp(pattern, flags)</c>. Throws <see cref="ArgumentException"/> for an invalid pattern or flags.</summary>
    public JsRegExp(string pattern, string? flags = null)
    {
        bool hasIndices = false, global = false, ignoreCase = false, multiline = false, dotAll = false, unicode = false, sticky = false;
        foreach (var flag in flags ?? "")
        {
            ref var target = ref hasIndices;
            switch (flag)
            {
                case 'd':
                    target = ref hasIndices;
                    break;
                case 'g':
                    target = ref global;
                    break;
                case 'i':
                    target = ref ignoreCase;
                    break;
                case 'm':
                    target = ref multiline;
                    break;
                case 's':
                    target = ref dotAll;
                    break;
                case 'u':
                    target = ref unicode;
                    break;
                case 'y':
                    target = ref sticky;
                    break;
                default:
                    throw new ArgumentException($"Invalid regular expression flags '{flags}'", nameof(flags));
            }
            if (target)
            {
                throw new ArgumentException($"Invalid regular expression flags '{flags}'", nameof(flags));
            }
            target = true;
        }

        IgnoreCase = ignoreCase;
        Multiline = multiline;
        DotAll = dotAll;
        Global = global;
        Sticky = sticky;
        Unicode = unicode;

        var builder = new StringBuilder();
        if (hasIndices)
        {
            builder.Append('d');
        }
        if (global)
        {
            builder.Append('g');
        }
        if (ignoreCase)
        {
            builder.Append('i');
        }
        if (multiline)
        {
            builder.Append('m');
        }
        if (dotAll)
        {
            builder.Append('s');
        }
        if (unicode)
        {
            builder.Append('u');
        }
        if (sticky)
        {
            builder.Append('y');
        }
        Flags = builder.ToString();

        var effective = pattern.Length == 0 ? "(?:)" : pattern;
        Source = EscapeRegExpSource(effective);
        if (dotAll)
        {
            effective = DotToAnyCodePoint(effective);
        }
        _regex = JsRegex.Create(effective, isRegex: true, matchCase: !ignoreCase, multiline: multiline).Regex;
    }

    /// <summary>The .NET expression that matches like the JavaScript one (snippet transforms replace with it).</summary>
    internal Regex Regex => _regex;

    /// <summary>JavaScript's <c>source</c>: the pattern with unescaped slashes and line terminators escaped.</summary>
    public string Source { get; }

    /// <summary>JavaScript's <c>flags</c>, in the canonical order <c>dgimsuy</c>.</summary>
    public string Flags { get; }

    public bool IgnoreCase { get; }

    public bool Multiline { get; }

    public bool DotAll { get; }

    public bool Global { get; }

    public bool Sticky { get; }

    public bool Unicode { get; }

    /// <summary><c>regexp.test(value)</c>, with <paramref name="value"/> converted by <c>String(value)</c>.</summary>
    public bool Test(object? value)
    {
        try
        {
            return _regex.IsMatch(ContextKeyValues.ToJsString(value));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public override string ToString() => "/" + Source + "/" + Flags;

    // EscapeRegExpSource as V8 implements EscapeRegExpPattern.
    private static string EscapeRegExpSource(string pattern)
    {
        var builder = new StringBuilder(pattern.Length + 8);
        var inCharacterClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                if (i + 1 < pattern.Length && IsLineTerminator(pattern[i + 1]))
                {
                    // This '\' is dropped since the next character itself will be escaped.
                    continue;
                }
                builder.Append(c);
                if (i + 1 < pattern.Length)
                {
                    // Escape. Copy the next character verbatim.
                    builder.Append(pattern[++i]);
                }
                continue;
            }
            switch (c)
            {
                case '/' when !inCharacterClass:
                    builder.Append("\\/");
                    continue;
                case '[':
                    inCharacterClass = true;
                    break;
                case ']':
                    inCharacterClass = false;
                    break;
                case '\n':
                    builder.Append("\\n");
                    continue;
                case '\r':
                    builder.Append("\\r");
                    continue;
                case '\u2028':
                    builder.Append("\\u2028");
                    continue;
                case '\u2029':
                    builder.Append("\\u2029");
                    continue;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    private static bool IsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';

    // The s flag: every `.` outside a character class matches any code point, which is `[^]` in JavaScript.
    private static string DotToAnyCodePoint(string pattern)
    {
        var builder = new StringBuilder(pattern.Length + 8);
        var inCharacterClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                builder.Append(c);
                if (i + 1 < pattern.Length)
                {
                    builder.Append(pattern[++i]);
                }
                continue;
            }
            if (c == '[' && !inCharacterClass)
            {
                inCharacterClass = true;
            }
            else if (c == ']' && inCharacterClass)
            {
                inCharacterClass = false;
            }
            else if (c == '.' && !inCharacterClass)
            {
                builder.Append("[^]");
                continue;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }
}

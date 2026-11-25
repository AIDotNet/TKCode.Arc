// Ported from vscode (src/vs/editor/standalone/common/monarch/monarchCommon.ts, commit b57d5c9e), MIT license.
//
// Differences:
// - ILexerMin / ILexer are one class, CompiledLexer; upstream reads the language definition's other keys straight off
//   the lexer object (lexer[attr]), here they are its Attributes.
// - FuzzyAction (a string or an IAction) is CompiledAction, which is one or the other (IsString).
// - A rule's regular expression is a .NET Regex built from the JavaScript source (JsRegex.TranslateMonarch); a rule
//   that uses $Sn builds it per state, as upstream's DynamicRegExp.
// - log writes to the lexer's Log callback (upstream: console.log); none by default.

using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Standalone.Monarch;

/// <summary>Whether a token opens or closes a bracket.</summary>
internal enum MonarchBracket
{
    None = 0,
    Open = 1,
    Close = -1,
}

/// <summary>A bracket pair of the lexer, with the postfixed token class.</summary>
internal sealed record CompiledBracket(string Token, string Open, string Close);

/// <summary>A compiled language definition (upstream's ILexerMin and ILexer).</summary>
internal sealed class CompiledLexer
{
    public required string LanguageId { get; init; }

    public bool IncludeLF { get; init; }

    public bool NoThrow { get; set; }

    public bool IgnoreCase { get; init; }

    public bool Unicode { get; init; }

    public bool UsesEmbedded { get; set; }

    public required string DefaultToken { get; init; }

    public int MaxStack { get; init; } = 100;

    public string? Start { get; set; }

    public required string TokenPostfix { get; init; }

    /// <summary>The state names of the definition (upstream's stateNames, the source tokenizer's keys).</summary>
    public required IReadOnlySet<string> StateNames { get; init; }

    /// <summary>The compiled rules of each state, includes expanded.</summary>
    public Dictionary<string, IReadOnlyList<CompiledRule>> Tokenizer { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<CompiledBracket> Brackets { get; set; } = [];

    /// <summary>The definition's other keys, which <c>@attr</c> refers to.</summary>
    public required MonarchAttributes Attributes { get; init; }

    /// <summary>The compiled regular expressions of the definition by pattern, so that a pattern many rules share is built once.</summary>
    internal Dictionary<string, Regex> RegexCache { get; } = new(StringComparer.Ordinal);

    /// <summary>Where <c>log</c> actions write (upstream: the console); nothing by default.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>A string attribute's value (upstream: typeof lexer[attr] === 'string'); null when it is not one.</summary>
    public string? GetStringAttribute(string name)
    {
        // upstream's lexer also answers its own string fields (languageId, defaultToken, tokenPostfix, start)
        switch (name)
        {
            case "languageId":
                return LanguageId;
            case "defaultToken":
                return DefaultToken;
            case "tokenPostfix":
                return TokenPostfix;
            case "start":
                return Start;
        }

        return Attributes[name] is { Kind: MonarchValueKind.String } value ? value.Text : null;
    }
}

/// <summary>
/// An action after compilation (upstream's FuzzyAction): a token class string, or an object with a group of actions, a
/// test that picks an action (cases), or a token and its attributes.
/// </summary>
internal sealed class CompiledAction
{
    /// <summary>The token class of a string action.</summary>
    public string? Text { get; init; }

    public bool IsString { get; init; }

    /// <summary>One action for each match group.</summary>
    public IReadOnlyList<CompiledAction>? Group { get; init; }

    public bool HasEmbeddedEndInCases { get; init; }

    /// <summary>Picks the action for a match (compiled cases): id, matches, state, end of stream.</summary>
    public Func<string, string[], string, bool, CompiledAction>? Test { get; init; }

    public string? Token { get; init; }

    public bool TokenSubst { get; init; }

    public string? Next { get; init; }

    public string? NextEmbedded { get; init; }

    public MonarchBracket Bracket { get; init; }

    public string? Log { get; init; }

    public string? SwitchTo { get; init; }

    public int? GoBack { get; init; }

    public static CompiledAction FromString(string token) => new() { Text = token, IsString = true };
}

/// <summary>A compiled rule (upstream's IRule and monarchCompile's Rule).</summary>
internal sealed class CompiledRule
{
    private Regex? _regex;
    private Func<string, Regex>? _dynamicRegex;

    public CompiledRule(string name)
    {
        Name = name;
    }

    public CompiledAction Action { get; set; } = new() { Token = string.Empty };

    public bool MatchOnlyAtLineStart { get; set; }

    public string Name { get; set; }

    private Func<(Regex? Regex, Func<string, Regex>? Dynamic)>? _searchFactory;
    private (Regex? Regex, Func<string, Regex>? Dynamic)? _search;

    internal void SetRegex(Regex regex) => _regex = regex;

    internal void SetDynamicRegex(Func<string, Regex> regex) => _dynamicRegex = regex;

    /// <summary>How to build the rule's expression without its ^(?:...) anchor, for searching a line (built on first use).</summary>
    internal void SetSearchRegexFactory(Func<(Regex? Regex, Func<string, Regex>? Dynamic)> factory) => _searchFactory = factory;

    public Regex ResolveRegex(string state) => _regex ?? _dynamicRegex!(state);

    /// <summary>
    /// The rule's expression without the anchor, to find where in a line it first matches (upstream's
    /// _findLeavingNestedLanguageOffset strips the ^(?: and ) from the source and searches with what is left).
    /// </summary>
    public Regex ResolveSearchRegex(string state)
    {
        _search ??= _searchFactory!();
        return _search.Value.Regex ?? _search.Value.Dynamic!(state);
    }
}

/// <summary>Helpers shared by the Monarch compiler and the tokenizer.</summary>
internal static class MonarchCommon
{
    private static readonly Regex s_substituteMatches = new(@"\$((\$)|(#)|(\d\d?)|[sS](\d\d?)|@(\w+))", RegexOptions.CultureInvariant);
    private static readonly Regex s_substituteMatchesRe = new(@"\$[sS](\d\d?)", RegexOptions.CultureInvariant);

    /// <summary>Is a string null or empty?</summary>
    public static bool Empty(string? s) => string.IsNullOrEmpty(s);

    /// <summary>Puts a string to lower case if 'ignoreCase' is set.</summary>
    public static string FixCase(CompiledLexer lexer, string str) =>
        lexer.IgnoreCase && !string.IsNullOrEmpty(str) ? str.ToLowerInvariant() : str;

    /// <summary>Ensures there are no bad characters in a CSS token class.</summary>
    public static string Sanitize(string s)
    {
        if (s.AsSpan().IndexOfAny("&<>'\"_") < 0)
        {
            return s;
        }

        var builder = new StringBuilder(s);
        for (var i = 0; i < builder.Length; i++)
        {
            if (builder[i] is '&' or '<' or '>' or '\'' or '"' or '_')
            {
                builder[i] = '-';
            }
        }

        return builder.ToString();
    }

    /// <summary>Logs a message.</summary>
    public static void Log(CompiledLexer lexer, string msg) => lexer.Log?.Invoke($"{lexer.LanguageId}: {msg}");

    public static MonarchException CreateError(CompiledLexer lexer, string msg) => new($"{lexer.LanguageId}: {msg}");

    /// <summary>
    /// Used on lexer strings; substitutes <c>$$</c> (=&gt; $), <c>$#</c> (=&gt; id), <c>$n</c> (=&gt; matched entry n),
    /// <c>$Sn</c> (=&gt; part n of the state) and <c>@attr</c> (=&gt; contents of lexer[attr]).
    /// </summary>
    public static string SubstituteMatches(CompiledLexer lexer, string str, string id, string[] matches, string state)
    {
        if (str.IndexOf('$') < 0)
        {
            return str;
        }

        string[]? stateMatches = null;
        return s_substituteMatches.Replace(str, m =>
        {
            var dollar = m.Groups[2].Value;
            var hash = m.Groups[3].Value;
            var n = m.Groups[4].Value;
            var s = m.Groups[5].Value;
            var attr = m.Groups[6].Value;
            if (!Empty(dollar))
            {
                return "$"; // $$
            }

            if (!Empty(hash))
            {
                return FixCase(lexer, id); // default $#
            }

            if (!Empty(n) && int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) is var ni && ni < matches.Length)
            {
                return FixCase(lexer, matches[ni]); // $n
            }

            if (!Empty(attr) && lexer.GetStringAttribute(attr) is { } attrValue)
            {
                return attrValue; // @attribute
            }

            stateMatches ??= SplitState(state); // split state on demand
            if (!Empty(s) && int.Parse(s, System.Globalization.CultureInfo.InvariantCulture) is var si && si < stateMatches.Length)
            {
                return FixCase(lexer, stateMatches[si]); // $Sn
            }

            return string.Empty;
        });
    }

    /// <summary>Used on lexer regex rules; substitutes <c>$Sn</c> (=&gt; n'th part of state, regex-escaped).</summary>
    public static string SubstituteMatchesRe(CompiledLexer lexer, string str, string state)
    {
        string[]? stateMatches = null;
        return s_substituteMatchesRe.Replace(str, m =>
        {
            stateMatches ??= SplitState(state); // split state on demand
            var s = m.Groups[1].Value;
            if (!Empty(s) && int.Parse(s, System.Globalization.CultureInfo.InvariantCulture) is var si && si < stateMatches.Length)
            {
                return SearchHelpers.EscapeRegExpCharacters(FixCase(lexer, stateMatches[si])); // $Sn
            }

            return string.Empty;
        });
    }

    /// <summary>The state split on '.', with the whole state first (upstream: state.split('.'), then unshift(state)).</summary>
    internal static string[] SplitState(string state)
    {
        var parts = state.Split('.');
        var result = new string[parts.Length + 1];
        result[0] = state;
        parts.CopyTo(result, 1);
        return result;
    }

    /// <summary>Find the tokenizer rules for a specific state (i.e. next action).</summary>
    public static IReadOnlyList<CompiledRule>? FindRules(CompiledLexer lexer, string inState)
    {
        string? state = inState;
        while (!string.IsNullOrEmpty(state))
        {
            if (lexer.Tokenizer.TryGetValue(state, out var rules))
            {
                return rules;
            }

            var idx = state.LastIndexOf('.');
            state = idx < 0 ? null : state[..idx];
        }

        return null;
    }

    /// <summary>Is a certain state defined? Used during compilation, before the rules are compiled.</summary>
    public static bool StateExists(CompiledLexer lexer, string inState)
    {
        string? state = inState;
        while (!string.IsNullOrEmpty(state))
        {
            if (lexer.StateNames.Contains(state))
            {
                return true;
            }

            var idx = state.LastIndexOf('.');
            state = idx < 0 ? null : state[..idx];
        }

        return false;
    }
}

/// <summary>An error in a Monarch language definition or while tokenizing with it (upstream: Error).</summary>
internal sealed class MonarchException(string message) : Exception(message);

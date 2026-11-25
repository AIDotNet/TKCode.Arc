// Ported from vscode (src/vs/editor/standalone/common/monarch/monarchTypes.ts, commit b57d5c9e), MIT license.
//
// Upstream's types describe JSON values; these classes hold the same values in typed form.
// Differences:
// - IMonarchLanguage's open-ended keys ([key: string]: any) are the Attributes map: strings, regular expressions
//   (their source; compileRegExp uses only the source), string arrays (keyword lists) and other values (which only an
//   error message can mention).
// - The tokenizer and the cases keep their keys in insertion order, as a JavaScript object does for these keys (a key
//   that looks like an array index would come first in JavaScript; the converter writes keys in JavaScript's order).
// - A regular expression is its JavaScript source text; MonarchRegExp translates it to .NET.
// - A rule is [regex, action], [regex, action, next], { regex, action, name } or { include }. The expanded rule's
//   matchOnlyAtStart / matchOnlyAtLineStart flags are not kept: upstream's setRegex overwrites them in every case.
// - IExpandedMonarchLanguageAction's `group` is not kept: compileAction does not read it (an action object with only
//   a group is an error upstream); an array action is the way to give the groups' actions.
// - MonarchBuilder has short factory methods for code that writes a language down (the generated built-in languages).

namespace TKCode.Arc.Editor.Standalone.Monarch;

/// <summary>A Monarch language definition (upstream's IMonarchLanguage).</summary>
internal sealed class MonarchLanguage
{
    /// <summary>Map from state name to the rules of the state, in order; the first state is the start state by default.</summary>
    public MonarchStates Tokenizer { get; init; } = new();

    /// <summary>Is the language case insensitive?</summary>
    public bool? IgnoreCase { get; init; }

    /// <summary>Is the language unicode-aware? (i.e., <c>/\u{1D306}/</c>)</summary>
    public bool? Unicode { get; init; }

    /// <summary>If no match in the tokenizer assign this token class (default 'source').</summary>
    public string? DefaultToken { get; init; }

    /// <summary>For example <c>[['{','}','delimiter.curly']]</c>.</summary>
    public IReadOnlyList<MonarchLanguageBracket>? Brackets { get; init; }

    /// <summary>Start symbol in the tokenizer (by default the first entry is used).</summary>
    public string? Start { get; init; }

    /// <summary>Attach this to every token class (by default '.' + name).</summary>
    public string? TokenPostfix { get; init; }

    /// <summary>Include line feeds (in the form of a \n character) at the end of lines. Defaults to false.</summary>
    public bool? IncludeLF { get; init; }

    /// <summary>Other keys that can be referred to by the tokenizer (<c>@name</c> in rules and cases).</summary>
    public MonarchAttributes Attributes { get; init; } = new();
}

/// <summary>The states of a tokenizer, in the order they were added.</summary>
internal sealed class MonarchStates : IEnumerable<KeyValuePair<string, IReadOnlyList<MonarchLanguageRule>>>
{
    private readonly List<KeyValuePair<string, IReadOnlyList<MonarchLanguageRule>>> _states = [];
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public int Count => _states.Count;

    public IReadOnlyList<MonarchLanguageRule> this[string name]
    {
        get => _states[_index[name]].Value;
        set
        {
            if (_index.TryGetValue(name, out var i))
            {
                _states[i] = new(name, value);
            }
            else
            {
                _index[name] = _states.Count;
                _states.Add(new(name, value));
            }
        }
    }

    public bool TryGetValue(string name, out IReadOnlyList<MonarchLanguageRule> rules)
    {
        if (_index.TryGetValue(name, out var i))
        {
            rules = _states[i].Value;
            return true;
        }

        rules = [];
        return false;
    }

    public bool ContainsKey(string name) => _index.ContainsKey(name);

    public IEnumerator<KeyValuePair<string, IReadOnlyList<MonarchLanguageRule>>> GetEnumerator() => _states.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>The other keys of a language definition, by name.</summary>
internal sealed class MonarchAttributes : IEnumerable<KeyValuePair<string, MonarchValue>>
{
    private readonly Dictionary<string, MonarchValue> _values = new(StringComparer.Ordinal);

    public MonarchValue? this[string name]
    {
        get => _values.GetValueOrDefault(name);
        set
        {
            if (value is null)
            {
                _values.Remove(name);
            }
            else
            {
                _values[name] = value;
            }
        }
    }

    public IEnumerator<KeyValuePair<string, MonarchValue>> GetEnumerator() => _values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>What kind of value an attribute holds.</summary>
internal enum MonarchValueKind
{
    String,
    RegExp,
    StringArray,
    Other,
}

/// <summary>An attribute value: a string, a regular expression, an array of strings, or something else.</summary>
internal sealed class MonarchValue
{
    private MonarchValue(MonarchValueKind kind, string? text, IReadOnlyList<string>? words)
    {
        Kind = kind;
        Text = text;
        Words = words;
    }

    public MonarchValueKind Kind { get; }

    /// <summary>The string, the source of the regular expression, or a description of another value.</summary>
    public string? Text { get; }

    /// <summary>The strings of an array of strings.</summary>
    public IReadOnlyList<string>? Words { get; }

    public static MonarchValue String(string text) => new(MonarchValueKind.String, text, null);

    /// <summary>A regular expression by its JavaScript source; its flags are not used (as upstream).</summary>
    public static MonarchValue RegExp(string source) => new(MonarchValueKind.RegExp, source, null);

    public static MonarchValue StringArray(IReadOnlyList<string> words) => new(MonarchValueKind.StringArray, null, words);

    /// <summary>A value of another type (a number, a boolean, an object, a mixed array); <paramref name="description"/> is for messages.</summary>
    public static MonarchValue Other(string description) => new(MonarchValueKind.Other, description, null);

    public static implicit operator MonarchValue(string text) => String(text);

    public static implicit operator MonarchValue(string[] words) => StringArray(words);
}

/// <summary>
/// A rule (upstream's IMonarchLanguageRule): <c>[regex, action]</c>, <c>[regex, action, next]</c>,
/// <c>{ regex, action, name }</c> or <c>{ include }</c>.
/// </summary>
internal sealed class MonarchLanguageRule
{
    /// <summary><c>[regex, action]</c>, or <c>[regex, action, next]</c> when <paramref name="next"/> is given.</summary>
    public MonarchLanguageRule(string regex, MonarchAction action, string? next = null)
    {
        Regex = regex;
        Action = action;
        Next = next;
        IsShortWithNext = next is not null;
    }

    private MonarchLanguageRule(string? regex, MonarchAction? action, string? include, string? name)
    {
        Regex = regex;
        Action = action;
        Include = include;
        Name = name;
        IsExpanded = true;
    }

    /// <summary>The JavaScript source of the regular expression that matches tokens.</summary>
    public string? Regex { get; }

    /// <summary>The action to take on match; null in an expanded rule means <c>{ token: '' }</c>.</summary>
    public MonarchAction? Action { get; }

    /// <summary>The third element of <c>[regex, action, next]</c>.</summary>
    public string? Next { get; }

    /// <summary>Whether the rule was written <c>[regex, action, next]</c>.</summary>
    public bool IsShortWithNext { get; }

    /// <summary>Whether the rule was written as an object (<c>{ regex, action }</c> or <c>{ include }</c>).</summary>
    public bool IsExpanded { get; }

    /// <summary>An include rule: include all rules from this state.</summary>
    public string? Include { get; }

    /// <summary>The name of an expanded rule, for messages.</summary>
    public string? Name { get; }

    /// <summary><c>{ include: state }</c>.</summary>
    public static MonarchLanguageRule IncludeRule(string state) => new(null, null, state, null);

    /// <summary><c>{ regex, action, name }</c>.</summary>
    public static MonarchLanguageRule Expanded(string? regex, MonarchAction? action, string? name = null) => new(regex, action, null, name);
}

/// <summary>
/// An action (upstream's IMonarchLanguageAction): a token class string, an array of actions for the match groups, or an
/// object with a token (and next state, bracket, ...) or with cases.
/// </summary>
internal sealed class MonarchAction
{
    /// <summary>The token class of a string action.</summary>
    public string? Text { get; private init; }

    /// <summary>Whether this is a string action (upstream's IShortMonarchLanguageAction).</summary>
    public bool IsString { get; private init; }

    /// <summary>An array of actions, one for each parenthesized match group.</summary>
    public IReadOnlyList<MonarchAction>? Array { get; private init; }

    /// <summary>Map from guard to action.</summary>
    public MonarchCases? Cases { get; init; }

    /// <summary>Token class (ie. css class) (or "@brackets" or "@rematch").</summary>
    public string? Token { get; init; }

    /// <summary>The next state to push, or "@push", "@pop", "@popall".</summary>
    public string? Next { get; init; }

    /// <summary>Switch to this state.</summary>
    public string? SwitchTo { get; init; }

    /// <summary>Go back n characters in the stream.</summary>
    public int? GoBack { get; init; }

    /// <summary>@open or @close.</summary>
    public string? Bracket { get; init; }

    /// <summary>Switch to embedded language (using the mimetype) or get out using "@pop".</summary>
    public string? NextEmbedded { get; init; }

    /// <summary>Log a message to the browser console window (here: to the lexer's log sink).</summary>
    public string? Log { get; init; }

    public static MonarchAction FromString(string token) => new() { Text = token, IsString = true };

    public static MonarchAction FromArray(IReadOnlyList<MonarchAction> actions) => new() { Array = actions };

    /// <summary>A copy of an object action with another next state (<c>[regex, action, next]</c> sets action.next).</summary>
    public MonarchAction WithNext(string? next) => new()
    {
        Text = Text,
        IsString = IsString,
        Array = Array,
        Cases = Cases,
        Token = Token,
        Next = next,
        SwitchTo = SwitchTo,
        GoBack = GoBack,
        Bracket = Bracket,
        NextEmbedded = NextEmbedded,
        Log = Log,
    };

    public static implicit operator MonarchAction(string token) => FromString(token);

    public static implicit operator MonarchAction(MonarchAction[] actions) => FromArray(actions);
}

/// <summary>The cases of an action: guards and their actions, in order.</summary>
internal sealed class MonarchCases : IEnumerable<KeyValuePair<string, MonarchAction>>
{
    private readonly List<KeyValuePair<string, MonarchAction>> _cases = [];

    public int Count => _cases.Count;

    public MonarchAction this[string guard]
    {
        set
        {
            var i = _cases.FindIndex(c => c.Key == guard);
            if (i >= 0)
            {
                _cases[i] = new(guard, value);
            }
            else
            {
                _cases.Add(new(guard, value));
            }
        }
    }

    public void Add(string guard, MonarchAction action) => this[guard] = action;

    public IEnumerator<KeyValuePair<string, MonarchAction>> GetEnumerator() => _cases.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A bracket pair and its token class (upstream's IMonarchLanguageBracket, or the array <c>['{','}','delimiter.curly']</c>).</summary>
internal sealed record MonarchLanguageBracket(string Open, string Close, string Token);

/// <summary>Short factory methods for writing a language definition down in code.</summary>
internal static class MonarchBuilder
{
    /// <summary><c>[regex, action]</c> or <c>[regex, action, next]</c>.</summary>
    public static MonarchLanguageRule R(string regex, MonarchAction action, string? next = null) => new(regex, action, next);

    /// <summary><c>{ include: state }</c>.</summary>
    public static MonarchLanguageRule Include(string state) => MonarchLanguageRule.IncludeRule(state);

    /// <summary><c>{ regex, action, name }</c>.</summary>
    public static MonarchLanguageRule Expanded(string? regex, MonarchAction? action, string? name = null) => MonarchLanguageRule.Expanded(regex, action, name);

    /// <summary>An array of actions, one for each match group.</summary>
    public static MonarchAction Groups(params MonarchAction[] actions) => MonarchAction.FromArray(actions);

    /// <summary>An action object with a token.</summary>
    public static MonarchAction Token(
        string token,
        string? next = null,
        string? switchTo = null,
        int? goBack = null,
        string? bracket = null,
        string? nextEmbedded = null,
        string? log = null) =>
        new() { Token = token, Next = next, SwitchTo = switchTo, GoBack = goBack, Bracket = bracket, NextEmbedded = nextEmbedded, Log = log };

    /// <summary>An action object with cases (guard, action), in order.</summary>
    public static MonarchAction Cases(params (string Guard, MonarchAction Action)[] cases)
    {
        var result = new MonarchCases();
        foreach (var (guard, action) in cases)
        {
            result[guard] = action;
        }

        return new MonarchAction { Cases = result };
    }

    /// <summary>A keyword list written as one space-separated string.</summary>
    public static MonarchValue Words(string words) => MonarchValue.StringArray(words.Split(' '));

    /// <summary>A regular expression attribute by its JavaScript source.</summary>
    public static MonarchValue RegExp(string source) => MonarchValue.RegExp(source);
}

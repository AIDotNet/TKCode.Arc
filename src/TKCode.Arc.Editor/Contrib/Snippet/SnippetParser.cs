// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetParser.ts: TokenType, Token, Scanner and
// SnippetParser, commit b57d5c9e), MIT license.
// Differences:
//  - JavaScript truthiness is spelled out: a token text or `_until` result that is empty counts as missing, as upstream's
//    `if (value = ...)` does;
//  - a transform's regular expression is a JsRegExp; a pattern or flags it rejects make the transform invalid, as
//    upstream's `new RegExp` throwing does (the .NET translation does not reject exactly the patterns JavaScript does,
//    see JsRegex.cs).

using System.Globalization;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>The kinds of snippet tokens (upstream's TokenType).</summary>
internal enum TokenType
{
    Dollar,
    Colon,
    Comma,
    CurlyOpen,
    CurlyClose,
    Backslash,
    Forwardslash,
    Pipe,
    Int,
    VariableName,
    Format,
    Plus,
    Dash,
    QuestionMark,
    EOF,
}

/// <summary>A snippet token: its kind and where it is (upstream's Token).</summary>
internal readonly record struct Token(TokenType Type, int Pos, int Len);

/// <summary>Splits a snippet into tokens (upstream's Scanner).</summary>
internal sealed class Scanner
{
    public string Value { get; private set; } = "";

    public int Pos { get; set; }

    public static bool IsDigitCharacter(int ch) => ch is >= '0' and <= '9';

    public static bool IsVariableCharacter(int ch) => ch == '_' || ch is >= 'a' and <= 'z' || ch is >= 'A' and <= 'Z';

    private static TokenType? StaticType(int ch) => ch switch
    {
        '$' => TokenType.Dollar,
        ':' => TokenType.Colon,
        ',' => TokenType.Comma,
        '{' => TokenType.CurlyOpen,
        '}' => TokenType.CurlyClose,
        '\\' => TokenType.Backslash,
        '/' => TokenType.Forwardslash,
        '|' => TokenType.Pipe,
        '+' => TokenType.Plus,
        '-' => TokenType.Dash,
        '?' => TokenType.QuestionMark,
        _ => null,
    };

    public void Text(string value)
    {
        Value = value;
        Pos = 0;
    }

    public string TokenText(Token token) => Value.Substring(token.Pos, token.Len);

    // charCodeAt: NaN (here -1) past the end
    private int CharCodeAt(int index) => index < Value.Length ? Value[index] : -1;

    public Token Next()
    {
        if (Pos >= Value.Length)
        {
            return new Token(TokenType.EOF, Pos, 0);
        }

        var pos = Pos;
        var len = 0;
        var ch = CharCodeAt(pos);

        // static types
        if (StaticType(ch) is { } type)
        {
            Pos += 1;
            return new Token(type, pos, 1);
        }

        // number
        if (IsDigitCharacter(ch))
        {
            do
            {
                len += 1;
                ch = CharCodeAt(pos + len);
            }
            while (IsDigitCharacter(ch));

            Pos += len;
            return new Token(TokenType.Int, pos, len);
        }

        // variable name
        if (IsVariableCharacter(ch))
        {
            do
            {
                ch = CharCodeAt(pos + (++len));
            }
            while (IsVariableCharacter(ch) || IsDigitCharacter(ch));

            Pos += len;
            return new Token(TokenType.VariableName, pos, len);
        }

        // format
        do
        {
            len += 1;
            ch = CharCodeAt(pos + len);
        }
        while (ch != -1
            && StaticType(ch) is null // not static token
            && !IsDigitCharacter(ch) // not number
            && !IsVariableCharacter(ch)); // not variable

        Pos += len;
        return new Token(TokenType.Format, pos, len);
    }
}

/// <summary>Parses TextMate snippet syntax into a <see cref="TextmateSnippet"/> (upstream's SnippetParser).</summary>
internal sealed class SnippetParser
{
    private static readonly Regex s_escape = new(@"\$|}|\\", RegexOptions.CultureInvariant);
    private static readonly Regex s_untilUnescape = new(@"\\(\$|}|\\)", RegexOptions.CultureInvariant);

    private readonly Scanner _scanner = new();
    private Token _token = new(TokenType.EOF, 0, 0);

    /// <summary>Escapes <c>$</c>, <c>}</c> and <c>\</c> so the text inserts literally.</summary>
    public static string Escape(string value) => s_escape.Replace(value, @"\$&");

    /// <summary>
    /// Takes a snippet and returns the insertable string, e.g return the snippet-string without any placeholder,
    /// tabstop, variables etc...
    /// </summary>
    public static string AsInsertText(string value) => new SnippetParser().Parse(value).ToString();

    /// <summary>Whether the snippet might use the <c>CLIPBOARD</c> variable.</summary>
    public static bool GuessNeedsClipboard(string template) =>
        template.Contains("$CLIPBOARD", StringComparison.Ordinal) || template.Contains("${CLIPBOARD", StringComparison.Ordinal);

    /// <summary>
    /// Parses <paramref name="value"/>. <paramref name="insertFinalTabstop"/> adds <c>$0</c> at the end when the snippet has
    /// placeholders but no final tab stop; <paramref name="enforceFinalTabstop"/> adds it also when it has none.
    /// </summary>
    public TextmateSnippet Parse(string value, bool insertFinalTabstop = false, bool enforceFinalTabstop = false)
    {
        var snippet = new TextmateSnippet();
        ParseFragment(value, snippet);
        EnsureFinalTabstop(snippet, enforceFinalTabstop, insertFinalTabstop);
        return snippet;
    }

    /// <summary>Parses <paramref name="value"/> into <paramref name="snippet"/>, after its children; returns the new children.</summary>
    public IReadOnlyList<Marker> ParseFragment(string value, TextmateSnippet snippet)
    {
        var offset = snippet.Children.Count;
        _scanner.Text(value);
        _token = _scanner.Next();
        while (ParseCore(snippet))
        {
            // nothing
        }

        // fill in values for placeholders. the first placeholder of an index
        // that has a value defines the value for all placeholders with that index
        var placeholderDefaultValues = new Dictionary<int, IReadOnlyList<Marker>?>();
        var incompletePlaceholders = new List<Placeholder>();
        snippet.Walk(marker =>
        {
            if (marker is Placeholder placeholder)
            {
                if (placeholder.IsFinalTabstop)
                {
                    placeholderDefaultValues[0] = null;
                }
                else if (!placeholderDefaultValues.ContainsKey(placeholder.Index) && placeholder.Children.Count > 0)
                {
                    placeholderDefaultValues[placeholder.Index] = placeholder.Children;
                }
                else
                {
                    incompletePlaceholders.Add(placeholder);
                }
            }

            return true;
        });

        void FillInIncompletePlaceholder(Placeholder placeholder, HashSet<int> stack)
        {
            if (!placeholderDefaultValues.TryGetValue(placeholder.Index, out var defaultValues) || defaultValues is null)
            {
                return;
            }

            var clone = new Placeholder(placeholder.Index) { Transform = placeholder.Transform };
            foreach (var child in defaultValues)
            {
                var newChild = child.Clone();
                clone.AppendChild(newChild);

                // "recurse" on children that are again placeholders
                if (newChild is Placeholder nested && placeholderDefaultValues.ContainsKey(nested.Index) && !stack.Contains(nested.Index))
                {
                    stack.Add(nested.Index);
                    FillInIncompletePlaceholder(nested, stack);
                    stack.Remove(nested.Index);
                }
            }

            snippet.Replace(placeholder, [clone]);
        }

        var stack = new HashSet<int>();
        foreach (var placeholder in incompletePlaceholders)
        {
            FillInIncompletePlaceholder(placeholder, stack);
        }

        return snippet.Children.Skip(offset).ToList();
    }

    /// <summary>Appends <c>$0</c> when it is enforced, or wanted and the snippet has placeholders but no final tab stop.</summary>
    public void EnsureFinalTabstop(TextmateSnippet snippet, bool enforceFinalTabstop, bool insertFinalTabstop)
    {
        if (enforceFinalTabstop || (insertFinalTabstop && snippet.Placeholders.Count > 0))
        {
            var finalTabstop = snippet.Placeholders.Find(p => p.Index == 0);
            if (finalTabstop is null)
            {
                // the snippet uses placeholders but has no
                // final tabstop defined -> insert at the end
                snippet.AppendChild(new Placeholder(0));
            }
        }
    }

    // upstream's _accept(type): true and the next token when the current one is of the type (any type for null)
    private bool Accept(TokenType? type)
    {
        if (type is null || _token.Type == type)
        {
            _token = _scanner.Next();
            return true;
        }

        return false;
    }

    // upstream's _accept(type, true): the token's text, or null when it is not of the type (EOF accepted with null
    // type gives "", which upstream treats as false too)
    private string? AcceptValue(TokenType? type)
    {
        if (type is null || _token.Type == type)
        {
            var ret = _scanner.TokenText(_token);
            _token = _scanner.Next();
            return ret;
        }

        return null;
    }

    private bool BackTo(Token token)
    {
        _scanner.Pos = token.Pos + token.Len;
        _token = token;
        return false;
    }

    private string? Until(TokenType type)
    {
        var start = _token;
        while (_token.Type != type)
        {
            if (_token.Type == TokenType.EOF)
            {
                return null;
            }
            else if (_token.Type == TokenType.Backslash)
            {
                var nextToken = _scanner.Next();
                if (nextToken.Type != TokenType.Dollar
                    && nextToken.Type != TokenType.CurlyClose
                    && nextToken.Type != TokenType.Backslash)
                {
                    return null;
                }
            }

            _token = _scanner.Next();
        }

        var value = s_untilUnescape.Replace(_scanner.Value.Substring(start.Pos, _token.Pos - start.Pos), "$1");
        _token = _scanner.Next();
        return value;
    }

    private bool ParseCore(Marker marker) =>
        ParseEscaped(marker)
        || ParseTabstopOrVariableName(marker)
        || ParseComplexPlaceholder(marker)
        || ParseComplexVariable(marker)
        || ParseAnything(marker);

    // \$, \\, \} -> just text
    private bool ParseEscaped(Marker marker)
    {
        if (AcceptValue(TokenType.Backslash) is { Length: > 0 } value)
        {
            // saw a backslash, append escaped token or that backslash
            value = NonEmpty(AcceptValue(TokenType.Dollar))
                ?? NonEmpty(AcceptValue(TokenType.CurlyClose))
                ?? NonEmpty(AcceptValue(TokenType.Backslash))
                ?? value;

            marker.AppendChild(new Text(value));
            return true;
        }

        return false;
    }

    // $foo -> variable, $1 -> tabstop
    private bool ParseTabstopOrVariableName(Marker parent)
    {
        var token = _token;
        string? value = null;
        var match = Accept(TokenType.Dollar)
            && (value = NonEmpty(AcceptValue(TokenType.VariableName)) ?? NonEmpty(AcceptValue(TokenType.Int))) is not null;

        if (!match)
        {
            return BackTo(token);
        }

        parent.AppendChild(IsAllDigits(value!)
            ? new Placeholder(ParseIndex(value!))
            : new Variable(value!));
        return true;
    }

    // ${1:<children>}, ${1} -> placeholder
    private bool ParseComplexPlaceholder(Marker parent)
    {
        var token = _token;
        string? index = null;
        var match = Accept(TokenType.Dollar)
            && Accept(TokenType.CurlyOpen)
            && (index = NonEmpty(AcceptValue(TokenType.Int))) is not null;

        if (!match)
        {
            return BackTo(token);
        }

        var placeholder = new Placeholder(ParseIndex(index!));

        if (Accept(TokenType.Colon))
        {
            // ${1:<children>}
            while (true)
            {
                // ...} -> done
                if (Accept(TokenType.CurlyClose))
                {
                    parent.AppendChild(placeholder);
                    return true;
                }

                if (ParseCore(placeholder))
                {
                    continue;
                }

                // fallback
                parent.AppendChild(new Text("${" + index + ":"));
                foreach (var child in placeholder.Children.ToList())
                {
                    parent.AppendChild(child);
                }

                return true;
            }
        }
        else if (placeholder.Index > 0 && Accept(TokenType.Pipe))
        {
            // ${1|one,two,three|}
            var choice = new Choice();

            while (true)
            {
                if (ParseChoiceElement(choice))
                {
                    if (Accept(TokenType.Comma))
                    {
                        // opt, -> more
                        continue;
                    }

                    if (Accept(TokenType.Pipe))
                    {
                        placeholder.AppendChild(choice);
                        if (Accept(TokenType.CurlyClose))
                        {
                            // ..|} -> done
                            parent.AppendChild(placeholder);
                            return true;
                        }
                    }
                }

                BackTo(token);
                return false;
            }
        }
        else if (Accept(TokenType.Forwardslash))
        {
            // ${1/<regex>/<format>/<options>}
            if (ParseTransform(placeholder))
            {
                parent.AppendChild(placeholder);
                return true;
            }

            BackTo(token);
            return false;
        }
        else if (Accept(TokenType.CurlyClose))
        {
            // ${1}
            parent.AppendChild(placeholder);
            return true;
        }
        else
        {
            // ${1 <- missing curly or colon
            return BackTo(token);
        }
    }

    private bool ParseChoiceElement(Choice parent)
    {
        var token = _token;
        var values = new List<string>();

        while (true)
        {
            if (_token.Type == TokenType.Comma || _token.Type == TokenType.Pipe)
            {
                break;
            }

            string? value;
            if (AcceptValue(TokenType.Backslash) is { Length: > 0 } backslash)
            {
                // \, \|, or \\
                value = NonEmpty(AcceptValue(TokenType.Comma))
                    ?? NonEmpty(AcceptValue(TokenType.Pipe))
                    ?? NonEmpty(AcceptValue(TokenType.Backslash))
                    ?? backslash;
            }
            else
            {
                value = AcceptValue(null);
            }

            if (string.IsNullOrEmpty(value))
            {
                // EOF
                BackTo(token);
                return false;
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            BackTo(token);
            return false;
        }

        parent.AppendChild(new Text(string.Concat(values)));
        return true;
    }

    // ${foo:<children>}, ${foo} -> variable
    private bool ParseComplexVariable(Marker parent)
    {
        var token = _token;
        string? name = null;
        var match = Accept(TokenType.Dollar)
            && Accept(TokenType.CurlyOpen)
            && (name = NonEmpty(AcceptValue(TokenType.VariableName))) is not null;

        if (!match)
        {
            return BackTo(token);
        }

        var variable = new Variable(name!);

        if (Accept(TokenType.Colon))
        {
            // ${foo:<children>}
            while (true)
            {
                // ...} -> done
                if (Accept(TokenType.CurlyClose))
                {
                    parent.AppendChild(variable);
                    return true;
                }

                if (ParseCore(variable))
                {
                    continue;
                }

                // fallback
                parent.AppendChild(new Text("${" + name + ":"));
                foreach (var child in variable.Children.ToList())
                {
                    parent.AppendChild(child);
                }

                return true;
            }
        }
        else if (Accept(TokenType.Forwardslash))
        {
            // ${foo/<regex>/<format>/<options>}
            if (ParseTransform(variable))
            {
                parent.AppendChild(variable);
                return true;
            }

            BackTo(token);
            return false;
        }
        else if (Accept(TokenType.CurlyClose))
        {
            // ${foo}
            parent.AppendChild(variable);
            return true;
        }
        else
        {
            // ${foo <- missing curly or colon
            return BackTo(token);
        }
    }

    private bool ParseTransform(TransformableMarker parent)
    {
        // ...<regex>/<format>/<options>}

        var transform = new Transform();
        var regexValue = "";
        var regexOptions = "";

        // (1) /regex
        while (true)
        {
            if (Accept(TokenType.Forwardslash))
            {
                break;
            }

            if (AcceptValue(TokenType.Backslash) is { Length: > 0 } escaped)
            {
                escaped = NonEmpty(AcceptValue(TokenType.Forwardslash)) ?? escaped;
                regexValue += escaped;
                continue;
            }

            if (_token.Type != TokenType.EOF)
            {
                regexValue += AcceptValue(null);
                continue;
            }

            return false;
        }

        // (2) /format
        while (true)
        {
            if (Accept(TokenType.Forwardslash))
            {
                break;
            }

            if (AcceptValue(TokenType.Backslash) is { Length: > 0 } escaped)
            {
                escaped = NonEmpty(AcceptValue(TokenType.Backslash)) ?? NonEmpty(AcceptValue(TokenType.Forwardslash)) ?? escaped;
                transform.AppendChild(new Text(escaped));
                continue;
            }

            if (ParseFormatString(transform) || ParseAnything(transform))
            {
                continue;
            }

            return false;
        }

        // (3) /option
        while (true)
        {
            if (Accept(TokenType.CurlyClose))
            {
                break;
            }

            if (_token.Type != TokenType.EOF)
            {
                regexOptions += AcceptValue(null);
                continue;
            }

            return false;
        }

        try
        {
            transform.Regexp = new JsRegExp(regexValue, regexOptions);
        }
        catch (ArgumentException)
        {
            // invalid regexp
            return false;
        }

        parent.Transform = transform;
        return true;
    }

    private bool ParseFormatString(Transform parent)
    {
        var token = _token;
        if (!Accept(TokenType.Dollar))
        {
            return false;
        }

        var complex = false;
        if (Accept(TokenType.CurlyOpen))
        {
            complex = true;
        }

        var index = NonEmpty(AcceptValue(TokenType.Int));

        if (index is null)
        {
            BackTo(token);
            return false;
        }
        else if (!complex)
        {
            // $1
            parent.AppendChild(new FormatString(ParseIndex(index)));
            return true;
        }
        else if (Accept(TokenType.CurlyClose))
        {
            // ${1}
            parent.AppendChild(new FormatString(ParseIndex(index)));
            return true;
        }
        else if (!Accept(TokenType.Colon))
        {
            BackTo(token);
            return false;
        }

        if (Accept(TokenType.Forwardslash))
        {
            // ${1:/upcase}
            var shorthand = NonEmpty(AcceptValue(TokenType.VariableName));
            if (shorthand is null || !Accept(TokenType.CurlyClose))
            {
                BackTo(token);
                return false;
            }
            else
            {
                parent.AppendChild(new FormatString(ParseIndex(index), shorthand));
                return true;
            }
        }
        else if (Accept(TokenType.Plus))
        {
            // ${1:+<if>}
            if (NonEmpty(Until(TokenType.CurlyClose)) is { } ifValue)
            {
                parent.AppendChild(new FormatString(ParseIndex(index), null, ifValue, null));
                return true;
            }
        }
        else if (Accept(TokenType.Dash))
        {
            // ${2:-<else>}
            if (NonEmpty(Until(TokenType.CurlyClose)) is { } elseValue)
            {
                parent.AppendChild(new FormatString(ParseIndex(index), null, null, elseValue));
                return true;
            }
        }
        else if (Accept(TokenType.QuestionMark))
        {
            // ${2:?<if>:<else>}
            if (NonEmpty(Until(TokenType.Colon)) is { } ifValue)
            {
                if (NonEmpty(Until(TokenType.CurlyClose)) is { } elseValue)
                {
                    parent.AppendChild(new FormatString(ParseIndex(index), null, ifValue, elseValue));
                    return true;
                }
            }
        }
        else
        {
            // ${1:<else>}
            if (NonEmpty(Until(TokenType.CurlyClose)) is { } elseValue)
            {
                parent.AppendChild(new FormatString(ParseIndex(index), null, null, elseValue));
                return true;
            }
        }

        BackTo(token);
        return false;
    }

    private bool ParseAnything(Marker marker)
    {
        if (_token.Type != TokenType.EOF)
        {
            marker.AppendChild(new Text(_scanner.TokenText(_token)));
            Accept(null);
            return true;
        }

        return false;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static bool IsAllDigits(string value) => value.All(ch => ch is >= '0' and <= '9');

    // Number(value): an index too large for int saturates (JavaScript keeps a double)
    private static int ParseIndex(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : int.MaxValue;
}

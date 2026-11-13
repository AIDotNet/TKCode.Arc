using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>One part of a parsed <c>formatString</c> template or <c>${...}</c> expression.</summary>
public abstract record A2UIExpressionNode
{
    /// <summary>The node as a protocol dynamic value: a literal, <c>{"@path"}</c> or <c>{"@call", "args"}</c>.</summary>
    public abstract JsonNode? ToDynamicValue();
}

/// <summary>A literal string, number or boolean.</summary>
public sealed record A2UILiteralExpression(JsonNode? Value) : A2UIExpressionNode
{
    public override JsonNode? ToDynamicValue() => Value?.DeepClone();
}

/// <summary>A data model path, absolute (<c>/a/b</c>) or relative to the current scope.</summary>
public sealed record A2UIPathExpression(string Path) : A2UIExpressionNode
{
    public override JsonNode? ToDynamicValue() => new JsonObject { ["@path"] = Path };
}

/// <summary>A function call with named arguments.</summary>
public sealed record A2UICallExpression(string Name, IReadOnlyList<KeyValuePair<string, A2UIExpressionNode>> Args) : A2UIExpressionNode
{
    public override JsonNode? ToDynamicValue()
    {
        var args = new JsonObject();
        foreach (var (key, value) in Args)
        {
            args[key] = value.ToDynamicValue();
        }

        return new JsonObject { ["@call"] = Name, ["args"] = args };
    }
}

/// <summary>
/// Parser of the <c>formatString</c> template language: literal text with <c>${expression}</c> parts, where an
/// expression is a quoted string, a number, <c>true</c>/<c>false</c>/<c>null</c>, a data path, a function call with
/// named arguments (<c>fn(name: value, ...)</c>) or a nested <c>${...}</c>. <c>\${</c> is a literal <c>${</c>.
/// </summary>
public static class A2UIExpressionParser
{
    /// <summary>The longest template accepted, in characters.</summary>
    public const int MaxLength = 10_000;

    /// <summary>The most parts a template may have.</summary>
    public const int MaxParts = 1_000;

    /// <summary>The deepest nesting of <c>${</c> and calls.</summary>
    public const int MaxDepth = 100;

    /// <summary>
    /// Parses a template into parts. Adjacent literal text is joined and empty text dropped. Throws
    /// <see cref="A2UIException"/> (<see cref="A2UIErrorCategory.Expression"/>) when it does not parse.
    /// </summary>
    public static IReadOnlyList<A2UIExpressionNode> ParseTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Length > MaxLength)
        {
            throw Error($"Template exceeds the maximum length of {MaxLength} characters.");
        }

        var parser = new Parser(template);
        return parser.Template();
    }

    /// <summary>True when the text contains no interpolation (only literal text and escapes).</summary>
    public static bool IsPlainText(string template) => !template.Contains("${", StringComparison.Ordinal);

    internal static A2UIException Error(string message) => A2UIException.Expression(message);

    private sealed class Parser(string text)
    {
        private int _pos;
        private int _depth;

        public List<A2UIExpressionNode> Template()
        {
            var parts = new List<A2UIExpressionNode>();
            var literal = new StringBuilder();
            while (_pos < text.Length)
            {
                var c = text[_pos];
                if (c == '\\' && At("\\${"))
                {
                    literal.Append("${");
                    _pos += 3;
                    continue;
                }

                if (c == '$' && At("${"))
                {
                    _pos += 2;
                    var node = Interpolation();
                    if (node is A2UILiteralExpression { Value: var v } && (v is null || A2UIJson.IsString(v)))
                    {
                        literal.Append(A2UIJson.ToDisplayString(v));
                    }
                    else
                    {
                        FlushLiteral(parts, literal);
                        parts.Add(node);
                    }

                    if (parts.Count > MaxParts)
                    {
                        throw Error($"Template has more than {MaxParts} parts.");
                    }

                    continue;
                }

                literal.Append(c);
                _pos++;
            }

            FlushLiteral(parts, literal);
            return parts;
        }

        private static void FlushLiteral(List<A2UIExpressionNode> parts, StringBuilder literal)
        {
            if (literal.Length > 0)
            {
                parts.Add(new A2UILiteralExpression(JsonValue.Create(literal.ToString())));
                literal.Clear();
            }
        }

        // After "${": an expression, then "}".
        private A2UIExpressionNode Interpolation()
        {
            Enter();
            SkipWhitespace();
            if (_pos >= text.Length)
            {
                throw Error("Unclosed interpolation: expected '}'.");
            }

            var node = Expression();
            SkipWhitespace();
            if (_pos >= text.Length)
            {
                throw Error("Unclosed interpolation: expected '}'.");
            }

            if (text[_pos] != '}')
            {
                throw Error($"Unexpected characters at {_pos} in expression.");
            }

            _pos++;
            _depth--;
            return node;
        }

        private A2UIExpressionNode Expression()
        {
            SkipWhitespace();
            if (_pos >= text.Length)
            {
                throw Error("Unclosed interpolation: expected an expression.");
            }

            var c = text[_pos];
            if (c == '$' && At("${"))
            {
                _pos += 2;
                return Interpolation();
            }

            if (c is '\'' or '"')
            {
                return new A2UILiteralExpression(JsonValue.Create(QuotedString(c)));
            }

            if (StartsNumber())
            {
                return Number();
            }

            var start = _pos;
            var token = Token();
            if (token.Length == 0)
            {
                throw Error($"Unexpected characters at {start} in expression.");
            }

            var save = _pos;
            SkipWhitespace();
            if (_pos < text.Length && text[_pos] == '(')
            {
                if (!IsName(token))
                {
                    throw Error($"Invalid function name '{token}'.");
                }

                _pos++;
                return Call(token);
            }

            _pos = save;
            switch (token)
            {
                case "true":
                    return new A2UILiteralExpression(JsonValue.Create(true));
                case "false":
                    return new A2UILiteralExpression(JsonValue.Create(false));
                case "null":
                    return new A2UILiteralExpression(JsonValue.Create(string.Empty));
            }

            return new A2UIPathExpression(token);
        }

        private A2UICallExpression Call(string name)
        {
            Enter();
            var args = new List<KeyValuePair<string, A2UIExpressionNode>>();
            while (true)
            {
                SkipWhitespace();
                if (_pos >= text.Length)
                {
                    throw Error("Expected ')' after function arguments.");
                }

                if (text[_pos] == ')')
                {
                    _pos++;
                    break;
                }

                var argStart = _pos;
                var argName = NameToken();
                SkipWhitespace();
                if (argName.Length == 0 || _pos >= text.Length || text[_pos] != ':')
                {
                    throw Error($"Expected ':' after argument name at {argStart}.");
                }

                _pos++;
                var value = Expression();
                args.Add(new(argName, value));
                SkipWhitespace();
                if (_pos >= text.Length)
                {
                    throw Error("Expected ')' after function arguments.");
                }

                if (text[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                if (text[_pos] == ')')
                {
                    _pos++;
                    break;
                }

                throw Error("Expected ')' after function arguments.");
            }

            _depth--;
            return new A2UICallExpression(name, args);
        }

        private void Enter()
        {
            if (++_depth > MaxDepth)
            {
                throw Error($"Max recursion depth of {MaxDepth} exceeded in expression.");
            }
        }

        private string QuotedString(char quote)
        {
            _pos++;
            var sb = new StringBuilder();
            while (_pos < text.Length)
            {
                var c = text[_pos++];
                if (c == quote)
                {
                    return sb.ToString();
                }

                if (c == '\\' && _pos < text.Length)
                {
                    var e = text[_pos++];
                    sb.Append(e switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        _ => e
                    });
                    continue;
                }

                sb.Append(c);
            }

            throw Error("Unterminated string literal in expression.");
        }

        private bool StartsNumber()
        {
            var i = _pos;
            if (text[i] is '+' or '-')
            {
                i++;
            }

            if (i >= text.Length)
            {
                return false;
            }

            if (char.IsAsciiDigit(text[i]))
            {
                return true;
            }

            return text[i] == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]);
        }

        private A2UIExpressionNode Number()
        {
            var start = _pos;
            if (text[_pos] is '+' or '-')
            {
                _pos++;
            }

            var digits = 0;
            while (_pos < text.Length && char.IsAsciiDigit(text[_pos]))
            {
                _pos++;
                digits++;
            }

            if (_pos < text.Length && text[_pos] == '.')
            {
                _pos++;
                while (_pos < text.Length && char.IsAsciiDigit(text[_pos]))
                {
                    _pos++;
                    digits++;
                }
            }

            if (digits == 0)
            {
                throw Error("Invalid number literal.");
            }

            if (_pos < text.Length && text[_pos] is 'e' or 'E')
            {
                _pos++;
                if (_pos < text.Length && text[_pos] is '+' or '-')
                {
                    _pos++;
                }

                var expDigits = 0;
                while (_pos < text.Length && char.IsAsciiDigit(text[_pos]))
                {
                    _pos++;
                    expDigits++;
                }

                if (expDigits == 0)
                {
                    throw Error("Invalid number literal: missing exponent digits.");
                }
            }

            if (_pos < text.Length)
            {
                var next = text[_pos];
                if (next == '.' || char.IsAsciiDigit(next) || next is 'e' or 'E')
                {
                    throw Error("Invalid number literal.");
                }

                if (IsPathChar(text, _pos, out _))
                {
                    throw Error($"Unexpected characters at {_pos} after a number.");
                }
            }

            var literal = text.AsSpan(start, _pos - start);
            if (literal[0] == '+')
            {
                literal = literal[1..];
            }

            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value))
            {
                throw Error("Number literal out of range.");
            }

            return new A2UILiteralExpression(A2UIJson.Number(value));
        }

        // A path or a name: a run of path characters.
        private string Token()
        {
            var start = _pos;
            while (_pos < text.Length && IsPathChar(text, _pos, out var width))
            {
                _pos += width;
            }

            return text[start.._pos];
        }

        private string NameToken()
        {
            var start = _pos;
            var first = true;
            while (_pos < text.Length)
            {
                if (!TryRune(text, _pos, out var rune, out var width))
                {
                    break;
                }

                var ok = first ? IsNameStart(rune) : IsXidContinue(rune);
                if (!ok)
                {
                    break;
                }

                first = false;
                _pos += width;
            }

            return text[start.._pos];
        }

        private void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }

        private bool At(string token) => string.CompareOrdinal(text, _pos, token, 0, token.Length) == 0;
    }

    /// <summary>True when the text is a function name: <c>@</c> or an identifier start, then identifier characters.</summary>
    internal static bool IsName(string token)
    {
        if (token.Length == 0)
        {
            return false;
        }

        var first = true;
        for (var i = 0; i < token.Length;)
        {
            if (!TryRune(token, i, out var rune, out var width))
            {
                return false;
            }

            if (first ? !IsNameStart(rune) : !IsXidContinue(rune))
            {
                return false;
            }

            first = false;
            i += width;
        }

        return true;
    }

    private static bool IsNameStart(Rune rune) => rune.Value is '_' or '@' || IsXidStart(rune);

    private static bool IsPathChar(string text, int index, out int width)
    {
        if (!TryRune(text, index, out var rune, out width))
        {
            return false;
        }

        return rune.Value is '/' or '.' or '-' or '~' or '_' or '@' || IsXidContinue(rune);
    }

    internal static bool IsXidStart(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
    }

    internal static bool IsXidContinue(Rune rune)
    {
        if (rune.Value == '_' || IsXidStart(rune))
        {
            return true;
        }

        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation;
    }

    private static bool TryRune(string text, int index, out Rune rune, out int width)
    {
        if (Rune.DecodeFromUtf16(text.AsSpan(index), out rune, out width) == System.Buffers.OperationStatus.Done)
        {
            return true;
        }

        width = 1;
        return false;
    }
}

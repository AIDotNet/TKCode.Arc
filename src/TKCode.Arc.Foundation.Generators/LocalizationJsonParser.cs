using System;
using System.Collections.Generic;
using System.Text;

namespace TKCode.Arc.Foundation.Generators
{
    internal enum JsonValueKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    internal sealed class JsonValue
    {
        public JsonValue(JsonValueKind kind, int line, string? stringValue = null, List<JsonProperty>? properties = null)
        {
            Kind = kind;
            Line = line;
            StringValue = stringValue;
            Properties = properties;
        }

        public JsonValueKind Kind { get; }
        public int Line { get; }
        public string? StringValue { get; }
        public List<JsonProperty>? Properties { get; }
    }

    internal sealed class JsonProperty
    {
        public JsonProperty(string name, JsonValue value, int line)
        {
            Name = name;
            Value = value;
            Line = line;
        }

        public string Name { get; }
        public JsonValue Value { get; }
        public int Line { get; }
    }

    /// <summary>
    /// Minimal RFC 8259 JSON parser for the <c>netstandard2.0</c> generator: zero external dependencies, 1-based line
    /// tracking for diagnostics, and duplicate object keys preserved so the emitter can report <c>ARCF023</c>.
    /// </summary>
    internal ref struct LocalizationJsonParser
    {
        private const int MaxDepth = 64;

        private readonly string _text;
        private int _index;
        private int _line;
        private string? _error;
        private int _errorLine;

        private LocalizationJsonParser(string text)
        {
            _text = text;
            _index = 0;
            _line = 1;
            _error = null;
            _errorLine = 1;
        }

        public static bool TryParse(string text, out JsonValue? root, out string? error, out int errorLine)
        {
            var parser = new LocalizationJsonParser(text);
            root = parser.ParseDocument();
            error = parser._error;
            errorLine = parser._errorLine;
            return root is not null && error is null;
        }

        private JsonValue? ParseDocument()
        {
            if (_text.Length > 0 && _text[0] == '\uFEFF')
            {
                _index = 1;
            }
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                return Fail("JSON file is empty");
            }
            var value = ParseValue(0);
            if (value is null)
            {
                return null;
            }
            SkipWhitespace();
            if (_index < _text.Length)
            {
                return Fail($"unexpected trailing character '{_text[_index]}'");
            }
            return value;
        }

        private JsonValue? ParseValue(int depth)
        {
            if (depth > MaxDepth)
            {
                return Fail("JSON nesting exceeds the maximum depth");
            }
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                return Fail("unexpected end of JSON input");
            }
            var c = _text[_index];
            switch (c)
            {
                case '{':
                    return ParseObject(depth + 1);
                case '[':
                    return ParseArray(depth + 1);
                case '"':
                {
                    var line = _line;
                    var text = ParseString();
                    return text is null ? null : new JsonValue(JsonValueKind.String, line, stringValue: text);
                }
                case 't':
                    return ParseLiteral("true", JsonValueKind.Boolean);
                case 'f':
                    return ParseLiteral("false", JsonValueKind.Boolean);
                case 'n':
                    return ParseLiteral("null", JsonValueKind.Null);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ParseNumber();
                    }
                    return Fail($"unexpected character '{c}'");
            }
        }

        private JsonValue? ParseObject(int depth)
        {
            var startLine = _line;
            _index++; // '{'
            var properties = new List<JsonProperty>();
            SkipWhitespace();
            if (_index < _text.Length && _text[_index] == '}')
            {
                _index++;
                return new JsonValue(JsonValueKind.Object, startLine, properties: properties);
            }
            while (true)
            {
                SkipWhitespace();
                if (_index >= _text.Length)
                {
                    return Fail("unterminated JSON object");
                }
                if (_text[_index] != '"')
                {
                    return Fail($"expected '\"' to start a property name, found '{_text[_index]}'");
                }
                var propLine = _line;
                var name = ParseString();
                if (name is null)
                {
                    return null;
                }
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != ':')
                {
                    return Fail($"expected ':' after property name '{name}'");
                }
                _index++; // ':'
                var value = ParseValue(depth);
                if (value is null)
                {
                    return null;
                }
                properties.Add(new JsonProperty(name, value, propLine));
                SkipWhitespace();
                if (_index >= _text.Length)
                {
                    return Fail("unterminated JSON object");
                }
                var separator = _text[_index];
                if (separator == ',')
                {
                    _index++;
                    continue;
                }
                if (separator == '}')
                {
                    _index++;
                    return new JsonValue(JsonValueKind.Object, startLine, properties: properties);
                }
                return Fail($"expected ',' or '}}' in JSON object, found '{separator}'");
            }
        }

        private JsonValue? ParseArray(int depth)
        {
            var startLine = _line;
            _index++; // '['
            SkipWhitespace();
            if (_index < _text.Length && _text[_index] == ']')
            {
                _index++;
                return new JsonValue(JsonValueKind.Array, startLine);
            }
            while (true)
            {
                if (ParseValue(depth) is null)
                {
                    return null;
                }
                SkipWhitespace();
                if (_index >= _text.Length)
                {
                    return Fail("unterminated JSON array");
                }
                var separator = _text[_index];
                if (separator == ',')
                {
                    _index++;
                    continue;
                }
                if (separator == ']')
                {
                    _index++;
                    return new JsonValue(JsonValueKind.Array, startLine);
                }
                return Fail($"expected ',' or ']' in JSON array, found '{separator}'");
            }
        }

        private string? ParseString()
        {
            _index++; // '"'
            var start = _index;
            StringBuilder? builder = null;
            while (_index < _text.Length)
            {
                var c = _text[_index];
                if (c == '"')
                {
                    var result = builder is null
                        ? _text.Substring(start, _index - start)
                        : builder.Append(_text, start, _index - start).ToString();
                    _index++;
                    return result;
                }
                if (c < 0x20)
                {
                    Fail("unescaped control character in JSON string");
                    return null;
                }
                if (c == '\\')
                {
                    builder ??= new StringBuilder();
                    if (_index > start)
                    {
                        builder.Append(_text, start, _index - start);
                    }
                    _index++;
                    if (_index >= _text.Length)
                    {
                        Fail("unterminated escape sequence in JSON string");
                        return null;
                    }
                    var esc = _text[_index++];
                    switch (esc)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            if (_index + 4 > _text.Length || !TryParseHex4(_text, _index, out var code))
                            {
                                Fail("invalid \\uXXXX escape sequence in JSON string");
                                return null;
                            }
                            builder.Append((char)code);
                            _index += 4;
                            break;
                        default:
                            Fail($"invalid escape sequence '\\{esc}' in JSON string");
                            return null;
                    }
                    start = _index;
                    continue;
                }
                _index++;
            }
            Fail("unterminated JSON string");
            return null;
        }

        private JsonValue? ParseLiteral(string literal, JsonValueKind kind)
        {
            var line = _line;
            if (_index + literal.Length > _text.Length
                || string.Compare(_text, _index, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
            {
                return Fail($"expected '{literal}'");
            }
            _index += literal.Length;
            return new JsonValue(kind, line);
        }

        private JsonValue? ParseNumber()
        {
            var line = _line;
            if (_text[_index] == '-')
            {
                _index++;
            }
            if (_index >= _text.Length)
            {
                return Fail("incomplete JSON number");
            }
            if (_text[_index] == '0')
            {
                _index++;
            }
            else if (_text[_index] >= '1' && _text[_index] <= '9')
            {
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                {
                    _index++;
                }
            }
            else
            {
                return Fail("invalid JSON number");
            }
            if (_index < _text.Length && _text[_index] == '.')
            {
                _index++;
                var digitsStart = _index;
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                {
                    _index++;
                }
                if (_index == digitsStart)
                {
                    return Fail("expected digit after decimal point in JSON number");
                }
            }
            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                _index++;
                if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-'))
                {
                    _index++;
                }
                var digitsStart = _index;
                while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9')
                {
                    _index++;
                }
                if (_index == digitsStart)
                {
                    return Fail("expected digit in JSON number exponent");
                }
            }
            return new JsonValue(JsonValueKind.Number, line);
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length)
            {
                var c = _text[_index];
                if (c == ' ' || c == '\t')
                {
                    _index++;
                }
                else if (c == '\r')
                {
                    _index++;
                    if (_index < _text.Length && _text[_index] == '\n')
                    {
                        _index++;
                    }
                    _line++;
                }
                else if (c == '\n')
                {
                    _index++;
                    _line++;
                }
                else
                {
                    break;
                }
            }
        }

        private static bool TryParseHex4(string text, int start, out int value)
        {
            value = 0;
            for (var i = 0; i < 4; i++)
            {
                var c = text[start + i];
                int digit;
                if (c >= '0' && c <= '9')
                {
                    digit = c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    digit = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    digit = c - 'A' + 10;
                }
                else
                {
                    return false;
                }
                value = (value << 4) | digit;
            }
            return true;
        }

        private JsonValue? Fail(string message)
        {
            _error = message;
            _errorLine = _line;
            return null;
        }
    }
}

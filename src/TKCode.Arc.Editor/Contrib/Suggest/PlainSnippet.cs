// The text a snippet inserts without a snippet session: the fallback the suggest controller uses until a snippet
// inserter (the snippet feature's SnippetController2) is wired in. It follows the TextMate snippet grammar of
// src/vs/editor/contrib/snippet/browser/snippetParser.ts (commit b57d5c9e, MIT license) only as far as needed to drop
// the syntax: tab stops ($1, ${1}), placeholders (${1:text}, nested), choices (${1|a,b|}: the first), variables
// ($NAME, ${NAME}, ${NAME:default}; CLIPBOARD reads the clipboard text, the others insert their default or nothing),
// transforms (dropped) and the escapes \$, \} and \\. Also SnippetParser.escape.

using System.Text;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>A snippet reduced to plain text, with where the cursor goes.</summary>
/// <param name="Text">The text to insert.</param>
/// <param name="SelectionStart">The offset in <see cref="Text"/> of the first tab stop (the lowest above 0, else $0), or the end.</param>
/// <param name="SelectionLength">The length of that tab stop's placeholder text.</param>
internal sealed record PlainSnippet(string Text, int SelectionStart, int SelectionLength)
{
    /// <summary>Escapes <c>$</c>, <c>}</c> and <c>\</c> so that plain text inserts as itself (upstream's SnippetParser.escape).</summary>
    public static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            if (ch is '$' or '}' or '\\')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>The plain text of <paramref name="template"/> and its first tab stop.</summary>
    public static PlainSnippet Parse(string template, string? clipboardText = null)
    {
        var parser = new Parser(template, clipboardText);
        parser.ParseUntil(sb: parser.Output, endChar: null);
        var text = parser.Output.ToString();

        // the first tab stop: the lowest index above 0, else $0, else the end
        (int Index, int Start, int Length)? best = null;
        foreach (var stop in parser.TabStops)
        {
            if (stop.Start < 0)
            {
                continue; // inside a variable's default
            }
            if (best is null
                || (stop.Index != 0 && (best.Value.Index == 0 || stop.Index < best.Value.Index)))
            {
                best = stop;
            }
        }
        return best is { } b ? new PlainSnippet(text, b.Start, b.Length) : new PlainSnippet(text, text.Length, 0);
    }

    private sealed class Parser(string template, string? clipboardText)
    {
        private int _pos;

        public StringBuilder Output { get; } = new();

        public List<(int Index, int Start, int Length)> TabStops { get; } = new();

        // Parses into sb until endChar (unescaped) or the end; returns whether endChar was found (and consumed).
        public bool ParseUntil(StringBuilder sb, char? endChar)
        {
            while (_pos < template.Length)
            {
                var ch = template[_pos];
                if (endChar is { } end && ch == end)
                {
                    _pos++;
                    return true;
                }
                if (ch == '\\' && _pos + 1 < template.Length && template[_pos + 1] is '$' or '}' or '\\' or ',' or '|')
                {
                    sb.Append(template[_pos + 1]);
                    _pos += 2;
                    continue;
                }
                if (ch == '$' && TryParseDollar(sb))
                {
                    continue;
                }
                sb.Append(ch);
                _pos++;
            }
            return false;
        }

        private bool TryParseDollar(StringBuilder sb)
        {
            var start = _pos;
            var next = _pos + 1 < template.Length ? template[_pos + 1] : '\0';

            // $1
            if (char.IsAsciiDigit(next))
            {
                _pos++;
                var index = ReadInt();
                TabStops.Add((index, Offset(sb), 0));
                return true;
            }

            // $NAME
            if (IsVariableStart(next))
            {
                _pos++;
                var name = ReadName();
                sb.Append(ResolveVariable(name) ?? string.Empty);
                return true;
            }

            if (next != '{')
            {
                return false;
            }

            _pos += 2;
            if (_pos < template.Length && char.IsAsciiDigit(template[_pos]))
            {
                var index = ReadInt();
                var at = Offset(sb);
                if (_pos < template.Length && template[_pos] == '}')
                {
                    // ${1}
                    _pos++;
                    TabStops.Add((index, at, 0));
                    return true;
                }
                if (_pos < template.Length && template[_pos] == ':')
                {
                    // ${1:placeholder}, placeholders may nest
                    _pos++;
                    var before = sb.Length;
                    ParseUntil(sb, '}');
                    TabStops.Add((index, at, sb.Length - before));
                    return true;
                }
                if (_pos < template.Length && template[_pos] == '|')
                {
                    // ${1|one,two|}: the first choice
                    _pos++;
                    var choice = new StringBuilder();
                    var first = true;
                    while (_pos < template.Length)
                    {
                        var c = template[_pos];
                        if (c == '\\' && _pos + 1 < template.Length)
                        {
                            if (first)
                            {
                                choice.Append(template[_pos + 1]);
                            }
                            _pos += 2;
                            continue;
                        }
                        if (c == '|' && _pos + 1 < template.Length && template[_pos + 1] == '}')
                        {
                            _pos += 2;
                            break;
                        }
                        if (c == ',')
                        {
                            first = false;
                        }
                        else if (first)
                        {
                            choice.Append(c);
                        }
                        _pos++;
                    }
                    sb.Append(choice);
                    TabStops.Add((index, at, choice.Length));
                    return true;
                }
                if (_pos < template.Length && template[_pos] == '/')
                {
                    // ${1/regex/format/options}: a transform of tab stop 1, dropped
                    SkipTransform();
                    TabStops.Add((index, at, 0));
                    return true;
                }
            }
            else if (_pos < template.Length && IsVariableStart(template[_pos]))
            {
                var name = ReadName();
                if (_pos < template.Length && template[_pos] == '}')
                {
                    _pos++;
                    sb.Append(ResolveVariable(name) ?? string.Empty);
                    return true;
                }
                if (_pos < template.Length && template[_pos] == ':')
                {
                    _pos++;
                    var fallback = new StringBuilder();
                    ParseUntil(fallback, '}');
                    sb.Append(ResolveVariable(name) ?? fallback.ToString());
                    return true;
                }
                if (_pos < template.Length && template[_pos] == '/')
                {
                    SkipTransform();
                    sb.Append(ResolveVariable(name) ?? string.Empty);
                    return true;
                }
            }

            // not a snippet construct: the text as it is
            _pos = start;
            return false;
        }

        private static bool IsVariableStart(char ch) => ch == '_' || char.IsAsciiLetter(ch);

        private int Offset(StringBuilder sb) => ReferenceEquals(sb, Output) ? sb.Length : -1;

        private int ReadInt()
        {
            var value = 0;
            while (_pos < template.Length && char.IsAsciiDigit(template[_pos]))
            {
                value = Math.Min(value * 10 + (template[_pos] - '0'), int.MaxValue / 10);
                _pos++;
            }
            return value;
        }

        private string ReadName()
        {
            var start = _pos;
            while (_pos < template.Length && (template[_pos] == '_' || char.IsAsciiLetterOrDigit(template[_pos])))
            {
                _pos++;
            }
            return template[start.._pos];
        }

        private void SkipTransform()
        {
            // /regex/format/options}
            var slashes = 0;
            var depth = 0;
            while (_pos < template.Length)
            {
                var c = template[_pos];
                if (c == '\\' && _pos + 1 < template.Length)
                {
                    _pos += 2;
                    continue;
                }
                _pos++;
                if (c == '/' && depth == 0)
                {
                    slashes++;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}' && depth > 0)
                {
                    depth--;
                }
                else if (c == '}' && slashes >= 3)
                {
                    return;
                }
            }
        }

        private string? ResolveVariable(string name) => name == "CLIPBOARD" ? clipboardText : null;
    }
}

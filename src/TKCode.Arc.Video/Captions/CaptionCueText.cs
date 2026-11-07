using System.Buffers;
using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Turns a cue payload into plain text and styled runs (design/components/video/video.md section 9). Hand-written single
/// pass, no regular expressions. WebVTT: <c>&lt;b&gt; &lt;i&gt; &lt;u&gt; &lt;c.class&gt; &lt;v Speaker&gt; &lt;lang xx&gt;
/// &lt;ruby&gt;/&lt;rt&gt;</c> (ruby text in parentheses after its base), inline timestamps dropped, character references
/// decoded. SRT: <c>&lt;b&gt; &lt;i&gt; &lt;u&gt; &lt;font ...&gt;</c> (font ignored), <c>{\an8}</c>-style ASS tags stripped,
/// anything else literal.
/// </summary>
internal static class CaptionCueText
{
    private const int MaxReferenceLength = 10;
    private static readonly SearchValues<char> VttSpecials = SearchValues.Create("<&");
    private static readonly SearchValues<char> SrtSpecials = SearchValues.Create("<{");

    /// <summary>
    /// Parses <paramref name="payload"/> (lines joined with <c>\n</c>). Pass the same <paramref name="state"/> for every cue of
    /// a file to reuse its buffers.
    /// </summary>
    public static (string Text, IReadOnlyList<VideoCueRun> Runs) Parse(string payload, bool srt, State? state = null)
    {
        state ??= new State();
        state.Reset();
        var i = 0;
        while (i < payload.Length)
        {
            var c = payload[i];
            if (c == '<')
            {
                var close = payload.IndexOf('>', i + 1);
                if (close < 0)
                {
                    if (srt)
                    {
                        state.Append(c);
                        i++;
                        continue;
                    }
                    // WebVTT: an unterminated tag runs to the end of the payload and is dropped.
                    break;
                }
                if (HandleTag(payload.AsSpan(i + 1, close - i - 1), srt, state))
                {
                    i = close + 1;
                    continue;
                }
                state.Append(c);
                i++;
                continue;
            }
            if (c == '&' && !srt && TryReference(payload, i, out var decoded, out var consumed))
            {
                state.Append(decoded);
                i += consumed;
                continue;
            }
            if (c == '{' && srt && i + 1 < payload.Length && payload[i + 1] == '\\')
            {
                var close = payload.IndexOf('}', i + 2);
                if (close >= 0)
                {
                    i = close + 1;
                    continue;
                }
            }
            // Plain text up to the next character that may start markup, appended in one go.
            var next = payload.AsSpan(i + 1).IndexOfAny(srt ? SrtSpecials : VttSpecials);
            var length = next < 0 ? payload.Length - i : next + 1;
            state.Append(payload.AsSpan(i, length));
            i += length;
        }
        return state.Finish();
    }

    /// <summary>Handles one tag (the text between the angle brackets); false when it is literal text (SRT only).</summary>
    private static bool HandleTag(ReadOnlySpan<char> tag, bool srt, State state)
    {
        if (tag.Length > 0 && tag[0] == '/')
        {
            var endName = NameOf(tag[1..].Trim());
            if (!IsKnown(endName, srt))
            {
                return !srt;
            }
            state.Pop(Normalize(endName));
            return true;
        }
        if (tag.Length > 0 && char.IsAsciiDigit(tag[0]))
        {
            // WebVTT inline timestamp (<00:01.500>): karaoke timing is not drawn.
            return !srt;
        }
        var name = NameOf(tag);
        if (!IsKnown(name, srt))
        {
            return !srt;
        }
        var normalized = Normalize(name);
        string? annotation = null;
        var space = IndexOfWhitespace(tag);
        if (space >= 0)
        {
            var rest = tag[(space + 1)..].Trim();
            annotation = rest.Length > 0 ? rest.ToString() : null;
        }
        state.Push(normalized, annotation);
        return true;
    }

    private static ReadOnlySpan<char> NameOf(ReadOnlySpan<char> tag)
    {
        var end = 0;
        while (end < tag.Length && tag[end] != '.' && !char.IsWhiteSpace(tag[end]))
        {
            end++;
        }
        return tag[..end];
    }

    private static int IndexOfWhitespace(ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsWhiteSpace(span[i]))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsKnown(ReadOnlySpan<char> name, bool srt)
    {
        if (Is(name, "b") || Is(name, "i") || Is(name, "u"))
        {
            return true;
        }
        if (srt)
        {
            return Is(name, "font");
        }
        return Is(name, "c") || Is(name, "v") || Is(name, "lang") || Is(name, "ruby") || Is(name, "rt");
    }

    private static string Normalize(ReadOnlySpan<char> name) => name.Length switch
    {
        1 => char.ToLowerInvariant(name[0]) switch
        {
            'b' => "b",
            'i' => "i",
            'u' => "u",
            'c' => "c",
            _ => "v",
        },
        2 => "rt",
        4 => Is(name, "lang") ? "lang" : Is(name, "ruby") ? "ruby" : "font",
        _ => name.ToString().ToLowerInvariant(),
    };

    private static bool Is(ReadOnlySpan<char> name, string expected) => name.Equals(expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>Decodes a WebVTT character reference at <paramref name="at"/> (<c>&amp;amp;</c>, <c>&amp;#233;</c>, <c>&amp;#xE9;</c>).</summary>
    private static bool TryReference(string text, int at, out string decoded, out int consumed)
    {
        decoded = string.Empty;
        consumed = 0;
        var end = -1;
        for (var j = at + 1; j < text.Length && j - at <= MaxReferenceLength + 1; j++)
        {
            var ch = text[j];
            if (ch == ';')
            {
                end = j;
                break;
            }
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '#')
            {
                return false;
            }
        }
        if (end < 0)
        {
            return false;
        }
        var name = text.AsSpan(at + 1, end - at - 1);
        consumed = end - at + 1;
        if (name.Length > 1 && name[0] == '#')
        {
            var hex = name[1] is 'x' or 'X';
            var digits = hex ? name[2..] : name[1..];
            if (digits.Length == 0 || !int.TryParse(digits, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            {
                return false;
            }
            decoded = code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(code) : "�";
            return true;
        }
        decoded = name switch
        {
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "nbsp" => " ",
            "lrm" => "‎",
            "rlm" => "‏",
            "quot" => "\"",
            "apos" => "'",
            _ => string.Empty,
        };
        return decoded.Length > 0;
    }

    /// <summary>Reusable parse buffers (one per file, not thread-safe).</summary>
    internal sealed class State
    {
        private readonly List<(string Name, string? Annotation)> _open = [];
        private readonly List<VideoCueRun> _runs = [];
        private readonly StringBuilder _text = new();
        private readonly StringBuilder _run = new();
        private bool _bold;
        private bool _italic;
        private bool _underline;
        private string? _voice;

        public void Reset()
        {
            _open.Clear();
            _runs.Clear();
            _text.Clear();
            _run.Clear();
            _bold = _italic = _underline = false;
            _voice = null;
        }

        public void Append(char c) => _run.Append(c);

        public void Append(string s) => _run.Append(s);

        public void Append(ReadOnlySpan<char> s) => _run.Append(s);

        public void Push(string name, string? annotation)
        {
            if (name == "font")
            {
                // SRT <font color=...>: ignored, but kept on the stack so its end tag matches.
                _open.Add((name, null));
                return;
            }
            Flush();
            _open.Add((name, name == "v" ? annotation : null));
            if (name == "rt")
            {
                _run.Append('(');
            }
            Recompute();
        }

        public void Pop(string name)
        {
            var at = _open.Count - 1;
            while (at >= 0 && _open[at].Name != name)
            {
                at--;
            }
            if (at < 0)
            {
                return;
            }
            Flush();
            if (name == "ruby")
            {
                // </ruby> also ends an open <rt>.
                for (var k = _open.Count - 1; k >= at; k--)
                {
                    CloseAt(k);
                }
            }
            else
            {
                // Misnested end tags (<b>a <i>b</b> c</i>) end only their own element, so "c" stays italic.
                CloseAt(at);
            }
            Recompute();
        }

        public (string Text, IReadOnlyList<VideoCueRun> Runs) Finish()
        {
            for (var k = _open.Count - 1; k >= 0; k--)
            {
                if (_open[k].Name == "rt")
                {
                    _run.Append(')');
                }
            }
            Flush();
            if (_runs.Count == 1)
            {
                // The usual cue: one run whose text is the whole text; share the string.
                return (_runs[0].Text, [_runs[0]]);
            }
            return (_text.ToString(), _runs.ToArray());
        }

        private void CloseAt(int k)
        {
            if (_open[k].Name == "rt")
            {
                _run.Append(')');
                Flush();
            }
            _open.RemoveAt(k);
        }

        private void Recompute()
        {
            _bold = _italic = _underline = false;
            _voice = null;
            foreach (var (name, annotation) in _open)
            {
                switch (name)
                {
                    case "b":
                        _bold = true;
                        break;
                    case "i":
                        _italic = true;
                        break;
                    case "u":
                        _underline = true;
                        break;
                    case "v":
                        _voice = annotation;
                        break;
                }
            }
        }

        private void Flush()
        {
            if (_run.Length == 0)
            {
                return;
            }
            var text = _run.ToString();
            _run.Clear();
            _text.Append(text);
            if (_runs.Count > 0)
            {
                var last = _runs[^1];
                if (last.IsBold == _bold && last.IsItalic == _italic && last.IsUnderline == _underline && last.Voice == _voice)
                {
                    _runs[^1] = last with { Text = last.Text + text };
                    return;
                }
            }
            _runs.Add(new VideoCueRun(text, _bold, _italic, _underline, _voice));
        }
    }
}

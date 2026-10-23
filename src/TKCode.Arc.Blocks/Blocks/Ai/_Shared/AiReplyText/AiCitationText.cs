using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Turns the numbered citations of an AI reply, <c>[1]</c>, into StreamMark's inline citation component,
/// <c>&lt;Citation id="1" /&gt;</c>, before the text reaches the Markdown view. Pure text in, text out.
/// </summary>
/// <remarks>
/// A citation is <c>[n]</c> with 1–3 digits and no leading zero. It stays text when it is escaped (<c>\[1]</c>), when it
/// is a link or image text (<c>[1](…)</c>, <c>![1](…)</c>), a link reference definition at the start of a line
/// (<c>[1]: …</c>) or the label of a reference link (<c>[docs][1]</c>), and anywhere inside inline code or a fenced code
/// block. Adjacent citations (<c>[1][2]</c>) are two citations. Streaming: <see cref="Scanner"/> takes chunks of any size
/// and holds back only what the next characters decide (an opening <c>[</c> and its digits, the character after the
/// <c>]</c>, a run of backticks or tildes); every split of a text gives the same result as converting it whole.
/// </remarks>
internal static class AiCitationText
{
    /// <summary>The longest citation number in digits.</summary>
    public const int MaxDigits = 3;

    private const string TagStart = "<Citation id=\"";
    private const string KeyAttribute = "\" key=\"";
    private const string TagEnd = "\" />";

    /// <summary>Converts a whole text (a finished reply).</summary>
    public static string Convert(string text)
    {
        var scanner = new Scanner();
        return scanner.Push(text) + scanner.Flush();
    }

    /// <summary>The component text for citation <paramref name="number"/>; <paramref name="key"/> numbers it within the reply.</summary>
    public static string Tag(int number, int? key = null)
    {
        var n = number.ToString(CultureInfo.InvariantCulture);
        return key is { } k
            ? TagStart + n + KeyAttribute + k.ToString(CultureInfo.InvariantCulture) + TagEnd
            : TagStart + n + TagEnd;
    }

    /// <summary>
    /// Finds the next keyed tag (as written by a <see cref="Scanner"/> with <see cref="Scanner.EmitsKeys"/>) at or after
    /// <paramref name="from"/>. Tags without a key (written by the model itself) are not reported.
    /// </summary>
    public static bool FindKeyedTag(string text, int from, out int start, out int length, out int number, out int key)
    {
        start = length = number = key = 0;
        var index = from;
        while ((index = text.IndexOf(TagStart, index, StringComparison.Ordinal)) >= 0)
        {
            var i = index + TagStart.Length;
            if (ReadNumber(text, ref i, out number) && string.CompareOrdinal(text, i, KeyAttribute, 0, KeyAttribute.Length) == 0)
            {
                i += KeyAttribute.Length;
                if (ReadNumber(text, ref i, out key) && string.CompareOrdinal(text, i, TagEnd, 0, TagEnd.Length) == 0)
                {
                    start = index;
                    length = i + TagEnd.Length - index;
                    return true;
                }
            }
            index += TagStart.Length;
        }
        return false;
    }

    private static bool ReadNumber(string text, ref int i, out int value)
    {
        value = 0;
        var begin = i;
        while (i < text.Length && i - begin < 9 && char.IsAsciiDigit(text[i]))
        {
            value = value * 10 + (text[i] - '0');
            i++;
        }
        return i > begin;
    }

    /// <summary>
    /// The streaming converter: <see cref="Push"/> returns the converted text that is decided so far, <see cref="Flush"/>
    /// the rest at the end of the stream. Remembers whether it is inside a code span or a fenced block across chunks.
    /// </summary>
    internal sealed class Scanner
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _pending = new();
        private readonly List<(int Number, int Key)> _citations = [];
        private Hold _hold;
        // Bracket hold: digits read, whether the ']' arrived, whether '[' opened the line.
        private int _digits;
        private bool _closed;
        private bool _bracketAtLineStart;
        // Run hold: the repeated character and whether the run opened the line.
        private char _runChar;
        private bool _runAtLineStart;
        // FenceInfo hold: the length of the backtick run that may open a fence.
        private int _infoRun;
        // Block state.
        private bool _atLineStart = true;
        private bool _lineBlank = true;
        private int _codeSpan;
        private char _fenceChar;
        private int _fenceLength;
        private Closer _closer;
        private int _closerRun;
        // The last emitted character, how many backslashes end the output, and whether a citation was just emitted.
        private char _previous = '\n';
        private int _backslashes;
        private bool _afterCitation;

        /// <summary>Whether tags carry a running <c>key</c> attribute (0, 1, 2… in the order the citations appear).</summary>
        public bool EmitsKeys { get; init; }

        /// <summary>The citations converted so far, in order: their number and key.</summary>
        public IReadOnlyList<(int Number, int Key)> Citations => _citations;

        /// <summary>True while text is held back until more arrives.</summary>
        public bool HasPending => _pending.Length > 0;

        /// <summary>True inside a fenced code block.</summary>
        public bool IsInFence => _fenceChar != '\0';

        /// <summary>Converts a chunk; returns the text that is decided (possibly empty).</summary>
        public string Push(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return string.Empty;
            }
            foreach (var c in chunk)
            {
                Process(c);
            }
            return TakeOutput();
        }

        /// <summary>Ends the stream: a held citation is converted, anything else held is released as text.</summary>
        public string Flush()
        {
            if (_hold == Hold.Bracket && _closed)
            {
                EmitCitation();
            }
            else if (_hold == Hold.Run)
            {
                ResolveRun();
            }
            if (_hold is Hold.Bracket or Hold.FenceInfo)
            {
                ReleaseLiteral();
            }
            return TakeOutput();
        }

        /// <summary>Forgets everything, for a new reply (keys start again at 0).</summary>
        public void Reset()
        {
            _output.Clear();
            _pending.Clear();
            _citations.Clear();
            _hold = Hold.None;
            _digits = 0;
            _closed = false;
            _atLineStart = true;
            _lineBlank = true;
            _codeSpan = 0;
            _fenceChar = '\0';
            _fenceLength = 0;
            _closer = Closer.Invalid;
            _closerRun = 0;
            _previous = '\n';
            _backslashes = 0;
            _afterCitation = false;
        }

        private string TakeOutput()
        {
            if (_output.Length == 0)
            {
                return string.Empty;
            }
            var text = _output.ToString();
            _output.Clear();
            return text;
        }

        private void Process(char c)
        {
            if (_hold == Hold.Bracket)
            {
                if (!_closed)
                {
                    if (char.IsAsciiDigit(c) && _digits < MaxDigits && (_digits > 0 || c != '0'))
                    {
                        _pending.Append(c);
                        _digits++;
                        return;
                    }
                    if (c == ']' && _digits > 0)
                    {
                        _pending.Append(c);
                        _closed = true;
                        return;
                    }
                    ReleaseLiteral();
                }
                else if (c == '(' || (c == ':' && _bracketAtLineStart))
                {
                    // Link or image text, or a link reference definition.
                    ReleaseLiteral();
                }
                else
                {
                    EmitCitation();
                }
            }
            else if (_hold == Hold.Run)
            {
                if (c == _runChar)
                {
                    _pending.Append(c);
                    return;
                }
                ResolveRun();
            }
            if (_hold == Hold.FenceInfo)
            {
                ProcessFenceInfo(c);
                return;
            }
            if (_fenceChar != '\0')
            {
                ProcessFenced(c);
            }
            else
            {
                ProcessText(c);
            }
        }

        private void ProcessText(char c)
        {
            if (c == '\n')
            {
                if (_lineBlank)
                {
                    // A blank line ends the paragraph: a code span still open was literal backticks.
                    _codeSpan = 0;
                }
                Emit(c);
                _atLineStart = true;
                _lineBlank = true;
                return;
            }
            if (_atLineStart && c is ' ' or '\t' or '>' or '\r')
            {
                Emit(c);
                return;
            }
            if (c == '`' || (c == '~' && _atLineStart))
            {
                _hold = Hold.Run;
                _runChar = c;
                _runAtLineStart = _atLineStart;
                _pending.Append(c);
                _atLineStart = false;
                _lineBlank = false;
                return;
            }
            var lineStart = _atLineStart;
            _atLineStart = false;
            _lineBlank = false;
            if (_codeSpan == 0 && c == '[' && _backslashes % 2 == 0 && !(_previous == ']' && !_afterCitation))
            {
                _hold = Hold.Bracket;
                _digits = 0;
                _closed = false;
                _bracketAtLineStart = lineStart;
                _pending.Append(c);
                return;
            }
            Emit(c);
        }

        private void ProcessFenced(char c)
        {
            Emit(c);
            if (c == '\n')
            {
                if (_closer == Closer.Valid || (_closer == Closer.Run && _closerRun >= _fenceLength))
                {
                    _fenceChar = '\0';
                    _fenceLength = 0;
                    _codeSpan = 0;
                }
                _closer = Closer.AtStart;
                _closerRun = 0;
                _atLineStart = true;
                _lineBlank = true;
                return;
            }
            _closer = _closer switch
            {
                Closer.AtStart when c is ' ' or '\t' or '>' or '\r' => Closer.AtStart,
                Closer.AtStart when c == _fenceChar => Closer.Run,
                Closer.Run when c == _fenceChar => Closer.Run,
                Closer.Run when c is ' ' or '\t' or '\r' => _closerRun >= _fenceLength ? Closer.Valid : Closer.Invalid,
                Closer.Valid when c is ' ' or '\t' or '\r' => Closer.Valid,
                _ => Closer.Invalid
            };
            if (_closer == Closer.Run)
            {
                _closerRun++;
            }
        }

        /// <summary>A run of backticks (or tildes at the start of a line) is complete.</summary>
        private void ResolveRun()
        {
            var length = _pending.Length;
            if (_runAtLineStart && length >= 3 && _runChar == '`')
            {
                // Maybe a fence opener: only if its info string has no backtick, so the line is held until it ends.
                _hold = Hold.FenceInfo;
                _infoRun = length;
                return;
            }
            var text = _pending.ToString();
            _pending.Clear();
            _hold = Hold.None;
            Emit(text);
            if (_runAtLineStart && length >= 3)
            {
                OpenFence(_runChar, length);
                return;
            }
            if (_runChar == '`')
            {
                ToggleCodeSpan(length);
            }
        }

        /// <summary>The line after a backtick fence opener: a newline confirms the fence, a backtick makes it inline code.</summary>
        private void ProcessFenceInfo(char c)
        {
            if (c != '\n' && c != '`')
            {
                _pending.Append(c);
                return;
            }
            var text = _pending.ToString();
            _pending.Clear();
            _hold = Hold.None;
            if (c == '\n')
            {
                Emit(text);
                OpenFence('`', _infoRun);
                ProcessFenced(c);
                return;
            }
            // Not a fence: the run opens (or closes) a code span and the rest of the line is read again as text.
            Emit(text[.._infoRun]);
            ToggleCodeSpan(_infoRun);
            foreach (var ch in text.AsSpan(_infoRun))
            {
                Process(ch);
            }
            Process(c);
        }

        /// <summary>The rest of the opener's line is the info string, then code until the closing fence.</summary>
        private void OpenFence(char fence, int length)
        {
            _fenceChar = fence;
            _fenceLength = length;
            _codeSpan = 0;
            _closer = Closer.Invalid;
        }

        private void ToggleCodeSpan(int length)
        {
            if (_codeSpan == 0)
            {
                _codeSpan = length;
            }
            else if (_codeSpan == length)
            {
                _codeSpan = 0;
            }
        }

        private void EmitCitation()
        {
            var number = int.Parse(_pending.ToString(1, _digits), NumberStyles.None, CultureInfo.InvariantCulture);
            _pending.Clear();
            _hold = Hold.None;
            int? key = EmitsKeys ? _citations.Count : null;
            _citations.Add((number, key ?? _citations.Count));
            Emit(Tag(number, key));
            _previous = ']';
            _afterCitation = true;
        }

        private void ReleaseLiteral()
        {
            var text = _pending.ToString();
            _pending.Clear();
            _hold = Hold.None;
            Emit(text);
        }

        private void Emit(char c)
        {
            _output.Append(c);
            _backslashes = c == '\\' ? _backslashes + 1 : 0;
            _previous = c;
            _afterCitation = false;
        }

        private void Emit(string text)
        {
            foreach (var c in text)
            {
                Emit(c);
            }
        }

        private enum Hold
        {
            None,

            /// <summary>An opening <c>[</c>, its digits and maybe the <c>]</c>; the next character decides.</summary>
            Bracket,

            /// <summary>A run of backticks, or of tildes at the start of a line; its length decides.</summary>
            Run,

            /// <summary>A backtick fence opener and its info string; the end of the line decides.</summary>
            FenceInfo
        }

        /// <summary>Whether the current line of a fenced block can still be its closing fence.</summary>
        private enum Closer
        {
            Invalid,
            AtStart,
            Run,
            Valid
        }
    }
}

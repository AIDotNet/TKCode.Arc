using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc.A2UI;

/// <summary>
/// The A2UI regex dialect: a portable subset of ECMAScript patterns in Unicode mode, checked for catastrophic
/// backtracking before use, then translated to a .NET pattern with the same meaning (ASCII <c>\d \w \b</c>,
/// <c>.</c> without line terminators, <c>$</c> only at the very end, search semantics).
/// </summary>
public static class A2UIRegex
{
    /// <summary>The longest pattern accepted, in code points.</summary>
    public const int MaxPatternLength = 256;

    /// <summary>Values longer than this many code points never match.</summary>
    public const int MaxValueLength = 4_096;

    private static readonly ConcurrentDictionary<string, Regex> s_cache = new(StringComparer.Ordinal);

    /// <summary>
    /// True when <paramref name="pattern"/> matches anywhere in <paramref name="value"/>. Throws
    /// <see cref="A2UIException"/> (<c>EXPRESSION_ERROR</c>) for a pattern outside the dialect or failing the safety check.
    /// </summary>
    public static bool IsMatch(string pattern, string value)
    {
        var regex = Compile(pattern);
        if (CodePointCount(value) > MaxValueLength)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>Checks and translates a pattern (cached). Throws <see cref="A2UIException"/> when it is not allowed.</summary>
    public static Regex Compile(string pattern)
    {
        if (s_cache.TryGetValue(pattern, out var cached))
        {
            return cached;
        }

        if (CodePointCount(pattern) > MaxPatternLength)
        {
            throw Error($"PATTERN_TOO_LONG: the pattern is longer than {MaxPatternLength} code points.");
        }

        var ast = new Parser(pattern).Parse();
        Safety.Check(ast);
        var dotnet = Translator.Translate(ast);
        Regex regex;
        try
        {
            regex = new Regex(dotnet, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException ex)
        {
            throw Error($"Invalid pattern: {ex.Message}");
        }

        if (s_cache.Count > 256)
        {
            s_cache.Clear();
        }

        s_cache[pattern] = regex;
        return regex;
    }

    private static int CodePointCount(string s)
    {
        var count = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static A2UIException Error(string message) => A2UIException.Expression("Invalid regex: " + message);

    // ---- AST ------------------------------------------------------------------------------------------------

    private abstract class Node;

    private sealed class Alternation(List<Node> branches) : Node
    {
        public List<Node> Branches { get; } = branches;
    }

    private sealed class Sequence(List<Node> items) : Node
    {
        public List<Node> Items { get; } = items;
    }

    private sealed class Group(Node body, GroupKind kind) : Node
    {
        public Node Body { get; } = body;
        public GroupKind Kind { get; } = kind;
    }

    private enum GroupKind
    {
        Capturing,
        NonCapturing,
        Lookahead,
        NegativeLookahead
    }

    private sealed class Quantifier(Node atom, int min, int max, bool lazy) : Node
    {
        public Node Atom { get; } = atom;
        public int Min { get; } = min;
        public int Max { get; } = max; // -1 = unbounded
        public bool Lazy { get; } = lazy;
        public bool Unbounded => Max < 0;
    }

    private sealed class CharAtom(CharSet set, string dotnet, bool astral) : Node
    {
        public CharSet Set { get; } = set;
        public string DotNet { get; } = dotnet;
        public bool Astral { get; } = astral; // translates to more than one UTF-16 unit
    }

    private sealed class Assertion(string dotnet) : Node
    {
        public string DotNet { get; } = dotnet;
    }

    // A set of code points as sorted, disjoint inclusive ranges.
    private sealed class CharSet
    {
        public static readonly CharSet Empty = new([]);

        public CharSet(List<(int Lo, int Hi)> ranges)
        {
            Ranges = Normalize(ranges);
        }

        public List<(int Lo, int Hi)> Ranges { get; }

        public static CharSet Of(int c) => new([(c, c)]);

        public static CharSet Range(int lo, int hi) => new([(lo, hi)]);

        public CharSet Union(CharSet other)
        {
            var list = new List<(int, int)>(Ranges);
            list.AddRange(other.Ranges);
            return new CharSet(list);
        }

        public CharSet Complement()
        {
            var list = new List<(int, int)>();
            var next = 0;
            foreach (var (lo, hi) in Ranges)
            {
                if (lo > next)
                {
                    list.Add((next, lo - 1));
                }

                next = hi + 1;
            }

            if (next <= 0x10FFFF)
            {
                list.Add((next, 0x10FFFF));
            }

            return new CharSet(list);
        }

        public bool Overlaps(CharSet other)
        {
            int i = 0, j = 0;
            while (i < Ranges.Count && j < other.Ranges.Count)
            {
                var (alo, ahi) = Ranges[i];
                var (blo, bhi) = other.Ranges[j];
                if (alo <= bhi && blo <= ahi)
                {
                    return true;
                }

                if (ahi < bhi)
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return false;
        }

        private static List<(int, int)> Normalize(List<(int Lo, int Hi)> ranges)
        {
            if (ranges.Count <= 1)
            {
                return ranges;
            }

            ranges.Sort((a, b) => a.Lo.CompareTo(b.Lo));
            var result = new List<(int, int)>();
            var (lo, hi) = ranges[0];
            for (var i = 1; i < ranges.Count; i++)
            {
                var r = ranges[i];
                if (r.Lo <= hi + 1)
                {
                    hi = Math.Max(hi, r.Hi);
                }
                else
                {
                    result.Add((lo, hi));
                    (lo, hi) = r;
                }
            }

            result.Add((lo, hi));
            return result;
        }
    }

    private static readonly CharSet s_digit = CharSet.Range('0', '9');
    private static readonly CharSet s_word = new([('0', '9'), ('A', 'Z'), ('a', 'z'), ('_', '_')]);

    private static readonly CharSet s_space = new([
        (0x09, 0x0D), (0x20, 0x20), (0xA0, 0xA0), (0x1680, 0x1680), (0x2000, 0x200A), (0x2028, 0x2029), (0x202F, 0x202F),
        (0x205F, 0x205F), (0x3000, 0x3000), (0xFEFF, 0xFEFF)
    ]);

    private static readonly CharSet s_dot = new([(0x0A, 0x0A), (0x0D, 0x0D), (0x2028, 0x2029)]);

    private const string DigitNet = "[0-9]";
    private const string NotDigitNet = "[^0-9]";
    private const string WordNet = "[A-Za-z0-9_]";
    private const string NotWordNet = "[^A-Za-z0-9_]";
    private const string SpaceClassBody = @"\t\n\v\f\r    -     　﻿";
    private const string DotNet = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\n\r  ])";
    private const string WordBoundaryNet = "(?:(?<=[A-Za-z0-9_])(?![A-Za-z0-9_])|(?<![A-Za-z0-9_])(?=[A-Za-z0-9_]))";
    private const string NotWordBoundaryNet = "(?:(?<=[A-Za-z0-9_])(?=[A-Za-z0-9_])|(?<![A-Za-z0-9_])(?![A-Za-z0-9_]))";

    // ---- Parser ---------------------------------------------------------------------------------------------

    private sealed class Parser(string pattern)
    {
        private int _pos;

        public Node Parse()
        {
            var node = ParseAlternation();
            if (_pos < pattern.Length)
            {
                throw Error($"Unexpected '{pattern[_pos]}' at {_pos}.");
            }

            return node;
        }

        private Node ParseAlternation()
        {
            var branches = new List<Node> { ParseSequence() };
            while (_pos < pattern.Length && pattern[_pos] == '|')
            {
                _pos++;
                branches.Add(ParseSequence());
            }

            return branches.Count == 1 ? branches[0] : new Alternation(branches);
        }

        private Node ParseSequence()
        {
            var items = new List<Node>();
            while (_pos < pattern.Length && pattern[_pos] is not ('|' or ')'))
            {
                var atom = ParseAtom(out var quantifiable);
                atom = ParseQuantifier(atom, quantifiable);
                items.Add(atom);
            }

            return new Sequence(items);
        }

        private Node ParseQuantifier(Node atom, bool quantifiable)
        {
            if (_pos >= pattern.Length)
            {
                return atom;
            }

            int min, max;
            var c = pattern[_pos];
            switch (c)
            {
                case '*':
                    min = 0;
                    max = -1;
                    _pos++;
                    break;
                case '+':
                    min = 1;
                    max = -1;
                    _pos++;
                    break;
                case '?':
                    min = 0;
                    max = 1;
                    _pos++;
                    break;
                case '{':
                    if (!TryBraces(out min, out max))
                    {
                        throw Error("A lone '{' must be escaped.");
                    }

                    break;
                default:
                    return atom;
            }

            if (!quantifiable)
            {
                throw Error("Nothing to repeat.");
            }

            var lazy = false;
            if (_pos < pattern.Length && pattern[_pos] == '?')
            {
                lazy = true;
                _pos++;
            }

            if (_pos < pattern.Length && pattern[_pos] is '*' or '+' or '?' or '{')
            {
                throw Error("Nothing to repeat.");
            }

            return new Quantifier(atom, min, max, lazy);
        }

        private bool TryBraces(out int min, out int max)
        {
            min = max = 0;
            var start = _pos;
            _pos++;
            var a = Digits();
            if (a is null)
            {
                _pos = start;
                return false;
            }

            min = a.Value;
            max = min;
            if (_pos < pattern.Length && pattern[_pos] == ',')
            {
                _pos++;
                var b = Digits();
                max = b ?? -1;
            }

            if (_pos >= pattern.Length || pattern[_pos] != '}')
            {
                _pos = start;
                return false;
            }

            _pos++;
            if (max >= 0 && max < min)
            {
                throw Error("Quantifier range out of order.");
            }

            return true;
        }

        private int? Digits()
        {
            var start = _pos;
            while (_pos < pattern.Length && char.IsAsciiDigit(pattern[_pos]))
            {
                _pos++;
            }

            if (_pos == start)
            {
                return null;
            }

            return int.TryParse(pattern.AsSpan(start, _pos - start), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? Math.Min(v, 100_000) : 100_000;
        }

        private Node ParseAtom(out bool quantifiable)
        {
            quantifiable = true;
            var c = pattern[_pos];
            switch (c)
            {
                case '^':
                    _pos++;
                    quantifiable = false;
                    return new Assertion("^");
                case '$':
                    _pos++;
                    quantifiable = false;
                    return new Assertion(@"\z");
                case '.':
                    _pos++;
                    return new CharAtom(s_dot.Complement(), DotNet, true);
                case '(':
                    return ParseGroup(out quantifiable);
                case '[':
                    return ParseClass();
                case '\\':
                    return ParseEscape(out quantifiable);
                case '*' or '+' or '?':
                    throw Error("Nothing to repeat.");
                case '{':
                    if (TryBraces(out _, out _))
                    {
                        throw Error("Nothing to repeat.");
                    }

                    throw Error("A lone '{' must be escaped.");
                case '}' or ']':
                    throw Error($"A lone '{c}' must be escaped.");
            }

            var rune = ReadRune();
            return Literal(rune);
        }

        private Rune ReadRune()
        {
            if (Rune.DecodeFromUtf16(pattern.AsSpan(_pos), out var rune, out var width) != System.Buffers.OperationStatus.Done)
            {
                rune = new Rune(0xFFFD);
                width = 1;
            }

            _pos += width;
            return rune;
        }

        private Node ParseGroup(out bool quantifiable)
        {
            _pos++;
            var kind = GroupKind.Capturing;
            if (_pos < pattern.Length && pattern[_pos] == '?')
            {
                var rest = pattern.AsSpan(_pos);
                if (rest.StartsWith("?:"))
                {
                    kind = GroupKind.NonCapturing;
                    _pos += 2;
                }
                else if (rest.StartsWith("?="))
                {
                    kind = GroupKind.Lookahead;
                    _pos += 2;
                }
                else if (rest.StartsWith("?!"))
                {
                    kind = GroupKind.NegativeLookahead;
                    _pos += 2;
                }
                else if (rest.StartsWith("?<=") || rest.StartsWith("?<!"))
                {
                    throw Error("UNSUPPORTED_SYNTAX: Lookbehind is not allowed.");
                }
                else if (rest.StartsWith("?<"))
                {
                    throw Error("UNSUPPORTED_SYNTAX: Named groups are not allowed.");
                }
                else
                {
                    throw Error("UNSUPPORTED_SYNTAX: Inline flags and other group modifiers are not allowed.");
                }
            }

            var body = ParseAlternation();
            if (_pos >= pattern.Length || pattern[_pos] != ')')
            {
                throw Error("Unterminated group.");
            }

            _pos++;
            quantifiable = kind is GroupKind.Capturing or GroupKind.NonCapturing;
            return new Group(body, kind);
        }

        private Node ParseEscape(out bool quantifiable)
        {
            quantifiable = true;
            _pos++;
            if (_pos >= pattern.Length)
            {
                throw Error("Pattern ends with '\\'.");
            }

            var c = pattern[_pos++];
            switch (c)
            {
                case 'd':
                    return new CharAtom(s_digit, DigitNet, false);
                case 'D':
                    return new CharAtom(s_digit.Complement(), NotDigitNet, false);
                case 'w':
                    return new CharAtom(s_word, WordNet, false);
                case 'W':
                    return new CharAtom(s_word.Complement(), NotWordNet, false);
                case 's':
                    return new CharAtom(s_space, "[" + SpaceClassBody + "]", false);
                case 'S':
                    return new CharAtom(s_space.Complement(), "[^" + SpaceClassBody + "]", false);
                case 'b':
                    quantifiable = false;
                    return new Assertion(WordBoundaryNet);
                case 'B':
                    quantifiable = false;
                    return new Assertion(NotWordBoundaryNet);
            }

            _pos--;
            return Literal(new Rune(ReadCharacterEscape(inClass: false)));
        }

        // After the backslash: an escape that stands for one code point.
        private int ReadCharacterEscape(bool inClass)
        {
            var c = pattern[_pos++];
            switch (c)
            {
                case 't':
                    return '\t';
                case 'n':
                    return '\n';
                case 'v':
                    return '\v';
                case 'f':
                    return '\f';
                case 'r':
                    return '\r';
                case '0':
                    if (_pos < pattern.Length && char.IsAsciiDigit(pattern[_pos]))
                    {
                        throw Error("UNSUPPORTED_SYNTAX: Octal escapes are not allowed.");
                    }

                    return 0;
                case 'x':
                    return Hex(2, 2);
                case 'u':
                    if (_pos < pattern.Length && pattern[_pos] == '{')
                    {
                        _pos++;
                        var start = _pos;
                        while (_pos < pattern.Length && Uri.IsHexDigit(pattern[_pos]))
                        {
                            _pos++;
                        }

                        if (_pos == start || _pos >= pattern.Length || pattern[_pos] != '}' || _pos - start > 6)
                        {
                            throw Error("Invalid \\u{...} escape.");
                        }

                        var value = int.Parse(pattern.AsSpan(start, _pos - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        _pos++;
                        if (value > 0x10FFFF)
                        {
                            throw Error("Code point out of range.");
                        }

                        return value;
                    }

                    var unit = Hex(4, 4);
                    // A surrogate pair written as two \u escapes is one code point.
                    if (char.IsHighSurrogate((char)unit) && pattern.AsSpan(_pos).StartsWith("\\u") && _pos + 6 <= pattern.Length)
                    {
                        var save = _pos;
                        _pos += 2;
                        var low = Hex(4, 4);
                        if (char.IsLowSurrogate((char)low))
                        {
                            return char.ConvertToUtf32((char)unit, (char)low);
                        }

                        _pos = save;
                    }

                    return unit;
                case 'c':
                    throw Error("UNSUPPORTED_SYNTAX: \\cX escapes are not allowed.");
                case 'p' or 'P':
                    throw Error("UNSUPPORTED_SYNTAX: Unicode property escapes are not allowed.");
                case 'k':
                    throw Error("UNSUPPORTED_SYNTAX: Backreferences are not allowed.");
                case 'b' when inClass:
                    return '\b';
                case '-' when inClass:
                    return '-';
            }

            if (c is >= '1' and <= '9')
            {
                throw Error("UNSUPPORTED_SYNTAX: Backreferences are not allowed.");
            }

            if ("^$\\.*+?()[]{}|/".Contains(c))
            {
                return c;
            }

            throw Error($"Invalid escape '\\{c}'.");
        }

        private int Hex(int minDigits, int maxDigits)
        {
            var start = _pos;
            while (_pos < pattern.Length && _pos - start < maxDigits && Uri.IsHexDigit(pattern[_pos]))
            {
                _pos++;
            }

            if (_pos - start < minDigits)
            {
                throw Error("Invalid hexadecimal escape.");
            }

            return int.Parse(pattern.AsSpan(start, _pos - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private Node ParseClass()
        {
            _pos++;
            var negated = false;
            if (_pos < pattern.Length && pattern[_pos] == '^')
            {
                negated = true;
                _pos++;
            }

            var ranges = new List<(int, int)>();
            var net = new StringBuilder();
            var astral = false;
            while (true)
            {
                if (_pos >= pattern.Length)
                {
                    throw Error("Unterminated character class.");
                }

                if (pattern[_pos] == ']')
                {
                    _pos++;
                    break;
                }

                var (lo, loNet, isSet) = ClassAtom(ranges);
                if (isSet)
                {
                    net.Append(loNet);
                    continue;
                }

                if (_pos + 1 < pattern.Length && pattern[_pos] == '-' && pattern[_pos + 1] != ']')
                {
                    _pos++;
                    var (hi, hiNet, hiIsSet) = ClassAtom(ranges);
                    if (hiIsSet)
                    {
                        throw Error("Invalid character class range.");
                    }

                    if (hi < lo)
                    {
                        throw Error("Character class range out of order.");
                    }

                    ranges.Add((lo, hi));
                    astral |= hi > 0xFFFF;
                    net.Append(loNet).Append('-').Append(hiNet);
                }
                else
                {
                    ranges.Add((lo, lo));
                    astral |= lo > 0xFFFF;
                    net.Append(loNet);
                }
            }

            var set = new CharSet(ranges);
            if (astral)
            {
                // Classes with code points above U+FFFF: only the simple, non-negated form is translated.
                if (negated)
                {
                    throw Error("Negated classes with code points above U+FFFF are not supported.");
                }

                return new CharAtom(set, AstralClass(set), true);
            }

            var body = net.ToString();
            if (body.Length == 0)
            {
                return negated ? new CharAtom(CharSet.Empty.Complement(), @"[\s\S]", false) : new CharAtom(CharSet.Empty, "(?!)", false);
            }

            return new CharAtom(negated ? set.Complement() : set, negated ? "[^" + body + "]" : "[" + body + "]", false);
        }

        // One class member: a code point (value, .NET text, false) or a class escape added to ranges (0, text, true).
        private (int Value, string Net, bool IsSet) ClassAtom(List<(int, int)> ranges)
        {
            var c = pattern[_pos];
            if (c == '\\')
            {
                _pos++;
                if (_pos >= pattern.Length)
                {
                    throw Error("Pattern ends with '\\'.");
                }

                var e = pattern[_pos];
                CharSet? set = e switch
                {
                    'd' => s_digit,
                    'D' => s_digit.Complement(),
                    'w' => s_word,
                    'W' => s_word.Complement(),
                    's' => s_space,
                    'S' => s_space.Complement(),
                    _ => null
                };
                if (set is not null)
                {
                    _pos++;
                    ranges.AddRange(set.Ranges);
                    var net = e switch
                    {
                        'd' => "0-9",
                        'D' => @"\x00-/:-￿",
                        'w' => "A-Za-z0-9_",
                        'W' => @"\x00-/:-@\[-^`{-￿",
                        's' => SpaceClassBody,
                        _ => @"\P{Zs}" // approximation for \S inside a class
                    };
                    return (0, net, true);
                }

                var value = ReadCharacterEscape(inClass: true);
                return (value, NetChar(value), false);
            }

            var rune = ReadRune();
            return (rune.Value, NetChar(rune.Value), false);
        }

        private static Node Literal(Rune rune)
        {
            return new CharAtom(CharSet.Of(rune.Value), EscapeNet(rune.ToString()), rune.Value > 0xFFFF);
        }
    }

    private static string NetChar(int codePoint)
    {
        if (codePoint > 0xFFFF)
        {
            return char.ConvertFromUtf32(codePoint);
        }

        return codePoint < 0x20 || codePoint is '\\' or ']' or '[' or '^' or '-' || codePoint > 0x7E
            ? "\\u" + codePoint.ToString("X4", CultureInfo.InvariantCulture)
            : ((char)codePoint).ToString();
    }

    private static string EscapeNet(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch < 0x20 || ch > 0x7E)
            {
                sb.Append("\\u").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
            }
            else if (@"\*+?|{}[]()^$.#- ".Contains(ch))
            {
                sb.Append('\\').Append(ch);
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string AstralClass(CharSet set)
    {
        var parts = new List<string>();
        var bmp = new StringBuilder();
        foreach (var (lo, hi) in set.Ranges)
        {
            if (hi <= 0xFFFF)
            {
                bmp.Append(NetChar(lo));
                if (hi > lo)
                {
                    bmp.Append('-').Append(NetChar(hi));
                }

                continue;
            }

            if (lo <= 0xFFFF)
            {
                bmp.Append(NetChar(lo)).Append('-').Append(@"￿");
            }

            var from = Math.Max(lo, 0x10000);
            if (hi - from > 4096)
            {
                throw Error("Character class range above U+FFFF is too wide.");
            }

            for (var cp = from; cp <= hi; cp++)
            {
                parts.Add(EscapeNet(char.ConvertFromUtf32(cp)));
            }
        }

        if (bmp.Length > 0)
        {
            parts.Add("[" + bmp + "]");
        }

        return "(?:" + string.Join("|", parts) + ")";
    }

    // ---- Safety check ---------------------------------------------------------------------------------------

    private static class Safety
    {
        public static void Check(Node node)
        {
            switch (node)
            {
                case Alternation alt:
                    foreach (var b in alt.Branches)
                    {
                        Check(b);
                    }

                    break;
                case Sequence seq:
                    foreach (var item in seq.Items)
                    {
                        Check(item);
                    }

                    CheckAdjacent(seq);
                    break;
                case Group group:
                    Check(group.Body);
                    break;
                case Quantifier q:
                    Check(q.Atom);
                    if ((q.Max < 0 || q.Max > 1) && ContainsUnbounded(q.Atom))
                    {
                        throw Error("NESTED_QUANTIFIER: a repeated group contains an unbounded quantifier.");
                    }

                    if (q.Unbounded && Unwrap(q.Atom) is Alternation alternation)
                    {
                        var firsts = alternation.Branches.Select(First).ToList();
                        for (var i = 0; i < firsts.Count; i++)
                        {
                            for (var j = i + 1; j < firsts.Count; j++)
                            {
                                if (firsts[i].Overlaps(firsts[j]))
                                {
                                    throw Error("OVERLAPPING_ALTERNATION: an unbounded quantifier applies to alternatives that can start with the same character.");
                                }
                            }
                        }
                    }

                    break;
            }
        }

        // Rule 4: two unbounded atoms with overlapping characters, separated only by nullable items.
        private static void CheckAdjacent(Sequence seq)
        {
            var items = seq.Items;
            for (var i = 0; i < items.Count; i++)
            {
                if (!IsUnboundedAtom(items[i], out var setA))
                {
                    continue;
                }

                for (var j = i + 1; j < items.Count; j++)
                {
                    if (IsUnboundedAtom(items[j], out var setB) && setA.Overlaps(setB))
                    {
                        throw Error("ADJACENT_OVERLAP: two unbounded repeats with overlapping characters are adjacent.");
                    }

                    if (!Nullable(items[j]))
                    {
                        break;
                    }
                }
            }
        }

        private static bool IsUnboundedAtom(Node node, out CharSet set)
        {
            set = CharSet.Empty;
            if (Unwrap(node) is Quantifier { Unbounded: true } q)
            {
                set = Chars(q.Atom);
                return true;
            }

            return false;
        }

        private static Node Unwrap(Node node)
        {
            while (true)
            {
                switch (node)
                {
                    case Group { Kind: GroupKind.Capturing or GroupKind.NonCapturing } g:
                        node = g.Body;
                        continue;
                    case Sequence { Items.Count: 1 } s:
                        node = s.Items[0];
                        continue;
                    default:
                        return node;
                }
            }
        }

        private static bool ContainsUnbounded(Node node) => node switch
        {
            Quantifier q => q.Unbounded || ContainsUnbounded(q.Atom),
            Group g => g.Kind is GroupKind.Capturing or GroupKind.NonCapturing && ContainsUnbounded(g.Body),
            Sequence s => s.Items.Any(ContainsUnbounded),
            Alternation a => a.Branches.Any(ContainsUnbounded),
            _ => false
        };

        private static bool Nullable(Node node) => node switch
        {
            Assertion => true,
            Group { Kind: GroupKind.Lookahead or GroupKind.NegativeLookahead } => true,
            Group g => Nullable(g.Body),
            Quantifier q => q.Min == 0 || Nullable(q.Atom),
            Sequence s => s.Items.All(Nullable),
            Alternation a => a.Branches.Any(Nullable),
            _ => false
        };

        // Every character the node can consume.
        private static CharSet Chars(Node node) => node switch
        {
            CharAtom c => c.Set,
            Group { Kind: GroupKind.Lookahead or GroupKind.NegativeLookahead } => CharSet.Empty,
            Group g => Chars(g.Body),
            Quantifier q => Chars(q.Atom),
            Sequence s => s.Items.Aggregate(CharSet.Empty, (acc, n) => acc.Union(Chars(n))),
            Alternation a => a.Branches.Aggregate(CharSet.Empty, (acc, n) => acc.Union(Chars(n))),
            _ => CharSet.Empty
        };

        // The characters the node can start with.
        private static CharSet First(Node node)
        {
            switch (node)
            {
                case CharAtom c:
                    return c.Set;
                case Group { Kind: GroupKind.Lookahead or GroupKind.NegativeLookahead }:
                    return CharSet.Empty;
                case Group g:
                    return First(g.Body);
                case Quantifier q:
                    return First(q.Atom);
                case Alternation a:
                    return a.Branches.Aggregate(CharSet.Empty, (acc, n) => acc.Union(First(n)));
                case Sequence s:
                    var set = CharSet.Empty;
                    foreach (var item in s.Items)
                    {
                        set = set.Union(First(item));
                        if (!Nullable(item))
                        {
                            break;
                        }
                    }

                    return set;
                default:
                    return CharSet.Empty;
            }
        }
    }

    // ---- Translation ----------------------------------------------------------------------------------------

    private static class Translator
    {
        public static string Translate(Node node)
        {
            var sb = new StringBuilder();
            Write(node, sb);
            return sb.ToString();
        }

        private static void Write(Node node, StringBuilder sb)
        {
            switch (node)
            {
                case Alternation alt:
                    for (var i = 0; i < alt.Branches.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append('|');
                        }

                        Write(alt.Branches[i], sb);
                    }

                    break;
                case Sequence seq:
                    foreach (var item in seq.Items)
                    {
                        Write(item, sb);
                    }

                    break;
                case Group g:
                    sb.Append(g.Kind switch
                    {
                        GroupKind.Capturing => "(",
                        GroupKind.NonCapturing => "(?:",
                        GroupKind.Lookahead => "(?=",
                        _ => "(?!"
                    });
                    Write(g.Body, sb);
                    sb.Append(')');
                    break;
                case Quantifier q:
                    if (q.Atom is CharAtom { Astral: true } || q.Atom is Sequence or Alternation)
                    {
                        sb.Append("(?:");
                        Write(q.Atom, sb);
                        sb.Append(')');
                    }
                    else
                    {
                        Write(q.Atom, sb);
                    }

                    AppendQuantifier(sb, q.Min, q.Max);
                    if (q.Lazy)
                    {
                        sb.Append('?');
                    }

                    break;
                case CharAtom c:
                    sb.Append(c.DotNet);
                    break;
                case Assertion a:
                    sb.Append(a.DotNet);
                    break;
            }
        }
    }

    private static StringBuilder AppendQuantifier(StringBuilder sb, int min, int max)
    {
        if (min == 0 && max < 0)
        {
            return sb.Append('*');
        }

        if (min == 1 && max < 0)
        {
            return sb.Append('+');
        }

        if (min == 0 && max == 1)
        {
            return sb.Append('?');
        }

        if (max < 0)
        {
            return sb.Append('{').Append(min.ToString(CultureInfo.InvariantCulture)).Append(",}");
        }

        return min == max
            ? sb.Append('{').Append(min.ToString(CultureInfo.InvariantCulture)).Append('}')
            : sb.Append('{').Append(min.ToString(CultureInfo.InvariantCulture)).Append(',').Append(max.ToString(CultureInfo.InvariantCulture)).Append('}');
    }
}

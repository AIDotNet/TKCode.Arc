using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What a word does during a move.</summary>
internal enum CodeMagicMoveRole : byte
{
    /// <summary>It stays and glides to its new line and column.</summary>
    Kept,

    /// <summary>It fades and lifts away.</summary>
    Removed,

    /// <summary>It is typed in, one character at a time, behind the caret.</summary>
    Typed
}

/// <summary>Where every word of a snippet is when a move starts (its rest state, or wherever a move left it).</summary>
internal sealed class CodeMagicMoveScene
{
    public CodeMagicMoveScene(CodeMagicMoveSource source, CodeMagicMoveBand[] bands)
    {
        Source = source;
        var count = source.Words.Length;
        X = new double[count];
        Y = new double[count];
        Alpha = new double[count];
        for (var i = 0; i < count; i++)
        {
            X[i] = source.Words[i].Column;
            Y[i] = source.Words[i].Line;
            Alpha[i] = 1;
        }
        Lines = source.LineCount;
        Bands = bands;
        BandAlpha = new double[bands.Length];
        Array.Fill(BandAlpha, 1);
    }

    /// <summary>The words.</summary>
    public CodeMagicMoveSource Source { get; }

    /// <summary>Each word's column (fractional while it moves).</summary>
    public double[] X { get; }

    /// <summary>Each word's line (fractional while it moves).</summary>
    public double[] Y { get; }

    /// <summary>Each word's opacity; a word not typed yet has 0.</summary>
    public double[] Alpha { get; }

    /// <summary>The height in lines.</summary>
    public double Lines { get; set; }

    /// <summary>The bands on show.</summary>
    public CodeMagicMoveBand[] Bands { get; }

    /// <summary>Their opacity.</summary>
    public double[] BandAlpha { get; }
}

/// <summary>
/// One move between two versions of a snippet as a pure function of time, so it can run forwards, run back, be scrubbed
/// under a finger and be interrupted anywhere. All times are milliseconds at speed 1.
/// </summary>
/// <remarks>
/// Phases (Arc Code magic move, design reference): removed words fade and lift 0.28 of a line on the standard curve
/// over 262ms; kept words glide on the in-out curve, starting 100ms in when something was removed and 12ms later per
/// line further down, for 720ms or longer when words travel further (600ms + 35ms per line travelled); the height
/// follows on the same curve. At 60% of the glide new code starts to type in, at about 28 characters a second (a long
/// change is typed faster, so typing never takes more than 1.5s), punctuation and the first character after a gap a
/// little slower; each word fades in over 50ms and the caret, after the last character, waits 50ms and fades over
/// 300ms. Bands of the new step fade in over 220ms as typing reaches them; the old ones fade with the removed words.
/// </remarks>
internal sealed class CodeMagicMovePlan
{
    public const double RemoveMs = 262;
    public const double LiftLines = 0.28;
    public const double RemovalLeadMs = 100;
    public const double LineStaggerMs = 12;
    public const double MinGlideMs = 720;
    public const double GlideBaseMs = 600;
    public const double GlidePerLineMs = 35;
    public const double TypeStartFraction = 0.6;
    public const double TypePerCharMs = 35.5;
    public const double MaxTypeMs = 1475;
    public const double WordFadeMs = 50;
    public const double CaretHoldMs = 50;
    public const double CaretFadeMs = 300;
    public const double BandFadeMs = 220;

    private readonly double[] _fromX;
    private readonly double[] _fromY;
    private readonly double[] _fromAlpha;
    private readonly double[] _start;
    private readonly int[] _charStart;
    private readonly double[] _charTimes;
    private readonly int[] _charOrder;
    private readonly double _fromLines;
    private readonly double[] _oldBandAlpha;
    private readonly double[] _bandStart;

    /// <summary>Plans the move from <paramref name="from"/> to the rest state of <paramref name="to"/>.</summary>
    public CodeMagicMovePlan(CodeMagicMoveScene from, CodeMagicMoveSource to, CodeMagicMoveBand[] toBands)
    {
        From = from;
        To = to;
        ToBands = toBands;
        OldBands = from.Bands;
        var match = CodeMagicMoveDiff.Match(from.Source, to);

        // Words of the new version: kept (a visible old word continues them) or typed.
        var items = new List<(CodeMagicMoveRole Role, int Word, int Old)>();
        var continued = new bool[from.Source.Words.Length];
        for (var j = 0; j < to.Words.Length; j++)
        {
            var old = match[j];
            if (old >= 0 && from.Alpha[old] >= 0.5)
            {
                continued[old] = true;
                items.Add((CodeMagicMoveRole.Kept, j, old));
            }
            else
            {
                items.Add((CodeMagicMoveRole.Typed, j, -1));
            }
        }
        for (var i = 0; i < continued.Length; i++)
        {
            if (!continued[i] && from.Alpha[i] > 0)
            {
                items.Add((CodeMagicMoveRole.Removed, i, i));
            }
        }

        var count = items.Count;
        Roles = new CodeMagicMoveRole[count];
        Words = new int[count];
        Texts = new string[count];
        _fromX = new double[count];
        _fromY = new double[count];
        _fromAlpha = new double[count];
        ToX = new double[count];
        ToY = new double[count];
        _start = new double[count];
        X = new double[count];
        Y = new double[count];
        Alpha = new double[count];
        Chars = new int[count];
        _charStart = new int[count];

        var travel = 0.0;
        for (var k = 0; k < count; k++)
        {
            var (role, word, old) = items[k];
            Roles[k] = role;
            Words[k] = word;
            if (role == CodeMagicMoveRole.Removed)
            {
                var w = from.Source.Words[word];
                Texts[k] = w.Text;
                _fromX[k] = ToX[k] = from.X[old];
                _fromY[k] = ToY[k] = from.Y[old];
                _fromAlpha[k] = from.Alpha[old];
                HasRemovals = true;
            }
            else
            {
                var w = to.Words[word];
                Texts[k] = w.Text;
                ToX[k] = w.Column;
                ToY[k] = w.Line;
                if (role == CodeMagicMoveRole.Kept)
                {
                    _fromX[k] = from.X[old];
                    _fromY[k] = from.Y[old];
                    _fromAlpha[k] = from.Alpha[old];
                    travel = Math.Max(travel, Math.Abs(w.Line - from.Y[old]));
                }
                else
                {
                    _fromX[k] = w.Column;
                    _fromY[k] = w.Line;
                }
            }
        }

        GlideStart = HasRemovals ? RemovalLeadMs : 0;
        GlideMs = Math.Max(MinGlideMs, GlideBaseMs + GlidePerLineMs * travel);
        _fromLines = from.Lines;
        ToLines = to.LineCount;

        // Typing: the new words in reading order.
        var typed = new List<int>();
        for (var k = 0; k < count; k++)
        {
            if (Roles[k] == CodeMagicMoveRole.Typed)
            {
                typed.Add(k);
            }
        }
        typed.Sort((p, q) => Words[p].CompareTo(Words[q]));
        _charOrder = typed.ToArray();
        var chars = 0;
        foreach (var k in typed)
        {
            _charStart[k] = chars;
            chars += Texts[k].Length;
        }
        TypedChars = chars;
        _charTimes = new double[chars];
        TypeStart = GlideStart + TypeStartFraction * GlideMs;
        if (chars > 0)
        {
            var weights = new double[chars];
            var total = 0.0;
            var index = 0;
            CodeMagicMoveWord? previous = null;
            foreach (var k in typed)
            {
                var w = to.Words[Words[k]];
                for (var c = 0; c < w.Text.Length; c++, index++)
                {
                    if (index == 0)
                    {
                        continue;
                    }
                    var ch = w.Text[c];
                    var weight = CodeMagicMoveSource.IsWordChar(ch) ? 1.0 : 1.6;
                    var adjacent = c > 0 || (previous is { } p && p.Line == w.Line && p.End == w.Column);
                    if (!adjacent)
                    {
                        weight += 0.6;
                    }
                    weights[index] = weight;
                    total += weight;
                }
                previous = w;
            }
            var window = Math.Min(TypePerCharMs * chars, MaxTypeMs);
            var sum = 0.0;
            for (var i = 0; i < chars; i++)
            {
                sum += weights[i];
                _charTimes[i] = TypeStart + (total > 0 ? window * sum / total : 0);
            }
            foreach (var k in typed)
            {
                _start[k] = _charTimes[_charStart[k]];
            }
            LastCharTime = _charTimes[chars - 1];
        }

        for (var k = 0; k < count; k++)
        {
            if (Roles[k] == CodeMagicMoveRole.Kept)
            {
                _start[k] = GlideStart + LineStaggerMs * ToY[k];
            }
        }

        // Bands: the old ones fade with the removed words, the new ones as typing reaches them.
        _oldBandAlpha = (double[])from.BandAlpha.Clone();
        OldBandAlpha = new double[OldBands.Length];
        BandAlpha = new double[toBands.Length];
        _bandStart = new double[toBands.Length];
        for (var b = 0; b < toBands.Length; b++)
        {
            var band = toBands[b];
            var start = TypeStart;
            foreach (var k in typed)
            {
                var line = to.Words[Words[k]].Line;
                if (line >= band.Line && line < band.Line + band.Count)
                {
                    start = _start[k];
                    break;
                }
            }
            _bandStart[b] = start;
        }

        var end = HasRemovals || OldBands.Length > 0 ? RemoveMs : 0;
        end = Math.Max(end, GlideStart + GlideMs);
        for (var k = 0; k < count; k++)
        {
            if (Roles[k] == CodeMagicMoveRole.Kept)
            {
                end = Math.Max(end, _start[k] + GlideMs);
            }
        }
        if (chars > 0)
        {
            end = Math.Max(end, LastCharTime + CaretHoldMs + CaretFadeMs);
        }
        foreach (var start in _bandStart)
        {
            end = Math.Max(end, start + BandFadeMs);
        }
        Duration = end;
        Evaluate(0);
    }

    /// <summary>Where the move starts.</summary>
    public CodeMagicMoveScene From { get; }

    /// <summary>The version the move arrives at.</summary>
    public CodeMagicMoveSource To { get; }

    /// <summary>The bands of the version the move arrives at.</summary>
    public CodeMagicMoveBand[] ToBands { get; }

    /// <summary>The bands on show when the move started.</summary>
    public CodeMagicMoveBand[] OldBands { get; }

    /// <summary>What each item does.</summary>
    public CodeMagicMoveRole[] Roles { get; }

    /// <summary>The word of each item: in <see cref="To"/>, or in the old version for a removed one.</summary>
    public int[] Words { get; }

    /// <summary>The text of each item.</summary>
    public string[] Texts { get; }

    /// <summary>Where each item rests at the end (column).</summary>
    public double[] ToX { get; }

    /// <summary>Where each item rests at the end (line).</summary>
    public double[] ToY { get; }

    /// <summary>Whether any word lifts away (the glide then waits for them).</summary>
    public bool HasRemovals { get; }

    /// <summary>When the glide of the first line starts.</summary>
    public double GlideStart { get; }

    /// <summary>How long each glide takes.</summary>
    public double GlideMs { get; }

    /// <summary>When the first character is typed.</summary>
    public double TypeStart { get; }

    /// <summary>When the last character is typed (0 without typing).</summary>
    public double LastCharTime { get; }

    /// <summary>How many characters are typed.</summary>
    public int TypedChars { get; }

    /// <summary>The line count at the end.</summary>
    public int ToLines { get; }

    /// <summary>The whole move.</summary>
    public double Duration { get; }

    /// <summary>The time last evaluated.</summary>
    public double Time { get; private set; }

    // --- the evaluated frame -----------------------------------------------------------------------------------------

    /// <summary>Each item's column at <see cref="Time"/>.</summary>
    public double[] X { get; }

    /// <summary>Each item's line at <see cref="Time"/> (a removed word's lift included).</summary>
    public double[] Y { get; }

    /// <summary>Each item's opacity.</summary>
    public double[] Alpha { get; }

    /// <summary>How many characters of each item show (all, except while it is typed).</summary>
    public int[] Chars { get; }

    /// <summary>The height in lines.</summary>
    public double Lines { get; private set; }

    /// <summary>The caret: column, line and opacity (0 when hidden).</summary>
    public (double Column, double Line, double Alpha) Caret { get; private set; }

    /// <summary>The opacity of each old band.</summary>
    public double[] OldBandAlpha { get; }

    /// <summary>The opacity of each new band.</summary>
    public double[] BandAlpha { get; }

    /// <summary>Evaluates the move at <paramref name="time"/> ms (clamped to the move).</summary>
    public void Evaluate(double time)
    {
        var t = Math.Clamp(time, 0, Duration);
        Time = t;
        var removal = ArcEasings.Standard.Ease(Math.Clamp(t / RemoveMs, 0, 1));
        for (var k = 0; k < Roles.Length; k++)
        {
            switch (Roles[k])
            {
                case CodeMagicMoveRole.Removed:
                    X[k] = _fromX[k];
                    Y[k] = _fromY[k] - LiftLines * removal;
                    Alpha[k] = _fromAlpha[k] * (1 - removal);
                    Chars[k] = Texts[k].Length;
                    break;
                case CodeMagicMoveRole.Kept:
                {
                    var p = ArcEasings.InOut.Ease(Math.Clamp((t - _start[k]) / GlideMs, 0, 1));
                    X[k] = _fromX[k] + (ToX[k] - _fromX[k]) * p;
                    Y[k] = _fromY[k] + (ToY[k] - _fromY[k]) * p;
                    Alpha[k] = _fromAlpha[k] + (1 - _fromAlpha[k]) * p;
                    Chars[k] = Texts[k].Length;
                    break;
                }
                default:
                {
                    X[k] = ToX[k];
                    Y[k] = ToY[k];
                    var first = _charStart[k];
                    var shown = 0;
                    var length = Texts[k].Length;
                    while (shown < length && _charTimes[first + shown] <= t)
                    {
                        shown++;
                    }
                    Chars[k] = shown;
                    Alpha[k] = shown == 0 ? 0 : Math.Clamp((t - _start[k]) / WordFadeMs, 0, 1);
                    break;
                }
            }
        }

        Lines = _fromLines + (ToLines - _fromLines) * ArcEasings.InOut.Ease(Math.Clamp((t - GlideStart) / GlideMs, 0, 1));

        for (var b = 0; b < OldBandAlpha.Length; b++)
        {
            OldBandAlpha[b] = _oldBandAlpha[b] * (1 - removal);
        }
        for (var b = 0; b < BandAlpha.Length; b++)
        {
            BandAlpha[b] = Math.Clamp((t - _bandStart[b]) / BandFadeMs, 0, 1);
        }

        Caret = EvaluateCaret(t);
    }

    private (double, double, double) EvaluateCaret(double t)
    {
        if (TypedChars == 0 || t < TypeStart)
        {
            return (0, 0, 0);
        }
        var alpha = t <= LastCharTime + CaretHoldMs ? 1 : Math.Clamp(1 - (t - LastCharTime - CaretHoldMs) / CaretFadeMs, 0, 1);
        // After the last typed character.
        var column = 0.0;
        var line = 0.0;
        foreach (var k in _charOrder)
        {
            var shown = Chars[k];
            if (shown == 0)
            {
                break;
            }
            column = ToX[k] + shown;
            line = ToY[k];
            if (shown < Texts[k].Length)
            {
                break;
            }
        }
        return (column, line, alpha);
    }

    /// <summary>The scene this move shows at <see cref="Time"/>, seen as a snippet of <see cref="To"/> (for a new move).</summary>
    public CodeMagicMoveScene Snapshot()
    {
        var scene = new CodeMagicMoveScene(To, ToBands) { Lines = Lines };
        for (var k = 0; k < Roles.Length; k++)
        {
            if (Roles[k] == CodeMagicMoveRole.Removed)
            {
                continue;
            }
            var w = Words[k];
            scene.X[w] = X[k];
            scene.Y[w] = Y[k];
            scene.Alpha[w] = Roles[k] == CodeMagicMoveRole.Typed && Chars[k] < Texts[k].Length ? 0 : Alpha[k];
        }
        for (var b = 0; b < BandAlpha.Length; b++)
        {
            scene.BandAlpha[b] = BandAlpha[b];
        }
        return scene;
    }
}

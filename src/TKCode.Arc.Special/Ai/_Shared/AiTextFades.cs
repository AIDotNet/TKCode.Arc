using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The per-chunk fade-in of streamed text (Arc's span fade): each chunk is a character range of an already laid-out text
/// that goes from transparent to its own color over <see cref="Duration"/>, while the text before it stays put. It fades
/// the glyphs themselves, so it looks the same on any background (solid, translucent or a gradient), and it never lays the
/// text out again: <see cref="Draw"/> draws each wrapped line once when it is settled, and otherwise through one clip per
/// piece (settled text, or a fading chunk at its opacity), so every glyph is drawn exactly once. A text that is laid out
/// whole before it streams (its final size reserved) passes how much of it is revealed, and the rest is not drawn.
/// <para>
/// The clips are measured with <see cref="TextLine.GetTextBounds"/> (right-to-left runs included) once per line and fade
/// set, and the opacities run on one Core <see cref="LiveLoop"/>, so a frame allocates nothing. Fades only run at full
/// motion: under reduced motion or Off, and when the owner cannot animate (detached, hidden or scrolled away), a chunk is
/// shown at once and fades in progress finish at once. Code-driven because a chunk is a range of characters inside one text
/// layout, which no ControlTheme transition can address.
/// </para>
/// </summary>
internal sealed class AiTextFades
{
    private readonly Visual _owner;
    private readonly Action _changed;
    private readonly List<Fade> _fades = [];
    private readonly Dictionary<TextLine, LinePlan> _plans = new(ReferenceEqualityComparer.Instance);
    private LiveLoop? _loop;
    private double _now;
    private int _version;

    /// <param name="owner">The visual that draws the text; its motion level and visibility gate the fades.</param>
    /// <param name="changed">Called whenever the drawing changes (usually the owner's <c>InvalidateVisual</c>).</param>
    public AiTextFades(Visual owner, Action changed)
    {
        _owner = owner;
        _changed = changed;
    }

    /// <summary>How long one chunk takes to fade in (140ms, Arc's span fade, unless a component measured another).</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(140);

    /// <summary>The fade's curve; null fades linearly.</summary>
    public Easing? Easing { get; set; } = ArcEasings.Standard;

    /// <summary>How many chunks are still fading in.</summary>
    public int Count => _fades.Count;

    /// <summary>The chunks still fading in, with their opacity now (tests).</summary>
    public IEnumerable<(int Start, int End, double Progress)> Snapshot
    {
        get
        {
            for (var i = 0; i < _fades.Count; i++)
            {
                yield return (_fades[i].Start, _fades[i].End, OpacityAt(i));
            }
        }
    }

    /// <summary>
    /// Starts fading in the characters from <paramref name="start"/> to <paramref name="end"/>. Returns false, and the
    /// range simply shows, when motion is not full or the owner cannot animate now.
    /// </summary>
    public bool Add(int start, int end)
    {
        if (end <= start || !ArcMotion.IsFull(_owner))
        {
            return false;
        }
        var loop = Loop;
        loop.Start();
        if (!loop.IsRunning)
        {
            if (_fades.Count == 0)
            {
                loop.Stop();
            }
            return false;
        }
        _fades.Add(new Fade(start, end, _now));
        Changed();
        return true;
    }

    /// <summary>Whether a chunk still fading overlaps the characters from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public bool Overlaps(int start, int end)
    {
        foreach (var fade in _fades)
        {
            if (fade.Start < end && fade.End > start)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Drops what fades past <paramref name="keep"/> characters (the text was cut back there).</summary>
    public void Truncate(int keep)
    {
        var changed = false;
        for (var i = _fades.Count - 1; i >= 0; i--)
        {
            var fade = _fades[i];
            if (fade.Start >= keep)
            {
                _fades.RemoveAt(i);
                changed = true;
            }
            else if (fade.End > keep)
            {
                _fades[i] = fade with { End = keep };
                changed = true;
            }
        }
        if (changed)
        {
            Changed();
        }
    }

    /// <summary>Ends every fade at once: everything shows in full.</summary>
    public void Clear()
    {
        _loop?.Stop();
        if (_fades.Count == 0)
        {
            return;
        }
        _fades.Clear();
        Changed();
    }

    /// <summary>Forgets the clips measured for lines that are no longer laid out (call after a new layout).</summary>
    public void ForgetLines() => _plans.Clear();

    /// <summary>
    /// Draws <paramref name="layout"/> at <paramref name="origin"/>: the characters before <paramref name="revealed"/> (use
    /// <see cref="int.MaxValue"/> for all), each fading chunk at its opacity.
    /// </summary>
    public void Draw(DrawingContext context, TextLayout layout, Point origin, int revealed)
    {
        if (_fades.Count == 0 && revealed >= int.MaxValue)
        {
            layout.Draw(context, origin);
            return;
        }
        var y = origin.Y;
        foreach (var line in layout.TextLines)
        {
            DrawLine(context, line, new Point(origin.X, y), 0, revealed);
            y += line.Height;
        }
    }

    /// <summary>
    /// Draws one wrapped line at <paramref name="origin"/> (as <see cref="TextLine.Draw"/> does). Its characters are numbered
    /// from <paramref name="indexBase"/> (the text's offset of the layout the line belongs to), like the fades and
    /// <paramref name="revealed"/>.
    /// </summary>
    public void DrawLine(DrawingContext context, TextLine line, Point origin, int indexBase, int revealed)
    {
        var from = indexBase + line.FirstTextSourceIndex;
        var to = from + line.Length;
        var end = Math.Min(to, revealed);
        if (end <= from)
        {
            return;
        }
        if (end == to && !Overlaps(from, to))
        {
            line.Draw(context, origin);
            return;
        }
        var plan = PlanFor(line, from, end, indexBase);
        foreach (var piece in plan.Pieces)
        {
            var opacity = piece.Fade < 0 ? 1 : OpacityAt(piece.Fade);
            if (opacity <= 0.002)
            {
                continue;
            }
            using (context.PushClip(piece.Clip.Translate(new Vector(origin.X, origin.Y))))
            {
                if (opacity < 0.998)
                {
                    using (context.PushOpacity(opacity))
                    {
                        line.Draw(context, origin);
                    }
                }
                else
                {
                    line.Draw(context, origin);
                }
            }
        }
    }

    /// <summary>The opacity of fade <paramref name="index"/> now.</summary>
    private double OpacityAt(int index)
    {
        var duration = Duration.TotalSeconds;
        var t = duration <= 0 ? 1 : Math.Clamp((_now - _fades[index].Born) / duration, 0, 1);
        return Easing is { } easing ? easing.Ease(t) : t;
    }

    private LiveLoop Loop
    {
        get
        {
            if (_loop is null)
            {
                var loop = new LiveLoop(_owner, Step);
                // A loop that has to pause (off screen, motion lowered, detached) must not leave words half faded.
                loop.RunningChanged += running =>
                {
                    if (!running && _fades.Count > 0 && !loop.CanRun)
                    {
                        _fades.Clear();
                        Changed();
                    }
                };
                _loop = loop;
            }
            return _loop;
        }
    }

    private bool Step(double seconds)
    {
        _now += seconds;
        var duration = Duration.TotalSeconds;
        var removed = false;
        for (var i = _fades.Count - 1; i >= 0; i--)
        {
            if (_now - _fades[i].Born >= duration)
            {
                _fades.RemoveAt(i);
                removed = true;
            }
        }
        if (removed)
        {
            _version++;
            _plans.Clear();
        }
        _changed();
        return _fades.Count > 0;
    }

    private void Changed()
    {
        _version++;
        _plans.Clear();
        _changed();
    }

    /// <summary>The pieces of one line: settled runs and fading chunks, each with the clip that holds its glyphs.</summary>
    private LinePlan PlanFor(TextLine line, int from, int end, int indexBase)
    {
        if (_plans.TryGetValue(line, out var cached) && cached.Version == _version && cached.End == end)
        {
            return cached;
        }
        if (_plans.Count > 256)
        {
            _plans.Clear();
        }
        var pieces = new List<Piece>();
        var order = new List<int>();
        for (var i = 0; i < _fades.Count; i++)
        {
            if (_fades[i].Start < end && _fades[i].End > from)
            {
                order.Add(i);
            }
        }
        order.Sort((a, b) => _fades[a].Start.CompareTo(_fades[b].Start));
        var cursor = from;
        foreach (var index in order)
        {
            var fade = _fades[index];
            var start = Math.Max(fade.Start, cursor);
            var stop = Math.Min(fade.End, end);
            if (start > cursor)
            {
                AddPiece(pieces, line, cursor, start, -1, indexBase);
            }
            if (stop > start)
            {
                AddPiece(pieces, line, start, stop, index, indexBase);
                cursor = stop;
            }
        }
        if (end > cursor)
        {
            AddPiece(pieces, line, cursor, end, -1, indexBase);
        }
        var plan = new LinePlan(_version, end, pieces.ToArray());
        _plans[line] = plan;
        return plan;
    }

    private static void AddPiece(List<Piece> pieces, TextLine line, int start, int stop, int fade, int indexBase)
    {
        // Only this line is drawn through the clip, so it may reach well past the line box: ink above and below, and past
        // the line's visual ends (overhangs, italics) is kept. Between two pieces the clips meet at the characters' edge.
        // A piece that is not revealed to the line's end never reaches its right edge, so unrevealed glyphs stay out.
        var slack = Math.Max(4, line.Height);
        var left = line.Start;
        var right = line.Start + line.WidthIncludingTrailingWhitespace;
        foreach (var bounds in line.GetTextBounds(start - indexBase, stop - start))
        {
            var rect = bounds.Rectangle;
            var x0 = rect.Left <= left + 0.01 ? rect.Left - slack : rect.Left;
            var x1 = rect.Right >= right - 0.01 ? rect.Right + slack : rect.Right;
            if (x1 - x0 <= 0)
            {
                continue;
            }
            pieces.Add(new Piece(new Rect(x0, -slack, x1 - x0, line.Height + 2 * slack), fade));
        }
    }

    private readonly record struct Fade(int Start, int End, double Born);

    private readonly record struct Piece(Rect Clip, int Fade);

    private sealed record LinePlan(int Version, int End, Piece[] Pieces);
}

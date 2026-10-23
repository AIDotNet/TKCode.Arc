using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The fade-in of streamed text in one text layout: each batch of new characters fades from 0 to 1 on its own clock
/// while the text before it stays as it is. Nothing is laid out again: the layout is drawn line by line, the settled
/// part of a line once and each fading part clipped to its characters at its opacity.
/// </summary>
/// <remarks>
/// Code-driven motion (<see cref="MotionValue{T}"/>): a batch is a character range inside a text layout, which has no
/// property a ControlTheme could animate, and every batch starts at 0 when it arrives.
/// </remarks>
internal sealed class AiAnswerFades
{
    /// <summary>How long a batch takes to fade in (the AI group's streaming fade: 150 ms, standard curve).</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);

    private const double ClipSlack = 6;

    private readonly Visual _owner;
    private readonly List<Fade> _fades = [];

    public AiAnswerFades(Visual owner) => _owner = owner;

    /// <summary>
    /// Fades in a control that arrives with a batch (a citation pill, a list marker): opacity 0 → 1 with the text's
    /// duration and curve, started once it is in the tree (a motion value off the tree jumps to its end).
    /// </summary>
    public static void FadeIn(Visual control)
    {
        control.Opacity = 0;
        if (control.IsAttachedToVisualTree())
        {
            _ = Fade();
        }
        else
        {
            control.AttachedToVisualTree += OnAttached;
        }

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            control.AttachedToVisualTree -= OnAttached;
            _ = Fade();
        }

        async Task Fade()
        {
            var value = new MotionValue<double>(control, 0, MotionVectors.Double);
            value.Changed += v => control.Opacity = v;
            await value.AnimateTo(1, MotionSpec.Tween(Duration, ArcEasings.Standard));
            control.Opacity = 1;
            value.Dispose();
        }
    }

    /// <summary>Whether any batch is fading.</summary>
    public bool IsActive => _fades.Count > 0;

    /// <summary>The fading ranges with their progress (tests).</summary>
    internal IEnumerable<(int Start, int End, double Progress)> Ranges => _fades.Select(f => (f.Start, f.End, f.Value.Value));

    /// <summary>The first fading character, or <see cref="int.MaxValue"/>.</summary>
    public int Start => _fades.Count == 0 ? int.MaxValue : _fades[0].Start;

    /// <summary>Fades in the characters from <paramref name="start"/> to <paramref name="end"/>; earlier fades are cut at <paramref name="start"/>.</summary>
    public void Add(int start, int end)
    {
        Truncate(start);
        if (end <= start)
        {
            return;
        }
        var fade = new Fade(start, end, new MotionValue<double>(_owner, 0, MotionVectors.Double));
        fade.Value.Changed += _ => _owner.InvalidateVisual();
        _fades.Add(fade);
        if (_owner.IsAttachedToVisualTree())
        {
            _ = Run(fade);
        }
    }

    /// <summary>
    /// Starts the fades added before the owner was in the visual tree (a new block's first batch: a motion value off the
    /// tree would jump to its end at once).
    /// </summary>
    public void OnAttached()
    {
        if (_fades.Count == 0)
        {
            return;
        }
        // A fade can end at once (and leave the list) when motion is off.
        foreach (var fade in _fades.ToArray())
        {
            if (!fade.Started)
            {
                _ = Run(fade);
            }
        }
    }

    /// <summary>Keeps only the characters before <paramref name="keep"/> fading (the text after it changed).</summary>
    public void Truncate(int keep)
    {
        for (var i = _fades.Count - 1; i >= 0; i--)
        {
            var fade = _fades[i];
            if (fade.Start >= keep)
            {
                fade.Value.Dispose();
                _fades.RemoveAt(i);
            }
            else if (fade.End > keep)
            {
                fade.End = keep;
            }
        }
    }

    /// <summary>Shows everything at once.</summary>
    public void Clear()
    {
        foreach (var fade in _fades)
        {
            fade.Value.Dispose();
        }
        _fades.Clear();
        _owner.InvalidateVisual();
    }

    /// <summary>The opacity of character <paramref name="index"/>: 1 unless it is still fading in.</summary>
    public double AlphaAt(int index)
    {
        foreach (var fade in _fades)
        {
            if (index >= fade.Start && index < fade.End)
            {
                return Math.Clamp(fade.Value.Value, 0, 1);
            }
        }
        return 1;
    }

    private async Task Run(Fade fade)
    {
        fade.Started = true;
        var done = await fade.Value.AnimateTo(1, MotionSpec.Tween(Duration, ArcEasings.Standard));
        if (done && _fades.Remove(fade))
        {
            fade.Value.Dispose();
            _owner.InvalidateVisual();
        }
    }

    /// <summary>
    /// Draws <paramref name="layout"/> at <paramref name="origin"/>: lines before the first fade at once, the rest line by
    /// line with each fading range clipped to its characters at its opacity.
    /// </summary>
    public void Draw(DrawingContext context, TextLayout layout, Point origin)
    {
        if (_fades.Count == 0)
        {
            layout.Draw(context, origin);
            return;
        }
        var first = _fades[0].Start;
        var lines = layout.TextLines;
        var y = origin.Y;
        for (var l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            var lineOrigin = new Point(origin.X, y);
            var lineStart = line.FirstTextSourceIndex;
            var lineEnd = lineStart + line.Length;
            var top = y - ClipSlack;
            var height = line.Height + 2 * ClipSlack;
            if (lineEnd <= first)
            {
                line.Draw(context, lineOrigin);
            }
            else
            {
                var left = origin.X + line.Start - 2 * ClipSlack;
                var right = origin.X + line.Start + line.WidthIncludingTrailingWhitespace + 2 * ClipSlack;
                if (lineStart < first)
                {
                    var x = origin.X + line.GetDistanceFromCharacterHit(new CharacterHit(first));
                    using (context.PushClip(new Rect(left, top, Math.Max(0, x - left), height)))
                    {
                        line.Draw(context, lineOrigin);
                    }
                }
                foreach (var fade in _fades)
                {
                    var from = Math.Max(fade.Start, lineStart);
                    var to = Math.Min(fade.End, lineEnd);
                    var alpha = Math.Clamp(fade.Value.Value, 0, 1);
                    if (from >= to || alpha <= 0.004)
                    {
                        continue;
                    }
                    var x0 = from <= lineStart ? left : origin.X + line.GetDistanceFromCharacterHit(new CharacterHit(from));
                    var x1 = to >= lineEnd ? right : origin.X + line.GetDistanceFromCharacterHit(new CharacterHit(to));
                    if (x1 <= x0)
                    {
                        continue;
                    }
                    using (context.PushClip(new Rect(x0, top, x1 - x0, height)))
                    using (context.PushOpacity(alpha))
                    {
                        line.Draw(context, lineOrigin);
                    }
                }
            }
            y += line.Height;
        }
    }

    private sealed class Fade(int start, int end, MotionValue<double> value)
    {
        public int Start { get; } = start;

        public int End { get; set; } = end;

        public MotionValue<double> Value { get; } = value;

        public bool Started { get; set; }
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One turn's line(s) in a <see cref="VoiceTranscript"/>: a word per <see cref="TextBlock"/>, wrapped with balance and
/// centred. New words rise 3 px out of a 4 px blur and fade in over 340 ms (enter curve); when a line is added the block
/// springs up from one line height lower (ω 13.5, ζ 1). A new turn's block fades in rising 8 px on a 700 ms critically
/// damped spring (ω 14); the old one leaves as 180 ms of fade, 10 px up and 4 px blur, taking no room.
/// </summary>
internal sealed class VoiceTranscriptBlock : Control
{
    /// <summary>A word's entrance (measured: WAAPI 340 ms, cubic-bezier(.16,1,.3,1)).</summary>
    public const double WordDuration = 0.34;

    /// <summary>Reduced motion: every fade is 120 ms of opacity only.</summary>
    public const double ReducedDuration = 0.12;

    /// <summary>The old turn's exit (measured 180 ms, standard curve).</summary>
    public const double ExitDuration = 0.18;

    /// <summary>The new turn's entrance (measured 700 ms <c>linear()</c> spring, ≈ ω 14, ζ 1).</summary>
    public const double EnterDuration = 0.7;

    private const double EnterOmega = 14;
    private const double LineOmega = 13.5;
    private const double WordRise = 3;
    private const double WordBlur = 4;

    private readonly VoiceTranscript _owner;
    private readonly List<Word> _words = new();
    private double[] _widths = new double[16];
    private int[] _lineOf = new int[16];
    private double _space;
    private double _laidOutWidth = double.NaN;
    private TranslateTransform? _blockShift;

    public VoiceTranscriptBlock(VoiceTranscript owner, int turn, VoiceTranscriptSpeaker speaker)
    {
        _owner = owner;
        Turn = turn;
        Speaker = speaker;
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    private sealed class Word
    {
        public required string Text { get; init; }
        public required TextBlock View { get; init; }
        public required TranslateTransform Shift { get; init; }
        public double Born { get; set; }
        public double Width { get; set; }
        public Point At { get; set; }
    }

    public int Turn { get; }

    public VoiceTranscriptSpeaker Speaker { get; private set; }

    /// <summary>The number of laid out lines.</summary>
    public int Lines { get; private set; }

    /// <summary>The block's height (lines × line height).</summary>
    public double BlockHeight { get; private set; }

    /// <summary>The layout spring's displacement (px below the resting place) and velocity.</summary>
    public double Offset { get; private set; }

    public double Velocity { get; private set; }

    /// <summary>When the block's entrance began (NaN: none).</summary>
    public double EnterBorn { get; set; } = double.NaN;

    /// <summary>When the block began to leave (NaN: it is the current block).</summary>
    public double ExitBorn { get; set; } = double.NaN;

    /// <summary>Whether the block has finished leaving.</summary>
    public bool IsGone { get; private set; }

    public int WordCount => _words.Count;

    public string WordAt(int index) => _words[index].Text;

    /// <summary>The words' text joined (tests).</summary>
    public string Joined => string.Join(" ", _words.ConvertAll(w => w.Text));

    /// <summary>The line of word <paramref name="index"/> after layout (tests).</summary>
    public int LineOf(int index) => _lineOf[index];

    /// <summary>The opacity of word <paramref name="index"/> (tests).</summary>
    public double WordOpacity(int index) => _words[index].View.Opacity;

    public void SetSpeaker(VoiceTranscriptSpeaker speaker)
    {
        if (Speaker == speaker)
        {
            return;
        }
        Speaker = speaker;
        foreach (var word in _words)
        {
            Style(word.View);
        }
        _laidOutWidth = double.NaN;
        InvalidateMeasure();
    }

    /// <summary>
    /// Brings the block's words to <paramref name="words"/>: appended words enter (born at <paramref name="now"/>, or
    /// already shown when <paramref name="animate"/> is false); a text that does not extend the current one is rewritten
    /// in place without entrances.
    /// </summary>
    public void Sync(string[] words, double now, bool animate)
    {
        var common = 0;
        while (common < words.Length && common < _words.Count && words[common] == _words[common].Text)
        {
            common++;
        }
        var extends = common == _words.Count;
        for (var i = _words.Count - 1; i >= common; i--)
        {
            VisualChildren.Remove(_words[i].View);
            _words.RemoveAt(i);
        }
        for (var i = common; i < words.Length; i++)
        {
            var shift = new TranslateTransform();
            var view = new TextBlock { Text = words[i], RenderTransform = shift, IsHitTestVisible = false };
            Style(view);
            AutomationProperties.SetAccessibilityView(view, AccessibilityView.Raw);
            var word = new Word { Text = words[i], View = view, Shift = shift, Born = extends && animate ? now : double.NegativeInfinity };
            _words.Add(word);
            VisualChildren.Add(view);
        }
        if (words.Length != common || !extends)
        {
            _laidOutWidth = double.NaN;
            InvalidateMeasure();
        }
    }

    /// <summary>Re-applies fonts and colors after a theme or token change.</summary>
    public void Restyle()
    {
        foreach (var word in _words)
        {
            Style(word.View);
        }
        _laidOutWidth = double.NaN;
        InvalidateMeasure();
    }

    private void Style(TextBlock view)
    {
        var system = Speaker == VoiceTranscriptSpeaker.System;
        view.FontFamily = _owner.CaptionFontFamily ?? FontFamily.Default;
        view.FontSize = system ? _owner.SystemFontSize : _owner.CaptionFontSize;
        view.LetterSpacing = _owner.CaptionLetterSpacing;
        view.Foreground = Speaker switch
        {
            VoiceTranscriptSpeaker.User => _owner.UserInk,
            VoiceTranscriptSpeaker.System => _owner.SystemInk,
            _ => _owner.AssistantInk
        };
    }

    private double LineHeight => Speaker == VoiceTranscriptSpeaker.System ? _owner.SystemLineHeight : _owner.CaptionLineHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var count = _words.Count;
        if (_widths.Length < count)
        {
            _widths = new double[Math.Max(count, _widths.Length * 2)];
            _lineOf = new int[_widths.Length];
        }
        var natural = 0.0;
        for (var i = 0; i < count; i++)
        {
            var view = _words[i].View;
            view.Measure(Size.Infinity);
            _words[i].Width = view.DesiredSize.Width;
            _widths[i] = view.DesiredSize.Width;
            natural += view.DesiredSize.Width;
        }
        _space = SpaceWidth();
        natural += Math.Max(0, count - 1) * _space;
        if (double.IsInfinity(width))
        {
            width = natural;
        }
        var lines = VoiceTranscriptLayout.Balance(_widths.AsSpan(0, count), _space, width, _lineOf);
        var lineHeight = LineHeight;
        // Centre every line in the width.
        var start = 0;
        for (var line = 0; line < lines; line++)
        {
            var end = start;
            var lineWidth = 0.0;
            while (end < count && _lineOf[end] == line)
            {
                lineWidth += (end > start ? _space : 0) + _widths[end];
                end++;
            }
            var x = Math.Max(0, (width - lineWidth) / 2);
            for (var i = start; i < end; i++)
            {
                var h = _words[i].View.DesiredSize.Height;
                _words[i].At = new Point(x, line * lineHeight + (lineHeight - h) / 2);
                x += _widths[i] + _space;
            }
            start = end;
        }
        var previous = Lines;
        Lines = lines;
        BlockHeight = lines * lineHeight;
        _laidOutWidth = width;
        if (lines > previous && double.IsNaN(ExitBorn))
        {
            _owner.OnBlockGrew(this, lines - previous, lineHeight);
        }
        return new Size(width, BlockHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var word in _words)
        {
            word.View.Arrange(new Rect(word.At, word.View.DesiredSize));
        }
        return finalSize;
    }

    /// <summary>Starts the layout spring <paramref name="distance"/> px lower, keeping its velocity.</summary>
    public void Kick(double distance)
    {
        Offset += distance;
    }

    /// <summary>Jumps every animation to its end.</summary>
    public void Settle()
    {
        Offset = 0;
        Velocity = 0;
        EnterBorn = double.NaN;
        foreach (var word in _words)
        {
            word.Born = double.NegativeInfinity;
        }
    }

    /// <summary>
    /// Advances the springs by <paramref name="dt"/> and paints the state at <paramref name="now"/>. Returns true while
    /// anything still moves.
    /// </summary>
    public bool Apply(double now, double dt, bool full)
    {
        var moving = false;
        // Layout spring (critically damped, ω 13.5), semi-implicit Euler in steps of at most 1/240 s.
        if (Math.Abs(Offset) > 0.01 || Math.Abs(Velocity) > 0.01)
        {
            if (!full)
            {
                Offset = 0;
                Velocity = 0;
            }
            else
            {
                var k = LineOmega * LineOmega;
                var c = 2 * LineOmega;
                var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
                var h = dt / steps;
                for (var s = 0; s < steps; s++)
                {
                    Velocity += (-k * Offset - c * Velocity) * h;
                    Offset += Velocity * h;
                }
                moving = true;
            }
        }
        else
        {
            Offset = 0;
            Velocity = 0;
        }

        var opacity = 1.0;
        var y = Offset;
        var blur = 0.0;
        if (!double.IsNaN(ExitBorn))
        {
            var p = Math.Clamp((now - ExitBorn) / (full ? ExitDuration : ReducedDuration), 0, 1);
            var e = full ? ArcEasings.Standard.Ease(p) : 1 - (1 - p) * (1 - p);
            opacity = 1 - e;
            if (full)
            {
                y -= 10 * e;
                blur = 4 * e;
            }
            IsGone = p >= 1;
            moving |= !IsGone;
        }
        else if (!double.IsNaN(EnterBorn))
        {
            var t = Math.Max(0, now - EnterBorn);
            double e;
            if (full)
            {
                var wt = EnterOmega * t;
                e = 1 - (1 + wt) * Math.Exp(-wt);
                y += 8 * (1 - e);
                if (t >= EnterDuration)
                {
                    e = 1;
                }
            }
            else
            {
                var p = Math.Clamp(t / ReducedDuration, 0, 1);
                e = 1 - (1 - p) * (1 - p);
            }
            opacity = e;
            if (e >= 1)
            {
                EnterBorn = double.NaN;
            }
            else
            {
                moving = true;
            }
        }
        Opacity = opacity;
        _blockShift ??= new TranslateTransform();
        RenderTransform = _blockShift;
        _blockShift.Y = y;
        Effect = blur > 0.05 ? ArcBlur.Get(blur) : null;

        foreach (var word in _words)
        {
            var age = now - word.Born;
            var view = word.View;
            if (double.IsNegativeInfinity(word.Born) || age >= (full ? WordDuration : ReducedDuration))
            {
                if (view.Opacity != 1 || view.Effect is not null || word.Shift.Y != 0)
                {
                    view.Opacity = 1;
                    view.Effect = null;
                    word.Shift.Y = 0;
                }
                word.Born = double.NegativeInfinity;
                continue;
            }
            moving = true;
            var p = Math.Clamp(age / (full ? WordDuration : ReducedDuration), 0, 1);
            var e = ArcEasings.Enter.Ease(p);
            view.Opacity = e;
            if (full)
            {
                word.Shift.Y = WordRise * (1 - e);
                var radius = WordBlur * (1 - e);
                view.Effect = radius > 0.05 ? ArcBlur.Get(radius) : null;
            }
        }
        return moving;
    }

    private double SpaceWidth()
    {
        var size = Speaker == VoiceTranscriptSpeaker.System ? _owner.SystemFontSize : _owner.CaptionFontSize;
        var family = _owner.CaptionFontFamily ?? FontFamily.Default;
        using var layout = new TextLayout(" ", new Typeface(family), size, null, letterSpacing: _owner.CaptionLetterSpacing);
        return layout.WidthIncludingTrailingWhitespace;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One token of the sequence under the stage: a 28 px chip (8 px radius, 14 px text with its spaces kept). Next tokens are
/// muted text, the current one has a ring, done ones a muted fill; the inspected one a strong border; while an expert is
/// inspected the tokens it took are tinted in their choice's color and the rest dim to 45%. Fades take 240 ms.
/// </summary>
internal sealed class MixtureOfExpertsSequenceToken : MixtureOfExpertsPart
{
    private MixtureOfExpertsFade _look = new(MixtureOfExpertsLooks.TokenNext);
    private MixtureOfExpertsTween _opacity = new(1);
    private string _text = string.Empty;
    private double _textWidth;

    public MixtureOfExpertsSequenceToken(MixtureOfExperts owner, int index) : base(owner, index)
    {
    }

    public override bool IsExpert => false;

    public int Look => _look.To;

    public bool IsDim => _opacity.Target < 1;

    public string Text => _text;

    public void SetText(string text)
    {
        if (_text != text)
        {
            _text = text;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public void Apply(int look, bool dim, double wall, bool animate)
    {
        var changed = _look.Set(look, wall, animate ? 0.24 : 0);
        changed |= _opacity.Set(dim ? 0.45 : 1, wall, animate ? 0.24 : 0);
        if (changed)
        {
            AnimatingUntil = Math.Max(AnimatingUntil, Math.Max(_look.Until, _opacity.Until));
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var palette = Owner.Palette;
        _textWidth = palette.IsReady ? palette.Text(_text, palette.Foreground).WidthIncludingTrailingWhitespace : _text.Length * 8;
        return new Size(Math.Ceiling(_textWidth + 16), 28);
    }

    public override void Render(DrawingContext context)
    {
        var palette = Owner.Palette;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (!palette.IsReady || w < 2)
        {
            return;
        }
        var wall = Owner.Wall;
        var mix = _look.Mix(wall);
        var opacity = _opacity.Value(wall);
        // Transparent fill so the whole chip takes the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            var (fromFill, fromBorder, fromText) = Style(palette, _look.From);
            var (toFill, toBorder, toText) = Style(palette, _look.To);
            var outer = new RoundedRect(new Rect(0, 0, w, h), 8);
            var (fa, ta) = FadeOpacity(fromFill, toFill, mix);
            Fill(context, outer, fromFill, fa);
            Fill(context, outer, toFill, ta);
            var inner = new RoundedRect(new Rect(0.5, 0.5, w - 1, h - 1), 7.5);
            var (ba, bb) = FadeOpacity(fromBorder, toBorder, mix);
            Stroke(context, inner, fromBorder, ba);
            Stroke(context, inner, toBorder, bb);
            var (xa, xb) = FadeOpacity(fromText, toText, mix);
            DrawText(context, palette, fromText, xa, h);
            DrawText(context, palette, toText, xb, h);
        }
        DrawFocusRing(context, new Rect(0, 0, w, h), 8);
    }

    private static (Color Fill, Color Border, Color Text) Style(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.TokenCurrent => (p.Surface, p.CurrentRing, p.Foreground),
        MixtureOfExpertsLooks.TokenDone => (p.SurfaceMuted, default, p.Foreground),
        MixtureOfExpertsLooks.TokenInspected => (p.Surface, p.Foreground, p.Foreground),
        MixtureOfExpertsLooks.TokenFirst => (p.TokenTint[0], p.TokenTintBorder[0], p.Foreground),
        MixtureOfExpertsLooks.TokenSecond => (p.TokenTint[1], p.TokenTintBorder[1], p.Foreground),
        _ => (default, default, p.TextMuted)
    };

    private void Fill(DrawingContext context, RoundedRect rect, Color color, double opacity)
    {
        if (opacity > 0.001 && color.A > 0)
        {
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(Owner.Palette.Brush(color), null, rect);
            }
        }
    }

    private void Stroke(DrawingContext context, RoundedRect rect, Color color, double opacity)
    {
        if (opacity > 0.001 && color.A > 0)
        {
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(null, Owner.Palette.Pen(color, 1), rect);
            }
        }
    }

    private void DrawText(DrawingContext context, MixtureOfExpertsPalette palette, Color color, double opacity, double h)
    {
        if (opacity <= 0.001 || color.A == 0 || _text.Length == 0)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            var text = palette.Text(_text, color);
            context.DrawText(text, new Point(8, (h - text.Height) / 2));
        }
    }
}

/// <summary>
/// The token sequence: the tokens as chips that wrap onto as many lines as they need, 4 px apart. One tab stop; the
/// arrow keys, Home and End move between tokens (handled by the card).
/// </summary>
internal sealed class MixtureOfExpertsSequence : Control
{
    private readonly List<MixtureOfExpertsSequenceToken> _tokens = new();

    public MixtureOfExpertsSequence()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
    }

    public IReadOnlyList<MixtureOfExpertsSequenceToken> Tokens => _tokens;

    public void SetTokens(MixtureOfExperts owner, IReadOnlyList<string> texts)
    {
        if (_tokens.Count != texts.Count || (_tokens.Count > 0 && _tokens[0].Owner != owner))
        {
            foreach (var token in _tokens)
            {
                VisualChildren.Remove(token);
                LogicalChildren.Remove(token);
            }
            _tokens.Clear();
            for (var i = 0; i < texts.Count; i++)
            {
                var token = new MixtureOfExpertsSequenceToken(owner, i);
                _tokens.Add(token);
                VisualChildren.Add(token);
                LogicalChildren.Add(token);
            }
            if (_tokens.Count > 0)
            {
                KeyboardNavigation.SetTabOnceActiveElement(this, _tokens[0]);
            }
        }
        for (var i = 0; i < texts.Count; i++)
        {
            _tokens[i].SetText(texts[i]);
        }
        InvalidateMeasure();
    }

    public void RemeasureTokens()
    {
        foreach (var token in _tokens)
        {
            token.InvalidateMeasure();
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        double x = 0;
        double y = 0;
        double widest = 0;
        foreach (var token in _tokens)
        {
            token.Measure(Size.Infinity);
            var size = token.DesiredSize;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += 28 + 4;
            }
            x += size.Width + 4;
            widest = Math.Max(widest, x - 4);
        }
        var height = _tokens.Count == 0 ? 0 : y + 28;
        return new Size(double.IsFinite(width) ? width : widest, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        double y = 0;
        foreach (var token in _tokens)
        {
            var size = token.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                x = 0;
                y += 28 + 4;
            }
            token.Arrange(new Rect(x, y, Math.Min(size.Width, finalSize.Width), 28));
            x += size.Width + 4;
        }
        return finalSize;
    }
}

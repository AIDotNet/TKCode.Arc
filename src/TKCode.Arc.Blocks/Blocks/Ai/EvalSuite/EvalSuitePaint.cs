using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>One case as the list shows it: the case, its two results, its status and the text laid out for drawing.</summary>
internal sealed class EvalSuiteRow
{
    public EvalSuiteRow(EvalCase evalCase, int number)
    {
        Case = evalCase;
        Number = number;
    }

    /// <summary>The case.</summary>
    public EvalCase Case { get; }

    /// <summary>Its position in the whole suite, from 1.</summary>
    public int Number { get; }

    public EvalResult? Baseline { get; set; }

    public EvalResult? Candidate { get; set; }

    public EvalCaseStatus Status { get; set; }

    /// <summary>Seconds since the result arrived, while its chip fades in; null when at rest.</summary>
    public double? Reveal { get; set; }

    // Laid out text, built for one width and one set of brushes; cleared when either changes.
    public double LayoutWidth = -1;
    public TextLayout? Title;
    public TextLayout? Input;
    public TextLayout? Check;
    public TextLayout? Chip;
    public TextLayout? BaseVerdict;
    public TextLayout? CandidateVerdict;

    public void ClearLayouts()
    {
        LayoutWidth = -1;
        Title?.Dispose();
        Input?.Dispose();
        Check?.Dispose();
        Chip?.Dispose();
        BaseVerdict?.Dispose();
        CandidateVerdict?.Dispose();
        Title = Input = Check = Chip = BaseVerdict = CandidateVerdict = null;
    }
}

/// <summary>Where the columns of the case list and its header sit at a given width.</summary>
internal readonly record struct EvalSuiteColumns(double StatusX, double StatusWidth, double CaseX, double CaseWidth, double CheckX,
    double CheckWidth, double BaseX, double CandidateX, double VerdictWidth)
{
    public const double Gap = 12;

    public static EvalSuiteColumns Compute(double width, double inset, double status, double check, double verdict, bool compact)
    {
        var checkWidth = compact ? 0 : check;
        var candidateX = Math.Max(inset, width - inset - verdict);
        var baseX = candidateX - verdict;
        var checkX = baseX - (checkWidth > 0 ? checkWidth + Gap : 0);
        var caseX = inset + status + Gap;
        var caseWidth = Math.Max(0, checkX - Gap - caseX);
        return new EvalSuiteColumns(inset, status, caseX, caseWidth, checkX, checkWidth, baseX, candidateX, verdict);
    }
}

/// <summary>The look of a status or a verdict: a token color and a Lucide glyph.</summary>
internal readonly record struct EvalSuiteMark(Color Color, Geometry Glyph, bool Tinted);

/// <summary>Shared drawing of the case list and the open case's chips: tints, glyphs and pills, with cached brushes and pens.</summary>
internal sealed class EvalSuitePainter
{
    private readonly Dictionary<Color, IImmutableBrush> _tints = new();
    private readonly Dictionary<Color, IImmutableBrush> _solids = new();
    private readonly Dictionary<Color, IPen> _pens = new();

    /// <summary>A chip's fill: the color at 12%.</summary>
    public const double TintAlpha = 0.12;

    public IImmutableBrush Tint(Color color)
    {
        if (!_tints.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromArgb((byte)(color.A * TintAlpha), color.R, color.G, color.B));
            _tints[color] = brush;
        }
        return brush;
    }

    public IImmutableBrush Solid(Color color)
    {
        if (!_solids.TryGetValue(color, out var brush))
        {
            brush = new ImmutableSolidColorBrush(color);
            _solids[color] = brush;
        }
        return brush;
    }

    /// <summary>Draws a 24 × 24 Lucide glyph at <paramref name="size"/>, centered at <paramref name="center"/>, optionally turned.</summary>
    public void Glyph(DrawingContext context, Geometry glyph, Color color, Point center, double size, double angle = 0)
    {
        if (!_pens.TryGetValue(color, out var pen))
        {
            pen = new ImmutablePen(Solid(color), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _pens[color] = pen;
        }
        var scale = size / 24;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateRotation(angle) * Matrix.CreateScale(scale, scale) *
                     Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, pen, glyph);
        }
    }

    /// <summary>The width of a chip around <paramref name="text"/>.</summary>
    public static double ChipWidth(TextLayout text, double height) => height * 0.35 + 14 + 5 + text.WidthIncludingTrailingWhitespace + height * 0.4;

    /// <summary>Draws a pill (tinted when <see cref="EvalSuiteMark.Tinted"/>) with the glyph and the text.</summary>
    public void Chip(DrawingContext context, Rect rect, EvalSuiteMark mark, TextLayout text, double radius, double angle = 0)
    {
        var r = Math.Min(radius, rect.Height / 2);
        if (mark.Tinted)
        {
            context.DrawRectangle(Tint(mark.Color), null, rect, r, r);
        }
        var glyphX = rect.X + rect.Height * 0.35 + 7;
        Glyph(context, mark.Glyph, mark.Color, new Point(glyphX, rect.Center.Y), 14, angle);
        text.Draw(context, new Point(glyphX + 7 + 5, rect.Center.Y - text.Height / 2));
    }
}

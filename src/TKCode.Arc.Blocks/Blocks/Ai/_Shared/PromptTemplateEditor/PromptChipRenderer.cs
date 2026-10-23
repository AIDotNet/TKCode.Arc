using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Paints the variable chips of a <see cref="PromptTemplateEditor"/> (Arc's <c>.chip</c>): the name alone at 0.93em in
/// the variable's tone, padding 1 / 5, margin 0 1, radius 6, 18 high around the text's baseline. Colors, with S the
/// tone's series color: fill S at 12%, a 1px inner ring S at 22%, text S mixed 68% into Foreground; active (its field has
/// focus) 24% / 45%; missing (its value is empty) no fill and no ring but a 1px dashed outline S at 60%; a flash takes
/// the fill to 40% and back to its state in 700ms. Active changes ease over 160ms (standard).
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>) rather than a theme transition: the chips are not controls but runs of the
/// editor's text, painted by the editor's mirror, so each name keeps its own active and flash values. Brushes and pens
/// are cached per tone and alpha step, so a running animation allocates nothing.
/// </remarks>
internal sealed class PromptChipRenderer : TokenRenderer
{
    internal static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(700);

    private static readonly Easing s_linear = new LinearEasing();
    private static readonly ImmutableDashStyle s_dash = new([3, 2], 0);

    private readonly PromptTemplateEditor _owner;
    private readonly Dictionary<string, Label> _labels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MotionValue<double>> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MotionValue<double>> _flash = new(StringComparer.Ordinal);
    private readonly Dictionary<int, IBrush> _fills = new();
    private readonly Dictionary<int, IPen> _rings = new();
    private readonly Dictionary<int, IPen> _dashes = new();
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private Typeface _typeface;
    private double _fontSize;
    private double _letterSpacing;
    private string? _activeName;

    /// <summary>A name's shaped text at the chip size, in its color.</summary>
    private sealed record Label(TextLayout Layout, Color Color, double Ascent, double Descent);

    public PromptChipRenderer(PromptTemplateEditor owner)
    {
        _owner = owner;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>The chip text size relative to the editor's (Arc 0.93em).</summary>
    public double FontScale { get; set; } = 0.93;

    /// <summary>Space between the name and the chip's edge (Arc padding 1 5).</summary>
    public Thickness Padding { get; set; } = new(5, 1);

    /// <summary>Space between the chip and the text beside it (Arc margin 0 1).</summary>
    public double Margin { get; set; } = 1;

    public double CornerRadius { get; set; } = 6;

    /// <summary>The names whose chips are dashed (their value is empty).</summary>
    public IReadOnlyCollection<string> Missing => _missing;

    /// <summary>The name whose chips are highlighted, or null.</summary>
    public string? ActiveName => _activeName;

    #region State

    /// <summary>Replaces the dashed names.</summary>
    public void SetMissing(IEnumerable<string>? names)
    {
        var next = names is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(names, StringComparer.Ordinal);
        if (next.SetEquals(_missing))
        {
            return;
        }
        _missing.Clear();
        _missing.UnionWith(next);
        Invalidate();
    }

    /// <summary>Highlights the chips of <paramref name="name"/> (null: none); the change eases at full motion.</summary>
    public void SetActive(string? name)
    {
        if (string.Equals(name, _activeName, StringComparison.Ordinal))
        {
            return;
        }
        if (_activeName is not null)
        {
            Animate(_active, _activeName, 0);
        }
        _activeName = name;
        if (name is not null)
        {
            Animate(_active, name, 1);
        }
    }

    /// <summary>Pulses the fill of the chips of <paramref name="name"/> once (full motion only; reduced and none keep still).</summary>
    public void Flash(string name)
    {
        if (ArcMotion.GetEffectiveLevel(_owner) != ArcMotionLevel.Full)
        {
            return;
        }
        var value = ValueFor(_flash, name);
        value.Jump(1);
        _ = value.AnimateTo(0, MotionSpec.Tween(FlashDuration, ArcEasings.Standard));
    }

    /// <summary>How active the chips of <paramref name="name"/> are (0 to 1).</summary>
    internal double ActiveAmount(string name) => _active.TryGetValue(name, out var value) ? value.Value : 0;

    /// <summary>How far into a flash the chips of <paramref name="name"/> are (1 at its start, 0 at rest).</summary>
    internal double FlashAmount(string name) => _flash.TryGetValue(name, out var value) ? value.Value : 0;

    private void Animate(Dictionary<string, MotionValue<double>> values, string name, double target)
    {
        var value = ValueFor(values, name);
        switch (ArcMotion.GetEffectiveLevel(_owner))
        {
            case ArcMotionLevel.Full:
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                value.Jump(target);
                break;
        }
    }

    private MotionValue<double> ValueFor(Dictionary<string, MotionValue<double>> values, string name)
    {
        if (!values.TryGetValue(name, out var value))
        {
            value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
            value.Changed += _ => Invalidate();
            values[name] = value;
        }
        return value;
    }

    /// <summary>Colors or tones changed: labels and brushes are made again.</summary>
    public void InvalidateColors()
    {
        ClearLabels();
        _fills.Clear();
        _rings.Clear();
        _dashes.Clear();
        Invalidate();
    }

    /// <summary>Stops the animations (the owner left the tree).</summary>
    public void Settle()
    {
        foreach (var value in _active.Values)
        {
            value.Jump(value.Target);
        }
        foreach (var value in _flash.Values)
        {
            value.Jump(0);
        }
    }

    #endregion

    #region Measure and render

    public override TokenInlineSize? Measure(TokenSpan token, in TokenRenderContext context)
    {
        if (token.Kind != PromptTemplateText.VariableKind)
        {
            return null;
        }
        var label = LabelFor(token.Id, context);
        var width = label.Layout.WidthIncludingTrailingWhitespace + Padding.Left + Padding.Right + 2 * Margin;
        // Report no more than the text's own extent, so a line with a chip is as tall as one without; the box may paint
        // a little past it.
        return new TokenInlineSize(width, Math.Min(label.Ascent + Padding.Top, context.Ascent),
            Math.Min(label.Descent + Padding.Bottom, context.Descent));
    }

    public override void Render(DrawingContext drawingContext, TokenSpan token, Rect bounds, double baseline, in TokenRenderContext context)
    {
        var name = token.Id;
        var label = LabelFor(name, context);
        var tone = _owner.ToneOf(name);
        var box = Snap(new Rect(bounds.X + Margin, baseline - label.Ascent - Padding.Top, Math.Max(0, bounds.Width - 2 * Margin),
            label.Ascent + label.Descent + Padding.Top + Padding.Bottom), _owner.RenderScaling);
        var active = ActiveAmount(name);
        var flash = FlashAmount(name);
        var radius = CornerRadius;
        if (_missing.Contains(name))
        {
            if (flash > 0)
            {
                drawingContext.DrawRectangle(Fill(tone, Lerp(0, PromptChipColors.FlashFill, flash)), null, box, radius, radius);
            }
            // CSS outline 1px dashed, offset -1: inside the box's edge.
            drawingContext.DrawRectangle(null, Dashed(tone), box.Deflate(0.5), radius - 0.5, radius - 0.5);
        }
        else
        {
            var fill = Lerp(PromptChipColors.Fill, PromptChipColors.ActiveFill, active);
            fill = Lerp(fill, PromptChipColors.FlashFill, flash);
            drawingContext.DrawRectangle(Fill(tone, fill), null, box, radius, radius);
            // The inset box-shadow: a 1px ring inside the edge.
            var ring = Lerp(PromptChipColors.Ring, PromptChipColors.ActiveRing, active);
            drawingContext.DrawRectangle(null, Ring(tone, ring), box.Deflate(0.5), radius - 0.5, radius - 0.5);
        }
        label.Layout.Draw(drawingContext, new Point(box.X + Padding.Left, baseline - label.Layout.TextLines[0].Baseline));
    }

    private static double Lerp(double from, double to, double amount) => from + (to - from) * Math.Clamp(amount, 0, 1);

    /// <summary>The box on whole device pixels, as a browser lays out a background, so the 1px ring stays crisp.</summary>
    private static Rect Snap(Rect box, double scaling)
    {
        static double Round(double value, double scale) => Math.Round(value * scale) / scale;
        var left = Round(box.Left, scaling);
        var top = Round(box.Top, scaling);
        return new Rect(left, top, Math.Max(0, Round(box.Right, scaling) - left), Math.Max(0, Round(box.Bottom, scaling) - top));
    }

    private Label LabelFor(string name, in TokenRenderContext context)
    {
        if (!context.Typeface.Equals(_typeface) || Math.Abs(context.FontSize - _fontSize) > 0.001 ||
            Math.Abs(context.LetterSpacing - _letterSpacing) > 0.001)
        {
            ClearLabels();
            _typeface = context.Typeface;
            _fontSize = context.FontSize;
            _letterSpacing = context.LetterSpacing;
        }
        var color = _owner.ChipTextColor(name);
        if (_labels.TryGetValue(name, out var label) && label.Color == color)
        {
            return label;
        }
        label?.Layout.Dispose();
        var size = context.FontSize * FontScale;
        // CSS inherits letter-spacing as a length: the chip keeps the editor's -0.14px.
        var layout = new TextLayout(name, context.Typeface, size, new ImmutableSolidColorBrush(color), TextAlignment.Left, TextWrapping.NoWrap,
            null, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, double.NaN, context.LetterSpacing);
        var metrics = context.Typeface.GlyphTypeface.Metrics;
        var scale = size / Math.Max(1, (double)metrics.DesignEmHeight);
        label = new Label(layout, color, -metrics.Ascent * scale, metrics.Descent * scale);
        _labels[name] = label;
        return label;
    }

    private void ClearLabels()
    {
        foreach (var label in _labels.Values)
        {
            label.Layout.Dispose();
        }
        _labels.Clear();
    }

    /// <summary>The cache key of a tone and an alpha step (0 to 255).</summary>
    private static int Key(int tone, double alpha) => tone * 256 + (int)Math.Round(Math.Clamp(alpha, 0, 1) * 255);

    private IBrush Fill(int tone, double alpha)
    {
        var key = Key(tone, alpha);
        if (!_fills.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(PromptTone.WithOpacity(_owner.SeriesColorOf(tone), (key % 256) / 255.0));
            _fills[key] = brush;
        }
        return brush;
    }

    private IPen Ring(int tone, double alpha)
    {
        var key = Key(tone, alpha);
        if (!_rings.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(new ImmutableSolidColorBrush(PromptTone.WithOpacity(_owner.SeriesColorOf(tone), (key % 256) / 255.0)), 1);
            _rings[key] = pen;
        }
        return pen;
    }

    private IPen Dashed(int tone)
    {
        if (!_dashes.TryGetValue(tone, out var pen))
        {
            var color = PromptTone.WithOpacity(_owner.SeriesColorOf(tone), PromptChipColors.MissingOutline);
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1, s_dash);
            _dashes[tone] = pen;
        }
        return pen;
    }

    #endregion
}

/// <summary>Arc's chip mixes (fractions of the variable's series color).</summary>
internal static class PromptChipColors
{
    public const double Fill = 0.12;
    public const double Ring = 0.22;
    public const double ActiveFill = 0.24;
    public const double ActiveRing = 0.45;
    public const double FlashFill = 0.40;
    public const double MissingOutline = 0.60;

    /// <summary>The chip text: the series color mixed 68% into Foreground.</summary>
    public const double TextMix = 0.68;
}

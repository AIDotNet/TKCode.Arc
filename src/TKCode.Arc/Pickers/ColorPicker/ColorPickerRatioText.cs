using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The contrast ratio ("4.52:1"). It counts to a new value on the thumb's follow spring (Arc's useFollow on the
/// ratio), drawing digits from glyphs shaped once, so counting never allocates.
/// </summary>
internal sealed class ColorPickerRatioText : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ColorPickerRatioText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ColorPickerRatioText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ColorPickerRatioText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ColorPickerRatioText>();

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const string Glyphs = "0123456789.:";
    private readonly FormattedText?[] _glyphs = new FormattedText?[Glyphs.Length];
    private double _digitWidth;
    private MotionValue<double>? _value;
    private double _target = 1;

    static ColorPickerRatioText()
    {
        AffectsMeasure<ColorPickerRatioText>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<ColorPickerRatioText>(ForegroundProperty);
    }

    /// <summary>The ratio being counted to.</summary>
    public double Ratio => _target;

    /// <summary>The ratio shown right now.</summary>
    public double ShownRatio => _value?.Value ?? _target;

    public void SetRatio(double ratio, bool animate)
    {
        _target = ratio;
        _value ??= CreateValue(ratio);
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _value.Jump(ratio);
            return;
        }
        _ = _value.AnimateTo(ratio, MotionSpec.Spring(ColorPickerPad.ThumbSpring));
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            Array.Clear(_glyphs);
        }
    }

    private FormattedText Glyph(char c)
    {
        var index = Glyphs.IndexOf(c);
        return _glyphs[index] ??= new FormattedText(c.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black);
    }

    private void EnsureMetrics()
    {
        _digitWidth = 0;
        for (var d = '0'; d <= '9'; d++)
        {
            _digitWidth = Math.Max(_digitWidth, Glyph(d).WidthIncludingTrailingWhitespace);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureMetrics();
        // Arc: min-width 4.4ch; "21.00:1" is the widest reading.
        var width = Math.Max(_digitWidth * 4.4, _digitWidth * 4 + Glyph('.').WidthIncludingTrailingWhitespace + Glyph(':').WidthIncludingTrailingWhitespace + _digitWidth);
        return new Size(Math.Ceiling(width), Glyph('0').Height);
    }

    public override void Render(DrawingContext context)
    {
        if (_digitWidth <= 0)
        {
            EnsureMetrics();
        }
        // toFixed(2), then ":1", drawn with tabular digits.
        var hundredths = (int)Math.Round(Math.Clamp(ShownRatio, 0, 99.99) * 100, MidpointRounding.AwayFromZero);
        var whole = hundredths / 100;
        var fraction = hundredths % 100;
        var x = 0.0;
        if (whole >= 10)
        {
            x = DrawDigit(context, (char)('0' + whole / 10), x);
        }
        x = DrawDigit(context, (char)('0' + whole % 10), x);
        x = DrawGlyph(context, '.', x);
        x = DrawDigit(context, (char)('0' + fraction / 10), x);
        x = DrawDigit(context, (char)('0' + fraction % 10), x);
        x = DrawGlyph(context, ':', x);
        DrawDigit(context, '1', x);
    }

    private double DrawDigit(DrawingContext context, char digit, double x)
    {
        var glyph = Glyph(digit);
        context.DrawText(glyph, new Point(x + (_digitWidth - glyph.WidthIncludingTrailingWhitespace) / 2, 0));
        return x + _digitWidth;
    }

    private double DrawGlyph(DrawingContext context, char c, double x)
    {
        var glyph = Glyph(c);
        context.DrawText(glyph, new Point(x, 0));
        return x + glyph.WidthIncludingTrailingWhitespace;
    }
}

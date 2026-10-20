using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The faint typing guide of the phone number: the rest of the country's example number in its expected shape, drawn
/// right after what was typed (Arc paints the typed part invisibly, then the rest). It shares the number's font, size,
/// tabular digits and line height and sits in the same box as the text presenter, so it lines up glyph for glyph. It
/// measures as zero width, so it never widens the editor.
/// </summary>
internal sealed class PhoneInputGuide : Control
{
    public static readonly StyledProperty<string?> TypedProperty =
        AvaloniaProperty.Register<PhoneInputGuide, string?>(nameof(Typed));

    public static readonly StyledProperty<string?> GuideProperty =
        AvaloniaProperty.Register<PhoneInputGuide, string?>(nameof(Guide));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<PhoneInputGuide, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<PhoneInputGuide>();

    public string? Typed { get => GetValue(TypedProperty); set => SetValue(TypedProperty, value); }

    public string? Guide { get => GetValue(GuideProperty); set => SetValue(GuideProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    private TextLayout? _layout;
    private double _typedWidth;

    static PhoneInputGuide()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<PhoneInputGuide>(false);
        ClipToBoundsProperty.OverrideDefaultValue<PhoneInputGuide>(false);
        AffectsMeasure<PhoneInputGuide>(TypedProperty, GuideProperty, LineHeightProperty, TextElement.FontFamilyProperty,
            TextElement.FontSizeProperty, TextElement.FontWeightProperty, TextElement.FontFeaturesProperty);
        AffectsRender<PhoneInputGuide>(ForegroundProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TypedProperty || change.Property == GuideProperty || change.Property == LineHeightProperty ||
            change.Property == ForegroundProperty || change.Property == TextElement.FontFamilyProperty ||
            change.Property == TextElement.FontSizeProperty || change.Property == TextElement.FontWeightProperty ||
            change.Property == TextElement.FontFeaturesProperty)
        {
            _layout?.Dispose();
            _layout = null;
        }
    }

    private TextLayout Layout()
    {
        if (_layout is not null)
        {
            return _layout;
        }
        var typed = Typed ?? string.Empty;
        var text = typed + (Guide ?? string.Empty);
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, TextElement.GetFontWeight(this));
        _layout = new TextLayout(text, typeface, TextElement.GetFontSize(this), Foreground, lineHeight: LineHeight,
            fontFeatures: TextElement.GetFontFeatures(this));
        _typedWidth = typed.Length == 0 ? 0 : _layout.HitTestTextPosition(typed.Length).X;
        return _layout;
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Layout().Height);

    public override void Render(DrawingContext context)
    {
        if (string.IsNullOrEmpty(Guide) || Foreground is null)
        {
            return;
        }
        var layout = Layout();
        // Everything up to the end of the typed text stays invisible; the guide continues from there.
        using (context.PushClip(new Rect(_typedWidth, -layout.Height, layout.WidthIncludingTrailingWhitespace + 4, layout.Height * 3)))
        {
            layout.Draw(context, default);
        }
    }
}

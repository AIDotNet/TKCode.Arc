using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Inline code in the answer text: the code in the monospace family at 0.85em on the muted surface, a 1px hairline
/// border, radius 6, padding 0.12em 0.38em. Embedded in the text as one object so it never breaks across lines (Arc's
/// chip moves to the next line whole) and sits on the text's baseline. It draws itself: one cached layout, no template.
/// </summary>
internal sealed class AiAnswerCodeChip : Control
{
    /// <summary>Vertical padding relative to the code size (Arc 0.12em).</summary>
    internal const double PaddingY = 0.12;

    /// <summary>Horizontal padding relative to the code size (Arc 0.38em).</summary>
    internal const double PaddingX = 0.38;

    /// <summary>Corner radius.</summary>
    internal const double Radius = 6;

    /// <summary>The code.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, string>(nameof(Text), string.Empty);

    /// <summary>The chip's fill (theme: SurfaceMuted).</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, IBrush?>(nameof(Background));

    /// <summary>The chip's border (the document's hairline).</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, IBrush?>(nameof(BorderBrush));

    /// <summary>The code's color (theme: Foreground).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, IBrush?>(nameof(Foreground));

    /// <summary>The monospace family (theme: MonoFontFamily).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, FontFamily>(nameof(FontFamily), FontFamily.Default);

    /// <summary>The code size (the text's size × 0.85, set by the text).</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<AiAnswerCodeChip, double>(nameof(FontSize), 13.6);

    private TextLayout? _layout;
    private Pen? _pen;

    static AiAnswerCodeChip()
    {
        AffectsMeasure<AiAnswerCodeChip>(TextProperty, FontFamilyProperty, FontSizeProperty);
        AffectsRender<AiAnswerCodeChip>(BackgroundProperty, BorderBrushProperty, ForegroundProperty);
        FocusableProperty.OverrideDefaultValue<AiAnswerCodeChip>(false);
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerCodeChip);

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="BackgroundProperty"/>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <inheritdoc cref="BorderBrushProperty"/>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty
            || change.Property == ForegroundProperty)
        {
            _layout?.Dispose();
            _layout = null;
        }
        else if (change.Property == BorderBrushProperty)
        {
            _pen = null;
        }
    }

    private TextLayout Layout()
    {
        return _layout ??= new TextLayout(Text, new Typeface(FontFamily), FontSize, Foreground,
            textWrapping: TextWrapping.NoWrap);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = Layout();
        var padX = Math.Round(FontSize * PaddingX, 1);
        var padY = Math.Round(FontSize * PaddingY, 1);
        // The text it sits in aligns this baseline with its own.
        TextBlock.SetBaselineOffset(this, 1 + padY + layout.Baseline);
        return new Size(Math.Ceiling(layout.WidthIncludingTrailingWhitespace) + 2 * (padX + 1), Math.Ceiling(layout.Height + 2 * (padY + 1)));
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (Background is { } fill)
        {
            context.DrawRectangle(fill, null, bounds.Deflate(0.5), Radius, Radius);
        }
        if (BorderBrush is { } border)
        {
            _pen ??= new Pen(border, 1);
            context.DrawRectangle(null, _pen, bounds.Deflate(0.5), Radius - 0.5, Radius - 0.5);
        }
        var layout = Layout();
        layout.Draw(context, new Point((bounds.Width - layout.WidthIncludingTrailingWhitespace) / 2, 1 + Math.Round(FontSize * PaddingY, 1)));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _layout?.Dispose();
        _layout = null;
    }
}

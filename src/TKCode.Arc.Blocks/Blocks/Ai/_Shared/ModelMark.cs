using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Draws the mark of a model or provider: no tile, only the glyph. A <see cref="Geometry"/> mark is a 24 × 24 shape filled
/// with <see cref="MarkColor"/> (or the foreground, which flips with the theme); an <see cref="IImage"/> mark, such as a
/// <see cref="DrawingImage"/>, keeps its own colors. Decorative: it has no automation name (the model's name sits next to it).
/// </summary>
internal sealed class ModelMark : Control
{
    private const double Box = 24;

    /// <summary>The mark: a <see cref="Geometry"/> in a 24 × 24 box or an <see cref="IImage"/>; anything else draws nothing.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<ModelMark, object?>(nameof(Mark));

    /// <summary>The fill of a <see cref="Geometry"/> mark; null uses <see cref="Foreground"/>.</summary>
    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<ModelMark, IBrush?>(nameof(MarkColor));

    /// <summary>The fallback fill of a <see cref="Geometry"/> mark (inherited text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ModelMark>();

    /// <summary>Width and height in DIPs: 16 in a pill or compact row, 18 in a featured row, 20 in a header.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ModelMark, double>(nameof(Size), 16);

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkColorProperty"/>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    static ModelMark()
    {
        AffectsMeasure<ModelMark>(SizeProperty);
        AffectsRender<ModelMark>(MarkProperty, MarkColorProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ModelMark>(false);
        FocusableProperty.OverrideDefaultValue<ModelMark>(false);
        // A fixed-size glyph centers itself in its cell instead of stretching.
        HorizontalAlignmentProperty.OverrideDefaultValue<ModelMark>(Avalonia.Layout.HorizontalAlignment.Center);
        VerticalAlignmentProperty.OverrideDefaultValue<ModelMark>(Avalonia.Layout.VerticalAlignment.Center);
    }

    /// <summary>
    /// The mark and its color for <paramref name="model"/>: its own, otherwise its provider's (found by
    /// <see cref="AiModel.ProviderId"/> in <paramref name="providers"/>). Both are null when neither has one.
    /// </summary>
    /// <param name="model">The model, or null.</param>
    /// <param name="providers">The providers to look the fallback up in, or null.</param>
    /// <param name="color">The mark color to use with the returned mark.</param>
    public static object? Resolve(AiModel? model, IEnumerable<AiModelProvider>? providers, out IBrush? color)
    {
        color = null;
        if (model is null)
        {
            return null;
        }
        if (model.Mark is { } own)
        {
            color = model.MarkColor;
            return own;
        }
        if (providers is not null && !string.IsNullOrEmpty(model.ProviderId))
        {
            foreach (var provider in providers)
            {
                if (provider.Id == model.ProviderId)
                {
                    color = provider.MarkColor;
                    return provider.Mark;
                }
            }
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var size = Size;
        switch (Mark)
        {
            case Geometry geometry when (MarkColor ?? Foreground) is { } brush:
            {
                var scale = size / Box;
                using (context.PushTransform(Matrix.CreateScale(scale, scale)))
                {
                    context.DrawGeometry(brush, null, geometry);
                }
                break;
            }
            case IImage image when image.Size is { Width: > 0, Height: > 0 } natural:
            {
                // Contain: keep the aspect ratio, centered in the square.
                var fit = Math.Min(size / natural.Width, size / natural.Height);
                var width = natural.Width * fit;
                var height = natural.Height * fit;
                context.DrawImage(image, new Rect(natural), new Rect((size - width) / 2, (size - height) / 2, width, height));
                break;
            }
        }
    }
}

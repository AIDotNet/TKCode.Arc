using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The neutral 14px payment-processor mark drawn before the card in the <see cref="SeatManager"/> footer: a rounded
/// square in the accent with two forward slashes cut in the accent's foreground. A fictional mark (never a real
/// processor's or network's logo); an image supplied in <see cref="SeatManagerPaymentMethod.Mark"/> replaces it.
/// </summary>
internal sealed class SeatManagerProcessorMark : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SeatManagerProcessorMark, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<SeatManagerProcessorMark, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<SeatManagerProcessorMark, IImage?>(nameof(Image));

    static SeatManagerProcessorMark()
    {
        AffectsRender<SeatManagerProcessorMark>(FillProperty, InkProperty, ImageProperty);
    }

    public SeatManagerProcessorMark()
    {
        Width = 14;
        Height = 14;
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (Image is { } image)
        {
            context.DrawImage(image, new Rect(image.Size), rect);
            return;
        }
        context.DrawRectangle(Fill, null, new RoundedRect(rect, 4));
        if (Ink is { } ink)
        {
            var pen = new ImmutablePen(ink.ToImmutable(), 1.6, lineCap: PenLineCap.Round);
            var w = rect.Width;
            var h = rect.Height;
            context.DrawLine(pen, new Point(w * 0.30, h * 0.72), new Point(w * 0.50, h * 0.28));
            context.DrawLine(pen, new Point(w * 0.52, h * 0.72), new Point(w * 0.72, h * 0.28));
        }
    }
}

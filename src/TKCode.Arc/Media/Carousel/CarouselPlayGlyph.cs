using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What the carousel play control offers.</summary>
internal enum CarouselPlayState
{
    Play,
    Pause
}

/// <summary>
/// Lucide play / pause drawn filled and stroked in the text color (Arc: <c>fill="currentColor"</c>), 15px with a
/// 1.75 stroke. <see cref="Template"/> builds one per state for the play button's icon swap.
/// </summary>
internal sealed class CarouselPlayGlyph : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<CarouselPlayGlyph>();

    /// <summary>Builds the glyph for a boxed <see cref="CarouselPlayState"/>.</summary>
    public static readonly IDataTemplate Template = new FuncDataTemplate<CarouselPlayState>(
        (state, _) => new CarouselPlayGlyph { State = state }, supportsRecycling: false);

    private Pen? _pen;
    private IBrush? _penBrush;

    static CarouselPlayGlyph()
    {
        AffectsRender<CarouselPlayGlyph>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CarouselPlayGlyph>(false);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public CarouselPlayState State { get; init; }

    /// <summary>Glyph size in px (15).</summary>
    public double Size { get; init; } = 15;

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Size / 24;
        var offsetX = (Bounds.Width - Size) / 2;
        var offsetY = (Bounds.Height - Size) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, _pen, State == CarouselPlayState.Pause ? ArcIcons.Pause : ArcIcons.Play);
        }
    }
}

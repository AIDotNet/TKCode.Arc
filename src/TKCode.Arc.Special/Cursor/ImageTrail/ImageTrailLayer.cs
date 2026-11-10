using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The drawing layer of an <see cref="ImageTrail"/>: every image on the trail, oldest first, each with its own pop,
/// tilt, fade and sink, drawn by this one control. A frame pushes a transform, an opacity and a rounded clip per image
/// and allocates nothing.
/// </summary>
internal sealed class ImageTrailLayer : Control
{
    public static readonly StyledProperty<IBrush?> PlaceholderProperty =
        AvaloniaProperty.Register<ImageTrailLayer, IBrush?>(nameof(Placeholder));

    public static readonly StyledProperty<IBrush?> OutlineProperty =
        AvaloniaProperty.Register<ImageTrailLayer, IBrush?>(nameof(Outline));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<ImageTrailLayer, BoxShadows>(nameof(Shadow));

    private IPen? _outlinePen;

    static ImageTrailLayer()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ImageTrailLayer>(false);
        AffectsRender<ImageTrailLayer>(PlaceholderProperty, ShadowProperty);
    }

    internal ImageTrail? Owner { get; set; }

    public IBrush? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public IBrush? Outline { get => GetValue(OutlineProperty); set => SetValue(OutlineProperty, value); }

    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OutlineProperty)
        {
            _outlinePen = null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var model = owner.Model;
        var moves = owner.MovesImages;
        var size = Math.Max(8, owner.ImageSize);
        var radius = owner.ImageCornerRadius;
        var pen = _outlinePen ??= Outline is { } outline ? new ImmutablePen(outline.ToImmutable(), 1) : null;
        var capacity = model.Capacity;
        for (var n = 0; n < capacity; n++)
        {
            var slot = (model.Oldest + n) % capacity;
            if (!model.IsLive(slot))
            {
                continue;
            }
            model.Pose(slot, moves, out var opacity, out var scale, out var sink);
            if (opacity <= 0.01)
            {
                continue;
            }
            var image = owner.ImageAt(model.Picture(slot));
            var width = size;
            var height = size * 0.75;
            if (image is { Size: { Width: > 0, Height: > 0 } natural })
            {
                // The longer side is ImageSize; the picture keeps its own shape.
                var aspect = natural.Width / natural.Height;
                width = aspect >= 1 ? size : size * aspect;
                height = aspect >= 1 ? size / aspect : size;
            }
            var box = new Rect(-width / 2, -height / 2, width, height);
            var tilt = moves ? model.Tilt(slot) * Math.PI / 180 : 0;
            var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateRotation(tilt)
                            * Matrix.CreateTranslation(model.X(slot), model.Y(slot) + sink);
            var rounded = new RoundedRect(box, radius);
            using (context.PushTransform(transform))
            using (context.PushOpacity(opacity))
            {
                context.DrawRectangle(Placeholder, null, rounded, Shadow);
                if (image is not null)
                {
                    using (context.PushClip(rounded))
                    {
                        context.DrawImage(image, box);
                    }
                }
                if (pen is not null)
                {
                    context.DrawRectangle(null, pen, rounded);
                }
            }
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The stickers of a <see cref="StickerPeel"/>: arranges each one at the place it is stuck (the placement fractions times
/// the surface size, so the arrangement scales with the container and survives resizes) and runs one
/// <see cref="LiveLoop"/> for all of them. The loop only runs while a sticker moves.
/// </summary>
internal sealed class StickerPeelLayer : Panel
{
    private readonly List<StickerPeelSticker> _stickers = [];
    private readonly LiveLoop _loop;
    private bool _glide;
    private int _topZ;

    public StickerPeelLayer()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    internal StickerPeel? Owner { get; set; }

    internal IReadOnlyList<StickerPeelSticker> Stickers => _stickers;

    /// <summary>Replaces the stickers with one per item.</summary>
    public void Rebuild(IEnumerable<StickerPeelItem> items)
    {
        Children.Clear();
        _stickers.Clear();
        foreach (var item in items)
        {
            var sticker = new StickerPeelSticker(item);
            _stickers.Add(sticker);
            Children.Add(sticker);
        }
        InvalidateMeasure();
    }

    /// <summary>The placements changed from outside: stickers glide from where they are to the new places.</summary>
    public void RequestGlide()
    {
        _glide = true;
        InvalidateArrange();
    }

    /// <summary>Something started moving: asks for frames.</summary>
    public void Wake()
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            // The loop does not run with motion off: poses jump to where they are headed.
            foreach (var sticker in _stickers)
            {
                sticker.Snap();
            }
            return;
        }
        _loop.Start();
    }

    public void BringToFront(StickerPeelSticker sticker) => sticker.ZIndex = ++_topZ;

    public void Announce(string message) => Owner?.Announce(message);

    /// <summary>A sticker landed. <paramref name="moved"/> says whether it is stuck somewhere new.</summary>
    public void StickerSettled(StickerPeelSticker sticker, bool moved)
    {
        if (moved)
        {
            Owner?.OnPlaced(sticker);
        }
    }

    private bool Step(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        foreach (var sticker in _stickers)
        {
            moving |= sticker.Step(dt, full);
        }
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var sticker in _stickers)
        {
            sticker.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var glide = _glide;
        _glide = false;
        foreach (var sticker in _stickers)
        {
            var placement = Owner?.GetPlacement(sticker.Item) ?? new StickerPlacement(sticker.Item.X, sticker.Item.Y, sticker.Item.Rotate);
            var size = sticker.DesiredSize;
            var center = new Point(placement.X * finalSize.Width, placement.Y * finalSize.Height);
            var glided = false;
            if (glide && sticker.HasHome)
            {
                var delta = new Vector(sticker.HomeCenter.X - center.X, sticker.HomeCenter.Y - center.Y);
                if (delta.Length > 0.5)
                {
                    sticker.Glide(delta);
                    glided = true;
                }
            }
            sticker.HomeCenter = center;
            sticker.HasHome = true;
            if (glided || sticker.Motion.Phase == StickerPeelPhase.Rest)
            {
                sticker.RememberHome();
            }
            sticker.RestRotation = placement.Rotate;
            sticker.Arrange(new Rect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

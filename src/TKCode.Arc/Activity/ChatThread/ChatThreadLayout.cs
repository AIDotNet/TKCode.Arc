using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A message row (Arc <c>.row</c>): the author's 28px gutter and 8px gap beside other people's messages, then the
/// message stack, at most <see cref="MaxRatio"/> of the row (78%, 84% when narrow) and 34rem wide; your own messages
/// hug the right edge.
/// </summary>
internal sealed class ChatThreadRowPanel : Panel
{
    public static readonly StyledProperty<bool> IsMineProperty =
        AvaloniaProperty.Register<ChatThreadRowPanel, bool>(nameof(IsMine));

    public static readonly StyledProperty<double> MaxRatioProperty =
        AvaloniaProperty.Register<ChatThreadRowPanel, double>(nameof(MaxRatio), 0.78);

    public static readonly StyledProperty<double> MaxStackWidthProperty =
        AvaloniaProperty.Register<ChatThreadRowPanel, double>(nameof(MaxStackWidth), 544);

    public static readonly StyledProperty<double> LeadProperty =
        AvaloniaProperty.Register<ChatThreadRowPanel, double>(nameof(Lead), 36);

    public bool IsMine { get => GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }
    public double MaxRatio { get => GetValue(MaxRatioProperty); set => SetValue(MaxRatioProperty, value); }
    public double MaxStackWidth { get => GetValue(MaxStackWidthProperty); set => SetValue(MaxStackWidthProperty, value); }

    /// <summary>Gutter plus gap before other people's messages (36px).</summary>
    public double Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    static ChatThreadRowPanel()
    {
        AffectsMeasure<ChatThreadRowPanel>(IsMineProperty, MaxRatioProperty, MaxStackWidthProperty, LeadProperty);
    }

    private double Limit(double width)
    {
        if (!double.IsFinite(width))
        {
            return MaxStackWidth;
        }
        var limit = Math.Min(width * MaxRatio, MaxStackWidth);
        return IsMine ? Math.Min(limit, width) : Math.Max(0, Math.Min(limit, width - Lead));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var limit = Limit(availableSize.Width);
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(limit, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : limit + (IsMine ? 0 : Lead);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            var x = IsMine ? finalSize.Width - size.Width : Lead;
            child.Arrange(new Rect(new Point(x, 0), new Size(size.Width, finalSize.Height)));
        }
        return finalSize;
    }
}

/// <summary>
/// A message's line (Arc <c>.line</c>): the parts (pictures, files, bubble) and the add-reaction button side by side,
/// vertically centered, 6px apart (reversed for your own messages); the author's portrait hangs in the gutter, 36px to
/// the left, level with the bottom of the line.
/// </summary>
internal sealed class ChatThreadLinePanel : Panel
{
    public static readonly StyledProperty<bool> IsMineProperty =
        AvaloniaProperty.Register<ChatThreadLinePanel, bool>(nameof(IsMine));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ChatThreadLinePanel, double>(nameof(Gap), 6);

    public static readonly StyledProperty<double> FaceOffsetProperty =
        AvaloniaProperty.Register<ChatThreadLinePanel, double>(nameof(FaceOffset), 36);

    public bool IsMine { get => GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public double FaceOffset { get => GetValue(FaceOffsetProperty); set => SetValue(FaceOffsetProperty, value); }

    /// <summary>The parts column (first child that is not the face or the button).</summary>
    public Control? Parts { get; set; }

    /// <summary>The add-reaction button.</summary>
    public Control? Button { get; set; }

    /// <summary>The author's portrait (other people's last message of a group).</summary>
    public Control? Face { get; set; }

    static ChatThreadLinePanel()
    {
        AffectsMeasure<ChatThreadLinePanel>(IsMineProperty, GapProperty, FaceOffsetProperty);
    }

    public ChatThreadLinePanel()
    {
        ClipToBounds = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var button = Button is { IsVisible: true } b ? b : null;
        var buttonSize = default(Size);
        if (button is not null)
        {
            button.Measure(Size.Infinity);
            buttonSize = button.DesiredSize;
        }
        var reserve = button is null ? 0 : buttonSize.Width + Gap;
        var partsSize = default(Size);
        if (Parts is { } parts)
        {
            parts.Measure(new Size(Math.Max(0, availableSize.Width - reserve), double.PositiveInfinity));
            partsSize = parts.DesiredSize;
        }
        if (Face is { } face)
        {
            face.Measure(Size.Infinity);
        }
        return new Size(partsSize.Width + reserve, Math.Max(partsSize.Height, buttonSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var button = Button is { IsVisible: true } b ? b : null;
        var buttonSize = button?.DesiredSize ?? default;
        var partsSize = Parts?.DesiredSize ?? default;
        var height = finalSize.Height;
        if (IsMine)
        {
            var x = 0.0;
            if (button is not null)
            {
                button.Arrange(new Rect(new Point(0, (height - buttonSize.Height) / 2), buttonSize));
                x = buttonSize.Width + Gap;
            }
            Parts?.Arrange(new Rect(new Point(x, (height - partsSize.Height) / 2), partsSize));
        }
        else
        {
            Parts?.Arrange(new Rect(new Point(0, (height - partsSize.Height) / 2), partsSize));
            button?.Arrange(new Rect(new Point(partsSize.Width + Gap, (height - buttonSize.Height) / 2), buttonSize));
        }
        if (Face is { } face)
        {
            var size = face.DesiredSize;
            face.Arrange(new Rect(new Point(-FaceOffset, height - size.Height), size));
        }
        return finalSize;
    }
}

/// <summary>A chat bubble: Arc's 18px radius with 6px corners on the author's side inside a group.</summary>
internal sealed class ChatThreadBubble : Border
{
    public static readonly StyledProperty<bool> IsMineProperty =
        AvaloniaProperty.Register<ChatThreadBubble, bool>(nameof(IsMine));

    public static readonly StyledProperty<bool> IsFailedProperty =
        AvaloniaProperty.Register<ChatThreadBubble, bool>(nameof(IsFailed));

    public bool IsMine { get => GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }
    public bool IsFailed { get => GetValue(IsFailedProperty); set => SetValue(IsFailedProperty, value); }

    protected override Type StyleKeyOverride => typeof(ChatThreadBubble);

    /// <summary>The corners for a bubble in its group.</summary>
    public static CornerRadius Corners(bool mine, bool first, bool last, double radius, double tight)
    {
        var top = first ? radius : tight;
        var bottom = last ? radius : tight;
        return mine
            ? new CornerRadius(radius, top, bottom, radius)
            : new CornerRadius(top, radius, radius, bottom);
    }
}

/// <summary>
/// A message's pictures (Arc <c>.gallery</c>): 240px wide, one picture at its own aspect ratio (4:3 without one),
/// two to four in two columns of squares (three: the first spans both columns at 2:1), a "+N" scrim on the fourth
/// when there are more, 3px gaps, 16px outer corners and 6px between tiles.
/// </summary>
internal sealed class ChatThreadGallery : Panel
{
    public static readonly StyledProperty<double> GalleryWidthProperty =
        AvaloniaProperty.Register<ChatThreadGallery, double>(nameof(GalleryWidth), 240);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ChatThreadGallery, double>(nameof(Gap), 3);

    public double GalleryWidth { get => GetValue(GalleryWidthProperty); set => SetValue(GalleryWidthProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Width / height of a lone picture (from its intrinsic size, else 4:3).</summary>
    public double SingleAspect { get; set; } = 4.0 / 3.0;

    static ChatThreadGallery()
    {
        AffectsMeasure<ChatThreadGallery>(GalleryWidthProperty, GapProperty);
    }

    private IEnumerable<Rect> Slots(double width)
    {
        var count = Children.Count;
        if (count == 1)
        {
            yield return new Rect(0, 0, width, width / Math.Max(0.1, SingleAspect));
            yield break;
        }
        var gap = Gap;
        var cell = (width - gap) / 2;
        var y = 0.0;
        var index = 0;
        if (count == 3)
        {
            yield return new Rect(0, 0, width, width / 2);
            y = width / 2 + gap;
            index = 1;
        }
        for (var i = index; i < count; i++)
        {
            var column = (i - index) % 2;
            var row = (i - index) / 2;
            yield return new Rect(column * (cell + gap), y + row * (cell + gap), cell, cell);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(GalleryWidth, double.IsFinite(availableSize.Width) ? availableSize.Width : GalleryWidth);
        var height = 0.0;
        using var slots = Slots(width).GetEnumerator();
        foreach (var child in Children)
        {
            if (!slots.MoveNext())
            {
                break;
            }
            var slot = slots.Current;
            child.Measure(slot.Size);
            height = Math.Max(height, slot.Bottom);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        using var slots = Slots(finalSize.Width).GetEnumerator();
        foreach (var child in Children)
        {
            if (!slots.MoveNext())
            {
                break;
            }
            child.Arrange(slots.Current);
        }
        return finalSize;
    }
}

/// <summary>The hidden-overflow column of a picture tile; its corners follow the gallery's shape.</summary>
internal static class ChatThreadGalleryCorners
{
    public static CornerRadius For(int index, int count, double outer, double inner)
    {
        if (count == 1)
        {
            return new CornerRadius(outer);
        }
        // Tiles keep a 6px radius; the gallery's 16px outer corners clip the outside ones.
        var tl = inner;
        var tr = inner;
        var br = inner;
        var bl = inner;
        var rows = count == 3 ? 2 : (count + 1) / 2;
        var row = count == 3 ? (index == 0 ? 0 : 1) : index / 2;
        var leftColumn = count == 3 ? index != 2 : index % 2 == 0;
        var rightColumn = count == 3 ? index != 1 : index % 2 == 1 || (index == count - 1 && count % 2 == 1 && count != 3);
        if (row == 0 && leftColumn)
        {
            tl = outer;
        }
        if (row == 0 && rightColumn)
        {
            tr = outer;
        }
        if (row == rows - 1 && leftColumn)
        {
            bl = outer;
        }
        if (row == rows - 1 && rightColumn)
        {
            br = outer;
        }
        return new CornerRadius(tl, tr, br, bl);
    }
}

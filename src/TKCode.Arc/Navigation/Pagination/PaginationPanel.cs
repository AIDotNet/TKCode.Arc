using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lays out the pagination row (flex-wrap with a gap) and runs the window belt: when the five-page window moves
/// by <c>n</c> slots, every number is drawn from where it was and springs to its new slot, entering numbers fade
/// in (standard + enter) and leaving numbers are drawn as ghosts that slide on and fade out in 120ms. The current
/// page mark (a <see cref="GlidingIndicator"/> child) is arranged over the whole panel and never takes a slot.
/// </summary>
internal sealed class PaginationPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PaginationPanel, double>(nameof(Spacing), 4);

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public Action? Arranged { get; set; }

    public PaginationButton?[]? Slots { get; set; }

    private readonly PaginationGhostLayer _ghostLayer = new();

    static PaginationPanel()
    {
        AffectsMeasure<PaginationPanel>(SpacingProperty);
    }

    public PaginationPanel()
    {
        Children.Add(_ghostLayer);
    }

    internal int GhostCount => _ghostLayer.Ghosts.Count;

    /// <summary>Starts the belt for a window move of <paramref name="shift"/> slots (full motion only).</summary>
    public void StartBelt(int shift, int oldStart, int oldShown, int newStart, int newShown)
    {
        var slots = Slots;
        if (slots is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        var first = slots[0];
        if (first is null || first.Bounds.Width <= 0)
        {
            return;
        }
        // One slot is a button plus the gap (Arc: 36px + 4px). Long jumps travel at most one window.
        var pitch = first.Bounds.Width + Spacing;
        var travel = Math.Clamp(shift, -Pagination.WindowSize, Pagination.WindowSize) * pitch;

        Span<double> previous = stackalloc double[Pagination.WindowSize];
        for (var i = 0; i < slots.Length; i++)
        {
            previous[i] = slots[i]?.BeltOffset ?? 0;
        }
        for (var i = 0; i < newShown && i < slots.Length; i++)
        {
            if (slots[i] is not { } slot)
            {
                continue;
            }
            var number = newStart + i;
            var oldIndex = number - oldStart;
            var stayed = oldIndex >= 0 && oldIndex < oldShown;
            var from = travel + (stayed && oldIndex < slots.Length ? previous[oldIndex] : 0);
            slot.Travel(from, fadeIn: !stayed);
        }
        for (var j = 0; j < oldShown && j < slots.Length; j++)
        {
            var number = oldStart + j;
            if (number >= newStart && number < newStart + newShown)
            {
                continue;
            }
            if (slots[j] is not { } slot)
            {
                continue;
            }
            AddGhost(number, slot, previous[j], -travel);
        }
    }

    private void AddGhost(int number, PaginationButton slot, double from, double by)
    {
        var text = new FormattedText(number.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(slot.FontFamily, slot.FontStyle, slot.FontWeight), slot.FontSize, slot.Foreground);
        text.SetFontFeatures(slot.FontFeatures);
        var layer = _ghostLayer;
        var x = new MotionValue<double>(layer, from, MotionVectors.Double);
        var opacity = new MotionValue<double>(layer, 1, MotionVectors.Double);
        var ghost = new PaginationGhostLayer.Ghost { Text = text, Slot = slot.Bounds, X = x, Opacity = opacity };
        x.Changed += _ => layer.InvalidateVisual();
        opacity.Changed += _ => layer.InvalidateVisual();
        layer.Ghosts.Add(ghost);
        _ = x.AnimateTo(from + by, MotionSpec.Spring(ArcSpring.Smooth));
        RemoveWhenFaded(ghost);
    }

    private async void RemoveWhenFaded(PaginationGhostLayer.Ghost ghost)
    {
        await ghost.Opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        ghost.X.Stop();
        _ghostLayer.Ghosts.Remove(ghost);
        _ghostLayer.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = availableSize.Width;
        double x = 0, y = 0, lineHeight = 0, width = 0;
        var spacing = Spacing;
        foreach (var child in Children)
        {
            if (child is GlidingIndicator or PaginationGhostLayer)
            {
                child.Measure(availableSize);
                continue;
            }
            child.Measure(Size.Infinity);
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > maxWidth)
            {
                y += lineHeight + spacing;
                x = 0;
                lineHeight = 0;
            }
            x += size.Width + spacing;
            width = Math.Max(width, x - spacing);
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return new Size(width, y + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, lineHeight = 0;
        var spacing = Spacing;
        foreach (var child in Children)
        {
            if (child is GlidingIndicator or PaginationGhostLayer)
            {
                child.Arrange(new Rect(finalSize));
                continue;
            }
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width + 0.5)
            {
                y += lineHeight + spacing;
                x = 0;
                lineHeight = 0;
            }
            child.Arrange(new Rect(new Point(x, y), size));
            x += size.Width + spacing;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        Arranged?.Invoke();
        return finalSize;
    }
}

/// <summary>Draws the numbers leaving the pagination window (no control per ghost).</summary>
internal sealed class PaginationGhostLayer : Control
{
    internal sealed class Ghost
    {
        public required FormattedText Text { get; init; }
        public required Rect Slot { get; init; }
        public required MotionValue<double> X { get; init; }
        public required MotionValue<double> Opacity { get; init; }
    }

    public List<Ghost> Ghosts { get; } = new();

    public PaginationGhostLayer()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        foreach (var ghost in Ghosts)
        {
            var opacity = Math.Clamp(ghost.Opacity.Value, 0, 1);
            if (opacity <= 0.001)
            {
                continue;
            }
            var slot = ghost.Slot;
            var origin = new Point(slot.X + ghost.X.Value + (slot.Width - ghost.Text.Width) / 2, slot.Y + (slot.Height - ghost.Text.Height) / 2);
            using (context.PushOpacity(opacity))
            {
                context.DrawText(ghost.Text, origin);
            }
        }
    }
}

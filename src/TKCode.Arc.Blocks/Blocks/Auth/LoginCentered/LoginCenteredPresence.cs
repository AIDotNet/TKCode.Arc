// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): AnimatePresence mode="popLayout" in login-centered.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Shows one piece of content at a time and swaps it the way Arc's <c>AnimatePresence mode="popLayout"</c> does: the
/// leaving content leaves layout at once and keeps its last place while it fades, so only the new content sizes the
/// panel. Opacity, a vertical offset and a blur run per channel as <see cref="PresenceStyle"/> says.
/// </summary>
/// <remarks>
/// Code-driven because the leaving content must stay on screen after it is replaced (a ControlTheme only animates one
/// element between states), and springs carry their velocity when a swap interrupts another.
/// </remarks>
internal class LoginCenteredPresence : Panel
{
    private sealed class Item
    {
        public Item(Control child)
        {
            Child = child;
        }

        public Control Child { get; }

        public TranslateTransform Transform { get; } = new();

        public MotionValue<double>? Opacity;
        public MotionValue<double>? Offset;
        public MotionValue<double>? Blur;
        public bool IsExiting;
        public Rect Rect;
    }

    private readonly List<Item> _items = new();
    private Item? _current;

    /// <summary>The motion of swaps.</summary>
    public LoginCenteredPresenceStyle PresenceStyle { get; set; } = LoginCenteredMotion.Swap;

    /// <summary>Where the content sits across the panel: Left (status lines) or Center (rolling characters); Stretch fills it.</summary>
    public HorizontalAlignment ContentAlignment { get; set; } = HorizontalAlignment.Left;

    /// <summary>The content on show (not the leaving one).</summary>
    public Control? Current => _current?.Child;

    /// <summary>Content still leaving (tests read it).</summary>
    internal int ExitingCount => _items.Count(i => i.IsExiting);

    /// <summary>
    /// Shows <paramref name="next"/> (null shows nothing). With <paramref name="animate"/> and motion, the new content
    /// enters from <paramref name="enterOffset"/> and the old one leaves towards <paramref name="exitOffset"/>; otherwise
    /// the swap is immediate (Arc's <c>initial={false}</c>).
    /// </summary>
    public void Show(Control? next, bool animate, double enterOffset, double exitOffset)
    {
        var level = IsAttachedToVisualTree() && animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var old = _current;
        _current = null;
        if (old is not null)
        {
            Leave(old, level, exitOffset);
        }
        if (next is null)
        {
            InvalidateMeasure();
            return;
        }
        var item = new Item(next);
        next.RenderTransform = item.Transform;
        _items.Add(item);
        _current = item;
        Children.Add(next);
        Enter(item, level, enterOffset);
        InvalidateMeasure();
    }

    /// <summary>Removes every child at once, leaving or not.</summary>
    public void Clear()
    {
        foreach (var item in _items)
        {
            Stop(item);
        }
        _items.Clear();
        _current = null;
        Children.Clear();
    }

    private bool IsAttachedToVisualTree() => VisualRoot is not null;

    private void Enter(Item item, ArcMotionLevel level, double offset)
    {
        var style = PresenceStyle;
        if (level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && style.ReducedEnter is null))
        {
            item.Child.Opacity = 1;
            item.Child.Effect = null;
            item.Transform.Y = 0;
            return;
        }
        var opacity = Value(item, 0, v => item.Child.Opacity = v);
        item.Opacity = opacity;
        if (level == ArcMotionLevel.Reduced)
        {
            // Reduced motion keeps only the fade (Arc's reduce branches: opacity on the instant duration).
            _ = opacity.AnimateTo(1, style.ReducedEnter!);
            return;
        }
        item.Offset = Value(item, offset, v => item.Transform.Y = v);
        item.Blur = Value(item, style.EnterBlur, v => item.Child.Effect = ArcBlur.Get(v));
        _ = opacity.AnimateTo(1, style.EnterOpacity);
        _ = item.Offset.AnimateTo(0, style.EnterOffset);
        _ = item.Blur.AnimateTo(0, style.EnterBlurSpec);
    }

    private async void Leave(Item item, ArcMotionLevel level, double offset)
    {
        item.IsExiting = true;
        if (level != ArcMotionLevel.Full)
        {
            Remove(item);
            return;
        }
        var style = PresenceStyle;
        item.Opacity ??= Value(item, item.Child.Opacity, v => item.Child.Opacity = v);
        item.Offset ??= Value(item, item.Transform.Y, v => item.Transform.Y = v);
        item.Blur ??= Value(item, 0, v => item.Child.Effect = ArcBlur.Get(v));
        var fade = item.Opacity.AnimateTo(0, style.ExitOpacity);
        var move = item.Offset.AnimateTo(offset, style.ExitOffset);
        var blur = item.Blur.AnimateTo(style.ExitBlur, style.ExitBlurSpec);
        await Task.WhenAll(fade, move, blur);
        Remove(item);
    }

    private MotionValue<double> Value(Item item, double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        apply(initial);
        return value;
    }

    private static void Stop(Item item)
    {
        item.Opacity?.Dispose();
        item.Offset?.Dispose();
        item.Blur?.Dispose();
    }

    private void Remove(Item item)
    {
        if (!_items.Remove(item))
        {
            return;
        }
        Stop(item);
        Children.Remove(item.Child);
        item.Child.Effect = null;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var constraint = ContentAlignment == HorizontalAlignment.Stretch
            ? new Size(availableSize.Width, double.PositiveInfinity)
            : availableSize;
        var desired = default(Size);
        foreach (var item in _items)
        {
            item.Child.Measure(constraint);
            if (ReferenceEquals(item, _current))
            {
                desired = item.Child.DesiredSize;
            }
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var item in _items)
        {
            if (ReferenceEquals(item, _current))
            {
                var size = item.Child.DesiredSize;
                var width = ContentAlignment == HorizontalAlignment.Stretch ? finalSize.Width : Math.Min(size.Width, finalSize.Width);
                var x = ContentAlignment switch
                {
                    HorizontalAlignment.Center => (finalSize.Width - width) / 2,
                    HorizontalAlignment.Right => finalSize.Width - width,
                    _ => 0
                };
                item.Rect = new Rect(x, 0, width, ContentAlignment == HorizontalAlignment.Stretch ? size.Height : finalSize.Height);
            }
            else if (item.Rect.Width <= 0 && item.Rect.Height <= 0)
            {
                // Replaced before its first arrange: it leaves from where it would have been.
                item.Rect = new Rect(item.Child.DesiredSize);
            }
            item.Child.Arrange(item.Rect);
        }
        return finalSize;
    }
}

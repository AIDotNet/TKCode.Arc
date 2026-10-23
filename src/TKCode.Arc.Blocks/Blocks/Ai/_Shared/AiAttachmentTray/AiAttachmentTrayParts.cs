using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The tray's row: its children side by side, <see cref="Spacing"/> apart, each at its own size. After
/// <see cref="RequestShift"/> the next arrange slides every child whose place changed from its old place to the new one
/// (FLIP: the layout jumps, a render offset springs back to zero), so the chips after a removed one close the gap on a
/// spring without the row laying out again during the motion.
/// </summary>
internal sealed class AiAttachmentTrayPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AiAttachmentTrayPanel, double>(nameof(Spacing), 8);

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private bool _shift;

    static AiAttachmentTrayPanel()
    {
        AffectsMeasure<AiAttachmentTrayPanel>(SpacingProperty);
    }

    /// <summary>The next arrange slides the children that move (only at full motion, and only once the row is showing).</summary>
    public void RequestShift()
    {
        _shift = IsLoaded && ArcMotion.IsFull(this);
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var count = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (!child.IsVisible)
            {
                continue;
            }
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            count++;
        }
        return new Size(width + Math.Max(0, count - 1) * Spacing, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var shift = _shift;
        _shift = false;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            if (child is AiAttachmentTrayItem item)
            {
                if (shift && !double.IsNaN(item.LastX) && Math.Abs(item.LastX - x) > 0.5)
                {
                    item.ShiftFrom(item.LastX - x);
                }
                item.LastX = x;
            }
            child.Arrange(new Rect(x, 0, size.Width, finalSize.Height));
            x += size.Width + Spacing;
        }
        return finalSize;
    }
}

/// <summary>
/// One place in the tray: the chip inside a <see cref="PresenceHost"/> (scale 0.92 and fade in on the settle spring,
/// out on a short tween), and a horizontal render offset for the slide after a neighbour leaves. The slide is
/// code-driven (<see cref="MotionValue{T}"/>): its start is the measured distance between the chip's old and new place,
/// which a ControlTheme cannot know, and a second removal mid-slide must continue from the current offset and speed.
/// </summary>
internal sealed class AiAttachmentTrayItem : Decorator
{
    /// <summary>The slide that closes a gap (Arc: visualDuration 0.31, bounce 0.05).</summary>
    internal static readonly SpringSpec ShiftSpring = SpringSpec.FromVisual(0.31, 0.05);

    /// <summary>The scale a chip grows from.</summary>
    internal const double EnterScale = 0.92;

    private readonly TranslateTransform _offset = new();
    private MotionValue<double>? _shift;
    private Action? _left;

    public AiAttachmentTrayItem(AiAttachment attachment, AiAttachmentChip chip)
    {
        Attachment = attachment;
        Chip = chip;
        Presence = new PresenceHost
        {
            Mode = PresenceMode.Scale,
            EnterScale = EnterScale,
            Spring = ArcSpring.Settle,
            Child = chip
        };
        Presence.Exited += OnExited;
        Child = Presence;
        RenderTransform = _offset;
    }

    public AiAttachment Attachment { get; }

    public AiAttachmentChip Chip { get; }

    public PresenceHost Presence { get; }

    /// <summary>True once the attachment is gone and the chip is on its way out.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The x the panel last placed it at (NaN before the first arrange).</summary>
    public double LastX { get; set; } = double.NaN;

    /// <summary>The current slide offset.</summary>
    public double Offset => _offset.X;

    /// <summary>Starts the entrance once the presence host is loaded (it plays nothing before).</summary>
    public void EnterWhenLoaded()
    {
        if (Presence.IsLoaded)
        {
            Presence.IsPresent = true;
            return;
        }
        Presence.Loaded += OnPresenceLoaded;
    }

    private void OnPresenceLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Presence.Loaded -= OnPresenceLoaded;
        if (!IsLeaving)
        {
            Presence.IsPresent = true;
            InvalidateMeasure();
        }
    }

    /// <summary>Plays the exit; <paramref name="left"/> runs when it is over (or at once if the chip is taken out of the window first).</summary>
    public void BeginLeave(Action left)
    {
        IsLeaving = true;
        _left = left;
        IsHitTestVisible = false;
        // Out of the tab order at once: the button is about to go.
        if (Chip.ActionButton is { } button)
        {
            KeyboardNavigation.SetIsTabStop(button, false);
        }
        Presence.Loaded -= OnPresenceLoaded;
        if (!Presence.IsPresent)
        {
            // It never got to come in.
            Finish();
            return;
        }
        Presence.IsPresent = false;
    }

    private void OnExited(object? sender, EventArgs e) => Finish();

    /// <summary>
    /// A chip waiting for its entrance is hidden by its presence host, but its place in the row is taken at once (Arc lays
    /// the new chip out immediately and only scales and fades it), so the row does not open in a second step. A compact
    /// chip sizes to its text: its height is known at once, its width once the entrance makes it visible (a frame later).
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (!IsLeaving && !Presence.IsPresent)
        {
            var width = double.IsFinite(Chip.Width) ? Math.Max(size.Width, Chip.Width) : size.Width;
            var height = double.IsFinite(Chip.Height) ? Math.Max(size.Height, Chip.Height) : size.Height;
            size = new Size(width, height);
        }
        return size;
    }

    private void Finish()
    {
        var left = _left;
        _left = null;
        left?.Invoke();
    }

    /// <summary>Slides from <paramref name="delta"/> px away from the new place back to it, keeping any slide in progress.</summary>
    public void ShiftFrom(double delta)
    {
        _shift ??= CreateShift();
        var velocity = _shift.IsAnimating ? _shift.Velocity : 0;
        _shift.Jump(_offset.X + delta);
        _ = _shift.AnimateTo(0, MotionSpec.Spring(ShiftSpring), velocity);
    }

    private MotionValue<double> CreateShift()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += x => _offset.X = x;
        return value;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A slide does not survive leaving the window. (A chip that was leaving stays out of the source; the tray rebuilds
        // its row from the source when it comes back.)
        _shift?.Stop();
        _offset.X = 0;
    }
}

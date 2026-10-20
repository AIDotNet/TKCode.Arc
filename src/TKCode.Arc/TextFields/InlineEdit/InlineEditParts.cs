using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The resting text of an <see cref="InlineEdit"/>: a button (Enter / Space edits) that shows the value.</summary>
internal sealed class InlineEditDisplay : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(InlineEditDisplay);
}

/// <summary>The field that replaces the text while editing, with the same metrics and no frame of its own.</summary>
internal sealed class InlineEditEditor : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(InlineEditEditor);
}

/// <summary>The 28px round save (primary: foreground fill) and cancel (outlined) buttons shown while editing.</summary>
internal sealed class InlineEditActionButton : AvaloniaButton
{
    public static readonly StyledProperty<bool> IsPrimaryProperty =
        AvaloniaProperty.Register<InlineEditActionButton, bool>(nameof(IsPrimary));

    public bool IsPrimary { get => GetValue(IsPrimaryProperty); set => SetValue(IsPrimaryProperty, value); }

    protected override Type StyleKeyOverride => typeof(InlineEditActionButton);
}

/// <summary>The underlined "Try again" link in a failed-save message.</summary>
internal sealed class InlineEditRetryLink : AvaloniaButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<InlineEditRetryLink, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(InlineEditRetryLink);
}

/// <summary>Why the inline edit box changes size, which decides the spring that follows it.</summary>
internal enum InlineEditSizeCause
{
    /// <summary>A container resize or late font: follow at once.</summary>
    Passive,

    /// <summary>Keystrokes: a quick spring without overshoot (snappy, 0.16s, no bounce).</summary>
    Typing,

    /// <summary>Entering or leaving edit mode, a commit or a rollback: the morph spring.</summary>
    Morph
}

/// <summary>
/// Lays out an <see cref="InlineEdit"/>'s box (P5 / P6). Single line: the box and the field take a new width at
/// once (the field never scrolls) while the visible frame springs after it and the action slot rides the frame's
/// edge. Multiline: the box height springs, so content below glides, while the field already has its room.
/// Only arrangement changes while the frame width springs; the multiline height is the one intended layout spring.
/// </summary>
internal sealed class InlineEditBox : Panel
{
    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<InlineEditBox, bool>(nameof(IsMultiline));

    public bool IsMultiline { get => GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }

    public Control? Display { get; set; }
    public Control? Editor { get; set; }
    public Control? Frame { get; set; }
    public Control? Ring { get; set; }
    public Control? Slot { get; set; }

    /// <summary>The cause of the next size change; consumed by the next measure.</summary>
    public InlineEditSizeCause PendingCause { get; set; }

    /// <summary>Current width of the visible frame (single line).</summary>
    public double FrameWidth => _frameWidth?.Value ?? _natural.Width;

    private static readonly SpringSpec TypingSpring = SpringSpec.FromVisual(0.16, 0);

    private MotionValue<double>? _frameWidth;
    private MotionValue<double>? _boxHeight;
    private Size _natural;
    private bool _measuring;

    static InlineEditBox()
    {
        AffectsMeasure<InlineEditBox>(IsMultilineProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            var multiline = IsMultiline;
            var display = Display;
            if (display is null)
            {
                return default;
            }
            display.Measure(multiline ? availableSize.WithHeight(double.PositiveInfinity) : new Size(availableSize.Width, double.PositiveInfinity));
            var natural = display.DesiredSize;
            var boxWidth = multiline && !double.IsInfinity(availableSize.Width)
                ? availableSize.Width
                : Math.Min(natural.Width, availableSize.Width);
            var cause = PendingCause;
            PendingCause = InlineEditSizeCause.Passive;
            var changed = Math.Abs(natural.Width - _natural.Width) > 0.1 || Math.Abs(natural.Height - _natural.Height) > 0.1;
            var first = _natural == default;
            _natural = natural;
            var animate = !first && cause != InlineEditSizeCause.Passive && IsLoaded && ArcMotion.IsFull(this);
            if (multiline)
            {
                _boxHeight ??= Create(natural.Height, layout: true);
                if (changed || Math.Abs(_boxHeight.Target - natural.Height) > 0.1)
                {
                    if (animate)
                    {
                        _ = _boxHeight.AnimateTo(natural.Height, MotionSpec.Spring(ArcSpring.Smooth));
                    }
                    else
                    {
                        _boxHeight.Jump(natural.Height);
                    }
                }
            }
            else
            {
                _frameWidth ??= Create(boxWidth, layout: false);
                if (Math.Abs(_frameWidth.Target - boxWidth) > 0.1)
                {
                    if (animate)
                    {
                        _ = _frameWidth.AnimateTo(boxWidth, cause == InlineEditSizeCause.Typing
                            ? MotionSpec.Spring(TypingSpring)
                            : MotionSpec.Spring(ArcSpring.Morph));
                    }
                    else
                    {
                        _frameWidth.Jump(boxWidth);
                    }
                }
            }
            var boxHeight = multiline ? _boxHeight!.Value : natural.Height;
            Editor?.Measure(new Size(boxWidth, Math.Max(boxHeight, natural.Height)));
            Frame?.Measure(new Size(boxWidth, boxHeight));
            Ring?.Measure(new Size(boxWidth + 6, boxHeight + 6));
            Slot?.Measure(Size.Infinity);
            return new Size(boxWidth, boxHeight);
        }
        finally
        {
            _measuring = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var multiline = IsMultiline;
        var height = finalSize.Height;
        Display?.Arrange(new Rect(0, 0, multiline ? finalSize.Width : _natural.Width, Math.Max(height, _natural.Height)));
        Editor?.Arrange(new Rect(0, 0, finalSize.Width, multiline ? Math.Max(height, _natural.Height) : height));
        var frameWidth = multiline ? finalSize.Width : Math.Max(0, FrameWidth);
        Frame?.Arrange(new Rect(0, 0, frameWidth, height));
        Ring?.Arrange(new Rect(-4, -4, frameWidth + 8, height + 8));
        if (Slot is { } slot)
        {
            var size = slot.DesiredSize;
            var top = multiline ? 0 : (height - size.Height) / 2;
            slot.Arrange(new Rect(frameWidth + (multiline ? 8 : 6), top, size.Width, size.Height));
        }
        return finalSize;
    }

    private MotionValue<double> Create(double initial, bool layout)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            if (_measuring)
            {
                return;
            }
            if (layout)
            {
                InvalidateMeasure();
            }
            else
            {
                InvalidateArrange();
            }
        };
        return value;
    }
}

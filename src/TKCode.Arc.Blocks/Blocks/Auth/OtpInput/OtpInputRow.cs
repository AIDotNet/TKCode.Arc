// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli): the .inputs row.
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays the slots of an <see cref="OtpInput"/> out in one row: each <see cref="SlotWidth"/> wide and
/// <see cref="Gap"/> apart, and when the field is offered less room than that, every slot shrinks by the same amount
/// (Arc's <c>flex: 0 1 42px; min-width: 0</c> inside a <c>max-width: 100%</c> row). The gaps never shrink.
/// </summary>
internal sealed class OtpInputRow : Panel
{
    public static readonly StyledProperty<double> SlotWidthProperty =
        AvaloniaProperty.Register<OtpInputRow, double>(nameof(SlotWidth), 42);

    public static readonly StyledProperty<double> SlotHeightProperty =
        AvaloniaProperty.Register<OtpInputRow, double>(nameof(SlotHeight), 48);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<OtpInputRow, double>(nameof(Gap), 8);

    static OtpInputRow()
    {
        AffectsMeasure<OtpInputRow>(SlotWidthProperty, SlotHeightProperty, GapProperty);
    }

    /// <summary>A slot's width when there is room.</summary>
    public double SlotWidth { get => GetValue(SlotWidthProperty); set => SetValue(SlotWidthProperty, value); }

    /// <summary>A slot's height.</summary>
    public double SlotHeight { get => GetValue(SlotHeightProperty); set => SetValue(SlotHeightProperty, value); }

    /// <summary>Space between slots.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Raised after every arrange (Arc's ResizeObserver on the row keeps the ring on the focused slot).</summary>
    public event EventHandler? Arranged;

    /// <summary>The width each slot gets for <paramref name="available"/> room.</summary>
    public double SlotWidthFor(double available)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return 0;
        }
        var gaps = Gap * (count - 1);
        var natural = SlotWidth * count + gaps;
        if (double.IsInfinity(available) || available >= natural)
        {
            return SlotWidth;
        }
        return Math.Max(0, (available - gaps) / count);
    }

    /// <summary>The slot's box in row coordinates.</summary>
    public Rect SlotRect(int index)
    {
        if (index < 0 || index >= Children.Count)
        {
            return default;
        }
        return Children[index].Bounds;
    }

    /// <summary>The row is the labelled group of slots (Arc's <c>role="group"</c> with <c>aria-label</c>).</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new OtpInputRowPeer(this);

    private sealed class OtpInputRowPeer : ControlAutomationPeer
    {
        public OtpInputRowPeer(OtpInputRow owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        var slot = SlotWidthFor(availableSize.Width);
        var height = SlotHeight;
        foreach (var child in Children)
        {
            child.Measure(new Size(slot, height));
        }
        return count == 0 ? new Size(0, height) : new Size(slot * count + Gap * (count - 1), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        var slot = SlotWidthFor(finalSize.Width);
        var height = SlotHeight;
        for (var i = 0; i < count; i++)
        {
            Children[i].Arrange(new Rect(i * (slot + Gap), 0, slot, height));
        }
        Arranged?.Invoke(this, EventArgs.Empty);
        return count == 0 ? new Size(0, height) : new Size(slot * count + Gap * (count - 1), height);
    }
}

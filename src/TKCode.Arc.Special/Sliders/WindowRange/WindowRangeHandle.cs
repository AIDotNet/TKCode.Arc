using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// One of the three focus stops of a <see cref="WindowRange"/> (start edge, window, end edge), each a slider for assistive
/// technology. It draws nothing: the plot draws the frame, the edges and the focus rings, and takes the pointer.
/// </summary>
internal sealed class WindowRangeHandle : Control
{
    private bool _showRing;

    static WindowRangeHandle()
    {
        FocusableProperty.OverrideDefaultValue<WindowRangeHandle>(true);
        IsHitTestVisibleProperty.OverrideDefaultValue<WindowRangeHandle>(false);
    }

    public WindowRangeHandle(WindowRange owner, WindowRangePart part)
    {
        Owner = owner;
        Part = part;
    }

    public WindowRange Owner { get; }

    public WindowRangePart Part { get; }

    /// <summary>Shows the ring after a key press even when the focus came from the pointer.</summary>
    public bool ShowRing
    {
        get => _showRing;
        set
        {
            if (_showRing != value)
            {
                _showRing = value;
                (Parent as Visual)?.InvalidateVisual();
            }
        }
    }

    /// <summary>Whether the plot draws this handle's focus ring now.</summary>
    public bool IsRingVisible => IsFocused && (_showRing || Classes.Contains(":focus-visible"));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty)
        {
            if (!IsFocused)
            {
                _showRing = false;
            }
            (Parent as Visual)?.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new WindowRangeHandleAutomationPeer(this);
}

/// <summary>Each handle is a Slider control with the range value pattern (Arc: role="slider" with valuemin/max/now).</summary>
internal sealed class WindowRangeHandleAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public WindowRangeHandleAutomationPeer(WindowRangeHandle owner) : base(owner)
    {
    }

    private WindowRangeHandle Handle => (WindowRangeHandle)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsReadOnly => !Handle.Owner.IsEffectivelyEnabled;

    public double Minimum => Handle.Owner.MinimumOf(Handle.Part);

    public double Maximum => Handle.Owner.MaximumOf(Handle.Part);

    public double Value => Handle.Owner.ValueOf(Handle.Part);

    public double LargeChange => Handle.Owner.EffectiveLargeStep;

    public double SmallChange => Handle.Owner.EffectiveStep;

    public void SetValue(double value) => Handle.Owner.SetPartValue(Handle.Part, value, fromKeyboard: false);
}

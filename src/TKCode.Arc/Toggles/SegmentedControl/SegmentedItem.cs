using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>One segment of a <see cref="SegmentedControl"/>. Segments never scale or change weight; only the pill moves.</summary>
internal sealed class SegmentedItem : TogglesItem
{
    public static readonly StyledProperty<object?> AccessoryProperty =
        AvaloniaProperty.Register<SegmentedItem, object?>(nameof(Accessory));

    public object? Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            (Owner as SegmentedControl)?.RaiseIntent(this);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        (Owner as SegmentedControl)?.RaiseIntent(this);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TogglesPressedAutomationPeer(this);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The chevron part of a <see cref="RibbonSplitButton"/>: a surface over the button that opens the menu instead of running the
/// action. It takes the press, so the button itself does not see it.
/// </summary>
internal sealed class RibbonSplitButtonPart : Border
{
    public event EventHandler<RoutedEventArgs>? Invoked;

    private bool _pressed;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed && e.InitialPressMouseButton == MouseButton.Left)
        {
            _pressed = false;
            e.Handled = true;
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
            {
                Invoked?.Invoke(this, new RoutedEventArgs());
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
    }
}

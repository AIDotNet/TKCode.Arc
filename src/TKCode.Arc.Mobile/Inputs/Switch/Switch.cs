using Avalonia;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile switch: a 51 by 31 toggle for settings that take effect immediately. The thumb glides on a spring, the
/// hit area is at least 44 by 44, and there is no hover state. The track fills with the on color when checked.
/// </summary>
public class Switch : ToggleButton
{
    internal static readonly StyledProperty<Thickness> ThumbMarginProperty =
        AvaloniaProperty.Register<Switch, Thickness>(nameof(ThumbMargin));

    internal Thickness ThumbMargin
    {
        get => GetValue(ThumbMarginProperty);
        set => SetValue(ThumbMarginProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Switch);
}

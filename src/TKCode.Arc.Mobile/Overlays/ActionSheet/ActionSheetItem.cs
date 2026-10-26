using Avalonia;
using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One row of an <see cref="ActionSheet"/>: a centered label in accent color, or in the danger color when
/// <see cref="IsDestructive"/> is set. It is a button: give it a <c>Command</c> or handle <c>Click</c>; a finger on it shades
/// the row. It has no hover state.
/// </summary>
public class ActionSheetItem : AvaloniaButton
{
    /// <summary>Whether the action destroys something (delete, sign out): the label takes the danger color.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<ActionSheetItem, bool>(nameof(IsDestructive));

    static ActionSheetItem()
    {
        IsDestructiveProperty.Changed.AddClassHandler<ActionSheetItem>(
            (item, args) => item.PseudoClasses.Set(":destructive", args.GetNewValue<bool>()));
    }

    /// <inheritdoc cref="IsDestructiveProperty"/>
    public bool IsDestructive
    {
        get => GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ActionSheetItem);
}

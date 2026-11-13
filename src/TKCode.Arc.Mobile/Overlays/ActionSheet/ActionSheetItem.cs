using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One row of an <see cref="ActionSheet"/>: a centered label in accent color, or in the danger color when
/// <see cref="IsDestructive"/> is set, with a check mark at its end when <see cref="IsChecked"/> is set (the current choice
/// of a list of options, as in a <see cref="Picker"/>). It is a button: give it a <c>Command</c> or handle <c>Click</c>; a
/// finger on it shades the row. It has no hover state.
/// </summary>
[PseudoClasses(":destructive", CheckedPseudoClass)]
public class ActionSheetItem : AvaloniaButton
{
    /// <summary>Set while <see cref="IsChecked"/> is true.</summary>
    public const string CheckedPseudoClass = ":checked";

    /// <summary>Whether the action destroys something (delete, sign out): the label takes the danger color.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<ActionSheetItem, bool>(nameof(IsDestructive));

    /// <summary>
    /// Whether the row is the current choice: a check mark shows at the row's end and the label stays centered beside it.
    /// Choosing a row does not set it; the owner of the list does (a <see cref="Picker"/> checks its selected item).
    /// </summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ActionSheetItem, bool>(nameof(IsChecked));

    static ActionSheetItem()
    {
        IsDestructiveProperty.Changed.AddClassHandler<ActionSheetItem>(
            (item, args) => item.PseudoClasses.Set(":destructive", args.GetNewValue<bool>()));
        IsCheckedProperty.Changed.AddClassHandler<ActionSheetItem>(
            (item, args) => item.PseudoClasses.Set(CheckedPseudoClass, args.GetNewValue<bool>()));
    }

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc cref="IsDestructiveProperty"/>
    public bool IsDestructive
    {
        get => GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ActionSheetItem);
}

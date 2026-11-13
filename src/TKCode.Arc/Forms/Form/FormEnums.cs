namespace TKCode.Arc;

/// <summary>Where a <see cref="FormItem"/> puts its label (<see cref="Form.LabelPlacementProperty"/>).</summary>
public enum FormLabelPlacement
{
    /// <summary>The label sits above the editor, as on a standalone Arc field (default).</summary>
    Top,

    /// <summary>
    /// The label sits in a column to the left of the editor. With <see cref="Form.LabelWidthProperty"/> NaN every left
    /// label of the same shared size scope takes the width of the widest one.
    /// </summary>
    Left
}

/// <summary>When a <see cref="FormItem"/> starts to show the errors it has collected (<see cref="Form.ErrorTriggerProperty"/>).</summary>
public enum FormErrorTrigger
{
    /// <summary>Errors show as soon as they arrive, like a standalone Arc field.</summary>
    Change,

    /// <summary>Errors show once focus has left the item, or when the form is submitted or validated (default).</summary>
    Blur,

    /// <summary>Errors show only when the form is submitted or validated.</summary>
    Submit
}

/// <summary>Where a <see cref="FormItem"/> shows its error (<see cref="Form.ErrorPlacementProperty"/>).</summary>
public enum FormErrorPlacement
{
    /// <summary>A row under the editor that opens on the smooth spring (default).</summary>
    Inline,

    /// <summary>A tooltip under the editor: it takes no space, opens on hover and stays open while the item has keyboard focus.</summary>
    Tooltip,

    /// <summary>No text, only the invalid state of the field; the errors stay in <see cref="FormItem.Errors"/> and the help text.</summary>
    None
}

/// <summary>How a <see cref="FormItem"/> marks whether it is required (<see cref="Form.RequiredIndicatorProperty"/>).</summary>
public enum FormRequiredIndicator
{
    /// <summary>Required items get a red asterisk after the label (default).</summary>
    Asterisk,

    /// <summary>Items that are not required get a muted, localized "Optional" after the label.</summary>
    Optional,

    /// <summary>No mark.</summary>
    None
}

/// <summary>What a button does to its nearest <see cref="Form"/> when clicked (<see cref="Form.ActionProperty"/>).</summary>
public enum FormAction
{
    /// <summary>Nothing (default).</summary>
    None,

    /// <summary>Calls <see cref="Form.SubmitAsync"/>.</summary>
    Submit,

    /// <summary>Calls <see cref="Form.ResetFields"/>.</summary>
    Reset
}

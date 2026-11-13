using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// How an Arc field inside a <see cref="FormItem"/> presents its own validation state (the library convention of the Form
/// spec, section 7.5). The item draws the label and the error, so the field hides its own label and error rows and turns
/// invalid only when the item shows its errors.
/// </summary>
internal enum FormFieldState
{
    /// <summary>Not inside a form item: the field shows its own label, error row and invalid state.</summary>
    None,

    /// <summary>Inside an item that does not show its errors yet: only an explicit error makes the field invalid.</summary>
    Hidden,

    /// <summary>The item shows its errors: the field is invalid while it has an error of its own.</summary>
    Shown,

    /// <summary>The item has an explicit error (<see cref="Field.ErrorMessage"/>): every field in it is invalid.</summary>
    Forced
}

/// <summary>Shared plumbing between <see cref="FormItem"/> and the Arc fields it hosts.</summary>
internal static class FormFieldHost
{
    /// <summary>
    /// The state a form item hands down to the fields inside it (inherited, so it reaches the content of the item and
    /// nothing else needs registering). Only fields that are not part of another control's template take part.
    /// </summary>
    public static readonly AttachedProperty<FormFieldState> StateProperty =
        AvaloniaProperty.RegisterAttached<FormItem, Control, FormFieldState>("FormFieldState", inherits: true);

    /// <summary>The field's own help text (description, value, its own errors), kept so the item's part can be added to it.</summary>
    private static readonly AttachedProperty<string?> OwnHelpTextProperty =
        AvaloniaProperty.RegisterAttached<FormItem, Control, string?>("FormOwnHelpText");

    /// <summary>The item's part of the help text: its description and the error it shows.</summary>
    private static readonly AttachedProperty<string?> ItemHelpTextProperty =
        AvaloniaProperty.RegisterAttached<FormItem, Control, string?>("FormItemHelpText");

    /// <summary>The state that applies to <paramref name="field"/>: <see cref="FormFieldState.None"/> inside a control template.</summary>
    public static FormFieldState StateOf(StyledElement field)
        => field.TemplatedParent is null ? field.GetValue(StateProperty) : FormFieldState.None;

    /// <summary>Whether <paramref name="field"/> sits in a form item (and leaves its label and error to it).</summary>
    public static bool IsHosted(StyledElement field) => StateOf(field) != FormFieldState.None;

    /// <summary>
    /// The invalid state of a field that has an explicit error and / or a binding validation error. Outside a form item it
    /// is the field's usual rule (either error); inside one it follows whether the item shows its errors.
    /// </summary>
    public static bool IsInvalid(FormFieldState state, bool explicitError, bool validationError) => state switch
    {
        FormFieldState.None => explicitError || validationError,
        FormFieldState.Hidden => explicitError,
        FormFieldState.Shown => explicitError || validationError,
        _ => true
    };

    /// <summary>
    /// Writes the help text a field computes for itself. Inside a form item the item's description and shown error come
    /// first; outside one this is <see cref="AutomationProperties.SetHelpText"/>.
    /// </summary>
    public static void SetHelpText(Control field, string? text)
    {
        field.SetValue(OwnHelpTextProperty, text);
        Apply(field);
    }

    /// <summary>Writes the item's part of <paramref name="target"/>'s help text (null removes it).</summary>
    public static void SetItemHelpText(Control target, string? text)
    {
        target.SetValue(ItemHelpTextProperty, text);
        Apply(target);
    }

    private static void Apply(Control control)
    {
        var item = control.GetValue(ItemHelpTextProperty);
        var own = control.GetValue(OwnHelpTextProperty);
        if (string.IsNullOrEmpty(item))
        {
            if (!control.IsSet(OwnHelpTextProperty))
            {
                control.ClearValue(AutomationProperties.HelpTextProperty);
                return;
            }
            AutomationProperties.SetHelpText(control, own ?? string.Empty);
            return;
        }
        AutomationProperties.SetHelpText(control, string.IsNullOrEmpty(own) ? item : $"{item} {own}");
    }

    /// <summary>The text of a validation error: an exception's message, a <see cref="DataValidationException"/>'s data, otherwise <c>ToString()</c>.</summary>
    public static string? TextOf(object? error) => error switch
    {
        null => null,
        AggregateException aggregate when aggregate.InnerExceptions.Count > 0 => TextOf(aggregate.InnerExceptions[0]),
        DataValidationException validation when validation.ErrorData is not null => TextOf(validation.ErrorData),
        Exception exception => exception.Message,
        _ => error.ToString()
    };

    /// <summary>
    /// The error a field shows itself: its explicit error, else the binding's validation message; null inside a form item,
    /// which shows the error instead.
    /// </summary>
    public static string? EffectiveError(StyledElement field, string? explicitError, string? validationMessage)
        => IsHosted(field) ? null : !string.IsNullOrEmpty(explicitError) ? explicitError : validationMessage;

    /// <summary>The validation message of a binding report, or null when it carries no error.</summary>
    public static string? ValidationMessage(BindingValueType state, Exception? error)
        => HasError(state, error) ? TextOf(error) : null;

    /// <summary>Whether a binding state carries an error (data validation or binding error).</summary>
    public static bool HasError(BindingValueType state, Exception? error)
        => error is not null && (state.HasFlag(BindingValueType.DataValidationError) || state.HasFlag(BindingValueType.BindingError));
}

/// <summary>
/// Turns on Avalonia data validation for the value properties of the Arc selection controls, so a view model's error on
/// <c>SelectedValue</c>, <c>SelectedIndex</c> (or <c>SelectedItems</c>) reaches the control and its form item, as on
/// <c>SelectedItem</c> already. Called from each control's static constructor.
/// </summary>
internal static class SelectionDataValidation
{
    public static void Enable<T>() where T : SelectingItemsControl
    {
        SelectingItemsControl.SelectedValueProperty.OverrideMetadata<T>(new StyledPropertyMetadata<object?>(enableDataValidation: true));
        // The unset value must stay -1 (nothing selected), not default(int).
        SelectingItemsControl.SelectedIndexProperty.OverrideMetadata<T>(new DirectPropertyMetadata<int>(-1, enableDataValidation: true));
    }
}

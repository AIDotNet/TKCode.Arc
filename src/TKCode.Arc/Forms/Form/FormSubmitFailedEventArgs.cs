using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Form.SubmitFailed"/>: what blocked the submit and whether the form should move focus.</summary>
public sealed class FormSubmitFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FormSubmitFailedEventArgs(RoutedEvent routedEvent, IReadOnlyList<FormItem> invalidItems, IReadOnlyList<object> formErrors)
        : base(routedEvent)
    {
        InvalidItems = invalidItems;
        FormErrors = formErrors;
    }

    /// <summary>The mounted items that have errors, in document order.</summary>
    public IReadOnlyList<FormItem> InvalidItems { get; }

    /// <summary>
    /// The first item of <see cref="InvalidItems"/>, or null when no mounted item has an error (the errors come from
    /// <see cref="Form.ErrorSource"/>, for example a field on a tab that is not selected).
    /// </summary>
    public FormItem? FirstInvalidItem => InvalidItems.Count > 0 ? InvalidItems[0] : null;

    /// <summary>The entity-level errors of <see cref="Form.ErrorSource"/> (<see cref="Form.FormErrors"/>).</summary>
    public IReadOnlyList<object> FormErrors { get; }

    /// <summary>
    /// Set to true to stop the form from scrolling to and focusing <see cref="FirstInvalidItem"/>, for example to switch
    /// to the tab that holds it first.
    /// </summary>
    public bool SuppressFocus { get; set; }
}

namespace TKCode.Arc;

/// <summary>Type of an app-wide toast: picks the icon, its color and the spoken prefix.</summary>
public enum ToastKind
{
    /// <summary>CircleCheck in success green ("Success:").</summary>
    Success,

    /// <summary>Info in the accent color ("Info:").</summary>
    Info,

    /// <summary>TriangleAlert in warning amber ("Warning:"); stays 1.6 times longer.</summary>
    Warning,

    /// <summary>CircleX in danger red ("Error:"); stays 1.6 times longer and is announced assertively.</summary>
    Error,

    /// <summary>A turning ring ("In progress:"); waits for an update instead of timing out.</summary>
    Loading
}

/// <summary>Where a <see cref="ToastStack"/> hangs its toasts.</summary>
public enum ToastStackPosition
{
    BottomRight,
    BottomCenter,
    BottomLeft
}

/// <summary>
/// A button on a toast (View, Undo, Retry). Running it closes the toast afterwards unless the callback updates the
/// toast, so an Undo can morph the toast into its result.
/// </summary>
public sealed class ToastAction
{
    /// <summary>Creates an action with a label and a callback that receives the toast id.</summary>
    public ToastAction(string label, Action<string> invoke)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    /// <summary>Creates an action whose callback does not need the toast id.</summary>
    public ToastAction(string label, Action invoke)
        : this(label, _ => invoke())
    {
    }

    /// <summary>The button label.</summary>
    public string Label { get; }

    /// <summary>Runs when the button is pressed; receives the toast id.</summary>
    public Action<string> Invoke { get; }
}

/// <summary>What to show in a new toast (<see cref="IToastService.Show"/>).</summary>
public sealed class ToastOptions
{
    /// <summary>Reuse an id to update a toast in place instead of stacking a new one.</summary>
    public string? Id { get; init; }

    /// <summary>Type (default <see cref="ToastKind.Info"/>).</summary>
    public ToastKind Kind { get; init; } = ToastKind.Info;

    /// <summary>The message.</summary>
    public required string Title { get; init; }

    /// <summary>Optional second line.</summary>
    public string? Description { get; init; }

    /// <summary>Optional action button.</summary>
    public ToastAction? Action { get; init; }

    /// <summary>
    /// Time before the toast closes itself. Null uses the stack's default for the type (loading toasts wait for an
    /// update); <see cref="Timeout.InfiniteTimeSpan"/> keeps it until closed.
    /// </summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>A change to a shown toast (<see cref="IToastService.Update"/>). Null members stay as they are.</summary>
public sealed class ToastUpdate
{
    /// <summary>New type (the icon morphs).</summary>
    public ToastKind? Kind { get; init; }

    /// <summary>New message.</summary>
    public string? Title { get; init; }

    /// <summary>New second line. Use <see cref="RemoveDescription"/> to remove it.</summary>
    public string? Description { get; init; }

    /// <summary>A new action. Use <see cref="RemoveAction"/> to take the action away.</summary>
    public ToastAction? Action { get; init; }

    /// <summary>Folds the action button away (its width springs closed).</summary>
    public bool RemoveAction { get; init; }

    /// <summary>Removes the description line.</summary>
    public bool RemoveDescription { get; init; }

    /// <summary>New lifetime; null restarts with the default for the (new) type.</summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>Shows, updates and dismisses app-wide toasts (Arc's <c>useToastStack</c>).</summary>
public interface IToastService
{
    /// <summary>Shows a toast and returns its id (an existing id updates that toast instead).</summary>
    string Show(ToastOptions options);

    /// <summary>Morphs a toast in place: icon, copy, height and action animate to the new content and its timer restarts.</summary>
    void Update(string id, ToastUpdate update);

    /// <summary>Dismisses one toast, or every toast when <paramref name="id"/> is null.</summary>
    void Dismiss(string? id = null);

    /// <summary>How many toasts are queued (shown or waiting behind).</summary>
    int Count { get; }
}

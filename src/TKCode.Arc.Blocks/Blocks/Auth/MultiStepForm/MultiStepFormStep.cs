using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One step of a <see cref="MultiStepForm"/>: a title (the step's heading and its label in the progress list), an optional
/// line of description and the step's fields as <see cref="Content"/>. The content stays alive while other steps are shown,
/// so whatever a person typed is still there when they come back.
/// </summary>
public class MultiStepFormStep : AvaloniaObject
{
    /// <summary>Defines the <see cref="Id"/> property.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<MultiStepFormStep, string?>(nameof(Id));

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MultiStepFormStep, string>(nameof(Title), string.Empty);

    /// <summary>Defines the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MultiStepFormStep, string?>(nameof(Description));

    /// <summary>Defines the <see cref="Content"/> property.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<MultiStepFormStep, object?>(nameof(Content));

    /// <summary>Defines the <see cref="ContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<MultiStepFormStep, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>A stable key for the step (your own name for it, such as "account"); the form does not require it.</summary>
    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>The step's heading above its fields; also its label in the progress list.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>One optional line under the heading; empty or null shows none.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>The step's fields: a control, or data shown through <see cref="ContentTemplate"/>.</summary>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Builds the step's fields when <see cref="Content"/> is data rather than a control.</summary>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Optional check that runs when the step is submitted (Continue, Finish or Enter in a text field). Return false to stay
    /// on the step, for example after setting an error message on a field. While the task runs the submit button shows a
    /// spinner; the token is cancelled when the form changes step (Back included), restarts or leaves the window.
    /// </summary>
    public Func<CancellationToken, Task<bool>>? ValidateAsync { get; set; }
}

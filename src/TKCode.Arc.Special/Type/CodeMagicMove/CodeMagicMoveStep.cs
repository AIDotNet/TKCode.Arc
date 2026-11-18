using Avalonia;

namespace TKCode.Arc;

/// <summary>One version of the snippet in a <see cref="CodeMagicMove"/>, with the caption shown under the code.</summary>
public class CodeMagicMoveStep : AvaloniaObject
{
    /// <summary>The code of this version. Tabs become two spaces; leading blank lines and the indentation all lines share are removed.</summary>
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<CodeMagicMoveStep, string>(nameof(Code), string.Empty);

    /// <summary>The step's title, shown under the code and in the step dot's name.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CodeMagicMoveStep, string>(nameof(Title), string.Empty);

    /// <summary>An optional sentence under the title.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<CodeMagicMoveStep, string?>(nameof(Description));

    /// <summary>The file name in the header for this step; null uses <see cref="CodeMagicMove.FileName"/>.</summary>
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<CodeMagicMoveStep, string?>(nameof(FileName));

    /// <summary>Creates an empty step.</summary>
    public CodeMagicMoveStep()
    {
    }

    /// <summary>Creates a step with its code and caption.</summary>
    public CodeMagicMoveStep(string title, string code, string? description = null)
    {
        Title = title;
        Code = code;
        Description = description;
    }

    /// <inheritdoc cref="CodeProperty"/>
    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="FileNameProperty"/>
    public string? FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    /// <summary>Raised when any property changes (the walkthrough rebuilds its versions).</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

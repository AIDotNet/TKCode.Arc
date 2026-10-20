using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// Base class of Arc text inputs (Input, Textarea, PasswordField, SearchField, and text-based special inputs).
/// It keeps everything Avalonia's <see cref="AvaloniaTextBox"/> does (caret, selection, IME, undo, clipboard,
/// two-way <c>Text</c>) and adds the field structure on the control itself: <see cref="Label"/>,
/// <see cref="Description"/>, <see cref="ErrorMessage"/>, <see cref="Placeholder"/>, <see cref="Size"/> and
/// <see cref="Prefix"/> / <see cref="Suffix"/> slots. A non-empty error, or a data-validation error from the
/// <c>Text</c> binding, makes the field invalid; an explicit <see cref="ErrorMessage"/> wins over validation text.
/// </summary>
/// <remarks>
/// A derived control's theme composes the shared parts: <see cref="Field"/> (label and message rows) around a
/// <see cref="FieldFrame"/> named <c>PART_Frame</c> that hosts <c>PART_ScrollViewer</c> and
/// <c>PART_TextPresenter</c>. Bind the frame's <c>IsFocusActive</c> to <c>IsFocused</c>, <c>IsInvalid</c>,
/// <c>IsReadOnly</c>, <c>Size</c>, <c>Prefix</c> and <c>Suffix</c>, and the field's <c>ErrorMessage</c> to
/// <see cref="EffectiveErrorMessage"/>.
/// </remarks>
[TemplatePart(FramePart, typeof(FieldFrame))]
[PseudoClasses(InvalidPseudoClass)]
public abstract class FieldTextBox : AvaloniaTextBox
{
    public const string FramePart = "PART_Frame";

    /// <summary>Set while <see cref="IsInvalid"/> is true.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>The visible label above the frame; also the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        Field.LabelProperty.AddOwner<FieldTextBox>();

    /// <summary>Helper copy under the frame (rewrites changed words in place; counts roll).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        Field.DescriptionProperty.AddOwner<FieldTextBox>();

    /// <summary>Error copy under the frame; non-empty makes the field invalid.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        Field.ErrorMessageProperty.AddOwner<FieldTextBox>();

    /// <summary>Invalid state; always true while an error is shown.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<FieldTextBox, bool>(nameof(IsInvalid), coerce: CoerceIsInvalid);

    /// <summary>When false the label is hidden visually but still names the field.</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        Field.IsLabelVisibleProperty.AddOwner<FieldTextBox>();

    /// <summary>Frame height: Small 36, Medium 44 (default), Large 50.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        Field.SizeProperty.AddOwner<FieldTextBox>();

    /// <summary>Content before the text (icon, unit, currency).</summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        FieldFrame.PrefixProperty.AddOwner<FieldTextBox>();

    /// <summary>Content after the text (unit, action button).</summary>
    public static readonly StyledProperty<object?> SuffixProperty =
        FieldFrame.SuffixProperty.AddOwner<FieldTextBox>();

    /// <summary>Hint shown while the field is empty (same as <see cref="AvaloniaTextBox.PlaceholderText"/>).</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<FieldTextBox, string?>(nameof(Placeholder));

    /// <summary>The error actually shown: <see cref="ErrorMessage"/>, else the binding's validation message.</summary>
    public static readonly DirectProperty<FieldTextBox, string?> EffectiveErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<FieldTextBox, string?>(nameof(EffectiveErrorMessage), o => o.EffectiveErrorMessage);

    /// <summary>The visible label above the frame.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the frame.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the frame.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>Whether the label is shown.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Frame height.</summary>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>Content before the text.</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>Content after the text.</summary>
    public object? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>Hint shown while the field is empty.</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    private string? _effectiveError;

    /// <summary>The error actually shown.</summary>
    public string? EffectiveErrorMessage
    {
        get => _effectiveError;
        private set => SetAndRaise(EffectiveErrorMessageProperty, ref _effectiveError, value);
    }

    /// <summary>The frame part of the applied template.</summary>
    protected FieldFrame? Frame { get; private set; }

    private string? _validationMessage;
    private string? _appliedName;

    private static bool CoerceIsInvalid(AvaloniaObject target, bool value)
        => value || !string.IsNullOrEmpty(((FieldTextBox)target).EffectiveErrorMessage);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Frame = e.NameScope.Find<FieldFrame>(FramePart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ErrorMessageProperty)
        {
            UpdateEffectiveError();
        }
        else if (change.Property == EffectiveErrorMessageProperty)
        {
            CoerceValue(IsInvalidProperty);
            UpdateAutomation();
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == PlaceholderProperty)
        {
            SetCurrentValue(PlaceholderTextProperty, Placeholder);
        }
    }

    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        if (property == TextProperty)
        {
            _validationMessage = state.HasFlag(BindingValueType.DataValidationError) || state.HasFlag(BindingValueType.BindingError)
                ? ValidationText(error)
                : null;
            UpdateEffectiveError();
        }
    }

    private static string? ValidationText(Exception? error) => error switch
    {
        null => null,
        AggregateException aggregate when aggregate.InnerExceptions.Count > 0 => ValidationText(aggregate.InnerExceptions[0]),
        DataValidationException validation when validation.ErrorData is not null => validation.ErrorData.ToString(),
        _ => error.Message
    };

    private void UpdateEffectiveError()
        => EffectiveErrorMessage = !string.IsNullOrEmpty(ErrorMessage) ? ErrorMessage : _validationMessage;

    /// <summary>Names the editor after its label and describes it with the description and error.</summary>
    protected virtual void UpdateAutomation()
    {
        var name = AutomationProperties.GetName(this);
        if (string.IsNullOrEmpty(name) || name == _appliedName)
        {
            _appliedName = Label;
            AutomationProperties.SetName(this, Label ?? string.Empty);
        }
        AutomationProperties.SetHelpText(this, FieldText.HelpText(Description, EffectiveErrorMessage));
    }
}

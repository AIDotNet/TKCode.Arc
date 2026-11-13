using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Arc field layout around any editor: a visible <see cref="Label"/> on top, the editor, then a helper
/// <see cref="Description"/> and an <see cref="ErrorMessage"/> row that open on the smooth spring and rewrite only
/// the words that change (<see cref="FieldMessage"/>). A non-empty error makes the field invalid. Use it to give a
/// composite or third-party editor the Arc field structure; Arc's text inputs already include it, so
/// <c>&lt;arc:Input Label="Email"/&gt;</c> needs no wrapper.
/// </summary>
/// <remarks>
/// Standalone, the field names its content for assistive technology (<c>LabeledBy</c> the label, <c>HelpText</c>
/// from the description and error) and clicking the label focuses the first focusable element of the content.
/// </remarks>
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(FieldMessage))]
[TemplatePart(ErrorPart, typeof(FieldMessage))]
[PseudoClasses(InvalidPseudoClass, SmallPseudoClass, LargePseudoClass)]
public class Field : ContentControl
{
    public const string LabelPart = "PART_Label";
    public const string DescriptionPart = "PART_Description";
    public const string ErrorPart = "PART_Error";

    /// <summary>Set while <see cref="IsInvalid"/> is true.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary><see cref="Size"/> Small or Large as a pseudo-class (Medium has none); the theme selects on these, not on the property.</summary>
    public const string SmallPseudoClass = ":small";

    /// <inheritdoc cref="SmallPseudoClass"/>
    public const string LargePseudoClass = ":large";

    /// <summary>The visible label (Arc: required). Also the accessible name of the content.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Field, string?>(nameof(Label));

    /// <summary>Helper copy under the editor.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Field, string?>(nameof(Description));

    /// <summary>Error copy under the editor; non-empty makes the field invalid.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<Field, string?>(nameof(ErrorMessage));

    /// <summary>Invalid state. Always true while <see cref="ErrorMessage"/> is not empty.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<Field, bool>(nameof(IsInvalid), coerce: CoerceIsInvalid);

    /// <summary>When false the label is hidden visually but still names the content (Arc's <c>hideLabel</c>).</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        AvaloniaProperty.Register<Field, bool>(nameof(IsLabelVisible), true);

    /// <summary>Field size; Small uses the 12px label of compact fields.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<Field, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>How the message rows move between texts: changed words (default) or the whole line (PasswordStrength).</summary>
    public static readonly StyledProperty<FieldMessageSwap> MessageSwapProperty =
        AvaloniaProperty.Register<Field, FieldMessageSwap>(nameof(MessageSwap));

    /// <summary>How the message rows move between texts.</summary>
    public FieldMessageSwap MessageSwap { get => GetValue(MessageSwapProperty); set => SetValue(MessageSwapProperty, value); }

    /// <summary>The visible label.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the editor.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the editor.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>Whether the label is shown.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Field size.</summary>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private TextBlock? _label;

    private static bool CoerceIsInvalid(AvaloniaObject target, bool value)
        => value || !string.IsNullOrEmpty(((Field)target).ErrorMessage);

    public Field()
    {
        UpdateSizePseudoClasses();
    }

    private void UpdateSizePseudoClasses()
    {
        var size = Size;
        PseudoClasses.Set(SmallPseudoClass, size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, size == ControlSize.Large);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_label is not null)
        {
            _label.PointerPressed -= OnLabelPressed;
        }
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        if (_label is not null)
        {
            _label.PointerPressed += OnLabelPressed;
        }
        UpdateContentAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ErrorMessageProperty)
        {
            CoerceValue(IsInvalidProperty);
            UpdateContentAutomation();
        }
        else if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty || change.Property == ContentProperty)
        {
            UpdateContentAutomation();
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
        }
    }

    private void OnLabelPressed(object? sender, PointerPressedEventArgs e)
    {
        // <label for> semantics: the label focuses its editor. Inside an editor's own template the editor already
        // receives the press, so only a standalone field forwards it (GetLabelTarget returns null there).
        if (GetLabelTarget() is not { } content)
        {
            return;
        }
        var target = content.Focusable ? content : content.GetVisualDescendants().OfType<InputElement>()
                                                          .FirstOrDefault(c => c.Focusable && c.IsEffectivelyEnabled);
        if (target is not null && target.Focus(NavigationMethod.Pointer))
        {
            e.Handled = true;
        }
    }

    /// <summary>The label part of the applied template (<see cref="LabelPart"/>), or null before the template is applied.</summary>
    protected TextBlock? LabelElement => _label;

    /// <summary>
    /// The control the label names and focuses when clicked. The default is the content when the field is not part of
    /// another control's template; a derived field can point it at a specific editor.
    /// </summary>
    protected virtual Control? GetLabelTarget() => TemplatedParent is null ? Content as Control : null;

    /// <summary>The error text that <see cref="UpdateContentAutomation"/> adds to the help text; the default is <see cref="ErrorMessage"/>.</summary>
    protected virtual string? GetAutomationErrorText() => ErrorMessage;

    /// <summary>
    /// Writes the accessible label, name and help text onto <see cref="GetLabelTarget"/>: <c>LabeledBy</c> the label,
    /// the name from <see cref="Label"/> unless the app set one, and the help text from <see cref="Description"/> and
    /// <see cref="GetAutomationErrorText"/>. Called when any of them changes.
    /// </summary>
    protected virtual void UpdateContentAutomation()
    {
        if (GetLabelTarget() is not { } content)
        {
            return;
        }
        if (_label is not null)
        {
            AutomationProperties.SetLabeledBy(content, _label);
        }
        if (string.IsNullOrEmpty(AutomationProperties.GetName(content)) || AutomationProperties.GetName(content) == _appliedName)
        {
            _appliedName = Label;
            AutomationProperties.SetName(content, Label ?? string.Empty);
        }
        AutomationProperties.SetHelpText(content, FieldText.HelpText(Description, GetAutomationErrorText()));
    }

    private string? _appliedName;
}

/// <summary>Shared text helpers for field automation.</summary>
internal static class FieldText
{
    public static string HelpText(string? description, string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return description ?? string.Empty;
        }
        return string.IsNullOrEmpty(description) ? error : $"{description} {error}";
    }
}

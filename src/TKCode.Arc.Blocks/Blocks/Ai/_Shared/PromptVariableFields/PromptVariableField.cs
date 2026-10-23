using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// One variable field of <see cref="PromptVariableFields"/> (a text box): a label row (6 × 6 dot in the series color, the
/// name in the chip text color, 12 / 16.8, 12px in so it lines up with the value) 5px above a 36px frame (radius 18, 1px
/// border, Surface, 0 12 padding) with the value at 14 in Foreground and the placeholder in TextMuted. The border is
/// Border at rest, BorderStrong under a mouse or pen, and <see cref="FocusBorderBrush"/> (the series color mixed into
/// Border) while focused, changing in 160ms; there is no focus ring. Away from focus a value too long for the frame ends
/// in an ellipsis. Clicking the label focuses the field, as a <c>label</c> would.
/// </summary>
/// <remarks>
/// The border color is set in code from the state (focus wins over hover) so each field can carry its own focus color;
/// the theme only declares the transition.
/// </remarks>
[TemplatePart(FramePart, typeof(Border))]
[PseudoClasses(WidePseudoClass, HoverPseudoClass)]
internal sealed class PromptVariableField : AvaloniaTextBox
{
    public const string FramePart = "PART_Frame";
    public const string WidePseudoClass = ":wide";
    public const string HoverPseudoClass = ":hover";

    /// <summary>The variable's name: the label and the accessible name.</summary>
    public static readonly StyledProperty<string?> VariableNameProperty =
        AvaloniaProperty.Register<PromptVariableField, string?>(nameof(VariableName));

    /// <summary>The variable's tone (1 to 4: Series1 to Series4).</summary>
    public static readonly StyledProperty<int> ToneProperty =
        AvaloniaProperty.Register<PromptVariableField, int>(nameof(Tone), 1);

    /// <summary>The dot's color (the series color).</summary>
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<PromptVariableField, IBrush?>(nameof(DotBrush));

    /// <summary>The label's color (the chip text color: the series color mixed into Foreground).</summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<PromptVariableField, IBrush?>(nameof(LabelBrush));

    /// <summary>The frame's border at rest (Border).</summary>
    public static readonly StyledProperty<IBrush?> RestBorderBrushProperty =
        AvaloniaProperty.Register<PromptVariableField, IBrush?>(nameof(RestBorderBrush));

    /// <summary>The frame's border under a mouse or pen (BorderStrong).</summary>
    public static readonly StyledProperty<IBrush?> HoverBorderBrushProperty =
        AvaloniaProperty.Register<PromptVariableField, IBrush?>(nameof(HoverBorderBrush));

    /// <summary>The frame's border while focused (the series color mixed into Border).</summary>
    public static readonly StyledProperty<IBrush?> FocusBorderBrushProperty =
        AvaloniaProperty.Register<PromptVariableField, IBrush?>(nameof(FocusBorderBrush));

    /// <summary>Whether the field takes two columns (<c>:wide</c>).</summary>
    public static readonly StyledProperty<bool> IsWideProperty =
        AvaloniaProperty.Register<PromptVariableField, bool>(nameof(IsWide));

    static PromptVariableField()
    {
        FocusAdornerProperty.OverrideDefaultValue<PromptVariableField>(null);
        AcceptsReturnProperty.OverrideDefaultValue<PromptVariableField>(false);
        TextWrappingProperty.OverrideDefaultValue<PromptVariableField>(TextWrapping.NoWrap);
    }

    protected override Type StyleKeyOverride => typeof(PromptVariableField);

    /// <inheritdoc cref="VariableNameProperty"/>
    public string? VariableName { get => GetValue(VariableNameProperty); set => SetValue(VariableNameProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public int Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="DotBrushProperty"/>
    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    /// <inheritdoc cref="LabelBrushProperty"/>
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }

    /// <inheritdoc cref="RestBorderBrushProperty"/>
    public IBrush? RestBorderBrush { get => GetValue(RestBorderBrushProperty); set => SetValue(RestBorderBrushProperty, value); }

    /// <inheritdoc cref="HoverBorderBrushProperty"/>
    public IBrush? HoverBorderBrush { get => GetValue(HoverBorderBrushProperty); set => SetValue(HoverBorderBrushProperty, value); }

    /// <inheritdoc cref="FocusBorderBrushProperty"/>
    public IBrush? FocusBorderBrush { get => GetValue(FocusBorderBrushProperty); set => SetValue(FocusBorderBrushProperty, value); }

    /// <inheritdoc cref="IsWideProperty"/>
    public bool IsWide { get => GetValue(IsWideProperty); set => SetValue(IsWideProperty, value); }

    /// <summary>Whether a mouse or pen is over the field (touch never leaves a hover look).</summary>
    internal bool IsHovered { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariableNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
        else if (change.Property == IsWideProperty)
        {
            PseudoClasses.Set(WidePseudoClass, change.GetNewValue<bool>());
            // A wide field takes two columns: the grid places its fields again.
            (Parent as PromptVariableFieldsPanel)?.InvalidateMeasure();
        }
        else if (change.Property == RestBorderBrushProperty || change.Property == HoverBorderBrushProperty ||
                 change.Property == FocusBorderBrushProperty || change.Property == IsFocusedProperty)
        {
            UpdateBorder();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        IsHovered = e.Pointer.Type != PointerType.Touch;
        PseudoClasses.Set(HoverPseudoClass, IsHovered);
        UpdateBorder();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        IsHovered = false;
        PseudoClasses.Set(HoverPseudoClass, false);
        UpdateBorder();
    }

    /// <summary>The border for the state: focus wins over hover, hover over rest.</summary>
    private void UpdateBorder()
    {
        var brush = IsFocused ? FocusBorderBrush ?? HoverBorderBrush ?? RestBorderBrush
            : IsHovered ? HoverBorderBrush ?? RestBorderBrush
            : RestBorderBrush;
        SetCurrentValue(BorderBrushProperty, brush);
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The value box of a document extraction field or table cell (spec 3.4): a plain text box without a focus ring whose fill
/// and border tell its state, highest first: invalid (Surface, 60% Danger border), low confidence (6% Warning fill, 50%
/// Warning border), focused (Surface, BorderStrong), linked (Surface, Border), empty (4% Foreground; Border under a mouse or
/// pen) and filled (transparent: the value reads like text). The colors come from the owner's <see cref="Palette"/>; the
/// theme only declares the 160ms transitions. A field is 32 high (radius 9, 14 / 19.6), a cell (<c>:cell</c>) 28 (radius 7,
/// 12 / 16.8); numbers are right-aligned with tabular figures (<c>:numeric</c>). Away from focus a long value ends in an
/// ellipsis.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> is two-way with the text: a change of <c>Value.Text</c> from elsewhere (the model, the owner) shows
/// at once, and the person's typing is written to <c>Value.Text</c> and reported with
/// <see cref="ExtractionFieldRow.ValueEditedEvent"/> (origin User); the owner parses it and sets the status and the error.
/// When <c>Value.Text</c> goes from empty to filled by anything but typing, the box is <c>:landed</c>: the text fades in
/// (160ms standard; 120ms linear with reduced motion; at once when off) while the fill fades to transparent. Getting the
/// focus raises <see cref="ExtractionFieldRow.PathFocusedEvent"/>.
/// </para>
/// <para>
/// The box observes its value only while it is in a window, so a value that outlives the form holds no reference to it.
/// </para>
/// </remarks>
[PseudoClasses(EmptyPseudoClass, LinkedPseudoClass, LowPseudoClass, InvalidPseudoClass, LandedPseudoClass, NumericPseudoClass,
    CellPseudoClass, HoverPseudoClass)]
internal sealed class ExtractionInput : AvaloniaTextBox
{
    public const string EmptyPseudoClass = ":empty";
    public const string LinkedPseudoClass = ":linked";
    public const string LowPseudoClass = ":low";
    public const string InvalidPseudoClass = ":invalid";
    public const string LandedPseudoClass = ":landed";
    public const string NumericPseudoClass = ":numeric";
    public const string CellPseudoClass = ":cell";
    public const string HoverPseudoClass = ":hover";

    /// <summary>The value shown and edited.</summary>
    public static readonly StyledProperty<ExtractionValue?> ValueProperty =
        AvaloniaProperty.Register<ExtractionInput, ExtractionValue?>(nameof(Value));

    /// <summary>Whether the field is linked (its row lit by a hover or the selection): Surface with a Border.</summary>
    public static readonly StyledProperty<bool> IsLinkedProperty =
        AvaloniaProperty.Register<ExtractionInput, bool>(nameof(IsLinked));

    /// <summary>Whether the value is a number: right-aligned with tabular figures.</summary>
    public static readonly StyledProperty<bool> IsNumericProperty =
        AvaloniaProperty.Register<ExtractionInput, bool>(nameof(IsNumeric));

    /// <summary>Whether the box is a table cell (28 high, 12px text).</summary>
    public static readonly StyledProperty<bool> IsCellProperty =
        AvaloniaProperty.Register<ExtractionInput, bool>(nameof(IsCell));

    private ExtractionPalette? _palette;
    private string? _description;
    private ExtractionValue? _observed;
    private int _syncing;
    private bool _hovered;

    static ExtractionInput()
    {
        FocusAdornerProperty.OverrideDefaultValue<ExtractionInput>(null);
        AcceptsReturnProperty.OverrideDefaultValue<ExtractionInput>(false);
        TextWrappingProperty.OverrideDefaultValue<ExtractionInput>(TextWrapping.NoWrap);
    }

    public ExtractionInput()
    {
        // The equivalents of spellcheck="false": no suggestions, no automatic capitals (codes, IBANs, amounts).
        TextInputOptions.SetShowSuggestions(this, false);
        TextInputOptions.SetAutoCapitalization(this, false);
        Text = string.Empty;
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    protected override Type StyleKeyOverride => typeof(ExtractionInput);

    /// <inheritdoc cref="ValueProperty"/>
    public ExtractionValue? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="IsLinkedProperty"/>
    public bool IsLinked { get => GetValue(IsLinkedProperty); set => SetValue(IsLinkedProperty, value); }

    /// <inheritdoc cref="IsNumericProperty"/>
    public bool IsNumeric { get => GetValue(IsNumericProperty); set => SetValue(IsNumericProperty, value); }

    /// <inheritdoc cref="IsCellProperty"/>
    public bool IsCell { get => GetValue(IsCellProperty); set => SetValue(IsCellProperty, value); }

    /// <summary>The colors of the states (set by the owner when the theme changes); null leaves the theme's.</summary>
    public ExtractionPalette? Palette
    {
        get => _palette;
        set
        {
            if (ReferenceEquals(_palette, value))
            {
                return;
            }
            _palette = value;
            UpdateLook();
        }
    }

    /// <summary>The help text the owner wants read with the box when it has no error (the model's note of a low-confidence cell).</summary>
    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            UpdateHelpText();
        }
    }

    /// <summary>Whether a mouse or pen is over the box (touch leaves no hover look).</summary>
    internal bool IsHovered => _hovered;

    /// <summary>The look the box has now (for tests): which state won.</summary>
    internal ExtractionInputLook Look { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (IsAttachedToVisualTree())
            {
                Observe(change.GetNewValue<ExtractionValue?>());
            }
            // A new value shows as it is: no landing.
            PseudoClasses.Set(LandedPseudoClass, false);
            ShowValueText();
            UpdateState();
        }
        else if (change.Property == TextProperty)
        {
            OnTextChanged();
        }
        else if (change.Property == IsLinkedProperty)
        {
            PseudoClasses.Set(LinkedPseudoClass, change.GetNewValue<bool>());
            UpdateLook();
        }
        else if (change.Property == IsNumericProperty)
        {
            var numeric = change.GetNewValue<bool>();
            PseudoClasses.Set(NumericPseudoClass, numeric);
            TextAlignment = numeric ? TextAlignment.End : TextAlignment.Start;
        }
        else if (change.Property == IsCellProperty)
        {
            PseudoClasses.Set(CellPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsFocusedProperty)
        {
            UpdateLook();
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Value);
        ShowValueText();
        UpdateState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        // Back in a window later the value shows as it is (the landing does not replay).
        PseudoClasses.Set(LandedPseudoClass, false);
        _hovered = false;
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void Observe(ExtractionValue? value)
    {
        if (ReferenceEquals(_observed, value))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnValuePropertyChanged;
        }
        _observed = value;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnValuePropertyChanged;
        }
    }

    private void OnValuePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ExtractionValue.TextProperty)
        {
            if (_syncing > 0)
            {
                // Our own write of the person's typing.
                return;
            }
            var wasEmpty = string.IsNullOrEmpty(e.GetOldValue<string?>());
            var text = e.GetNewValue<string?>() ?? string.Empty;
            ShowValueText();
            if (text.Length == 0)
            {
                PseudoClasses.Set(LandedPseudoClass, false);
            }
            else if (wasEmpty)
            {
                Land();
            }
            UpdateState();
        }
        else if (e.Property == ExtractionValue.NeedsCheckProperty || e.Property == ExtractionValue.ErrorProperty ||
                 e.Property == ExtractionValue.NoteProperty)
        {
            UpdateState();
        }
    }

    /// <summary>
    /// The value arrived (empty → filled, not by typing): the text fades in and the fill fades out. Only in a shown box with
    /// motion: the first state of a box, and every state with motion off, shows as it is.
    /// </summary>
    private void Land()
    {
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            return;
        }
        // Off and on again, so a value that empties and fills within one turn replays the fade.
        PseudoClasses.Set(LandedPseudoClass, false);
        PseudoClasses.Set(LandedPseudoClass, true);
    }

    /// <summary>Puts <c>Value.Text</c> in the box without treating it as typing.</summary>
    private void ShowValueText()
    {
        var text = Value?.Text ?? string.Empty;
        if (string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _syncing++;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _syncing--;
        }
    }

    private void OnTextChanged()
    {
        var text = Text ?? string.Empty;
        PseudoClasses.Set(EmptyPseudoClass, text.Length == 0);
        if (_syncing > 0 || Value is not { } value)
        {
            UpdateLook();
            return;
        }
        var old = value.Text ?? string.Empty;
        if (string.Equals(old, text, StringComparison.Ordinal))
        {
            UpdateLook();
            return;
        }
        _syncing++;
        try
        {
            value.Text = text;
        }
        finally
        {
            _syncing--;
        }
        if (text.Length == 0)
        {
            PseudoClasses.Set(LandedPseudoClass, false);
        }
        UpdateState();
        RaiseEvent(new ExtractionValueChangedEventArgs(ExtractionFieldRow.ValueEditedEvent, this, value.Path, old, text, ExtractionOrigin.User));
    }

    /// <summary>Reads the value's review state into the pseudo-classes, the look and the help text.</summary>
    private void UpdateState()
    {
        var value = Value;
        PseudoClasses.Set(EmptyPseudoClass, string.IsNullOrEmpty(Text));
        PseudoClasses.Set(LowPseudoClass, value?.NeedsCheck == true);
        PseudoClasses.Set(InvalidPseudoClass, value?.Error is not null);
        UpdateLook();
        UpdateHelpText();
    }

    private void UpdateHelpText()
    {
        // The error first (aria-invalid + describedby), else the owner's description.
        var error = Value?.Error;
        AutomationProperties.SetHelpText(this, error ?? _description);
    }

    /// <summary>The fill and border of the state that wins (invalid, low, focused, linked, empty, filled).</summary>
    private void UpdateLook()
    {
        var value = Value;
        var look = value?.Error is not null ? ExtractionInputLook.Invalid
            : value?.NeedsCheck == true ? ExtractionInputLook.Low
            : IsFocused ? ExtractionInputLook.Focused
            : IsLinked ? ExtractionInputLook.Linked
            : string.IsNullOrEmpty(Text) ? _hovered ? ExtractionInputLook.EmptyHover : ExtractionInputLook.Empty
            : ExtractionInputLook.Filled;
        Look = look;
        if (_palette is not { } palette)
        {
            return;
        }
        var (fill, border) = look switch
        {
            ExtractionInputLook.Invalid => (palette.Surface, palette.InvalidBorder),
            ExtractionInputLook.Low => (palette.LowFill, palette.LowBorder),
            ExtractionInputLook.Focused => (palette.Surface, palette.BorderStrong),
            ExtractionInputLook.Linked => (palette.Surface, palette.Border),
            ExtractionInputLook.EmptyHover => (palette.EmptyFill, palette.Border),
            ExtractionInputLook.Empty => (palette.EmptyFill, Brushes.Transparent),
            _ => ((IBrush)Brushes.Transparent, (IBrush)Brushes.Transparent)
        };
        // Local values, not SetCurrentValue: a transition starts from the property's base value, which a current value
        // does not change (a second change within one transition would be lost).
        SetValue(BackgroundProperty, fill);
        SetValue(BorderBrushProperty, border);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (Value is { } value)
        {
            RaiseEvent(new ExtractionPathEventArgs(ExtractionFieldRow.PathFocusedEvent, this, value.Path));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = e.Pointer.Type != PointerType.Touch;
        PseudoClasses.Set(HoverPseudoClass, _hovered);
        UpdateLook();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        PseudoClasses.Set(HoverPseudoClass, false);
        UpdateLook();
    }

    /// <summary>Focuses the box with the caret after the text.</summary>
    public bool FocusWithCaretAtEnd(NavigationMethod method)
    {
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled || !Focus(method))
        {
            return false;
        }
        var end = Text?.Length ?? 0;
        SelectionStart = end;
        SelectionEnd = end;
        CaretIndex = end;
        return true;
    }
}

/// <summary>The state an <see cref="ExtractionInput"/> shows (the one that won).</summary>
internal enum ExtractionInputLook
{
    Filled,
    Empty,
    EmptyHover,
    Linked,
    Focused,
    Low,
    Invalid
}

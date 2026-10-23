using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One field of the document extraction form (spec 3.4): the label (12 / 16.8, TextSecondary, ellipsis), the value box
/// (<see cref="ExtractionInput"/>) and the confidence column (<see cref="ConfidenceIndicator"/>) on a grid of 88 | 1fr | 60
/// (76 | 1fr | 54 when <see cref="IsNarrow"/>), 8 apart, in a row of radius 12 with padding 2 4 2 8 (36 high). Under the box
/// an invalid value shows its error (12 / 16.8, Danger) and a low-confidence value with a note opens the model's reason
/// (12 / 16.8, TextSecondary) with the "Looks right" pill on its right (narrow: on the next line, left).
/// </summary>
/// <remarks>
/// <para>
/// The row works on <see cref="Value"/>, created by the owner: the box is two-way with <c>Value.Text</c> (the person's
/// typing is written there and raised as <see cref="ValueEdited"/>; the owner parses it and sets the status and the error),
/// the reason shows while <c>Value.NeedsCheck</c> and <c>Value.Note</c>, the error while <c>Value.Error</c>. The pill
/// changes nothing itself: it raises <see cref="LooksRightRequested"/>. The row is lit (its group color at 8%, the box on
/// Surface with a Border) while <see cref="IsLinked"/>, which the owner sets from <see cref="PathEntered"/> /
/// <see cref="PathExited"/> (the pointer over the row) and the selection.
/// </para>
/// <para>
/// Tab goes to the box, then to the pill. When the reason closes while its pill has the focus (Looks right by keyboard), the
/// focus moves to the box. Clicking the label focuses the box, as a <c>label</c> would.
/// </para>
/// <para>
/// The routed events (<see cref="PathEnteredEvent"/>, <see cref="PathExitedEvent"/>, <see cref="PathFocusedEvent"/>,
/// <see cref="ValueEditedEvent"/>, <see cref="LooksRightRequestedEvent"/>) are shared with <see cref="ExtractionTableGrid"/>:
/// the owner can handle them once on a container of every row and table.
/// </para>
/// </remarks>
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(InputPart, typeof(ExtractionInput))]
[TemplatePart(ConfidencePart, typeof(ConfidenceIndicator))]
[TemplatePart(ErrorPart, typeof(TextBlock))]
[TemplatePart(ReasonPart, typeof(ExtractionReveal))]
[TemplatePart(ReasonTextPart, typeof(TextBlock))]
[TemplatePart(LooksRightPart, typeof(ExtractionPillButton))]
[PseudoClasses(LinkedPseudoClass, NarrowPseudoClass, RunningPseudoClass, InvalidPseudoClass, ReasonPseudoClass)]
internal sealed class ExtractionFieldRow : TemplatedControl
{
    public const string LabelPart = "PART_Label";
    public const string InputPart = "PART_Input";
    public const string ConfidencePart = "PART_Confidence";
    public const string ErrorPart = "PART_Error";
    public const string ReasonPart = "PART_Reason";
    public const string ReasonTextPart = "PART_ReasonText";
    public const string LooksRightPart = "PART_LooksRight";

    public const string LinkedPseudoClass = ":linked";
    public const string NarrowPseudoClass = ":narrow";
    public const string RunningPseudoClass = ":running";
    public const string InvalidPseudoClass = ":invalid";
    public const string ReasonPseudoClass = ":reason";

    #region Properties

    /// <summary>The value of the field (created by the owner).</summary>
    public static readonly StyledProperty<ExtractionValue?> ValueProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, ExtractionValue?>(nameof(Value));

    /// <summary>The label ("Due date").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(Label));

    /// <summary>The group's tone, 1 to 4 (<see cref="PromptTone"/>): the color of the lit row.</summary>
    public static readonly StyledProperty<int> ToneProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, int>(nameof(Tone), 1);

    /// <summary>The field's type (the owner turns it into <see cref="TypeName"/>).</summary>
    public static readonly StyledProperty<ExtractionFieldType> FieldTypeProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, ExtractionFieldType>(nameof(FieldType));

    /// <summary>Whether the row is lit (a hover or the selection, set by the owner).</summary>
    public static readonly StyledProperty<bool> IsLinkedProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, bool>(nameof(IsLinked));

    /// <summary>Whether a run is going (<c>:running</c>).</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, bool>(nameof(IsRunning));

    /// <summary>The narrow layout (76 | 1fr | 54; the pill under the reason).</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, bool>(nameof(IsNarrow));

    /// <summary>Whether Accept all has reached the field (the confidence column shows the check).</summary>
    public static readonly StyledProperty<bool> IsAcceptedProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, bool>(nameof(IsAccepted));

    /// <summary>The confidence column of an edited value ("Edited").</summary>
    public static readonly StyledProperty<string?> EditedTextProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(EditedText));

    /// <summary>The type name shown while the value is pending ("Date").</summary>
    public static readonly StyledProperty<string?> TypeNameProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(TypeName));

    /// <summary>The pill's text ("Looks right").</summary>
    public static readonly StyledProperty<string?> LooksRightTextProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(LooksRightText));

    /// <summary>What screen readers hear for the confidence column.</summary>
    public static readonly StyledProperty<string?> ConfidenceAutomationTextProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(ConfidenceAutomationText));

    /// <summary>The accessible name of the value box (the label, or more).</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<ExtractionFieldRow, string?>(nameof(AutomationName));

    /// <summary>The value's error (<c>Value.Error</c>), shown under the box (read-only).</summary>
    public static readonly DirectProperty<ExtractionFieldRow, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionFieldRow, string?>(nameof(ErrorText), o => o.ErrorText);

    /// <summary>The model's reason while it shows (<c>Value.Note</c> of a value that needs a check; read-only).</summary>
    public static readonly DirectProperty<ExtractionFieldRow, string?> ReasonTextProperty =
        AvaloniaProperty.RegisterDirect<ExtractionFieldRow, string?>(nameof(ReasonText), o => o.ReasonText);

    /// <summary>Whether the reason is open (read-only).</summary>
    public static readonly DirectProperty<ExtractionFieldRow, bool> IsReasonOpenProperty =
        AvaloniaProperty.RegisterDirect<ExtractionFieldRow, bool>(nameof(IsReasonOpen), o => o.IsReasonOpen);

    #endregion

    #region Events (shared with ExtractionTableGrid)

    /// <summary>Raised when a pointer (mouse or pen) enters the row; the path is the value's.</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathEnteredEvent =
        RoutedEvent.Register<ExtractionFieldRow, ExtractionPathEventArgs>(nameof(PathEntered), RoutingStrategies.Bubble);

    /// <summary>Raised when the pointer leaves the row.</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathExitedEvent =
        RoutedEvent.Register<ExtractionFieldRow, ExtractionPathEventArgs>(nameof(PathExited), RoutingStrategies.Bubble);

    /// <summary>Raised when the value box gets the focus (keyboard or pointer).</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> PathFocusedEvent =
        RoutedEvent.Register<ExtractionFieldRow, ExtractionPathEventArgs>(nameof(PathFocused), RoutingStrategies.Bubble);

    /// <summary>Raised after every edit the person makes, once <c>Value.Text</c> holds it (origin User).</summary>
    public static readonly RoutedEvent<ExtractionValueChangedEventArgs> ValueEditedEvent =
        RoutedEvent.Register<ExtractionFieldRow, ExtractionValueChangedEventArgs>(nameof(ValueEdited), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses "Looks right" (nothing else changes; the owner marks the value checked).</summary>
    public static readonly RoutedEvent<ExtractionPathEventArgs> LooksRightRequestedEvent =
        RoutedEvent.Register<ExtractionFieldRow, ExtractionPathEventArgs>(nameof(LooksRightRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a pointer enters the row.</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathEntered { add => AddHandler(PathEnteredEvent, value); remove => RemoveHandler(PathEnteredEvent, value); }

    /// <summary>Raised when the pointer leaves the row.</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathExited { add => AddHandler(PathExitedEvent, value); remove => RemoveHandler(PathExitedEvent, value); }

    /// <summary>Raised when the value box gets the focus.</summary>
    public event EventHandler<ExtractionPathEventArgs>? PathFocused { add => AddHandler(PathFocusedEvent, value); remove => RemoveHandler(PathFocusedEvent, value); }

    /// <summary>Raised after every edit the person makes.</summary>
    public event EventHandler<ExtractionValueChangedEventArgs>? ValueEdited { add => AddHandler(ValueEditedEvent, value); remove => RemoveHandler(ValueEditedEvent, value); }

    /// <summary>Raised when the person presses "Looks right".</summary>
    public event EventHandler<ExtractionPathEventArgs>? LooksRightRequested { add => AddHandler(LooksRightRequestedEvent, value); remove => RemoveHandler(LooksRightRequestedEvent, value); }

    #endregion

    private ExtractionPalette? _palette;
    private TextBlock? _label;
    private ExtractionInput? _input;
    private ExtractionReveal? _reason;
    private ExtractionPillButton? _looksRight;
    private ExtractionValue? _observed;
    private string? _errorText;
    private string? _reasonText;
    private bool _isReasonOpen;
    private bool _pointerInside;

    static ExtractionFieldRow()
    {
        FocusableProperty.OverrideDefaultValue<ExtractionFieldRow>(false);
    }

    public ExtractionFieldRow()
    {
        ExtractionPalette.BindProbes(this);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public ExtractionValue? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public int Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="FieldTypeProperty"/>
    public ExtractionFieldType FieldType { get => GetValue(FieldTypeProperty); set => SetValue(FieldTypeProperty, value); }

    /// <inheritdoc cref="IsLinkedProperty"/>
    public bool IsLinked { get => GetValue(IsLinkedProperty); set => SetValue(IsLinkedProperty, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="IsAcceptedProperty"/>
    public bool IsAccepted { get => GetValue(IsAcceptedProperty); set => SetValue(IsAcceptedProperty, value); }

    /// <inheritdoc cref="EditedTextProperty"/>
    public string? EditedText { get => GetValue(EditedTextProperty); set => SetValue(EditedTextProperty, value); }

    /// <inheritdoc cref="TypeNameProperty"/>
    public string? TypeName { get => GetValue(TypeNameProperty); set => SetValue(TypeNameProperty, value); }

    /// <inheritdoc cref="LooksRightTextProperty"/>
    public string? LooksRightText { get => GetValue(LooksRightTextProperty); set => SetValue(LooksRightTextProperty, value); }

    /// <inheritdoc cref="ConfidenceAutomationTextProperty"/>
    public string? ConfidenceAutomationText { get => GetValue(ConfidenceAutomationTextProperty); set => SetValue(ConfidenceAutomationTextProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; private set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }

    /// <inheritdoc cref="ReasonTextProperty"/>
    public string? ReasonText { get => _reasonText; private set => SetAndRaise(ReasonTextProperty, ref _reasonText, value); }

    /// <inheritdoc cref="IsReasonOpenProperty"/>
    public bool IsReasonOpen { get => _isReasonOpen; private set => SetAndRaise(IsReasonOpenProperty, ref _isReasonOpen, value); }

    /// <summary>The value box (template part).</summary>
    internal ExtractionInput? Input => _input;

    /// <summary>The reason box (template part).</summary>
    internal ExtractionReveal? Reason => _reason;

    /// <summary>The "Looks right" pill (template part).</summary>
    internal ExtractionPillButton? LooksRightButton => _looksRight;

    /// <summary>The colors the row and its box use now.</summary>
    internal ExtractionPalette? Palette => _palette;

    /// <summary>Focuses the value box with the caret after the text (a span was clicked, Review); false when it cannot.</summary>
    public bool FocusInput(NavigationMethod method = NavigationMethod.Pointer)
    {
        if (_input is null)
        {
            ApplyTemplate();
        }
        return _input?.FocusWithCaretAtEnd(method) == true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_label is not null)
        {
            _label.PointerPressed -= OnLabelPressed;
        }
        if (_looksRight is not null)
        {
            _looksRight.Click -= OnLooksRightClick;
        }
        if (_reason is not null)
        {
            _reason.ContentUnmounted -= OnReasonUnmounted;
        }
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        _input = e.NameScope.Find<ExtractionInput>(InputPart);
        _reason = e.NameScope.Find<ExtractionReveal>(ReasonPart);
        _looksRight = e.NameScope.Find<ExtractionPillButton>(LooksRightPart);
        if (_label is not null)
        {
            _label.PointerPressed += OnLabelPressed;
        }
        if (_looksRight is not null)
        {
            _looksRight.Click += OnLooksRightClick;
        }
        if (_reason is not null)
        {
            _reason.ContentUnmounted += OnReasonUnmounted;
        }
        if (_input is not null)
        {
            _input.Palette = _palette;
            _input.IsLinked = IsLinked;
            AutomationProperties.SetName(_input, AutomationName);
        }
        UpdateFromValue();
        UpdateTint();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Value);
        UpdatePalette();
        UpdateFromValue();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        _pointerInside = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Observe(change.GetNewValue<ExtractionValue?>());
            }
            UpdateFromValue();
        }
        else if (change.Property == IsLinkedProperty)
        {
            var linked = change.GetNewValue<bool>();
            PseudoClasses.Set(LinkedPseudoClass, linked);
            if (_input is not null)
            {
                _input.IsLinked = linked;
            }
            UpdateTint();
        }
        else if (change.Property == ToneProperty)
        {
            UpdateTint();
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == AutomationNameProperty)
        {
            if (_input is not null)
            {
                AutomationProperties.SetName(_input, change.GetNewValue<string?>());
            }
        }
        else if (ExtractionPalette.IsProbe(change.Property))
        {
            UpdatePalette();
        }
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
        if (e.Property == ExtractionValue.ErrorProperty || e.Property == ExtractionValue.NeedsCheckProperty ||
            e.Property == ExtractionValue.NoteProperty)
        {
            UpdateFromValue();
        }
    }

    /// <summary>The error line and the reason follow the value.</summary>
    private void UpdateFromValue()
    {
        var value = Value;
        ErrorText = value?.Error;
        PseudoClasses.Set(InvalidPseudoClass, value?.Error is not null);
        var note = value?.Note;
        var open = value is { NeedsCheck: true } && !string.IsNullOrEmpty(note);
        if (open)
        {
            // The text stays while the box closes, so the reason fades out as it was.
            ReasonText = note;
        }
        IsReasonOpen = open;
        PseudoClasses.Set(ReasonPseudoClass, open);
        if (_input is not null)
        {
            // Read with the box: the error first (the box adds it), else the reason.
            _input.Description = open ? note : null;
        }
        if (_reason is not null && _reason.IsExpanded != open)
        {
            if (!open && _looksRight is { IsFocused: true })
            {
                // The pill is about to leave (Looks right by keyboard): the focus goes back to the box.
                _input?.FocusWithCaretAtEnd(NavigationMethod.Tab);
            }
            _reason.IsExpanded = open;
        }
    }

    private void OnReasonUnmounted(object? sender, EventArgs e)
    {
        if (!IsReasonOpen)
        {
            ReasonText = null;
        }
    }

    private void UpdatePalette()
    {
        var palette = ExtractionPalette.Build(this);
        if (palette is null || ReferenceEquals(palette, _palette))
        {
            return;
        }
        _palette = palette;
        if (_input is not null)
        {
            _input.Palette = palette;
        }
        UpdateTint();
    }

    /// <summary>A lit row takes its group color at 8% (the theme fades it in 160ms).</summary>
    private void UpdateTint()
    {
        if (_palette is null)
        {
            return;
        }
        SetValue(BackgroundProperty, IsLinked ? _palette.RowTint(Tone) : Brushes.Transparent);
    }

    private void OnLooksRightClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Value is { } value)
        {
            RaiseEvent(new ExtractionPathEventArgs(LooksRightRequestedEvent, this, value.Path));
        }
    }

    private void OnLabelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && FocusInput(NavigationMethod.Pointer))
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch || _pointerInside || Value is not { } value)
        {
            return;
        }
        _pointerInside = true;
        RaiseEvent(new ExtractionPathEventArgs(PathEnteredEvent, this, value.Path));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_pointerInside)
        {
            return;
        }
        _pointerInside = false;
        RaiseEvent(new ExtractionPathEventArgs(PathExitedEvent, this, Value?.Path));
    }
}

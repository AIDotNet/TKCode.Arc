// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli): one slot's <input>.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// One slot of an <see cref="OtpInput"/>: a single-character text box whose own text is transparent (the owner's overlay
/// draws the character so it can rise in), so only its caret and selection show. Typing, Backspace, Delete and paste are
/// handed to the owner, which applies Arc's rules to the whole code and writes every slot back; anything else that edits
/// the text (cut, an input method) is reported the same way, like React's controlled <c>onChange</c>.
/// </summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
[PseudoClasses(FilledPseudoClass, InvalidPseudoClass, SuccessPseudoClass, HoverPseudoClass)]
internal sealed class OtpInputSlot : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";
    public const string FilledPseudoClass = ":filled";
    public const string InvalidPseudoClass = ":invalid";
    public const string SuccessPseudoClass = ":success";

    /// <summary>Arc's <c>@media (hover: hover) and (pointer: fine)</c> hover: mouse and pen only, never touch.</summary>
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<OtpInputSlot, bool>(nameof(IsInvalid));

    public static readonly StyledProperty<bool> IsSuccessProperty =
        AvaloniaProperty.Register<OtpInputSlot, bool>(nameof(IsSuccess));

    private bool _syncing;

    public OtpInputSlot(OtpInput owner, int index)
    {
        Owner = owner;
        Index = index;
        AddHandler(PastingFromClipboardEvent, OnPasting);
    }

    /// <summary>The field this slot belongs to.</summary>
    public OtpInput Owner { get; }

    /// <summary>Position in the row, from 0.</summary>
    public int Index { get; set; }

    /// <summary>Danger border and fill (the field has an error).</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>Success border and fill (ours; see <see cref="OtpInput.IsSuccess"/>).</summary>
    public bool IsSuccess { get => GetValue(IsSuccessProperty); set => SetValue(IsSuccessProperty, value); }

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    protected override Type StyleKeyOverride => typeof(OtpInputSlot);

    /// <summary>Writes the owner's character for this slot without reporting it as an edit; the caret goes to the end, as a browser does when a controlled value changes.</summary>
    public void Sync(string character)
    {
        var current = Text ?? string.Empty;
        PseudoClasses.Set(FilledPseudoClass, character.Length > 0);
        if (current == character)
        {
            return;
        }
        _syncing = true;
        try
        {
            SetCurrentValue(TextProperty, character);
            ClearSelection();
            CaretIndex = character.Length;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>True when the slot's character is selected (typing replaces it, Backspace clears it).</summary>
    public bool HasSelection => SelectionStart != SelectionEnd;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        }
        else if (change.Property == IsSuccessProperty)
        {
            PseudoClasses.Set(SuccessPseudoClass, IsSuccess);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
        else if (change.Property == TextProperty && !_syncing)
        {
            // An edit the slot made on its own (cut, an input method, a platform autofill): hand it over as Arc's
            // onChange would receive it. The owner writes the result back after this change has finished.
            Owner.OnSlotEdited(this, change.GetNewValue<string?>() ?? string.Empty);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            base.OnTextInput(e);
            return;
        }
        e.Handled = true;
        Owner.OnSlotTextInput(this, e.Text);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner.OnSlotKeyDown(this, e))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner.OnSlotGotFocus(this);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner.OnSlotLostFocus(this, e.NewFocusedElement);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!IsPointerOver)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    /// <summary>Paste is the owner's: Arc spreads the clipboard text over the slots from this one.</summary>
    internal void OnPasting(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner.OnSlotPaste(this);
    }
}

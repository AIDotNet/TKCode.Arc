using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The in-place editor that replaces a memory row: a text box that grows with the draft, the hint line, and Cancel
/// (ghost) and Save (primary) buttons. Enter or Save submits a valid draft (<see cref="Submitted"/>), Shift+Enter starts a
/// new line, Escape in the box or Cancel gives up (<see cref="Cancelled"/>). The editor validates before it submits: an
/// empty draft or one over <see cref="MaxLength"/> shows <see cref="EmptyErrorText"/> or <see cref="TooLongErrorText"/>
/// instead of the hint, in the danger color, and the owner never sees an invalid draft.
/// <code>
/// &lt;arc:RowEditor Text="{Binding Draft}" MaxLength="200" HintText="Enter to save, Esc to cancel"
///                CancelText="Cancel" SaveText="Save" Submitted="OnSave" Cancelled="OnCancel" /&gt;
/// </code>
/// Past the limit a live count ("212 of 200") replaces the hint while typing, which is never blocked; an error clears as
/// soon as the person changes the draft. <see cref="IsSaving"/> makes the box read-only, turns Save into a spinner and
/// keeps both buttons from acting. The line under the box crossfades between the hint, the count and an error; in a
/// narrow editor it takes its own line above the buttons. Enter and Escape belong to an input method while it composes.
/// Every word comes from the owner.
/// </summary>
[TemplatePart(TextBoxPart, typeof(RowEditorTextBox))]
[TemplatePart(HintPart, typeof(ArcContentSwap))]
[TemplatePart(FooterPart, typeof(RowEditorFooter))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(SavePart, typeof(Button))]
[PseudoClasses(InvalidPseudoClass, OverLimitPseudoClass, SavingPseudoClass)]
internal class RowEditor : TemplatedControl
{
    public const string TextBoxPart = "PART_TextBox";
    public const string HintPart = "PART_Hint";
    public const string FooterPart = "PART_Footer";
    public const string CancelPart = "PART_Cancel";
    public const string SavePart = "PART_Save";

    /// <summary>Set while an error shows.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>Set while the draft is longer than <see cref="MaxLength"/>.</summary>
    public const string OverLimitPseudoClass = ":over-limit";

    /// <summary>Set while <see cref="IsSaving"/>.</summary>
    public const string SavingPseudoClass = ":saving";

    #region Properties

    /// <summary>The draft (two-way).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<RowEditor, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => value ?? string.Empty);

    /// <summary>The longest valid draft, after trimming (default 200); zero or less means no limit. Typing past it is allowed.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<RowEditor, int>(nameof(MaxLength), 200);

    /// <summary>While true the box is read-only, Save shows a spinner, and neither button nor Enter / Escape acts.</summary>
    public static readonly StyledProperty<bool> IsSavingProperty =
        AvaloniaProperty.Register<RowEditor, bool>(nameof(IsSaving));

    /// <summary>The error shown instead of the hint (with a circle-alert glyph, in the danger color); null shows none. Cleared when the person changes the draft.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(ErrorText));

    /// <summary>The hint under the box ("Enter to save, Esc to cancel").</summary>
    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(HintText));

    /// <summary>The label of the cancel button.</summary>
    public static readonly StyledProperty<string?> CancelTextProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(CancelText));

    /// <summary>The label of the save button.</summary>
    public static readonly StyledProperty<string?> SaveTextProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(SaveText));

    /// <summary>The live count past the limit, from the length and the limit ("212 of 200"); null shows "212/200".</summary>
    public static readonly StyledProperty<Func<int, int, string>?> CountTextProperty =
        AvaloniaProperty.Register<RowEditor, Func<int, int, string>?>(nameof(CountText));

    /// <summary>The error for submitting an empty draft.</summary>
    public static readonly StyledProperty<string?> EmptyErrorTextProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(EmptyErrorText));

    /// <summary>The error for submitting a draft over the limit, from the limit.</summary>
    public static readonly StyledProperty<Func<int, string>?> TooLongErrorTextProperty =
        AvaloniaProperty.Register<RowEditor, Func<int, string>?>(nameof(TooLongErrorText));

    /// <summary>The accessible name of the text box ("Edit memory: ...").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<RowEditor, string?>(nameof(AccessibleName));

    /// <summary>The length of the trimmed draft, which the limit and the count use.</summary>
    public static readonly DirectProperty<RowEditor, int> DraftLengthProperty =
        AvaloniaProperty.RegisterDirect<RowEditor, int>(nameof(DraftLength), o => o.DraftLength);

    /// <summary>True while the trimmed draft is longer than <see cref="MaxLength"/>.</summary>
    public static readonly DirectProperty<RowEditor, bool> IsOverLimitProperty =
        AvaloniaProperty.RegisterDirect<RowEditor, bool>(nameof(IsOverLimit), o => o.IsOverLimit);

    /// <summary>Raised when Enter or Save submits a valid draft (trimmed into <see cref="Text"/> first).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SubmittedEvent =
        RoutedEvent.Register<RowEditor, RoutedEventArgs>(nameof(Submitted), RoutingStrategies.Bubble);

    /// <summary>Raised by Cancel, or Escape while the focus is in the box.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<RowEditor, RoutedEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <inheritdoc cref="IsSavingProperty"/>
    public bool IsSaving { get => GetValue(IsSavingProperty); set => SetValue(IsSavingProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="HintTextProperty"/>
    public string? HintText { get => GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <inheritdoc cref="SaveTextProperty"/>
    public string? SaveText { get => GetValue(SaveTextProperty); set => SetValue(SaveTextProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public Func<int, int, string>? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <inheritdoc cref="EmptyErrorTextProperty"/>
    public string? EmptyErrorText { get => GetValue(EmptyErrorTextProperty); set => SetValue(EmptyErrorTextProperty, value); }

    /// <inheritdoc cref="TooLongErrorTextProperty"/>
    public Func<int, string>? TooLongErrorText { get => GetValue(TooLongErrorTextProperty); set => SetValue(TooLongErrorTextProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="DraftLengthProperty"/>
    public int DraftLength { get => _draftLength; private set => SetAndRaise(DraftLengthProperty, ref _draftLength, value); }

    /// <inheritdoc cref="IsOverLimitProperty"/>
    public bool IsOverLimit { get => _isOverLimit; private set => SetAndRaise(IsOverLimitProperty, ref _isOverLimit, value); }

    /// <inheritdoc cref="SubmittedEvent"/>
    public event EventHandler<RoutedEventArgs>? Submitted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    /// <inheritdoc cref="CancelledEvent"/>
    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    #endregion

    private int _draftLength;
    private bool _isOverLimit;
    private RowEditorTextBox? _box;
    private ArcContentSwap? _hintSwap;
    private Button? _cancel;
    private Button? _save;
    private RowEditorHintLine? _hintLine;
    private RowEditorHintKind _hintKind;
    private bool _pushingText;
    private bool _textFromBox;
    private bool _focusPending;
    private bool _focusCaretAtEnd;

    public RowEditor()
    {
        UpdateHint();
    }

    /// <summary>The text box part.</summary>
    internal RowEditorTextBox? TextBox => _box;

    /// <summary>The cancel button part.</summary>
    internal Button? CancelButton => _cancel;

    /// <summary>The save button part.</summary>
    internal Button? SaveButton => _save;

    /// <summary>The crossfade host of the line under the box.</summary>
    internal ArcContentSwap? HintSwap => _hintSwap;

    /// <summary>The line under the box on screen.</summary>
    internal RowEditorHintLine? HintLine => _hintLine;

    /// <summary>Which message the line under the box shows.</summary>
    internal RowEditorHintKind HintKind => _hintKind;

    /// <summary>True while an error shows (Arc's <c>aria-invalid</c> on the box).</summary>
    internal bool IsInvalid => ErrorText is not null;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_box is not null)
        {
            _box.TextChanged -= OnBoxTextChanged;
            _box.RemoveHandler(KeyDownEvent, OnBoxKeyDown);
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        base.OnApplyTemplate(e);
        _box = e.NameScope.Find<RowEditorTextBox>(TextBoxPart);
        _hintSwap = e.NameScope.Find<ArcContentSwap>(HintPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _save = e.NameScope.Find<Button>(SavePart);
        if (_box is not null)
        {
            PushText();
            // Tunnel: Enter and Escape are decided before the box itself would insert a line.
            _box.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
            _box.TextChanged += OnBoxTextChanged;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_hintSwap is not null)
        {
            // A new host starts with the current line (its first content never animates).
            _hintSwap.Content = _hintLine;
        }
        UpdateAutomation();
        TryFocus();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        TryFocus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (!_textFromBox)
            {
                PushText();
            }
            UpdateHint();
        }
        else if (change.Property == ErrorTextProperty || change.Property == MaxLengthProperty || change.Property == HintTextProperty ||
                 change.Property == CountTextProperty)
        {
            UpdateHint();
        }
        else if (change.Property == IsSavingProperty)
        {
            PseudoClasses.Set(SavingPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == AccessibleNameProperty)
        {
            UpdateAutomation();
        }
    }

    /// <summary>Copies the draft into the box (the box's own change does not come back as an edit).</summary>
    private void PushText()
    {
        if (_box is null || _box.Text == Text)
        {
            return;
        }
        _pushingText = true;
        try
        {
            _box.Text = Text;
        }
        finally
        {
            _pushingText = false;
        }
    }

    private void OnBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_pushingText || _box is null)
        {
            return;
        }
        _textFromBox = true;
        try
        {
            SetCurrentValue(TextProperty, _box.Text ?? string.Empty);
        }
        finally
        {
            _textFromBox = false;
        }
        // The person changed the draft: the error of the last submit no longer applies.
        if (ErrorText is not null)
        {
            SetCurrentValue(ErrorTextProperty, null);
        }
    }

    #endregion

    #region Hint line

    /// <summary>
    /// Picks the message under the box (an error, else the live count past the limit, else the hint). A different kind
    /// of message, or a different error, crossfades in as a new line; the count changes its digits in place.
    /// </summary>
    private void UpdateHint()
    {
        var length = Text.Trim().Length;
        var limit = MaxLength;
        DraftLength = length;
        IsOverLimit = limit > 0 && length > limit;
        PseudoClasses.Set(OverLimitPseudoClass, IsOverLimit);
        PseudoClasses.Set(InvalidPseudoClass, IsInvalid);

        RowEditorHintKind kind;
        string? text;
        if (ErrorText is { } error)
        {
            kind = RowEditorHintKind.Error;
            text = error;
        }
        else if (IsOverLimit)
        {
            kind = RowEditorHintKind.Count;
            text = CountText is { } count
                ? count(length, limit)
                : length.ToString(CultureInfo.InvariantCulture) + "/" + limit.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            kind = RowEditorHintKind.Hint;
            text = HintText;
        }

        if (_hintLine is not null && kind == _hintKind && (kind == RowEditorHintKind.Count || _hintLine.Text == text))
        {
            _hintLine.Text = text;
        }
        else
        {
            _hintKind = kind;
            _hintLine = new RowEditorHintLine { Text = text, IsAlert = kind != RowEditorHintKind.Hint };
            if (_hintSwap is not null)
            {
                _hintSwap.Content = _hintLine;
            }
        }
        if (_box is not null)
        {
            _box.IsInvalid = IsInvalid;
        }
        UpdateAutomation();
    }

    /// <summary>
    /// The box is named by <see cref="AccessibleName"/> and described by the line under it (Arc's
    /// <c>aria-describedby</c>); the line is a polite live region, so a new count or error is read as it appears.
    /// </summary>
    private void UpdateAutomation()
    {
        var text = _hintLine?.Text ?? string.Empty;
        if (_box is not null)
        {
            AutomationProperties.SetName(_box, AccessibleName ?? string.Empty);
            AutomationProperties.SetHelpText(_box, text);
        }
        if (_hintSwap is not null)
        {
            AutomationProperties.SetLiveSetting(_hintSwap, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(_hintSwap, text);
        }
    }

    #endregion

    #region Input

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key is not (Key.Enter or Key.Escape))
        {
            return;
        }
        // While an input method composes, Enter commits the composition and Escape drops it: neither is ours.
        if (_box is { IsComposing: true })
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            // Shift+Enter is a new line (the box inserts it); Alt+Enter is left to the app.
            if ((e.KeyModifiers & (KeyModifiers.Shift | KeyModifiers.Alt)) != 0)
            {
                return;
            }
            e.Handled = true;
            Submit();
            return;
        }
        e.Handled = true;
        Cancel();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!Submit())
        {
            // The draft needs fixing: back to the box, where the person types.
            FocusEditor();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

    /// <summary>
    /// Validates the draft and raises <see cref="Submitted"/> when it is valid (with <see cref="Text"/> trimmed first);
    /// otherwise shows the matching error. Does nothing while saving. Returns whether the draft was submitted.
    /// </summary>
    internal bool Submit()
    {
        if (IsSaving)
        {
            return false;
        }
        var draft = Text.Trim();
        if (draft.Length == 0)
        {
            SetCurrentValue(ErrorTextProperty, EmptyErrorText ?? string.Empty);
            return false;
        }
        var limit = MaxLength;
        if (limit > 0 && draft.Length > limit)
        {
            SetCurrentValue(ErrorTextProperty, TooLongErrorText?.Invoke(limit) ?? string.Empty);
            return false;
        }
        if (draft != Text)
        {
            SetCurrentValue(TextProperty, draft);
        }
        RaiseEvent(new RoutedEventArgs(SubmittedEvent, this));
        return true;
    }

    /// <summary>Raises <see cref="Cancelled"/> unless saving.</summary>
    internal void Cancel()
    {
        if (IsSaving)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(CancelledEvent, this));
    }

    /// <summary>
    /// Focuses the box, with the caret after the last character when <paramref name="caretAtEnd"/> (otherwise where it
    /// was). Called before the editor is on screen, it focuses as soon as the box can take the focus.
    /// </summary>
    public void FocusEditor(bool caretAtEnd = true)
    {
        _focusPending = true;
        _focusCaretAtEnd = caretAtEnd;
        TryFocus();
    }

    private void TryFocus()
    {
        if (!_focusPending || _box is null || !_box.IsAttachedToVisualTree() || !_box.IsEffectivelyVisible || !_box.Focus())
        {
            return;
        }
        _focusPending = false;
        if (_focusCaretAtEnd)
        {
            var end = _box.Text?.Length ?? 0;
            _box.SelectionStart = end;
            _box.SelectionEnd = end;
            _box.CaretIndex = end;
        }
    }

    #endregion
}

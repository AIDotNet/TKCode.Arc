using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Visual weight of an <see cref="InlineEdit"/>.</summary>
public enum InlineEditVariant
{
    /// <summary>Names and headings: 22px, medium weight.</summary>
    Title,

    /// <summary>Descriptions: 14px secondary text.</summary>
    Body
}

/// <summary>Where an <see cref="InlineEdit"/> is in its save cycle.</summary>
public enum InlineEditPhase
{
    Idle,
    Saving,
    Saved,
    Failed
}

/// <summary>Arguments of <see cref="InlineEdit.TextCommitted"/>.</summary>
public sealed class InlineEditCommittedEventArgs : RoutedEventArgs
{
    public InlineEditCommittedEventArgs(RoutedEvent routedEvent, string oldValue, string newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The last saved value.</summary>
    public string OldValue { get; }

    /// <summary>The committed text (trimmed; single-line text has its whitespace collapsed).</summary>
    public string NewValue { get; }
}

/// <summary>Arguments of <see cref="InlineEdit.SaveFailed"/>.</summary>
public sealed class InlineEditSaveFailedEventArgs : RoutedEventArgs
{
    public InlineEditSaveFailedEventArgs(RoutedEvent routedEvent, string attemptedValue, Exception error)
        : base(routedEvent)
    {
        AttemptedValue = attemptedValue;
        Error = error;
    }

    public string AttemptedValue { get; }

    public Exception Error { get; }
}

/// <summary>
/// Arc inline edit: click-to-edit text for names and short fields read far more often than they change. The text
/// turns into a field in place with the same metrics, so nothing around it moves, and the frame grows with what is
/// typed. Enter commits (<see cref="TextCommitted"/>) and saves optimistically through <see cref="SaveHandler"/>: the
/// new text shows at once, quieter until the save lands, then a check draws. Escape cancels and the text rolls back;
/// a failed save restores the last saved <see cref="Value"/>, says why, and offers to try again. Tab or a click
/// elsewhere saves like Enter. Multiline: Shift+Enter adds a line break.
/// </summary>
[TemplatePart(BoxPart, typeof(Panel))]
[TemplatePart(DisplayPart, typeof(AvaloniaButton))]
[TemplatePart(EditorPart, typeof(TextBox))]
[PseudoClasses(EditingPseudoClass, InvalidPseudoClass, SavingPseudoClass, FlashPseudoClass, FlashOffPseudoClass, EmptyPseudoClass,
    TextHoverPseudoClass, TextFocusPseudoClass)]
public class InlineEdit : TemplatedControl
{
    public const string BoxPart = "PART_Box";
    public const string DisplayPart = "PART_Display";
    public const string EditorPart = "PART_Editor";
    public const string TextPart = "PART_Text";
    public const string FramePart = "PART_Frame";
    public const string RingPart = "PART_Ring";
    public const string SlotPart = "PART_Slot";
    public const string MessagePart = "PART_Message";
    public const string StatusPart = "PART_Status";

    public const string EditingPseudoClass = ":editing";
    public const string InvalidPseudoClass = ":invalid";
    public const string SavingPseudoClass = ":saving";
    public const string FlashPseudoClass = ":flash";
    public const string FlashOffPseudoClass = ":flash-off";
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while a mouse hovers the resting text (the frame tints, the pencil shows).</summary>
    public const string TextHoverPseudoClass = ":text-hover";

    /// <summary>Set while the resting text has keyboard focus.</summary>
    public const string TextFocusPseudoClass = ":text-focus";

    /// <summary>The saved value (two-way). A new value from outside replaces the text while it is not being edited.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<InlineEdit, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name, for example "Project name".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<InlineEdit, string>(nameof(Label), "Text");

    /// <summary>Shown while the value is empty.</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<InlineEdit, string>(nameof(PlaceholderText), string.Empty);

    /// <summary>Wraps onto several lines and grows in height; Enter still saves, Shift+Enter adds a line break.</summary>
    public static readonly StyledProperty<bool> IsMultilineProperty =
        AvaloniaProperty.Register<InlineEdit, bool>(nameof(IsMultiline));

    /// <summary>Title for names and headings (default), Body for descriptions.</summary>
    public static readonly StyledProperty<InlineEditVariant> VariantProperty =
        AvaloniaProperty.Register<InlineEdit, InlineEditVariant>(nameof(Variant));

    /// <summary>Heading level reported to assistive technology (Arc's <c>as="h1".."h3"</c>); 0 for none.</summary>
    public static readonly StyledProperty<int> HeadingLevelProperty =
        AvaloniaProperty.Register<InlineEdit, int>(nameof(HeadingLevel));

    /// <summary>Returns a message when the draft cannot be saved.</summary>
    public static readonly StyledProperty<Func<string, string?>?> ValidatorProperty =
        AvaloniaProperty.Register<InlineEdit, Func<string, string?>?>(nameof(Validator));

    /// <summary>Persists a committed value; a faulted task rolls the text back. Null saves at once.</summary>
    public static readonly StyledProperty<Func<string, Task>?> SaveHandlerProperty =
        AvaloniaProperty.Register<InlineEdit, Func<string, Task>?>(nameof(SaveHandler));

    /// <summary>The failed-save sentence: {0} the attempted text, {1} the label in lower case.</summary>
    public static readonly StyledProperty<string> FailedMessageFormatProperty =
        AvaloniaProperty.Register<InlineEdit, string>(nameof(FailedMessageFormat), "Couldn’t save “{0}”, so the last saved {1} is back.");

    /// <summary>The retry link after a failed save.</summary>
    public static readonly StyledProperty<string> RetryTextProperty =
        AvaloniaProperty.Register<InlineEdit, string>(nameof(RetryText), "Try again");

    /// <summary>True while the field is open.</summary>
    public static readonly DirectProperty<InlineEdit, bool> IsEditingProperty =
        AvaloniaProperty.RegisterDirect<InlineEdit, bool>(nameof(IsEditing), o => o.IsEditing);

    /// <summary>Where the control is in its save cycle.</summary>
    public static readonly DirectProperty<InlineEdit, InlineEditPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<InlineEdit, InlineEditPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The validation message shown under the field, if any.</summary>
    public static readonly DirectProperty<InlineEdit, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<InlineEdit, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>Raised when an edit is committed (Enter, Tab / click away, or the save button), before saving.</summary>
    public static readonly RoutedEvent<InlineEditCommittedEventArgs> TextCommittedEvent =
        RoutedEvent.Register<InlineEdit, InlineEditCommittedEventArgs>(nameof(TextCommitted), RoutingStrategies.Bubble);

    /// <summary>Raised when an edit is cancelled with Escape or the cancel button.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EditCanceledEvent =
        RoutedEvent.Register<InlineEdit, RoutedEventArgs>(nameof(EditCanceled), RoutingStrategies.Bubble);

    /// <summary>Raised when a save lands.</summary>
    public static readonly RoutedEvent<InlineEditCommittedEventArgs> SavedEvent =
        RoutedEvent.Register<InlineEdit, InlineEditCommittedEventArgs>(nameof(Saved), RoutingStrategies.Bubble);

    /// <summary>Raised when a save fails and the text rolls back.</summary>
    public static readonly RoutedEvent<InlineEditSaveFailedEventArgs> SaveFailedEvent =
        RoutedEvent.Register<InlineEdit, InlineEditSaveFailedEventArgs>(nameof(SaveFailed), RoutingStrategies.Bubble);

    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public bool IsMultiline { get => GetValue(IsMultilineProperty); set => SetValue(IsMultilineProperty, value); }
    public InlineEditVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public int HeadingLevel { get => GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }
    public Func<string, string?>? Validator { get => GetValue(ValidatorProperty); set => SetValue(ValidatorProperty, value); }
    public Func<string, Task>? SaveHandler { get => GetValue(SaveHandlerProperty); set => SetValue(SaveHandlerProperty, value); }
    public string FailedMessageFormat { get => GetValue(FailedMessageFormatProperty); set => SetValue(FailedMessageFormatProperty, value); }
    public string RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    private bool _isEditing;

    /// <summary>True while the field is open.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetAndRaise(IsEditingProperty, ref _isEditing, value);
    }

    private InlineEditPhase _phase;

    /// <summary>Where the control is in its save cycle.</summary>
    public InlineEditPhase Phase
    {
        get => _phase;
        private set => SetAndRaise(PhaseProperty, ref _phase, value);
    }

    private string? _errorMessage;

    /// <summary>The validation message shown under the field, if any.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value);
    }

    /// <summary>The text on screen (the optimistic value while saving).</summary>
    public string ShownText => _shown;

    public event EventHandler<InlineEditCommittedEventArgs>? TextCommitted
    {
        add => AddHandler(TextCommittedEvent, value);
        remove => RemoveHandler(TextCommittedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? EditCanceled
    {
        add => AddHandler(EditCanceledEvent, value);
        remove => RemoveHandler(EditCanceledEvent, value);
    }

    public event EventHandler<InlineEditCommittedEventArgs>? Saved
    {
        add => AddHandler(SavedEvent, value);
        remove => RemoveHandler(SavedEvent, value);
    }

    public event EventHandler<InlineEditSaveFailedEventArgs>? SaveFailed
    {
        add => AddHandler(SaveFailedEvent, value);
        remove => RemoveHandler(SaveFailedEvent, value);
    }

    private InlineEditBox? _box;
    private AvaloniaButton? _display;
    private Control? _pencil;
    private TextBox? _editor;
    private FieldSwapText? _text;
    private ArcContentSwap? _slot;
    private ArcContentSwap? _message;
    private TextBlock? _status;

    private string _shown = string.Empty;
    private string _committed = string.Empty;
    private string _draft = string.Empty;
    private string? _failed;
    private int _saveRun;
    private Point? _pressPoint;
    private bool _syncingValue;
    private DispatcherTimer? _phaseTimer;
    private DispatcherTimer? _flashTimer;

    static InlineEdit()
    {
        FocusableProperty.OverrideDefaultValue<InlineEdit>(false);
        FocusModality.Watch();
    }

    // ---- template ------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _box = e.NameScope.Find<InlineEditBox>(BoxPart);
        _display = e.NameScope.Find<AvaloniaButton>(DisplayPart);
        _editor = e.NameScope.Find<TextBox>(EditorPart);
        _text = e.NameScope.Find<FieldSwapText>(TextPart);
        _slot = e.NameScope.Find<ArcContentSwap>(SlotPart);
        _message = e.NameScope.Find<ArcContentSwap>(MessagePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_box is not null)
        {
            _box.Display = _display;
            _box.Editor = _editor;
            _box.Frame = e.NameScope.Find<Control>(FramePart);
            _box.Ring = e.NameScope.Find<Control>(RingPart);
            _box.Slot = _slot;
        }
        if (_display is not null)
        {
            _display.Click += OnDisplayClick;
            _display.AddHandler(PointerPressedEvent, OnDisplayPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _display.PointerEntered += OnDisplayPointerEntered;
            _display.PointerExited += OnDisplayPointerExited;
            _display.GotFocus += OnDisplayGotFocus;
            _display.LostFocus += OnDisplayLostFocus;
        }
        if (_editor is not null)
        {
            _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
            _editor.PropertyChanged += OnEditorPropertyChanged;
            _editor.LostFocus += OnEditorLostFocus;
            _editor.IsVisible = IsEditing;
            _editor.AcceptsReturn = IsMultiline;
            _editor.TextWrapping = IsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }
        _shown = Value;
        _committed = Value;
        ShowText(roll: null);
        UpdateSlot();
        UpdateMessage();
        UpdateAutomation();
    }

    private void Detach()
    {
        if (_display is not null)
        {
            _display.Click -= OnDisplayClick;
            _display.RemoveHandler(PointerPressedEvent, OnDisplayPressed);
            _display.PointerEntered -= OnDisplayPointerEntered;
            _display.PointerExited -= OnDisplayPointerExited;
            _display.GotFocus -= OnDisplayGotFocus;
            _display.LostFocus -= OnDisplayLostFocus;
        }
        if (_editor is not null)
        {
            _editor.RemoveHandler(KeyDownEvent, OnEditorKeyDown);
            _editor.PropertyChanged -= OnEditorPropertyChanged;
            _editor.LostFocus -= OnEditorLostFocus;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !_syncingValue)
        {
            // A new value from outside replaces the text unless the person is typing or a save is in flight.
            _committed = Value;
            if (!IsEditing && Phase != InlineEditPhase.Saving && Value != _shown)
            {
                _shown = Value;
                ShowText(roll: 1);
            }
        }
        else if (change.Property == LabelProperty || change.Property == PlaceholderTextProperty || change.Property == HeadingLevelProperty)
        {
            UpdateAutomation();
            if (change.Property == PlaceholderTextProperty)
            {
                ShowText(roll: null);
            }
        }
        else if (change.Property == IsMultilineProperty && _editor is not null)
        {
            _editor.AcceptsReturn = IsMultiline;
            _editor.TextWrapping = IsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
            UpdateSlot();
        }
    }

    private void UpdateAutomation()
    {
        if (_display is not null)
        {
            AutomationProperties.SetName(_display, $"{Label}: {(_shown.Length > 0 ? _shown : PlaceholderText)}");
            AutomationProperties.SetHelpText(_display, "Activate to edit.");
            AutomationProperties.SetHeadingLevel(_display, HeadingLevel);
        }
        if (_editor is not null)
        {
            AutomationProperties.SetName(_editor, Label);
            AutomationProperties.SetHelpText(_editor, ErrorMessage ?? (IsMultiline
                ? "Enter saves, Shift+Enter adds a line break, Escape cancels."
                : "Enter saves, Escape cancels."));
        }
    }

    // ---- display text ------------------------------------------------------------------------------------------

    /// <summary>Shows the draft while editing and the shown value otherwise; <paramref name="roll"/> animates the swap.</summary>
    private void ShowText(int? roll, InlineEditSizeCause cause = InlineEditSizeCause.Morph)
    {
        if (_text is null)
        {
            return;
        }
        var layer = IsEditing ? _draft : _shown;
        PseudoClasses.Set(EmptyPseudoClass, layer.Length == 0);
        // A zero-width space keeps the line box, so an empty field never collapses; multiline editing keeps a trailing line.
        var text = (layer.Length > 0 ? layer : PlaceholderText.Length > 0 && !IsEditing ? PlaceholderText : layer)
                   + (IsMultiline && IsEditing ? "​" : string.Empty);
        if (IsEditing && layer.Length == 0)
        {
            text = PlaceholderText.Length > 0 ? PlaceholderText : "​";
        }
        if (_box is not null)
        {
            _box.PendingCause = cause;
        }
        if (roll is { } direction)
        {
            _text.Direction = direction;
            _text.Text = text;
        }
        else
        {
            _text.SetTextSilently(text);
        }
        UpdateAutomation();
    }

    // ---- editing -------------------------------------------------------------------------------------------------

    // Hover hints follow fine pointers only; touch goes straight to editing.
    private void OnDisplayPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(TextHoverPseudoClass, true);
            UpdatePencil();
        }
    }

    private void OnDisplayPointerExited(object? sender, PointerEventArgs e)
    {
        PseudoClasses.Set(TextHoverPseudoClass, false);
        UpdatePencil();
    }

    private void OnDisplayGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Keyboard focus (focus-visible) only: a click goes straight into the field.
        PseudoClasses.Set(TextFocusPseudoClass, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
        UpdatePencil();
    }

    private void OnDisplayLostFocus(object? sender, FocusChangedEventArgs e)
    {
        PseudoClasses.Set(TextFocusPseudoClass, false);
        UpdatePencil();
    }

    private void UpdatePencil()
    {
        if (_pencil is not null)
        {
            var shown = _pencil.IsPointerOver || Classes.Contains(TextHoverPseudoClass) || Classes.Contains(TextFocusPseudoClass);
            _pencil.Opacity = shown ? 1 : 0;
        }
    }

    private void OnDisplayPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressPoint = _text is not null ? e.GetPosition(_text) : null;
    }

    private void OnDisplayClick(object? sender, RoutedEventArgs e)
    {
        if (Phase == InlineEditPhase.Saving)
        {
            return;
        }
        // A keyboard activation selects everything, ready to retype; a click puts the caret where it landed.
        var caret = _pressPoint is { } point && _text is not null && _shown.Length > 0 ? Math.Min(_text.HitTest(point), _shown.Length) : -1;
        _pressPoint = null;
        BeginEdit(caret);
        e.Handled = true;
    }

    /// <summary>Opens the field. <paramref name="caret"/> -1 selects everything.</summary>
    public void BeginEdit(int caret = -1) => StartEdit(caret, _shown);

    private void StartEdit(int caret, string text)
    {
        if (IsEditing || Phase == InlineEditPhase.Saving || _editor is null)
        {
            return;
        }
        _draft = text;
        ErrorMessage = null;
        _failed = null;
        SetFlash(null);
        Phase = InlineEditPhase.Idle;
        IsEditing = true;
        PseudoClasses.Set(EditingPseudoClass, true);
        PseudoClasses.Set(InvalidPseudoClass, false);
        _editor.Text = text;
        _editor.IsVisible = true;
        ShowText(roll: null);
        UpdateSlot();
        UpdateMessage();
        _editor.Focus(NavigationMethod.Pointer);
        if (caret < 0)
        {
            _editor.SelectAll();
        }
        else
        {
            _editor.ClearSelection();
            _editor.CaretIndex = Math.Clamp(caret, 0, text.Length);
        }
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || !IsEditing)
        {
            return;
        }
        _draft = _editor?.Text ?? string.Empty;
        if (ErrorMessage is not null)
        {
            SetError(Validator?.Invoke(Clean(_draft)));
        }
        ShowText(roll: null, InlineEditSizeCause.Typing);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelEdit();
        }
        else if (e.Key == Key.Enter && !(IsMultiline && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            e.Handled = true;
            Submit(InlineEditSource.Key);
        }
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        // Leaving the component saves, the way a rename does; switching windows does not.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsEditing)
            {
                return;
            }
            if (TopLevel.GetTopLevel(this) is { } top)
            {
                if (top is WindowBase { IsActive: false })
                {
                    return;
                }
                if (top.FocusManager?.GetFocusedElement() is Visual focused && (focused == this || this.IsVisualAncestorOf(focused)))
                {
                    return;
                }
            }
            Submit(InlineEditSource.Blur);
        }, DispatcherPriority.Input);
    }

    private enum InlineEditSource
    {
        Key,
        Button,
        Blur
    }

    private string Clean(string text) => IsMultiline
        ? text.Trim()
        : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Commits the open edit (validates first; a validation error keeps the field open).</summary>
    public void CommitEdit() => Submit(InlineEditSource.Button);

    private void Submit(InlineEditSource source)
    {
        if (!IsEditing)
        {
            return;
        }
        var next = Clean(_draft);
        var problem = Validator?.Invoke(next);
        if (!string.IsNullOrEmpty(problem))
        {
            SetError(problem);
            if (source != InlineEditSource.Blur)
            {
                _editor?.Focus();
            }
            return;
        }
        SetError(null);
        var cleaned = next != _draft;
        CloseEditor(focusDisplay: source != InlineEditSource.Blur);
        if (next == _shown)
        {
            // Trimmed spaces would shift the text in one frame, so a cleaned value arrives with the text motion.
            ShowText(roll: cleaned ? 1 : null);
            return;
        }
        var previous = _committed;
        var run = ++_saveRun;
        _shown = next;
        ShowText(roll: cleaned ? 1 : null);
        Phase = InlineEditPhase.Saving;
        PseudoClasses.Set(SavingPseudoClass, true);
        Announce($"Saving {Label.ToLowerInvariant()}");
        UpdateSlot();
        RaiseEvent(new InlineEditCommittedEventArgs(TextCommittedEvent, previous, next));
        _ = SaveAsync(run, previous, next);
    }

    private async Task SaveAsync(int run, string previous, string next)
    {
        Exception? failure = null;
        try
        {
            if (SaveHandler is { } handler)
            {
                await handler(next);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        if (run != _saveRun)
        {
            return;
        }
        PseudoClasses.Set(SavingPseudoClass, false);
        if (failure is null)
        {
            _committed = next;
            _syncingValue = true;
            try
            {
                SetCurrentValue(ValueProperty, next);
            }
            finally
            {
                _syncingValue = false;
            }
            Phase = InlineEditPhase.Saved;
            Announce($"{Label} saved");
            UpdateSlot();
            RaiseEvent(new InlineEditCommittedEventArgs(SavedEvent, previous, next));
            RestartTimer(ref _phaseTimer, TimeSpan.FromMilliseconds(1800), () =>
            {
                if (Phase == InlineEditPhase.Saved)
                {
                    Phase = InlineEditPhase.Idle;
                    UpdateSlot();
                }
            });
            return;
        }
        // Rollback: the saved text drops back in from above, the frame washes danger, and the message offers a retry.
        _shown = previous;
        ShowText(roll: -1);
        Phase = InlineEditPhase.Failed;
        _failed = next;
        SetFlash(true);
        Announce(string.Empty);
        UpdateSlot();
        UpdateMessage();
        RaiseEvent(new InlineEditSaveFailedEventArgs(SaveFailedEvent, next, failure));
        RestartTimer(ref _flashTimer, TimeSpan.FromMilliseconds(1400), () =>
        {
            SetFlash(false);
            RestartTimer(ref _flashTimer, TimeSpan.FromMilliseconds(600), () => SetFlash(null));
        });
    }

    /// <summary>Closes the field without saving; the text rolls back if it changed.</summary>
    public void CancelEdit()
    {
        if (!IsEditing)
        {
            return;
        }
        var changed = _draft != _shown;
        SetError(null);
        CloseEditor(focusDisplay: true);
        ShowText(roll: changed ? -1 : null);
        RaiseEvent(new RoutedEventArgs(EditCanceledEvent));
    }

    private void Retry()
    {
        if (_failed is { } failed)
        {
            StartEdit(failed.Length, failed);
        }
    }

    private void CloseEditor(bool focusDisplay)
    {
        IsEditing = false;
        PseudoClasses.Set(EditingPseudoClass, false);
        if (_editor is not null)
        {
            _editor.IsVisible = false;
        }
        UpdateSlot();
        UpdateMessage();
        if (focusDisplay)
        {
            // Save and Cancel clicked with the mouse hand focus back without the keyboard ring.
            _display?.Focus(FocusModality.For(this));
        }
    }

    private void SetError(string? error)
    {
        ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        PseudoClasses.Set(InvalidPseudoClass, ErrorMessage is not null);
        UpdateMessage();
        UpdateAutomation();
    }

    private void SetFlash(bool? on)
    {
        PseudoClasses.Set(FlashPseudoClass, on == true);
        PseudoClasses.Set(FlashOffPseudoClass, on == false);
    }

    private void Announce(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    private static void RestartTimer(ref DispatcherTimer? timer, TimeSpan interval, Action action)
    {
        timer?.Stop();
        var created = new DispatcherTimer { Interval = interval };
        created.Tick += (_, _) =>
        {
            created.Stop();
            action();
        };
        timer = created;
        created.Start();
    }

    // ---- slot and message ----------------------------------------------------------------------------------------

    private string _slotKey = string.Empty;

    private void UpdateSlot()
    {
        if (_slot is null)
        {
            return;
        }
        var key = IsEditing ? "edit" : Phase.ToString();
        if (key == _slotKey && _slot.Content is not null)
        {
            return;
        }
        _slotKey = key;
        _slot.Content = key switch
        {
            "edit" => CreateActions(),
            nameof(InlineEditPhase.Saving) => CreateSpinner(),
            nameof(InlineEditPhase.Saved) => CreateSavedCheck(),
            nameof(InlineEditPhase.Failed) => CreateGlyph(ArcIcons.CircleAlert, 16, DesignTokens.ArcDesignTokenKind.Danger, "inline-edit-failed"),
            _ => CreatePencil()
        };
    }

    private Control CreateActions()
    {
        var noun = Label.ToLowerInvariant();
        var save = new InlineEditActionButton { IsPrimary = true, Content = new ArcGlyph { Data = ArcIcons.Check, Size = 15, StrokeThickness = 2 } };
        AutomationProperties.SetName(save, $"Save {noun}");
        save.Click += (_, e) =>
        {
            e.Handled = true;
            Submit(InlineEditSource.Button);
        };
        var cancel = new InlineEditActionButton { Content = new ArcGlyph { Data = ArcIcons.X, Size = 15, StrokeThickness = 2 } };
        AutomationProperties.SetName(cancel, "Cancel editing");
        cancel.Click += (_, e) =>
        {
            e.Handled = true;
            CancelEdit();
        };
        return new StackPanel
        {
            Orientation = IsMultiline ? Orientation.Vertical : Orientation.Horizontal,
            Spacing = 4,
            Classes = { "inline-edit-actions" },
            Children = { save, cancel }
        };
    }

    private Control CreatePencil()
    {
        var pencil = CreateGlyph(ArcIcons.Pencil, 14, DesignTokens.ArcDesignTokenKind.TextMuted, "inline-edit-pencil");
        pencil.IsHitTestVisible = true;
        pencil.Margin = new Thickness(2, 0, 0, 0);
        pencil.Cursor = new Cursor(StandardCursorType.Hand);
        pencil.PointerReleased += (_, e) =>
        {
            e.Handled = true;
            StartEdit(_shown.Length, _shown);
        };
        pencil.PointerEntered += (_, _) => UpdatePencil();
        pencil.PointerExited += (_, _) => UpdatePencil();
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            pencil.Transitions = new Avalonia.Animation.Transitions { new ArcDoubleTransition { Property = OpacityProperty } };
        }
        _pencil = pencil;
        UpdatePencil();
        return pencil;
    }

    private Control CreateSpinner()
    {
        var spinner = new ArcSpinner { Width = 14, Height = 14, Margin = new Thickness(2, 0, 0, 0), Classes = { "inline-edit-spinner" } };
        ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        return spinner;
    }

    private Control CreateSavedCheck()
    {
        var check = new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2.25, Progress = 0, Margin = new Thickness(1, 0, 0, 0) };
        ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Success);
        check.AttachedToVisualTree += (_, _) =>
        {
            // The tick draws from its short stroke; reduced motion shows it drawn.
            if (!ArcMotion.IsFull(this))
            {
                check.Progress = 1;
                return;
            }
            check.Progress = 0;
            _ = check.Play(TimeSpan.FromMilliseconds(320), ArcEasings.Enter, TimeSpan.FromMilliseconds(60));
        };
        return check;
    }

    private static ArcGlyph CreateGlyph(Geometry data, double size, DesignTokens.ArcDesignTokenKind color, string cls)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, StrokeThickness = 1.75, Classes = { cls } };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    private string _messageKey = string.Empty;

    private void UpdateMessage()
    {
        if (_message is null)
        {
            return;
        }
        string key;
        Control? content = null;
        if (ErrorMessage is { } error)
        {
            key = "error:" + error;
            if (key != _messageKey)
            {
                content = CreateMessage(error, DesignTokens.ArcDesignTokenKind.Danger, retry: false);
            }
        }
        else if (_failed is { } failed && !IsEditing)
        {
            key = "failed:" + failed;
            if (key != _messageKey)
            {
                var text = string.Format(System.Globalization.CultureInfo.CurrentCulture, FailedMessageFormat, failed, Label.ToLowerInvariant());
                content = CreateMessage(text, DesignTokens.ArcDesignTokenKind.TextSecondary, retry: true);
            }
        }
        else
        {
            key = string.Empty;
        }
        if (key == _messageKey)
        {
            return;
        }
        _messageKey = key;
        _message.Content = content;
        AutomationProperties.SetName(_message, content is null ? string.Empty : key[(key.IndexOf(':') + 1)..]);
    }

    private Control CreateMessage(string text, DesignTokens.ArcDesignTokenKind color, bool retry)
    {
        var icon = CreateGlyph(ArcIcons.CircleAlert, 14, DesignTokens.ArcDesignTokenKind.Danger, "inline-edit-message-icon");
        icon.StrokeThickness = 2;
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 6, 0);
        var copy = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 12 * 1.4 };
        ArcTokens.Bind(copy, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(copy, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
        copy.Inlines = new Avalonia.Controls.Documents.InlineCollection { new Avalonia.Controls.Documents.Run(text) };
        var row = new DockPanel { Margin = new Thickness(9, 6, 8, 4) };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        if (retry)
        {
            var link = new InlineEditRetryLink { Text = RetryText, Margin = new Thickness(2, 0, 0, 0) };
            link.Click += (_, e) =>
            {
                e.Handled = true;
                Retry();
            };
            var flow = new WrapPanel { Children = { copy, link } };
            row.Children.Add(flow);
        }
        else
        {
            row.Children.Add(copy);
        }
        return row;
    }
}

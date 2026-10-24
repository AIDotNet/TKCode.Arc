// Ported from Arc's free otp-input block (MIT, Copyright (c) 2026 Elia Kuratli): otp-input.tsx.
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A row of single-character slots for one-time codes (Arc's OTP input block): a visible label, one slot per character,
/// helper and error copy under the row. Typing fills a slot and moves on; a paste fills the slots from the focused one;
/// arrow keys move between slots and Backspace on an empty slot clears the one before it. Characters rise into their
/// slot, a paste lands as a short left to right wave, one focus ring glides between the slots, and a new error nudges
/// the row side to side once. <see cref="Value"/> is two-way; <see cref="Completed"/> fires when an edit fills the last
/// slot, which is where a host checks the code.
/// </summary>
/// <remarks>
/// The code is one string, as in Arc: slot <c>i</c> shows <c>Value[i]</c>, so clearing a slot in the middle moves the
/// characters after it one slot to the left, and a character typed into a slot past the end of the code lands in the
/// first empty slot. Arc's previews use the defaults: six digits labelled "Verification code".
/// </remarks>
[TemplatePart(FieldPart, typeof(Field))]
[TemplatePart(InputsPart, typeof(Panel))]
[TemplatePart(RowPart, typeof(OtpInputRow))]
[TemplatePart(OverlayPart, typeof(OtpInputOverlay))]
[PseudoClasses(InvalidPseudoClass, SuccessPseudoClass, CompletePseudoClass, ReadOnlyPseudoClass, NarrowPseudoClass)]
public class OtpInput : TemplatedControl
{
    public const string FieldPart = "PART_Field";
    public const string InputsPart = "PART_Inputs";
    public const string RowPart = "PART_Row";
    public const string OverlayPart = "PART_Overlay";

    /// <summary>Set while <see cref="IsInvalid"/> is true.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>Set while <see cref="IsSuccess"/> is true.</summary>
    public const string SuccessPseudoClass = ":success";

    /// <summary>Set while <see cref="IsComplete"/> is true.</summary>
    public const string CompletePseudoClass = ":complete";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while the field is offered at most 328px: slots 6 apart and 44 high (Arc's 360px breakpoint).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The field is narrow at or below this offered width: Arc's <c>max-width: 360px</c> viewport less two 16px gutters.</summary>
    internal const double NarrowWidth = 328;

    private const int MaxLength = 32;

    /// <summary>Number of slots (Arc's <c>length</c>), 1 to 32.</summary>
    public static readonly StyledProperty<int> LengthProperty =
        AvaloniaProperty.Register<OtpInput, int>(nameof(Length), 6, coerce: (_, v) => Math.Clamp(v, 1, MaxLength));

    /// <summary>The code, one character per slot (Arc's <c>value</c> and <c>onChange</c>); two-way.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<OtpInput, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => v ?? string.Empty);

    /// <summary>The visible label and the name of the group of slots (Arc's required <c>label</c>).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<OtpInput, string?>(nameof(Label), "Verification code");

    /// <summary>Helper copy under the slots (Arc's <c>description</c>).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OtpInput, string?>(nameof(Description));

    /// <summary>Error copy under the slots (Arc's <c>error</c>); non-empty makes the field invalid, and a new error shakes the row once.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<OtpInput, string?>(nameof(ErrorMessage));

    /// <summary>Invalid state: danger slots and ring. Always true while <see cref="ErrorMessage"/> is not empty.</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<OtpInput, bool>(nameof(IsInvalid), coerce: CoerceIsInvalid);

    /// <summary>Success state: success slots and ring (an addition to Arc, which has no success look).</summary>
    public static readonly StyledProperty<bool> IsSuccessProperty =
        AvaloniaProperty.Register<OtpInput, bool>(nameof(IsSuccess));

    /// <summary>Which characters the slots accept (Arc's <c>inputMode</c>): digits only (default) or any text.</summary>
    public static readonly StyledProperty<OtpInputMode> InputModeProperty =
        AvaloniaProperty.Register<OtpInput, OtpInputMode>(nameof(InputMode));

    /// <summary>Focuses the first slot when the field is loaded (Arc's <c>autoFocus</c>).</summary>
    public static readonly StyledProperty<bool> AutoFocusProperty =
        AvaloniaProperty.Register<OtpInput, bool>(nameof(AutoFocus));

    /// <summary>When true the slots keep focus and arrow keys but ignore typing, paste and deletion (while a host checks a code).</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<OtpInput, bool>(nameof(IsReadOnly));

    /// <summary>
    /// The accessible name of each slot: <c>{0}</c> is <see cref="Label"/>, <c>{1}</c> the slot's position from 1 and
    /// <c>{2}</c> <see cref="Length"/> ("Verification code, digit 1 of 6").
    /// </summary>
    public static readonly StyledProperty<string> SlotLabelFormatProperty =
        AvaloniaProperty.Register<OtpInput, string>(nameof(SlotLabelFormat), "{0}, digit {1} of {2}");

    /// <summary>Executed with the code when an edit fills every slot (after <see cref="Completed"/>).</summary>
    public static readonly StyledProperty<ICommand?> CompletedCommandProperty =
        AvaloniaProperty.Register<OtpInput, ICommand?>(nameof(CompletedCommand));

    /// <summary>True while every slot holds a character.</summary>
    public static readonly DirectProperty<OtpInput, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<OtpInput, bool>(nameof(IsComplete), o => o.IsComplete);

    /// <summary>Raised whenever <see cref="Value"/> changes; <see cref="OtpInputValueChangedEventArgs.ChangeSource"/> tells edits from code.</summary>
    public static readonly RoutedEvent<OtpInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<OtpInput, OtpInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after every edit by the person that leaves all slots filled (Arc: <c>if (next.length === length) verify(next)</c>).</summary>
    public static readonly RoutedEvent<OtpInputCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<OtpInput, OtpInputCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Number of slots.</summary>
    public int Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <summary>The code.</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The visible label.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the slots.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the slots.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Invalid state.</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>Success state.</summary>
    public bool IsSuccess { get => GetValue(IsSuccessProperty); set => SetValue(IsSuccessProperty, value); }

    /// <summary>Which characters the slots accept.</summary>
    public OtpInputMode InputMode { get => GetValue(InputModeProperty); set => SetValue(InputModeProperty, value); }

    /// <summary>Focus the first slot when loaded.</summary>
    public bool AutoFocus { get => GetValue(AutoFocusProperty); set => SetValue(AutoFocusProperty, value); }

    /// <summary>Ignore edits while keeping focus and navigation.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>The accessible name of each slot.</summary>
    public string SlotLabelFormat { get => GetValue(SlotLabelFormatProperty); set => SetValue(SlotLabelFormatProperty, value); }

    /// <summary>Executed with the code when an edit completes it.</summary>
    public ICommand? CompletedCommand { get => GetValue(CompletedCommandProperty); set => SetValue(CompletedCommandProperty, value); }

    /// <summary>True while every slot holds a character.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value);
    }

    /// <summary>Raised whenever the code changes.</summary>
    public event EventHandler<OtpInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>Raised when an edit fills every slot.</summary>
    public event EventHandler<OtpInputCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    private readonly List<OtpInputSlot> _slots = new();
    private readonly TranslateTransform _shakeTransform = new();
    private Field? _field;
    private Panel? _inputs;
    private OtpInputRow? _row;
    private OtpInputOverlay? _overlay;
    private MotionValue<double>? _shake;
    private string[] _shown = [];
    private string? _lastError;
    private bool _isComplete;
    private OtpInputChangeSource _editSource = OtpInputChangeSource.Programmatic;

    private static bool CoerceIsInvalid(AvaloniaObject target, bool value)
        => value || !string.IsNullOrEmpty(((OtpInput)target).ErrorMessage);

    /// <summary>The slots, in order (template parts created in code).</summary>
    internal IReadOnlyList<OtpInputSlot> Slots => _slots;

    /// <summary>The drawn characters and ring.</summary>
    internal OtpInputOverlay? Overlay => _overlay;

    /// <summary>The row's horizontal shake offset right now.</summary>
    internal double ShakeOffset => _shakeTransform.X;

    // ---- public methods ---------------------------------------------------------------------------------------

    /// <summary>Focuses slot <paramref name="index"/> (clamped to the row). Returns false when it cannot take focus.</summary>
    public bool FocusSlot(int index)
    {
        if (_slots.Count == 0)
        {
            return false;
        }
        return _slots[Math.Clamp(index, 0, _slots.Count - 1)].Focus(NavigationMethod.Unspecified);
    }

    /// <summary>Nudges the row side to side once (what a new <see cref="ErrorMessage"/> does). Full motion only.</summary>
    public void Shake()
    {
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shake.Changed += p => _shakeTransform.X = ShakeAt(p);
        }
        _shake.Jump(0);
        _ = _shake.AnimateTo(1, MotionSpec.Tween(ShakeDuration, s_linear));
    }

    // ---- template and state -----------------------------------------------------------------------------------

    private static readonly Easing s_linear = new LinearEasing();
    private static readonly TimeSpan ShakeDuration = TimeSpan.FromMilliseconds(360);
    private static readonly double[] s_shakeFrames = [0, -6, 5, -3, 2, 0];

    /// <summary>
    /// The shake at progress <paramref name="p"/>: x through 0, −6, 5, −3, 2, 0 in five equal segments, each on the
    /// standard curve (motion applies a single ease to every keyframe segment; measured on Arc's sign-in block).
    /// </summary>
    internal static double ShakeAt(double p)
    {
        if (p <= 0 || p >= 1)
        {
            return 0;
        }
        var segments = s_shakeFrames.Length - 1;
        var scaled = p * segments;
        var index = Math.Min((int)scaled, segments - 1);
        var local = ArcEasings.Standard.Ease(scaled - index);
        return s_shakeFrames[index] + (s_shakeFrames[index + 1] - s_shakeFrames[index]) * local;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_row is not null)
        {
            _row.Arranged -= OnRowArranged;
            _row.Children.Clear();
        }
        if (_inputs is not null)
        {
            _inputs.RenderTransform = null;
        }
        _field = e.NameScope.Find<Field>(FieldPart);
        _inputs = e.NameScope.Find<Panel>(InputsPart);
        _row = e.NameScope.Find<OtpInputRow>(RowPart);
        _overlay = e.NameScope.Find<OtpInputOverlay>(OverlayPart);
        if (_inputs is not null)
        {
            _inputs.RenderTransform = _shakeTransform;
        }
        if (_overlay is not null)
        {
            _overlay.Row = _row;
        }
        if (_row is not null)
        {
            _row.Arranged += OnRowArranged;
        }
        _slots.Clear();
        RebuildSlots();
        UpdateStates();
    }

    private void RebuildSlots()
    {
        if (_row is null)
        {
            return;
        }
        var focused = _slots.FindIndex(s => s.IsFocused);
        _row.Children.Clear();
        _slots.Clear();
        for (var i = 0; i < Length; i++)
        {
            var slot = new OtpInputSlot(this, i);
            _slots.Add(slot);
            _row.Children.Add(slot);
        }
        _shown = [];
        ApplyValue(animate: false);
        UpdateSlotOptions();
        UpdateAutomation();
        UpdateStates();
        if (focused >= 0)
        {
            FocusSlot(focused);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var source = _editSource;
            ApplyValue(animate: true);
            RaiseEvent(new OtpInputValueChangedEventArgs(ValueChangedEvent, change.GetOldValue<string>() ?? string.Empty,
                change.GetNewValue<string>() ?? string.Empty, source));
        }
        else if (change.Property == LengthProperty)
        {
            RebuildSlots();
        }
        else if (change.Property == ErrorMessageProperty)
        {
            CoerceValue(IsInvalidProperty);
            var error = ErrorMessage;
            // A new error nudges the row once, so a rejected code reads as a response to the attempt.
            if (!string.IsNullOrEmpty(error) && error != _lastError)
            {
                Shake();
            }
            _lastError = error;
            UpdateAutomation();
        }
        else if (change.Property == IsInvalidProperty || change.Property == IsSuccessProperty)
        {
            UpdateStates();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
        }
        else if (change.Property == InputModeProperty)
        {
            UpdateSlotOptions();
        }
        else if (change.Property == LabelProperty || change.Property == SlotLabelFormatProperty || change.Property == DescriptionProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == AutoFocusProperty && AutoFocus && IsLoaded)
        {
            FocusSlot(0);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _lastError = ErrorMessage;
        if (AutoFocus)
        {
            FocusSlot(0);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        PseudoClasses.Set(NarrowPseudoClass, availableSize.Width <= NarrowWidth);
        if (HorizontalAlignment != HorizontalAlignment.Stretch && double.IsNaN(Width) && !double.IsInfinity(availableSize.Width))
        {
            // Not stretched (Arc's field in a centring preview): as wide as its widest part, like a grid at max-content,
            // and never wider than the room offered. The message rows would otherwise take all the width they are given.
            var natural = base.MeasureOverride(availableSize.WithWidth(double.PositiveInfinity));
            return base.MeasureOverride(availableSize.WithWidth(Math.Min(natural.Width, availableSize.Width)));
        }
        return base.MeasureOverride(availableSize);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OtpInputAutomationPeer(this);

    /// <summary>
    /// The field itself is only a wrapper, as Arc's <c>.field</c> div: the label is text, and the row of slots is the
    /// one group (named by the label) that assistive technology announces.
    /// </summary>
    private sealed class OtpInputAutomationPeer : ControlAutomationPeer
    {
        public OtpInputAutomationPeer(OtpInput owner)
            : base(owner)
        {
        }

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }

    /// <summary>The characters of the code, one per slot ("" for an empty slot).</summary>
    private string[] Characters()
    {
        var value = Value;
        var result = new string[Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = i < value.Length ? value[i].ToString() : string.Empty;
        }
        return result;
    }

    private void ApplyValue(bool animate)
    {
        var characters = Characters();
        IsComplete = Value.Length >= Length;
        PseudoClasses.Set(CompletePseudoClass, IsComplete);
        // A paste fills several slots in one update: those characters land as a short left to right wave.
        var changed = 0;
        for (var i = 0; i < characters.Length; i++)
        {
            var before = i < _shown.Length ? _shown[i] : string.Empty;
            if (before != characters[i])
            {
                changed++;
            }
        }
        var wave = changed > 1;
        for (var i = 0; i < _slots.Count && i < characters.Length; i++)
        {
            _slots[i].Sync(characters[i]);
        }
        _overlay?.SetCharacters(characters, wave, animate && _shown.Length == characters.Length);
        _shown = characters;
    }

    private void UpdateStates()
    {
        PseudoClasses.Set(InvalidPseudoClass, IsInvalid);
        PseudoClasses.Set(SuccessPseudoClass, IsSuccess);
        PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
        foreach (var slot in _slots)
        {
            slot.IsInvalid = IsInvalid;
            slot.IsSuccess = IsSuccess && !IsInvalid;
        }
        _overlay?.SetRingTone(IsInvalid ? OtpInputRingTone.Invalid : IsSuccess ? OtpInputRingTone.Success : OtpInputRingTone.Normal);
    }

    private void UpdateSlotOptions()
    {
        foreach (var slot in _slots)
        {
            TextInputOptions.SetContentType(slot, InputMode == OtpInputMode.Numeric ? TextInputContentType.Digits : TextInputContentType.Normal);
            TextInputOptions.SetShowSuggestions(slot, false);
        }
    }

    private void UpdateAutomation()
    {
        if (_row is not null)
        {
            AutomationProperties.SetName(_row, Label ?? string.Empty);
        }
        var help = HelpText(Description, ErrorMessage);
        for (var i = 0; i < _slots.Count; i++)
        {
            var name = string.Format(CultureInfo.CurrentCulture, SlotLabelFormat, Label ?? string.Empty, i + 1, _slots.Count);
            AutomationProperties.SetName(_slots[i], name);
            AutomationProperties.SetHelpText(_slots[i], help);
        }
    }

    private static string HelpText(string? description, string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return description ?? string.Empty;
        }
        return string.IsNullOrEmpty(description) ? error : $"{description} {error}";
    }

    private void OnRowArranged(object? sender, EventArgs e)
    {
        // Slots shrink in a narrow row: keep the ring on the focused slot, without travel.
        if (_row is null || _overlay is null)
        {
            return;
        }
        var focused = _slots.FindIndex(s => s.IsFocused);
        if (focused >= 0)
        {
            _overlay.SnapRing(_row.SlotRect(focused));
        }
    }

    // ---- editing (Arc's handleChange / handlePaste / handleKeyDown) -------------------------------------------

    private string Sanitize(string text)
    {
        if (InputMode != OtpInputMode.Numeric)
        {
            return text;
        }
        return string.Create(text.Count(char.IsAsciiDigit), text, static (span, source) =>
        {
            var at = 0;
            foreach (var c in source)
            {
                if (char.IsAsciiDigit(c))
                {
                    span[at++] = c;
                }
            }
        });
    }

    private void Commit(string[] next, OtpInputChangeSource source)
    {
        var value = string.Concat(next);
        _editSource = source;
        try
        {
            SetCurrentValue(ValueProperty, value);
        }
        finally
        {
            _editSource = OtpInputChangeSource.Programmatic;
        }
        // The slots always show the code afterwards, even when the edit left it unchanged (a rejected letter).
        ApplyValue(animate: true);
        if (Value.Length >= Length)
        {
            var code = Value;
            RaiseEvent(new OtpInputCompletedEventArgs(CompletedEvent, code));
            if (CompletedCommand is { } command && command.CanExecute(code))
            {
                command.Execute(code);
            }
        }
    }

    private void UpdateAt(int index, string raw, OtpInputChangeSource source)
    {
        var next = Characters();
        var clean = Sanitize(raw);
        clean = clean.Length > 0 ? clean[^1..] : string.Empty;
        next[index] = clean;
        Commit(next, source);
        if (clean.Length > 0 && index < Length - 1)
        {
            FocusSlot(index + 1);
        }
    }

    private void HandleChange(int index, string raw, OtpInputChangeSource source)
    {
        var clean = Sanitize(raw);
        if (clean.Length <= 1)
        {
            UpdateAt(index, clean, source);
            return;
        }
        var next = Characters();
        var count = Math.Min(clean.Length, Length - index);
        for (var offset = 0; offset < count; offset++)
        {
            next[index + offset] = clean[offset].ToString();
        }
        Commit(next, source);
        FocusSlot(Math.Min(index + clean.Length, Length - 1));
    }

    /// <summary>Spreads <paramref name="text"/> over the slots from <paramref name="index"/> (Arc's handlePaste).</summary>
    internal void ApplyPaste(int index, string text)
    {
        if (IsReadOnly || index < 0 || index >= Length)
        {
            return;
        }
        var pasted = Sanitize(text);
        if (pasted.Length > Length - index)
        {
            pasted = pasted[..(Length - index)];
        }
        if (pasted.Length == 0)
        {
            return;
        }
        var next = Characters();
        for (var offset = 0; offset < pasted.Length; offset++)
        {
            next[index + offset] = pasted[offset].ToString();
        }
        Commit(next, OtpInputChangeSource.Paste);
        FocusSlot(Math.Min(index + pasted.Length, Length - 1));
    }

    internal void OnSlotTextInput(OtpInputSlot slot, string text)
    {
        if (IsReadOnly)
        {
            return;
        }
        var index = slot.Index;
        var current = Characters()[index];
        if (current.Length > 0 && !slot.HasSelection && text.Length == 1)
        {
            // maxLength=1: a filled slot takes no more typing until its character is selected or deleted.
            return;
        }
        HandleChange(index, text, OtpInputChangeSource.Typing);
    }

    internal bool OnSlotKeyDown(OtpInputSlot slot, KeyEventArgs e)
    {
        var index = slot.Index;
        switch (e.Key)
        {
            case Key.Left:
                FocusSlot(index - 1);
                return true;
            case Key.Right:
                FocusSlot(index + 1);
                return true;
            case Key.Back:
            {
                if (IsReadOnly)
                {
                    return true;
                }
                var characters = Characters();
                if (characters[index].Length == 0)
                {
                    if (index > 0)
                    {
                        characters[index - 1] = string.Empty;
                        Commit(characters, OtpInputChangeSource.Deletion);
                        FocusSlot(index - 1);
                    }
                }
                else if (slot.HasSelection || slot.CaretIndex > 0)
                {
                    // The text box's own Backspace: it deletes the character before the caret (none when the caret is at 0).
                    UpdateAt(index, string.Empty, OtpInputChangeSource.Deletion);
                }
                return true;
            }
            case Key.Delete:
            {
                if (IsReadOnly)
                {
                    return true;
                }
                var characters = Characters();
                if (characters[index].Length > 0)
                {
                    characters[index] = string.Empty;
                    Commit(characters, OtpInputChangeSource.Deletion);
                }
                return true;
            }
            default:
                return false;
        }
    }

    internal async void OnSlotPaste(OtpInputSlot slot)
    {
        if (IsReadOnly || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        var index = slot.Index;
        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch (Exception)
        {
            text = null;
        }
        if (!string.IsNullOrEmpty(text))
        {
            ApplyPaste(index, text);
        }
    }

    internal void OnSlotEdited(OtpInputSlot slot, string text)
    {
        // The text box changed its text itself (cut, an input method): apply it as Arc's onChange would, once the text
        // box has finished its own update, then write the code back to every slot.
        Dispatcher.UIThread.Post(() =>
        {
            if (!_slots.Contains(slot))
            {
                return;
            }
            if (!IsReadOnly)
            {
                HandleChange(slot.Index, text, text.Length == 0 ? OtpInputChangeSource.Deletion : OtpInputChangeSource.Typing);
            }
            ApplyValue(animate: true);
        });
    }

    internal void OnSlotGotFocus(OtpInputSlot slot)
    {
        if (_overlay is null || _row is null)
        {
            return;
        }
        // One ring for the whole row: it glides between slots while focus stays inside, and appears where focus lands
        // when it arrives from outside.
        _overlay.ShowRing(_row.SlotRect(slot.Index), glide: _overlay.IsRingShown);
    }

    internal void OnSlotLostFocus(OtpInputSlot slot, IInputElement? next)
    {
        if (next is OtpInputSlot other && other.Owner == this)
        {
            return;
        }
        _overlay?.HideRing();
    }
}

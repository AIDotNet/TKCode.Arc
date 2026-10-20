using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Arc shortcut recorder: a field that records a keyboard shortcut. Press it (Enter, Space or a click), then press the
/// combination: held modifiers light up as key caps while the chord is built, the finished shortcut settles into place
/// (caps pop in with a 35ms stagger), and a combination that is already taken (<see cref="Bindings"/>, or one the
/// browser or system keeps such as ⌘W and ⌘C) asks before it is used. Escape cancels, Backspace / Delete clears, Tab
/// stops recording and moves on, and the reset button restores <see cref="ResetValue"/>. Caps follow the platform:
/// ⌘ ⇧ glyphs on a Mac, Ctrl+Shift names elsewhere.
/// </summary>
/// <remarks>
/// <para>Keys are read from the physical key, so ⌥K records Alt+K (not "˚") and numpad digits record as digits. While
/// recording, every key down on the recorder is handled in the tunnel phase, so window key bindings and handlers do not
/// fire for the chord. Chords the operating system keeps (⌘Tab, Alt+Tab, Ctrl+Alt+Delete, most Windows-key
/// combinations, the macOS screenshot keys) never reach the application and cannot be recorded.</para>
/// <para>Assistive technology sees a toggle button (pressed while recording) named "&lt;Label&gt; &lt;spoken shortcut&gt;"
/// ("Command Shift K"), the message row as its help text, and Arc's polite announcements. The caps are drawn and hidden
/// from automation.</para>
/// </remarks>
[TemplatePart(FramePart, typeof(FieldFrame))]
[TemplatePart(RecorderPart, typeof(ShortcutRecorderButton))]
[TemplatePart(ResetButtonPart, typeof(ShortcutRecorderActionButton))]
[TemplatePart(ClearButtonPart, typeof(ShortcutRecorderActionButton))]
[TemplatePart(MessagePart, typeof(ShortcutRecorderMessageSlot))]
[TemplatePart(LiveRegionPart, typeof(SpecialInputsLiveRegion))]
[PseudoClasses(RecordingPseudoClass, ConflictPseudoClass, RecorderFocusVisiblePseudoClass, HoverPseudoClass)]
public class ShortcutRecorder : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string RecorderPart = "PART_Recorder";
    public const string ResetButtonPart = "PART_ResetButton";
    public const string ClearButtonPart = "PART_ClearButton";
    public const string MessagePart = "PART_Message";
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>Set while recording (Accent border, tinted field).</summary>
    public const string RecordingPseudoClass = ":recording";

    /// <summary>Set while a conflict waits for "Use anyway" or "Cancel" (Warning border).</summary>
    public const string ConflictPseudoClass = ":conflict";

    /// <summary>Set while the recorder has keyboard focus (the field border answers with Foreground).</summary>
    public const string RecorderFocusVisiblePseudoClass = ":recorder-focus-visible";

    /// <summary>Set while a mouse or pen (not touch) is over the field frame; also used by the internal buttons.</summary>
    public const string HoverPseudoClass = ":hover";

    #region Properties

    /// <summary>The recorded shortcut (two-way); null means none.</summary>
    public static readonly StyledProperty<KeyGesture?> ValueProperty =
        AvaloniaProperty.Register<ShortcutRecorder, KeyGesture?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the reset button restores (Arc: <c>resetValue</c>, defaulting to <c>defaultValue</c>).</summary>
    public static readonly StyledProperty<KeyGesture?> ResetValueProperty =
        AvaloniaProperty.Register<ShortcutRecorder, KeyGesture?>(nameof(ResetValue));

    /// <summary>Shortcuts already in use.</summary>
    public static readonly DirectProperty<ShortcutRecorder, IList<ShortcutBinding>> BindingsProperty =
        AvaloniaProperty.RegisterDirect<ShortcutRecorder, IList<ShortcutBinding>>(nameof(Bindings), o => o.Bindings, (o, v) => o.Bindings = v);

    /// <summary>Also warn about combinations the browser or system keeps (Arc: <c>warnReserved</c>, on by default).</summary>
    public static readonly StyledProperty<bool> IsReservedWarningEnabledProperty =
        AvaloniaProperty.Register<ShortcutRecorder, bool>(nameof(IsReservedWarningEnabled), true);

    /// <summary>Require ⌘, Ctrl or ⌥ / Alt; function keys are always allowed (Arc: <c>requireModifier</c>, on by default).</summary>
    public static readonly StyledProperty<bool> IsModifierRequiredProperty =
        AvaloniaProperty.Register<ShortcutRecorder, bool>(nameof(IsModifierRequired), true);

    /// <summary>Key cap conventions; null detects the platform (Mac on macOS and iOS).</summary>
    public static readonly StyledProperty<ShortcutPlatform?> PlatformProperty =
        AvaloniaProperty.Register<ShortcutRecorder, ShortcutPlatform?>(nameof(Platform));

    /// <summary>Shown while there is no shortcut.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(Placeholder), "Record shortcut");

    /// <summary>Shown while recording, before a key is held.</summary>
    public static readonly StyledProperty<string> RecordingPlaceholderProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(RecordingPlaceholder), "Press keys");

    /// <summary>The message under the field while recording.</summary>
    public static readonly StyledProperty<string> RecordingHintProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(RecordingHint), "Esc to cancel, Backspace to clear");

    /// <summary>The conflict action that takes the shortcut.</summary>
    public static readonly StyledProperty<string> UseAnywayTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(UseAnywayText), "Use anyway");

    /// <summary>The conflict action that keeps the previous shortcut.</summary>
    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(CancelText), "Cancel");

    /// <summary>Accessible name of the clear button.</summary>
    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string>(nameof(ClearText), "Clear shortcut");

    /// <summary>The field label (Arc: required); also the start of the recorder's accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string?>(nameof(Label));

    /// <summary>When false the label is only read by assistive technology (Arc: <c>hideLabel</c>).</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        AvaloniaProperty.Register<ShortcutRecorder, bool>(nameof(IsLabelVisible), true);

    /// <summary>Helper copy under the field, shown while no other message is.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ShortcutRecorder, string?>(nameof(Description));

    /// <summary>True while the recorder listens for a combination.</summary>
    public static readonly DirectProperty<ShortcutRecorder, bool> IsRecordingProperty =
        AvaloniaProperty.RegisterDirect<ShortcutRecorder, bool>(nameof(IsRecording), o => o.IsRecording);

    /// <summary>Raised when <see cref="Value"/> changes (recorded, cleared, reset, or set by code).</summary>
    public static readonly RoutedEvent<ShortcutRecorderValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ShortcutRecorder, ShortcutRecorderValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>The recorded shortcut (two-way); null means none.</summary>
    public KeyGesture? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>
    /// What the reset button restores. Arc defaults it to the uncontrolled <c>defaultValue</c>; Avalonia has no such
    /// value, so it stays null unless set: set it to the app's default shortcut. The reset button shows whenever
    /// <see cref="Value"/> differs from it (null included) and while a conflict is pending.
    /// </summary>
    public KeyGesture? ResetValue { get => GetValue(ResetValueProperty); set => SetValue(ResetValueProperty, value); }

    /// <summary>Shortcuts already in use (checked before the reserved list). Recording one of them asks before taking it.</summary>
    public IList<ShortcutBinding> Bindings
    {
        get => _bindings;
        set => SetAndRaise(BindingsProperty, ref _bindings, value ?? new AvaloniaList<ShortcutBinding>());
    }

    /// <summary>Warn about ⌘W / Ctrl+W, ⌘C, ⌘V and the other combinations browsers and systems keep.</summary>
    public bool IsReservedWarningEnabled { get => GetValue(IsReservedWarningEnabledProperty); set => SetValue(IsReservedWarningEnabledProperty, value); }

    /// <summary>Require ⌘, Ctrl or ⌥ / Alt (function keys are always allowed).</summary>
    public bool IsModifierRequired { get => GetValue(IsModifierRequiredProperty); set => SetValue(IsModifierRequiredProperty, value); }

    /// <summary>Key cap conventions; null detects the platform.</summary>
    public ShortcutPlatform? Platform { get => GetValue(PlatformProperty); set => SetValue(PlatformProperty, value); }

    /// <summary>Shown while there is no shortcut ("Record shortcut").</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Shown while recording, before a key is held ("Press keys").</summary>
    public string RecordingPlaceholder { get => GetValue(RecordingPlaceholderProperty); set => SetValue(RecordingPlaceholderProperty, value); }

    /// <summary>The message while recording ("Esc to cancel, Backspace to clear").</summary>
    public string RecordingHint { get => GetValue(RecordingHintProperty); set => SetValue(RecordingHintProperty, value); }

    /// <summary>"Use anyway".</summary>
    public string UseAnywayText { get => GetValue(UseAnywayTextProperty); set => SetValue(UseAnywayTextProperty, value); }

    /// <summary>"Cancel".</summary>
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>"Clear shortcut".</summary>
    public string ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    /// <summary>The field label.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Whether the label is shown.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Helper copy under the field.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>True while the recorder listens for a combination (see <see cref="StartRecording"/>).</summary>
    public bool IsRecording => _recording;

    /// <summary>The platform in effect: <see cref="Platform"/>, or the detected one.</summary>
    public ShortcutPlatform EffectivePlatform => Platform ?? ShortcutFormatter.CurrentPlatform;

    /// <summary>Raised when <see cref="Value"/> changes, with the old and new shortcut and the binding it was taken from.</summary>
    public event EventHandler<ShortcutRecorderValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    private sealed record Pending(KeyGesture Gesture, ShortcutBinding Conflict, bool Reserved);

    private const string RecordingAnnouncement = "Recording. Press the new shortcut, or Escape to cancel.";
    private const string CanceledAnnouncement = "Recording canceled";
    private const string ClearedAnnouncement = "Shortcut cleared";
    private const string KeptAnnouncement = "Kept the previous shortcut";
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(700);
    private static IReadOnlyList<ShortcutBinding>? s_reservedMac;
    private static IReadOnlyList<ShortcutBinding>? s_reservedOther;

    private IList<ShortcutBinding> _bindings = new AvaloniaList<ShortcutBinding>();
    private bool _recording;
    private KeyModifiers _live;
    private Pending? _pending;
    private string? _nudge;
    private bool _settled;
    private DispatcherTimer? _settleTimer;
    private ShortcutBinding? _replacing;
    private int _batch;
    private WindowBase? _window;

    private FieldFrame? _frame;
    private ShortcutRecorderButton? _recorder;
    private ShortcutRecorderActionButton? _reset;
    private ShortcutRecorderActionButton? _clear;
    private ShortcutRecorderMessageSlot? _message;
    private SpecialInputsLiveRegion? _liveRegion;

    /// <summary>The last announcement made through the live region (for tests and diagnostics).</summary>
    internal string LastAnnouncement { get; private set; } = string.Empty;

    /// <summary>The message currently shown under the field (null: none), for tests and diagnostics.</summary>
    internal string? MessageText => _message?.Current?.PlainText;

    /// <summary>Starts recording and focuses the recorder (Arc: pressing the recorder).</summary>
    public void StartRecording()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        if (_recorder is { IsFocused: false } recorder)
        {
            recorder.Focus();
        }
        Start();
    }

    /// <summary>Stops recording without recording anything and without an announcement.</summary>
    public void StopRecording()
    {
        if (!_recording)
        {
            return;
        }
        Stop();
        Refresh();
    }

    // ---- template ------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_recorder is not null)
        {
            _recorder.Click -= OnRecorderClick;
            _recorder.Owner = null;
        }
        if (_reset is not null)
        {
            _reset.Click -= OnResetClick;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_message is not null)
        {
            _message.UseAnyway -= OnUseAnyway;
            _message.Cancel -= OnCancel;
        }
        if (_frame is not null)
        {
            _frame.PointerEntered -= OnFramePointerEntered;
            _frame.PointerExited -= OnFramePointerExited;
        }

        _frame = e.NameScope.Find<FieldFrame>(FramePart);
        _recorder = e.NameScope.Find<ShortcutRecorderButton>(RecorderPart);
        _reset = e.NameScope.Find<ShortcutRecorderActionButton>(ResetButtonPart);
        _clear = e.NameScope.Find<ShortcutRecorderActionButton>(ClearButtonPart);
        _message = e.NameScope.Find<ShortcutRecorderMessageSlot>(MessagePart);
        _liveRegion = e.NameScope.Find<SpecialInputsLiveRegion>(LiveRegionPart);

        if (_recorder is not null)
        {
            _recorder.Owner = this;
            _recorder.Click += OnRecorderClick;
        }
        if (_reset is not null)
        {
            _reset.Glyph = ArcIcons.RotateCcw;
            _reset.Click += OnResetClick;
        }
        if (_clear is not null)
        {
            _clear.Glyph = ArcIcons.X;
            _clear.Click += OnClearClick;
        }
        if (_message is not null)
        {
            _message.UseAnyway += OnUseAnyway;
            _message.Cancel += OnCancel;
        }
        if (_frame is not null)
        {
            _frame.PointerEntered += OnFramePointerEntered;
            _frame.PointerExited += OnFramePointerExited;
        }
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty)
        {
            RaiseEvent(new ShortcutRecorderValueChangedEventArgs(ValueChangedEvent, change.GetOldValue<KeyGesture?>(),
                change.GetNewValue<KeyGesture?>(), _replacing));
            Refresh();
        }
        else if (property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled && _recording)
            {
                Stop();
            }
            Refresh();
        }
        else if (property == ResetValueProperty || property == PlatformProperty || property == PlaceholderProperty
                 || property == RecordingPlaceholderProperty || property == RecordingHintProperty || property == UseAnywayTextProperty
                 || property == CancelTextProperty || property == ClearTextProperty || property == LabelProperty
                 || property == DescriptionProperty)
        {
            Refresh();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_recording)
        {
            Stop();
        }
        _settleTimer?.Stop();
        _settled = false;
        Refresh();
    }

    // ---- state machine (Arc's start / stop / commit) -----------------------------------------------------------------------

    private void Start()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        _pending = null;
        _nudge = null;
        _live = KeyModifiers.None;
        SetRecording(true);
        Announce(RecordingAnnouncement);
        Refresh();
    }

    private void Stop()
    {
        SetRecording(false);
        _live = KeyModifiers.None;
    }

    private void SetRecording(bool recording)
    {
        if (_recording == recording)
        {
            return;
        }
        SetAndRaise(IsRecordingProperty, ref _recording, recording);
        if (recording)
        {
            // A window that loses activation blurs the recorder in a browser; do the same.
            _window = TopLevel.GetTopLevel(this) as WindowBase;
            if (_window is not null)
            {
                _window.Deactivated += OnWindowDeactivated;
            }
        }
        else if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    private void Commit(KeyGesture? next, ShortcutBinding? replaced)
    {
        _batch++;
        try
        {
            _pending = null;
            _nudge = null;
            _replacing = replaced;
            try
            {
                SetCurrentValue(ValueProperty, next);
            }
            finally
            {
                _replacing = null;
            }
            Settle();
            Announce(next is not null ? $"Shortcut set to {Spoken(next)}" : ClearedAnnouncement);
        }
        finally
        {
            _batch--;
        }
        Refresh();
    }

    /// <summary>Caps stay pressed for 700ms after a commit (when nothing is pending).</summary>
    private void Settle()
    {
        _settled = true;
        if (_settleTimer is null)
        {
            _settleTimer = new DispatcherTimer { Interval = s_settle };
            _settleTimer.Tick += (_, _) =>
            {
                _settleTimer!.Stop();
                _settled = false;
                Refresh();
            };
        }
        _settleTimer.Stop();
        _settleTimer.Start();
    }

    private Pending? ConflictFor(KeyGesture gesture)
    {
        if (Value is { } current && ShortcutFormatter.AreEquivalent(current, gesture))
        {
            return null;
        }
        foreach (var binding in _bindings)
        {
            if (binding.Gesture is { } taken && ShortcutFormatter.AreEquivalent(taken, gesture))
            {
                return new Pending(gesture, binding, false);
            }
        }
        if (IsReservedWarningEnabled)
        {
            foreach (var reserved in ReservedBindings(EffectivePlatform))
            {
                if (ShortcutFormatter.AreEquivalent(reserved.Gesture, gesture))
                {
                    return new Pending(gesture, reserved, true);
                }
            }
        }
        return null;
    }

    /// <summary>Arc's reserved list: combinations browsers and systems keep, with "mod" resolved for <paramref name="platform"/>.</summary>
    internal static IReadOnlyList<ShortcutBinding> ReservedBindings(ShortcutPlatform platform)
    {
        ref var cache = ref platform == ShortcutPlatform.Mac ? ref s_reservedMac : ref s_reservedOther;
        if (cache is not null)
        {
            return cache;
        }
        var mod = ShortcutFormatter.CommandModifier(platform);
        ShortcutBinding B(Key key, string label, KeyModifiers extra = KeyModifiers.None) => new(new KeyGesture(key, mod | extra), label);
        cache = new[]
        {
            B(Key.W, "Close tab"), B(Key.T, "New tab"), B(Key.N, "New window"), B(Key.Q, "Quit"),
            B(Key.L, "Address bar"), B(Key.R, "Reload"), B(Key.T, "Reopen tab", KeyModifiers.Shift),
            B(Key.C, "Copy"), B(Key.V, "Paste"), B(Key.X, "Cut"), B(Key.Z, "Undo"), B(Key.A, "Select all")
        };
        return cache;
    }

    private string Spoken(KeyGesture gesture) => ShortcutFormatter.Format(gesture, EffectivePlatform).Spoken;

    private void Announce(string message)
    {
        // Arc's live region only speaks when its text changes (a repeated nudge is not read twice).
        if (message == LastAnnouncement)
        {
            return;
        }
        LastAnnouncement = message;
        _liveRegion?.Announce(message);
    }

    // ---- keyboard ------------------------------------------------------------------------------------------------------------

    internal void OnRecorderKeyDown(KeyEventArgs e)
    {
        if (!_recording)
        {
            if ((e.Key == Key.Back || e.Key == Key.Delete) && Value is not null && IsEffectivelyEnabled)
            {
                e.Handled = true;
                Commit(null, null);
            }
            return;
        }
        var modifiers = e.KeyModifiers & ShortcutFormatter.AllModifiers;
        if (e.Key == Key.Tab && modifiers == KeyModifiers.None)
        {
            // Tab is never recorded: recording stops and focus moves on.
            Stop();
            Refresh();
            return;
        }
        // The chord belongs to the recorder: keep it from the window's own shortcuts.
        e.Handled = true;
        var bare = modifiers == KeyModifiers.None;
        if (bare && e.Key == Key.Escape)
        {
            Stop();
            Announce(CanceledAnnouncement);
            Refresh();
            return;
        }
        if (bare && (e.Key == Key.Back || e.Key == Key.Delete))
        {
            _batch++;
            try
            {
                Stop();
                Commit(null, null);
            }
            finally
            {
                _batch--;
            }
            Refresh();
            return;
        }
        var gesture = ShortcutFormatter.FromKeyEvent(e);
        if (gesture is null)
        {
            _live = LiveModifiers(e, down: true);
            Refresh();
            return;
        }
        var strong = (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        var functionKey = gesture.Key is >= Key.F1 and <= Key.F24;
        if (IsModifierRequired && !strong && !functionKey)
        {
            var hint = EffectivePlatform == ShortcutPlatform.Mac ? "Include ⌘, ⌃, or ⌥" : "Include Ctrl or Alt";
            _nudge = hint;
            Announce(hint);
            _live = LiveModifiers(e, down: true);
            Refresh();
            return;
        }
        _batch++;
        try
        {
            Stop();
            var conflict = ConflictFor(gesture);
            if (conflict is not null)
            {
                _pending = conflict;
                Announce($"{Spoken(gesture)} is {(conflict.Reserved ? "reserved for" : "used by")} {conflict.Conflict.Label}. Use anyway, or record another.");
            }
            else
            {
                Commit(gesture, null);
            }
        }
        finally
        {
            _batch--;
        }
        Refresh();
    }

    internal void OnRecorderKeyUp(KeyEventArgs e)
    {
        if (_recording)
        {
            _live = LiveModifiers(e, down: false);
            Refresh();
        }
    }

    /// <summary>The modifiers held after this event (the event's own modifier key counted as down or up).</summary>
    private static KeyModifiers LiveModifiers(KeyEventArgs e, bool down)
    {
        var modifiers = e.KeyModifiers & ShortcutFormatter.AllModifiers;
        var own = ShortcutFormatter.ModifierOf(e);
        return down ? modifiers | own : modifiers & ~own;
    }

    // ---- pointer, focus, automation -------------------------------------------------------------------------------------------

    private void OnRecorderClick(object? sender, RoutedEventArgs e)
    {
        if (_recording)
        {
            // Keys are intercepted while recording, so this is a pointer (or assistive technology) press: cancel.
            Stop();
            Announce(CanceledAnnouncement);
            Refresh();
            return;
        }
        Start();
    }

    internal void ToggleFromAutomation() => OnRecorderClick(this, new RoutedEventArgs());

    internal void OnRecorderFocusChanged()
    {
        var visible = _recorder is { IsFocused: true } recorder && recorder.Classes.Contains(":focus-visible");
        PseudoClasses.Set(RecorderFocusVisiblePseudoClass, visible);
    }

    internal void OnRecorderLostFocus()
    {
        if (_recording)
        {
            Stop();
            _nudge = null;
            Refresh();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => OnRecorderLostFocus();

    // Arc's idle hover only answers fine pointers: touch never darkens the border.
    private void OnFramePointerEntered(object? sender, PointerEventArgs e) =>
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);

    private void OnFramePointerExited(object? sender, PointerEventArgs e) => PseudoClasses.Set(HoverPseudoClass, false);

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Commit(ResetValue, null);
        FocusRecorder(_reset?.ActivatedByKeyboard == true);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Commit(null, null);
        FocusRecorder(_clear?.ActivatedByKeyboard == true);
    }

    private void OnUseAnyway(object? sender, bool keyboard)
    {
        if (_pending is not { } taken)
        {
            return;
        }
        Commit(taken.Gesture, taken.Reserved ? null : taken.Conflict);
        FocusRecorder(keyboard);
    }

    private void OnCancel(object? sender, bool keyboard)
    {
        if (_pending is null)
        {
            return;
        }
        _pending = null;
        Announce(KeptAnnouncement);
        Refresh();
        FocusRecorder(keyboard);
    }

    private void FocusRecorder(bool keyboard)
    {
        // Keyboard users get the field's focus answer back, pointer users do not (focus-visible heuristics).
        _recorder?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        OnRecorderFocusChanged();
    }

    // ---- view ------------------------------------------------------------------------------------------------------------------

    private void Refresh()
    {
        if (_batch > 0)
        {
            return;
        }
        var platform = EffectivePlatform;
        var shown = _pending?.Gesture ?? Value;
        IReadOnlyList<ShortcutKeyCap> caps = _recording
            ? ShortcutFormatter.Tokens(_live, Key.None, platform)
            : shown is not null ? ShortcutFormatter.Tokens(shown, platform) : Array.Empty<ShortcutKeyCap>();
        var pressed = _recording || (_settled && _pending is null);
        var message = BuildMessage();

        if (_recorder is not null)
        {
            _recorder.Present(new ShortcutRecorderView(caps, _recording, pressed, _pending is not null, Placeholder ?? string.Empty,
                RecordingPlaceholder ?? string.Empty));
            _recorder.IsOn = _recording;
            var state = _recording ? "Recording" : shown is not null ? Spoken(shown) : Placeholder;
            AutomationProperties.SetName(_recorder, string.IsNullOrEmpty(Label) ? state : $"{Label} {state}");
            AutomationProperties.SetHelpText(_recorder, message?.PlainText ?? string.Empty);
        }
        var canReset = !_recording && (_pending is not null || !ShortcutFormatter.AreEquivalent(Value, ResetValue));
        var canClear = !_recording && Value is not null && _pending is null;
        if (_reset is not null)
        {
            _reset.IsShown = canReset;
            AutomationProperties.SetName(_reset, $"Reset to {(ResetValue is { } reset ? Spoken(reset) : "none")}");
        }
        if (_clear is not null)
        {
            _clear.IsShown = canClear;
            AutomationProperties.SetName(_clear, ClearText);
        }
        if (_message is not null)
        {
            _message.UseAnywayText = UseAnywayText;
            _message.CancelText = CancelText;
            _message.Show(message);
        }
        PseudoClasses.Set(RecordingPseudoClass, _recording);
        PseudoClasses.Set(ConflictPseudoClass, !_recording && _pending is not null);
    }

    /// <summary>Conflict (warning) &gt; nudge &gt; recording hint &gt; description.</summary>
    private ShortcutRecorderMessageModel? BuildMessage()
    {
        if (_pending is { } pending)
        {
            var text = ShortcutFormatter.Format(pending.Gesture, EffectivePlatform).Text;
            return new ShortcutRecorderMessageModel($"c-{text}", true, pending.Reserved ? "Reserved for" : "Already used by",
                pending.Conflict.Label, true);
        }
        if (_nudge is { } nudge)
        {
            return new ShortcutRecorderMessageModel($"n-{nudge}", false, nudge, null, false);
        }
        if (_recording)
        {
            return new ShortcutRecorderMessageModel("rec", false, RecordingHint ?? string.Empty, null, false);
        }
        if (!string.IsNullOrEmpty(Description))
        {
            return new ShortcutRecorderMessageModel("d", false, Description, null, false);
        }
        return null;
    }
}

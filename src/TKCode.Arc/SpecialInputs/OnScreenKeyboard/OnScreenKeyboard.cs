using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// An on-screen keyboard for touch screens without a physical keyboard (kiosks, touch panels, industrial HMIs). It shows
/// a built-in layout (<see cref="Preset"/>: QWERTY, a number pad or a PIN pad) or your own <see cref="Layout"/>, and types
/// into the text input that has focus in the same window (Arc <c>Input</c>, <c>Textarea</c>, <c>NumberField</c>, an
/// Avalonia <c>TextBox</c>...) or into <see cref="Target"/>. Text goes through Avalonia's text input event, so the text
/// box inserts it at the caret and replaces a selection; Backspace, Enter, Tab and the arrows are sent as key presses.
/// Keys never take focus. <see cref="ItemInvoked"/> is raised for every key and can be handled to take a key over.
/// </summary>
[TemplatePart(KeysPart, typeof(OnScreenKeyboardPanel))]
[TemplatePart(LivePart, typeof(SpecialInputsLiveRegion))]
[PseudoClasses(ShiftPseudoClass, CapsLockPseudoClass)]
public partial class OnScreenKeyboard : TemplatedControl
{
    public const string KeysPart = "PART_Keys";
    public const string LivePart = "PART_Live";
    public const string ShiftPseudoClass = ":shift";
    public const string CapsLockPseudoClass = ":capslock";

    private static readonly TimeSpan s_minimumRepeatInterval = TimeSpan.FromMilliseconds(10);

    #region 公共属性定义

    /// <summary>The built-in layout shown when <see cref="Layout"/> is null: QWERTY (default), a number pad (with or without a minus key) or a PIN pad.</summary>
    public static readonly StyledProperty<OnScreenKeyboardPreset> PresetProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, OnScreenKeyboardPreset>(nameof(Preset));

    /// <summary>Your own layout (pages of rows of keys); when set it replaces <see cref="Preset"/>. The keyboard follows changes made to it.</summary>
    public static readonly StyledProperty<OnScreenKeyboardLayout?> LayoutProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, OnScreenKeyboardLayout?>(nameof(Layout));

    /// <summary>The name of the page shown (letters, symbols...); null, or a name the layout does not have, shows its first page. Page keys change it.</summary>
    public static readonly DirectProperty<OnScreenKeyboard, string?> CurrentPageProperty =
        AvaloniaProperty.RegisterDirect<OnScreenKeyboard, string?>(nameof(CurrentPage), o => o.CurrentPage, (o, v) => o.CurrentPage = v,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shift: <c>Off</c>, <c>On</c> for the next key, or <c>Locked</c> (caps lock). The Shift key changes it: one tap turns it on, a double tap locks it.</summary>
    public static readonly DirectProperty<OnScreenKeyboard, OnScreenKeyboardShiftState> ShiftStateProperty =
        AvaloniaProperty.RegisterDirect<OnScreenKeyboard, OnScreenKeyboardShiftState>(nameof(ShiftState), o => o.ShiftState,
            (o, v) => o.ShiftState = v, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The element that receives the text and key presses; null sends them to the element that has focus in the keyboard's window.</summary>
    public static readonly StyledProperty<InputElement?> TargetProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, InputElement?>(nameof(Target));

    /// <summary>The culture used to put letters in upper case and for the number pads' decimal separator and minus sign; null uses the current culture.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, CultureInfo?>(nameof(Culture));

    /// <summary>How long a repeating key (Backspace, the arrows) is held before it starts to repeat.</summary>
    public static readonly StyledProperty<TimeSpan> RepeatDelayProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, TimeSpan>(nameof(RepeatDelay), TimeSpan.FromMilliseconds(500));

    /// <summary>The time between two repeats of a held key.</summary>
    public static readonly StyledProperty<TimeSpan> RepeatIntervalProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, TimeSpan>(nameof(RepeatInterval), TimeSpan.FromMilliseconds(50));

    /// <summary>The clock of key repeat and of the Shift double tap. Tests pass a clock they move by hand.</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>Announced to screen readers when Shift turns on for one key.</summary>
    public static readonly StyledProperty<string> ShiftOnTextProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, string>(nameof(ShiftOnText), "Shift on");

    /// <summary>Announced to screen readers when a double tap on Shift locks capitals.</summary>
    public static readonly StyledProperty<string> CapsLockOnTextProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, string>(nameof(CapsLockOnText), "Caps lock on");

    /// <summary>Announced to screen readers when the Shift key turns Shift or caps lock off.</summary>
    public static readonly StyledProperty<string> ShiftOffTextProperty =
        AvaloniaProperty.Register<OnScreenKeyboard, string>(nameof(ShiftOffText), "Shift off");

    /// <summary>
    /// Raised for every key press, and for every repeat of a held key, before the keyboard acts on it. Set
    /// <c>Handled</c> to true to take the key over: the keyboard then types nothing and leaves Shift and the page as they are.
    /// </summary>
    public static readonly RoutedEvent<OnScreenKeyboardItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<OnScreenKeyboard, OnScreenKeyboardItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a <c>Hide</c> key is pressed. A keyboard opened by <c>OnScreenKeyboard.Popup</c> closes; a docked keyboard leaves hiding to you.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> HideRequestedEvent =
        RoutedEvent.Register<OnScreenKeyboard, RoutedEventArgs>(nameof(HideRequested), RoutingStrategies.Bubble);

    #endregion

    private string? _currentPage;
    private OnScreenKeyboardShiftState _shiftState;
    private OnScreenKeyboardLayout? _builtIn;
    private OnScreenKeyboardLayout? _subscribed;
    private OnScreenKeyboardPanel? _panel;
    private SpecialInputsLiveRegion? _live;
    private readonly List<Press> _presses = new();
    private long? _lastShiftTap;
    private ITimer? _repeatTimer;
    private OnScreenKeyboardKeyPresenter? _repeatKey;
    private int _repeatGeneration;
    private int _repeatQueued;

    static OnScreenKeyboard()
    {
        FocusableProperty.OverrideDefaultValue<OnScreenKeyboard>(false);
        RegisterPopupHandlers();
    }

    public OnScreenKeyboard()
    {
        // Tunnel: the keyboard claims every press inside it before Avalonia's focus manager (which focuses the nearest
        // focusable ancestor of what was pressed: on press for a mouse, on release for touch) can move focus away from
        // the text box being typed into.
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnTunnelPointerReleased, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc cref="PresetProperty"/>
    public OnScreenKeyboardPreset Preset { get => GetValue(PresetProperty); set => SetValue(PresetProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public OnScreenKeyboardLayout? Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="CurrentPageProperty"/>
    public string? CurrentPage
    {
        get => _currentPage;
        set => SetAndRaise(CurrentPageProperty, ref _currentPage, value);
    }

    /// <inheritdoc cref="ShiftStateProperty"/>
    public OnScreenKeyboardShiftState ShiftState
    {
        get => _shiftState;
        set => SetAndRaise(ShiftStateProperty, ref _shiftState, value);
    }

    /// <inheritdoc cref="TargetProperty"/>
    public InputElement? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="RepeatDelayProperty"/>
    public TimeSpan RepeatDelay { get => GetValue(RepeatDelayProperty); set => SetValue(RepeatDelayProperty, value); }

    /// <inheritdoc cref="RepeatIntervalProperty"/>
    public TimeSpan RepeatInterval { get => GetValue(RepeatIntervalProperty); set => SetValue(RepeatIntervalProperty, value); }

    /// <inheritdoc cref="TimeProviderProperty"/>
    public TimeProvider TimeProvider { get => GetValue(TimeProviderProperty); set => SetValue(TimeProviderProperty, value); }

    /// <inheritdoc cref="ShiftOnTextProperty"/>
    public string ShiftOnText { get => GetValue(ShiftOnTextProperty); set => SetValue(ShiftOnTextProperty, value); }

    /// <inheritdoc cref="CapsLockOnTextProperty"/>
    public string CapsLockOnText { get => GetValue(CapsLockOnTextProperty); set => SetValue(CapsLockOnTextProperty, value); }

    /// <inheritdoc cref="ShiftOffTextProperty"/>
    public string ShiftOffText { get => GetValue(ShiftOffTextProperty); set => SetValue(ShiftOffTextProperty, value); }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<OnScreenKeyboardItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <inheritdoc cref="HideRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? HideRequested
    {
        add => AddHandler(HideRequestedEvent, value);
        remove => RemoveHandler(HideRequestedEvent, value);
    }

    /// <summary>The layout in use: <see cref="Layout"/>, or the built-in layout of <see cref="Preset"/>.</summary>
    internal OnScreenKeyboardLayout EffectiveLayout => Layout ?? (_builtIn ??= OnScreenKeyboardLayout.Create(Preset, Culture));

    /// <summary>The page shown.</summary>
    internal OnScreenKeyboardPage? ActivePage
    {
        get
        {
            var layout = EffectiveLayout;
            return layout.FindPage(CurrentPage) ?? (layout.Pages.Count > 0 ? layout.Pages[0] : null);
        }
    }

    /// <summary>The keys shown, in order (template parts).</summary>
    internal IReadOnlyList<OnScreenKeyboardKeyPresenter> KeyPresenters =>
        _panel?.Children.OfType<OnScreenKeyboardKeyPresenter>().ToArray() ?? Array.Empty<OnScreenKeyboardKeyPresenter>();

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>
    /// Presses <paramref name="key"/> as if it were tapped: raises <see cref="ItemInvoked"/>, then (unless it was handled)
    /// types its text, sends its key press, changes Shift or switches page. Does nothing for a disabled, hidden or spacer key.
    /// </summary>
    public void Invoke(OnScreenKeyboardKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        InvokeCore(key, isRepeat: false);
    }

    /// <summary>The text <paramref name="key"/> types with the current Shift state; null for keys that type no text.</summary>
    public string? GetOutputText(OnScreenKeyboardKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Kind switch
        {
            OnScreenKeyboardKeyKind.Text => ShiftedText(key),
            OnScreenKeyboardKeyKind.Space => " ",
            _ => null,
        };
    }

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        EndAllPresses();
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<OnScreenKeyboardPanel>(KeysPart);
        _live = e.NameScope.Find<SpecialInputsLiveRegion>(LivePart);
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeLayout();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EndAllPresses();
        StopRepeat();
        UnsubscribeLayout();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PresetProperty || change.Property == CultureProperty)
        {
            _builtIn = null;
            if (Layout is null)
            {
                SubscribeLayout();
                Rebuild();
            }
            else if (change.Property == CultureProperty)
            {
                RefreshKeys();
            }
        }
        else if (change.Property == LayoutProperty)
        {
            SubscribeLayout();
            Rebuild();
        }
        else if (change.Property == CurrentPageProperty)
        {
            Rebuild();
        }
        else if (change.Property == ShiftStateProperty)
        {
            PseudoClasses.Set(ShiftPseudoClass, ShiftState == OnScreenKeyboardShiftState.On);
            PseudoClasses.Set(CapsLockPseudoClass, ShiftState == OnScreenKeyboardShiftState.Locked);
            RefreshKeys();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            EndAllPresses();
        }
        else if (change.Property == RepeatDelayProperty || change.Property == RepeatIntervalProperty || change.Property == TimeProviderProperty)
        {
            StopRepeat();
        }
    }

    private void SubscribeLayout()
    {
        var layout = VisualRoot is null ? null : EffectiveLayout;
        if (ReferenceEquals(layout, _subscribed))
        {
            return;
        }
        UnsubscribeLayout();
        if (layout is not null)
        {
            layout.Changed += OnLayoutChanged;
            _subscribed = layout;
        }
    }

    private void UnsubscribeLayout()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnLayoutChanged;
            _subscribed = null;
        }
    }

    private void OnLayoutChanged(OnScreenKeyboardKey? key)
    {
        if (key is null || _panel is null)
        {
            Rebuild();
            return;
        }
        foreach (var presenter in KeyPresenters)
        {
            if (ReferenceEquals(presenter.Key, key))
            {
                ShowKey(presenter);
            }
        }
        _panel.InvalidateMeasure();
    }

    /// <summary>Shows the active page: reuses the key presenters in order and adds or removes only the difference.</summary>
    private void Rebuild()
    {
        if (_panel is null)
        {
            return;
        }
        EndAllPresses();
        StopRepeat();
        var children = _panel.Children;
        var index = 0;
        if (ActivePage is { } page)
        {
            for (var r = 0; r < page.Rows.Count; r++)
            {
                if (page.Rows[r] is not { } row)
                {
                    continue;
                }
                foreach (var key in row.Keys)
                {
                    if (key is null)
                    {
                        continue;
                    }
                    OnScreenKeyboardKeyPresenter presenter;
                    if (index < children.Count)
                    {
                        presenter = (OnScreenKeyboardKeyPresenter)children[index];
                    }
                    else
                    {
                        presenter = new OnScreenKeyboardKeyPresenter(this);
                        children.Add(presenter);
                    }
                    presenter.Bind(key, r);
                    ShowKey(presenter);
                    index++;
                }
            }
        }
        if (children.Count > index)
        {
            children.RemoveRange(index, children.Count - index);
        }
        _panel.InvalidateMeasure();
    }

    /// <summary>Updates every key's label and state (after Shift or the culture changed).</summary>
    private void RefreshKeys()
    {
        if (_panel is null)
        {
            return;
        }
        foreach (var child in _panel.Children)
        {
            if (child is OnScreenKeyboardKeyPresenter presenter)
            {
                ShowKey(presenter);
            }
        }
    }

    private void ShowKey(OnScreenKeyboardKeyPresenter presenter)
    {
        if (presenter.Key is not { } key)
        {
            return;
        }
        var kind = key.Kind;
        var shift = ShiftState;
        var glyph = key.Glyph;
        var filled = false;
        if (glyph is null && key.Label is null)
        {
            glyph = OnScreenKeyboardGlyphs.For(kind, shift);
        }
        if (kind == OnScreenKeyboardKeyKind.Shift)
        {
            filled = shift != OnScreenKeyboardShiftState.Off;
        }
        var label = glyph is null ? key.Label ?? DefaultLabel(key) : null;
        var name = key.AutomationName ?? DefaultName(kind) ?? label ?? key.Text ?? kind.ToString();
        var isFunction = kind is OnScreenKeyboardKeyKind.Shift or OnScreenKeyboardKeyKind.Backspace or OnScreenKeyboardKeyKind.Tab
            or OnScreenKeyboardKeyKind.Left or OnScreenKeyboardKeyKind.Right or OnScreenKeyboardKeyKind.Up or OnScreenKeyboardKeyKind.Down
            or OnScreenKeyboardKeyKind.Page or OnScreenKeyboardKeyKind.Hide or OnScreenKeyboardKeyKind.Key or OnScreenKeyboardKeyKind.Custom;
        var isShift = kind == OnScreenKeyboardKeyKind.Shift;
        presenter.Show(label, glyph, filled, name, isFunction, kind == OnScreenKeyboardKeyKind.Enter,
            isShift && shift != OnScreenKeyboardShiftState.Off, isShift && shift == OnScreenKeyboardShiftState.Locked);
        if (presenter.IsPressed && !presenter.IsEffectivelyEnabled)
        {
            EndPress(presenter);
        }
    }

    private string? DefaultLabel(OnScreenKeyboardKey key) => key.Kind switch
    {
        OnScreenKeyboardKeyKind.Text => ShiftedText(key),
        OnScreenKeyboardKeyKind.Page => key.TargetPage,
        OnScreenKeyboardKeyKind.Custom => key.Text,
        OnScreenKeyboardKeyKind.Key => key.KeyCode == Key.None ? null : key.KeyCode.ToString(),
        _ => null,
    };

    private static string? DefaultName(OnScreenKeyboardKeyKind kind) => kind switch
    {
        OnScreenKeyboardKeyKind.Space => "Space",
        OnScreenKeyboardKeyKind.Backspace => "Backspace",
        OnScreenKeyboardKeyKind.Enter => "Enter",
        OnScreenKeyboardKeyKind.Tab => "Tab",
        OnScreenKeyboardKeyKind.Shift => "Shift",
        OnScreenKeyboardKeyKind.Left => "Left",
        OnScreenKeyboardKeyKind.Right => "Right",
        OnScreenKeyboardKeyKind.Up => "Up",
        OnScreenKeyboardKeyKind.Down => "Down",
        OnScreenKeyboardKeyKind.Hide => "Hide keyboard",
        _ => null,
    };

    #endregion

    #region Output

    private string? ShiftedText(OnScreenKeyboardKey key)
    {
        var text = key.Text;
        var shifted = ShiftState switch
        {
            OnScreenKeyboardShiftState.On => true,
            // Caps lock, like a physical one, capitalises letters only: digits and symbols keep their own character.
            OnScreenKeyboardShiftState.Locked => text is not null && IsLetters(text),
            _ => false,
        };
        if (!shifted)
        {
            return text;
        }
        return key.ShiftText ?? text?.ToUpper(EffectiveCulture);
    }

    private static bool IsLetters(string text)
    {
        var any = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                any = true;
                continue;
            }
            var category = Rune.GetUnicodeCategory(rune);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
            {
                return false;
            }
        }
        return any;
    }

    /// <summary>The element that receives what the keys send: <see cref="Target"/>, or the focused element of the keyboard's window.</summary>
    internal InputElement? ResolveTarget()
    {
        if (Target is { } target)
        {
            return target;
        }
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is InputElement focused &&
            !ReferenceEquals(focused, this) && !this.IsVisualAncestorOf(focused))
        {
            return focused;
        }
        return null;
    }

    private void InvokeCore(OnScreenKeyboardKey key, bool isRepeat)
    {
        if (!IsEffectivelyEnabled || !key.IsEnabled || !key.IsVisible || key.Kind == OnScreenKeyboardKeyKind.Spacer)
        {
            return;
        }
        var text = GetOutputText(key);
        var target = ResolveTarget();
        var args = new OnScreenKeyboardItemInvokedEventArgs(ItemInvokedEvent, key, text, target, isRepeat);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        var shiftModifier = ShiftState == OnScreenKeyboardShiftState.On ? KeyModifiers.Shift : KeyModifiers.None;
        switch (key.Kind)
        {
            case OnScreenKeyboardKeyKind.Text:
            case OnScreenKeyboardKeyKind.Space:
                if (target is not null && !string.IsNullOrEmpty(text))
                {
                    target.RaiseEvent(new TextInputEventArgs { RoutedEvent = TextInputEvent, Text = text });
                }
                break;
            case OnScreenKeyboardKeyKind.Backspace:
                SendKey(target, Key.Back, PhysicalKey.Backspace, KeyModifiers.None);
                break;
            case OnScreenKeyboardKeyKind.Enter:
                SendKey(target, Key.Enter, PhysicalKey.Enter, KeyModifiers.None);
                break;
            case OnScreenKeyboardKeyKind.Tab:
                SendKey(target, Key.Tab, PhysicalKey.Tab, shiftModifier);
                break;
            case OnScreenKeyboardKeyKind.Left:
                SendKey(target, Key.Left, PhysicalKey.ArrowLeft, shiftModifier);
                break;
            case OnScreenKeyboardKeyKind.Right:
                SendKey(target, Key.Right, PhysicalKey.ArrowRight, shiftModifier);
                break;
            case OnScreenKeyboardKeyKind.Up:
                SendKey(target, Key.Up, PhysicalKey.ArrowUp, shiftModifier);
                break;
            case OnScreenKeyboardKeyKind.Down:
                SendKey(target, Key.Down, PhysicalKey.ArrowDown, shiftModifier);
                break;
            case OnScreenKeyboardKeyKind.Key:
                if (key.KeyCode != Key.None)
                {
                    SendKey(target, key.KeyCode, PhysicalKey.None, shiftModifier);
                }
                break;
            case OnScreenKeyboardKeyKind.Shift:
                TapShift();
                return;
            case OnScreenKeyboardKeyKind.Page:
                SwitchPage(key.TargetPage);
                return;
            case OnScreenKeyboardKeyKind.Hide:
                RaiseEvent(new RoutedEventArgs(HideRequestedEvent));
                return;
        }
        // Shift for one key: the key that used it turns it off (caps lock stays).
        if (ShiftState == OnScreenKeyboardShiftState.On)
        {
            ShiftState = OnScreenKeyboardShiftState.Off;
        }
    }

    private static void SendKey(InputElement? target, Key key, PhysicalKey physicalKey, KeyModifiers modifiers)
    {
        if (target is null)
        {
            return;
        }
        // The same events a physical key raises on the focused element: the text box handles editing keys itself, and
        // what it leaves unhandled bubbles on (Enter to a default button, Tab to the window's keyboard navigation).
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = key, PhysicalKey = physicalKey, KeyModifiers = modifiers });
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyUpEvent, Key = key, PhysicalKey = physicalKey, KeyModifiers = modifiers });
    }

    private void TapShift()
    {
        var clock = TimeProvider;
        var now = clock.GetTimestamp();
        var sinceLast = _lastShiftTap is { } last ? clock.GetElapsedTime(last, now) : TimeSpan.MaxValue;
        _lastShiftTap = now;
        var next = ShiftState switch
        {
            OnScreenKeyboardShiftState.Locked => OnScreenKeyboardShiftState.Off,
            OnScreenKeyboardShiftState.On => sinceLast <= DoubleTapTime() ? OnScreenKeyboardShiftState.Locked : OnScreenKeyboardShiftState.Off,
            _ => OnScreenKeyboardShiftState.On,
        };
        ShiftState = next;
        _live?.Announce(next switch
        {
            OnScreenKeyboardShiftState.On => ShiftOnText,
            OnScreenKeyboardShiftState.Locked => CapsLockOnText,
            _ => ShiftOffText,
        });
    }

    private TimeSpan DoubleTapTime()
    {
        var time = Application.Current?.PlatformSettings?.GetDoubleTapTime(PointerType.Touch) ?? TimeSpan.Zero;
        return time > TimeSpan.Zero ? time : TimeSpan.FromMilliseconds(500);
    }

    private void SwitchPage(string? page)
    {
        if (ShiftState == OnScreenKeyboardShiftState.On)
        {
            ShiftState = OnScreenKeyboardShiftState.Off;
        }
        CurrentPage = page;
        if (EffectiveLayout.FindPage(page)?.Label is { Length: > 0 } label)
        {
            _live?.Announce(label);
        }
    }

    #endregion

    #region Pointer

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (_panel is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // A press on a key, or in the gap next to one (keys only hit-test where they draw), presses that key.
        var presenter = (e.Source as Visual)?.FindAncestorOfType<OnScreenKeyboardKeyPresenter>(includeSelf: true)
                        ?? _panel.KeyAt(e.GetPosition(_panel));
        if (presenter is not { IsSpacer: false, IsEffectivelyEnabled: true, Key: { } key } || !_panel.Children.Contains(presenter))
        {
            return;
        }
        EndPress(e.Pointer);
        StopRepeat();
        _presses.Add(new Press(e.Pointer, presenter));
        e.Pointer.Capture(presenter);
        presenter.SetPressed(true);
        InvokeCore(key, isRepeat: false);
        // The key may have switched page (its presenter now shows another key) or been taken away: repeat only a key
        // that is still held and still the same.
        if (key.RepeatsOnHold && ReferenceEquals(presenter.Key, key) && IsHeld(presenter))
        {
            StartRepeat(presenter);
        }
    }

    private void OnTunnelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
        EndPress(e.Pointer);
    }

    internal void OnKeyCaptureLost(OnScreenKeyboardKeyPresenter presenter, IPointer pointer) => EndPress(pointer);

    private bool IsHeld(OnScreenKeyboardKeyPresenter presenter)
    {
        foreach (var press in _presses)
        {
            if (ReferenceEquals(press.Key, presenter))
            {
                return true;
            }
        }
        return false;
    }

    private void EndPress(IPointer pointer)
    {
        for (var i = 0; i < _presses.Count; i++)
        {
            if (ReferenceEquals(_presses[i].Pointer, pointer))
            {
                var press = _presses[i];
                _presses.RemoveAt(i);
                Release(press);
                return;
            }
        }
    }

    private void EndPress(OnScreenKeyboardKeyPresenter presenter)
    {
        for (var i = _presses.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_presses[i].Key, presenter))
            {
                var press = _presses[i];
                _presses.RemoveAt(i);
                Release(press);
            }
        }
    }

    private void EndAllPresses()
    {
        while (_presses.Count > 0)
        {
            var press = _presses[^1];
            _presses.RemoveAt(_presses.Count - 1);
            Release(press);
        }
    }

    private void Release(Press press)
    {
        // Removed from the list first: releasing capture raises capture-lost, which comes back here and finds nothing.
        if (ReferenceEquals(_repeatKey, press.Key))
        {
            StopRepeat();
        }
        if (!IsHeld(press.Key))
        {
            press.Key.SetPressed(false);
        }
        if (ReferenceEquals(press.Pointer.Captured, press.Key))
        {
            press.Pointer.Capture(null);
        }
    }

    private void StartRepeat(OnScreenKeyboardKeyPresenter presenter)
    {
        StopRepeat();
        _repeatKey = presenter;
        var generation = _repeatGeneration;
        var interval = RepeatInterval < s_minimumRepeatInterval ? s_minimumRepeatInterval : RepeatInterval;
        var delay = RepeatDelay < TimeSpan.Zero ? TimeSpan.Zero : RepeatDelay;
        _repeatTimer = TimeProvider.CreateTimer(_ => OnRepeatTick(generation), null, delay, interval);
    }

    private void OnRepeatTick(int generation)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Repeat(generation);
            return;
        }
        // One tick in flight at a time: a busy UI thread gets the next repeat late, never a burst of them.
        if (Interlocked.Exchange(ref _repeatQueued, 1) == 1)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _repeatQueued, 0);
            Repeat(generation);
        }, DispatcherPriority.Input);
    }

    private void Repeat(int generation)
    {
        if (generation != _repeatGeneration || _repeatKey is not { Key: { } key } presenter || !presenter.IsPressed ||
            !presenter.IsEffectivelyEnabled)
        {
            return;
        }
        InvokeCore(key, isRepeat: true);
    }

    private void StopRepeat()
    {
        _repeatGeneration++;
        _repeatTimer?.Dispose();
        _repeatTimer = null;
        _repeatKey = null;
    }

    private readonly record struct Press(IPointer Pointer, OnScreenKeyboardKeyPresenter Key);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new KeyboardPeer(this);

    /// <summary>The keyboard is a group of key buttons, named "Keyboard" unless <c>AutomationProperties.Name</c> is set.</summary>
    private sealed class KeyboardPeer : ControlAutomationPeer
    {
        public KeyboardPeer(OnScreenKeyboard owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(OnScreenKeyboard);

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? "Keyboard" : name;
        }
    }
}

/// <summary>The default glyphs of the special keys (Lucide, 24×24).</summary>
internal static class OnScreenKeyboardGlyphs
{
    /// <summary>Lucide <c>delete</c>.</summary>
    public static Geometry Backspace => ArcIcons.Parse("M10 5a2 2 0 0 0-1.344.519l-6.328 5.74a1 1 0 0 0 0 1.481l6.328 5.741A2 2 0 0 0 10 19h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z M12 9l6 6 M18 9l-6 6");

    /// <summary>Lucide <c>arrow-big-up</c>.</summary>
    public static Geometry Shift => ArcIcons.Parse("M9 19a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-6a1 1 0 0 1 1-1h3.293a.707.707 0 0 0 .5-1.207l-7.086-7.086a1 1 0 0 0-1.414 0l-7.086 7.086a.707.707 0 0 0 .5 1.207H8a1 1 0 0 1 1 1z");

    /// <summary>Lucide <c>arrow-big-up-dash</c>.</summary>
    public static Geometry CapsLock => ArcIcons.Parse("M14 16a1 1 0 0 0 1-1v-2a1 1 0 0 1 1-1h3.293a.707.707 0 0 0 .5-1.207l-6.939-6.939a1.207 1.207 0 0 0-1.708 0l-6.94 6.94a.707.707 0 0 0 .5 1.206H8a1 1 0 0 1 1 1v2a1 1 0 0 0 1 1z M9 20h6");

    /// <summary>Lucide <c>arrow-right-to-line</c>.</summary>
    public static Geometry Tab => ArcIcons.Parse("M17 12H3 M11 18l6-6-6-6 M21 5v14");

    public static Geometry? For(OnScreenKeyboardKeyKind kind, OnScreenKeyboardShiftState shift) => kind switch
    {
        OnScreenKeyboardKeyKind.Backspace => Backspace,
        OnScreenKeyboardKeyKind.Enter => ArcIcons.CornerDownLeft,
        OnScreenKeyboardKeyKind.Tab => Tab,
        OnScreenKeyboardKeyKind.Shift => shift == OnScreenKeyboardShiftState.Locked ? CapsLock : Shift,
        OnScreenKeyboardKeyKind.Left => ArcIcons.ArrowLeft,
        OnScreenKeyboardKeyKind.Right => ArcIcons.ArrowRight,
        OnScreenKeyboardKeyKind.Up => ArcIcons.ArrowUp,
        OnScreenKeyboardKeyKind.Down => ArcIcons.ArrowDown,
        OnScreenKeyboardKeyKind.Hide => ArcIcons.ChevronDown,
        _ => null,
    };
}

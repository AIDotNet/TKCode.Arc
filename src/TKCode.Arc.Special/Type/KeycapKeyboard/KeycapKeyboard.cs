using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="KeycapKeyboard.KeyInvoked"/>.</summary>
public sealed class KeycapKeyInvokedEventArgs : RoutedEventArgs
{
    internal KeycapKeyInvokedEventArgs(RoutedEvent routedEvent, PhysicalKey key, string name, KeyModifiers modifiers,
                                       KeycapBinding? binding) : base(routedEvent)
    {
        Key = key;
        KeyName = name;
        Modifiers = modifiers;
        Binding = binding;
    }

    /// <summary>The key, by its place on a US keyboard (None for a custom key with no place on it).</summary>
    public PhysicalKey Key { get; }

    /// <summary>The key as it is printed or read: "K", "Return", "Space".</summary>
    public string KeyName { get; }

    /// <summary>The modifiers held with it.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>The shortcut in <see cref="KeycapKeyboard.Bindings"/> that the key and modifiers make, or null.</summary>
    public KeycapBinding? Binding { get; }
}

/// <summary>Event data of <see cref="KeycapKeyboard.ValueChanged"/>.</summary>
public sealed class KeycapKeyboardValueChangedEventArgs : RoutedEventArgs
{
    internal KeycapKeyboardValueChangedEventArgs(RoutedEvent routedEvent, string oldValue, string newValue) : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The text before the change.</summary>
    public string OldValue { get; }

    /// <summary>The text after the change.</summary>
    public string NewValue { get; }
}

/// <summary>
/// A board of sculpted mechanical keycaps with the typed text above it. Real typing and clicks press the matching cap into
/// its housing, light a soft glow under it and add the character to <see cref="Value"/>. <see cref="Layout"/> picks a
/// compact 60% board or letters only, and <see cref="Rows"/> builds any board, such as a macro pad.
/// </summary>
/// <remarks>
/// Every measure scales from one key unit that grows with the width up to 54 px (or <see cref="KeySize"/>). Caps match
/// real keys by their place, so any keyboard layout lights the right cap. The shortcut-viewer members
/// (<see cref="Shortcut"/>, <see cref="Bindings"/>, <see cref="Platform"/>, <see cref="ShowsFunctionRow"/>) still work: a
/// lit shortcut turns its caps to the accent, and bindings print what a key does while its modifiers are held.
/// </remarks>
[PseudoClasses(PressingPseudoClass)]
public class KeycapKeyboard : TemplatedControl
{
    internal const string PressingPseudoClass = ":pressing";

    /// <summary>Accessible name of the text area.</summary>
    internal const string TextName = "Text typed so far";

    // --- public properties ----------------------------------------------------------------------------------------

    /// <summary>The typed text. Typing and clicks append to it, Delete removes its last character and Return starts a new line.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceValueText);

    /// <summary>The built-in board: a compact 60% board (default) or letters only. <see cref="Rows"/> replaces it when it has keys.</summary>
    public static readonly StyledProperty<KeycapLayout> LayoutProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeycapLayout>(nameof(Layout));

    /// <summary>The longest text kept; older characters drop off the start. 0 or less keeps everything.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<KeycapKeyboard, int>(nameof(MaxLength), 280);

    /// <summary>Shown in the text area while the text is empty.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string>(nameof(Placeholder), "Type something");

    /// <summary>Whether the text area above the board is shown. Turn it off when you show <see cref="Value"/> yourself.</summary>
    public static readonly StyledProperty<bool> ShowsTextProperty =
        AvaloniaProperty.Register<KeycapKeyboard, bool>(nameof(ShowsText), true);

    /// <summary>The shortcut to light up in the accent, such as "Ctrl+Shift+P" or "⌘K". Null or empty lights up nothing.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string?>(nameof(Shortcut));

    /// <summary>The modifiers held now, by the real keyboard or by clicked modifier keys. Bindings for exactly these show.</summary>
    public static readonly StyledProperty<KeyModifiers> ActiveModifiersProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeyModifiers>(nameof(ActiveModifiers), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which real key presses the keyboard hears: none, presses while it has focus (default), or every press in the window.</summary>
    public static readonly StyledProperty<KeycapTracking> TrackingProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeycapTracking>(nameof(Tracking), KeycapTracking.WhenFocused);

    /// <summary>
    /// Which words the modifier keys of the compact board carry, and in which order they sit. When not set, the board
    /// carries ctrl, opt and cmd; set it to Windows for ctrl, win and alt, or to Auto to follow the system.
    /// </summary>
    public static readonly StyledProperty<KeycapPlatform> PlatformProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeycapPlatform>(nameof(Platform));

    /// <summary>Whether the compact board has a row of Esc and F1 to F12 on top.</summary>
    public static readonly StyledProperty<bool> ShowsFunctionRowProperty =
        AvaloniaProperty.Register<KeycapKeyboard, bool>(nameof(ShowsFunctionRow));

    /// <summary>Whether keys can be clicked and focused. Real presses still light them when false.</summary>
    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<KeycapKeyboard, bool>(nameof(IsClickable), true);

    /// <summary>Largest key unit in px (the side of a one-unit key) when set; the keys shrink below it to fit. When not set, keys grow up to 54 px.</summary>
    public static readonly StyledProperty<double> KeySizeProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>(nameof(KeySize), 44);

    /// <summary>Accessible name of the board.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string>(nameof(Label), "Keyboard");

    /// <summary>Raised when a key is clicked, pressed with Enter or Space while focused, or pressed on the real keyboard while presses are heard. Modifier keys alone do not raise it.</summary>
    public static readonly RoutedEvent<KeycapKeyInvokedEventArgs> KeyInvokedEvent =
        RoutedEvent.Register<KeycapKeyboard, KeycapKeyInvokedEventArgs>(nameof(KeyInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Value"/> changes, by typing or by code.</summary>
    public static readonly RoutedEvent<KeycapKeyboardValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<KeycapKeyboard, KeycapKeyboardValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    // --- theme values (set by the ControlTheme) ----------------------------------------------------------------------

    internal static readonly StyledProperty<double> MaxKeyUnitProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("MaxKeyUnit", 54);
    internal static readonly StyledProperty<double> TextGapProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("TextGap", 20);
    internal static readonly StyledProperty<double> GlowOpacityProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("GlowOpacity", 0.34);
    internal static readonly StyledProperty<double> PressTintOpacityProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("PressTintOpacity", 0.14);
    internal static readonly StyledProperty<double> AccentPressTintOpacityProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("AccentPressTintOpacity", 0.08);
    internal static readonly StyledProperty<IBrush?> PlateBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("PlateBrush");
    internal static readonly StyledProperty<IBrush?> PlateBorderBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("PlateBorderBrush");
    internal static readonly StyledProperty<IBrush?> PlateShadowBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("PlateShadowBrush");
    internal static readonly StyledProperty<IBrush?> HousingBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("HousingBrush");
    internal static readonly StyledProperty<IBrush?> HousingShadowBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("HousingShadowBrush");
    internal static readonly StyledProperty<IBrush?> CapTopBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("CapTopBrush");
    internal static readonly StyledProperty<IBrush?> CapBottomBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("CapBottomBrush");
    internal static readonly StyledProperty<IBrush?> CapRingBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("CapRingBrush");
    internal static readonly StyledProperty<IBrush?> CapLipBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("CapLipBrush");
    internal static readonly StyledProperty<IBrush?> FaceTopBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FaceTopBrush");
    internal static readonly StyledProperty<IBrush?> FaceBottomBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FaceBottomBrush");
    internal static readonly StyledProperty<IBrush?> FaceShadeBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FaceShadeBrush");
    internal static readonly StyledProperty<IBrush?> FaceLightBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FaceLightBrush");
    internal static readonly StyledProperty<IBrush?> LegendBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("LegendBrush");
    internal static readonly StyledProperty<IBrush?> CornerLegendBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("CornerLegendBrush");
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("AccentBrush");
    internal static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("AccentForegroundBrush");
    internal static readonly StyledProperty<IBrush?> BindingTextBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("BindingTextBrush");
    internal static readonly StyledProperty<IBrush?> BindingFaceBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("BindingFaceBrush");
    internal static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("PlaceholderBrush");
    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FocusBrush");
    internal static readonly StyledProperty<FontFamily> TextFontFamilyProperty =
        AvaloniaProperty.Register<KeycapKeyboard, FontFamily>("TextFontFamily", FontFamily.Default);

    // Theme-set values, named by the XAML setters through these accessors.
    internal double MaxKeyUnit
    {
        get => GetValue(MaxKeyUnitProperty);
        set => SetValue(MaxKeyUnitProperty, value);
    }

    internal double TextGap
    {
        get => GetValue(TextGapProperty);
        set => SetValue(TextGapProperty, value);
    }

    internal double GlowOpacity
    {
        get => GetValue(GlowOpacityProperty);
        set => SetValue(GlowOpacityProperty, value);
    }

    internal double PressTintOpacity
    {
        get => GetValue(PressTintOpacityProperty);
        set => SetValue(PressTintOpacityProperty, value);
    }

    internal double AccentPressTintOpacity
    {
        get => GetValue(AccentPressTintOpacityProperty);
        set => SetValue(AccentPressTintOpacityProperty, value);
    }

    internal IBrush? PlateBrush
    {
        get => GetValue(PlateBrushProperty);
        set => SetValue(PlateBrushProperty, value);
    }

    internal IBrush? PlateBorderBrush
    {
        get => GetValue(PlateBorderBrushProperty);
        set => SetValue(PlateBorderBrushProperty, value);
    }

    internal IBrush? PlateShadowBrush
    {
        get => GetValue(PlateShadowBrushProperty);
        set => SetValue(PlateShadowBrushProperty, value);
    }

    internal IBrush? HousingBrush
    {
        get => GetValue(HousingBrushProperty);
        set => SetValue(HousingBrushProperty, value);
    }

    internal IBrush? HousingShadowBrush
    {
        get => GetValue(HousingShadowBrushProperty);
        set => SetValue(HousingShadowBrushProperty, value);
    }

    internal IBrush? CapTopBrush
    {
        get => GetValue(CapTopBrushProperty);
        set => SetValue(CapTopBrushProperty, value);
    }

    internal IBrush? CapBottomBrush
    {
        get => GetValue(CapBottomBrushProperty);
        set => SetValue(CapBottomBrushProperty, value);
    }

    internal IBrush? CapRingBrush
    {
        get => GetValue(CapRingBrushProperty);
        set => SetValue(CapRingBrushProperty, value);
    }

    internal IBrush? CapLipBrush
    {
        get => GetValue(CapLipBrushProperty);
        set => SetValue(CapLipBrushProperty, value);
    }

    internal IBrush? FaceTopBrush
    {
        get => GetValue(FaceTopBrushProperty);
        set => SetValue(FaceTopBrushProperty, value);
    }

    internal IBrush? FaceBottomBrush
    {
        get => GetValue(FaceBottomBrushProperty);
        set => SetValue(FaceBottomBrushProperty, value);
    }

    internal IBrush? FaceShadeBrush
    {
        get => GetValue(FaceShadeBrushProperty);
        set => SetValue(FaceShadeBrushProperty, value);
    }

    internal IBrush? FaceLightBrush
    {
        get => GetValue(FaceLightBrushProperty);
        set => SetValue(FaceLightBrushProperty, value);
    }

    internal IBrush? LegendBrush
    {
        get => GetValue(LegendBrushProperty);
        set => SetValue(LegendBrushProperty, value);
    }

    internal IBrush? CornerLegendBrush
    {
        get => GetValue(CornerLegendBrushProperty);
        set => SetValue(CornerLegendBrushProperty, value);
    }

    internal IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    internal IBrush? AccentForegroundBrush
    {
        get => GetValue(AccentForegroundBrushProperty);
        set => SetValue(AccentForegroundBrushProperty, value);
    }

    internal IBrush? BindingTextBrush
    {
        get => GetValue(BindingTextBrushProperty);
        set => SetValue(BindingTextBrushProperty, value);
    }

    internal IBrush? BindingFaceBrush
    {
        get => GetValue(BindingFaceBrushProperty);
        set => SetValue(BindingFaceBrushProperty, value);
    }

    internal IBrush? PlaceholderBrush
    {
        get => GetValue(PlaceholderBrushProperty);
        set => SetValue(PlaceholderBrushProperty, value);
    }

    internal IBrush? FocusBrush
    {
        get => GetValue(FocusBrushProperty);
        set => SetValue(FocusBrushProperty, value);
    }

    internal FontFamily TextFontFamily
    {
        get => GetValue(TextFontFamilyProperty);
        set => SetValue(TextFontFamilyProperty, value);
    }

    // Motion (Arc keycap keyboard, design reference): press 35 ms down, 200 ms up; tint, legend and glow fade in 40 ms
    // and out in 600, 400 and 700 ms; hover lights the legend in 400 ms; a board change morphs the plate on a critically
    // damped spring (ω 18.2 rad/s, cut at 440 ms) while the rows fade in over 200 ms.
    private const double DownSeconds = 0.035;
    private const double UpSeconds = 0.2;
    private const double QuickSeconds = 0.04;
    private const double TintOutSeconds = 0.6;
    private const double LegendSeconds = 0.4;
    private const double GlowOutSeconds = 0.7;
    private const double MorphSeconds = 0.44;
    private const double MorphOmega = 18.18;
    private const double RowFadeSeconds = 0.2;
    private static readonly TimeSpan TapHold = TimeSpan.FromMilliseconds(90);

    private readonly LiveLoop _loop;
    private readonly KeycapKeyboardText _text;
    private readonly KeycapGlowLayer _glowLayer;
    private KeycapBoard _board = KeycapBoard.Empty;
    private KeycapKeyboardKey[] _keys = [];
    private KeycapKeyVisual[] _visuals = [];
    private KeycapRow?[] _rowOf = [];
    private bool[] _held = [];
    private bool[] _lit = [];
    private bool[] _hover = [];
    private KeycapBinding?[] _bindingOf = [];
    private KeycapTween[] _offset = [];
    private KeycapTween[] _tint = [];
    private KeycapTween[] _legend = [];
    private KeycapTween[] _glow = [];
    private int _pointerKey = -1;
    private int _buttonKey = -1;
    private int _tapKey = -1;
    private KeyModifiers _pointerModifiers;
    private KeyModifiers _sticky;
    private KeyModifiers _physical;
    private KeyModifiers _lastKeyModifiers;
    private TopLevel? _watched;
    private KeycapKeyboardSkin? _skin;
    private bool _skinDirty = true;
    private double _clock;
    private int _typed;

    // Board geometry from the last arrange.
    private double _unit;
    private double _padding;
    private double _textHeight;
    private Rect _plate;

    // Plate morph and row fade after a board change.
    private Size _plateShown;
    private Size _morphFrom;
    private double _morphStart;
    private bool _morphPending;
    private bool _morphing;
    private double _fadeStart;
    private bool _fading;

    static KeycapKeyboard()
    {
        FocusableProperty.OverrideDefaultValue<KeycapKeyboard>(true);
        IsTabStopProperty.OverrideDefaultValue<KeycapKeyboard>(false);
        ClipToBoundsProperty.OverrideDefaultValue<KeycapKeyboard>(false);
        AffectsMeasure<KeycapKeyboard>(KeySizeProperty, ShowsTextProperty, MaxKeyUnitProperty, TextGapProperty);
        AffectsRender<KeycapKeyboard>(PlateBrushProperty, PlateBorderBrushProperty, PlateShadowBrushProperty);
    }

    /// <summary>Creates a compact keyboard.</summary>
    public KeycapKeyboard()
    {
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                SettleMotion();
            }
        };
        _text = new KeycapKeyboardText(this);
        _glowLayer = new KeycapGlowLayer(this);
        VisualChildren.Add(_text);
        LogicalChildren.Add(_text);
        VisualChildren.Add(_glowLayer);
        Bindings.CollectionChanged += OnBindingsChanged;
        Rows.CollectionChanged += OnRowsChanged;
        RebuildBoard(animate: false);
        _text.SetPlaceholder(Placeholder);
    }

    /// <summary>The shortcuts whose labels show on their keys while their modifiers are held.</summary>
    [Content]
    public AvaloniaList<KeycapBinding> Bindings { get; } = [];

    /// <summary>Custom rows that replace <see cref="Layout"/>, top first, such as a single row "k e y backspace" for a macro pad.</summary>
    public AvaloniaList<KeycapRow> Rows { get; } = [];

    /// <inheritdoc cref="ValueProperty"/>
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="LayoutProperty"/>
    public KeycapLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <inheritdoc cref="ShowsTextProperty"/>
    public bool ShowsText
    {
        get => GetValue(ShowsTextProperty);
        set => SetValue(ShowsTextProperty, value);
    }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <inheritdoc cref="ActiveModifiersProperty"/>
    public KeyModifiers ActiveModifiers
    {
        get => GetValue(ActiveModifiersProperty);
        set => SetValue(ActiveModifiersProperty, value);
    }

    /// <inheritdoc cref="TrackingProperty"/>
    public KeycapTracking Tracking
    {
        get => GetValue(TrackingProperty);
        set => SetValue(TrackingProperty, value);
    }

    /// <inheritdoc cref="PlatformProperty"/>
    public KeycapPlatform Platform
    {
        get => GetValue(PlatformProperty);
        set => SetValue(PlatformProperty, value);
    }

    /// <inheritdoc cref="ShowsFunctionRowProperty"/>
    public bool ShowsFunctionRow
    {
        get => GetValue(ShowsFunctionRowProperty);
        set => SetValue(ShowsFunctionRowProperty, value);
    }

    /// <inheritdoc cref="IsClickableProperty"/>
    public bool IsClickable
    {
        get => GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    /// <inheritdoc cref="KeySizeProperty"/>
    public double KeySize
    {
        get => GetValue(KeySizeProperty);
        set => SetValue(KeySizeProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="KeyInvokedEvent"/>
    public event EventHandler<KeycapKeyInvokedEventArgs>? KeyInvoked
    {
        add => AddHandler(KeyInvokedEvent, value);
        remove => RemoveHandler(KeyInvokedEvent, value);
    }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<KeycapKeyboardValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>
    /// Presses a key as a click would: a modifier holds until the next key, Shift shifts the next character, any other key
    /// types (or deletes, or starts a line) and raises <see cref="KeyInvoked"/>. A key not on the board does nothing.
    /// </summary>
    public void Invoke(PhysicalKey key)
    {
        var index = KeycapKeyboardLayout.IndexOf(_keys, key);
        if (index < 0)
        {
            return;
        }
        var entry = _keys[index];
        if (entry.IsModifier)
        {
            _sticky ^= entry.Modifier;
            UpdateModifiers();
            return;
        }
        Tap(index);
    }

    /// <summary>Clears the typed text.</summary>
    public void Clear() => SetCurrentValue(ValueProperty, string.Empty);

    // --- internal views (tests, parts) --------------------------------------------------------------------------------

    /// <summary>Whether the keys carry Mac words and order (the default) or Windows ones.</summary>
    internal bool IsMac
    {
        get
        {
            if (!IsSet(PlatformProperty))
            {
                return true;
            }
            return Platform switch
            {
                KeycapPlatform.Mac => true,
                KeycapPlatform.Windows => false,
                _ => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
            };
        }
    }

    internal IReadOnlyList<KeycapKeyboardKey> Keys => _keys;

    internal KeycapKeyVisual[] KeyVisuals => _visuals;

    internal KeycapKeyboardText TextArea => _text;

    internal KeycapKeyboardSkin? Skin => _skin;

    internal double Clock => _clock;

    internal int PointerKey => _pointerKey;

    /// <summary>The key unit of the last arrange (tests).</summary>
    internal double Unit => _unit;

    /// <summary>The plate's box of the last arrange (tests).</summary>
    internal Rect PlateBounds => _plate;

    /// <summary>The accent key of the board, or −1 (tests).</summary>
    internal int AccentIndex => _board.AccentIndex;

    /// <summary>The number of rows on the board (tests).</summary>
    internal int RowCount => _board.Rows;

    /// <summary>Whether a key is lit by <see cref="Shortcut"/> (tests).</summary>
    internal bool IsLit(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i && _lit[i];

    /// <summary>Whether a key is down: held by the real keyboard or the pointer, or a clicked modifier that holds (tests).</summary>
    internal bool IsDown(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i && (IsKeyDown(i) || IsLatched(i));

    /// <summary>How far a key is drawn pressed, as a share of the side (0.92 down, 0.5 latched, tests).</summary>
    internal double PressOf(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i ? _offset[i].Value : 0;

    /// <summary>The glow under a key, 0 to 1 (tests).</summary>
    internal double GlowOf(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i ? _glow[i].Value : 0;

    /// <summary>The binding label a key shows now, or null (tests).</summary>
    internal string? BindingLabelOf(PhysicalKey key)
        => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i ? _bindingOf[i]?.Label : null;

    /// <summary>The box of a key in this control's coordinates.</summary>
    internal Rect KeyBounds(int index) => _visuals[index].Bounds;

    internal bool IsAccentKey(int index) => _lit[index] || (index == _board.AccentIndex && !HasLitShortcut);

    private bool HasLitShortcut => Array.IndexOf(_lit, true) >= 0;

    internal double OffsetOf(int index) => _offset[index].Value;

    internal double TintOf(int index) => _tint[index].Value;

    internal double LegendOf(int index) => _legend[index].Value;

    internal double GlowOf(int index) => _glow[index].Value;

    // --- property changes ---------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty)
        {
            var oldValue = change.GetOldValue<string>() ?? string.Empty;
            var newValue = change.GetNewValue<string>() ?? string.Empty;
            _text.SetText(newValue, _typed, _clock);
            if (_typed > 0)
            {
                _loop.Start();
            }
            _typed = 0;
            RaiseEvent(new KeycapKeyboardValueChangedEventArgs(ValueChangedEvent, oldValue, newValue));
        }
        else if (property == MaxLengthProperty)
        {
            CoerceValue(ValueProperty);
        }
        else if (property == PlaceholderProperty)
        {
            _text.SetPlaceholder(Placeholder ?? string.Empty);
        }
        else if (property == ShowsTextProperty)
        {
            _text.IsVisible = ShowsText;
        }
        else if (property == LayoutProperty || property == PlatformProperty || property == ShowsFunctionRowProperty)
        {
            RebuildBoard(animate: true);
        }
        else if (property == ShortcutProperty)
        {
            UpdateLit();
        }
        else if (property == ActiveModifiersProperty)
        {
            UpdateBindingLabels();
        }
        else if (property == TrackingProperty)
        {
            ReleaseHeld();
            UpdateWatch();
        }
        else if (property == IsClickableProperty || property == IsEffectivelyEnabledProperty)
        {
            UpdateTabStops();
        }
        else if (property == FontFamilyProperty || property == ForegroundProperty || property == TextFontFamilyProperty ||
                 property == PlaceholderBrushProperty || property == AccentBrushProperty || property == FocusBrushProperty ||
                 IsSkinProperty(property))
        {
            _skinDirty = true;
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    private static bool IsSkinProperty(AvaloniaProperty p)
        => p == HousingBrushProperty || p == HousingShadowBrushProperty || p == CapTopBrushProperty || p == CapBottomBrushProperty ||
           p == CapRingBrushProperty || p == CapLipBrushProperty || p == FaceTopBrushProperty || p == FaceBottomBrushProperty ||
           p == FaceShadeBrushProperty || p == FaceLightBrushProperty || p == LegendBrushProperty || p == CornerLegendBrushProperty ||
           p == AccentForegroundBrushProperty || p == BindingTextBrushProperty || p == BindingFaceBrushProperty ||
           p == GlowOpacityProperty || p == PressTintOpacityProperty || p == AccentPressTintOpacityProperty;

    private static string CoerceValueText(AvaloniaObject sender, string? value)
    {
        value ??= string.Empty;
        var max = sender.GetValue(MaxLengthProperty);
        if (max > 0 && value.Length > max)
        {
            var start = value.Length - max;
            if (start < value.Length && char.IsLowSurrogate(value[start]))
            {
                start++;
            }
            value = value[start..];
        }
        return value;
    }

    // --- board ----------------------------------------------------------------------------------------------------

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (KeycapRow row in e.OldItems)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (KeycapRow row in e.NewItems)
            {
                row.PropertyChanged += OnRowPropertyChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
                row.PropertyChanged += OnRowPropertyChanged;
            }
        }
        RebuildBoard(animate: true);
    }

    private void OnRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KeycapRow.IsEnabledProperty)
        {
            UpdateTabStops();
            return;
        }
        RebuildBoard(animate: true);
    }

    private KeycapBoard ComputeBoard(out KeycapRow?[] rowOf)
    {
        var custom = new List<IReadOnlyList<string>>();
        var owners = new List<KeycapRow?>();
        foreach (var row in Rows)
        {
            if (!row.IsVisible)
            {
                continue;
            }
            var tokens = KeycapKeyboardLayout.Tokens(row.Keys);
            if (tokens.Length > 0)
            {
                custom.Add(tokens);
                owners.Add(row);
            }
        }
        if (custom.Count > 0)
        {
            var board = KeycapKeyboardLayout.Custom(custom);
            if (board.Keys.Length > 0)
            {
                rowOf = owners.ToArray();
                return board;
            }
        }
        var builtIn = KeycapKeyboardLayout.Board(Layout, IsMac, ShowsFunctionRow);
        rowOf = new KeycapRow?[builtIn.Rows];
        return builtIn;
    }

    /// <summary>Builds the keys of the current board; after the first layout the plate morphs and the rows fade in.</summary>
    private void RebuildBoard(bool animate)
    {
        var board = ComputeBoard(out var rowOf);
        if (ReferenceEquals(board, _board) && rowOf.Length == _rowOf.Length)
        {
            _rowOf = rowOf;
            UpdateTabStops();
            return;
        }
        var morph = animate && IsLoaded && ArcMotion.IsFull(this) && _plate.Width > 0;
        ReleaseHeld();
        foreach (var visual in _visuals)
        {
            visual.DisposeText();
            VisualChildren.Remove(visual);
            LogicalChildren.Remove(visual);
        }
        _board = board;
        _rowOf = rowOf;
        _keys = board.Keys;
        var count = _keys.Length;
        _visuals = new KeycapKeyVisual[count];
        _held = new bool[count];
        _lit = new bool[count];
        _hover = new bool[count];
        _bindingOf = new KeycapBinding?[count];
        _offset = new KeycapTween[count];
        _tint = new KeycapTween[count];
        _legend = new KeycapTween[count];
        _glow = new KeycapTween[count];
        _pointerKey = -1;
        _buttonKey = -1;
        _tapKey = -1;
        for (var i = 0; i < count; i++)
        {
            var visual = new KeycapKeyVisual(this, i, _keys[i]);
            _visuals[i] = visual;
            VisualChildren.Add(visual);
            LogicalChildren.Add(visual);
        }
        _skinDirty = true;
        UpdateTabStops();
        UpdateLit();
        UpdateBindingLabels();
        UpdateModifiers();
        if (morph)
        {
            _morphFrom = _plateShown;
            _morphPending = true;
            _fading = true;
            _fadeStart = _clock;
            foreach (var visual in _visuals)
            {
                visual.Opacity = 0;
            }
            _loop.Start();
        }
        InvalidateMeasure();
        InvalidateVisual();
        _glowLayer.InvalidateVisual();
    }

    private void UpdateTabStops()
    {
        for (var i = 0; i < _visuals.Length; i++)
        {
            var can = CanClick(i);
            _visuals[i].IsTabStop = can;
            _visuals[i].Focusable = can;
            _visuals[i].IsHitTestVisible = IsClickable;
        }
    }

    internal bool CanClick(int index)
        => IsClickable && IsEffectivelyEnabled && (index >= _keys.Length || _rowOf.Length <= _keys[index].Row ||
                                                   _rowOf[_keys[index].Row] is not { IsEnabled: false });

    // --- layout -------------------------------------------------------------------------------------------------------

    private readonly record struct Metrics(double Width, double Unit, double Padding, double RowsWidth, Size Plate, double FontSize, double TextHeight, double TextGap);

    private Metrics ComputeMetrics(double width)
    {
        var units = Math.Max(1, _board.Units);
        var rows = Math.Max(1, _board.Rows);
        var maxUnit = IsSet(KeySizeProperty) ? KeySize : GetValue(MaxKeyUnitProperty);
        maxUnit = Math.Max(1, maxUnit);
        if (!double.IsFinite(width))
        {
            width = maxUnit * (units * (1 + KeycapKeyboardSkin.GapRatio) - KeycapKeyboardSkin.GapRatio) + 2 * 16 + 2;
        }
        var padding = Math.Clamp(width * 0.022, 6, 16);
        var unit = Math.Min(maxUnit, (width - 2 * padding - 2) / (units * (1 + KeycapKeyboardSkin.GapRatio) - KeycapKeyboardSkin.GapRatio));
        unit = Math.Max(1, unit);
        var gap = unit * KeycapKeyboardSkin.GapRatio;
        var rowsWidth = units * (unit + gap) - gap;
        var keyHeight = unit * (1 + KeycapKeyboardSkin.TravelRatio);
        var plate = new Size(rowsWidth + 2 * padding + 2, rows * keyHeight + (rows - 1) * gap + 2 * padding + 2);
        var fontSize = Math.Clamp(width * 0.044, 20, 32);
        var showsText = ShowsText;
        var textHeight = showsText ? KeycapKeyboardText.HeightFor(fontSize) : 0;
        var textGap = showsText ? GetValue(TextGapProperty) : 0;
        return new Metrics(width, unit, padding, rowsWidth, plate, fontSize, textHeight, textGap);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var m = ComputeMetrics(availableSize.Width);
        _text.SetFontSize(m.FontSize);
        _text.Measure(new Size(m.Width, m.TextHeight));
        var keySize = new Size(m.Unit, m.Unit * (1 + KeycapKeyboardSkin.TravelRatio));
        foreach (var visual in _visuals)
        {
            visual.Measure(keySize);
        }
        _glowLayer.Measure(availableSize);
        var width = double.IsFinite(availableSize.Width) ? m.Width : Math.Max(m.Plate.Width, 1);
        return new Size(width, m.TextHeight + m.TextGap + m.Plate.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var m = ComputeMetrics(finalSize.Width);
        var unitChanged = !m.Unit.Equals(_unit);
        _unit = m.Unit;
        _padding = m.Padding;
        _textHeight = m.TextHeight;
        _text.SetFontSize(m.FontSize);
        _text.Arrange(new Rect(0, 0, finalSize.Width, m.TextHeight));
        _plate = new Rect((finalSize.Width - m.Plate.Width) / 2, m.TextHeight + m.TextGap, m.Plate.Width, m.Plate.Height);
        if (unitChanged || _skinDirty || _skin is null)
        {
            BuildSkin();
        }
        var skin = _skin!;
        var pitch = skin.Unit + skin.Gap;
        var rowStep = skin.KeyHeight + skin.Gap;
        var rowWidths = RowWidths();
        for (var i = 0; i < _visuals.Length; i++)
        {
            var key = _keys[i];
            var rowWidth = key.Row < rowWidths.Length ? rowWidths[key.Row] : m.RowsWidth;
            var x = _plate.X + 1 + m.Padding + (m.RowsWidth - rowWidth) / 2 + key.X * pitch;
            var y = _plate.Y + 1 + m.Padding + key.Row * rowStep;
            _visuals[i].Arrange(new Rect(x, y, skin.KeyWidth(key.Width), skin.KeyHeight));
        }
        _glowLayer.Arrange(new Rect(finalSize));
        if (unitChanged || _skinDirty)
        {
            _skinDirty = false;
            RebuildLegends();
        }
        if (_morphPending)
        {
            _morphPending = false;
            if (_morphFrom.Width > 0 && _morphFrom != _plate.Size)
            {
                _morphing = true;
                _morphStart = _clock;
                _loop.Start();
            }
        }
        if (!_morphing)
        {
            _plateShown = _plate.Size;
        }
        return finalSize;
    }

    private double[] RowWidths()
    {
        var widths = new double[Math.Max(0, _board.Rows)];
        var gap = _unit * KeycapKeyboardSkin.GapRatio;
        foreach (var key in _keys)
        {
            if (key.Row < widths.Length)
            {
                widths[key.Row] = Math.Max(widths[key.Row], (key.X + key.Width) * (_unit + gap) - gap);
            }
        }
        return widths;
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * solid.Opacity), solid.Color.R, solid.Color.G, solid.Color.B)
        : fallback;

    private void BuildSkin()
    {
        var accent = ColorOf(GetValue(AccentBrushProperty), Colors.Gray);
        var colors = new KeycapKeyboardColors(
            Housing: ColorOf(GetValue(HousingBrushProperty), Colors.Black),
            HousingShadow: ColorOf(GetValue(HousingShadowBrushProperty), Colors.Transparent),
            CapTop: ColorOf(GetValue(CapTopBrushProperty), Colors.DimGray),
            CapBottom: ColorOf(GetValue(CapBottomBrushProperty), Colors.DimGray),
            CapRing: ColorOf(GetValue(CapRingBrushProperty), Colors.Transparent),
            CapLip: ColorOf(GetValue(CapLipBrushProperty), Colors.Transparent),
            FaceTop: ColorOf(GetValue(FaceTopBrushProperty), Colors.Gray),
            FaceBottom: ColorOf(GetValue(FaceBottomBrushProperty), Colors.Gray),
            FaceShade: ColorOf(GetValue(FaceShadeBrushProperty), Colors.Transparent),
            FaceLight: ColorOf(GetValue(FaceLightBrushProperty), Colors.Transparent),
            Legend: ColorOf(GetValue(LegendBrushProperty), Colors.White),
            CornerLegend: ColorOf(GetValue(CornerLegendBrushProperty), Colors.LightGray),
            ActiveLegend: ColorOf(Foreground, Colors.White),
            Accent: accent,
            AccentLegend: ColorOf(GetValue(AccentForegroundBrushProperty), Colors.Black),
            BindingText: ColorOf(GetValue(BindingTextBrushProperty), accent),
            BindingFace: ColorOf(GetValue(BindingFaceBrushProperty), Colors.Transparent),
            Focus: ColorOf(GetValue(FocusBrushProperty), accent),
            GlowOpacity: GetValue(GlowOpacityProperty),
            PressTintOpacity: GetValue(PressTintOpacityProperty),
            AccentPressTintOpacity: GetValue(AccentPressTintOpacityProperty));
        _skin = new KeycapKeyboardSkin(_unit > 0 ? _unit : 40, colors, FontFamily);
        _text.TextFont = GetValue(TextFontFamilyProperty);
        _text.TextBrush = Foreground;
        _text.PlaceholderBrush = GetValue(PlaceholderBrushProperty);
        _text.CaretBrush = GetValue(AccentBrushProperty);
        _text.FocusPen = _skin.FocusPen;
        _text.Invalidate();
        _glowLayer.InvalidateVisual();
        InvalidateVisual();
    }

    private void RebuildLegends()
    {
        if (_skin is null)
        {
            return;
        }
        var bindingBrush = GetValue(BindingTextBrushProperty);
        for (var i = 0; i < _visuals.Length; i++)
        {
            _visuals[i].Rebuild(_skin, IsAccentKey(i), _bindingOf[i]?.Label, bindingBrush);
        }
    }

    // --- drawing --------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (_plate.Width <= 0 || _skin is null)
        {
            return;
        }
        var size = _morphing ? _plateShown : _plate.Size;
        var rect = new Rect(_plate.X + (_plate.Width - size.Width) / 2, _plate.Y, size.Width, size.Height);
        var radius = _padding + _skin.Radius;
        var shadow = new BoxShadows(new BoxShadow
        {
            OffsetY = 1,
            Blur = 2,
            Color = ColorOf(GetValue(PlateShadowBrushProperty), Colors.Transparent)
        });
        context.DrawRectangle(GetValue(PlateBrushProperty), null, new RoundedRect(rect, radius), shadow);
        if (GetValue(PlateBorderBrushProperty) is { } border)
        {
            context.DrawRectangle(null, new ImmutablePen(border.ToImmutable(), 1), new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }

    // --- shortcut and bindings ----------------------------------------------------------------------------------------

    private void UpdateLit()
    {
        Array.Clear(_lit);
        if (KeycapKeyboardLayout.TryParse(Shortcut, out var modifiers, out var key))
        {
            Light(key);
            foreach (var modifier in new[] { KeyModifiers.Control, KeyModifiers.Shift, KeyModifiers.Alt, KeyModifiers.Meta })
            {
                if (modifiers.HasFlag(modifier))
                {
                    Light(KeycapKeyboardLayout.KeyOf(modifier));
                }
            }
        }
        RebuildLegends();
        InvalidateKeys();

        void Light(PhysicalKey physical)
        {
            var index = KeycapKeyboardLayout.IndexOf(_keys, physical);
            if (index >= 0)
            {
                _lit[index] = true;
            }
        }
    }

    private void OnBindingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (KeycapBinding binding in e.OldItems)
            {
                binding.PropertyChanged -= OnBindingPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (KeycapBinding binding in e.NewItems)
            {
                binding.PropertyChanged += OnBindingPropertyChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var binding in Bindings)
            {
                binding.PropertyChanged -= OnBindingPropertyChanged;
                binding.PropertyChanged += OnBindingPropertyChanged;
            }
        }
        UpdateBindingLabels();
    }

    private void OnBindingPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => UpdateBindingLabels();

    /// <summary>Puts each binding whose modifiers are exactly the held ones on its key.</summary>
    private void UpdateBindingLabels()
    {
        Array.Clear(_bindingOf);
        var held = ActiveModifiers;
        foreach (var binding in Bindings)
        {
            if (!KeycapKeyboardLayout.TryParse(binding.Gesture, out var modifiers, out var key) || modifiers != held)
            {
                continue;
            }
            var index = KeycapKeyboardLayout.IndexOf(_keys, key);
            if (index >= 0 && _bindingOf[index] is null)
            {
                _bindingOf[index] = binding;
            }
        }
        RebuildLegends();
    }

    private KeycapBinding? BindingFor(PhysicalKey key, KeyModifiers modifiers)
    {
        foreach (var binding in Bindings)
        {
            if (KeycapKeyboardLayout.TryParse(binding.Gesture, out var m, out var k) && k == key && m == modifiers)
            {
                return binding;
            }
        }
        return null;
    }

    private void RaiseInvoked(KeycapKeyboardKey key, KeyModifiers modifiers)
        => RaiseEvent(new KeycapKeyInvokedEventArgs(KeyInvokedEvent, key.Physical, KeycapKeyboardLayout.NameOf(key), modifiers,
            BindingFor(key.Physical, modifiers)));

    /// <summary>The help text read with the board: the lit shortcut and the shortcuts shown now.</summary>
    internal string Describe()
    {
        var parts = new List<string>();
        if (KeycapKeyboardLayout.TryParse(Shortcut, out var modifiers, out var key))
        {
            parts.Add("Shortcut " + KeycapKeyboardLayout.Describe(modifiers, key, IsMac, _keys) + ".");
        }
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_bindingOf[i] is { Label: { Length: > 0 } label })
            {
                parts.Add(KeycapKeyboardLayout.Describe(ActiveModifiers, _keys[i].Physical, IsMac, _keys) + ": " + label + ".");
            }
        }
        return string.Join(" ", parts);
    }

    // --- key state and motion -----------------------------------------------------------------------------------------

    private bool IsKeyDown(int index) => _held[index] || _pointerKey == index || _buttonKey == index || _tapKey == index;

    private bool IsLatched(int index) => _keys[index].IsModifier && (_sticky & _keys[index].Modifier) != 0;

    internal void SetHover(int index, bool hover)
    {
        if (index < _hover.Length && _hover[index] != hover)
        {
            _hover[index] = hover;
            UpdateKey(index);
        }
    }

    /// <summary>Moves a key's press, tint, legend and glow toward its state.</summary>
    private void UpdateKey(int index)
    {
        if (index < 0 || index >= _keys.Length)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var instant = !IsLoaded || level != ArcMotionLevel.Full;
        var down = IsKeyDown(index);
        var latched = IsLatched(index);
        var now = _clock;
        // Reduced motion removes the travel; the light and the legend still answer, at once.
        var offset = level == ArcMotionLevel.Reduced ? 0 : latched ? KeycapKeyboardSkin.LatchDepth : down ? KeycapKeyboardSkin.PressDepth : 0;
        var deeper = offset > _offset[index].Value;
        var changed = _offset[index].Set(offset, now, deeper ? DownSeconds : UpSeconds,
            deeper ? KeycapKeyboardSkin.DownEasing : KeycapKeyboardSkin.UpEasing, instant);
        var active = down || latched;
        changed |= _tint[index].Set(active ? 1 : 0, now, active ? QuickSeconds : TintOutSeconds, KeycapKeyboardSkin.FadeEasing, instant);
        var lit = active || _hover[index];
        changed |= _legend[index].Set(lit ? 1 : 0, now, active ? QuickSeconds : LegendSeconds, KeycapKeyboardSkin.FadeEasing, instant);
        var glowChanged = _glow[index].Set(down ? 1 : 0, now, down ? QuickSeconds : GlowOutSeconds,
            down ? KeycapKeyboardSkin.Linear : KeycapKeyboardSkin.FadeEasing, instant);
        if (changed || glowChanged)
        {
            _visuals[index].InvalidateVisual();
            if (glowChanged)
            {
                _glowLayer.InvalidateVisual();
            }
            if (_offset[index].Active || _tint[index].Active || _legend[index].Active || _glow[index].Active)
            {
                _loop.Start();
            }
        }
        PseudoClasses.Set(PressingPseudoClass, AnyDown());
    }

    private bool AnyDown()
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            if (IsKeyDown(i))
            {
                return true;
            }
        }
        return false;
    }

    private void InvalidateKeys()
    {
        foreach (var visual in _visuals)
        {
            visual.InvalidateVisual();
        }
    }

    private bool Step(double seconds)
    {
        _clock += seconds;
        var now = _clock;
        var moving = false;
        var glowMoving = false;
        for (var i = 0; i < _keys.Length; i++)
        {
            var changed = _offset[i].Step(now) | _tint[i].Step(now) | _legend[i].Step(now);
            var glow = _glow[i].Step(now);
            if (changed || glow)
            {
                _visuals[i].InvalidateVisual();
            }
            glowMoving |= glow;
            moving |= _offset[i].Active || _tint[i].Active || _legend[i].Active || _glow[i].Active;
        }
        if (glowMoving)
        {
            _glowLayer.InvalidateVisual();
        }
        if (_fading)
        {
            var t = (now - _fadeStart) / RowFadeSeconds;
            var opacity = t >= 1 ? 1 : KeycapKeyboardSkin.FadeEasing.Ease(Math.Max(0, t));
            foreach (var visual in _visuals)
            {
                visual.Opacity = opacity;
            }
            _fading = t < 1;
            moving |= _fading;
        }
        if (_morphing)
        {
            var t = now - _morphStart;
            if (t >= MorphSeconds)
            {
                _morphing = false;
                _plateShown = _plate.Size;
            }
            else
            {
                var w = MorphOmega * t;
                var s = 1 - Math.Exp(-w) * (1 + w);
                _plateShown = new Size(_morphFrom.Width + (_plate.Width - _morphFrom.Width) * s,
                    _morphFrom.Height + (_plate.Height - _morphFrom.Height) * s);
                moving = true;
            }
            InvalidateVisual();
        }
        moving |= _text.Step(now);
        return moving;
    }

    /// <summary>Without frames (reduced motion, off, out of view) everything is drawn where it rests.</summary>
    private void SettleMotion()
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            _offset[i].Settle();
            _tint[i].Settle();
            _legend[i].Settle();
            _glow[i].Settle();
            _visuals[i].Opacity = 1;
            _visuals[i].InvalidateVisual();
        }
        _fading = false;
        _morphing = false;
        _plateShown = _plate.Size;
        _text.SettleArrivals();
        _glowLayer.InvalidateVisual();
        InvalidateVisual();
    }

    // --- typing ---------------------------------------------------------------------------------------------------

    private void Type(string text)
    {
        if (text.Length == 0)
        {
            return;
        }
        _typed = ArcMotion.IsFull(this) && IsLoaded ? text.Length : 0;
        SetCurrentValue(ValueProperty, (Value ?? string.Empty) + text);
        _typed = 0;
        _text.NoteTyping();
    }

    private void DeleteLast()
    {
        var value = Value ?? string.Empty;
        if (value.Length > 0)
        {
            var cut = value.Length >= 2 && char.IsLowSurrogate(value[^1]) && char.IsHighSurrogate(value[^2]) ? 2 : 1;
            SetCurrentValue(ValueProperty, value[..^cut]);
        }
        _text.NoteTyping();
    }

    /// <summary>What a key does when pressed by a click, by Enter or Space on it, or by <see cref="Invoke"/>.</summary>
    private void Activate(int index, bool raise = true)
    {
        var key = _keys[index];
        var modifiers = ActiveModifiers;
        var types = (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0;
        var shifted = (_sticky & KeyModifiers.Shift) != 0;
        if (types)
        {
            switch (key.Kind)
            {
                case KeycapKind.Character:
                case KeycapKind.Space:
                    Type((shifted ? key.ShiftText : key.Text) ?? key.Label);
                    break;
                case KeycapKind.Backspace:
                    DeleteLast();
                    break;
                case KeycapKind.Enter:
                    Type("\n");
                    break;
            }
        }
        if (raise)
        {
            RaiseInvoked(key, modifiers);
        }
        // Delete leaves a latched Shift for the next character; every other key uses the held modifiers up.
        if (_sticky != KeyModifiers.None && key.Kind != KeycapKind.Backspace)
        {
            _sticky = KeyModifiers.None;
            UpdateModifiers();
        }
    }

    /// <summary>A press by pointer, Enter/Space or automation: Shift latches, a modifier holds (when there are bindings), any other key acts.</summary>
    private void Press(int index, bool raise = true)
    {
        var key = _keys[index];
        if (key.Kind == KeycapKind.Shift || (key.IsModifier && Bindings.Count > 0))
        {
            _sticky ^= key.Modifier;
            UpdateModifiers();
            return;
        }
        if (key.IsModifier)
        {
            return;
        }
        Activate(index, raise);
    }

    internal void PointerDown(int index)
    {
        if (!IsKeyboardFocusWithin)
        {
            Focus(NavigationMethod.Pointer);
        }
        _pointerKey = index;
        _pointerModifiers = ActiveModifiers;
        // The key acts on the way down; KeyInvoked keeps its meaning of a click and comes when the pointer lifts over it.
        Press(index, raise: false);
        UpdateKey(index);
    }

    internal void PointerUp(int index, bool over)
    {
        var wasDown = _pointerKey == index;
        _pointerKey = -1;
        if (wasDown && over && !_keys[index].IsModifier)
        {
            RaiseInvoked(_keys[index], _pointerModifiers);
        }
        UpdateKey(index);
    }

    internal void ButtonDown(int index, bool first)
    {
        if (!CanClick(index))
        {
            return;
        }
        if (first)
        {
            _buttonKey = index;
            Press(index);
            UpdateKey(index);
        }
        else if (_keys[index].Kind is KeycapKind.Character or KeycapKind.Space or KeycapKind.Backspace)
        {
            // Holding Enter or Space on a key repeats it, as holding the real key does.
            Activate(index);
        }
    }

    internal void ButtonUp(int index)
    {
        if (_buttonKey == index)
        {
            _buttonKey = -1;
            UpdateKey(index);
        }
    }

    /// <summary>Presses a key once and lets it go a moment later (automation, <see cref="Invoke"/>).</summary>
    internal void Tap(int index)
    {
        if (index < 0 || index >= _keys.Length)
        {
            return;
        }
        Press(index);
        var previous = _tapKey;
        _tapKey = index;
        UpdateKey(previous);
        UpdateKey(index);
        DispatcherTimer.RunOnce(() =>
        {
            if (_tapKey == index && index < _keys.Length)
            {
                _tapKey = -1;
                UpdateKey(index);
            }
        }, TapHold);
    }

    private void UpdateModifiers()
    {
        SetCurrentValue(ActiveModifiersProperty, _sticky | _physical);
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i].IsModifier)
            {
                UpdateKey(i);
            }
        }
    }

    // --- real keyboard ------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateWatch();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ReleaseHeld();
        UpdateWatch();
    }

    private void UpdateWatch()
    {
        var top = Tracking == KeycapTracking.Window && VisualRoot is not null ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(top, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
            _watched.RemoveHandler(KeyUpEvent, OnWindowKeyUp);
            _watched.RemoveHandler(TextInputEvent, OnWindowTextInput);
        }
        _watched = top;
        if (top is not null)
        {
            top.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(TextInputEvent, OnWindowTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    /// <summary>Whether an event comes from inside this keyboard (then its own handlers take it, as with focus).</summary>
    private bool IsInside(object? source) => source is Visual visual && (visual == this || this.IsVisualAncestorOf(visual));

    /// <summary>Whether the focused element takes text itself: window listening never types what is meant for it.</summary>
    private bool IsTextFieldFocused()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is Visual visual && !IsInside(visual) && (visual is TextBox || visual.FindAncestorOfType<TextBox>() is not null);
    }

    private bool HearsOwnEvents => Tracking == KeycapTracking.WhenFocused || Tracking == KeycapTracking.Window;

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsInside(e.Source))
        {
            KeyDownCore(e, !IsTextFieldFocused());
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (!IsInside(e.Source))
        {
            KeyUpCore(e);
        }
    }

    private void OnWindowTextInput(object? sender, TextInputEventArgs e)
    {
        if (!IsInside(e.Source) && !IsTextFieldFocused())
        {
            TextInputCore(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!HearsOwnEvents || e.Handled)
        {
            return;
        }
        KeyDownCore(e, true);
        // Typing plays the board; Tab still moves focus on and shortcuts pass through.
        if (e.Key != Key.Tab && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
        {
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (HearsOwnEvents)
        {
            KeyUpCore(e);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (HearsOwnEvents && !e.Handled)
        {
            TextInputCore(e);
            e.Handled = true;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // Focus may only be moving between keys; let go once it has left the keyboard.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin && Tracking == KeycapTracking.WhenFocused)
            {
                ReleaseHeld();
            }
        });
    }

    private void KeyDownCore(KeyEventArgs e, bool mayType)
    {
        _lastKeyModifiers = e.KeyModifiers;
        var index = KeycapKeyboardLayout.IndexOf(_keys, e.PhysicalKey);
        if (index >= 0 && !_held[index])
        {
            _held[index] = true;
            var key = _keys[index];
            if (key.IsModifier)
            {
                _physical |= key.Modifier;
                UpdateModifiers();
            }
            else
            {
                RaiseInvoked(key, ActiveModifiers);
            }
            UpdateKey(index);
        }
        if (!mayType || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        if (e.Key == Key.Back)
        {
            DeleteLast();
        }
        else if (e.Key == Key.Enter)
        {
            Type("\n");
        }
    }

    private void KeyUpCore(KeyEventArgs e)
    {
        var index = KeycapKeyboardLayout.IndexOf(_keys, e.PhysicalKey);
        if (index < 0 || !_held[index])
        {
            return;
        }
        _held[index] = false;
        if (_keys[index].IsModifier)
        {
            _physical = PhysicalModifiers();
            UpdateModifiers();
        }
        UpdateKey(index);
    }

    private void TextInputCore(TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || (_lastKeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return;
        }
        var text = e.Text;
        var printable = true;
        foreach (var c in text)
        {
            if (char.IsControl(c))
            {
                printable = false;
                break;
            }
        }
        if (!printable)
        {
            return;
        }
        // A real key types what the system gives; it uses up a latched Shift all the same.
        if ((_sticky & KeyModifiers.Shift) != 0)
        {
            _sticky &= ~KeyModifiers.Shift;
            UpdateModifiers();
        }
        Type(text);
    }

    private KeyModifiers PhysicalModifiers()
    {
        var modifiers = KeyModifiers.None;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_held[i])
            {
                modifiers |= _keys[i].Modifier;
            }
        }
        return modifiers;
    }

    /// <summary>Lets go of every key held by the real keyboard (focus left, tracking changed, the board left the window).</summary>
    private void ReleaseHeld()
    {
        if (Array.IndexOf(_held, true) < 0 && _physical == KeyModifiers.None)
        {
            return;
        }
        Array.Clear(_held);
        _physical = KeyModifiers.None;
        UpdateModifiers();
        for (var i = 0; i < _keys.Length; i++)
        {
            UpdateKey(i);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KeycapKeyboardAutomationPeer(this);
}

/// <summary>One animated value of a key: a tween from where it is toward a target, on the keyboard's clock.</summary>
internal struct KeycapTween
{
    private double _from;
    private double _to;
    private double _start;
    private double _duration;
    private Easing? _easing;

    public double Value;

    public bool Active;

    /// <summary>Heads for <paramref name="target"/>; true when anything changed.</summary>
    public bool Set(double target, double now, double duration, Easing easing, bool instant)
    {
        if (target.Equals(_to) && (Active || Value.Equals(target)))
        {
            return false;
        }
        _to = target;
        if (instant || duration <= 0)
        {
            var changed = !Value.Equals(target) || Active;
            Value = target;
            Active = false;
            return changed;
        }
        _from = Value;
        _start = now;
        _duration = duration;
        _easing = easing;
        Active = true;
        return true;
    }

    /// <summary>Advances to <paramref name="now"/>; true when the value moved.</summary>
    public bool Step(double now)
    {
        if (!Active)
        {
            return false;
        }
        var t = (now - _start) / _duration;
        if (t >= 1)
        {
            Value = _to;
            Active = false;
            return true;
        }
        Value = _from + (_to - _from) * (_easing?.Ease(Math.Max(0, t)) ?? t);
        return true;
    }

    public void Settle()
    {
        Value = _to;
        Active = false;
    }
}

/// <summary>The board as a group named by its label, with the lit shortcut and the shown bindings as help text.</summary>
internal sealed class KeycapKeyboardAutomationPeer : ControlAutomationPeer
{
    public KeycapKeyboardAutomationPeer(KeycapKeyboard owner) : base(owner)
    {
    }

    private new KeycapKeyboard Owner => (KeycapKeyboard)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Owner.Label;

    protected override string? GetHelpTextCore() => Owner.Describe();

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Metadata;
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

    /// <summary>The key, by its place on a US keyboard.</summary>
    public PhysicalKey Key { get; }

    /// <summary>The key as it is printed or read: "K", "Enter", "Space".</summary>
    public string KeyName { get; }

    /// <summary>The modifiers held with it.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>The shortcut in <see cref="KeycapKeyboard.Bindings"/> that the key and modifiers make, or null.</summary>
    public KeycapBinding? Binding { get; }
}

/// <summary>
/// A keyboard of keycaps that shows shortcuts. <see cref="Shortcut"/> lights up one combination; <see cref="Bindings"/>
/// print what each key does while its modifiers are held. Keys go down as they are pressed on the real keyboard and can
/// be clicked: a click on a modifier holds it until the next key, and every key raises <see cref="KeyInvoked"/>.
/// </summary>
/// <remarks>
/// The keys follow a compact US layout, 15 key units wide, with an optional function row. The keyboard shrinks its keys
/// to fit the width it is given, and grows them up to its key size. Keys are drawn by this one control.
/// </remarks>
[PseudoClasses(PressingPseudoClass)]
public class KeycapKeyboard : TemplatedControl
{
    internal const string PressingPseudoClass = ":pressing";

    /// <summary>The shortcut to light up, such as "Ctrl+Shift+P" or "⌘K". Null or empty lights up nothing.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string?>(nameof(Shortcut));

    /// <summary>The modifiers held now, by the real keyboard or by clicked modifier keys. Bindings for exactly these show.</summary>
    public static readonly StyledProperty<KeyModifiers> ActiveModifiersProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeyModifiers>(nameof(ActiveModifiers), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which real key presses light up keys.</summary>
    public static readonly StyledProperty<KeycapTracking> TrackingProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeycapTracking>(nameof(Tracking), KeycapTracking.WhenFocused);

    /// <summary>Which labels the modifier keys carry, and in which order they sit.</summary>
    public static readonly StyledProperty<KeycapPlatform> PlatformProperty =
        AvaloniaProperty.Register<KeycapKeyboard, KeycapPlatform>(nameof(Platform));

    /// <summary>Whether the row of Esc and F1 to F12 is shown.</summary>
    public static readonly StyledProperty<bool> ShowsFunctionRowProperty =
        AvaloniaProperty.Register<KeycapKeyboard, bool>(nameof(ShowsFunctionRow));

    /// <summary>Whether keys can be clicked. Real presses still light up when false.</summary>
    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<KeycapKeyboard, bool>(nameof(IsClickable), true);

    /// <summary>Largest width of one key unit in px; the keys shrink below it to fit.</summary>
    public static readonly StyledProperty<double> KeySizeProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>(nameof(KeySize), 44);

    /// <summary>Accessible name of the keyboard.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KeycapKeyboard, string>(nameof(Label), "Keyboard");

    /// <summary>Raised when a key is clicked, or pressed on the real keyboard while it is tracked. Modifier keys alone do not raise it.</summary>
    public static readonly RoutedEvent<KeycapKeyInvokedEventArgs> KeyInvokedEvent =
        RoutedEvent.Register<KeycapKeyboard, KeycapKeyInvokedEventArgs>(nameof(KeyInvoked), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> KeyGapProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("KeyGap", 5);

    internal static readonly StyledProperty<double> KeyDepthProperty =
        AvaloniaProperty.Register<KeycapKeyboard, double>("KeyDepth", 3);

    internal static readonly StyledProperty<CornerRadius> KeyCornerRadiusProperty =
        AvaloniaProperty.Register<KeycapKeyboard, CornerRadius>("KeyCornerRadius");

    internal static readonly StyledProperty<IBrush?> KeyFaceProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("KeyFace");

    internal static readonly StyledProperty<IBrush?> KeyHoverFaceProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("KeyHoverFace");

    internal static readonly StyledProperty<IBrush?> KeySkirtProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("KeySkirt");

    internal static readonly StyledProperty<IBrush?> KeyEdgeProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("KeyEdge");

    internal static readonly StyledProperty<IBrush?> HighlightFaceProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("HighlightFace");

    internal static readonly StyledProperty<IBrush?> HighlightSkirtProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("HighlightSkirt");

    internal static readonly StyledProperty<IBrush?> HighlightTextProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("HighlightText");

    internal static readonly StyledProperty<IBrush?> BindingFaceProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("BindingFace");

    internal static readonly StyledProperty<IBrush?> BindingTextProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("BindingText");

    internal static readonly StyledProperty<IBrush?> MutedTextProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("MutedText");

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<KeycapKeyboard, IBrush?>("FocusBrush");

    /// <summary>How fast a key goes down and comes back: the share of the remaining travel per 1/60 s.</summary>
    private const double PressRate = 0.45;

    private readonly LiveLoop _loop;
    private KeycapKeyboardKey[] _keys = [];
    private double[] _press = [];
    private bool[] _held = [];
    private bool[] _lit = [];
    private KeycapBinding?[] _bindingOf = [];
    private TextLayout?[] _labels = [];
    private TextLayout?[] _bindingLabels = [];
    private Pen? _edgePen;
    private Pen? _focusPen;
    private double _unit;
    private Point _offset;
    private int _pointerKey = -1;
    private int _hover = -1;
    private KeyModifiers _sticky;
    private KeyModifiers _physical;
    private TopLevel? _watched;
    private double _builtUnit = double.NaN;

    // Theme-set values, read through these so the XAML setters can name them.
    internal double KeyGap => GetValue(KeyGapProperty);
    internal double KeyDepth => GetValue(KeyDepthProperty);
    internal CornerRadius KeyCornerRadius => GetValue(KeyCornerRadiusProperty);
    internal IBrush? KeyFace => GetValue(KeyFaceProperty);
    internal IBrush? KeyHoverFace => GetValue(KeyHoverFaceProperty);
    internal IBrush? KeySkirt => GetValue(KeySkirtProperty);
    internal IBrush? KeyEdge => GetValue(KeyEdgeProperty);
    internal IBrush? HighlightFace => GetValue(HighlightFaceProperty);
    internal IBrush? HighlightSkirt => GetValue(HighlightSkirtProperty);
    internal IBrush? HighlightText => GetValue(HighlightTextProperty);
    internal IBrush? BindingFace => GetValue(BindingFaceProperty);
    internal IBrush? BindingText => GetValue(BindingTextProperty);
    internal IBrush? MutedText => GetValue(MutedTextProperty);
    internal IBrush? FocusBrush => GetValue(FocusBrushProperty);

    static KeycapKeyboard()
    {
        FocusableProperty.OverrideDefaultValue<KeycapKeyboard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<KeycapKeyboard>(false);
        AffectsMeasure<KeycapKeyboard>(KeySizeProperty, ShowsFunctionRowProperty, PaddingProperty);
        AffectsRender<KeycapKeyboard>(BackgroundProperty, KeyFaceProperty, KeyHoverFaceProperty, KeySkirtProperty,
            HighlightFaceProperty, HighlightSkirtProperty, BindingFaceProperty, KeyCornerRadiusProperty, KeyDepthProperty,
            CornerRadiusProperty);
    }

    public KeycapKeyboard()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Full };
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                SettlePresses();
            }
        };
        Bindings.CollectionChanged += OnBindingsChanged;
        Rebuild();
    }

    /// <summary>The shortcuts whose labels show on their keys while their modifiers are held.</summary>
    [Content]
    public AvaloniaList<KeycapBinding> Bindings { get; } = [];

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

    /// <summary>Raised when a key is clicked, or pressed on the real keyboard while it is tracked. Modifier keys alone do not raise it.</summary>
    public event EventHandler<KeycapKeyInvokedEventArgs>? KeyInvoked
    {
        add => AddHandler(KeyInvokedEvent, value);
        remove => RemoveHandler(KeyInvokedEvent, value);
    }

    /// <summary>Whether the keys carry Mac labels.</summary>
    internal bool IsMac => Platform switch
    {
        KeycapPlatform.Mac => true,
        KeycapPlatform.Windows => false,
        _ => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()
    };

    internal IReadOnlyList<KeycapKeyboardKey> Keys => _keys;

    /// <summary>Whether a key is lit by <see cref="Shortcut"/> (tests).</summary>
    internal bool IsLit(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i && _lit[i];

    /// <summary>Whether a key is down: held by the real keyboard or the pointer, or a clicked modifier that holds (tests).</summary>
    internal bool IsDown(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i && IsKeyDown(i);

    /// <summary>How far a key is drawn pressed, 0 to 1 (tests).</summary>
    internal double PressOf(PhysicalKey key) => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i ? _press[i] : 0;

    /// <summary>The binding label a key shows now, or null (tests).</summary>
    internal string? BindingLabelOf(PhysicalKey key)
        => KeycapKeyboardLayout.IndexOf(_keys, key) is >= 0 and var i ? _bindingOf[i]?.Label : null;

    /// <summary>The box of a key in this control's coordinates.</summary>
    internal Rect KeyBounds(int index)
    {
        var key = _keys[index];
        var gap = GetValue(KeyGapProperty);
        return new Rect(_offset.X + key.X * _unit + gap / 2, _offset.Y + key.Y * _unit + gap / 2,
            Math.Max(0, key.Width * _unit - gap), Math.Max(0, key.Height * _unit - gap));
    }

    /// <summary>Presses a key as a click would: a modifier holds until the next key; any other key raises <see cref="KeyInvoked"/>.</summary>
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
        RaiseInvoked(entry, ActiveModifiers);
        if (_sticky != KeyModifiers.None)
        {
            _sticky = KeyModifiers.None;
            UpdateModifiers();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlatformProperty || change.Property == ShowsFunctionRowProperty)
        {
            Rebuild();
        }
        else if (change.Property == ShortcutProperty)
        {
            UpdateLit();
        }
        else if (change.Property == ActiveModifiersProperty)
        {
            UpdateBindingLabels();
        }
        else if (change.Property == TrackingProperty)
        {
            ReleaseAll();
            UpdateWatch();
        }
        else if (change.Property == FontFamilyProperty || change.Property == ForegroundProperty ||
                 change.Property == HighlightTextProperty || change.Property == BindingTextProperty ||
                 change.Property == MutedTextProperty)
        {
            _builtUnit = double.NaN;
            BuildText();
        }
        else if (change.Property == KeyEdgeProperty || change.Property == FocusBrushProperty)
        {
            _edgePen = null;
            _focusPen = null;
        }
        else if (change.Property == KeyGapProperty)
        {
            InvalidateArrange();
        }
    }

    // --- layout ---------------------------------------------------------------------------------------------------

    private void Rebuild()
    {
        _keys = KeycapKeyboardLayout.Keys(IsMac, ShowsFunctionRow);
        _press = new double[_keys.Length];
        _held = new bool[_keys.Length];
        _lit = new bool[_keys.Length];
        _bindingOf = new KeycapBinding?[_keys.Length];
        DisposeText();
        _labels = new TextLayout?[_keys.Length];
        _bindingLabels = new TextLayout?[_keys.Length];
        _pointerKey = -1;
        _hover = -1;
        _builtUnit = double.NaN;
        UpdateLit();
        UpdateBindingLabels();
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var rows = KeycapKeyboardLayout.Rows(ShowsFunctionRow);
        var unit = KeySize;
        if (double.IsFinite(availableSize.Width))
        {
            unit = Math.Min(unit, Math.Max(12, (availableSize.Width - padding.Left - padding.Right) / KeycapKeyboardLayout.Columns));
        }
        return new Size(unit * KeycapKeyboardLayout.Columns + padding.Left + padding.Right, unit * rows + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var rows = KeycapKeyboardLayout.Rows(ShowsFunctionRow);
        var inner = finalSize.Deflate(padding);
        _unit = Math.Max(1, Math.Min(Math.Min(KeySize, inner.Width / KeycapKeyboardLayout.Columns), inner.Height / rows));
        var width = _unit * KeycapKeyboardLayout.Columns;
        var height = _unit * rows;
        _offset = new Point(padding.Left + (inner.Width - width) / 2, padding.Top + (inner.Height - height) / 2);
        if (!_unit.Equals(_builtUnit))
        {
            BuildText();
        }
        return finalSize;
    }

    private double LabelSize => Math.Clamp(_unit * 0.3, 9, 14);

    private double BindingSize => Math.Clamp(_unit * 0.21, 7, 10);

    private Typeface Face(FontWeight weight) => new(FontFamily, FontStyle.Normal, weight);

    private void DisposeText()
    {
        foreach (var layout in _labels)
        {
            layout?.Dispose();
        }
        foreach (var layout in _bindingLabels)
        {
            layout?.Dispose();
        }
    }

    /// <summary>Lays out every key's label for the current key size and colors (outside the render pass).</summary>
    private void BuildText()
    {
        if (_unit <= 0 || _keys.Length == 0)
        {
            return;
        }
        _builtUnit = _unit;
        var size = LabelSize;
        for (var i = 0; i < _keys.Length; i++)
        {
            _labels[i]?.Dispose();
            var key = _keys[i];
            var brush = _lit[i] ? GetValue(HighlightTextProperty) ?? Foreground : key.IsWide ? GetValue(MutedTextProperty) ?? Foreground : Foreground;
            var keySize = key.IsWide || key.Label.Length > 1 ? size * 0.86 : size;
            _labels[i] = key.Label.Length == 0
                ? null
                : new TextLayout(key.Label, Face(FontWeight.Medium), keySize, brush, TextAlignment.Left, TextWrapping.NoWrap,
                    TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, key.Width * _unit - 10));
        }
        BuildBindingText();
        InvalidateVisual();
    }

    private void BuildBindingText()
    {
        if (_unit <= 0)
        {
            return;
        }
        var brush = GetValue(BindingTextProperty) ?? Foreground;
        for (var i = 0; i < _keys.Length; i++)
        {
            _bindingLabels[i]?.Dispose();
            _bindingLabels[i] = _bindingOf[i]?.Label is { Length: > 0 } text
                ? new TextLayout(text, Face(FontWeight.Medium), BindingSize, brush, TextAlignment.Center, TextWrapping.NoWrap,
                    TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, Math.Max(1, _keys[i].Width * _unit - 8))
                : null;
        }
    }

    // --- shortcut and bindings ------------------------------------------------------------------------------------

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
        _builtUnit = double.NaN;
        BuildText();
        InvalidateVisual();

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
        BuildBindingText();
        InvalidateVisual();
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

    private void UpdateModifiers()
    {
        var modifiers = _sticky | _physical;
        SetCurrentValue(ActiveModifiersProperty, modifiers);
        // Held modifier keys stay down on screen while they hold.
        for (var i = 0; i < _keys.Length; i++)
        {
            var key = _keys[i];
            if (key.IsModifier && IsLeftOf(key))
            {
                SetTarget(i, _held[i] || (_sticky.HasFlag(key.Modifier)) || _pointerKey == i);
            }
        }
    }

    /// <summary>Sticky modifiers show on the left-hand key only.</summary>
    private static bool IsLeftOf(KeycapKeyboardKey key)
        => key.Physical is PhysicalKey.ControlLeft or PhysicalKey.ShiftLeft or PhysicalKey.AltLeft or PhysicalKey.MetaLeft;

    private void RaiseInvoked(KeycapKeyboardKey key, KeyModifiers modifiers)
        => RaiseEvent(new KeycapKeyInvokedEventArgs(KeyInvokedEvent, key.Physical, KeycapKeyboardLayout.NameOf(key), modifiers,
            BindingFor(key.Physical, modifiers)));

    // --- press animation ------------------------------------------------------------------------------------------

    private void SetTarget(int index, bool down)
    {
        var target = down ? 1.0 : 0.0;
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _press[index] = target;
            InvalidateVisual();
            return;
        }
        if (!_press[index].Equals(target))
        {
            _loop.Start();
        }
    }

    private bool IsKeyDown(int index)
    {
        var key = _keys[index];
        return _held[index] || _pointerKey == index || (key.IsModifier && IsLeftOf(key) && _sticky.HasFlag(key.Modifier));
    }

    private bool Step(double seconds)
    {
        var keep = 1 - Math.Pow(1 - PressRate, seconds * 60);
        var moving = false;
        for (var i = 0; i < _press.Length; i++)
        {
            var target = IsKeyDown(i) ? 1.0 : 0.0;
            var value = _press[i];
            if (value.Equals(target))
            {
                continue;
            }
            value += (target - value) * keep;
            if (Math.Abs(target - value) < 0.01)
            {
                value = target;
            }
            else
            {
                moving = true;
            }
            _press[i] = value;
        }
        InvalidateVisual();
        return moving;
    }

    /// <summary>Without frames (reduced motion, off, out of view) keys are drawn where they rest.</summary>
    private void SettlePresses()
    {
        for (var i = 0; i < _press.Length; i++)
        {
            _press[i] = IsKeyDown(i) ? 1 : 0;
        }
        InvalidateVisual();
    }

    // --- real keyboard --------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateWatch();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ReleaseAll();
        UpdateWatch();
    }

    private void UpdateWatch()
    {
        var top = Tracking == KeycapTracking.Window ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(top, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
            _watched.RemoveHandler(KeyUpEvent, OnWindowKeyUp);
        }
        _watched = top;
        if (top is not null)
        {
            top.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            top.AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e) => TrackDown(e);

    private void OnWindowKeyUp(object? sender, KeyEventArgs e) => TrackUp(e);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Tracking != KeycapTracking.WhenFocused)
        {
            return;
        }
        TrackDown(e);
        // Typing on the keyboard plays it; only Tab still moves focus on.
        if (e.Key != Key.Tab)
        {
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (Tracking == KeycapTracking.WhenFocused)
        {
            TrackUp(e);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (Tracking == KeycapTracking.WhenFocused)
        {
            ReleaseAll();
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    private void TrackDown(KeyEventArgs e)
    {
        var index = KeycapKeyboardLayout.IndexOf(_keys, e.PhysicalKey);
        if (index < 0 || _held[index])
        {
            return;
        }
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
        SetTarget(index, true);
        PseudoClasses.Set(PressingPseudoClass, true);
    }

    private void TrackUp(KeyEventArgs e)
    {
        var index = KeycapKeyboardLayout.IndexOf(_keys, e.PhysicalKey);
        if (index < 0 || !_held[index])
        {
            return;
        }
        _held[index] = false;
        var key = _keys[index];
        if (key.IsModifier)
        {
            _physical = PhysicalModifiers();
            UpdateModifiers();
        }
        SetTarget(index, IsKeyDown(index));
        PseudoClasses.Set(PressingPseudoClass, Array.IndexOf(_held, true) >= 0);
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

    /// <summary>Lets go of every held key (focus left, tracking changed, the keyboard left the window).</summary>
    private void ReleaseAll()
    {
        Array.Clear(_held);
        _physical = KeyModifiers.None;
        _pointerKey = -1;
        PseudoClasses.Set(PressingPseudoClass, false);
        UpdateModifiers();
        for (var i = 0; i < _keys.Length; i++)
        {
            SetTarget(i, IsKeyDown(i));
        }
    }

    // --- pointer --------------------------------------------------------------------------------------------------

    private int KeyAt(Point point)
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            if (KeyBounds(i).Contains(point))
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(NavigationMethod.Pointer);
        if (!IsClickable || !IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var index = KeyAt(e.GetPosition(this));
        if (index < 0)
        {
            return;
        }
        _pointerKey = index;
        e.Pointer.Capture(this);
        SetTarget(index, true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var index = _pointerKey;
        if (index < 0)
        {
            return;
        }
        _pointerKey = -1;
        e.Pointer.Capture(null);
        var over = KeyAt(e.GetPosition(this)) == index;
        if (over)
        {
            Invoke(_keys[index].Physical);
        }
        SetTarget(index, IsKeyDown(index));
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pointerKey >= 0)
        {
            var index = _pointerKey;
            _pointerKey = -1;
            SetTarget(index, IsKeyDown(index));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var index = IsClickable ? KeyAt(e.GetPosition(this)) : -1;
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KeycapKeyboardAutomationPeer(this);

    /// <summary>The help text read with the keyboard: the lit shortcut and the shortcuts shown now.</summary>
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

    // --- drawing --------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var plate = CornerRadius;
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, bounds, plate.TopLeft, plate.TopLeft);
        }
        if (_unit <= 0)
        {
            return;
        }
        var depth = Math.Min(GetValue(KeyDepthProperty), _unit * 0.1);
        var radius = Math.Min(GetValue(KeyCornerRadiusProperty).TopLeft, _unit * 0.25);
        var face = GetValue(KeyFaceProperty);
        var hoverFace = GetValue(KeyHoverFaceProperty) ?? face;
        var skirt = GetValue(KeySkirtProperty);
        var litFace = GetValue(HighlightFaceProperty);
        var litSkirt = GetValue(HighlightSkirtProperty) ?? litFace;
        var bindingFace = GetValue(BindingFaceProperty) ?? face;
        if (GetValue(KeyEdgeProperty) is { } edge)
        {
            _edgePen ??= new Pen(edge, 1);
        }
        for (var i = 0; i < _keys.Length; i++)
        {
            var box = KeyBounds(i);
            if (box.Width <= 0 || box.Height <= depth)
            {
                continue;
            }
            var press = _press[i];
            var lit = _lit[i];
            var top = new Rect(box.X, box.Y + depth * press, box.Width, box.Height - depth);
            context.DrawRectangle(lit ? litSkirt : skirt, null, box, radius, radius);
            var fill = lit ? litFace : _bindingOf[i] is not null ? bindingFace : i == _hover ? hoverFace : face;
            context.DrawRectangle(fill, lit ? null : _edgePen, top, radius, radius);
            DrawLabel(context, i, top);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && GetValue(FocusBrushProperty) is { } focus)
        {
            _focusPen ??= new Pen(focus, 2);
            context.DrawRectangle(null, _focusPen, bounds.Inflate(3), plate.TopLeft + 3, plate.TopLeft + 3);
        }
    }

    private void DrawLabel(DrawingContext context, int index, Rect top)
    {
        var key = _keys[index];
        var binding = _bindingLabels[index];
        if (_labels[index] is { } label)
        {
            var width = label.WidthIncludingTrailingWhitespace;
            Point at;
            if (key.IsWide)
            {
                // Wide keys print their word at the bottom left, as on real keycaps.
                at = new Point(top.X + 6, top.Bottom - label.Height - 4);
            }
            else if (binding is not null)
            {
                at = new Point(top.X + (top.Width - width) / 2, top.Y + 3);
            }
            else
            {
                at = new Point(top.X + (top.Width - width) / 2, top.Y + (top.Height - label.Height) / 2);
            }
            label.Draw(context, at);
        }
        if (binding is not null)
        {
            var y = key.IsWide ? top.Y + 4 : top.Bottom - binding.Height - 3;
            binding.Draw(context, new Point(top.X + 4, y));
        }
    }
}

/// <summary>The keyboard as a group named by its label, with the lit shortcut and the shown bindings as help text.</summary>
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

using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Text format of a <see cref="ColorPicker"/> value field.</summary>
public enum ColorPickerFormat
{
    /// <summary>#RRGGBB, or #RRGGBBAA when translucent (CSS order).</summary>
    Hex,

    /// <summary>rgb(47 107 255 / 80%).</summary>
    Rgb,

    /// <summary>hsl(223 100% 59%).</summary>
    Hsl,

    /// <summary>oklch(56.2% 0.214 263.5).</summary>
    Oklch
}

/// <summary>A saved swatch. <see cref="Id"/> keeps its identity while the row is reordered.</summary>
public sealed record ColorSwatch(string Id, Color Color)
{
    private static int s_count;

    /// <summary>A swatch with a fresh id.</summary>
    public static ColorSwatch Create(Color color) =>
        new($"swatch-{DateTime.UtcNow.Ticks:x}-{Interlocked.Increment(ref s_count)}", color);
}

/// <summary>Raised when <see cref="ColorPicker.Color"/> changes.</summary>
public sealed class ColorPickerColorChangedEventArgs : RoutedEventArgs
{
    public ColorPickerColorChangedEventArgs(RoutedEvent routedEvent, Color oldColor, Color newColor, string hex)
        : base(routedEvent)
    {
        OldColor = oldColor;
        NewColor = newColor;
        Hex = hex;
    }

    public Color OldColor { get; }

    public Color NewColor { get; }

    /// <summary>The new color as upper-case CSS hex (#RRGGBB, or #RRGGBBAA when translucent): Arc's onValueChange value.</summary>
    public string Hex { get; }
}

/// <summary>Raised when the saved swatches change (save, reorder, remove).</summary>
public sealed class ColorPickerSwatchesChangedEventArgs : RoutedEventArgs
{
    public ColorPickerSwatchesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ColorSwatch> swatches)
        : base(routedEvent) => Swatches = swatches;

    public IReadOnlyList<ColorSwatch> Swatches { get; }
}

/// <summary>
/// Arc color field: a swatch whose box grows into the full picker (a clipped reveal on a spring, its shadow following
/// the shape). Pick saturation and brightness on the area, hue and opacity on the sliders, or type a value in hex,
/// RGB, HSL or OKLCH; the format button morphs its label and the value between formats. A contrast readout counts
/// to the WCAG ratio against <see cref="ContrastBackground"/> with its grade, and saved swatches pop in, reorder by
/// drag or Alt + arrows and go with Delete. Arrow keys move every thumb (Shift and Page keys by ten); Escape drops a
/// draft, then closes and returns focus. The eyedropper is not offered: Avalonia has no cross-platform screen color API.
/// </summary>
[TemplatePart(TriggerPart, typeof(Control))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[PseudoClasses(OpenPseudoClass)]
public class ColorPicker : TemplatedControl, IPickerAutomation
{
    public const string TriggerPart = "PART_Trigger";
    public const string PopupPart = "PART_Popup";
    public const string OpenPseudoClass = ":open";

    internal static readonly SpringSpec OpenSpring = SpringSpec.FromVisual(0.42, 0.1);
    internal static readonly SpringSpec CloseSpring = SpringSpec.FromVisual(0.3, 0);
    private const double TriggerRadius = 22;
    private const double PanelRadius = 26;

    #region Properties

    /// <summary>The color (two-way). Arc's default is #2F6BFF.</summary>
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<ColorPicker, Color>(nameof(Color), Color.Parse("#2F6BFF"), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The color as upper-case CSS hex (#RRGGBB, or #RRGGBBAA when translucent).</summary>
    public static readonly DirectProperty<ColorPicker, string> HexValueProperty =
        AvaloniaProperty.RegisterDirect<ColorPicker, string>(nameof(HexValue), o => o.HexValue);

    /// <summary>The background the color will sit on, for the contrast readout (Arc: white).</summary>
    public static readonly StyledProperty<Color> ContrastBackgroundProperty =
        AvaloniaProperty.Register<ColorPicker, Color>(nameof(ContrastBackground), Colors.White);

    /// <summary>Name shown on the swatch, such as "Accent" (Arc: "Color").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        Field.LabelProperty.AddOwner<ColorPicker>(new StyledPropertyMetadata<string?>("Color"));

    public static readonly StyledProperty<string?> DescriptionProperty = Field.DescriptionProperty.AddOwner<ColorPicker>();
    public static readonly StyledProperty<string?> ErrorMessageProperty = Field.ErrorMessageProperty.AddOwner<ColorPicker>();

    /// <summary>Format of the value field; the format button cycles it.</summary>
    public static readonly StyledProperty<ColorPickerFormat> FormatProperty =
        AvaloniaProperty.Register<ColorPicker, ColorPickerFormat>(nameof(Format), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Saved swatches, newest first (two-way; every change produces a new list).</summary>
    public static readonly StyledProperty<IReadOnlyList<ColorSwatch>?> SwatchesProperty =
        AvaloniaProperty.Register<ColorPicker, IReadOnlyList<ColorSwatch>?>(nameof(Swatches), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Most saved swatches; saving past it drops the oldest (Arc: 7).</summary>
    public static readonly StyledProperty<int> MaxSwatchesProperty =
        AvaloniaProperty.Register<ColorPicker, int>(nameof(MaxSwatches), 7);

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<ColorPicker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shown under the value field for text that is not a color.</summary>
    public static readonly StyledProperty<string> InvalidColorTextProperty =
        AvaloniaProperty.Register<ColorPicker, string>(nameof(InvalidColorText), "Enter a hex, RGB, HSL, or OKLCH color");

    /// <summary>Text after the contrast ratio that compares the color with the background (default "against background").</summary>
    public static readonly StyledProperty<string> AgainstBackgroundTextProperty =
        AvaloniaProperty.Register<ColorPicker, string>(nameof(AgainstBackgroundText), "against background");

    /// <summary>Label of the Done button that closes the panel (default "Done").</summary>
    public static readonly StyledProperty<string> DoneTextProperty =
        AvaloniaProperty.Register<ColorPicker, string>(nameof(DoneText), "Done");

    public static readonly RoutedEvent<ColorPickerColorChangedEventArgs> ColorChangedEvent =
        RoutedEvent.Register<ColorPicker, ColorPickerColorChangedEventArgs>(nameof(ColorChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<ColorPickerSwatchesChangedEventArgs> SwatchesChangedEvent =
        RoutedEvent.Register<ColorPicker, ColorPickerSwatchesChangedEventArgs>(nameof(SwatchesChanged), RoutingStrategies.Bubble);

    /// <summary>The color.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    private string _hexValue = "#2F6BFF";

    /// <summary>The color as upper-case CSS hex.</summary>
    public string HexValue { get => _hexValue; private set => SetAndRaise(HexValueProperty, ref _hexValue, value); }

    /// <summary>The background for the contrast readout.</summary>
    public Color ContrastBackground { get => GetValue(ContrastBackgroundProperty); set => SetValue(ContrastBackgroundProperty, value); }

    /// <summary>Name shown on the swatch.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the swatch.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the swatch.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Format of the value field.</summary>
    public ColorPickerFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>Saved swatches.</summary>
    public IReadOnlyList<ColorSwatch>? Swatches { get => GetValue(SwatchesProperty); set => SetValue(SwatchesProperty, value); }

    /// <summary>Most saved swatches.</summary>
    public int MaxSwatches { get => GetValue(MaxSwatchesProperty); set => SetValue(MaxSwatchesProperty, value); }

    /// <summary>Whether the panel is open.</summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    public string InvalidColorText { get => GetValue(InvalidColorTextProperty); set => SetValue(InvalidColorTextProperty, value); }
    /// <inheritdoc cref="AgainstBackgroundTextProperty"/>
    public string AgainstBackgroundText { get => GetValue(AgainstBackgroundTextProperty); set => SetValue(AgainstBackgroundTextProperty, value); }
    /// <inheritdoc cref="DoneTextProperty"/>
    public string DoneText { get => GetValue(DoneTextProperty); set => SetValue(DoneTextProperty, value); }

    /// <summary>Raised when <see cref="Color"/> changes.</summary>
    public event EventHandler<ColorPickerColorChangedEventArgs>? ColorChanged
    {
        add => AddHandler(ColorChangedEvent, value);
        remove => RemoveHandler(ColorChangedEvent, value);
    }

    /// <summary>Raised when the saved swatches change.</summary>
    public event EventHandler<ColorPickerSwatchesChangedEventArgs>? SwatchesChanged
    {
        add => AddHandler(SwatchesChangedEvent, value);
        remove => RemoveHandler(SwatchesChangedEvent, value);
    }

    // Theme-provided (token) colors.
    internal static readonly StyledProperty<IBrush?> PanelFillProperty = AvaloniaProperty.Register<ColorPicker, IBrush?>(nameof(PanelFill));
    internal static readonly StyledProperty<IBrush?> PanelBorderProperty = AvaloniaProperty.Register<ColorPicker, IBrush?>(nameof(PanelBorder));
    internal static readonly StyledProperty<BoxShadows> FloatShadowProperty = AvaloniaProperty.Register<ColorPicker, BoxShadows>(nameof(FloatShadow));

    internal IBrush? PanelFill { get => GetValue(PanelFillProperty); set => SetValue(PanelFillProperty, value); }
    internal IBrush? PanelBorder { get => GetValue(PanelBorderProperty); set => SetValue(PanelBorderProperty, value); }
    internal BoxShadows FloatShadow { get => GetValue(FloatShadowProperty); set => SetValue(FloatShadowProperty, value); }

    #endregion

    private static readonly string[] s_formatNames = ["Hex", "RGB", "HSL", "OKLCH"];

    private ColorPickerHsva _hsva;
    private bool _syncingColor;
    private bool _syncingInput;
    private Control? _trigger;
    private PickerMorphPopup? _popup;
    private PickerMorphSurface? _surface;
    private ColorPickerPanel? _panel;
    private PickerAutomationPeer? _peer;
    private PickerShake? _shake;
    private readonly TranslateTransform _bodyShift = new();
    private MotionValue<double>? _fade;
    private PickerRevealScroll? _reveal;
    private double _shift;
    private double _rise;
    private string? _draft;
    private bool _invalid;
    private int _generation;
    private bool _keyboardOpen;
    private bool _pressedOnTrigger;
    private TopLevel? _outsideTop;
    private TextMorph? _morph;
    private IDisposable? _morphTimer;
    private bool _morphing;
    private string _shownText = string.Empty;
    private readonly SolidColorBrush _sampleBackground = new(Colors.White);
    private readonly SolidColorBrush _sampleForeground = new(Colors.Black);

    static ColorPicker()
    {
        FocusableProperty.OverrideDefaultValue<ColorPicker>(true);
        FocusAdornerProperty.OverrideDefaultValue<ColorPicker>(null);
    }

    public ColorPicker()
    {
        _hsva = ColorPickerMath.FromColor(Color, 220);
        _hexValue = ColorPickerMath.ToHex(_hsva);
    }

    internal ColorPickerPanel? Panel => _panel;

    internal PickerMorphSurface? Surface => _surface;

    internal ArcPopup? Popup => _popup;

    internal ColorPickerHsva Hsva => _hsva;

    /// <summary>The value field's text in the current format (what the field shows without a draft).</summary>
    public string FormattedValue => ColorPickerMath.Format(_hsva, Format);

    /// <summary>Contrast of the color over <see cref="ContrastBackground"/> (WCAG ratio, 1 to 21).</summary>
    public double ContrastRatio => ColorPickerMath.Contrast(_hsva, ColorPickerMath.Rgba(ContrastBackground));

    /// <summary>WCAG grade of <see cref="ContrastRatio"/>: AAA, AA, AA large or Fails.</summary>
    public string ContrastGrade => ColorPickerMath.Level(ContrastRatio);

    string? IPickerAutomation.AutomationLabel => Label;

    string? IPickerAutomation.AutomationValue => HexValue;

    bool IPickerAutomation.AutomationIsOpen
    {
        get => IsDropDownOpen;
        set => SetCurrentValue(IsDropDownOpenProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new PickerAutomationPeer(this, this, AutomationControlType.Button);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
            _trigger.PointerCaptureLost -= OnTriggerCaptureLost;
        }
        if (_popup is not null)
        {
            _popup.Child = null;
        }
        _trigger = e.NameScope.Find<Control>(TriggerPart);
        _popup = e.NameScope.Find<PickerMorphPopup>(PopupPart);
        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
            _trigger.PointerCaptureLost += OnTriggerCaptureLost;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            if (_surface is not null)
            {
                _popup.Child = _surface;
            }
        }
        UpdateAutomation();
        if (IsDropDownOpen)
        {
            OpenPanel();
        }
    }

    #endregion

    #region Value

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty)
        {
            var oldColor = change.GetOldValue<Color>();
            var newColor = change.GetNewValue<Color>();
            if (!_syncingColor)
            {
                var hex = ColorPickerMath.ToHex(ColorPickerMath.FromColor(newColor));
                if (hex != ColorPickerMath.ToHex(_hsva))
                {
                    _hsva = ColorPickerMath.FromColor(newColor, _hsva.H);
                }
            }
            HexValue = ColorPickerMath.ToHex(_hsva);
            UpdateVisuals(animate: true);
            UpdateAutomation();
            RaiseEvent(new ColorPickerColorChangedEventArgs(ColorChangedEvent, oldColor, newColor, HexValue));
        }
        else if (change.Property == FormatProperty)
        {
            OnFormatChanged(change.GetOldValue<ColorPickerFormat>(), change.GetNewValue<ColorPickerFormat>());
        }
        else if (change.Property == SwatchesProperty)
        {
            _panel?.Swatches?.Sync(Swatches ?? [], HexValue, animate: true);
        }
        else if (change.Property == ContrastBackgroundProperty)
        {
            UpdateVisuals(animate: true);
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenPanel();
            }
            else
            {
                ClosePanel(returnFocus: _closeReturnsFocus);
                _closeReturnsFocus = false;
            }
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAutomation();
            if (_panel?.HeadName is { } name)
            {
                name.Text = Label;
            }
        }
        else if (change.Property == PanelFillProperty || change.Property == PanelBorderProperty || change.Property == FloatShadowProperty)
        {
            StyleSurface();
        }
        else if (change.Property == AgainstBackgroundTextProperty || change.Property == InvalidColorTextProperty || change.Property == DoneTextProperty)
        {
            UpdatePanelText();
        }
    }

    /// <summary>Commits a picked color: the value, the hex and every thumb follow.</summary>
    internal void Commit(ColorPickerHsva next)
    {
        _hsva = next;
        var color = ColorPickerMath.ToColor(next);
        if (color == Color)
        {
            // Same Color (a hue change on a gray, a sub-byte move): still move the thumbs.
            HexValue = ColorPickerMath.ToHex(_hsva);
            UpdateVisuals(animate: true);
            return;
        }
        _syncingColor = true;
        try
        {
            SetCurrentValue(ColorProperty, color);
        }
        finally
        {
            _syncingColor = false;
        }
    }

    private void UpdateVisuals(bool animate)
    {
        if (_panel is null)
        {
            return;
        }
        var color = ColorPickerMath.ToColor(_hsva);
        var opaque = ColorPickerMath.Opaque(_hsva);
        if (_panel.HeadChip is { } chip)
        {
            chip.Color = color;
        }
        if (_panel.HeadHex is { } hex)
        {
            hex.Text = HexValue;
        }
        var shown = IsLoaded && _popup?.IsOpen == true && animate;
        _panel.Area?.SetColors(_hsva.H, opaque, color);
        _panel.Area?.SetValue(new Point(_hsva.S, 1 - _hsva.V), shown);
        _panel.Hue?.SetColors(_hsva.H, opaque, color);
        _panel.Hue?.SetValue(new Point(_hsva.H / 360, 0), shown);
        _panel.Alpha?.SetColors(_hsva.H, opaque, color);
        _panel.Alpha?.SetValue(new Point(_hsva.A, 0), shown);
        if (_draft is null)
        {
            SyncInput();
        }
        var ratio = ContrastRatio;
        var grade = ColorPickerMath.Level(ratio);
        _panel.Ratio?.SetRatio(ratio, shown);
        _panel.Grade?.Show(grade, placeholder: false, direction: 1, shown);
        _panel.SetGrade(grade);
        if (_panel.Sample is { } sample)
        {
            _sampleBackground.Color = ContrastBackground;
            sample.Background = _sampleBackground;
        }
        if (_panel.SampleText is { } aa)
        {
            _sampleForeground.Color = color;
            aa.Foreground = _sampleForeground;
        }
        if (_panel.ContrastText is { } live)
        {
            live.Text = $"Contrast {ratio.ToString("0.00", CultureInfo.InvariantCulture)} to 1, {(grade == "Fails" ? "fails" : $"passes {grade}")}";
        }
        _panel.Swatches?.SetCurrentHex(HexValue, shown);
        if (_panel.AddButton is { } add)
        {
            AutomationProperties.SetName(add, $"Save {HexValue}");
        }
    }

    private void SyncInput()
    {
        _shownText = ColorPickerMath.Format(_hsva, Format);
        if (_panel?.Input is not { } input)
        {
            return;
        }
        _syncingInput = true;
        try
        {
            input.Text = _shownText;
        }
        finally
        {
            _syncingInput = false;
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetHelpText(this, FieldText.HelpText(Description, ErrorMessage));
        if (_panel is not null)
        {
            AutomationProperties.SetName(_panel, $"{Label} color".Trim());
        }
    }

    private void UpdatePanelText()
    {
        if (_panel is null)
        {
            return;
        }
        if (_panel.Against is { } against)
        {
            against.Text = AgainstBackgroundText;
        }
        if (_panel.Error is { } error)
        {
            error.Text = _invalid ? InvalidColorText : string.Empty;
        }
        if (_panel.DoneButton is { } done)
        {
            AutomationProperties.SetName(done, DoneText);
        }
        if (_panel.FormatButton is { } format)
        {
            AutomationProperties.SetName(format, $"Format: {s_formatNames[(int)Format]}. Switch format");
        }
        if (_panel.Input is { } input)
        {
            AutomationProperties.SetName(input, $"{Label} in {s_formatNames[(int)Format]}");
        }
        if (_panel.HeadName is { } name)
        {
            name.Text = Label;
        }
    }

    #endregion

    #region Panel

    private void EnsurePanel()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new ColorPickerPanel();
        _surface = new PickerMorphSurface(_panel);
        _surface.ShapeChanged += (_, _) => ApplyContentProgress();
        _surface.OutsidePressed += (_, _) => SetCurrentValue(IsDropDownOpenProperty, false);
        _surface.ContentResized += (_, _) => Dispatcher.UIThread.Post(Relayout);
        _popup.Child = _surface;
        _panel.ApplyTemplate();
        StyleSurface();
        if (_panel.Body is { } body)
        {
            body.RenderTransform = _bodyShift;
        }
        if (_panel.Field is { } field)
        {
            _shake = new PickerShake(field);
        }
        WirePad(_panel.Area, ColorPickerPadKind.Area);
        WirePad(_panel.Hue, ColorPickerPadKind.Hue);
        WirePad(_panel.Alpha, ColorPickerPadKind.Alpha);
        if (_panel.DoneButton is { } done)
        {
            done.Click += (_, _) => Hide(returnFocus: true);
        }
        if (_panel.FormatButton is { } format)
        {
            format.Click += (_, _) => CycleFormat();
        }
        if (_panel.FormatLabel is { } label)
        {
            label.Text = s_formatNames[(int)Format];
        }
        if (_panel.Input is { } input)
        {
            input.PropertyChanged += OnInputPropertyChanged;
            input.KeyDown += OnInputKeyDown;
            input.LostFocus += (_, _) =>
            {
                if (!SubmitDraft())
                {
                    _draft = null;
                    SetInvalid(false);
                    SyncInput();
                }
            };
        }
        if (_panel.AddButton is { } add)
        {
            add.Click += (_, _) => SaveSwatch();
        }
        if (_panel.Swatches is { } row)
        {
            row.SwatchInvoked += (_, swatch) =>
            {
                if (ColorPickerMath.Parse(ColorPickerMath.ToHex(ColorPickerMath.FromColor(swatch.Color)), _hsva.H) is { } next)
                {
                    Commit(next);
                }
            };
            row.MoveRequested += (_, move) => MoveSwatch(move.From, move.To);
            row.RemoveRequested += (_, swatch) => SetSwatches((Swatches ?? []).Where(s => s.Id != swatch.Id).ToList());
            row.EmptiedWhileFocused += (_, _) => _panel.Input?.Focus(NavigationMethod.Directional);
            row.Sync(Swatches ?? [], HexValue, animate: false);
        }
        _panel.KeyDown += OnPanelKeyDown;
        UpdatePanelText();
        UpdateAutomation();
    }

    private void WirePad(ColorPickerPad? pad, ColorPickerPadKind kind)
    {
        if (pad is null)
        {
            return;
        }
        pad.ValueRequested += (_, value) =>
        {
            var next = kind switch
            {
                ColorPickerPadKind.Area => _hsva with { S = value.X, V = 1 - value.Y },
                ColorPickerPadKind.Hue => _hsva with { H = Math.Clamp(value.X * 360, 0, 359.9) },
                _ => _hsva with { A = ColorPickerMath.Clamp01(value.X) }
            };
            Commit(next);
        };
        AutomationProperties.SetName(pad, kind switch
        {
            ColorPickerPadKind.Area => "Saturation and brightness",
            ColorPickerPadKind.Hue => "Hue",
            _ => "Opacity"
        });
    }

    private void StyleSurface()
    {
        if (_surface is null)
        {
            return;
        }
        _surface.Fill = PickerDrawing.ColorOf(PanelFill);
        _surface.BorderPen = PanelBorder is { } border ? new Pen(border, 1) : null;
        _surface.RestShadow = FloatShadow;
        _surface.OpenShadow = FloatShadow;
        _surface.InvalidateVisual();
    }

    private PickerMorphShape TriggerShape => _trigger is { } trigger
        ? new PickerMorphShape(0, 0, trigger.Bounds.Width, trigger.Bounds.Height, TriggerRadius)
        : new PickerMorphShape(0, 0, 160, 44, TriggerRadius);

    /// <summary>The panel opens over the swatch and grows down and right; it moves left or up only where the window ends.</summary>
    private PickerMorphShape PanelShape => _panel is { } panel
        ? new PickerMorphShape(_shift, _rise, panel.DesiredSize.Width, panel.DesiredSize.Height, PanelRadius)
        : TriggerShape;

    private void OpenPanel()
    {
        if (_popup is null || !IsEffectivelyEnabled)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
            return;
        }
        EnsurePanel();
        if (_panel is null || _surface is null)
        {
            return;
        }
        var generation = ++_generation;
        _draft = null;
        SetInvalid(false);
        var viewport = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? double.PositiveInfinity;
        _panel.Width = Math.Min(_panel.PreferredWidth, Math.Max(0, viewport - 32));
        _panel.SetNarrow(viewport <= 360);
        UpdateVisuals(animate: false);
        _panel.Swatches?.Sync(Swatches ?? [], HexValue, animate: false);
        _panel.Measure(Size.Infinity);
        _surface.ExpectContentSize(_panel.DesiredSize);
        PseudoClasses.Set(OpenPseudoClass, true);
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        // Reopening while the shape still folds back keeps the popup where it is, so the panel keeps its place too.
        var fit = _popup.IsOpen ? default : PickerMorphPlacement.Fit(this, _trigger, _panel.DesiredSize.Height, PickerMorphPlacement.Edge);
        if (!_popup.IsOpen)
        {
            _shift = PickerMorphPlacement.Shift(this, _trigger, _panel.DesiredSize.Width, PickerMorphPlacement.Edge);
            _rise = fit.Rise;
        }
        var from = TriggerShape;
        var to = PanelShape;
        _surface.Contain(from, to);
        if (!_popup.IsOpen)
        {
            _surface.ShadowRoom = PickerMorphPlacement.Room(this, _trigger, _surface, fit.Scroll);
            _popup.ContentOffset = new Point(-_surface.Origin.X, -_surface.Origin.Y);
            _surface.Jump(level == ArcMotionLevel.Full ? from : to, 1);
            _surface.Opacity = level == ArcMotionLevel.Reduced ? 0 : 1;
            _popup.IsOpen = true;
            StartOutsideWatch();
            (_reveal ??= new PickerRevealScroll(this)).Start(fit, animate: level == ArcMotionLevel.Full);
        }
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = _surface.MorphTo(to, OpenSpring, 1, MotionSpec.Instant);
                break;
            case ArcMotionLevel.Reduced:
                _surface.Jump(to, 1);
                Fade(1);
                break;
            default:
                _surface.Jump(to, 1);
                break;
        }
        ApplyContentProgress();
        _peer?.RaiseExpandChanged(true);
        var keyboard = _keyboardOpen;
        _keyboardOpen = false;
        // The area thumb takes focus as the panel opens, so the arrows work at once.
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == _generation && IsDropDownOpen)
            {
                _panel.Area?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
            }
        }, DispatcherPriority.Loaded);
    }

    private bool _closeReturnsFocus;

    /// <summary>The panel settled at another size after it was shown: the shape follows (springing if it still moves).</summary>
    private void Relayout()
    {
        if (!IsDropDownOpen || _surface is null || _panel is null || _popup is not { IsOpen: true })
        {
            return;
        }
        var from = TriggerShape;
        var to = PanelShape;
        _surface.Contain(from, to);
        if (_surface.IsMoving)
        {
            _surface.Retarget(to, OpenSpring);
        }
        else
        {
            _surface.Jump(to, 1);
        }
        ApplyContentProgress();
    }

    /// <summary>Closes the panel; <paramref name="returnFocus"/> puts focus back on the swatch (Done, the swatch itself).</summary>
    private void Hide(bool returnFocus)
    {
        _closeReturnsFocus = returnFocus;
        SetCurrentValue(IsDropDownOpenProperty, false);
        _closeReturnsFocus = false;
    }

    private async void ClosePanel(bool returnFocus)
    {
        PseudoClasses.Set(OpenPseudoClass, false);
        StopOutsideWatch();
        CancelMorph();
        _reveal?.Stop();
        if (_popup is not { IsOpen: true } popup || _surface is null)
        {
            return;
        }
        var generation = ++_generation;
        var focusInside = _panel?.IsKeyboardFocusWithin == true;
        _draft = null;
        SetInvalid(false);
        if (returnFocus || focusInside)
        {
            Focus(NavigationMethod.Unspecified);
        }
        _peer?.RaiseExpandChanged(false);
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Full)
        {
            await _surface.MorphTo(TriggerShape, CloseSpring, 1, MotionSpec.Instant);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            await Fade(0);
        }
        if (generation == _generation && !IsDropDownOpen)
        {
            popup.IsOpen = false;
        }
    }

    private Task<bool> Fade(double target)
    {
        if (_surface is null)
        {
            return Task.FromResult(true);
        }
        if (_fade is null)
        {
            _fade = new MotionValue<double>(_surface, _surface.Opacity, MotionVectors.Double);
            _fade.Changed += v => _surface.Opacity = v;
        }
        _fade.Jump(_surface.Opacity);
        return _fade.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    /// <summary>The content fades in behind the leading edge of the reveal: opacity over progress 0.35–0.9, y −10 → 0.</summary>
    private void ApplyContentProgress()
    {
        if (_panel is null || _surface is null)
        {
            return;
        }
        var from = TriggerShape;
        var to = PanelShape;
        var span = to.Height - from.Height;
        var q = span > 1 ? Math.Clamp((_surface.Shape.Height - from.Height) / span, 0, 1.2) : 1;
        var opacity = Math.Clamp((q - 0.35) / (0.9 - 0.35), 0, 1);
        if (_panel.Body is { } body)
        {
            body.Opacity = opacity;
        }
        if (_panel.Actions is { } actions)
        {
            actions.Opacity = opacity;
        }
        _bodyShift.Y = -10 * (1 - Math.Min(q, 1));
    }

    private void StartOutsideWatch()
    {
        StopOutsideWatch();
        _outsideTop = TopLevel.GetTopLevel(this);
        _outsideTop?.AddHandler(PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void StopOutsideWatch()
    {
        _outsideTop?.RemoveHandler(PointerPressedEvent, OnTopLevelPressed);
        _outsideTop = null;
    }

    /// <summary>Any press outside the picker closes it (and keeps focus where the press put it).</summary>
    private void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (InSurface(source) || this.IsVisualAncestorOf(source) || ReferenceEquals(source, this)))
        {
            return;
        }
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private bool InSurface(Visual visual) => _surface is not null && (ReferenceEquals(visual, _surface) || _surface.IsVisualAncestorOf(visual));

    #endregion

    #region Field and swatches

    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Avalonia.Controls.TextBox.TextProperty || _syncingInput || _panel?.Input is not { } input)
        {
            return;
        }
        var text = input.Text ?? string.Empty;
        _draft = text == _shownText ? null : text;
        SetInvalid(false);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (SubmitDraft())
            {
                _panel?.Input?.SelectAll();
            }
        }
        else if (e.Key == Key.Escape && _draft is not null)
        {
            // Escape first drops the draft; a second Escape closes the panel.
            e.Handled = true;
            _draft = null;
            SetInvalid(false);
            SyncInput();
        }
    }

    /// <summary>Applies typed text; anything that is not a color shakes the field and shows the error (P13).</summary>
    internal bool SubmitDraft()
    {
        if (_draft is null)
        {
            return true;
        }
        if (ColorPickerMath.Parse(_draft, _hsva.H) is not { } next)
        {
            SetInvalid(true);
            _shake?.Play();
            return false;
        }
        _draft = null;
        SetInvalid(false);
        Commit(next);
        SyncInput();
        return true;
    }

    private void SetInvalid(bool invalid)
    {
        _invalid = invalid;
        _panel?.SetInvalid(invalid);
        if (_panel?.Error is { } error)
        {
            error.Text = invalid ? InvalidColorText : string.Empty;
            error.IsVisible = invalid;
        }
        if (_panel?.Input is { } input)
        {
            AutomationProperties.SetHelpText(input, invalid ? InvalidColorText : string.Empty);
        }
    }

    internal bool IsInputInvalid => _invalid;

    private void CycleFormat()
    {
        var next = (ColorPickerFormat)(((int)Format + 1) % 4);
        SetCurrentValue(FormatProperty, next);
    }

    private void OnFormatChanged(ColorPickerFormat from, ColorPickerFormat to)
    {
        var fromText = ColorPickerMath.Format(_hsva, from);
        _draft = null;
        SetInvalid(false);
        SyncInput();
        if (_panel?.FormatLabel is { } label)
        {
            label.Text = s_formatNames[(int)to];
        }
        UpdatePanelText();
        if (IsLoaded && _popup?.IsOpen == true && ArcMotion.IsFull(this))
        {
            StartMorph(fromText, _shownText);
        }
    }

    /// <summary>
    /// Format morph (P4): a TextMorph mounts over the field showing the old string, morphs to the new one on the next
    /// frame, and the live field returns once it settles (620ms).
    /// </summary>
    private void StartMorph(string fromText, string toText)
    {
        if (_panel?.MorphHost is not { } host || _panel.Input is not { } input)
        {
            return;
        }
        CancelMorph();
        var morph = new TextMorph
        {
            Text = fromText,
            FontSize = input.FontSize,
            FontFamily = input.FontFamily,
            FontWeight = input.FontWeight,
            Foreground = input.Foreground,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _morph = morph;
        host.Children.Add(morph);
        _morphing = true;
        input.Opacity = 0;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_morph, morph))
            {
                morph.Text = toText;
            }
        }, DispatcherPriority.Background);
        _morphTimer = DispatcherTimer.RunOnce(CancelMorph, TimeSpan.FromMilliseconds(620));
    }

    private void CancelMorph()
    {
        _morphTimer?.Dispose();
        _morphTimer = null;
        if (_morph is not null && _panel?.MorphHost is { } host)
        {
            host.Children.Remove(_morph);
        }
        _morph = null;
        if (_morphing && _panel?.Input is { } input)
        {
            input.Opacity = 1;
        }
        _morphing = false;
    }

    internal bool IsMorphing => _morphing;

    private void SaveSwatch()
    {
        var entry = ColorSwatch.Create(ColorPickerMath.ToColor(_hsva));
        var next = new List<ColorSwatch> { entry };
        next.AddRange(Swatches ?? []);
        SetSwatches(next.Take(Math.Max(0, MaxSwatches)).ToList());
    }

    private void MoveSwatch(int from, int to)
    {
        var list = (Swatches ?? []).ToList();
        if (from < 0 || from >= list.Count || to < 0 || to >= list.Count)
        {
            return;
        }
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        SetSwatches(list);
    }

    private void SetSwatches(IReadOnlyList<ColorSwatch> next)
    {
        SetCurrentValue(SwatchesProperty, next);
        RaiseEvent(new ColorPickerSwatchesChangedEventArgs(SwatchesChangedEvent, next));
    }

    #endregion

    #region Input

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressedOnTrigger = true;
        e.Handled = true;
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressedOnTrigger && _trigger is not null && new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)))
        {
            Focus(NavigationMethod.Pointer);
            if (IsDropDownOpen)
            {
                Hide(returnFocus: true);
            }
            else
            {
                SetCurrentValue(IsDropDownOpenProperty, true);
            }
        }
        _pressedOnTrigger = false;
    }

    private void OnTriggerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _pressedOnTrigger = false;

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide(returnFocus: true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !ReferenceEquals(e.Source, this))
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            if (IsDropDownOpen)
            {
                Hide(returnFocus: true);
            }
            else
            {
                _keyboardOpen = true;
                SetCurrentValue(IsDropDownOpenProperty, true);
            }
        }
        else if (e.Key == Key.Escape && IsDropDownOpen)
        {
            e.Handled = true;
            Hide(returnFocus: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopOutsideWatch();
        CancelMorph();
        _reveal?.Stop();
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    #endregion
}

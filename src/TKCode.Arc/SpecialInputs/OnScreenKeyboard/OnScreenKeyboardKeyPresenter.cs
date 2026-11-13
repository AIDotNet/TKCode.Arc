using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One key of an <see cref="OnScreenKeyboard"/>, drawn by itself (a keyboard shows around 40 of them): a key cap with a
/// 1px edge and a thicker bottom lip, and a label or a glyph in the middle. It never takes focus; the keyboard handles
/// the pointer and sets <c>:pressed</c>. Colors, font and press motion come from its ControlTheme.
/// </summary>
[PseudoClasses(PressedPseudoClass, HoverPseudoClass, FunctionPseudoClass, PrimaryPseudoClass, ActivePseudoClass, LockedPseudoClass, WordPseudoClass, SpacerPseudoClass)]
internal sealed class OnScreenKeyboardKeyPresenter : Control
{
    public const string PressedPseudoClass = ":pressed";
    public const string HoverPseudoClass = ":hover";
    public const string FunctionPseudoClass = ":function";
    public const string PrimaryPseudoClass = ":primary";
    public const string ActivePseudoClass = ":active";
    public const string LockedPseudoClass = ":locked";
    public const string WordPseudoClass = ":word";
    public const string SpacerPseudoClass = ":spacer";

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<OnScreenKeyboardKeyPresenter>();

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> LipBrushProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, IBrush?>(nameof(LipBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<OnScreenKeyboardKeyPresenter>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<OnScreenKeyboardKeyPresenter>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<OnScreenKeyboardKeyPresenter>();

    public static readonly StyledProperty<double> LipThicknessProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, double>(nameof(LipThickness), 2);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<OnScreenKeyboardKeyPresenter, double>(nameof(GlyphSize), 20);

    private const double GlyphStroke = 1.75;

    private readonly OnScreenKeyboard _owner;
    private string? _label;
    private Geometry? _glyph;
    private bool _glyphFilled;
    private TextLayout? _text;
    private double _textMaxWidth = -1;
    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    static OnScreenKeyboardKeyPresenter()
    {
        FocusableProperty.OverrideDefaultValue<OnScreenKeyboardKeyPresenter>(false);
        AffectsRender<OnScreenKeyboardKeyPresenter>(BackgroundProperty, ForegroundProperty, EdgeBrushProperty, LipBrushProperty,
            CornerRadiusProperty, LipThicknessProperty, GlyphSizeProperty);
    }

    public OnScreenKeyboardKeyPresenter(OnScreenKeyboard owner)
    {
        _owner = owner;
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public IBrush? LipBrush { get => GetValue(LipBrushProperty); set => SetValue(LipBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public double LipThickness { get => GetValue(LipThicknessProperty); set => SetValue(LipThicknessProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <summary>The key this presenter shows.</summary>
    public OnScreenKeyboardKey? Key { get; private set; }

    /// <summary>The row the key sits in (the panel lays rows out top to bottom).</summary>
    public int Row { get; private set; }

    /// <summary>The key's width in key units.</summary>
    public double Units => Key?.Width ?? 0;

    /// <summary>A spacer: takes room, draws nothing, cannot be pressed.</summary>
    public bool IsSpacer => Key?.Kind == OnScreenKeyboardKeyKind.Spacer;

    /// <summary>The label currently drawn (null when a glyph is drawn).</summary>
    public string? Label => _label;

    /// <summary>The glyph currently drawn.</summary>
    public Geometry? Glyph => _glyph;

    /// <summary>Whether the glyph is drawn filled (Shift while on).</summary>
    public bool IsGlyphFilled => _glyphFilled;

    public bool IsPressed => PseudoClasses.Contains(PressedPseudoClass);

    /// <summary>Shows <paramref name="key"/> in row <paramref name="row"/>; the keyboard then calls <see cref="Show"/>.</summary>
    public void Bind(OnScreenKeyboardKey key, int row)
    {
        Key = key;
        Row = row;
    }

    /// <summary>Updates what the key shows and how it is named and styled.</summary>
    public void Show(string? label, Geometry? glyph, bool glyphFilled, string? automationName, bool isFunction, bool isPrimary, bool isActive, bool isLocked)
    {
        var key = Key;
        IsVisible = key is { IsVisible: true } && key.Width > 0;
        IsEnabled = key is { IsEnabled: true };
        IsHitTestVisible = !IsSpacer;
        if (!string.Equals(_label, label, StringComparison.Ordinal))
        {
            _label = label;
            DropText();
        }
        if (!ReferenceEquals(_glyph, glyph) || _glyphFilled != glyphFilled)
        {
            _glyph = glyph;
            _glyphFilled = glyphFilled;
        }
        AutomationProperties.SetName(this, automationName ?? string.Empty);
        PseudoClasses.Set(FunctionPseudoClass, isFunction);
        PseudoClasses.Set(PrimaryPseudoClass, isPrimary);
        var wasActive = PseudoClasses.Contains(ActivePseudoClass);
        PseudoClasses.Set(ActivePseudoClass, isActive);
        if (wasActive != isActive && ControlAutomationPeer.FromElement(this) is KeyPeer peer)
        {
            peer.RaiseToggleChanged(wasActive, isActive);
        }
        PseudoClasses.Set(LockedPseudoClass, isLocked);
        PseudoClasses.Set(SpacerPseudoClass, IsSpacer);
        PseudoClasses.Set(WordPseudoClass, glyph is null && label is not null && new StringInfo(label).LengthInTextElements > 1);
        InvalidateVisual();
    }

    public void SetPressed(bool pressed)
    {
        if (pressed == IsPressed)
        {
            return;
        }
        PseudoClasses.Set(PressedPseudoClass, pressed);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == FontWeightProperty)
        {
            DropText();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover is for a mouse only: a finger has no hover, and a tap must not leave a key looking hovered.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type == PointerType.Mouse && !IsSpacer);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner.OnKeyCaptureLost(this, e.Pointer);
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    public override void Render(DrawingContext context)
    {
        if (IsSpacer)
        {
            return;
        }
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }
        // Narrow keys (a QWERTY on a phone-width screen) keep a key shape instead of turning into pills.
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(bounds.Width, bounds.Height) * 0.3);
        // The lip shows under the edge: a thicker bottom on a key at rest, flush (1px) while pressed.
        var lipExtra = IsPressed ? 0 : Math.Max(0, LipThickness - 1);
        if (lipExtra > 0)
        {
            context.DrawRectangle(LipBrush, null, new RoundedRect(bounds, radius));
        }
        var edgeRect = new Rect(0, 0, bounds.Width, bounds.Height - lipExtra);
        context.DrawRectangle(EdgeBrush, null, new RoundedRect(edgeRect, radius));
        var face = edgeRect.Deflate(1);
        context.DrawRectangle(Background, null, new RoundedRect(face, Math.Max(0, radius - 1)));

        var foreground = Foreground;
        if (foreground is null)
        {
            return;
        }
        if (_glyph is { } glyph)
        {
            DrawGlyph(context, glyph, foreground, face);
        }
        else if (!string.IsNullOrEmpty(_label))
        {
            // One character is never cut (a "w" on a narrow key may touch the edges); longer labels end with an ellipsis.
            var text = EnsureText(PseudoClasses.Contains(WordPseudoClass) ? face.Width - 8 : double.PositiveInfinity, foreground);
            var x = face.X + (face.Width - text.WidthIncludingTrailingWhitespace) / 2;
            var y = face.Y + (face.Height - text.Height) / 2;
            text.Draw(context, new Point(x, y));
        }
    }

    private void DrawGlyph(DrawingContext context, Geometry glyph, IBrush brush, Rect face)
    {
        var size = Math.Min(GlyphSize, Math.Min(face.Width, face.Height));
        if (size <= 0)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - GlyphStroke) > double.Epsilon)
        {
            _pen = new Pen(brush, GlyphStroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = GlyphStroke;
        }
        var scale = size / 24.0;
        var x = face.X + (face.Width - size) / 2;
        var y = face.Y + (face.Height - size) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(_glyphFilled ? brush : null, _pen, glyph);
        }
    }

    private TextLayout EnsureText(double maxWidth, IBrush foreground)
    {
        maxWidth = Math.Max(1, maxWidth);
        if (_text is null || (Math.Abs(_textMaxWidth - maxWidth) > 0.5 && !(double.IsInfinity(maxWidth) && double.IsInfinity(_textMaxWidth))))
        {
            _text?.Dispose();
            _textMaxWidth = maxWidth;
            _text = new TextLayout(_label, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, foreground,
                maxWidth: maxWidth, textTrimming: TextTrimming.CharacterEllipsis);
        }
        return _text;
    }

    private void DropText()
    {
        _text?.Dispose();
        _text = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new KeyPeer(this);

    /// <summary>The key's peer: a button (Invoke presses it); Shift is also a toggle that reports whether it is on.</summary>
    private sealed class KeyPeer : ControlAutomationPeer, IInvokeProvider, IToggleProvider
    {
        public KeyPeer(OnScreenKeyboardKeyPresenter owner) : base(owner)
        {
        }

        private OnScreenKeyboardKeyPresenter Presenter => (OnScreenKeyboardKeyPresenter)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => !Presenter.IsSpacer;

        protected override bool IsControlElementCore() => !Presenter.IsSpacer;

        public void Invoke()
        {
            if (Presenter.Key is { } key && Presenter.IsEffectivelyEnabled)
            {
                Presenter._owner.Invoke(key);
            }
        }

        public ToggleState ToggleState => Presenter.PseudoClasses.Contains(ActivePseudoClass) ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        protected override object? GetProviderCore(Type providerType)
        {
            // Only Shift is a toggle; every other key is a plain button.
            if (providerType == typeof(IToggleProvider) && Presenter.Key?.Kind != OnScreenKeyboardKeyKind.Shift)
            {
                return null;
            }
            return base.GetProviderCore(providerType);
        }

        /// <summary>Tells screen readers that Shift turned on or off.</summary>
        public void RaiseToggleChanged(bool oldOn, bool newOn) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, oldOn ? ToggleState.On : ToggleState.Off,
                newOn ? ToggleState.On : ToggleState.Off);
    }
}

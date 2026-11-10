using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One action of an <see cref="OrbitMenu"/>: a round button with a glyph, named by <see cref="Label"/> (its tooltip and
/// accessible name). It draws itself, circle, shadow, glyph and keyboard ring, so a ring of actions costs one element
/// each.
/// </summary>
[PseudoClasses(PressedPseudoClass)]
public class OrbitMenuAction : Control
{
    internal const string PressedPseudoClass = ":pressed";

    /// <summary>Gap between the button and its keyboard focus ring, plus half the ring's width.</summary>
    private const double RingGap = 4;

    /// <summary>The 24 × 24 stroked glyph in the middle of the button.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<OrbitMenuAction, Geometry?>(nameof(Glyph));

    /// <summary>What the action does, in a few words: the tooltip and the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<OrbitMenuAction, string?>(nameof(Label));

    /// <summary>Command run when the action is invoked.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<OrbitMenuAction, ICommand?>(nameof(Command));

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<OrbitMenuAction, object?>(nameof(CommandParameter));

    /// <summary>Fill of the circle (set by the theme, with its hover and press states).</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<OrbitMenuAction, IBrush?>(nameof(Background));

    /// <summary>Color of the glyph.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<OrbitMenuAction, IBrush?>(nameof(Foreground));

    /// <summary>Color of the circle's edge.</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<OrbitMenuAction, IBrush?>(nameof(BorderBrush));

    /// <summary>Corner radius of the button; the theme's pill radius draws a circle.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<OrbitMenuAction, CornerRadius>(nameof(CornerRadius));

    /// <summary>Shadow under the button.</summary>
    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<OrbitMenuAction, BoxShadows>(nameof(Shadow));

    /// <summary>Color of the keyboard focus ring.</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<OrbitMenuAction, IBrush?>(nameof(FocusRingBrush));

    /// <summary>Diameter of the button.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<OrbitMenuAction, double>(nameof(Size), 44);

    /// <summary>Size of the glyph.</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<OrbitMenuAction, double>(nameof(GlyphSize), 18);

    /// <summary>Raised when the action is invoked by a click, Enter, Space or assistive technology. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<OrbitMenuAction, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;
    private Pen? _edgePen;
    private IBrush? _edgePenBrush;
    private Pen? _ringPen;
    private IBrush? _ringPenBrush;
    private bool _pressed;

    static OrbitMenuAction()
    {
        AffectsMeasure<OrbitMenuAction>(SizeProperty);
        AffectsRender<OrbitMenuAction>(GlyphProperty, BackgroundProperty, ForegroundProperty, BorderBrushProperty,
            CornerRadiusProperty, ShadowProperty, FocusRingBrushProperty, GlyphSizeProperty);
        FocusableProperty.OverrideDefaultValue<OrbitMenuAction>(true);
        CursorProperty.OverrideDefaultValue<OrbitMenuAction>(new Cursor(StandardCursorType.Hand));
    }

    public OrbitMenuAction()
    {
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = new TransformGroup { Children = { _scale, _translate } };
    }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <inheritdoc cref="BackgroundProperty"/>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="BorderBrushProperty"/>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <inheritdoc cref="CornerRadiusProperty"/>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <inheritdoc cref="ShadowProperty"/>
    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <inheritdoc cref="FocusRingBrushProperty"/>
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="GlyphSizeProperty"/>
    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    /// <inheritdoc cref="ClickEvent"/>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>Where the action rests when the menu is open, relative to the trigger's center.</summary>
    internal Point Offset { get; set; }

    /// <summary>How far out of the trigger the action is: 0 inside it, 1 at its place on the arc.</summary>
    internal MotionValue<double>? Travel { get; set; }

    /// <summary>The scale an action starts from.</summary>
    internal double LaunchScale { get; set; } = 0.4;

    /// <summary>Places the action <paramref name="progress"/> of the way out; with <paramref name="travels"/> false it only fades in place.</summary>
    internal void ApplyTravel(double progress, bool travels)
    {
        var p = travels ? progress : 1;
        _translate.X = Offset.X * p;
        _translate.Y = Offset.Y * p;
        var scale = travels ? LaunchScale + (1 - LaunchScale) * Math.Max(0, progress) : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = Math.Clamp(travels ? progress * 1.6 : progress, 0, 1);
    }

    /// <summary>Invokes the action: raises <see cref="Click"/> and runs <see cref="Command"/>.</summary>
    public void Invoke()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(ClickEvent));
        var command = Command;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            Tooltip.SetTip(this, Label);
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            PseudoClasses.Set(PressedPseudoClass, true);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
        e.Pointer.Capture(null);
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Invoke();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private void DrawFace(DrawingContext context, Rect bounds, double radius)
    {
        if (BorderBrush is { } edge && !ReferenceEquals(edge, _edgePenBrush))
        {
            _edgePen = new Pen(edge, 1);
            _edgePenBrush = edge;
        }
        context.DrawRectangle(Background ?? Brushes.Transparent, BorderBrush is null ? null : _edgePen,
            new RoundedRect(bounds.Deflate(0.5), radius), Shadow);
        if (Glyph is { } glyph && Foreground is { } ink)
        {
            if (_glyphPen is null || !ReferenceEquals(_glyphPenBrush, ink))
            {
                _glyphPen = new Pen(ink, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                _glyphPenBrush = ink;
            }
            var size = GlyphSize;
            var scale = size / 24;
            var origin = bounds.Center - new Point(size / 2, size / 2);
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawGeometry(null, _glyphPen, glyph);
            }
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(bounds.Width, bounds.Height) / 2);
        if (IsEffectivelyEnabled)
        {
            DrawFace(context, bounds, radius);
        }
        else
        {
            using (context.PushOpacity(0.45))
            {
                DrawFace(context, bounds, radius);
            }
        }
        if (Classes.Contains(":focus-visible") && FocusRingBrush is { } ring)
        {
            if (_ringPen is null || !ReferenceEquals(_ringPenBrush, ring))
            {
                _ringPen = new Pen(ring, 2);
                _ringPenBrush = ring;
            }
            context.DrawRectangle(null, _ringPen, new RoundedRect(bounds.Inflate(RingGap), radius > 0 ? radius + RingGap : 0));
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new OrbitMenuActionAutomationPeer(this);

    private sealed class OrbitMenuActionAutomationPeer : ControlAutomationPeer, IInvokeProvider
    {
        public OrbitMenuActionAutomationPeer(OrbitMenuAction owner) : base(owner)
        {
        }

        private new OrbitMenuAction Owner => (OrbitMenuAction)base.Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => Owner.Invoke();
    }
}

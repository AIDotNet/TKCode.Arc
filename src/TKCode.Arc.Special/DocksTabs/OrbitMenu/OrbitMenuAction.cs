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
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The color of an <see cref="OrbitMenuAction"/>.</summary>
public enum OrbitMenuActionTone
{
    /// <summary>The foreground color.</summary>
    Neutral,

    /// <summary>The danger color, for actions that destroy something (delete, remove).</summary>
    Danger
}

/// <summary>
/// One action of an <see cref="OrbitMenu"/>: a round button with a glyph, named by <see cref="Label"/> (the pill text and the
/// accessible name). It draws itself, circle, shadow and glyph, so an arc of actions costs one element each.
/// </summary>
[PseudoClasses(PressedPseudoClass, TargetedPseudoClass, DangerPseudoClass)]
public class OrbitMenuAction : Control
{
    internal const string PressedPseudoClass = ":pressed";
    internal const string TargetedPseudoClass = ":targeted";
    internal const string DangerPseudoClass = ":danger";

    private const double GlyphThickness = 1.75;

    /// <summary>The 24 × 24 stroked glyph in the middle of the button.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<OrbitMenuAction, Geometry?>(nameof(Glyph));

    /// <summary>What the action does, in a few words: the pill shows it while the action is targeted, and it is the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<OrbitMenuAction, string?>(nameof(Label));

    /// <summary>What the pill says once the action has run, such as "Saved"; null falls back to <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> DoneTextProperty =
        AvaloniaProperty.Register<OrbitMenuAction, string?>(nameof(DoneText));

    /// <summary>The action's color: <see cref="OrbitMenuActionTone.Danger"/> for destructive actions.</summary>
    public static readonly StyledProperty<OrbitMenuActionTone> ToneProperty =
        AvaloniaProperty.Register<OrbitMenuAction, OrbitMenuActionTone>(nameof(Tone));

    /// <summary>Command run when the action fires.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<OrbitMenuAction, ICommand?>(nameof(Command));

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<OrbitMenuAction, object?>(nameof(CommandParameter));

    /// <summary>Fill of the circle (set by the theme; it fades out while the lens is under the action).</summary>
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

    /// <summary>Diameter of the button.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<OrbitMenuAction, double>(nameof(Size), 46);

    /// <summary>Size of the glyph.</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<OrbitMenuAction, double>(nameof(GlyphSize), 20);

    /// <summary>Raised when the action fires: a release over it, a click, Enter, Space or assistive technology. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<OrbitMenuAction, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private Pen? _glyphPen;
    private IBrush? _glyphPenBrush;
    private Pen? _edgePen;
    private IBrush? _edgePenBrush;
    private bool _pressed;
    private double _iconScale = 1;
    private double _iconOpacity = 1;
    private double _blur;
    private bool _targeted;

    static OrbitMenuAction()
    {
        AffectsMeasure<OrbitMenuAction>(SizeProperty);
        AffectsRender<OrbitMenuAction>(GlyphProperty, BackgroundProperty, ForegroundProperty, BorderBrushProperty,
            CornerRadiusProperty, ShadowProperty, GlyphSizeProperty);
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

    /// <inheritdoc cref="DoneTextProperty"/>
    public string? DoneText { get => GetValue(DoneTextProperty); set => SetValue(DoneTextProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public OrbitMenuActionTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

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
    internal Point Rest { get; set; }

    /// <summary>The angle of <see cref="Rest"/> in radians, clockwise from the right.</summary>
    internal double RestAngle { get; set; }

    /// <summary>Distance from the trigger's center to <see cref="Rest"/>.</summary>
    internal double OrbitRadius { get; set; }

    /// <summary>How far out of the trigger the action is: 0 inside it, 1 at its place on the arc.</summary>
    internal MotionValue<double>? Travel { get; set; }

    /// <summary>The action's opacity, faded on its own clock (it reaches full well before the travel settles).</summary>
    internal MotionValue<double>? Fade { get; set; }

    /// <summary>The emphasis of the targeted action: 0 normal, 1 under the lens.</summary>
    internal MotionValue<double>? Boost { get; set; }

    /// <summary>The magnet's pull toward the pointer (added to the position).</summary>
    internal Vector Pull { get; set; }

    /// <summary>The magnet's growth near the pointer (multiplies the scale).</summary>
    internal double PullScale { get; set; } = 1;

    /// <summary>The scale the action is drawn at right now (read by the lens to sit exactly under it).</summary>
    internal double VisualScale => _scale.ScaleX;

    /// <summary>The center the action is drawn at right now, relative to the trigger's center.</summary>
    internal Point VisualOffset => new(_translate.X, _translate.Y);

    /// <summary>
    /// Places and fades the action from its travel, fade, emphasis and pull. With <paramref name="full"/> false (reduced or
    /// no motion) it never moves: it fades in place and the pull only grows it.
    /// </summary>
    internal void ApplyMotion(bool full)
    {
        var fade = Fade?.Value ?? 0;
        var boost = 1 + OrbitMenuGeometry.TargetGain * (Boost?.Value ?? 0);
        double x;
        double y;
        double scale;
        double blur;
        double iconScale;
        double iconOpacity;
        if (full)
        {
            var p = Travel?.Value ?? 0;
            var angle = RestAngle - OrbitMenuGeometry.Swirl * (1 - p);
            var reach = OrbitRadius * p;
            var hold = Math.Clamp(p, 0, 1);
            x = Math.Cos(angle) * reach + Pull.X * hold;
            y = Math.Sin(angle) * reach + Pull.Y * hold;
            scale = (OrbitMenuGeometry.LaunchScale + (1 - OrbitMenuGeometry.LaunchScale) * p) * PullScale * boost;
            blur = OrbitMenuGeometry.LaunchBlur * Math.Max(0, 1 - p / 0.6);
            iconScale = 0.5 + 0.5 * Math.Clamp((p - 0.3) / 0.7, 0, 1);
            iconOpacity = Math.Clamp((p - 0.35) / 0.5, 0, 1);
        }
        else
        {
            x = Rest.X;
            y = Rest.Y;
            scale = PullScale * boost;
            blur = 0;
            iconScale = 1;
            iconOpacity = 1;
        }
        _translate.X = x;
        _translate.Y = y;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = Math.Clamp(fade, 0, 1);
        if (Math.Abs(blur - _blur) > 0.01)
        {
            _blur = blur;
            Effect = ArcBlur.Get(blur);
        }
        if (Math.Abs(iconScale - _iconScale) > 1e-4 || Math.Abs(iconOpacity - _iconOpacity) > 1e-4)
        {
            _iconScale = iconScale;
            _iconOpacity = iconOpacity;
            InvalidateVisual();
        }
    }

    /// <summary>Marks the action as the one the lens sits under.</summary>
    internal void SetTargeted(bool targeted)
    {
        if (targeted == _targeted)
        {
            return;
        }
        _targeted = targeted;
        PseudoClasses.Set(TargetedPseudoClass, targeted);
        InvalidateVisual();
    }

    internal bool IsTargeted => _targeted;

    /// <summary>Fires the action: raises <see cref="Click"/> and runs <see cref="Command"/>.</summary>
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
        }
        else if (change.Property == ToneProperty)
        {
            PseudoClasses.Set(DangerPseudoClass, Tone == OrbitMenuActionTone.Danger);
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
        var point = e.GetCurrentPoint(this);
        if (!e.Handled && (point.Properties.IsLeftButtonPressed || e.Pointer.Type != PointerType.Mouse))
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
        var size = Bounds.Size;
        var position = e.GetPosition(this);
        var radius = Math.Min(size.Width, size.Height) / 2;
        if (new Vector(position.X - size.Width / 2, position.Y - size.Height / 2).Length <= radius + 1)
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

    private void DrawFace(DrawingContext context, Rect bounds, double radius)
    {
        if (BorderBrush is { } edge && !ReferenceEquals(edge, _edgePenBrush))
        {
            _edgePen = new Pen(edge, 1);
            _edgePenBrush = edge;
        }
        // The fill is always drawn (transparent while targeted) so the whole circle stays hit-testable; the lens under a
        // targeted action brings its own shadow.
        context.DrawRectangle(Background ?? Brushes.Transparent, BorderBrush is null ? null : _edgePen,
            new RoundedRect(bounds.Deflate(0.5), radius), _targeted ? default : Shadow);
        if (Glyph is not { } glyph || Foreground is not { } ink || _iconOpacity <= 0)
        {
            return;
        }
        if (_glyphPen is null || !ReferenceEquals(_glyphPenBrush, ink))
        {
            _glyphPen = new Pen(ink, GlyphThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _glyphPenBrush = ink;
        }
        var size = GlyphSize * _iconScale;
        var scale = size / 24;
        var origin = bounds.Center - new Point(size / 2, size / 2);
        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y);
        if (_iconOpacity >= 1)
        {
            using (context.PushTransform(transform))
            {
                context.DrawGeometry(null, _glyphPen, glyph);
            }
            return;
        }
        using (context.PushOpacity(_iconOpacity))
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(null, _glyphPen, glyph);
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
            return;
        }
        using (context.PushOpacity(0.45))
        {
            DrawFace(context, bounds, radius);
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

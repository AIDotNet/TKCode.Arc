using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A Lucide glyph for reactions: stroked like <see cref="ArcGlyph"/>, optionally filled with its own color at
/// <see cref="FillOpacity"/> (a chosen reaction), and kicked by a velocity impulse (P27): the glyph starts at rest with
/// velocity on a stiff spring, overshoots and settles; a second kick adds to the motion instead of restarting it.
/// The kick is drawn in <see cref="Render"/>, so it never touches layout or the render transform styles use; it is
/// code-driven because each tap adds velocity to the running spring, which a declarative transition restarts at zero.
/// </summary>
internal sealed class ReactionsGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<ReactionsGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ReactionsGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<ReactionsGlyph, bool>(nameof(IsFilled));

    public static readonly StyledProperty<double> FillOpacityProperty =
        AvaloniaProperty.Register<ReactionsGlyph, double>(nameof(FillOpacity), 0.22);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ReactionsGlyph>();

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }
    public double FillOpacity { get => GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double StrokeThickness = 1.75;

    private MotionValue<double>? _scale;
    private MotionValue<double>? _rotate;
    private Pen? _pen;
    private IBrush? _penBrush;

    static ReactionsGlyph()
    {
        AffectsMeasure<ReactionsGlyph>(SizeProperty);
        AffectsRender<ReactionsGlyph>(DataProperty, IsFilledProperty, FillOpacityProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ReactionsGlyph>(false);
    }

    /// <summary>Current kick scale (1 at rest).</summary>
    public double KickScale => _scale?.Value ?? 1;

    /// <summary>Current kick rotation in degrees (0 at rest).</summary>
    public double KickRotation => _rotate?.Value ?? 0;

    /// <summary>Scale velocity of the running kick (per second).</summary>
    public double KickScaleVelocity => _scale?.Velocity ?? 0;

    /// <summary>
    /// Adds <paramref name="scaleVelocity"/> (per second) and <paramref name="rotateVelocity"/> (degrees per second) to
    /// the glyph on <paramref name="spring"/>. Only under full motion (Arc skips the kick when motion is reduced).
    /// </summary>
    public void Kick(SpringSpec spring, double scaleVelocity, double rotateVelocity)
    {
        if (!ArcMotion.IsFull(this) || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _scale ??= CreateValue(1);
        _rotate ??= CreateValue(0);
        _ = _scale.AnimateTo(1, MotionSpec.Spring(spring), _scale.Velocity + scaleVelocity);
        _ = _rotate.AnimateTo(0, MotionSpec.Spring(spring), _rotate.Velocity + rotateVelocity);
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scale?.Jump(1);
        _rotate?.Jump(0);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var unit = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var kick = KickScale;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(unit * kick, unit * kick) *
                     Matrix.CreateRotation(KickRotation * Math.PI / 180) * Matrix.CreateTranslation(cx, cy);
        using (context.PushTransform(matrix))
        {
            if (IsFilled && FillOpacity > 0)
            {
                using (context.PushOpacity(FillOpacity))
                {
                    context.DrawGeometry(brush, null, data);
                }
            }
            context.DrawGeometry(null, _pen, data);
        }
    }
}

/// <summary>
/// One reaction pill: an icon and a count that rolls (Core <see cref="Primitives.RollingDigits"/>), a toggle button
/// whose pressed state is the viewer's own reaction (the value owns it: clicking only asks <see cref="Reactions"/>).
/// Enters from scale 0.5 out of a soft blur and leaves to 0.6 with a subtle blur (P26); the bar moves it with its
/// FLIP offset and this presence scale in one transform. Presence is code-driven: the exit gates the pill's removal,
/// a revived pill turns around from where it is, and the scale shares one transform with the bar's FLIP glide.
/// </summary>
internal sealed class ReactionsPill : ToggleButton
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ReactionsPill, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<double> CountProperty =
        AvaloniaProperty.Register<ReactionsPill, double>(nameof(Count));

    public static readonly StyledProperty<double> KickStiffnessProperty =
        AvaloniaProperty.Register<ReactionsPill, double>(nameof(KickStiffness), 420);

    public static readonly StyledProperty<double> KickDampingProperty =
        AvaloniaProperty.Register<ReactionsPill, double>(nameof(KickDamping), 17);

    public static readonly StyledProperty<double> KickScaleVelocityProperty =
        AvaloniaProperty.Register<ReactionsPill, double>(nameof(KickScaleVelocity), 11);

    public static readonly StyledProperty<double> KickRotateVelocityProperty =
        AvaloniaProperty.Register<ReactionsPill, double>(nameof(KickRotateVelocity), -420);

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public double Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public double KickStiffness { get => GetValue(KickStiffnessProperty); set => SetValue(KickStiffnessProperty, value); }
    public double KickDamping { get => GetValue(KickDampingProperty); set => SetValue(KickDampingProperty, value); }
    public double KickScaleVelocity { get => GetValue(KickScaleVelocityProperty); set => SetValue(KickScaleVelocityProperty, value); }
    public double KickRotateVelocity { get => GetValue(KickRotateVelocityProperty); set => SetValue(KickRotateVelocityProperty, value); }

    protected override Type StyleKeyOverride => typeof(ReactionsPill);

    private static readonly ImmutableBlurEffect[] s_blurs = CreateBlurs();

    private MotionValue<double>? _scale;
    private MotionValue<double>? _fade;
    private double _blur;
    private int _generation;

    /// <summary>The reaction id this pill shows.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>True while the exit plays (out of the flow, not focusable).</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Presence scale applied by the bar together with its FLIP offset.</summary>
    public double PresenceScale => _scale?.Value ?? 1;

    /// <summary>The glyph (after the template is applied).</summary>
    public ReactionsGlyph? Glyph { get; private set; }

    private bool _pendingKick;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Glyph = e.NameScope.Find<ReactionsGlyph>("PART_Glyph");
        if (_pendingKick)
        {
            // A pill that joins on a tap kicks as it mounts (Arc's pop count is already ahead of what it has seen).
            _pendingKick = false;
            Kick();
        }
    }

    /// <summary>The value owns the pressed state: a click asks the bar to toggle the reaction instead.</summary>
    protected override void Toggle()
    {
    }

    /// <summary>The tap impulse: adds velocity to the glyph (stacks with a running kick).</summary>
    public void Kick()
    {
        if (Glyph is not { } glyph)
        {
            _pendingKick = true;
            return;
        }
        glyph.Kick(SpringSpec.FromPhysical(KickStiffness, KickDamping), KickScaleVelocity, KickRotateVelocity);
    }

    /// <summary>
    /// Enters: a <paramref name="fresh"/> pill starts at scale 0.5 and transparent; one revived mid-exit turns around from
    /// where it is. Scale on snappy, opacity and the soft blur on fast; Reduced fades only.
    /// </summary>
    public void Enter(ArcMotionLevel level, bool animate, bool fresh)
    {
        _generation++;
        IsLeaving = false;
        Focusable = true;
        IsHitTestVisible = true;
        _scale ??= CreateValue(1);
        _fade ??= CreateValue(1);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _scale.Jump(1);
            _fade.Jump(1);
            Apply();
            return;
        }
        if (fresh)
        {
            _fade.Jump(0);
            _scale.Jump(level == ArcMotionLevel.Full ? 0.5 : 1);
        }
        if (level == ArcMotionLevel.Full)
        {
            _blur = 4;
            _ = _scale.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        else
        {
            _scale.Jump(1);
            _blur = 0;
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
        }
        Apply();
    }

    /// <summary>Leaves (scale 0.6, subtle blur, fast); completes true when the exit finished and was not revived.</summary>
    public async Task<bool> LeaveAsync(ArcMotionLevel level, bool animate)
    {
        var generation = ++_generation;
        IsLeaving = true;
        Focusable = false;
        IsHitTestVisible = false;
        _scale ??= CreateValue(1);
        _fade ??= CreateValue(1);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _fade.Jump(0);
            return generation == _generation;
        }
        Task<bool> fade;
        if (level == ArcMotionLevel.Full)
        {
            _blur = 2;
            var exit = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
            _ = _scale.AnimateTo(0.6, exit);
            fade = _fade.AnimateTo(0, exit);
        }
        else
        {
            _blur = 0;
            fade = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        var done = await fade;
        return done && generation == _generation && IsLeaving;
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => Apply();
        return value;
    }

    private void Apply()
    {
        var fade = Math.Clamp(_fade?.Value ?? 1, 0, 1);
        Opacity = fade;
        var radius = _blur * (1 - fade);
        var index = (int)Math.Round(radius * 4);
        Effect = index <= 0 ? null : s_blurs[Math.Min(index, s_blurs.Length - 1)];
        (this.GetVisualParent() as ReactionsBar)?.UpdateTransform(this);
    }

    private static ImmutableBlurEffect[] CreateBlurs()
    {
        var blurs = new ImmutableBlurEffect[17];
        for (var i = 0; i < blurs.Length; i++)
        {
            blurs[i] = new ImmutableBlurEffect(i * 0.25);
        }
        return blurs;
    }
}

/// <summary>
/// The add button that anchors the picker: it answers a press with color and never scales (the picker is measured
/// against it); its glyph pops between smile-plus and x while the picker is open.
/// </summary>
internal sealed class ReactionsAddButton : AvaloniaButton
{
    public const string OpenPseudoClass = ":open";

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ReactionsAddButton, bool>(nameof(IsOpen));

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ReactionsAddButton, Geometry?>(nameof(Glyph), ArcIcons.SmilePlus);

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>The glyph shown (smile-plus, or x while open).</summary>
    public Geometry? Glyph { get => GetValue(GlyphProperty); private set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(ReactionsAddButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            Glyph = open ? ArcIcons.X : ArcIcons.SmilePlus;
            // Mirrors aria-expanded for assistive technology.
            AutomationProperties.SetItemStatus(this, open ? "Expanded" : "Collapsed");
        }
    }
}

/// <summary>A visually hidden polite status region (Arc's <c>role="status"</c>): every change is announced, repeats included.</summary>
internal sealed class ReactionsLiveRegion : TextBlock
{
    private int _count;

    public ReactionsLiveRegion()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsHitTestVisible = false;
        Focusable = false;
        Opacity = 0;
        Width = 1;
        Height = 1;
        ClipToBounds = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>The last message (without the repeat marker).</summary>
    public string Message { get; private set; } = string.Empty;

    public void Announce(string message)
    {
        Message = message;
        _count++;
        // A repeated message toggles a zero-width space so it is announced again.
        Text = _count % 2 == 0 ? message + "​" : message;
        AutomationProperties.SetName(this, Text);
    }
}

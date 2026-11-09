using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A glowing fluid sphere for a voice assistant (Arc Voice orb, design reference). It breathes slowly at rest, swells
/// with the user's voice and sends out ripples while listening, draws in with a bright band running around its rim while
/// thinking, and grows lumpier and pulses with the assistant's voice while speaking. Muted while listening it goes quiet
/// and gray. Every change of state glides on springs that keep their speed when interrupted.
/// </summary>
/// <remarks>
/// The orb is a picture: it takes no focus and no input (wrap it in a button to make it a tap target). Its colors follow
/// the accent, or <see cref="Tint"/> and <see cref="Tint2"/>. Levels come from <see cref="InputLevel"/> and
/// <see cref="OutputLevel"/>, from <see cref="LevelSource"/>, or from a built-in simulated voice; the orb never opens a
/// microphone. The frame loop pauses while the orb is off screen or hidden; under reduced motion each state is one still
/// frame.
/// </remarks>
public class VoiceOrb : Control
{
    /// <summary>What the assistant is doing; the orb glides to the new state's look.</summary>
    public static readonly StyledProperty<VoiceOrbState> StateProperty =
        AvaloniaProperty.Register<VoiceOrb, VoiceOrbState>(nameof(State));

    /// <summary>
    /// The user's voice level from 0 to 1, read every frame while listening (for example a microphone meter). Not a number
    /// (the default) uses <see cref="LevelSource"/> or the built-in simulated voice.
    /// </summary>
    public static readonly StyledProperty<double> InputLevelProperty =
        AvaloniaProperty.Register<VoiceOrb, double>(nameof(InputLevel), double.NaN);

    /// <summary>
    /// The assistant's voice level from 0 to 1, read every frame while speaking. Not a number (the default) uses pulses
    /// from <see cref="PulseOutput"/>, then <see cref="LevelSource"/> or the built-in simulated voice.
    /// </summary>
    public static readonly StyledProperty<double> OutputLevelProperty =
        AvaloniaProperty.Register<VoiceOrb, double>(nameof(OutputLevel), double.NaN);

    /// <summary>Whether the microphone is muted: while listening the orb shrinks a little, drops its ripples and turns gray.</summary>
    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VoiceOrb, bool>(nameof(IsMuted));

    /// <summary>The orb's width and height (208 by default); the body is about 60 % of it, the rest is halo and ripples.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<VoiceOrb, double>(nameof(Size), 208, validate: v => v > 0 && !double.IsInfinity(v));

    /// <summary>The main color of the body, halo and ripples. Null uses the theme's accent.</summary>
    public static readonly StyledProperty<Color?> TintProperty =
        AvaloniaProperty.Register<VoiceOrb, Color?>(nameof(Tint));

    /// <summary>The second color of the marble's bright veins. Null derives a lighter, softer shade from the tint.</summary>
    public static readonly StyledProperty<Color?> Tint2Property =
        AvaloniaProperty.Register<VoiceOrb, Color?>(nameof(Tint2));

    /// <summary>A fixed accessible name. Null names the orb after its state from <see cref="Labels"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VoiceOrb, string?>(nameof(Label));

    /// <summary>The accessible names per state (English defaults when null).</summary>
    public static readonly StyledProperty<VoiceOrbLabels?> LabelsProperty =
        AvaloniaProperty.Register<VoiceOrb, VoiceOrbLabels?>(nameof(Labels));

    /// <summary>
    /// Where levels come from when <see cref="InputLevel"/> or <see cref="OutputLevel"/> is not set. Null uses
    /// <see cref="VoiceOrbSimulatedLevels.Default"/>.
    /// </summary>
    public static readonly StyledProperty<IVoiceOrbLevelSource?> LevelSourceProperty =
        AvaloniaProperty.Register<VoiceOrb, IVoiceOrbLevelSource?>(nameof(LevelSource));

    /// <summary>The accent color the orb takes without a tint (set by the theme).</summary>
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<VoiceOrb, IBrush?>(nameof(AccentInk));

    /// <summary>The surface color, which tells light from dark themes (set by the theme).</summary>
    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<VoiceOrb, IBrush?>(nameof(SurfaceInk));

    /// <summary>The ripple stroke at size 200 (set by the theme from the token).</summary>
    internal static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<VoiceOrb, double>(nameof(RingThickness), 2.2);

    // A pulse pushed with PulseOutput drives the output for this long after the last one, then the simulated voice
    // takes over again. It decays linearly at this rate (level per second).
    private const double PulseHold = 1.5;
    private const double PulseDecay = 0.9;

    private readonly VoiceOrbModel _model = new();
    private readonly VoiceOrbTexture _texture = new();
    private readonly LiveLoop _loop;
    private readonly double[] _radii = new double[VoiceOrbModel.Points];
    // A new immutable geometry per frame: the compositor keeps the one a recorded frame used, and rebuilding a StreamGeometry
    // costs one small object, where mutating 96 Bézier segments raised 288 property changes (about 45 KB a frame).
    private Geometry _outline = new StreamGeometry();
    private WriteableBitmap? _front;
    private WriteableBitmap? _back;
    private bool _paletteDirty = true;
    private bool _textureDirty = true;
    private (int Side, double Flow, double Twist, double Band, double BandAngle, double Saturation) _textureInputs = (-1, 0, 0, 0, 0, 0);
    private VoiceOrbTexture.Palette _palette;
    private IBrush? _haloBrush;
    private IBrush? _bandGlowBrush;
    private IPen? _ringPen;
    private IPen? _ringSoftPen;
    private double _pulse;
    private double _pulseAt = double.NegativeInfinity;

    static VoiceOrb()
    {
        FocusableProperty.OverrideDefaultValue<VoiceOrb>(false);
        AffectsMeasure<VoiceOrb>(SizeProperty);
        AffectsRender<VoiceOrb>(SizeProperty);
    }

    /// <summary>Creates an idle orb.</summary>
    public VoiceOrb()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        // Why code and not the ControlTheme: the orb is drawn every frame from ten springs coupled to a live level, a
        // breathing sine, drifting harmonics and a ripple phase; none of it is a property a transition could animate.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Full };
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                PoseStill();
            }
        };
        UpdateName();
    }

    /// <inheritdoc cref="StateProperty"/>
    public VoiceOrbState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <inheritdoc cref="InputLevelProperty"/>
    public double InputLevel
    {
        get => GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    /// <inheritdoc cref="OutputLevelProperty"/>
    public double OutputLevel
    {
        get => GetValue(OutputLevelProperty);
        set => SetValue(OutputLevelProperty, value);
    }

    /// <inheritdoc cref="IsMutedProperty"/>
    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="TintProperty"/>
    public Color? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <inheritdoc cref="Tint2Property"/>
    public Color? Tint2
    {
        get => GetValue(Tint2Property);
        set => SetValue(Tint2Property, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public VoiceOrbLabels? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="LevelSourceProperty"/>
    public IVoiceOrbLevelSource? LevelSource
    {
        get => GetValue(LevelSourceProperty);
        set => SetValue(LevelSourceProperty, value);
    }

    internal IBrush? AccentInk
    {
        get => GetValue(AccentInkProperty);
        set => SetValue(AccentInkProperty, value);
    }

    internal IBrush? SurfaceInk
    {
        get => GetValue(SurfaceInkProperty);
        set => SetValue(SurfaceInkProperty, value);
    }

    internal double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    /// <summary>The animated state (tests).</summary>
    internal VoiceOrbModel Model => _model;

    /// <summary>Whether the frame loop runs (tests).</summary>
    internal bool IsLooping => _loop.IsRunning;

    /// <summary>The body texture (tests, snapshots).</summary>
    internal VoiceOrbTexture Texture => _texture;

    /// <summary>The resolved colors: the tint and the light tint (tests).</summary>
    internal (Color Tint, Color Tint2) Colors => ResolveColors(Tint, Tint2, ColorOf(AccentInk, Avalonia.Media.Colors.Gray), IsDark);

    private bool IsDark => SurfaceInk is ISolidColorBrush surface && Luminance(surface.Color) < 0.5;

    /// <summary>
    /// Pushes one pulse of the assistant's voice (0 to 1, for example when a word is spoken). While pulses arrive they
    /// drive the speaking orb instead of the simulated voice; each one rises within a frame and fades over about a second.
    /// Ignored when <see cref="OutputLevel"/> is set.
    /// </summary>
    public void PulseOutput(double strength = 0.75)
    {
        if (double.IsNaN(strength))
        {
            return;
        }
        _pulse = Math.Clamp(strength, 0, 1);
        _pulseAt = _model.Time;
    }

    /// <summary>
    /// The lighter tint the marble's veins take when <see cref="Tint2"/> is not set: brighter (at least 68 % OKLCH
    /// lightness), about half the chroma, warm hues turned toward orange.
    /// </summary>
    public static Color DeriveTint2(Color tint)
    {
        var (l, c, h) = Oklch.FromColor(tint);
        var warm = Math.Max(0, Math.Cos((h - 50) * Math.PI / 180));
        return Oklch.ToColor(Math.Min(0.99, Math.Max(l + 0.2, 0.68)), c * 0.55, h + 40 * warm);
    }

    internal static (Color Tint, Color Tint2) ResolveColors(Color? tint, Color? tint2, Color accent, bool dark)
    {
        var main = tint ?? accent;
        return (main, tint2 ?? DeriveTint2(main));
    }

    /// <summary>Advances the orb by <paramref name="dt"/> seconds (the frame loop calls it; tests may too).</summary>
    internal bool Step(double dt)
    {
        var state = State;
        double level = 0;
        var sharp = false;
        if (state == VoiceOrbState.Listening && !IsMuted)
        {
            var input = InputLevel;
            level = double.IsNaN(input) ? (LevelSource ?? VoiceOrbSimulatedLevels.Default).ReadInput(_model.Time) : input;
        }
        else if (state == VoiceOrbState.Speaking)
        {
            var output = OutputLevel;
            if (!double.IsNaN(output))
            {
                level = output;
            }
            else if (_model.Time - _pulseAt < PulseHold)
            {
                level = Math.Max(0, _pulse - PulseDecay * (_model.Time - _pulseAt));
                sharp = true;
            }
            else
            {
                level = (LevelSource ?? VoiceOrbSimulatedLevels.Default).ReadOutput(_model.Time);
            }
        }
        _model.Step(dt, level, sharp);
        UpdateFrame();
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Size;
        return new Size(size, size);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model.SetState(State, IsMuted);
        _model.Snap();
        _paletteDirty = true;
        PoseStill();
        _loop.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _front?.Dispose();
        _back?.Dispose();
        _front = null;
        _back = null;
        _textureDirty = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StateProperty || property == IsMutedProperty)
        {
            _model.SetState(State, IsMuted);
            UpdateName();
            if (!_loop.IsRunning)
            {
                PoseStill();
            }
        }
        else if (property == LabelProperty || property == LabelsProperty)
        {
            UpdateName();
        }
        else if (property == TintProperty || property == Tint2Property || property == AccentInkProperty ||
                 property == SurfaceInkProperty || property == RingThicknessProperty)
        {
            _paletteDirty = true;
            if (!_loop.IsRunning)
            {
                PoseStill();
            }
            else
            {
                InvalidateVisual();
            }
        }
        else if (property == SizeProperty && !_loop.IsRunning)
        {
            PoseStill();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new VoiceOrbAutomationPeer(this);

    /// <summary>The still frame (reduced motion, motion off, before the loop runs): every parameter at its target.</summary>
    private void PoseStill()
    {
        _model.Snap();
        _model.PoseStill();
        UpdateFrame();
    }

    private void UpdateFrame()
    {
        var size = Size;
        _model.Outline(size, _radii);
        UpdateOutline();
        var paletteChanged = _paletteDirty;
        if (paletteChanged)
        {
            RebuildPalette();
        }
        var side = VoiceOrbTexture.SideFor(size);
        _texture.Ensure(side);
        // The texture depends on these and the palette alone; the outline above changes every frame regardless.
        var inputs = (side, _model.Flow, _model.Twist, _model.BandStrength, _model.BandAngle, _model.SaturationFactor);
        if (paletteChanged || inputs != _textureInputs)
        {
            _textureInputs = inputs;
            _texture.Render(_model, _palette);
            _textureDirty = true;
        }
        InvalidateVisual();
    }

    private void UpdateOutline()
    {
        // A closed Catmull-Rom curve through the 96 outline points, as cubic Béziers.
        var n = _radii.Length;
        Span<Point> points = stackalloc Point[VoiceOrbModel.Points];
        for (var i = 0; i < n; i++)
        {
            var theta = 2 * Math.PI * i / n;
            points[i] = new Point(Math.Cos(theta) * _radii[i], Math.Sin(theta) * _radii[i]);
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            for (var i = 0; i < n; i++)
            {
                var p0 = points[(i - 1 + n) % n];
                var p1 = points[i];
                var p2 = points[(i + 1) % n];
                var p3 = points[(i + 2) % n];
                context.CubicBezierTo(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2);
            }
            context.EndFigure(true);
        }
        _outline = geometry;
    }

    private void RebuildPalette()
    {
        _paletteDirty = false;
        var dark = IsDark;
        var (tint, tint2) = ResolveColors(Tint, Tint2, ColorOf(AccentInk, Avalonia.Media.Colors.Gray), dark);
        var (l, c, h) = Oklch.FromColor(tint);
        var warm = Math.Max(0, Math.Cos((h - 50) * Math.PI / 180));
        var body = Oklch.ToColor(Math.Max(0, l * 0.92), c, h + 18 * (1 - warm) * Math.Min(1, c / 0.05));
        var band = Oklch.MixOklab(tint2, Avalonia.Media.Colors.White, 0.6);
        _palette = new VoiceOrbTexture.Palette(
            body,
            tint2,
            band,
            dark ? Avalonia.Media.Colors.White : Oklch.MixOklab(tint2, Avalonia.Media.Colors.White, 0.4),
            dark ? 0.1 : 0.18,
            dark ? 0.06 : 0.12,
            dark ? 0.45 : 0.08);

        // The halo: the tint fading out from the rim, halving every 0.04·size, gone by 0.47·size (measured at rest:
        // .086 at the rim, .051 at .34, .024 at .38, .008 at .42). The orb's opacity scales it per state.
        var halo = new RadialGradientBrush
        {
            Center = RelativePoint.Center,
            GradientOrigin = RelativePoint.Center,
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative)
        };
        for (var i = 0; i <= 24; i++)
        {
            var r = 0.23 + i * 0.01;
            var alpha = HaloAlpha(r);
            halo.GradientStops.Add(new GradientStop(Oklch.WithAlpha(tint, Math.Min(1, 3 * alpha)), r / VoiceOrbModel.HaloExtent));
        }
        _haloBrush = halo.ToImmutable();

        var glow = new RadialGradientBrush
        {
            Center = RelativePoint.Center,
            GradientOrigin = RelativePoint.Center,
            GradientStops =
            {
                new GradientStop(Oklch.WithAlpha(band, 0.85), 0),
                new GradientStop(Oklch.WithAlpha(band, 0.35), 0.45),
                new GradientStop(Oklch.WithAlpha(band, 0), 1)
            }
        };
        _bandGlowBrush = glow.ToImmutable();
        var scale = Size / 200;
        _ringPen = new ImmutablePen(new ImmutableSolidColorBrush(tint), RingThickness * scale);
        _ringSoftPen = new ImmutablePen(new ImmutableSolidColorBrush(Oklch.WithAlpha(tint, 0.3)), RingThickness * 2.8 * scale);
    }

    /// <summary>The halo's alpha at <paramref name="r"/> (a share of the size) for the idle orb.</summary>
    internal static double HaloAlpha(double r)
    {
        var rr = Math.Max(r, 0.26);
        var decay = Math.Exp(-(rr - 0.30) * Math.Log(2) / 0.04);
        var t = Math.Clamp((r - 0.40) / 0.07, 0, 1);
        var taper = 1 - t * t * (3 - 2 * t);
        return Math.Min(1, 0.086 * decay * taper);
    }

    public override void Render(DrawingContext context)
    {
        var size = Size;
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        if (_paletteDirty)
        {
            // A theme switch while the loop is paused: the next pose rebuilds; draw with what we have.
            RebuildPalette();
        }
        UploadTexture();
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        using var origin = context.PushTransform(Matrix.CreateTranslation(center.X, center.Y));
        var halo = VoiceOrbModel.HaloExtent * size;
        if (_haloBrush is not null)
        {
            using (context.PushOpacity(Math.Clamp(_model.HaloStrength, 0, 3) / 3))
            {
                // The brush holds the idle alpha times three, so states up to three times brighter stay in range.
                context.DrawEllipse(_haloBrush, null, default, halo, halo);
            }
        }
        var mean = _model.MeanRadius(size);
        if (_ringPen is not null && _ringSoftPen is not null)
        {
            for (var ring = 0; ring < 2; ring++)
            {
                var (offset, alpha) = _model.RippleRing(ring, size);
                if (alpha <= 0.004)
                {
                    continue;
                }
                using (context.PushOpacity(alpha))
                {
                    var rr = mean + offset;
                    context.DrawEllipse(null, _ringSoftPen, default, rr, rr);
                    context.DrawEllipse(null, _ringPen, default, rr, rr);
                }
            }
        }
        if (_front is { } picture)
        {
            var extent = VoiceOrbTexture.Extent * mean;
            using (context.PushGeometryClip(_outline))
            {
                context.DrawImage(picture, new Rect(0, 0, picture.PixelSize.Width, picture.PixelSize.Height),
                    new Rect(-extent, -extent, extent * 2, extent * 2));
            }
        }
        var band = _model.BandStrength;
        if (band > 0.01 && _bandGlowBrush is not null)
        {
            // The band's light spills a little past the outline.
            var angle = _model.BandAngle;
            var at = new Point(Math.Cos(angle) * mean * 0.93, Math.Sin(angle) * mean * 0.93);
            using (context.PushOpacity(0.5 * Math.Min(1, band)))
            {
                context.DrawEllipse(_bandGlowBrush, null, at, mean * 0.42, mean * 0.42);
            }
        }
    }

    private void UploadTexture()
    {
        if (!_textureDirty || _texture.Side == 0)
        {
            return;
        }
        _textureDirty = false;
        var side = _texture.Side;
        if (_back is null || _back.PixelSize.Width != side)
        {
            _back?.Dispose();
            _back = new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }
        using (var locked = _back.Lock())
        {
            var pixels = _texture.Pixels;
            if (locked.RowBytes == side * 4)
            {
                Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
            }
            else
            {
                for (var y = 0; y < side; y++)
                {
                    Marshal.Copy(pixels, y * side, locked.Address + y * locked.RowBytes, side);
                }
            }
        }
        (_front, _back) = (_back, _front);
        if (_back is not null && _back.PixelSize.Width != side)
        {
            _back.Dispose();
            _back = null;
        }
    }

    private void UpdateName()
    {
        var name = Label ?? (Labels ?? s_defaultLabels).For(State, IsMuted);
        AutomationProperties.SetName(this, name);
    }

    private static readonly VoiceOrbLabels s_defaultLabels = new();

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;

    private sealed class VoiceOrbAutomationPeer(VoiceOrb owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}

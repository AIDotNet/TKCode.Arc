using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A pixel-art robot on a 24 × 24 grid that shows what an assistant is doing (Arc Pixel mascot, design reference). It
/// idles, thinks, types, celebrates and errors in frame-stepped sprite animations on a fixed 40ms clock, and status
/// changes morph from the current pose one pixel at a time. Every sprite pixel is <see cref="Scale"/> device-independent
/// pixels, snapped to whole device pixels, so it stays crisp at every size from 24px to 192px.
/// <para>
/// Set <see cref="Status"/> from your request lifecycle, or leave it null to play the built-in loop through every state.
/// The palette follows the theme neutrals and <see cref="AccentBrush"/> (the theme's accent by default).
/// </para>
/// </summary>
public class PixelMascot : Control
{
    /// <summary>The smallest and largest <see cref="Scale"/>.</summary>
    public const int MinScale = 1;

    /// <inheritdoc cref="MinScale"/>
    public const int MaxScale = 8;

    /// <summary>What the assistant is doing. Leave it null to play the built-in loop through every state.</summary>
    public static readonly StyledProperty<PixelMascotStatus?> StatusProperty =
        AvaloniaProperty.Register<PixelMascot, PixelMascotStatus?>(nameof(Status));

    /// <summary>
    /// Pixel scale from 1 to 8: each sprite pixel is this many device-independent pixels, so 1 renders 24px, 2 renders
    /// 48px, 4 renders 96px and 8 renders 192px. Other values clamp.
    /// </summary>
    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<PixelMascot, int>(nameof(Scale), 4, coerce: (_, v) => Math.Clamp(v, MinScale, MaxScale));

    /// <summary>Who the mascot stands for, used in the accessible label ("Assistant is thinking").</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<PixelMascot, string>(nameof(AssistantName), "Assistant");

    /// <summary>Replaces the generated accessible label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PixelMascot, string?>(nameof(Label));

    /// <summary>
    /// Announces status changes politely to assistive technology. Null (the default) announces when <see cref="Status"/>
    /// is set and stays quiet for the built-in loop. Turn it off when nearby text already announces progress.
    /// </summary>
    public static readonly StyledProperty<bool?> AnnounceProperty =
        AvaloniaProperty.Register<PixelMascot, bool?>(nameof(Announce));

    /// <summary>Freezes the animation on the current frame. The mascot also pauses by itself while off screen.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<PixelMascot, bool>(nameof(IsPaused));

    /// <summary>The accent of the antenna, chest light, typed bar, sparkles and the eyes' glow; the theme's accent by default.</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(AccentBrush));

    /// <summary>Every word the mascot says.</summary>
    public static readonly StyledProperty<PixelMascotLabels> LabelsProperty =
        AvaloniaProperty.Register<PixelMascot, PixelMascotLabels>(nameof(Labels), PixelMascotLabels.Default);

    /// <summary>The status the mascot shows: <see cref="Status"/>, or the built-in loop's current step when it is null.</summary>
    public static readonly DirectProperty<PixelMascot, PixelMascotStatus> ShownStatusProperty =
        AvaloniaProperty.RegisterDirect<PixelMascot, PixelMascotStatus>(nameof(ShownStatus), o => o.ShownStatus);

    /// <summary>Raised when <see cref="ShownStatus"/> changes, with the old and new status. Bubbles.</summary>
    public static readonly RoutedEvent<PixelMascotStatusChangedEventArgs> ShownStatusChangedEvent =
        RoutedEvent.Register<PixelMascot, PixelMascotStatusChangedEventArgs>(nameof(ShownStatusChanged), RoutingStrategies.Bubble);

    // Ink: theme colors captured into the sprite palette.
    internal static readonly StyledProperty<IBrush?> OutlineInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(OutlineInk));
    internal static readonly StyledProperty<IBrush?> ShellInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(ShellInk));
    internal static readonly StyledProperty<IBrush?> ShadeInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(ShadeInk));
    internal static readonly StyledProperty<IBrush?> ScreenInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(ScreenInk));
    internal static readonly StyledProperty<IBrush?> ShadowInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(ShadowInk));
    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<PixelMascot, IBrush?>(nameof(DangerInk));

    /// <summary>The longest catch-up after a stalled frame: the clock drops time instead of racing through steps.</summary>
    private const int MaxStepsPerFrame = 6;

    private readonly LiveLoop _loop;
    private readonly ImmutableSolidColorBrush?[] _palette = new ImmutableSolidColorBrush?[PixelMascotSprite.PaletteSize];
    private readonly double[] _edgesX = new double[PixelMascotSprite.Size + 1];
    private readonly double[] _edgesY = new double[PixelMascotSprite.Size + 1];
    private byte[] _grid = new byte[PixelMascotSprite.Size * PixelMascotSprite.Size];
    private byte[] _next = new byte[PixelMascotSprite.Size * PixelMascotSprite.Size];
    private PixelMascotPose _pose = PixelMascotPose.Rest;
    private PixelMascotStatus _shown;
    private int _tick;
    private double _pending;
    private double _loopTime;
    private bool _paletteDirty = true;

    static PixelMascot()
    {
        AffectsMeasure<PixelMascot>(ScaleProperty);
        AffectsRender<PixelMascot>(ScaleProperty);
        FocusableProperty.OverrideDefaultValue<PixelMascot>(false);
    }

    /// <summary>Creates the mascot in its idle resting pose.</summary>
    public PixelMascot()
    {
        _loop = new LiveLoop(this, OnFrame);
        _loop.RunningChanged += OnRunningChanged;
        PixelMascotSprite.Compose(_pose, _grid);
        UpdateAccessibility();
        UpdateLoop();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public PixelMascotStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ScaleProperty"/>
    public int Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool? Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public PixelMascotLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ShownStatusProperty"/>
    public PixelMascotStatus ShownStatus
    {
        get => _shown;
        private set => SetAndRaise(ShownStatusProperty, ref _shown, value);
    }

    /// <inheritdoc cref="ShownStatusChangedEvent"/>
    public event EventHandler<PixelMascotStatusChangedEventArgs>? ShownStatusChanged
    {
        add => AddHandler(ShownStatusChangedEvent, value);
        remove => RemoveHandler(ShownStatusChangedEvent, value);
    }

    internal IBrush? OutlineInk { get => GetValue(OutlineInkProperty); set => SetValue(OutlineInkProperty, value); }
    internal IBrush? ShellInk { get => GetValue(ShellInkProperty); set => SetValue(ShellInkProperty, value); }
    internal IBrush? ShadeInk { get => GetValue(ShadeInkProperty); set => SetValue(ShadeInkProperty, value); }
    internal IBrush? ScreenInk { get => GetValue(ScreenInkProperty); set => SetValue(ScreenInkProperty, value); }
    internal IBrush? ShadowInk { get => GetValue(ShadowInkProperty); set => SetValue(ShadowInkProperty, value); }
    internal IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }

    /// <summary>The current pose (tests).</summary>
    internal PixelMascotPose Pose => _pose;

    /// <summary>Ticks into the shown status (tests).</summary>
    internal int Tick => _tick;

    /// <summary>The palette index at a sprite pixel (tests).</summary>
    internal byte PixelAt(int x, int y) => _grid[y * PixelMascotSprite.Size + x];

    /// <summary>The frame loop (tests).</summary>
    internal LiveLoop Loop => _loop;

    /// <summary>The device-pixel edges used by the last render, in local coordinates (tests).</summary>
    internal ReadOnlySpan<double> EdgesX => _edgesX;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var side = PixelMascotSprite.Size * Scale;
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = PixelMascotSprite.Size * Scale;
        return new Size(side, side);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PixelMascotAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty)
        {
            if (Status is { } status)
            {
                Show(status);
            }
            else
            {
                Show(PixelMascotLoop.StatusAt(TimeSpan.FromSeconds(_loopTime)));
            }
            UpdateLoop();
            UpdateAccessibility();
        }
        else if (property == IsPausedProperty)
        {
            UpdateLoop();
        }
        else if (property == AssistantNameProperty || property == LabelProperty || property == LabelsProperty
                 || property == AnnounceProperty)
        {
            UpdateAccessibility();
        }
        else if (property == AccentBrushProperty || property == OutlineInkProperty || property == ShellInkProperty
                 || property == ShadeInkProperty || property == ScreenInkProperty || property == ShadowInkProperty
                 || property == DangerInkProperty)
        {
            _paletteDirty = true;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_paletteDirty)
        {
            BuildPalette();
        }
        ComputeEdges();
        const int n = PixelMascotSprite.Size;
        using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
        {
            for (var y = 0; y < n; y++)
            {
                var top = _edgesY[y];
                var height = _edgesY[y + 1] - top;
                var x = 0;
                while (x < n)
                {
                    var index = _grid[y * n + x];
                    var start = x;
                    while (x < n && _grid[y * n + x] == index)
                    {
                        x++;
                    }
                    if (index == PixelMascotSprite.Empty || _palette[index] is not { } brush)
                    {
                        continue;
                    }
                    context.FillRectangle(brush, new Rect(_edgesX[start], top, _edgesX[x] - _edgesX[start], height));
                }
            }
        }
    }

    /// <summary>
    /// The edges of the sprite's pixels in local coordinates, on whole device pixels: the control's offset on the
    /// device grid is measured so a fractional layout position or render scaling never smears a pixel across two.
    /// </summary>
    private void ComputeEdges()
    {
        var top = TopLevel.GetTopLevel(this);
        var scaling = top?.RenderScaling ?? 1;
        var origin = top is null ? default : this.TranslatePoint(default, top) ?? default;
        var ox = origin.X * scaling;
        var oy = origin.Y * scaling;
        var step = Scale * scaling;
        for (var i = 0; i <= PixelMascotSprite.Size; i++)
        {
            _edgesX[i] = (Math.Round(ox + i * step) - ox) / scaling;
            _edgesY[i] = (Math.Round(oy + i * step) - oy) / scaling;
        }
    }

    private void BuildPalette()
    {
        _paletteDirty = false;
        var outline = ColorOf(OutlineInk, Colors.Black);
        var shell = ColorOf(ShellInk, Colors.White);
        var shade = ColorOf(ShadeInk, Colors.LightGray);
        var screen = ColorOf(ScreenInk, Color.FromRgb(0x15, 0x15, 0x15));
        var accent = ColorOf(AccentBrush, Colors.DimGray);
        var danger = ColorOf(DangerInk, Colors.IndianRed);
        // The eyes glow in the accent's hue on the dark screen: the accent lifted to at least 0.84 lightness (OKLCH).
        var (l, c, h) = Oklch.FromColor(accent);
        var eye = Oklch.ToColor(Math.Max(l, 0.84), c, h);
        Set(PixelMascotSprite.Outline, outline);
        Set(PixelMascotSprite.Shell, shell);
        Set(PixelMascotSprite.Shade, shade);
        Set(PixelMascotSprite.Screen, screen);
        Set(PixelMascotSprite.Eye, eye);
        Set(PixelMascotSprite.Accent, accent);
        Set(PixelMascotSprite.Shadow, ColorOf(ShadowInk, Colors.Gainsboro));
        Set(PixelMascotSprite.Danger, danger);
        Set(PixelMascotSprite.EyeDim, Oklch.MixOklab(screen, eye, 0.4));
        Set(PixelMascotSprite.ScreenTintHalf, Oklch.MixOklab(screen, danger, 0.14));
        Set(PixelMascotSprite.ScreenTintFull, Oklch.MixOklab(screen, danger, 0.28));

        void Set(byte index, Color color) => _palette[index] = new ImmutableSolidColorBrush(color);
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : fallback;

    // Sets the shown status. Animating, the parts head for the new status from where they are; otherwise (reduced motion,
    // off, paused or off screen) the sprite holds the new status's resting pose.
    private void Show(PixelMascotStatus status)
    {
        var old = _shown;
        if (old == status)
        {
            return;
        }
        ShownStatus = status;
        _tick = 0;
        if (!Animates)
        {
            Snap();
        }
        UpdateAccessibility();
        RaiseEvent(new PixelMascotStatusChangedEventArgs(ShownStatusChangedEvent, old, status) { Source = this });
    }

    private bool Animates => _loop.IsRunning && ArcMotion.IsFull(this);

    private void Snap()
    {
        _pose = PixelMascotMotion.Rest(_shown);
        _pending = 0;
        Publish();
    }

    private void Publish()
    {
        PixelMascotSprite.Compose(_pose, _next);
        if (_next.AsSpan().SequenceEqual(_grid))
        {
            return;
        }
        (_grid, _next) = (_next, _grid);
        InvalidateVisual();
    }

    private void UpdateLoop()
    {
        // The built-in loop keeps cycling statuses under reduced motion (each shown as its resting pose); a controlled
        // mascot has nothing to do there and lets its loop stop.
        _loop.MinimumLevel = Status is null ? ArcMotionLevel.Reduced : ArcMotionLevel.Full;
        if (IsPaused)
        {
            _loop.Stop();
        }
        else
        {
            _loop.Start();
        }
    }

    private void OnRunningChanged(bool running)
    {
        _pending = 0;
        if (!ArcMotion.IsFull(this))
        {
            Snap();
        }
    }

    private bool OnFrame(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.25);
        if (Status is null)
        {
            _loopTime = (_loopTime + dt) % PixelMascotLoop.CycleDuration.TotalSeconds;
            Show(PixelMascotLoop.StatusAt(TimeSpan.FromSeconds(_loopTime)));
        }
        if (!ArcMotion.IsFull(this))
        {
            return true;
        }
        _pending += dt;
        var steps = 0;
        while (_pending >= PixelMascotMotion.TickSeconds - 1e-9)
        {
            _pending -= PixelMascotMotion.TickSeconds;
            if (++steps > MaxStepsPerFrame)
            {
                _pending = 0;
                break;
            }
            Advance();
        }
        if (steps > 0)
        {
            Publish();
        }
        return true;
    }

    /// <summary>One 40ms step of the sprite clock.</summary>
    internal void Advance()
    {
        var target = PixelMascotMotion.Target(_shown, _tick);
        PixelMascotMotion.Step(ref _pose, target, _shown == PixelMascotStatus.Writing);
        _tick++;
    }

    /// <summary>Runs <paramref name="ticks"/> steps of the sprite clock and redraws (tests).</summary>
    internal void AdvanceTicks(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Advance();
        }
        Publish();
    }

    private void UpdateAccessibility()
    {
        var label = Label ?? Labels.Describe(AssistantName, _shown);
        SetValue(AutomationProperties.NameProperty, label);
        var announce = Announce ?? Status is not null;
        SetValue(AutomationProperties.LiveSettingProperty, announce ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
    }
}

/// <summary>The mascot reads as one image named after what the assistant is doing.</summary>
internal sealed class PixelMascotAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

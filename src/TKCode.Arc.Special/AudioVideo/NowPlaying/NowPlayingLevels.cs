using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Three small level bars that dance while a track plays and rest while paused (Arc Now playing's levels, design
/// reference): 3 × 14 bars 2 apart, growing from the bottom. Playing, each bar swings between 25% and 100% of its height
/// (ease-in-out, back and forth) with its own period — 900, 640 and 1100ms, started 0, 300 and 500ms in — at full
/// opacity. Paused, they settle in 240ms to 30%, 60% and 42% at 55% opacity. Reduced motion holds them still while
/// playing (70%, 100%, 70%). Decorative: hidden from assistive tech.
/// </summary>
public sealed class NowPlayingLevels : Control
{
    /// <summary>Whether a track is playing: the bars move only while true.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<NowPlayingLevels, bool>(nameof(IsPlaying));

    /// <summary>The bar color (inherited; the theme uses the text color).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<NowPlayingLevels>();

    internal static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<NowPlayingLevels, double>(nameof(BarWidth), 3);

    internal static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<NowPlayingLevels, double>(nameof(BarHeight), 14);

    internal static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<NowPlayingLevels, double>(nameof(BarGap), 2);

    internal static readonly StyledProperty<double> BarRadiusProperty =
        AvaloniaProperty.Register<NowPlayingLevels, double>(nameof(BarRadius), 1);

    internal static readonly StyledProperty<double> RestOpacityProperty =
        AvaloniaProperty.Register<NowPlayingLevels, double>(nameof(RestOpacity), 0.55);

    /// <summary>Resting heights (fractions of the bar height).</summary>
    internal static readonly double[] RestScales = [0.3, 0.6, 0.42];

    /// <summary>Still heights while playing under reduced motion.</summary>
    internal static readonly double[] ReducedScales = [0.7, 1, 0.7];

    /// <summary>One sweep (25% to 100% or back), in seconds.</summary>
    internal static readonly double[] Periods = [0.9, 0.64, 1.1];

    /// <summary>How far into its swing each bar starts, in seconds (CSS negative delays).</summary>
    internal static readonly double[] Phases = [0, 0.3, 0.5];

    internal const double MinScale = 0.25;

    private readonly double[] _scales = [0.3, 0.6, 0.42];
    private readonly double[] _from = new double[3];
    private LiveLoop? _loop;
    private MotionValue<double>? _settle;
    private MotionValue<double>? _opacity;
    private double _time;
    private double _drawOpacity = 0.55;
    private bool _loaded;
    private bool _syncing;

    static NowPlayingLevels()
    {
        AffectsRender<NowPlayingLevels>(ForegroundProperty, BarRadiusProperty);
        AffectsMeasure<NowPlayingLevels>(BarWidthProperty, BarHeightProperty, BarGapProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<NowPlayingLevels>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<NowPlayingLevels>(AccessibilityView.Raw);
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    internal double BarWidth { get => GetValue(BarWidthProperty); set => SetValue(BarWidthProperty, value); }

    internal double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    internal double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }

    internal double BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }

    internal double RestOpacity { get => GetValue(RestOpacityProperty); set => SetValue(RestOpacityProperty, value); }

    /// <summary>The bar heights drawn now, as fractions (tests read them).</summary>
    internal IReadOnlyList<double> Scales => _scales;

    /// <summary>The opacity the bars are drawn with now.</summary>
    internal double DrawOpacity => _drawOpacity;

    /// <summary>True while the dance loop runs.</summary>
    internal bool IsDancing => _loop?.IsRunning == true;

    /// <summary>The height of bar <paramref name="index"/> (0..2) at <paramref name="seconds"/> into the dance: 0.25 to 1.</summary>
    internal static double DanceScale(int index, double seconds)
    {
        var period = Periods[index];
        var u = (seconds + Phases[index]) / period;
        var cycle = Math.Floor(u);
        var f = u - cycle;
        // Alternate: odd sweeps run back down.
        if (((long)cycle & 1) == 1)
        {
            f = 1 - f;
        }
        return MinScale + (1 - MinScale) * NowPlayingMotion.EaseInOut.Ease(f);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(3 * BarWidth + 2 * BarGap, BarHeight);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loaded = true;
        _loop ??= CreateLoop();
        Sync(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loaded = false;
        _loop?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            Sync(_loaded);
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == RestOpacityProperty)
        {
            Sync(false);
        }
    }

    private LiveLoop CreateLoop()
    {
        var loop = new LiveLoop(this, dt =>
        {
            _time += dt;
            for (var i = 0; i < 3; i++)
            {
                _scales[i] = DanceScale(i, _time);
            }
            InvalidateVisual();
            return true;
        });
        // Outside full motion (or off screen) the loop pauses: draw the still frame of the state.
        loop.RunningChanged += running =>
        {
            if (!running && !_syncing)
            {
                Sync(false);
            }
        };
        return loop;
    }

    private void Sync(bool animate)
    {
        _syncing = true;
        try
        {
            SyncCore(animate);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncCore(bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var full = level == ArcMotionLevel.Full;
        var playing = IsPlaying;
        var opacityTarget = playing ? 1 : RestOpacity;
        _opacity ??= CreateValue(_drawOpacity, v =>
        {
            _drawOpacity = v;
            InvalidateVisual();
        });
        if (animate)
        {
            _ = _opacity.AnimateTo(opacityTarget, MotionSpec.Tween(NowPlayingMotion.Enter, ArcEasings.Standard));
        }
        else
        {
            _opacity.Jump(opacityTarget);
        }

        if (playing && full)
        {
            _settle?.Stop();
            // The dance starts where its keyframes are (CSS applies the animation at once).
            for (var i = 0; i < 3; i++)
            {
                _scales[i] = DanceScale(i, _time);
            }
            _loop?.Start();
            InvalidateVisual();
            return;
        }
        _loop?.Stop();
        var target = playing ? ReducedScales : RestScales;
        if (animate && level != ArcMotionLevel.Off)
        {
            // A transition from wherever the bars are to the still heights (240ms, standard curve).
            Array.Copy(_scales, _from, 3);
            _settle ??= CreateValue(1, SettleStep);
            _settle.Jump(0);
            _settleTarget = target;
            _ = _settle.AnimateTo(1, MotionSpec.Tween(NowPlayingMotion.Enter, ArcEasings.Standard));
        }
        else
        {
            _settle?.Stop();
            Array.Copy(target, _scales, 3);
            InvalidateVisual();
        }
    }

    private double[] _settleTarget = RestScales;

    private void SettleStep(double k)
    {
        for (var i = 0; i < 3; i++)
        {
            _scales[i] = _from[i] + (_settleTarget[i] - _from[i]) * k;
        }
        InvalidateVisual();
    }

    private MotionValue<double> CreateValue(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        var w = BarWidth;
        var gap = BarGap;
        var h = BarHeight;
        var radius = BarRadius;
        var top = Bounds.Height - h;
        using (context.PushOpacity(_drawOpacity))
        {
            for (var i = 0; i < 3; i++)
            {
                var barHeight = h * _scales[i];
                // Bars grow from the bottom (scaleY with a bottom origin), so the radius scales with them.
                var r = Math.Min(radius, barHeight / 2);
                context.DrawRectangle(brush, null,
                    new RoundedRect(new Rect(i * (w + gap), top + h - barHeight, w, barHeight), r));
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}

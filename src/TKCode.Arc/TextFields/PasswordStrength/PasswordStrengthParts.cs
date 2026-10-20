using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The four-step strength meter (P23): each fill slides in from the left inside its clipped, rounded track on the
/// smooth spring, in a staggered wave (a pasted password fills left to right, clearing empties right to left),
/// and the tone (danger, warning, success) changes colour on the standard tween. Drawn in one control.
/// </summary>
internal sealed class PasswordStrengthMeter : Control
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> EmptyToneProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, IBrush?>(nameof(EmptyTone));

    public static readonly StyledProperty<IBrush?> WeakToneProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, IBrush?>(nameof(WeakTone));

    public static readonly StyledProperty<IBrush?> FairToneProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, IBrush?>(nameof(FairTone));

    public static readonly StyledProperty<IBrush?> StrongToneProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, IBrush?>(nameof(StrongTone));

    public static readonly StyledProperty<double> SegmentHeightProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, double>(nameof(SegmentHeight), 4);

    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<PasswordStrengthMeter, double>(nameof(SegmentGap), 5);

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? EmptyTone { get => GetValue(EmptyToneProperty); set => SetValue(EmptyToneProperty, value); }
    public IBrush? WeakTone { get => GetValue(WeakToneProperty); set => SetValue(WeakToneProperty, value); }
    public IBrush? FairTone { get => GetValue(FairToneProperty); set => SetValue(FairToneProperty, value); }
    public IBrush? StrongTone { get => GetValue(StrongToneProperty); set => SetValue(StrongToneProperty, value); }
    public double SegmentHeight { get => GetValue(SegmentHeightProperty); set => SetValue(SegmentHeightProperty, value); }
    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    /// <summary>Current level (0 to 4).</summary>
    public int Level { get; private set; }

    /// <summary>The value text read by assistive technology.</summary>
    public string ValueText { get; private set; } = string.Empty;

    private readonly MotionValue<double>?[] _fills = new MotionValue<double>?[4];
    private MotionValue<Color>? _tone;
    private readonly ImmutableSolidColorBrush?[] _toneCache = new ImmutableSolidColorBrush?[1];
    private Color _toneCacheColor;

    static PasswordStrengthMeter()
    {
        AffectsMeasure<PasswordStrengthMeter>(SegmentHeightProperty);
        AffectsRender<PasswordStrengthMeter>(TrackBrushProperty, SegmentGapProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PasswordStrengthMeter>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EmptyToneProperty || change.Property == WeakToneProperty ||
            change.Property == FairToneProperty || change.Property == StrongToneProperty)
        {
            // Theme changes snap the tone.
            _tone?.Jump(ToneColor(Level));
            InvalidateVisual();
        }
    }

    private Color ToneColor(int level)
    {
        var brush = level switch
        {
            1 => WeakTone,
            2 => FairTone,
            >= 3 => StrongTone,
            _ => EmptyTone
        };
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
    }

    /// <summary>Moves to <paramref name="level"/>; <paramref name="previousLevel"/> orders the wave.</summary>
    public void SetLevel(int level, int previousLevel, string valueText)
    {
        ValueText = valueText;
        AutomationProperties.SetItemStatus(this, valueText);
        var old = Level;
        Level = level;
        var animate = IsLoaded && ArcMotion.IsFull(this);
        for (var i = 0; i < 4; i++)
        {
            var on = i < level;
            var fill = _fills[i] ??= CreateFill(i < old ? 1 : 0);
            var target = on ? 1.0 : 0.0;
            if (Math.Abs(fill.Target - target) < 0.001)
            {
                continue;
            }
            if (!animate)
            {
                fill.Jump(target);
                continue;
            }
            var wave = on ? i - previousLevel : previousLevel - 1 - i;
            var delay = TimeSpan.FromSeconds(Math.Max(0, wave) * (on ? 0.05 : 0.03));
            _ = fill.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth, delay));
        }
        _tone ??= CreateTone(ToneColor(old));
        if (animate)
        {
            _ = _tone.AnimateTo(ToneColor(level), MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        else
        {
            _tone.Jump(ToneColor(level));
        }
        if (old != level)
        {
            if (ControlAutomationPeer.FromElement(this) is { } peer)
            {
                peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)old, (double)level);
            }
        }
        InvalidateVisual();
    }

    private MotionValue<double> CreateFill(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private MotionValue<Color> CreateTone(Color initial)
    {
        var value = new MotionValue<Color>(this, initial, MotionVectors.Color);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, SegmentHeight);

    public override void Render(DrawingContext context)
    {
        var gap = SegmentGap;
        var height = SegmentHeight;
        var width = (Bounds.Width - 3 * gap) / 4;
        if (width <= 0)
        {
            return;
        }
        var radius = height / 2;
        var tone = ToneBrush(_tone?.Value ?? ToneColor(Level));
        var y = (Bounds.Height - height) / 2;
        for (var i = 0; i < 4; i++)
        {
            var rect = new Rect(i * (width + gap), y, width, height);
            var rounded = new RoundedRect(rect, radius);
            if (TrackBrush is { } track)
            {
                context.DrawRectangle(track, null, rounded);
            }
            var p = _fills[i]?.Value ?? (i < Level ? 1 : 0);
            if (p <= 0.001)
            {
                continue;
            }
            // The fill keeps its rounded ends at every frame: it slides inside the clipped track.
            var offset = -1.01 * width * (1 - p);
            using (context.PushClip(rounded))
            {
                context.DrawRectangle(tone, null, new RoundedRect(rect.Translate(new Vector(offset, 0)), radius));
            }
        }
    }

    private IBrush ToneBrush(Color color)
    {
        if (_toneCache[0] is null || _toneCacheColor != color)
        {
            _toneCacheColor = color;
            _toneCache[0] = new ImmutableSolidColorBrush(color);
        }
        return _toneCache[0]!;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MeterPeer(this);

    private sealed class MeterPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public MeterPeer(PasswordStrengthMeter owner)
            : base(owner)
        {
        }

        private PasswordStrengthMeter Meter => (PasswordStrengthMeter)Owner;

        public bool IsReadOnly => true;
        public double Minimum => 0;
        public double Maximum => 4;
        public double Value => Meter.Level;
        public double SmallChange => 1;
        public double LargeChange => 1;

        public void SetValue(double value)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A requirement's mark (P15): an outlined 16px circle hands over to a faint success tint that grows from 0.5 on the
/// snappy spring while the tick draws from its short stroke; unchecking retracts the tick faster than it drew.
/// </summary>
internal sealed class PasswordRuleMark : Control
{
    public static readonly StyledProperty<IBrush?> OutlineBrushProperty =
        AvaloniaProperty.Register<PasswordRuleMark, IBrush?>(nameof(OutlineBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<PasswordRuleMark, IBrush?>(nameof(SuccessBrush));

    public IBrush? OutlineBrush { get => GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }
    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    public bool IsMet { get; private set; }

    private const int TickSteps = 32;
    private static readonly Geometry s_tick = Geometry.Parse("M4.75 8.25 7 10.5l4.25-4.75");
    private static readonly Geometry?[] s_tickSegments = new Geometry?[TickSteps + 1];

    private MotionValue<double>? _fill;
    private MotionValue<double>? _fillOpacity;
    private MotionValue<double>? _tick;
    private MotionValue<double>? _tickOpacity;
    private MotionValue<double>? _outline;
    private IPen? _outlinePen;
    private IBrush? _outlinePenBrush;
    private IPen? _tickPen;
    private IBrush? _tickPenBrush;
    private IBrush? _tint;
    private IBrush? _tintSource;

    static PasswordRuleMark()
    {
        AffectsRender<PasswordRuleMark>(OutlineBrushProperty, SuccessBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PasswordRuleMark>(false);
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public void SetMet(bool met, TimeSpan delay)
    {
        if (_fill is not null && met == IsMet)
        {
            return;
        }
        var first = _fill is null;
        IsMet = met;
        var target = met ? 1.0 : 0.0;
        _fill ??= Create(0.5 + 0.5 * target);
        _fillOpacity ??= Create(target);
        _tick ??= Create(target);
        _tickOpacity ??= Create(target);
        _outline ??= Create(1 - target);
        if (first || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _fill.Jump(met ? 1 : 0.5);
            _fillOpacity.Jump(target);
            _tick.Jump(target);
            _tickOpacity.Jump(target);
            _outline.Jump(1 - target);
            InvalidateVisual();
            return;
        }
        var tickDelay = delay + TimeSpan.FromMilliseconds(60);
        _ = _fill.AnimateTo(met ? 1 : 0.5, MotionSpec.Spring(ArcSpring.Snappy, delay));
        _ = _fillOpacity.AnimateTo(target, MotionSpec.Tween(met ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, ArcEasings.Standard, delay));
        _ = _outline.AnimateTo(1 - target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        if (met)
        {
            _ = _tick.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, tickDelay));
            _ = _tickOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(50), ArcEasings.Standard, tickDelay));
        }
        else
        {
            _ = _tick.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = _tickOpacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, TimeSpan.FromMilliseconds(60)));
        }
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(8, 8);
        var outline = Math.Clamp(_outline?.Value ?? 1, 0, 1);
        if (outline > 0.001 && OutlineBrush is { } outlineBrush)
        {
            if (_outlinePen is null || !ReferenceEquals(_outlinePenBrush, outlineBrush))
            {
                _outlinePenBrush = outlineBrush;
                _outlinePen = new ImmutablePen(outlineBrush.ToImmutable(), 1.25);
            }
            using (context.PushOpacity(outline))
            {
                context.DrawEllipse(null, _outlinePen, center, 8 - 0.625, 8 - 0.625);
            }
        }
        if (SuccessBrush is not { } success)
        {
            return;
        }
        var fillOpacity = Math.Clamp(_fillOpacity?.Value ?? 0, 0, 1);
        if (fillOpacity > 0.001)
        {
            if (_tint is null || !ReferenceEquals(_tintSource, success))
            {
                _tintSource = success;
                var color = success is ISolidColorBrush solid ? solid.Color : Colors.Green;
                _tint = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(0.16 * color.A), color.R, color.G, color.B));
            }
            var scale = _fill?.Value ?? 1;
            var r = 8 * Math.Max(0, scale);
            using (context.PushOpacity(fillOpacity))
            {
                context.DrawEllipse(_tint, null, center, r, r);
            }
        }
        var tick = Math.Clamp(_tick?.Value ?? 0, 0, 1);
        var tickOpacity = Math.Clamp(_tickOpacity?.Value ?? 0, 0, 1);
        if (tick > 0.001 && tickOpacity > 0.001)
        {
            if (_tickPen is null || !ReferenceEquals(_tickPenBrush, success))
            {
                _tickPenBrush = success;
                _tickPen = new ImmutablePen(success.ToImmutable(), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            }
            using (context.PushOpacity(tickOpacity))
            {
                context.DrawGeometry(null, _tickPen, TickSegment(tick));
            }
        }
    }

    private static Geometry TickSegment(double progress)
    {
        var step = (int)Math.Round(progress * TickSteps);
        if (s_tickSegments[step] is { } cached)
        {
            return cached;
        }
        Geometry segment = s_tick;
        if (step < TickSteps && s_tick.TryGetSegment(0, s_tick.ContourLength * step / TickSteps, true, out var part) && part is not null)
        {
            segment = part;
        }
        s_tickSegments[step] = segment;
        return segment;
    }
}

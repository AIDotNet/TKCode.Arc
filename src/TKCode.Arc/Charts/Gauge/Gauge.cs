using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A 270° ring gauge (Arc "Gauge"): one value against a known range, read as a percentage with an optional named
/// state from <see cref="Thresholds"/>. The ring fills once when half of it scrolls into view, then springs to every
/// new value while the percentage rolls (Core <see cref="RollingDigits"/>); the state color and label switch exactly
/// when the moving count crosses a band. Reduced and Off motion land on the value at once.
/// </summary>
public class Gauge : Control
{
    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Value));

    /// <summary>Defines the <see cref="Minimum"/> property.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Minimum));

    /// <summary>Defines the <see cref="Maximum"/> property.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(Maximum), 100);

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Gauge, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Detail"/> property.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<Gauge, string?>(nameof(Detail));

    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<GaugeTone> ToneProperty =
        AvaloniaProperty.Register<Gauge, GaugeTone>(nameof(Tone));

    internal static readonly StyledProperty<double> DialMaxWidthProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(DialMaxWidth), 176);

    internal static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(RingThickness), 9);

    internal static readonly StyledProperty<double> CaptionGapProperty =
        AvaloniaProperty.Register<Gauge, double>(nameof(CaptionGap), 10);

    private const double Start = -135;
    private const double Span = 270;
    private const double Radius = 42;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBReveal _reveal;
    private readonly RollingDigits _digits;
    private readonly ArcContentSwap _status;
    private readonly ArcContentSwap _label;
    private readonly ArcContentSwap _detail;
    private readonly MotionValue<double> _sweep;
    private readonly MotionValue<double> _count;
    private readonly MotionValue<Color> _ringColor;
    private readonly MotionValue<Color> _textColor;
    private IList<GaugeThreshold> _thresholds;
    // The bands list may be a view model's, and the bands' properties may be bound: they are observed only while the
    // gauge is shown, and re-read when it comes back.
    private readonly ObservedItems _observedThresholds;
    private bool _watching;
    private GaugeThreshold[] _bands = [];
    private GaugeThreshold? _live;
    private bool _filled;
    private bool _toneInitialized;
    private int _shownPercent;
    private FormattedText? _unit;
    private int _unitVersion = -1;
    private double _digitBaseline;
    private Rect _dial;
    private Point _unitOrigin;
    private StreamGeometry? _track;
    private StreamGeometry? _arc;
    private double _arcSweep = -1;
    private ChartsBAutomationPeerGauge? _peer;

    /// <summary>Creates a gauge.</summary>
    public Gauge()
    {
        _observedThresholds = new ObservedItems(_ => OnThresholdsChanged());
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _reveal = new ChartsBReveal(this, 0.5, _ => Sync());
        _sweep = new MotionValue<double>(this, 0, MotionVectors.Double);
        _sweep.Changed += _ => InvalidateVisual();
        _count = new MotionValue<double>(this, 0, MotionVectors.Double);
        _count.Changed += OnCountChanged;
        _ringColor = new MotionValue<Color>(this, Colors.Transparent, MotionVectors.Color);
        _ringColor.Changed += _ => InvalidateVisual();
        _textColor = new MotionValue<Color>(this, Colors.Transparent, MotionVectors.Color);
        _textColor.Changed += color => _status!.SetValue(TextElement.ForegroundProperty, new ImmutableSolidColorBrush(color));

        _digits = new RollingDigits { IsArmed = true, UseGrouping = false };
        ArcTokens.Bind(_digits, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_digits, RollingDigits.FontSizeProperty, ArcDesignTokenKind.Type3XL);
        ArcTokens.Bind(_digits, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_digits, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);

        _status = Swap(ContentSwapMode.BlurSwap, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.WeightMedium, null);
        _status.MinWidth = 100;
        _label = Swap(ContentSwapMode.BlurSwap, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.WeightMedium, ArcDesignTokenKind.Foreground);
        _detail = Swap(ContentSwapMode.BlurSwap, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.WeightRegular, ArcDesignTokenKind.TextMuted);
        foreach (var child in new Control[] { _digits, _status, _label, _detail })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _thresholds = new AvaloniaList<GaugeThreshold>();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    static Gauge()
    {
        AffectsMeasure<Gauge>(DialMaxWidthProperty, CaptionGapProperty);
        AffectsRender<Gauge>(RingThicknessProperty);
    }

    /// <summary>The current value, between <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Lower end of the range (default 0).</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Upper end of the range (default 100).</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>What is measured, such as "Disk usage"; shown under the ring and used as the accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Secondary line under the label, such as "360 of 500 GB".</summary>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Ring tone when no threshold band applies (default <see cref="GaugeTone.Accent"/>).</summary>
    public GaugeTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Named bands; the highest one the value reaches sets the tone and the state label.</summary>
    public IList<GaugeThreshold> Thresholds
    {
        get => _thresholds;
        set
        {
            _thresholds = value ?? new AvaloniaList<GaugeThreshold>();
            WatchThresholds();
            OnThresholdsChanged();
        }
    }

    /// <summary>The value as a whole percentage of the range (what the ring and the number show once settled).</summary>
    public int Percent => (int)Math.Round(Fraction * 100);

    /// <summary>The band the value is in (null without thresholds or below the first band).</summary>
    public GaugeThreshold? CurrentThreshold => BandAt(Value);

    internal double DialMaxWidth
    {
        get => GetValue(DialMaxWidthProperty);
        set => SetValue(DialMaxWidthProperty, value);
    }

    internal double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    internal double CaptionGap
    {
        get => GetValue(CaptionGapProperty);
        set => SetValue(CaptionGapProperty, value);
    }

    /// <summary>Current drawn sweep (0..1), for tests.</summary>
    internal double DrawnSweep => _sweep.Value;

    /// <summary>The state label on screen (empty until the first fill lands).</summary>
    internal string StatusText => _status.Content as string ?? string.Empty;

    internal string DigitsText => _digits.DisplayText;

    private double SafeMax => Maximum > Minimum ? Maximum : Minimum + 1;

    private double Fraction => ChartsBMath.Clamp((Value - Minimum) / (SafeMax - Minimum), 0, 1);

    /// <summary>Accessible value text, such as "72%, Healthy".</summary>
    internal string ValueText => CurrentThreshold is { } band ? $"{Percent}%, {band.Label}" : $"{Percent}%";

    private ArcContentSwap Swap(ContentSwapMode mode, ArcDesignTokenKind size, ArcDesignTokenKind weight, ArcDesignTokenKind? color)
    {
        var swap = new ArcContentSwap { Mode = mode, HorizontalContentAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(swap, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(swap, TextElement.FontSizeProperty, size);
        ArcTokens.Bind(swap, TextElement.FontWeightProperty, weight);
        if (color is { } kind)
        {
            ArcTokens.Bind(swap, TextElement.ForegroundProperty, kind);
        }
        return swap;
    }

    private GaugeThreshold? BandAt(double at)
    {
        GaugeThreshold? found = null;
        foreach (var band in _bands)
        {
            if (at >= band.From - 1e-6)
            {
                found = band;
            }
        }
        return found;
    }

    /// <summary>Listens to the current bands list while shown (and to nothing otherwise).</summary>
    private void WatchThresholds()
    {
        _observedThresholds.Observe(_watching ? _thresholds : null);
    }

    private void OnThresholdsChanged()
    {
        _bands = _thresholds.Where(t => t is not null).OrderBy(t => t.From).ToArray();
        _status.IsVisible = _bands.Length > 0;
        if (_filled)
        {
            _live = BandAt(Minimum + _count.Value / 100 * (SafeMax - Minimum));
        }
        UpdateState(animate: false);
        UpdateAccessibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            Sync();
            UpdateAccessibility();
        }
        else if (change.Property == ToneProperty)
        {
            UpdateState(animate: true);
        }
        else if (change.Property == LabelProperty)
        {
            _label.Content = Label;
            _label.IsVisible = !string.IsNullOrEmpty(Label);
            UpdateAccessibility();
        }
        else if (change.Property == DetailProperty)
        {
            _detail.Content = Detail;
            _detail.IsVisible = !string.IsNullOrEmpty(Detail);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _label.IsVisible = !string.IsNullOrEmpty(Label);
        _detail.IsVisible = !string.IsNullOrEmpty(Detail);
        _watching = true;
        WatchThresholds();
        // Reads the bands again (they may have changed while the gauge was away) and shows the state without animating.
        OnThresholdsChanged();
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchThresholds();
        _reveal.Detach();
    }

    /// <summary>Moves ring and number toward the current value (the first fill is slower and never overshoots).</summary>
    private void Sync()
    {
        var percent = Percent;
        var direction = percent < _shownPercent ? -1 : 1;
        _shownPercent = percent;
        _status.Direction = direction;
        _detail.Direction = direction;
        if (!_reveal.IsRevealed)
        {
            _digits.Value = percent;
            UpdateState(animate: false);
            return;
        }
        var fraction = Fraction;
        _digits.Value = percent;
        _digits.IsArmed = false;
        if (!ArcMotion.IsFull(this) || !IsLoaded)
        {
            _sweep.Jump(fraction);
            _count.Jump(fraction * 100);
            _filled = true;
            _live = BandAt(Value);
            UpdateState(animate: false);
            return;
        }
        var reveal = SpringSpec.FromVisual(ArcMotionPresets.Considered.TotalSeconds * 1.6, 0);
        _ = _sweep.AnimateTo(fraction, _filled ? MotionSpec.Spring(ArcSpring.Morph) : MotionSpec.Spring(reveal));
        _ = _count.AnimateTo(fraction * 100, _filled ? MotionSpec.Spring(ArcSpring.Smooth) : MotionSpec.Spring(reveal));
        UpdateState(animate: true);
    }

    // The state arrives as the first fill lands; after that it follows the number on screen, so color and label
    // change exactly when the count crosses a threshold.
    private void OnCountChanged(double current)
    {
        var next = BandAt(Minimum + current / 100 * (SafeMax - Minimum));
        if (_filled)
        {
            if (!ReferenceEquals(next, _live))
            {
                _live = next;
                UpdateState(animate: true);
            }
        }
        else if (_reveal.IsRevealed && Math.Abs(current - Fraction * 100) < 1)
        {
            _live = next;
            _filled = true;
            UpdateState(animate: true);
        }
    }

    private void UpdateState(bool animate)
    {
        var shown = !ArcMotion.IsFull(this) || !_filled ? BandAt(Value) : _live;
        var tone = shown?.Tone ?? Tone;
        var ring = ToneColor(tone);
        var text = tone == GaugeTone.Accent ? _palette.AccentStrong : ring;
        if (animate && _toneInitialized && ArcMotion.IsFull(this) && IsLoaded)
        {
            var spec = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);
            _ = _ringColor.AnimateTo(ring, spec);
            _ = _textColor.AnimateTo(text, spec);
        }
        else
        {
            _ringColor.Jump(ring);
            _textColor.Jump(text);
        }
        _toneInitialized = true;
        _status.Content = _bands.Length == 0 ? null : _filled ? shown?.Label ?? string.Empty : string.Empty;
    }

    private Color ToneColor(GaugeTone tone) => tone switch
    {
        GaugeTone.Success => _palette.Success,
        GaugeTone.Warning => _palette.Warning,
        GaugeTone.Danger => _palette.Danger,
        _ => _palette.Accent
    };

    private void OnPaletteChanged()
    {
        if (_toneInitialized)
        {
            UpdateState(animate: false);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetHelpText(this, ValueText);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeerGauge(this);

    private FormattedText Unit()
    {
        if (_unit is null || _unitVersion != _palette.Version)
        {
            _unitVersion = _palette.Version;
            var size = _digits.FontSize > 0 ? _digits.FontSize : 36;
            _unit = ChartsBText.Make("%", _palette, size * 0.5, _palette.TextSecondary);
            var zero = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(_digits.FontFamily, FontStyle.Normal, _digits.FontWeight), size, null);
            _digitBaseline = zero.Baseline;
        }
        return _unit;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = DialMaxWidth;
        var width = double.IsInfinity(availableSize.Width) ? maxWidth : Math.Min(availableSize.Width, maxWidth);
        var dialHeight = width * 0.9;
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _digits.Measure(unbounded);
        _status.Measure(unbounded);
        var captionWidth = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        _label.Measure(new Size(captionWidth, double.PositiveInfinity));
        _detail.Measure(new Size(captionWidth, double.PositiveInfinity));
        var caption = Caption(out var captionMax);
        var total = Math.Max(width, captionMax);
        return new Size(double.IsInfinity(availableSize.Width) ? total : Math.Min(total, availableSize.Width),
            dialHeight + (caption > 0 ? CaptionGap + caption : 0));
    }

    private double Caption(out double width)
    {
        var height = 0.0;
        width = 0;
        if (_label.IsVisible)
        {
            height += _label.DesiredSize.Height;
            width = Math.Max(width, _label.DesiredSize.Width);
        }
        if (_detail.IsVisible)
        {
            height += (height > 0 ? 3 : 0) + _detail.DesiredSize.Height;
            width = Math.Max(width, _detail.DesiredSize.Width);
        }
        return height;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Min(finalSize.Width, DialMaxWidth);
        _dial = new Rect((finalSize.Width - width) / 2, 0, width, width * 0.9);
        var unit = Unit();
        var digits = _digits.DesiredSize;
        var numberWidth = digits.Width + 1 + unit.WidthIncludingTrailingWhitespace;
        var statusHeight = _status.IsVisible ? Math.Max(_status.DesiredSize.Height, (_status.GetValue(TextElement.FontSizeProperty)) * 1.4) : 0;
        var blockHeight = digits.Height + (statusHeight > 0 ? 4 + statusHeight : 0);
        // The readout sits on the ring's true center: the middle of a square as wide as the ring.
        var top = _dial.Y + width / 2 - blockHeight / 2;
        var left = _dial.X + (width - numberWidth) / 2;
        _digits.Arrange(new Rect(left, top, digits.Width, digits.Height));
        _unitOrigin = new Point(left + digits.Width + 1, top + _digitBaseline - unit.Baseline);
        if (_status.IsVisible)
        {
            var statusWidth = Math.Max(100, _status.DesiredSize.Width);
            _status.Arrange(new Rect(_dial.X + (width - statusWidth) / 2, top + digits.Height + 4, statusWidth, statusHeight));
        }
        var y = _dial.Bottom + CaptionGap;
        if (_label.IsVisible)
        {
            var size = _label.DesiredSize;
            _label.Arrange(new Rect((finalSize.Width - size.Width) / 2, y, size.Width, size.Height));
            y += size.Height + 3;
        }
        if (_detail.IsVisible)
        {
            var size = _detail.DesiredSize;
            _detail.Arrange(new Rect((finalSize.Width - size.Width) / 2, y, size.Width, size.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var scale = _dial.Width / 100;
        if (scale <= 0)
        {
            return;
        }
        using var fade = context.PushOpacity(_reveal.Fade);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(_dial.X, _dial.Y)))
        {
            var ring = _ringColor.Value;
            var thickness = RingThickness;
            _track ??= ArcGeometry(1);
            context.DrawGeometry(null, _palette.Pen(ChartsBMath.Alpha(ring, 0.13), thickness, PenLineCap.Round), _track);
            var sweep = _sweep.Value;
            // The cap fades in over the first sliver instead of popping in as a dot.
            var opacity = ChartsBMath.Clamp(sweep / 0.02, 0, 1);
            if (opacity > 0.001)
            {
                if (_arc is null || _arcSweep != sweep)
                {
                    _arcSweep = sweep;
                    _arc = ArcGeometry(sweep);
                }
                using (context.PushOpacity(opacity))
                {
                    context.DrawGeometry(null, _palette.Pen(ring, thickness, PenLineCap.Round), _arc);
                }
            }
        }
        context.DrawText(Unit(), _unitOrigin);
    }

    /// <summary>The ring path from its lower left end, clockwise, in the 100 × 90 view box (radius 42 around 50, 50).</summary>
    private static StreamGeometry ArcGeometry(double sweep)
    {
        var end = Start + Span * ChartsBMath.Clamp(sweep, 0, 1);
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(At(Start), false);
        if (end - Start > 0.01)
        {
            ctx.ArcTo(At(end), new Size(Radius, Radius), 0, end - Start > 180, SweepDirection.Clockwise);
        }
        else
        {
            ctx.LineTo(At(Start + 0.01));
        }
        ctx.EndFigure(false);
        return geometry;
    }

    private static Point At(double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(50 + Radius * Math.Sin(radians), 50 - Radius * Math.Cos(radians));
    }

    /// <summary>Meter semantics: Avalonia has no meter role, so this is a read-only range value (progress bar role).</summary>
    private sealed class ChartsBAutomationPeerGauge : ControlAutomationPeer, IRangeValueProvider
    {
        private string? _lastHelp;

        public ChartsBAutomationPeerGauge(Gauge owner) : base(owner)
        {
        }

        private Gauge Gauge => (Gauge)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetLocalizedControlTypeCore() => "meter";

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(Owner);
            return string.IsNullOrEmpty(name) ? Gauge.Label : name;
        }

        protected override string? GetHelpTextCore() => Gauge.ValueText;

        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => [];

        public bool IsReadOnly => true;
        public double Minimum => Gauge.Minimum;
        public double Maximum => Gauge.SafeMax;
        public double Value => Math.Clamp(Gauge.Value, Gauge.Minimum, Gauge.SafeMax);
        public double SmallChange => 1;
        public double LargeChange => 10;

        public void SetValue(double value)
        {
        }

        public void Announce()
        {
            var help = Gauge.ValueText;
            if (help == _lastHelp)
            {
                return;
            }
            var old = _lastHelp;
            _lastHelp = help;
            RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, old, help);
        }
    }
}

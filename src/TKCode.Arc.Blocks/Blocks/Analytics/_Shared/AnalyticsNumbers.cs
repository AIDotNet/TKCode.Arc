using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A number that counts to a new value as text: the value tweens and every frame shows it formatted (Metrics dashboard
/// KPIs and ranked list values: 480ms (.22, 1, .36, 1), all values together, rounded to the display precision each frame;
/// Connected scatter readouts). The first value shows at once (no count from zero); reduced motion and Off replace.
/// Digits are tabular so the width does not jitter; the control measures to the widest of the start and end texts
/// while counting. Compare <see cref="AnalyticsRollingNumber"/> (per-digit wheels).
/// </summary>
internal sealed class AnalyticsCountUp : Control
{
    /// <summary>The value.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, double>(nameof(Value));

    /// <summary>Format of the shown text (default: grouped integer). <see cref="Formatter"/> wins when set.</summary>
    public static readonly StyledProperty<AnalyticsNumberFormat> FormatProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, AnalyticsNumberFormat>(nameof(Format), AnalyticsNumberFormat.Integer);

    /// <summary>Custom formatter (wins over <see cref="Format"/>).</summary>
    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, Func<double, string>?>(nameof(Formatter));

    /// <summary>Count duration (480ms).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, TimeSpan>(nameof(Duration), ArcMotionPresets.Considered);

    /// <summary>Horizontal placement of the text when the control is wider.</summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, TextAlignment>(nameof(TextAlignment));

    /// <summary>Fixed line height (0 uses the font's).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AnalyticsCountUp, double>(nameof(LineHeight));

    /// <summary>Font family (inherited).</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AnalyticsCountUp>();

    /// <summary>Font size (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AnalyticsCountUp>();

    /// <summary>Font weight (inherited).</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AnalyticsCountUp>();

    /// <summary>Text brush (inherited).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AnalyticsCountUp>();

    private readonly AnalyticsTextCache _cache = new(256);
    private readonly LiveLoop _loop;
    private AnalyticsTween _shown = new(0);
    private bool _hasValue;
    private string _text = string.Empty;
    private double _holdWidth;

    static AnalyticsCountUp()
    {
        AffectsMeasure<AnalyticsCountUp>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty, FormatProperty, FormatterProperty);
        AffectsRender<AnalyticsCountUp>(ForegroundProperty, TextAlignmentProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AnalyticsCountUp>(false);
    }

    /// <summary>Creates the control.</summary>
    public AnalyticsCountUp()
    {
        _loop = new LiveLoop(this, Advance);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public AnalyticsNumberFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <inheritdoc cref="FormatterProperty"/>
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="TextAlignmentProperty"/>
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc cref="FontWeightProperty"/>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The text on screen now (mid-count while counting).</summary>
    public string DisplayText => _text;

    /// <summary>The value on screen now.</summary>
    public double DisplayValue => _shown.Value;

    /// <summary>The final text (screen readers read it, never the counting text).</summary>
    public string FinalText => FormatValue(Value);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            OnValueChanged(Value);
        }
        else if (change.Property == FormatProperty || change.Property == FormatterProperty)
        {
            _text = FormatValue(_shown.Value);
            AutomationProperties.SetName(this, FinalText);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontWeightProperty)
        {
            _cache.Clear();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_hasValue)
        {
            OnValueChanged(Value);
        }
    }

    private void OnValueChanged(double value)
    {
        AutomationProperties.SetName(this, FormatValue(value));
        var animate = _hasValue && IsLoaded && ArcMotion.IsFull(this) && Duration > TimeSpan.Zero && double.IsFinite(_shown.Value);
        _hasValue = true;
        if (!animate)
        {
            _shown.Jump(value);
            _loop.Stop();
            _holdWidth = 0;
            SetText(FormatValue(value));
            return;
        }
        // Hold the wider of the start and end texts so the row does not reflow while counting.
        _holdWidth = Math.Max(TextWidth(FormatValue(_shown.Value)), TextWidth(FormatValue(value)));
        _shown.Start(value, Duration, AnalyticsEasings.Standard);
        _loop.Start();
        InvalidateMeasure();
    }

    private bool Advance(double dt)
    {
        var running = _shown.Advance(dt);
        SetText(FormatValue(_shown.Value));
        if (!running)
        {
            _holdWidth = 0;
            InvalidateMeasure();
        }
        return running;
    }

    private void SetText(string text)
    {
        if (text == _text)
        {
            return;
        }
        var widthChanged = _holdWidth <= 0 && Math.Abs(TextWidth(text) - TextWidth(_text)) > 0.01;
        _text = text;
        if (widthChanged)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private string FormatValue(double value) => Formatter?.Invoke(value) ?? Format.Format(value);

    private AnalyticsText Layout(string text) => _cache.Get(text.Length == 0 ? " " : text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, true);

    private double TextWidth(string text) => text.Length == 0 ? 0 : Layout(text).WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = Layout(_text);
        var width = Math.Max(_holdWidth, TextWidth(_text));
        return new Size(Math.Ceiling(width), LineHeight > 0 ? LineHeight : layout.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (_text.Length == 0)
        {
            return;
        }
        var layout = Layout(_text);
        var width = layout.WidthIncludingTrailingWhitespace;
        var x = TextAlignment switch
        {
            TextAlignment.Right or TextAlignment.End => Bounds.Width - width,
            TextAlignment.Center => (Bounds.Width - width) / 2,
            _ => 0
        };
        var lineBox = LineHeight > 0 ? LineHeight : layout.Height;
        layout.Draw(context, Foreground ?? Brushes.Black, new Point(x, (lineBox - layout.Height) / 2));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NumberPeer(this);

    private sealed class NumberPeer(AnalyticsCountUp owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => owner.FinalText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// A <see cref="RollingDigits"/> odometer that takes a signed amount and an <see cref="AnalyticsNumberFormat"/>: the
/// sign (true minus) and currency symbol become the static prefix, the unsigned scaled number rolls, the unit ("k",
/// "%", " pts", "ms") is the static suffix. Every digit column rolls through the digits in between, in the direction the
/// whole value moved, on one Smooth spring (700ms, k ≈ 155–164: KPI drilldown, MRR waterfall, Cohort retention, Usage
/// forecast, Uptime, Journey flow, Revenue explorer, Metric explorer deltas); glyphs fade linearly with distance
/// (<see cref="RollingDigitsFalloff.Linear"/>: Arc's <c>1 − |offset|</c>; Journey flow's clipped strips use
/// <see cref="RollingDigitsFalloff.None"/>). New columns grow in, removed ones shrink away. Not reproduced: Arc's 2px
/// blur on rolling glyphs (per-glyph blur costs too much; the fade carries the effect). The first value shows at once;
/// reduced motion and Off replace. Screen readers read the full formatted text.
/// </summary>
internal sealed class AnalyticsRollingNumber : RollingDigits
{
    /// <summary>The signed amount.</summary>
    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<AnalyticsRollingNumber, double>(nameof(Amount));

    /// <summary>The format (default: grouped integer).</summary>
    public static readonly StyledProperty<AnalyticsNumberFormat> FormatProperty =
        AvaloniaProperty.Register<AnalyticsRollingNumber, AnalyticsNumberFormat>(nameof(Format), AnalyticsNumberFormat.Integer);

    static AnalyticsRollingNumber()
    {
        FalloffProperty.OverrideDefaultValue<AnalyticsRollingNumber>(RollingDigitsFalloff.Linear);
        WheelSpringProperty.OverrideDefaultValue<AnalyticsRollingNumber>(ArcSpring.Smooth);
    }

    /// <summary>Creates the control.</summary>
    public AnalyticsRollingNumber()
    {
        Apply();
    }

    /// <inheritdoc cref="AmountProperty"/>
    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public AnalyticsNumberFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The full text (true minus, symbol, unit): what screen readers read.</summary>
    public string FullText => Format.Format(Amount);

    protected override Type StyleKeyOverride => typeof(RollingDigits);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AmountProperty || change.Property == FormatProperty)
        {
            Apply();
        }
    }

    private void Apply()
    {
        var (prefix, value, decimals, suffix) = Format.Parts(Amount);
        // Prefix and suffix first: they are static glyphs, and Value last rebuilds the wheels once with every part known.
        SetCurrentValue(PrefixProperty, prefix);
        SetCurrentValue(SuffixProperty, suffix);
        SetCurrentValue(DecimalsProperty, decimals);
        SetCurrentValue(ValueProperty, value);
        AutomationProperties.SetName(this, FullText);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NumberPeer(this);

    private sealed class NumberPeer(AnalyticsRollingNumber owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => owner.FullText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

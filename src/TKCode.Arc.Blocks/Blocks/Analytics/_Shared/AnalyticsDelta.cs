using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace TKCode.Arc;

/// <summary>Which way a change went (the arrow), independent of whether that is good (the tone).</summary>
internal enum AnalyticsDirection
{
    /// <summary>No change at the display precision: a level arrow (→).</summary>
    Flat,

    /// <summary>Up: ↗.</summary>
    Up,

    /// <summary>Down: ↘.</summary>
    Down
}

/// <summary>
/// The change line under a KPI value: arrow + change text (+ optional muted context such as "since Jul" or "30 days"),
/// tinted by tone (good = Success, bad = Danger, flat = TextMuted; colour transition 160ms, or 240ms with
/// <see cref="IsToneSlow"/>). Direction comes from the sign, tone from direction × lower-is-better, so a falling churn
/// rate is ↘ and green. The arrow is Lucide <c>arrow-up-right</c>; down is that arrow turned 90° (at once, or on Arc's
/// 580ms CSS spring with <see cref="IsArrowRotating"/>: Revenue explorer, Metric explorer); flat is <c>arrow-right</c>.
/// The text swaps per <see cref="Swap"/> (instant by default: Metrics dashboard; <see cref="AnalyticsSwapStyle.Rise"/>
/// for MRR waterfall / Cohort retention / Revenue explorer). Screen readers hear the direction in words
/// (<c>"up 11.7%"</c>), not only the colour. Sizes default to Metrics dashboard / KPI drilldown (12px 500 text, 13px
/// icon, stroke 2, gap 2); <see cref="Show"/> fills everything from a number.
/// </summary>
[TemplatePart(ArrowPart, typeof(ArcGlyph))]
[PseudoClasses(GoodPseudoClass, BadPseudoClass, FlatPseudoClass, SlowPseudoClass, RotatesPseudoClass)]
internal sealed class AnalyticsDelta : TemplatedControl
{
    /// <summary>Template part: the arrow.</summary>
    public const string ArrowPart = "PART_Arrow";

    /// <summary>Set for a good change.</summary>
    public const string GoodPseudoClass = ":good";

    /// <summary>Set for a bad change.</summary>
    public const string BadPseudoClass = ":bad";

    /// <summary>Set for no change.</summary>
    public const string FlatPseudoClass = ":flat";

    /// <summary>Set with <see cref="IsToneSlow"/>.</summary>
    public const string SlowPseudoClass = ":slow-tone";

    /// <summary>Set with <see cref="IsArrowRotating"/>.</summary>
    public const string RotatesPseudoClass = ":rotates";

    /// <summary>The change text ("11.7%", "0.1 pts", "+$55.8k").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AnalyticsDelta, string?>(nameof(Text));

    /// <summary>Muted text after the change ("since Jul", "30 days"); hidden when empty.</summary>
    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<AnalyticsDelta, string?>(nameof(Context));

    /// <summary>Arrow direction.</summary>
    public static readonly StyledProperty<AnalyticsDirection> DirectionProperty =
        AvaloniaProperty.Register<AnalyticsDelta, AnalyticsDirection>(nameof(Direction));

    /// <summary>Colour.</summary>
    public static readonly StyledProperty<AnalyticsTone> ToneProperty =
        AvaloniaProperty.Register<AnalyticsDelta, AnalyticsTone>(nameof(Tone));

    /// <summary>Arrow size (13; Revenue explorer / Cohort / Metric explorer 14; MRR waterfall 16).</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<AnalyticsDelta, double>(nameof(IconSize), 13);

    /// <summary>Arrow stroke in Lucide units (2; 1.75 in Revenue explorer, Cohort, MRR waterfall, Metric explorer).</summary>
    public static readonly StyledProperty<double> IconStrokeProperty =
        AvaloniaProperty.Register<AnalyticsDelta, double>(nameof(IconStroke), 2);

    /// <summary>Gap between arrow and text (2; Revenue explorer 4, MRR waterfall 6).</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AnalyticsDelta, double>(nameof(Spacing), 2);

    /// <summary>Gap before the context text (4; Metric explorer and MRR waterfall 6).</summary>
    public static readonly StyledProperty<double> ContextSpacingProperty =
        AvaloniaProperty.Register<AnalyticsDelta, double>(nameof(ContextSpacing), 4);

    /// <summary>How the text animates when it changes (default <see cref="AnalyticsSwapStyle.Instant"/>).</summary>
    public static readonly StyledProperty<AnalyticsSwapStyle> SwapProperty =
        AvaloniaProperty.Register<AnalyticsDelta, AnalyticsSwapStyle>(nameof(Swap), AnalyticsSwapStyle.Instant);

    /// <summary>Weight of the change text (Medium; Metrics dashboard Normal). The context text is always Normal.</summary>
    public static readonly StyledProperty<FontWeight> TextWeightProperty =
        AvaloniaProperty.Register<AnalyticsDelta, FontWeight>(nameof(TextWeight), FontWeight.Medium);

    /// <summary>Colour transition of 240ms instead of 160ms (Revenue explorer, Cohort retention).</summary>
    public static readonly StyledProperty<bool> IsToneSlowProperty =
        AvaloniaProperty.Register<AnalyticsDelta, bool>(nameof(IsToneSlow));

    /// <summary>The arrow turns between up and down on the 580ms CSS spring instead of switching.</summary>
    public static readonly StyledProperty<bool> IsArrowRotatingProperty =
        AvaloniaProperty.Register<AnalyticsDelta, bool>(nameof(IsArrowRotating));

    /// <summary>Spoken direction words: up, down, unchanged (screen-reader text "up 11.7% since Jul").</summary>
    public static readonly StyledProperty<string> UpTextProperty =
        AvaloniaProperty.Register<AnalyticsDelta, string>(nameof(UpText), "up");

    /// <summary>Spoken word for a fall.</summary>
    public static readonly StyledProperty<string> DownTextProperty =
        AvaloniaProperty.Register<AnalyticsDelta, string>(nameof(DownText), "down");

    /// <summary>Spoken word for no change.</summary>
    public static readonly StyledProperty<string> FlatTextProperty =
        AvaloniaProperty.Register<AnalyticsDelta, string>(nameof(FlatText), "unchanged");

    private static readonly ITransform s_up = TransformOperations.Parse("rotate(0deg)");
    private static readonly ITransform s_down = TransformOperations.Parse("rotate(90deg)");
    private ArcGlyph? _arrow;

    static AnalyticsDelta()
    {
        FocusableProperty.OverrideDefaultValue<AnalyticsDelta>(false);
    }

    /// <summary>Creates the control.</summary>
    public AnalyticsDelta()
    {
        UpdateState();
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ContextProperty"/>
    public string? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }

    /// <inheritdoc cref="DirectionProperty"/>
    public AnalyticsDirection Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public AnalyticsTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="IconSizeProperty"/>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <inheritdoc cref="IconStrokeProperty"/>
    public double IconStroke { get => GetValue(IconStrokeProperty); set => SetValue(IconStrokeProperty, value); }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <inheritdoc cref="ContextSpacingProperty"/>
    public double ContextSpacing { get => GetValue(ContextSpacingProperty); set => SetValue(ContextSpacingProperty, value); }

    /// <inheritdoc cref="SwapProperty"/>
    public AnalyticsSwapStyle Swap { get => GetValue(SwapProperty); set => SetValue(SwapProperty, value); }

    /// <inheritdoc cref="TextWeightProperty"/>
    public FontWeight TextWeight { get => GetValue(TextWeightProperty); set => SetValue(TextWeightProperty, value); }

    /// <inheritdoc cref="IsToneSlowProperty"/>
    public bool IsToneSlow { get => GetValue(IsToneSlowProperty); set => SetValue(IsToneSlowProperty, value); }

    /// <inheritdoc cref="IsArrowRotatingProperty"/>
    public bool IsArrowRotating { get => GetValue(IsArrowRotatingProperty); set => SetValue(IsArrowRotatingProperty, value); }

    /// <inheritdoc cref="UpTextProperty"/>
    public string UpText { get => GetValue(UpTextProperty); set => SetValue(UpTextProperty, value); }

    /// <inheritdoc cref="DownTextProperty"/>
    public string DownText { get => GetValue(DownTextProperty); set => SetValue(DownTextProperty, value); }

    /// <inheritdoc cref="FlatTextProperty"/>
    public string FlatText { get => GetValue(FlatTextProperty); set => SetValue(FlatTextProperty, value); }

    /// <summary>The text screen readers hear ("up 11.7% since Jul").</summary>
    public string SpokenText
    {
        get
        {
            var word = Direction switch { AnalyticsDirection.Up => UpText, AnalyticsDirection.Down => DownText, _ => FlatText };
            var text = Direction == AnalyticsDirection.Flat && string.IsNullOrEmpty(Text) ? word : $"{word} {Text}";
            return string.IsNullOrEmpty(Context) ? text : $"{text} {Context}";
        }
    }

    /// <summary>
    /// Fills text, direction and tone from a displayed change: <paramref name="change"/> in the format's units (percent
    /// for <see cref="AnalyticsNumberKind.Percent"/>, points for <see cref="AnalyticsNumberKind.Points"/>, currency …).
    /// The text is unsigned unless the format is <c>Signed</c> (Metrics dashboard shows "11.7%" with the arrow; KPI
    /// drilldown signs with a true minus). A change that rounds to zero at the format's decimals is flat.
    /// </summary>
    public void Show(double change, AnalyticsNumberFormat format, bool lowerIsBetter = false)
    {
        var decimals = format.Kind is AnalyticsNumberKind.Compact or AnalyticsNumberKind.CompactFixed ? 0 : format.Decimals;
        var tone = AnalyticsFormat.Tone(change, lowerIsBetter, decimals);
        Direction = tone == AnalyticsTone.Flat ? AnalyticsDirection.Flat : change > 0 ? AnalyticsDirection.Up : AnalyticsDirection.Down;
        Tone = tone;
        Text = format.Format(format.Signed ? change : Math.Abs(change));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _arrow = e.NameScope.Find<ArcGlyph>(ArrowPart);
        UpdateArrow();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DirectionProperty || change.Property == ToneProperty || change.Property == IsToneSlowProperty ||
            change.Property == IsArrowRotatingProperty || change.Property == TextProperty || change.Property == ContextProperty ||
            change.Property == UpTextProperty || change.Property == DownTextProperty || change.Property == FlatTextProperty)
        {
            UpdateState();
            UpdateArrow();
        }
    }

    private void UpdateState()
    {
        PseudoClasses.Set(GoodPseudoClass, Tone == AnalyticsTone.Good);
        PseudoClasses.Set(BadPseudoClass, Tone == AnalyticsTone.Bad);
        PseudoClasses.Set(FlatPseudoClass, Tone == AnalyticsTone.Flat);
        PseudoClasses.Set(SlowPseudoClass, IsToneSlow);
        PseudoClasses.Set(RotatesPseudoClass, IsArrowRotating);
        AutomationProperties.SetName(this, SpokenText);
    }

    private void UpdateArrow()
    {
        if (_arrow is null)
        {
            return;
        }
        if (Direction == AnalyticsDirection.Flat)
        {
            _arrow.Data = ArcIcons.ArrowRight;
            _arrow.RenderTransform = s_up;
            return;
        }
        _arrow.Data = ArcIcons.ArrowUpRight;
        _arrow.RenderTransform = Direction == AnalyticsDirection.Down ? s_down : s_up;
    }
}

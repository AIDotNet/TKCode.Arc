using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One likely cause in an <see cref="AiInsightCard"/>: a toggle row with the cause's title, its evidence and how much of
/// the change it explains, as a bar and a percentage. Checking it selects the cause (the card keeps one at most).
/// </summary>
internal sealed class AiInsightCardCauseRow : ToggleButton
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiInsightCardCauseRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AiInsightCardCauseRow, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> ShareTextProperty =
        AvaloniaProperty.Register<AiInsightCardCauseRow, string?>(nameof(ShareText));

    public static readonly StyledProperty<double> ShareProperty =
        AvaloniaProperty.Register<AiInsightCardCauseRow, double>(nameof(Share));

    private LazyFocusRing? _focusRing;

    public AiInsightCardCauseRow(AiInsightCause cause)
    {
        Cause = cause;
    }

    public AiInsightCause Cause { get; }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? ShareText { get => GetValue(ShareTextProperty); set => SetValue(ShareTextProperty, value); }

    public double Share { get => GetValue(ShareProperty); set => SetValue(ShareProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiInsightCardCauseRow);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, AiInsightCardTokens.Key(AiInsightCardTokenKind.RowFocusMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>A cause's share as a thin bar: a muted track with the filled part in the theme's fill brush.</summary>
internal sealed class AiInsightCardShareBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AiInsightCardShareBar, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AiInsightCardShareBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<AiInsightCardShareBar, IBrush?>(nameof(FillBrush));

    static AiInsightCardShareBar()
    {
        AffectsRender<AiInsightCardShareBar>(ValueProperty, TrackBrushProperty, FillBrushProperty);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        // A capsule that the corner styles can square off.
        var radius = Math.Min(size.Height / 2,
            ArcTokens.GetCornerRadius(this, ArcDesignTokenKind.RadiusPill, new CornerRadius(9999)).TopLeft);
        context.DrawRectangle(TrackBrush, null, new Rect(size), radius, radius);
        var fill = Math.Clamp(Value, 0, 1) * size.Width;
        if (fill > 0)
        {
            context.DrawRectangle(FillBrush, null, new Rect(0, 0, Math.Max(fill, size.Height), size.Height), radius, radius);
        }
    }
}

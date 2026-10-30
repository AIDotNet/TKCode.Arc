using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The thin progress line under the canvas: a hairline track and a fill drawn to <see cref="Fraction"/>, which a frame sets
/// without allocating. It reports itself as a read-only progress bar from 0 to 100; <see cref="Percent"/> (and so the
/// reported value) changes only when the rounded value does.
/// </summary>
internal sealed class SketchToImageProgress : Control
{
    /// <summary>The fill color.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<SketchToImageProgress, IBrush?>(nameof(Foreground));

    /// <summary>The track color.</summary>
    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<SketchToImageProgress, IBrush?>(nameof(Track));

    private double _fraction;

    static SketchToImageProgress()
    {
        AffectsRender<SketchToImageProgress>(ForegroundProperty, TrackProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SketchToImageProgress>(false);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <inheritdoc cref="TrackProperty"/>
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <summary>The share of the line that is filled (0 to 1).</summary>
    public double Fraction
    {
        get => _fraction;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (value == _fraction)
            {
                return;
            }
            _fraction = value;
            InvalidateVisual();
        }
    }

    /// <summary>The rounded percent the line reports.</summary>
    public int Percent { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer() => new SketchToImageProgressPeer(this);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        var radius = h / 2;
        if (Track is { } track)
        {
            context.DrawRectangle(track, null, new RoundedRect(new Rect(0, 0, w, h), radius));
        }
        if (Foreground is { } fill && _fraction > 0)
        {
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, 0, Math.Max(h, w * _fraction), h), radius));
        }
    }
}

/// <summary>Reports <see cref="SketchToImageProgress"/> as a read-only progress bar from 0 to 100.</summary>
internal sealed class SketchToImageProgressPeer(SketchToImageProgress owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private SketchToImageProgress Line => (SketchToImageProgress)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsReadOnly => true;

    public double Minimum => 0;

    public double Maximum => 100;

    public double Value => Line.Percent;

    public double SmallChange => 1;

    public double LargeChange => 10;

    public void SetValue(double value) => throw new InvalidOperationException("The progress line is read only.");
}

/// <summary>One stage label: the stage's icon and its word, which shimmers while the stage is in progress.</summary>
internal sealed class SketchToImageStageLabel : TemplatedControl
{
    /// <summary>The 24 × 24 stroked icon (pen, brush, sparkles, check).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<SketchToImageStageLabel, Geometry?>(nameof(Glyph));

    /// <summary>The icon's color (the tone).</summary>
    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<SketchToImageStageLabel, IBrush?>(nameof(GlyphBrush));

    /// <summary>The stage's word.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SketchToImageStageLabel, string?>(nameof(Text));

    /// <summary>Whether the word shimmers (the stage is in progress).</summary>
    public static readonly StyledProperty<bool> IsShimmeringProperty =
        AvaloniaProperty.Register<SketchToImageStageLabel, bool>(nameof(IsShimmering));

    public SketchToImageStageLabel(SketchToImageStatus stage)
    {
        Stage = stage;
        Focusable = false;
        IsHitTestVisible = false;
    }

    /// <summary>The stage this label names.</summary>
    public SketchToImageStatus Stage { get; }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="GlyphBrushProperty"/>
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsShimmeringProperty"/>
    public bool IsShimmering { get => GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }
}

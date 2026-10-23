using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How <see cref="ProjectsProgressLine"/> draws its fill.</summary>
internal enum ProjectsProgressFill
{
    /// <summary>A square-ended fill that grows from the left edge (Arc's <c>scaleX</c> sprint line).</summary>
    Scale,

    /// <summary>A full-width pill that slides in from the left (Arc's <c>translateX</c> readiness bar: the right end stays round).</summary>
    Slide,
}

/// <summary>
/// A thin progress line for the Projects blocks: a rounded track and a fill for <see cref="Value"/> /
/// <see cref="Maximum"/>, drawn in <see cref="Render"/> (no relayout). The fill moves on <see cref="Motion"/> (default:
/// the card spring); the first value after attach does not animate. The fill brush may change (Accent → Success when
/// complete): the theme animates it. Screen readers see a progress bar when <see cref="IsDecorative"/> is false.
/// </summary>
internal sealed class ProjectsProgressLine : Control
{
    /// <summary>The amount done (0 to <see cref="Maximum"/>).</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, double>(nameof(Value));

    /// <summary>The amount for a full line (1).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, double>(nameof(Maximum), 1);

    /// <summary>The track brush.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, IBrush?>(nameof(TrackBrush));

    /// <summary>The fill brush.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, IBrush?>(nameof(FillBrush));

    /// <summary>Corner radius of the track (and of the fill in <see cref="ProjectsProgressFill.Slide"/>); NaN = half the height.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, double>(nameof(Radius), double.NaN);

    /// <summary>How the fill is drawn.</summary>
    public static readonly StyledProperty<ProjectsProgressFill> FillModeProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, ProjectsProgressFill>(nameof(FillMode));

    /// <summary>True hides the line from screen readers (Arc's sprint line is <c>aria-hidden</c>).</summary>
    public static readonly StyledProperty<bool> IsDecorativeProperty =
        AvaloniaProperty.Register<ProjectsProgressLine, bool>(nameof(IsDecorative), true);

    private readonly MotionValue<double> _shown;
    private bool _hasShown;

    static ProjectsProgressLine()
    {
        AffectsRender<ProjectsProgressLine>(TrackBrushProperty, FillBrushProperty, RadiusProperty, FillModeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProjectsProgressLine>(false);
        FocusableProperty.OverrideDefaultValue<ProjectsProgressLine>(false);
    }

    public ProjectsProgressLine()
    {
        _shown = new MotionValue<double>(this, 0, MotionVectors.Double);
        _shown.Changed += _ => InvalidateVisual();
        UpdateAccessibility();
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="TrackBrushProperty"/>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <inheritdoc cref="FillBrushProperty"/>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <inheritdoc cref="FillModeProperty"/>
    public ProjectsProgressFill FillMode { get => GetValue(FillModeProperty); set => SetValue(FillModeProperty, value); }

    /// <inheritdoc cref="IsDecorativeProperty"/>
    public bool IsDecorative { get => GetValue(IsDecorativeProperty); set => SetValue(IsDecorativeProperty, value); }

    /// <summary>How the fill moves to a new value (default: the card spring, Gentle).</summary>
    public MotionSpec Motion { get; set; } = MotionSpec.Spring(ProjectsSprings.Card);

    /// <summary>The fraction drawn right now (0–1).</summary>
    public double ShownFraction => _shown.Value;

    /// <summary>The fraction <see cref="Value"/> asks for (0–1).</summary>
    public double Fraction => Maximum > 0 && double.IsFinite(Value) ? Math.Clamp(Value / Maximum, 0, 1) : 0;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The first value after attach is shown as it is (Arc renders the final state on load).
        _shown.Jump(Fraction);
        _hasShown = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hasShown = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MaximumProperty)
        {
            if (_hasShown)
            {
                _ = _shown.AnimateTo(Fraction, Motion);
            }
            else
            {
                _shown.Jump(Fraction);
            }
        }
        else if (change.Property == IsDecorativeProperty)
        {
            UpdateAccessibility();
        }
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetAccessibilityView(this, IsDecorative ? AccessibilityView.Raw : AccessibilityView.Content);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectsProgressLinePeer(this);

    protected override Size MeasureOverride(Size availableSize) => new(0, double.IsNaN(Height) ? 2 : Height);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        var radius = double.IsNaN(Radius) ? h / 2 : Math.Min(Radius, h / 2);
        var track = new RoundedRect(new Rect(0, 0, w, h), radius);
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, track);
        }
        var fraction = Math.Clamp(_shown.Value, 0, 1);
        if (FillBrush is not { } fill || fraction <= 0)
        {
            return;
        }
        using (context.PushClip(track))
        {
            if (FillMode == ProjectsProgressFill.Scale)
            {
                context.DrawRectangle(fill, null, new Rect(0, 0, w * fraction, h));
            }
            else
            {
                context.DrawRectangle(fill, null, new RoundedRect(new Rect(-(1 - fraction) * w, 0, w, h), radius));
            }
        }
    }
}

/// <summary>Reports <see cref="ProjectsProgressLine"/> as a read-only progress bar.</summary>
internal sealed class ProjectsProgressLinePeer(ProjectsProgressLine owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private ProjectsProgressLine Line => (ProjectsProgressLine)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override bool IsContentElementCore() => !Line.IsDecorative;

    protected override bool IsControlElementCore() => !Line.IsDecorative;

    public bool IsReadOnly => true;

    public double Minimum => 0;

    public double Maximum => Line.Maximum;

    public double Value => Line.Value;

    public double SmallChange => 1;

    public double LargeChange => 1;

    public void SetValue(double value) => throw new InvalidOperationException("The progress line is read only.");
}

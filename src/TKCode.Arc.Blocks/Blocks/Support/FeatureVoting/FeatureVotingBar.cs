using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The share bar under a <see cref="FeatureVoting"/> option: a track and a fill drawn in one pass. The fill follows
/// <see cref="Fraction"/> on a smooth spring (code-driven: the target is a share computed from live counts, and a new
/// vote that lands mid-glide keeps the speed); reduced or no motion places it at once.
/// </summary>
internal sealed class FeatureVotingBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<FeatureVotingBar, double>(nameof(Fraction));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<FeatureVotingBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<FeatureVotingBar, IBrush?>(nameof(FillBrush));

    private MotionValue<double>? _motion;
    private double _shown;

    static FeatureVotingBar()
    {
        AffectsRender<FeatureVotingBar>(TrackBrushProperty, FillBrushProperty);
    }

    public FeatureVotingBar()
    {
        IsHitTestVisible = false;
        ArcTokens.Bind(this, TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted);
    }

    /// <summary>The share to show, 0–1.</summary>
    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The share drawn now (tests).</summary>
    public double Shown => _shown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FractionProperty)
        {
            var target = Math.Clamp(change.GetNewValue<double>(), 0, 1);
            if (VisualRoot is null || !ArcMotion.IsFull(this))
            {
                _motion?.Jump(target);
                _shown = target;
                InvalidateVisual();
                return;
            }
            if (_motion is null)
            {
                _motion = new MotionValue<double>(this, _shown, MotionVectors.Double);
                _motion.Changed += value =>
                {
                    _shown = value;
                    InvalidateVisual();
                };
            }
            _ = _motion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        // The pill radius follows the corner style (square under the pixel style).
        var pill = ArcTokens.GetCornerRadius(this, ArcDesignTokenKind.RadiusPill, new CornerRadius(9999)).TopLeft;
        var radius = Math.Min(pill, bounds.Height / 2);
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, bounds, radius, radius);
        }
        var width = bounds.Width * Math.Clamp(_shown, 0, 1);
        if (FillBrush is { } fill && width > 0.5)
        {
            context.DrawRectangle(fill, null, new Rect(0, 0, Math.Max(width, bounds.Height), bounds.Height), radius, radius);
        }
    }
}

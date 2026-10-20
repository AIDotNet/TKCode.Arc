using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The Progress bar, drawn directly: a pill track (1px border, muted fill) with a full-width fill pill that slides in
/// from the left inside the rounded clip, so its round end keeps its shape at every value (P23). At 100% the success
/// fill fades in over the accent after a 200ms delay (one-way: leaving 100% returns without delay).
/// </summary>
internal sealed class ProgressTrack : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ProgressTrack, double>(nameof(Fraction));

    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<ProgressTrack, bool>(nameof(IsComplete));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> TrackBorderBrushProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(TrackBorderBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> CompleteFillBrushProperty =
        AvaloniaProperty.Register<ProgressTrack, IBrush?>(nameof(CompleteFillBrush));

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public bool IsComplete { get => GetValue(IsCompleteProperty); set => SetValue(IsCompleteProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? TrackBorderBrush { get => GetValue(TrackBorderBrushProperty); set => SetValue(TrackBorderBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? CompleteFillBrush { get => GetValue(CompleteFillBrushProperty); set => SetValue(CompleteFillBrushProperty, value); }

    private MotionValue<double>? _done;
    private IPen? _borderPen;

    static ProgressTrack()
    {
        AffectsRender<ProgressTrack>(FractionProperty, TrackBrushProperty, TrackBorderBrushProperty, FillBrushProperty, CompleteFillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProgressTrack>(false);
    }

    /// <summary>0 = accent fill, 1 = success fill.</summary>
    internal double Doneness => _done?.Value ?? (IsComplete ? 1 : 0);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompleteProperty)
        {
            var target = IsComplete ? 1.0 : 0.0;
            _done ??= CreateValue(1 - target);
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                var delay = IsComplete ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero;
                _ = _done.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, delay));
            }
            else
            {
                _done.Jump(target);
            }
        }
        else if (change.Property == TrackBorderBrushProperty)
        {
            _borderPen = null;
        }
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var radius = size.Height / 2;
        var outer = new RoundedRect(new Rect(size), radius);
        context.DrawRectangle(TrackBrush, null, outer);
        var inner = new Rect(size).Deflate(1);
        if (inner.Width > 0 && inner.Height > 0)
        {
            var innerRadius = inner.Height / 2;
            var fraction = Math.Clamp(Fraction, 0, 1);
            if (fraction > 0)
            {
                using (context.PushClip(new RoundedRect(inner, innerRadius)))
                {
                    var fill = new RoundedRect(inner.Translate(new Vector((fraction - 1) * inner.Width, 0)), innerRadius);
                    var done = Math.Clamp(Doneness, 0, 1);
                    if (done < 1)
                    {
                        context.DrawRectangle(FillBrush, null, fill);
                    }
                    if (done > 0)
                    {
                        using (context.PushOpacity(done))
                        {
                            context.DrawRectangle(CompleteFillBrush, null, fill);
                        }
                    }
                }
            }
        }
        if (TrackBorderBrush is { } borderBrush)
        {
            _borderPen ??= new Pen(borderBrush, 1);
            context.DrawRectangle(null, _borderPen, new RoundedRect(new Rect(size).Deflate(0.5), radius - 0.5));
        }
    }
}

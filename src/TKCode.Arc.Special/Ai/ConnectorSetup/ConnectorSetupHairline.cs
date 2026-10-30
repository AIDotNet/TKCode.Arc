using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The 2px progress line along the bottom of a connecting row. Between steps it eases to the step's share (240ms on the
/// standard curve); while the first sync counts it tracks the count linearly (a short linear tween per report, so sparse
/// reports still move evenly). When the connect ends it fills, then fades away. Reduced motion and Off: it jumps.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) because the target is the reported progress, which changes many times while
/// the line moves, and the fill is drawn rather than laid out; a ControlTheme transition cannot follow a value that
/// arrives from the app's sync.
/// </para>
/// </summary>
internal sealed class ConnectorSetupHairline : Control
{
    /// <summary>The line's color.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ConnectorSetupHairline, IBrush?>(nameof(Fill));

    private static readonly TimeSpan s_track = TimeSpan.FromMilliseconds(120);
    private static readonly Easing s_linear = new LinearEasing();

    private MotionValue<double>? _value;
    private MotionValue<double>? _fade;
    private double _target;

    static ConnectorSetupHairline()
    {
        AffectsRender<ConnectorSetupHairline>(FillProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ConnectorSetupHairline>(false);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>The share drawn now (0–1).</summary>
    internal double DisplayedValue => _value?.Value ?? 0;

    /// <summary>The share the line moves to.</summary>
    internal double Target => _target;

    /// <summary>The line's opacity now.</summary>
    internal double DisplayedOpacity => _fade?.Value ?? 0;

    private bool Animates => IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private MotionValue<double> Value
    {
        get
        {
            if (_value is null)
            {
                _value = new MotionValue<double>(this, 0, MotionVectors.Double);
                _value.Changed += _ => InvalidateVisual();
            }
            return _value;
        }
    }

    private MotionValue<double> Fade
    {
        get
        {
            if (_fade is null)
            {
                _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
                _fade.Changed += _ => InvalidateVisual();
            }
            return _fade;
        }
    }

    /// <summary>Moves the line to <paramref name="value"/>: eased between steps, linear while it tracks a count.</summary>
    public void MoveTo(double value, bool tracking)
    {
        value = Math.Clamp(value, 0, 1);
        _target = value;
        if (Fade.Target < 1)
        {
            if (Animates)
            {
                _ = Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            else
            {
                Fade.Jump(1);
            }
        }
        if (!Animates)
        {
            Value.Jump(value);
            InvalidateVisual();
            return;
        }
        if (value < Value.Value)
        {
            Value.Jump(value);
            InvalidateVisual();
            return;
        }
        _ = Value.AnimateTo(value, tracking
            ? MotionSpec.Tween(s_track, s_linear)
            : MotionSpec.Tween(ArcMotionPresets.Exit + TimeSpan.FromMilliseconds(60), ArcEasings.Standard));
    }

    /// <summary>Hides the line: <paramref name="complete"/> fills it first (a finished connect), otherwise it just fades.</summary>
    public void Finish(bool complete)
    {
        if (!Animates)
        {
            _target = 0;
            Value.Jump(0);
            Fade.Jump(0);
            InvalidateVisual();
            return;
        }
        _ = FinishAsync(complete);
    }

    private async Task FinishAsync(bool complete)
    {
        if (complete)
        {
            _target = 1;
            await Value.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            if (_target != 1)
            {
                return;
            }
        }
        _target = 0;
        var faded = await Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
        if (faded && _target == 0)
        {
            Value.Jump(0);
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var value = _value?.Value ?? 0;
        var opacity = _fade?.Value ?? 0;
        if (value <= 0 || opacity <= 0 || Fill is not { } fill)
        {
            return;
        }
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.FillRectangle(fill, new Rect(0, 0, Bounds.Width * Math.Clamp(value, 0, 1), Bounds.Height));
        }
    }
}

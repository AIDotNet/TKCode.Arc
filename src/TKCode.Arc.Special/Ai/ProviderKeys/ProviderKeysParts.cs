using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A 32px icon button of a provider row (remove): transparent at rest, SurfaceMuted under the pointer.</summary>
internal sealed class ProviderKeysIconButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ProviderKeysIconButton);
}

/// <summary>The reveal toggle: an icon button that reports pressed while the key is shown (Arc's <c>aria-pressed</c>).</summary>
internal sealed class ProviderKeysRevealButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(ProviderKeysRevealButton);
}

/// <summary>Rotate and Add key: buttons that report whether the inline form under the row is open (<c>aria-expanded</c>).</summary>
internal sealed class ProviderKeysDisclosureButton : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ProviderKeysDisclosureButton);
}

/// <summary>The status of a row with a key: icon plus label; pressing it tests the stored key again.</summary>
internal sealed class ProviderKeysStatusButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ProviderKeysStatusButton);
}

/// <summary>
/// A glyph whose changes scale and fade in over 160ms (the reveal eye, the helper's icon): code-driven because the new
/// glyph must start from 0.6 and transparent each time it changes, which a ControlTheme transition between two property
/// values cannot express. Reduced motion keeps the fade and drops the scale; Off changes at once.
/// </summary>
internal sealed class ProviderKeysSwapGlyph : ArcGlyph
{
    private readonly ScaleTransform _scale = new(1, 1);
    private MotionValue<double>? _enter;

    public ProviderKeysSwapGlyph()
    {
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty && change.OldValue is not null && IsLoaded)
        {
            Enter();
        }
    }

    private void Enter()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        var scales = level == ArcMotionLevel.Full;
        if (_enter is null)
        {
            _enter = new MotionValue<double>(this, 1, MotionVectors.Double);
            _enter.Changed += value =>
            {
                Opacity = Math.Clamp(value, 0, 1);
                var s = _scalesNow ? 0.6 + 0.4 * value : 1;
                _scale.ScaleX = s;
                _scale.ScaleY = s;
            };
        }
        _scalesNow = scales;
        _enter.Jump(0);
        _ = _enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private bool _scalesNow;
}

/// <summary>
/// The small spinner of Testing: a 270° arc turning once every 0.8s, every 1.6s under reduced motion (Arc slows it rather
/// than stopping it), still with motion off. Drawn under a rotation so a frame allocates nothing; driven by a Core
/// <see cref="LiveLoop"/> that only runs while the spinner is shown.
/// </summary>
internal sealed class ProviderKeysSpinner : Control
{
    /// <summary>The arc's brush.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ProviderKeysSpinner, IBrush?>(nameof(Foreground));

    private readonly LiveLoop _loop;
    private StreamGeometry? _arc;
    private Size _arcSize;
    private IPen? _pen;
    private double _angle;

    static ProviderKeysSpinner()
    {
        AffectsRender<ProviderKeysSpinner>(ForegroundProperty);
    }

    public ProviderKeysSpinner()
    {
        Width = 14;
        Height = 14;
        IsHitTestVisible = false;
        _loop = new LiveLoop(this, dt =>
        {
            var turn = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? 0.8 : 1.6;
            _angle = (_angle + dt * 360 / turn) % 360;
            InvalidateVisual();
            return true;
        })
        { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The rotation drawn now, in degrees.</summary>
    internal double Angle => _angle;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loop.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loop.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _pen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || Foreground is not { } brush)
        {
            return;
        }
        const double stroke = 1.75;
        if (_arc is null || _arcSize != size)
        {
            _arcSize = size;
            var r = Math.Min(size.Width, size.Height) / 2 - stroke / 2;
            var c = new Point(size.Width / 2, size.Height / 2);
            _arc = new StreamGeometry();
            using (var g = _arc.Open())
            {
                g.BeginFigure(new Point(c.X, c.Y - r), false);
                g.ArcTo(new Point(c.X - r, c.Y), new Size(r, r), 0, true, SweepDirection.Clockwise);
                g.EndFigure(false);
            }
        }
        _pen ??= new Pen(brush, stroke, lineCap: PenLineCap.Round);
        var center = new Point(size.Width / 2, size.Height / 2);
        var rotation = Matrix.CreateTranslation(-center.X, -center.Y)
                       * Matrix.CreateRotation(_angle * Math.PI / 180)
                       * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(rotation))
        {
            context.DrawGeometry(null, _pen, _arc);
        }
    }
}

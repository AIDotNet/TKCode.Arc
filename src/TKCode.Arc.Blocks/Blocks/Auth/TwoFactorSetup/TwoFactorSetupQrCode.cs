using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Draws the QR symbol of <see cref="Text"/> as square modules inside a quiet zone, scaled to its square. The modules are
/// grouped into diagonal bands, each one cached geometry, so the reveal (bands fading in from the top left corner) draws
/// the same geometry with a different opacity every frame and allocates nothing.
/// </summary>
internal sealed class TwoFactorSetupQrCode : Control
{
    private const int Bands = 12;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TwoFactorSetupQrCode, string?>(nameof(Text));

    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<TwoFactorSetupQrCode, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<int> QuietZoneProperty =
        AvaloniaProperty.Register<TwoFactorSetupQrCode, int>(nameof(QuietZone), 2);

    private readonly StreamGeometry?[] _bands = new StreamGeometry?[Bands];
    private TwoFactorSetupQrMatrix? _matrix;
    private MotionValue<double>? _reveal;

    static TwoFactorSetupQrCode()
    {
        AffectsRender<TwoFactorSetupQrCode>(InkProperty, QuietZoneProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<TwoFactorSetupQrCode>(AutomationControlType.Image);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

    public int QuietZone { get => GetValue(QuietZoneProperty); set => SetValue(QuietZoneProperty, value); }

    /// <summary>The encoded symbol (null when there is no text or it is too long).</summary>
    public TwoFactorSetupQrMatrix? Matrix => _matrix;

    /// <summary>How far the reveal has come (1 when nothing is animating).</summary>
    internal double RevealProgress => _reveal?.Value ?? 1;

    /// <summary>
    /// Lets the bands fade in from the top left corner. Code-driven because it plays each time the step opens (the view is
    /// reused) and the bands are drawn, not separate elements a theme transition could reach. Full motion only.
    /// </summary>
    public void PlayReveal()
    {
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _reveal?.Jump(1);
            InvalidateVisual();
            return;
        }
        if (_reveal is null)
        {
            _reveal = new MotionValue<double>(this, 0, MotionVectors.Double);
            _reveal.Changed += _ => InvalidateVisual();
        }
        _reveal.Jump(0);
        _ = _reveal.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Enter));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _matrix = string.IsNullOrEmpty(Text) ? null : TwoFactorSetupQrEncoder.Encode(Text);
        Array.Clear(_bands);
        if (_matrix is { } matrix)
        {
            var contexts = new StreamGeometryContext?[Bands];
            var size = matrix.Size;
            var span = 2 * size - 1;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (!matrix[x, y])
                    {
                        continue;
                    }
                    // One rectangle per run of dark modules in the same band, so no seams show between neighbours.
                    var band = (x + y) * Bands / span;
                    var end = x + 1;
                    while (end < size && matrix[end, y] && (end + y) * Bands / span == band)
                    {
                        end++;
                    }
                    if (contexts[band] is null)
                    {
                        _bands[band] = new StreamGeometry();
                        contexts[band] = _bands[band]!.Open();
                    }
                    var context = contexts[band]!;
                    context.BeginFigure(new Point(x, y), true);
                    context.LineTo(new Point(end, y));
                    context.LineTo(new Point(end, y + 1));
                    context.LineTo(new Point(x, y + 1));
                    context.EndFigure(true);
                    x = end - 1;
                }
            }
            foreach (var context in contexts)
            {
                context?.Dispose();
            }
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Math.Min(availableSize.Width, availableSize.Height);
        if (double.IsInfinity(side))
        {
            side = double.IsInfinity(availableSize.Width) ? availableSize.Height : availableSize.Width;
        }
        if (double.IsInfinity(side))
        {
            side = 160;
        }
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        if (_matrix is not { } matrix || Ink is not { } ink)
        {
            return;
        }
        var side = Math.Min(Bounds.Width, Bounds.Height);
        var modules = matrix.Size + 2 * QuietZone;
        var scale = side / modules;
        if (scale <= 0)
        {
            return;
        }
        // Whole-pixel modules keep the edges sharp (a camera reads crisp edges best); the rest goes to the margin.
        if (scale >= 2)
        {
            scale = Math.Floor(scale);
        }
        var drawn = scale * matrix.Size;
        var offset = new Vector(Math.Round((Bounds.Width - drawn) / 2), Math.Round((Bounds.Height - drawn) / 2));
        var progress = RevealProgress;
        using (context.PushTransform(Avalonia.Matrix.CreateScale(scale, scale) * Avalonia.Matrix.CreateTranslation(offset)))
        {
            for (var band = 0; band < Bands; band++)
            {
                if (_bands[band] is not { } geometry)
                {
                    continue;
                }
                var opacity = progress >= 1 ? 1 : Math.Clamp((progress * (Bands + 3) - band) / 3, 0, 1);
                if (opacity <= 0)
                {
                    continue;
                }
                if (opacity >= 1)
                {
                    context.DrawGeometry(ink, null, geometry);
                }
                else
                {
                    using (context.PushOpacity(opacity))
                    {
                        context.DrawGeometry(ink, null, geometry);
                    }
                }
            }
        }
    }
}

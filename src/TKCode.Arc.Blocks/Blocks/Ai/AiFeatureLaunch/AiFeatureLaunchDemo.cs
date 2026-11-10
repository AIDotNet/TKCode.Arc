using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The built-in demo of an <see cref="AiFeatureLaunch"/>: a short loop in which a page of message lines is read from top
/// to bottom by a soft band and turned into a summary card of three points. One loop is <see cref="Period"/> long: the
/// lines write in (0–1.2s), the band sweeps them (1.2–2.6s), the card rises and its points grow (2.6–4.4s), the scene
/// holds and then fades for the next round. It is drawn in one pass with no allocations per frame and runs on a
/// <see cref="LiveLoop"/>, so it stops when hidden, scrolled away, paused, or when motion is reduced or off; then it
/// shows the finished scene.
/// </summary>
internal sealed class AiFeatureLaunchDemo : Control
{
    /// <summary>One round of the loop, in seconds.</summary>
    public const double Period = 6.4;

    /// <summary>The moment drawn while the loop does not run: the finished scene.</summary>
    public const double RestTime = 4.6;

    private static readonly double[] s_lineWidths = [0.92, 0.74, 0.86, 0.58, 0.9, 0.7, 0.82, 0.46];
    private static readonly double[] s_pointWidths = [0.86, 0.7, 0.78];

    public static readonly StyledProperty<IBrush?> PageBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(PageBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentSoftBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(AccentSoftBrush));

    public static readonly StyledProperty<IBrush?> CardBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(CardBrush));

    public static readonly StyledProperty<IBrush?> CardBorderBrushProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, IBrush?>(nameof(CardBorderBrush));

    public static readonly StyledProperty<CornerRadius> PageRadiusProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, CornerRadius>(nameof(PageRadius), new CornerRadius(10));

    public static readonly StyledProperty<CornerRadius> CardRadiusProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, CornerRadius>(nameof(CardRadius), new CornerRadius(12));

    public static readonly StyledProperty<CornerRadius> LineRadiusProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, CornerRadius>(nameof(LineRadius), new CornerRadius(9999));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<AiFeatureLaunchDemo, bool>(nameof(IsPlaying), true);

    private readonly LiveLoop _loop;
    private Pen? _cardPen;
    private double _time;
    private bool _running;

    static AiFeatureLaunchDemo()
    {
        AffectsRender<AiFeatureLaunchDemo>(PageBrushProperty, InkBrushProperty, AccentBrushProperty, AccentSoftBrushProperty,
            CardBrushProperty, CardBorderBrushProperty, PageRadiusProperty, CardRadiusProperty, LineRadiusProperty);
    }

    public AiFeatureLaunchDemo()
    {
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running =>
        {
            _running = running;
            InvalidateVisual();
        };
        _loop.Start();
    }

    public IBrush? PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? AccentSoftBrush { get => GetValue(AccentSoftBrushProperty); set => SetValue(AccentSoftBrushProperty, value); }

    public IBrush? CardBrush { get => GetValue(CardBrushProperty); set => SetValue(CardBrushProperty, value); }

    public IBrush? CardBorderBrush { get => GetValue(CardBorderBrushProperty); set => SetValue(CardBorderBrushProperty, value); }

    public CornerRadius PageRadius { get => GetValue(PageRadiusProperty); set => SetValue(PageRadiusProperty, value); }

    public CornerRadius CardRadius { get => GetValue(CardRadiusProperty); set => SetValue(CardRadiusProperty, value); }

    public CornerRadius LineRadius { get => GetValue(LineRadiusProperty); set => SetValue(LineRadiusProperty, value); }

    /// <summary>Whether the loop may run; false holds the current frame.</summary>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <summary>True while frames are being drawn.</summary>
    public bool IsRunning => _running;

    /// <summary>The moment of the loop being drawn, in seconds.</summary>
    public double Time => _running || !IsPlaying ? _time : RestTime;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            if (IsPlaying)
            {
                _loop.Start();
            }
            else
            {
                // Pausing under full motion holds the frame on screen; otherwise the finished scene is shown.
                if (!_running)
                {
                    _time = RestTime;
                }
                _loop.Stop();
            }
            InvalidateVisual();
        }
        else if (change.Property == CardBorderBrushProperty)
        {
            _cardPen = null;
        }
    }

    private bool Step(double dt)
    {
        _time += dt;
        if (_time >= Period)
        {
            _time -= Period;
        }
        InvalidateVisual();
        return true;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    // Ease out (cubic): fast start, soft landing.
    private static double EaseOut(double v)
    {
        var inv = 1 - Clamp01(v);
        return 1 - inv * inv * inv;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width < 40 || size.Height < 40)
        {
            return;
        }
        var t = Time;
        // The whole scene fades out at the end of a round and back in at its start.
        var sceneOpacity = t > Period - 0.5 ? Clamp01((Period - t) / 0.5) : 1;
        using var fade = context.PushOpacity(sceneOpacity);

        // The page: a raised sheet on the left two thirds.
        var page = new Rect(size.Width * 0.08, size.Height * 0.12, size.Width * 0.56, size.Height * 0.76);
        DrawRounded(context, PageBrush, null, page, PageRadius);

        var lineHeight = Math.Max(4, page.Height / 22);
        var pitch = (page.Height - lineHeight * 2) / s_lineWidths.Length;
        var inner = page.Width - lineHeight * 4;
        var sweep = Clamp01((t - 1.2) / 1.4);
        var bandY = page.Top + lineHeight + sweep * (page.Height - lineHeight * 2);
        for (var i = 0; i < s_lineWidths.Length; i++)
        {
            var write = EaseOut((t - i * 0.12) / 0.5);
            if (write <= 0)
            {
                continue;
            }
            var y = page.Top + lineHeight + i * pitch + (pitch - lineHeight) / 2;
            var x = page.Left + lineHeight * 2;
            var rect = new Rect(x, y, inner * s_lineWidths[i] * write, lineHeight);
            // Lines the band has passed keep a small accent mark: they were read.
            var read = sweep > 0 && y + lineHeight <= bandY;
            DrawRounded(context, InkBrush, null, rect, LineRadius);
            if (read && i % 3 != 1)
            {
                var mark = Math.Max(3, lineHeight * 0.9);
                context.DrawEllipse(AccentBrush, null, new Point(x - lineHeight, y + lineHeight / 2), mark / 2, mark / 2);
            }
        }
        if (sweep > 0 && sweep < 1)
        {
            var band = new Rect(page.Left, bandY - lineHeight * 1.5, page.Width, lineHeight * 3);
            using (context.PushOpacity(Math.Sin(sweep * Math.PI) * 0.9))
            {
                context.FillRectangle(AccentSoftBrush!, band);
            }
        }

        // The summary card rises over the right edge of the page.
        var rise = EaseOut((t - 2.6) / 0.6);
        if (rise > 0)
        {
            var card = new Rect(size.Width * 0.44, size.Height * 0.3, size.Width * 0.48, size.Height * 0.48);
            using (context.PushOpacity(rise))
            using (context.PushTransform(Matrix.CreateTranslation(0, (1 - rise) * 10)))
            {
                _cardPen ??= new Pen(CardBorderBrush, 1);
                DrawRounded(context, CardBrush, _cardPen, card, CardRadius);
                // A sparkle-like accent dot leads the card's title line.
                var pad = card.Height * 0.14;
                var dot = Math.Max(4, lineHeight * 1.1);
                context.DrawEllipse(AccentBrush, null, new Point(card.Left + pad + dot / 2, card.Top + pad + dot / 2), dot / 2, dot / 2);
                var title = new Rect(card.Left + pad + dot * 1.8, card.Top + pad + dot / 2 - lineHeight / 2, (card.Width - pad * 2) * 0.42, lineHeight);
                DrawRounded(context, AccentSoftBrush, null, title, LineRadius);
                var rowPitch = (card.Height - pad * 2 - dot * 1.6) / s_pointWidths.Length;
                for (var i = 0; i < s_pointWidths.Length; i++)
                {
                    var grow = EaseOut((t - 3.0 - i * 0.22) / 0.5);
                    if (grow <= 0)
                    {
                        continue;
                    }
                    var y = card.Top + pad + dot * 1.8 + i * rowPitch + (rowPitch - lineHeight) / 2;
                    var bullet = lineHeight * 0.8;
                    context.DrawEllipse(AccentBrush, null, new Point(card.Left + pad + bullet / 2, y + lineHeight / 2), bullet / 2, bullet / 2);
                    var x = card.Left + pad + bullet * 2;
                    var rect = new Rect(x, y, (card.Right - pad - x) * s_pointWidths[i] * grow, lineHeight);
                    DrawRounded(context, InkBrush, null, rect, LineRadius);
                }
            }
        }
    }

    private static void DrawRounded(DrawingContext context, IBrush? brush, IPen? pen, Rect rect, CornerRadius radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var r = Math.Min(radius.TopLeft, Math.Min(rect.Width, rect.Height) / 2);
        context.DrawRectangle(brush, pen, rect, r, r);
    }
}

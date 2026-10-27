using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the hover card says about a day.</summary>
/// <param name="Title">"Service · Mon, Mar 12".</param>
/// <param name="Percent">The day's uptime ("98.40%").</param>
/// <param name="Status">The day's status.</param>
/// <param name="StatusName">Its name ("Operational").</param>
/// <param name="Rows">One row per incident of the day: title and duration.</param>
/// <param name="EmptyText">The row shown when there was no incident.</param>
internal sealed record UptimeTipContent(string Title, string Percent, ServiceStatus Status, string StatusName,
    IReadOnlyList<(string Title, string Duration)> Rows, string EmptyText);

/// <summary>
/// The hover card of the day bars (notes 3.6, 4 and 6.2): an overlay over the whole Uptime section so it can cover the row
/// above, 256 wide, padding (12, 10), radius 12, a 1px border, Surface (SurfaceRaised in dark) with the floating shadow.
/// Row 1: "service · date" left and the day's uptime right (12, tabular); row 2: the status mark and name (14, 500); row 3:
/// a rule, then each incident's title and duration, or the empty text. It sits 8 above the band, centred on the bar and
/// clamped 8 from the section's edges; appearing, it fades in 160ms (Enter) from 97%; between bars it follows on a spring
/// (k 925, c 58); leaving, it fades out in 120ms (Standard) to 98% and goes. Hidden from assistive technology.
/// </summary>
/// <remarks>Why code-driven: the card's position follows a spring per move and it is drawn over the clipping rows above it.</remarks>
internal sealed class UptimeTipLayer : Control
{
    private const double Padding = 12;
    private const double PaddingY = 10;
    private const double RowGap = 6;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _x = new(0);
    private AnalyticsSpringChannel _y = new(0);
    private AnalyticsTween _opacity = new(0);
    private AnalyticsTween _scale = new(0.97);
    private UptimeTipContent? _content;
    private bool _shown;
    private bool _visible;

    public UptimeTipLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>The card is up (or on its way up).</summary>
    public bool IsShown => _shown;

    /// <summary>The card's top-left now.</summary>
    public Point Position => new(_x.Value, _y.Value);

    /// <summary>The card's opacity now.</summary>
    public double CardOpacity => Math.Clamp(_opacity.Value, 0, 1);

    /// <summary>The current content.</summary>
    internal UptimeTipContent? Content => _content;

    /// <summary>Where the card goes when shown: the left edge and top.</summary>
    internal Point Target => new(_x.Target, _y.Target);

    /// <summary>The card's size for a content.</summary>
    public Size Measure(UptimeTipContent content)
    {
        var rows = Math.Max(1, content.Rows.Count);
        var height = PaddingY + 16.8 + RowGap + 19.6 + RowGap + 1 + 8 + rows * 16.8 + (rows - 1) * 4 + PaddingY;
        return new Size(UptimeEngine.TipWidth, Math.Ceiling(height));
    }

    /// <summary>
    /// Shows the card for a bar: <paramref name="barCenter"/> is the bar's centre and <paramref name="stripTop"/> the
    /// band's top, both in this layer's coordinates.
    /// </summary>
    public void Show(UptimeTipContent content, double barCenter, double stripTop)
    {
        var size = Measure(content);
        var left = UptimeEngine.TipLeft(barCenter, Bounds.Width);
        var top = stripTop - UptimeEngine.TipGap - size.Height;
        var level = ArcMotion.GetEffectiveLevel(this);
        _content = content;
        var follow = _shown && level == ArcMotionLevel.Full;
        if (follow)
        {
            _x.SpringTo(left, AnalyticsSprings.CardFollow);
            _y.SpringTo(top, AnalyticsSprings.CardFollow);
        }
        else
        {
            _x.Jump(left);
            _y.Jump(top);
        }
        if (!_shown)
        {
            _shown = true;
            _visible = true;
            if (level == ArcMotionLevel.Off)
            {
                _opacity.Jump(1);
                _scale.Jump(1);
            }
            else
            {
                // Reduced motion keeps the short fade; the scale is a transform and is skipped.
                _opacity.Jump(0);
                _opacity.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter);
                if (level == ArcMotionLevel.Full)
                {
                    _scale.Jump(0.97);
                    _scale.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.Enter);
                }
                else
                {
                    _scale.Jump(1);
                }
            }
        }
        InvalidateVisual();
        _loop.Start();
    }

    /// <summary>Fades the card out (120ms, to 98%).</summary>
    public void Hide()
    {
        if (!_shown)
        {
            return;
        }
        _shown = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            _opacity.Jump(0);
            _visible = false;
        }
        else
        {
            _opacity.Start(0, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            if (level == ArcMotionLevel.Full)
            {
                _scale.Start(0.98, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            }
        }
        InvalidateVisual();
        _loop.Start();
    }

    private bool Advance(double dt)
    {
        var moving = _x.Step(dt) | _y.Step(dt);
        moving |= _opacity.Advance(dt);
        moving |= _scale.Advance(dt);
        if (!_shown && !_opacity.IsRunning)
        {
            _visible = false;
        }
        InvalidateVisual();
        return moving;
    }

    private void RefreshPalette()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        if (!_visible || _content is null)
        {
            return;
        }
        var p = _palette;
        var content = _content;
        var size = Measure(content);
        var rect = new Rect(new Point(_x.Value, _y.Value), size);
        var center = rect.Center;
        var scale = Math.Clamp(_scale.Value, 0.5, 1.2);
        using (context.PushOpacity(Math.Clamp(_opacity.Value, 0, 1)))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            var fill = p.Brush(dark ? p.SurfaceRaised : p.Surface);
            context.DrawRectangle(fill, p.Pen(p.Border, 1), new RoundedRect(rect.Deflate(0.5), 12), p.ShadowFloating);
            var body = new Typeface(p.BodyFont);
            var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
            var left = rect.X + Padding;
            var right = rect.Right - Padding;
            var y = rect.Y + PaddingY;

            var percent = _text.Get(content.Percent, body, 12, true);
            percent.Draw(context, p.Brush(p.TextSecondary), new Point(right - percent.Width, y + (16.8 - percent.Height) / 2));
            var title = _text.GetTrimmed(content.Title, body, 12, Math.Max(10, right - left - percent.Width - 12));
            title.Draw(context, p.Brush(p.TextSecondary), new Point(left, y + (16.8 - title.Height) / 2));
            y += 16.8 + RowGap;

            var h = content.Status switch { ServiceStatus.Operational => 14.0, ServiceStatus.Degraded => 9.0, _ => 5.0 };
            context.DrawRectangle(p.Brush(UptimeColors.Bar(p, content.Status)), null,
                new RoundedRect(new Rect(left, y + (19.6 - 14) / 2 + 14 - h, 8, h), 1.5));
            var status = _text.Get(content.StatusName, medium, 14);
            status.Draw(context, p.Brush(p.Foreground), new Point(left + 8 + 8, y + (19.6 - status.Height) / 2));
            y += 19.6 + RowGap;

            var rule = Math.Floor(y) + 0.5;
            context.DrawLine(p.Pen(p.BorderSubtle, 1, cap: PenLineCap.Flat), new Point(left, rule), new Point(right, rule));
            y += 1 + 8;
            if (content.Rows.Count == 0)
            {
                var empty = _text.GetTrimmed(content.EmptyText, body, 12, right - left);
                empty.Draw(context, p.Brush(p.TextMuted), new Point(left, y + (16.8 - empty.Height) / 2));
            }
            foreach (var (rowTitle, duration) in content.Rows)
            {
                var d = _text.Get(duration, body, 12, true);
                d.Draw(context, p.Brush(p.TextMuted), new Point(right - d.Width, y + (16.8 - d.Height) / 2));
                var t = _text.GetTrimmed(rowTitle, body, 12, Math.Max(10, right - left - d.Width - 12));
                t.Draw(context, p.Brush(p.TextSecondary), new Point(left, y + (16.8 - t.Height) / 2));
                y += 16.8 + 4;
            }
        }
    }
}

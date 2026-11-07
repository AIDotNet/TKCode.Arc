using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The colors a <see cref="LinkUnfurlChipRenderer"/> paints with, captured from the composer's theme.</summary>
internal sealed record LinkUnfurlChipColors(
    Color Fill,
    Color Ring,
    Color Text,
    Color Muted,
    Color Strong,
    Color Surface,
    Color Accent,
    Color Sweep,
    double AccentFill,
    double AccentRing);

/// <summary>
/// Paints the links of a <see cref="LinkUnfurl"/> as inline chips (Arc Link unfurl, design reference): 26 high, margin
/// 0 1, padding 0 10 0 5, a 16px favicon slot and a 6px gap before a 14px label, at most 224 wide with an ellipsis.
/// Loading: surface-muted fill, a 1px border-subtle ring, secondary text, a breathing 14px globe and a sweep band (55% of
/// the chip, −110% → 210% of its width in 1.15s, in-out). Resolved: the favicon pops in (0.6 → 1, 272ms) and the title
/// rolls up 5px (340ms) while the width eases to the title's (340ms, enter). Unfurled: the accent at 11% / 34% (dark 20%
/// / 48%) as fill and ring, eased over 160ms.
/// </summary>
/// <remarks>
/// Code-driven (<see cref="MotionValue{T}"/>, one <see cref="LiveLoop"/> for the sweep): the chips are runs of the
/// editor's text painted by its mirror, not controls a theme could animate. Brushes, pens and shaped text are cached,
/// so a running sweep or morph allocates nothing per frame.
/// </remarks>
internal sealed class LinkUnfurlChipRenderer : TokenRenderer, IDisposable
{
    /// <summary>The token kind of a link.</summary>
    public const string Kind = "link";

    public const double Height = 26;
    public const double MaxWidth = 224;
    public const double Margin = 1;
    public const double PadLeft = 5;
    public const double PadRight = 10;
    public const double Slot = 16;
    public const double Gap = 6;
    public const double LabelSize = 14;
    public const double LabelSpacing = -0.14;
    public const double SweepPeriod = 1.15;
    public const double SweepBand = 0.55;

    private readonly LinkUnfurl _owner;
    private readonly LiveLoop _sweep;
    private readonly Dictionary<int, IBrush> _brushes = new();
    private readonly Dictionary<int, IPen> _pens = new();
    private LinkUnfurlChipColors? _colors;
    private int _version;
    private IBrush? _sweepBrush;
    private IPen? _globePen;
    private Typeface _typeface;
    private bool _hasTypeface;
    private double _phase;

    public LinkUnfurlChipRenderer(LinkUnfurl owner)
    {
        _owner = owner;
        Cursor = new Cursor(StandardCursorType.Hand);
        _sweep = new LiveLoop(owner, dt =>
        {
            _phase = (_phase + dt / SweepPeriod) % 1;
            Invalidate();
            return true;
        });
        _sweep.RunningChanged += _ => Invalidate();
    }

    /// <summary>The sweep's phase within its 1.15s period (0 to 1).</summary>
    public double Phase => _phase;

    /// <summary>Whether the sweep is moving (full motion, shown, something loading, not paused).</summary>
    public bool IsSweeping => _sweep.IsRunning;

    /// <summary>The editor's typeface once a chip was measured (labels are shaped with it at 14px).</summary>
    public bool HasTypeface => _hasTypeface;

    /// <summary>Starts or stops the sweep (the owner calls it when a link starts or stops loading, or on pause).</summary>
    public void UpdateSweep(bool wanted)
    {
        if (wanted)
        {
            _sweep.Start();
        }
        else
        {
            _sweep.Stop();
            _phase = 0;
        }
    }

    /// <summary>Takes new colors: brushes and shaped text are made again.</summary>
    public void SetColors(LinkUnfurlChipColors colors)
    {
        if (Equals(colors, _colors))
        {
            return;
        }
        _colors = colors;
        _version++;
        _brushes.Clear();
        _pens.Clear();
        _sweepBrush = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Colors.Transparent),
                new ImmutableGradientStop(0.5, colors.Sweep),
                new ImmutableGradientStop(1, Colors.Transparent)
            ],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
        _globePen = new ImmutablePen(new ImmutableSolidColorBrush(colors.Muted), 1.4 * 24 / 14, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Invalidate();
    }

    /// <summary>Asks the editor to lay the chips out again (a width changed).</summary>
    public void Remeasure() => InvalidateMeasure();

    /// <summary>Asks the editor to paint the chips again.</summary>
    public void Repaint() => Invalidate();

    /// <summary>The width a chip needs for <paramref name="label"/> (padding included, at most 224); NaN before the first measure.</summary>
    public double NaturalWidth(string label)
    {
        if (!_hasTypeface)
        {
            return double.NaN;
        }
        using var layout = Shape(label, Brushes.Black, double.PositiveInfinity);
        return Math.Min(MaxWidth, PadLeft + Slot + Gap + Math.Ceiling(layout.WidthIncludingTrailingWhitespace) + PadRight);
    }

    public override TokenInlineSize? Measure(TokenSpan token, in TokenRenderContext context)
    {
        if (token.Kind != Kind)
        {
            return null;
        }
        if (!_hasTypeface || !context.Typeface.Equals(_typeface))
        {
            _typeface = context.Typeface;
            _hasTypeface = true;
            _owner.OnChipTypefaceChanged();
        }
        var width = MaxWidth;
        if (_owner.EntryFor(token.Id) is { } entry)
        {
            if (double.IsNaN(entry.ShownWidth))
            {
                entry.ShownWidth = NaturalWidth(entry.Label);
                entry.TargetWidth = entry.ShownWidth;
            }
            width = entry.ShownWidth;
        }
        return new TokenInlineSize(width + 2 * Margin, context.Ascent, context.Descent);
    }

    /// <summary>The chip's pill inside the box the editor gave it: 26 high, centred on the line.</summary>
    public static Rect Pill(Rect bounds)
    {
        var center = bounds.Y + bounds.Height / 2;
        return new Rect(bounds.X + Margin, center - Height / 2, Math.Max(0, bounds.Width - 2 * Margin), Height);
    }

    public override void Render(DrawingContext drawingContext, TokenSpan token, Rect bounds, double baseline, in TokenRenderContext context)
    {
        if (_colors is not { } colors || _owner.EntryFor(token.Id) is not { } entry)
        {
            return;
        }
        var pill = Pill(bounds);
        var exit = Math.Clamp(entry.Exit.Value, 0, 1);
        if (exit <= 0.001)
        {
            return;
        }
        using var opacity = drawingContext.PushOpacity(exit);
        var scale = 0.9 + 0.1 * exit;
        var center = pill.Center;
        using var transform = drawingContext.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                                           Matrix.CreateTranslation(center.X, center.Y));
        var status = entry.Link.Status;
        var hot = Math.Clamp(entry.Hot.Value, 0, 1);
        var radius = Height / 2;
        var accentFill = Over(colors.Surface, Oklch.WithAlpha(colors.Accent, colors.AccentFill));
        var fill = Mix(colors.Fill, accentFill, hot);
        var baseRing = status == LinkUnfurlStatus.Ready ? Oklch.WithAlpha(colors.Ring, 0) : colors.Ring;
        var ring = Mix(baseRing, Oklch.WithAlpha(colors.Accent, colors.AccentRing), hot);
        drawingContext.DrawRectangle(Brush(fill), null, new RoundedRect(pill, radius));
        if (ring.A > 0)
        {
            drawingContext.DrawRectangle(null, Pen(ring), new RoundedRect(pill.Deflate(0.5), radius - 0.5));
        }
        var loading = status == LinkUnfurlStatus.Loading;
        using (drawingContext.PushClip(new RoundedRect(pill, radius)))
        {
            if (loading && _sweep.IsRunning && _sweepBrush is not null)
            {
                var band = pill.Width * SweepBand;
                var eased = ArcEasings.InOut.Ease(_phase);
                var x = pill.X + (-1.1 + 3.2 * eased) * band;
                drawingContext.DrawRectangle(_sweepBrush, null, new Rect(x, pill.Y, band, pill.Height));
            }
            DrawSlot(drawingContext, entry, colors, new Rect(pill.X + PadLeft, pill.Y + (Height - Slot) / 2, Slot, Slot));
            DrawLabel(drawingContext, entry, colors, pill);
        }
    }

    private void DrawSlot(DrawingContext context, LinkUnfurlEntry entry, LinkUnfurlChipColors colors, Rect slot)
    {
        if (entry.Link.Status != LinkUnfurlStatus.Ready || entry.Link.Preview is not { } preview)
        {
            // The globe: 14px, Lucide stroke 1.4, breathing 1 → 0.45 → 1 while the sweep runs.
            var breath = 1.0;
            if (entry.Link.Status == LinkUnfurlStatus.Loading && _sweep.IsRunning)
            {
                breath = _phase < 0.5
                    ? 1 - 0.55 * ArcEasings.InOut.Ease(_phase * 2)
                    : 0.45 + 0.55 * ArcEasings.InOut.Ease((_phase - 0.5) * 2);
            }
            using var o = context.PushOpacity(breath);
            var size = 14.0;
            var origin = new Point(slot.X + (Slot - size) / 2, slot.Y + (Slot - size) / 2);
            using var t = context.PushTransform(Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(origin.X, origin.Y));
            context.DrawGeometry(null, _globePen, ArcIcons.Globe);
            return;
        }
        var pop = Math.Clamp(entry.Pop.Value, 0, 1);
        if (pop <= 0.001)
        {
            return;
        }
        var s = 0.6 + 0.4 * pop;
        var c = slot.Center;
        using var fade = context.PushOpacity(pop);
        using var grow = context.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(c.X, c.Y));
        DrawFavicon(context, preview, slot, entry, Brush(colors.Strong), colors.Surface, _version);
    }

    /// <summary>Draws a 16px favicon (or the site's initial on a square) at <paramref name="slot"/>, rounded 4.</summary>
    internal static void DrawFavicon(DrawingContext context, LinkUnfurlPreview preview, Rect slot, LinkUnfurlEntry? cache, IBrush ink,
                                     Color paper, int version)
    {
        var rounded = new RoundedRect(slot, 4);
        if (preview.Favicon is { } icon)
        {
            using var clip = context.PushClip(rounded);
            context.DrawImage(icon, LinkUnfurlMedia.SourceRect(icon), slot);
            return;
        }
        context.DrawRectangle(ink, null, rounded);
        TextLayout? layout = cache?.MonogramLayout;
        if (layout is null || cache!.MonogramVersion != version)
        {
            layout?.Dispose();
            layout = new TextLayout(preview.Monogram, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), 10,
                new ImmutableSolidColorBrush(paper));
            if (cache is not null)
            {
                cache.MonogramLayout = layout;
                cache.MonogramVersion = version;
            }
        }
        layout.Draw(context, new Point(slot.X + (slot.Width - layout.WidthIncludingTrailingWhitespace) / 2, slot.Y + (slot.Height - layout.Height) / 2));
        if (cache is null)
        {
            layout.Dispose();
        }
    }

    private void DrawLabel(DrawingContext context, LinkUnfurlEntry entry, LinkUnfurlChipColors colors, Rect pill)
    {
        var x = pill.X + PadLeft + Slot + Gap;
        var target = double.IsNaN(entry.TargetWidth) ? pill.Width : entry.TargetWidth;
        var room = Math.Max(0, target - PadLeft - Slot - Gap - PadRight);
        var ready = entry.Link.Status == LinkUnfurlStatus.Ready;
        var layout = LabelFor(entry, room, ready ? colors.Strong : colors.Text);
        var reveal = ready ? Math.Clamp(entry.Reveal.Value, 0, 1) : 1;
        var y = pill.Y + (Height - layout.Height) / 2 + 5 * (1 - reveal);
        using var fade = context.PushOpacity(reveal);
        layout.Draw(context, new Point(x, y));
    }

    private TextLayout LabelFor(LinkUnfurlEntry entry, double room, Color color)
    {
        var key = HashCode.Combine(_version, color);
        if (entry.LabelLayout is { } cached && entry.LabelLayoutText == entry.Label && Math.Abs(entry.LabelLayoutWidth - room) < 0.5 &&
            entry.LabelLayoutVersion == key)
        {
            return cached;
        }
        entry.LabelLayout?.Dispose();
        var layout = Shape(entry.Label, new ImmutableSolidColorBrush(color), room);
        entry.LabelLayout = layout;
        entry.LabelLayoutText = entry.Label;
        entry.LabelLayoutWidth = room;
        entry.LabelLayoutVersion = key;
        return layout;
    }

    private TextLayout Shape(string text, IBrush brush, double maxWidth) =>
        new(text, _typeface, LabelSize, brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, double.IsInfinity(maxWidth) ? double.PositiveInfinity : Math.Max(1, maxWidth), double.PositiveInfinity,
            LabelSize, LabelSpacing);

    /// <summary><paramref name="top"/> composited over <paramref name="bottom"/> (both straight alpha).</summary>
    internal static Color Over(Color bottom, Color top)
    {
        var a = top.A / 255.0;
        return Color.FromArgb(bottom.A, (byte)Math.Round(bottom.R + (top.R - bottom.R) * a), (byte)Math.Round(bottom.G + (top.G - bottom.G) * a),
            (byte)Math.Round(bottom.B + (top.B - bottom.B) * a));
    }

    internal static Color Mix(Color a, Color b, double t)
    {
        if (t <= 0)
        {
            return a;
        }
        if (t >= 1)
        {
            return b;
        }
        static byte L(byte x, byte y, double t) => (byte)Math.Round(x + (y - x) * t);
        return Color.FromArgb(L(a.A, b.A, t), L(a.R, b.R, t), L(a.G, b.G, t), L(a.B, b.B, t));
    }

    private IBrush Brush(Color color)
    {
        var key = (int)color.ToUInt32();
        if (!_brushes.TryGetValue(key, out var brush))
        {
            if (_brushes.Count > 256)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[key] = brush;
        }
        return brush;
    }

    private IPen Pen(Color color)
    {
        var key = (int)color.ToUInt32();
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 256)
            {
                _pens.Clear();
            }
            pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1);
            _pens[key] = pen;
        }
        return pen;
    }

    public void Dispose()
    {
        _sweep.Dispose();
    }
}

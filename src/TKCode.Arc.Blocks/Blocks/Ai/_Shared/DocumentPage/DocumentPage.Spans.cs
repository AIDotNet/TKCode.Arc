using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <remarks>
/// <para>
/// Spans: every <see cref="DocumentSpan"/> is a character range of one <see cref="DocumentPageText"/> (a paragraph, a cell, a
/// line) with two <see cref="TextRangeDecoration"/>s on it: its background (the group's series color at full strength, the
/// decoration's opacity being the 10 / 32 / 24% of the state) and its check underline. A span that wraps gets one rounded
/// background per line, as CSS <c>box-decoration-break: clone</c> does.
/// </para>
/// <para>
/// Code-driven motion (<see cref="MotionValue{T}"/>): a span's tint is painted inside its text's render pass, not by a
/// control, so no ControlTheme transition can reach it; its 160 ms change is a value that redraws the text.
/// </para>
/// </remarks>
internal sealed partial class DocumentPage
{
    /// <summary>The tint of a linked span (the group color at 10%).</summary>
    internal const double LinkedAlpha = 0.10;

    /// <summary>The tint of a value that just landed (32%).</summary>
    internal const double FreshAlpha = 0.32;

    /// <summary>The tint of a lit span (24%).</summary>
    internal const double LitAlpha = 0.24;

    /// <summary>A span's background reaches this far past its text horizontally (CSS padding 2px, margin −2px).</summary>
    internal const double SpanInlinePadding = 2;

    /// <summary>A span's background reaches this far past its text box vertically (CSS padding 1px).</summary>
    internal const double SpanBlockPadding = 1;

    /// <summary>The corner radius of a span's background.</summary>
    internal const double SpanRadius = 3;

    /// <summary>
    /// Where the center of the check underline sits below the baseline: CSS <c>text-underline-offset: 2.5px</c> puts the
    /// top of the 1px line there.
    /// </summary>
    internal const double CheckUnderlineOffset = 3;

    /// <summary>The share of paper ink mixed into the warning color of the check underline.</summary>
    internal const double CheckInkShare = 0.15;

    private static readonly MotionSpec s_fullTint = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
    private static readonly MotionSpec s_reducedTint = MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing());

    private readonly Dictionary<string, SpanEntry> _spans = new(StringComparer.Ordinal);
    private readonly List<SpanEntry> _spanOrder = [];
    private readonly IBrush?[] _toneBrushes = new IBrush?[4];
    private readonly Color[] _inkKey = new Color[5];
    private IPen? _checkPen;

    /// <summary>The ids of the document's spans in reading order (tests and the owner's checks).</summary>
    internal IEnumerable<string> SpanIds => _spanOrder.Select(s => s.Id);

    /// <summary>
    /// Paints the span <paramref name="id"/>, or clears it with null. <see cref="DocumentSpanState.Fresh"/> comes on at once;
    /// every other change of the tint takes 160 ms on the standard curve at full motion, 120 ms linear at reduced motion,
    /// and jumps when motion is off. The check underline follows <see cref="DocumentSpanPaint.NeedsCheck"/> at once. An
    /// unknown id does nothing.
    /// </summary>
    public void SetSpan(string id, DocumentSpanPaint? paint)
    {
        if (!_spans.TryGetValue(id, out var entry))
        {
            return;
        }
        entry.Paint = paint;
        var brush = paint is { } p ? ToneBrush(p.Tone) : null;
        var check = paint is { NeedsCheck: true } ? 1.0 : 0.0;
        foreach (var segment in entry.Segments)
        {
            if (brush is not null)
            {
                segment.Background.Background = brush;
            }
            segment.Underline.Opacity = check;
            // The tone or the underline may change while the tint stays where it is.
            segment.Text.InvalidateVisual();
        }
        var target = paint is { } q ? AlphaOf(q.State) : 0;
        if (paint is { State: DocumentSpanState.Fresh })
        {
            JumpTint(entry, target);
        }
        else
        {
            AnimateTint(entry, target);
        }
        if (paint is null && string.Equals(_hoveredSpanId, id, StringComparison.Ordinal))
        {
            SetHover(null);
        }
    }

    /// <summary>Clears every span's paint (each tint fades out as <see cref="SetSpan"/> with null does).</summary>
    public void ClearSpans()
    {
        foreach (var entry in _spanOrder)
        {
            if (entry.Paint is not null || entry.Tint > 0)
            {
                SetSpan(entry.Id, null);
            }
        }
    }

    /// <summary>The paint of span <paramref name="id"/> (null when unpainted or unknown).</summary>
    internal DocumentSpanPaint? GetSpanPaint(string id) => _spans.TryGetValue(id, out var entry) ? entry.Paint : null;

    /// <summary>The tint the span shows now, 0 to 0.32 (tests).</summary>
    internal double GetSpanTint(string id) => _spans.TryGetValue(id, out var entry) ? entry.Tint : 0;

    /// <summary>The text control and the two decorations of each piece of a span (tests).</summary>
    internal IEnumerable<(DocumentPageText Text, TextRangeDecoration Background, TextRangeDecoration Underline)> GetSpanPieces(string id) =>
        _spans.TryGetValue(id, out var entry) ? entry.Segments.Select(s => (s.Text, s.Background, s.Underline)) : [];

    /// <summary>
    /// The rectangles of span <paramref name="id"/>: one per line its text occupies, in the page's own unscaled coordinates,
    /// grown by the 2px × 1px padding its background is painted with. False for an unknown id or a span not laid out yet.
    /// The list is cached and kept while neither the text layouts nor their places on the page change; a change gives a new
    /// list, so a list once returned never changes.
    /// </summary>
    public bool TryGetSpanRects(string id, out IReadOnlyList<Rect> rects)
    {
        if (!_spans.TryGetValue(id, out var entry))
        {
            rects = [];
            return false;
        }
        rects = Measure(entry);
        return rects.Count > 0;
    }

    /// <summary>
    /// The id of the span under <paramref name="point"/> (page coordinates, as <see cref="TryGetSpanRects"/>), painted or
    /// not, else null. Where two padded rectangles overlap the span whose text is nearest wins.
    /// </summary>
    public string? SpanAt(Point point) => FindSpan(point, paintedOnly: false)?.Id;

    /// <summary>Registers a span the builder put on <paramref name="text"/> from <paramref name="start"/>.</summary>
    internal void RegisterSpan(string id, DocumentPageText text, int start, int length)
    {
        if (!_spans.TryGetValue(id, out var entry))
        {
            entry = new SpanEntry(id);
            _spans.Add(id, entry);
            _spanOrder.Add(entry);
        }
        var segment = new SpanSegment(text, start, length)
        {
            Background =
            {
                CornerRadius = SpanRadius,
                Outset = new Thickness(SpanInlinePadding, SpanBlockPadding),
                Extent = TextRangeExtent.Text,
                Opacity = 0
            },
            Underline =
            {
                Underline = _checkPen,
                UnderlineOffset = CheckUnderlineOffset,
                Opacity = 0
            }
        };
        entry.Segments.Add(segment);
        text.AddSpan(segment.Background, segment.Underline);
    }

    private SpanEntry? FindSpan(Point point, bool paintedOnly)
    {
        SpanEntry? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var entry in _spanOrder)
        {
            if (paintedOnly && entry.Paint is null)
            {
                continue;
            }
            var rects = Measure(entry);
            for (var i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                if (!rect.Contains(point))
                {
                    continue;
                }
                // Distance to the text itself (the rectangle without its padding): 0 inside it.
                var text = rect.Deflate(new Thickness(SpanInlinePadding, SpanBlockPadding));
                var dx = Math.Max(0, Math.Max(text.Left - point.X, point.X - text.Right));
                var dy = Math.Max(0, Math.Max(text.Top - point.Y, point.Y - text.Bottom));
                var distance = dx + dy;
                if (distance < bestDistance)
                {
                    best = entry;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The page rectangles of a span: each segment's rectangles in its layout's coordinates are cached on the layout
    /// instance (a text block makes a new layout whenever it is measured or arranged again), and moved by the segment's
    /// offset on the page, read through the visual tree on every call (no layout pass is needed to notice a move).
    /// </summary>
    private Rect[] Measure(SpanEntry entry)
    {
        var changed = false;
        var count = 0;
        foreach (var segment in entry.Segments)
        {
            var text = segment.Text;
            if (text.TranslatePoint(text.TextOrigin, this) is not { } offset || text.Bounds.Width <= 0)
            {
                if (segment.Layout is not null)
                {
                    segment.Layout = null;
                    segment.LocalRects = [];
                    changed = true;
                }
                continue;
            }
            var layout = text.TextLayout;
            if (!ReferenceEquals(layout, segment.Layout))
            {
                segment.Layout = layout;
                segment.LocalRects = TextRangeRenderer.GetRects(layout, segment.Start, segment.Length);
                changed = true;
            }
            if (offset != segment.Offset)
            {
                segment.Offset = offset;
                changed = true;
            }
            count += segment.LocalRects.Count;
        }
        if (!changed && count == entry.Rects.Length)
        {
            return entry.Rects;
        }
        var rects = count == 0 ? [] : new Rect[count];
        var k = 0;
        var padding = new Thickness(SpanInlinePadding, SpanBlockPadding);
        foreach (var segment in entry.Segments)
        {
            if (segment.Layout is null)
            {
                continue;
            }
            foreach (var local in segment.LocalRects)
            {
                rects[k++] = local.Translate(new Vector(segment.Offset.X, segment.Offset.Y)).Inflate(padding);
            }
        }
        entry.Rects = rects;
        return rects;
    }

    private static double AlphaOf(DocumentSpanState state) => state switch
    {
        DocumentSpanState.Fresh => FreshAlpha,
        DocumentSpanState.Lit => LitAlpha,
        _ => LinkedAlpha
    };

    private void AnimateTint(SpanEntry entry, double target)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            JumpTint(entry, target);
            return;
        }
        if (entry.Motion is null)
        {
            if (entry.Tint == target)
            {
                return;
            }
            entry.Motion = new MotionValue<double>(this, entry.Tint, MotionVectors.Double);
            entry.Motion.Changed += entry.Apply;
        }
        _ = entry.Motion.AnimateTo(target, level == ArcMotionLevel.Full ? s_fullTint : s_reducedTint);
    }

    private static void JumpTint(SpanEntry entry, double target)
    {
        if (entry.Motion is { } motion)
        {
            motion.Jump(target);
        }
        else
        {
            entry.Apply(target);
        }
    }

    /// <summary>Drops every span (the document is being rebuilt).</summary>
    private void ResetSpans()
    {
        foreach (var entry in _spanOrder)
        {
            entry.Motion?.Dispose();
        }
        _spans.Clear();
        _spanOrder.Clear();
    }

    /// <summary>Puts every running tint at its target (the page left the tree).</summary>
    private void SettleSpans()
    {
        foreach (var entry in _spanOrder)
        {
            if (entry.Motion is { IsAnimating: true } motion)
            {
                motion.Jump(motion.Target);
            }
        }
    }

    /// <summary>The series color of a tone at full strength (the decoration's opacity makes the tint).</summary>
    private IBrush ToneBrush(int tone)
    {
        var index = Math.Clamp(tone, 1, 4) - 1;
        return _toneBrushes[index] ??= new ImmutableSolidColorBrush(PromptTone.SeriesColor(this, index + 1));
    }

    /// <summary>
    /// The theme may have changed: the series colors and the warning color of the check underline are read again, and the
    /// spans repaint when one of them differs.
    /// </summary>
    private void RefreshInk()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        var warning = ArcTokens.GetColor(this, ArcDesignTokenKind.Warning);
        var changed = _checkPen is null || warning != _inkKey[4];
        _inkKey[4] = warning;
        for (var tone = 1; tone <= 4; tone++)
        {
            var series = PromptTone.SeriesColor(this, tone);
            changed |= series != _inkKey[tone - 1];
            _inkKey[tone - 1] = series;
        }
        if (!changed)
        {
            return;
        }
        Array.Clear(_toneBrushes);
        var color = Oklch.MixOklab(warning, DocumentInk.Ink, CheckInkShare);
        _checkPen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1, new ImmutableDashStyle([1, 1], 0));
        foreach (var entry in _spanOrder)
        {
            var brush = entry.Paint is { } paint ? ToneBrush(paint.Tone) : null;
            foreach (var segment in entry.Segments)
            {
                segment.Underline.Underline = _checkPen;
                if (brush is not null)
                {
                    segment.Background.Background = brush;
                }
                segment.Text.InvalidateVisual();
            }
        }
    }

    /// <summary>A span id: its pieces on the page, its paint and its tint.</summary>
    private sealed class SpanEntry(string id)
    {
        public string Id { get; } = id;

        public List<SpanSegment> Segments { get; } = new(1);

        public DocumentSpanPaint? Paint { get; set; }

        /// <summary>The tint shown now (the background decorations' opacity).</summary>
        public double Tint { get; private set; }

        public MotionValue<double>? Motion { get; set; }

        public Rect[] Rects { get; set; } = [];

        public void Apply(double tint)
        {
            Tint = tint;
            foreach (var segment in Segments)
            {
                segment.Background.Opacity = tint;
                segment.Text.InvalidateVisual();
            }
        }
    }

    /// <summary>One piece of a span: a range of one text, its two decorations and the cached rectangles.</summary>
    private sealed class SpanSegment(DocumentPageText text, int start, int length)
    {
        public DocumentPageText Text { get; } = text;

        public int Start { get; } = start;

        public int Length { get; } = length;

        public TextRangeDecoration Background { get; } = new(start, length);

        public TextRangeDecoration Underline { get; } = new(start, length);

        public TextLayout? Layout { get; set; }

        public IReadOnlyList<Rect> LocalRects { get; set; } = [];

        public Point Offset { get; set; }
    }
}

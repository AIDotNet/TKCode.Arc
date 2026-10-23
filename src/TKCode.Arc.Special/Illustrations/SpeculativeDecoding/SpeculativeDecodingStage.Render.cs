using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal sealed partial class SpeculativeDecodingStage
{
    private const int BandStrips = 12;
    private const double BandEm = 4.4;
    private const double ShimmerDelay = 0.145;
    private const double ShimmerSweep = 0.9;
    private const double ShimmerPeriod = 1.8;

    private bool _paletteDirty = true;
    private Color _draft;
    private Color _target;
    private Color _fg;
    private Color _secondary;
    private Color _muted;
    private Color _ghost;
    private IBrush _ghostFill = Brushes.Transparent;
    private IBrush _acceptedFill = Brushes.Transparent;
    private IBrush _rejectedFill = Brushes.Transparent;
    private IBrush _targetFill = Brushes.Transparent;
    private IBrush _discardFill = Brushes.Transparent;
    private IBrush _bonusFill = Brushes.Transparent;
    private IBrush _cellFill = Brushes.Transparent;
    private IBrush _ruleFill = Brushes.Transparent;
    private IBrush _strikeFill = Brushes.Transparent;
    private IBrush _draftDot = Brushes.Transparent;
    private IBrush _targetDot = Brushes.Transparent;
    private IBrush _draftUnderline = Brushes.Transparent;
    private IBrush _targetUnderline = Brushes.Transparent;
    private IPen? _ghostPen;
    private IPen? _acceptedPen;
    private IPen? _rejectedPen;
    private IPen? _slotPen;
    private IPen? _checkPen;
    private IPen? _crossPen;
    private IPen? _plusPen;
    private IPen? _metaCheckPen;
    private int _summaryKey = -1;
    private string _summaryDrafts = string.Empty;
    private string _summaryPasses = string.Empty;
    private int _metaCount = -1;
    private string _metaText = string.Empty;
    private int _compareCount = -1;
    private string _compareText = string.Empty;

    public override void Render(DrawingContext context)
    {
        if (_layoutDirty || Math.Abs(Bounds.Width - _layoutWidth) > 0.01)
        {
            EnsureLayout(Bounds.Width);
        }
        if (_paletteDirty)
        {
            RebuildPalette();
        }
        var t = _time;
        DrawLaneNames(context);
        DrawTrack(context, t);
        context.DrawRectangle(_ruleFill, null, new Rect(0, RuleY, Bounds.Width, 1));
        DrawOutputs(context, t);
    }

    // ---- lanes ------------------------------------------------------------------------------------------------------

    private void DrawLaneNames(DrawingContext context)
    {
        if (_isNarrow)
        {
            DrawLegendItem(context, 0, 0, _draftName, _draftDetail, _draftDot, 3);
            DrawLegendItem(context, _legendTargetX, _legendTargetY, _targetName, _targetDetail, _targetDot, 5);
            return;
        }
        DrawLaneName(context, _lanesTop, _draftName, _draftDetail, _draftDot, 3);
        DrawLaneName(context, _lanesTop + Row + RowGap, _targetName, _targetDetail, _targetDot, 5);
    }

    // Wide: a 10 px dot slot (6 px dot for the small model, 10 px for the large one), then the name over the detail.
    private void DrawLaneName(DrawingContext context, double top, string name, string detail, IBrush dot, double radius)
    {
        context.DrawEllipse(dot, null, new Point(5, top + Row / 2), radius, radius);
        var width = LabelColumn - 20;
        var nameLayout = _text.Get(name, Body, FontWeight.Medium, _fg, width);
        if (detail.Length == 0)
        {
            nameLayout.Draw(context, new Point(20, top + (Row - nameLayout.Height) / 2));
            return;
        }
        var detailLayout = _text.Get(detail, Small, FontWeight.Normal, _muted, width);
        var block = 16.8 + 14.4;
        var y = top + (Row - block) / 2;
        nameLayout.Draw(context, new Point(20, y + (16.8 - nameLayout.Height) / 2));
        detailLayout.Draw(context, new Point(20, y + 16.8 + (14.4 - detailLayout.Height) / 2));
    }

    // Narrow: dot, name and detail on one line, the detail on the name's baseline.
    private void DrawLegendItem(DrawingContext context, double x, double y, string name, string detail, IBrush dot, double radius)
    {
        var nameLayout = _text.Get(name, Body, FontWeight.Medium, _fg, Math.Max(1, _layoutWidth - x - 16));
        var nameTop = y + (LegendRow - nameLayout.Height) / 2;
        context.DrawEllipse(dot, null, new Point(x + 5, y + LegendRow / 2), radius, radius);
        nameLayout.Draw(context, new Point(x + 10 + LegendInner, nameTop));
        if (detail.Length == 0)
        {
            return;
        }
        var detailX = x + 10 + LegendInner + nameLayout.WidthIncludingTrailingWhitespace + LegendInner;
        var detailLayout = _text.Get(detail, Small, FontWeight.Normal, _muted, Math.Max(1, _layoutWidth - detailX));
        detailLayout.Draw(context, new Point(detailX, nameTop + nameLayout.Baseline - detailLayout.Baseline));
    }

    private void DrawTrack(DrawingContext context, double t)
    {
        var timeline = _timeline;
        using (context.PushClip(new Rect(_trackX - 4, _lanesTop, _trackWidth + 4, TrackHeightAt(t))))
        {
            if (_showsSlots || timeline is null || timeline.Count == 0)
            {
                DrawSlots(context);
                return;
            }
            if (t >= timeline.MainEnd)
            {
                DrawSummary(context, timeline, t);
                return;
            }
            var r = timeline.RoundAt(t);
            if (r >= 0 && r < timeline.Count)
            {
                DrawRound(context, timeline, r, t);
            }
        }
    }

    private void DrawSlots(DrawingContext context)
    {
        foreach (var slot in _slots)
        {
            var x = _trackX + slot.X;
            var y = _lanesTop + slot.Y;
            context.DrawRectangle(null, _slotPen, new RoundedRect(new Rect(x + 0.5, y + 0.5, slot.Width - 1, Row - 1), ChipRadius - 0.5));
            context.DrawRectangle(_cellFill, null, new RoundedRect(new Rect(x, y + Row + RowGap, slot.Width, Row), ChipRadius));
        }
    }

    // At the end the columns give way to two lines of totals, one beside each lane.
    private void DrawSummary(DrawingContext context, DecodeTimeline timeline, double t)
    {
        var alpha = Eased(ArcEasings.Enter, (t - timeline.MainEnd - SummaryDelay) / SummaryIn);
        if (alpha <= 0)
        {
            return;
        }
        var key = timeline.GetHashCode() ^ _labels.GetHashCode();
        if (key != _summaryKey)
        {
            _summaryKey = key;
            var plan = timeline.Plan;
            _summaryDrafts = _labels.DraftSummary(plan.Drafted, plan.Accepted);
            _summaryPasses = _labels.PassSummary(plan.Passes, plan.Tokens.Count);
        }
        using (context.PushOpacity(alpha))
        {
            var first = _text.Get(_summaryDrafts, Body, FontWeight.Normal, _secondary, _trackWidth);
            var second = _text.Get(_summaryPasses, Body, FontWeight.Normal, _secondary, _trackWidth);
            first.Draw(context, new Point(_trackX, _lanesTop + (Row - first.Height) / 2));
            second.Draw(context, new Point(_trackX, _lanesTop + Row + RowGap + (Row - second.Height) / 2));
        }
    }

    private void DrawRound(DrawingContext context, DecodeTimeline timeline, int r, double t)
    {
        var columns = _rounds[r];
        var settle = timeline.Settle(r);
        var exit = t >= settle ? 1 - Eased(ArcEasings.Standard, (t - settle) / ColumnOut) : 1;
        if (exit <= 0)
        {
            return;
        }
        var sweep = timeline.Sweep(r, t);
        if (!_isTravel)
        {
            // Reduced motion: the sweep steps a whole cell at a time, at the verdict.
            sweep = Math.Floor(sweep + 0.5);
        }
        for (var j = 0; j < columns.Length; j++)
        {
            var column = columns[j];
            var mount = column.Kind == CellKind.Bonus ? timeline.SweepEnd(r) : timeline.ChipTime(r, j);
            if (t < mount)
            {
                continue;
            }
            var enter = _isTravel ? Eased(ArcEasings.Enter, (t - mount) / ColumnIn) : 1;
            var x = _trackX + column.X - (_isTravel ? 4 * (1 - enter) : 0);
            var y = _lanesTop + column.Y;
            using (context.PushOpacity(enter * exit))
            {
                var verdict = column.Kind == CellKind.Bonus ? mount : timeline.VerdictTime(r, j);
                if (column.Kind != CellKind.Bonus)
                {
                    DrawChip(context, column, x, y, t, mount, verdict);
                }
                DrawCell(context, column, j, x, y + Row + RowGap, t, sweep, verdict);
            }
        }
    }

    private void DrawChip(DrawingContext context, Column column, double x, double y, double t, double mount, double verdict)
    {
        var judged = t >= verdict;
        var v = judged ? Eased(ArcEasings.Standard, (t - verdict) / VerdictIn) : 0;
        var rect = new Rect(x + 0.5, y + 0.5, column.Width - 1, Row - 1);
        var box = new RoundedRect(rect, ChipRadius - 0.5);
        var chipAlpha = column.Kind == CellKind.Discarded ? 1 - 0.7 * v : 1;
        using (context.PushOpacity(chipAlpha))
        {
            var ghost = column.Kind == CellKind.Discarded ? 1 : 1 - v;
            if (ghost > 0)
            {
                using (context.PushOpacity(ghost))
                {
                    context.DrawRectangle(_ghostFill, _ghostPen, box);
                }
            }
            if (v > 0 && column.Kind is CellKind.Accepted or CellKind.Rejected)
            {
                using (context.PushOpacity(v))
                {
                    context.DrawRectangle(column.Kind == CellKind.Accepted ? _acceptedFill : _rejectedFill,
                        column.Kind == CellKind.Accepted ? _acceptedPen : _rejectedPen, box);
                }
            }

            var maxWidth = column.Width - 2 * ChipInset;
            var width = Math.Min(column.ChipWidth, maxWidth);
            var left = x + (column.Width - width) / 2;
            var top = y + (Row - _textHeight) / 2;
            // The guess waits in the muted ghost color under a passing shimmer; at its verdict the text settles into the
            // verdict's color (foreground when kept or discarded, muted when struck) while the band slides off.
            var settled = judged ? Eased(ArcEasings.Standard, (t - verdict) / TextSettle) : 0;
            var final = column.Kind == CellKind.Rejected ? _muted : _fg;
            if (settled < 1)
            {
                using (context.PushOpacity(1 - settled))
                {
                    _text.Get(column.Chip, Body, FontWeight.Normal, _ghost, maxWidth + 0.5).Draw(context, new Point(left, top));
                }
            }
            if (settled > 0)
            {
                using (context.PushOpacity(settled))
                {
                    _text.Get(column.Chip, Body, FontWeight.Normal, final, maxWidth + 0.5).Draw(context, new Point(left, top));
                }
            }
            if (_isTravel && settled < 1)
            {
                DrawShimmer(context, column.Chip, maxWidth, left, top, width, t - mount - ShimmerDelay, 1 - settled);
            }
            if (judged && column.Kind == CellKind.Rejected)
            {
                var p = _isTravel ? Eased(ArcEasings.Enter, (t - verdict) / StrikeIn) : 1;
                var strike = new Rect(left - 2, top + _textHeight / 2 - UnderlineThickness / 2, (width + 4) * p, UnderlineThickness);
                context.DrawRectangle(_strikeFill, null, new RoundedRect(strike, 1));
            }
        }
    }

    // A soft band of foreground (4.4 em, bell shaped) crosses the ghost text in 0.9 s every 1.8 s, drawn as twelve
    // clipped strips of the bright text at stepped opacities, so nothing is allocated per frame.
    private void DrawShimmer(DrawingContext context, string text, double maxWidth, double left, double top, double width, double since, double alpha)
    {
        if (since < 0 || alpha <= 0)
        {
            return;
        }
        var phase = since % ShimmerPeriod;
        if (phase >= ShimmerSweep)
        {
            return;
        }
        var band = BandEm * Body;
        var start = left - band + (width + band) * (phase / ShimmerSweep);
        var layout = _text.Get(text, Body, FontWeight.Normal, _fg, maxWidth + 0.5);
        var strip = band / BandStrips;
        for (var i = 0; i < BandStrips; i++)
        {
            var a = start + i * strip;
            var l = Math.Max(a, left);
            var r = Math.Min(a + strip, left + width);
            if (r <= l)
            {
                continue;
            }
            var weight = Bell((i + 0.5) / BandStrips) * alpha;
            if (weight < 0.02)
            {
                continue;
            }
            using (context.PushClip(new Rect(l, top - 2, r - l, _textHeight + 4)))
            using (context.PushOpacity(weight))
            {
                layout.Draw(context, new Point(left, top));
            }
        }
    }

    private void DrawCell(DrawingContext context, Column column, int j, double x, double y, double t, double sweep, double verdict)
    {
        var rect = new Rect(x, y, column.Width, Row);
        var box = new RoundedRect(rect, ChipRadius);
        if (column.Kind == CellKind.Bonus)
        {
            // The bonus cell arrives already tinted, with a plus and the target's extra token.
            context.DrawRectangle(_bonusFill, null, box);
            DrawCellWord(context, ArcIcons.Plus, _plusPen, column, x, y, 0);
            return;
        }
        context.DrawRectangle(_cellFill, null, box);
        var fill = Math.Clamp(sweep - j, 0, 1);
        if (fill > 0)
        {
            using (context.PushClip(box))
            {
                context.DrawRectangle(column.Kind == CellKind.Discarded ? _discardFill : _targetFill, null, new Rect(x, y, column.Width * fill, Row));
            }
        }
        if (t < verdict || column.Kind == CellKind.Discarded)
        {
            return;
        }
        var c = Eased(ArcEasings.Enter, (t - verdict) / VerdictIn);
        var dy = _isTravel ? 2 * (1 - c) : 0;
        using (context.PushOpacity(c))
        {
            if (column.Kind == CellKind.Accepted)
            {
                DrawIcon(context, ArcIcons.Check, _checkPen, x + (column.Width - 14) / 2, y + (Row - 14) / 2 + dy, 14);
            }
            else
            {
                DrawCellWord(context, ArcIcons.X, _crossPen, column, x, y, dy);
            }
        }
    }

    // A 13 px icon, 4 px, then the target's token, centered in the cell.
    private void DrawCellWord(DrawingContext context, Geometry icon, IPen? pen, Column column, double x, double y, double dy)
    {
        var maxWidth = Math.Max(1, column.Width - 2 * ChipInset - 17);
        var word = Math.Min(column.CellWidth, maxWidth);
        var left = x + (column.Width - (17 + word)) / 2;
        DrawIcon(context, icon, pen, left, y + (Row - 13) / 2 + dy, 13);
        _text.Get(column.Cell, Body, FontWeight.Normal, _fg, maxWidth + 0.5).Draw(context, new Point(left + 17, y + (Row - _textHeight) / 2 + dy));
    }

    // ---- outputs ----------------------------------------------------------------------------------------------------

    private void DrawOutputs(DrawingContext context, double t)
    {
        var top = OutputsTop;
        DrawLine(context, top, main: true, t);
        if (_compareOpen <= 0)
        {
            return;
        }
        var line = LineBlockHeight;
        var second = top + line + LineGap;
        var visible = _compareOpen * (LineGap + line) - LineGap;
        if (visible <= 0)
        {
            return;
        }
        using (context.PushClip(new Rect(0, second, Bounds.Width, visible + UnderlineOffset + 2)))
        using (context.PushOpacity(_compareOpen))
        {
            DrawLine(context, second, main: false, t);
        }
    }

    private void DrawLine(DrawingContext context, double top, bool main, double t)
    {
        var timeline = _timeline;
        var count = timeline?.Plan.Tokens.Count ?? 0;
        var running = !_showsSlots && timeline is not null;

        // Name (the answer line's name crossfades between Output and Speculative), then the pass count.
        var nameTop = (_isNarrow ? top : top + 3) + (NameLine - _textHeight) / 2;
        double nameBaseline;
        if (main)
        {
            var since = _wall - _labelSwapWall;
            var current = _text.Get(_compareLabel ? _labels.Speculative : _labels.Output, Body, FontWeight.Medium, _fg, LabelColumn);
            nameBaseline = nameTop + current.Baseline;
            if (since < 0.16)
            {
                var previous = _text.Get(_compareLabel ? _labels.Output : _labels.Speculative, Body, FontWeight.Medium, _fg, LabelColumn);
                var outAlpha = 1 - Math.Clamp(since / 0.1, 0, 1);
                if (outAlpha > 0)
                {
                    using (context.PushOpacity(outAlpha))
                    {
                        previous.Draw(context, new Point(0, nameTop));
                    }
                }
                var inAlpha = Eased(ArcEasings.Enter, since / 0.16);
                using (context.PushOpacity(inAlpha))
                {
                    current.Draw(context, new Point(0, nameTop + (_isTravel ? 2 * (1 - inAlpha) : 0)));
                }
            }
            else
            {
                current.Draw(context, new Point(0, nameTop));
            }
        }
        else
        {
            var name = _text.Get(_labels.TargetOnly, Body, FontWeight.Medium, _fg, LabelColumn);
            name.Draw(context, new Point(0, nameTop));
            nameBaseline = nameTop + name.Baseline;
        }

        var passes = !running ? 0 : main ? timeline!.SettledAt(t) : timeline!.CompareWritten(t);
        var done = running && (main ? t >= timeline!.MainEnd && timeline.Count > 0 : count > 0 && passes >= count);
        var meta = _text.Get(MetaText(main, passes), Small, FontWeight.Normal, _muted);
        var metaWidth = meta.WidthIncludingTrailingWhitespace + (done ? 17 : 0);
        double metaX, metaTop;
        if (_isNarrow)
        {
            metaX = Math.Max(0, _layoutWidth - metaWidth);
            metaTop = nameBaseline - meta.Baseline;
        }
        else
        {
            metaX = 0;
            metaTop = top + 3 + NameLine + 1 + (MetaLine - meta.Height) / 2;
        }
        if (done)
        {
            DrawIcon(context, ArcIcons.Check, _metaCheckPen, metaX, metaTop + (meta.Height - 13) / 2, 13);
            metaX += 17;
        }
        meta.Draw(context, new Point(metaX, metaTop));

        if (!running)
        {
            return;
        }
        var textTop = LineTextTop(top);
        for (var q = 0; q < count; q++)
        {
            DrawToken(context, timeline!, q, textTop, main, t);
        }
    }

    private void DrawToken(DrawingContext context, DecodeTimeline timeline, int q, double textTop, bool main, double t)
    {
        var landed = main ? timeline.LandTime(q) : timeline.CompareTime(q);
        var since = t - landed;
        double alpha, underline, dx = 0, dy = 0;
        var slotX = _textX + _tokX[q];
        var top = textTop + _tokLine[q] * TextLine + (TextLine - _textHeight) / 2;
        if (since >= 0)
        {
            alpha = Eased(ArcEasings.Enter, since / TokenIn);
            underline = Eased(ArcEasings.Standard, (since - UnderlineDelay) / UnderlineIn);
            if (main && _isTravel)
            {
                // Critically damped spring from the chip (or the cell) to the token's place in the answer.
                var f = (1 + FlightOmega * since) * Math.Exp(-FlightOmega * since);
                var (sx, sy) = Source(timeline, q, slotX, top);
                dx = sx * f;
                dy = sy * f;
                if (Math.Abs(dx) < 0.15 && Math.Abs(dy) < 0.15)
                {
                    dx = dy = 0;
                }
            }
        }
        else if (main && _fadeFrom >= 0 && landed <= _fadeFrom)
        {
            // Restart: what the last run had written fades out where it stands.
            alpha = 1 - Math.Clamp((_wall - _fadeWall) / TokenIn, 0, 1);
            underline = 1;
        }
        else
        {
            return;
        }
        if (alpha <= 0)
        {
            return;
        }
        using (context.PushOpacity(alpha))
        {
            _text.Get(_tokText[q], Body, FontWeight.Normal, _fg).Draw(context, new Point(slotX + dx, top + dy));
            if (underline > 0 && _tokW[q] > 0)
            {
                var byDraft = main && timeline.Plan.ByDraft[q];
                using (context.PushOpacity(underline))
                {
                    context.DrawRectangle(byDraft ? _draftUnderline : _targetUnderline, null,
                        new Rect(slotX + dx, top + dy + TextBaseline + UnderlineOffset - UnderlineThickness / 2, _tokW[q], UnderlineThickness));
                }
            }
        }
    }

    private double TextBaseline => _text.Get("Ag", Body, FontWeight.Normal, Colors.Black).Baseline;

    /// <summary>
    /// Where answer token <paramref name="q"/> starts its flight, relative to its place: the text of its chip for an
    /// accepted guess, the target's word in its cell for the target's token (left edges and text middles aligned).
    /// </summary>
    private (double X, double Y) Source(DecodeTimeline timeline, int q, double slotX, double slotTop)
    {
        var plan = timeline.Plan;
        var r = plan.RoundOf(q);
        var round = plan.Rounds[r];
        var columns = _rounds[r];
        var i = q - round.Start;
        double left, top;
        if (i < round.Accepted)
        {
            var column = columns[i];
            var width = Math.Min(column.ChipWidth, column.Width - 2 * ChipInset);
            left = _trackX + column.X + (column.Width - width) / 2;
            top = _lanesTop + column.Y + (Row - _textHeight) / 2;
        }
        else
        {
            var column = columns[round.IsBonus ? round.Count : round.Accepted];
            var word = Math.Min(column.CellWidth, Math.Max(1, column.Width - 2 * ChipInset - 17));
            left = _trackX + column.X + (column.Width - (17 + word)) / 2 + 17;
            top = _lanesTop + column.Y + Row + RowGap + (Row - _textHeight) / 2;
        }
        return (left - slotX, top - slotTop);
    }

    private string MetaText(bool main, int passes)
    {
        if (main)
        {
            if (passes != _metaCount)
            {
                _metaCount = passes;
                _metaText = _labels.PassCount(passes);
            }
            return _metaText;
        }
        if (passes != _compareCount)
        {
            _compareCount = passes;
            _compareText = _labels.PassCount(passes);
        }
        return _compareText;
    }

    // ---- palette ----------------------------------------------------------------------------------------------------

    private void RebuildPalette()
    {
        _paletteDirty = false;
        _text.Clear();
        _summaryKey = -1;
        _metaCount = -1;
        _compareCount = -1;
        var surface = ColorOf(SurfaceInk, Colors.White);
        var dark = Oklch.FromColor(surface).L < 0.5;
        _draft = ColorOf(DraftInk, Color.Parse("#7128A5"));
        if (dark)
        {
            // The draft color (series 2) is deep in the dark theme; lift it to the lightness the lanes need there.
            var (l, c, h) = Oklch.FromColor(_draft);
            _draft = Oklch.ToColor(Math.Max(l, 0.658), Math.Min(c, 0.16), h);
        }
        _target = ColorOf(TargetInk, Color.Parse("#1F7DCF"));
        _fg = ColorOf(ForegroundInk, Colors.Black);
        _secondary = ColorOf(SecondaryInk, Colors.Gray);
        _muted = ColorOf(MutedInk, Colors.Gray);
        _ghost = ColorOf(GhostInk, _muted);
        var surfaceMuted = ColorOf(SurfaceMutedInk, Colors.WhiteSmoke);
        var border = ColorOf(BorderInk, Colors.LightGray);
        var borderStrong = ColorOf(BorderStrongInk, Colors.Gray);
        var success = ColorOf(SuccessInk, Colors.Green);
        var danger = ColorOf(DangerInk, Colors.Red);

        _ghostFill = Solid(Oklch.WithAlpha(_draft, 0.06));
        _acceptedFill = Solid(Oklch.MixOklab(surface, _draft, 0.11));
        _rejectedFill = Solid(Oklch.WithAlpha(danger, 0.06));
        _targetFill = Solid(Oklch.WithAlpha(_target, 0.16));
        _discardFill = Solid(Oklch.WithAlpha(_muted, 0.10));
        _bonusFill = Solid(Oklch.MixOklab(surface, _target, 0.16));
        _cellFill = Solid(surfaceMuted);
        _ruleFill = Solid(border);
        _strikeFill = Solid(danger);
        _draftDot = Solid(_draft);
        _targetDot = Solid(_target);
        _draftUnderline = Solid(Oklch.WithAlpha(_draft, 0.6));
        _targetUnderline = Solid(Oklch.WithAlpha(_target, 0.7));
        var dashes = new ImmutableDashStyle([3, 2], 0);
        _ghostPen = new ImmutablePen(Solid(Oklch.WithAlpha(_draft, 0.6)), 1, dashes);
        _acceptedPen = new ImmutablePen(Solid(Oklch.WithAlpha(_draft, 0.42)), 1);
        _rejectedPen = new ImmutablePen(Solid(Oklch.WithAlpha(danger, 0.55)), 1, dashes);
        _slotPen = new ImmutablePen(Solid(borderStrong), 1, dashes);
        _checkPen = IconPen(success, 2, 14);
        _crossPen = IconPen(danger, 2, 13);
        _plusPen = IconPen(_target, 2, 13);
        _metaCheckPen = IconPen(success, 2, 13);
    }

    private static ImmutableSolidColorBrush Solid(Color color) => new(color);

    // Icons are 24-unit Lucide paths scaled to their size, so the stroke is given in icon units.
    private static ImmutablePen IconPen(Color color, double stroke, double size)
        => new(Solid(color), stroke * 24 / size, null, PenLineCap.Round, PenLineJoin.Round);

    private static void DrawIcon(DrawingContext context, Geometry icon, IPen? pen, double x, double y, double size)
    {
        var scale = size / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(null, pen, icon);
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback)
        => brush is ISolidColorBrush solid ? Oklch.WithAlpha(solid.Color, solid.Color.A / 255.0 * solid.Opacity) : fallback;

    private static double Eased(Easing easing, double p) => p <= 0 ? 0 : p >= 1 ? 1 : easing.Ease(p);

    // The shimmer's profile across its band: 0, ½, 0.9, 1, 0.9, ½, 0 at sixths.
    private static double Bell(double p)
    {
        ReadOnlySpan<double> stops = [0, 0.5, 0.9, 1, 0.9, 0.5, 0];
        var x = Math.Clamp(p, 0, 1) * 6;
        var i = Math.Min(5, (int)x);
        return stops[i] + (stops[i + 1] - stops[i]) * (x - i);
    }
}

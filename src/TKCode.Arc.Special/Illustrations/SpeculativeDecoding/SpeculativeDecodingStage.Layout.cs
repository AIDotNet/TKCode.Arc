using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal sealed partial class SpeculativeDecodingStage
{
    private enum CellKind
    {
        Slot,
        Accepted,
        Rejected,
        Discarded,
        Bonus
    }

    /// <summary>One column of the track: a chip over a cell (a bonus column has only its cell), at its wrapped place.</summary>
    private sealed class Column
    {
        public double X;
        public double Y;
        public double Width;
        public string Chip = string.Empty;
        public double ChipWidth;
        public string Cell = string.Empty;
        public double CellWidth;
        public CellKind Kind;
        public int Rows;
    }

    private bool _layoutDirty = true;
    private double _layoutWidth = -1;
    private Column[][] _rounds = [];
    private Column[] _slots = [];
    private string[] _tokText = [];
    private double[] _tokX = [];
    private double[] _tokW = [];
    private int[] _tokLine = [];
    private int _lines = 1;
    private double _trackX;
    private double _trackWidth;
    private double _lanesTop;
    private double _legendHeight;
    private double _legendTargetX;
    private double _legendTargetY;
    private double _textX;
    private double _textWidth;
    private double _textHeight = 17;
    private double _smallHeight = 14.5;
    private (double At, double Height)[] _heights = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? availableSize.Width
            : _layoutWidth > 0 ? _layoutWidth : 560;
        EnsureLayout(width);
        _measuredHeight = ContentHeight();
        return new Size(width, _measuredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureLayout(finalSize.Width);
        return finalSize;
    }

    /// <summary>The height of the lanes' track for <paramref name="rows"/> rows of columns.</summary>
    internal static double RowsHeight(int rows) => Math.Max(1, rows) * LaneBlock + (Math.Max(1, rows) - 1) * WrapGap;

    /// <summary>The stage's height now: lanes (with the legend), the rule and the output lines.</summary>
    internal double ContentHeight()
    {
        if (_layoutDirty || _layoutWidth < 0)
        {
            return _measuredHeight < 0 ? 0 : _measuredHeight;
        }
        var section = _isNarrow ? NarrowSection : Section;
        var lanes = _lanesTop + TrackHeightAt(_time);
        var line = LineBlockHeight;
        return lanes + section + 1 + section + line + _compareOpen * (LineGap + line);
    }

    /// <summary>The y of the rule between the lanes and the output lines.</summary>
    private double RuleY => _lanesTop + TrackHeightAt(_time) + (_isNarrow ? NarrowSection : Section);

    private double OutputsTop => RuleY + 1 + (_isNarrow ? NarrowSection : Section);

    private double LineBlockHeight => _isNarrow
        ? NameLine + NarrowLabelGap + _lines * TextLine
        : Math.Max(_lines * TextLine, 3 + NameLine + 1 + MetaLine);

    private double LineTextTop(double lineTop) => _isNarrow ? lineTop + NameLine + NarrowLabelGap : lineTop;

    private void EnsureLayout(double width)
    {
        if (!_layoutDirty && Math.Abs(width - _layoutWidth) < 0.01)
        {
            return;
        }
        _layoutDirty = false;
        _layoutWidth = width;
        _text.SetFontFamily(FontFamily);
        _textHeight = _text.Get("Ag", Body, FontWeight.Normal, Colors.Black).Height;
        _smallHeight = _text.Get("Ag", Small, FontWeight.Normal, Colors.Black).Height;

        _trackX = _isNarrow ? 0 : LabelColumn + LabelGap;
        _trackWidth = Math.Max(ColumnMin, width - _trackX);
        _textX = _trackX;
        _textWidth = _trackWidth;

        // The narrow legend: dot, name and detail per lane, wrapping to a second row when both do not fit.
        if (_isNarrow)
        {
            var first = LegendItemWidth(_draftName, _draftDetail);
            var second = LegendItemWidth(_targetName, _targetDetail);
            if (first + LegendItemGap + second <= width)
            {
                _legendTargetX = first + LegendItemGap;
                _legendTargetY = 0;
                _legendHeight = LegendRow;
            }
            else
            {
                _legendTargetX = 0;
                _legendTargetY = LegendRow + LegendRowGap;
                _legendHeight = LegendRow * 2 + LegendRowGap;
            }
            _lanesTop = _legendHeight + LegendGap;
        }
        else
        {
            _legendHeight = 0;
            _lanesTop = 0;
        }

        _slots = new Column[_slotCount];
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new Column { Width = SlotWidth, Kind = CellKind.Slot };
        }
        Wrap(_slots);

        var plan = _timeline?.Plan ?? DecodePlan.Empty;
        _rounds = new Column[plan.Rounds.Count][];
        for (var r = 0; r < _rounds.Length; r++)
        {
            _rounds[r] = BuildColumns(plan.Rounds[r]);
        }

        LayoutAnswer(plan);
        BuildHeights();
    }

    private double LegendItemWidth(string name, string detail)
        => 10 + LegendInner + _text.Width(name, Body, FontWeight.Medium) + (detail.Length > 0 ? LegendInner + _text.Width(detail, Small, FontWeight.Normal) : 0);

    private Column[] BuildColumns(DecodePlanRound round)
    {
        var k = round.Count;
        var columns = new Column[k + (round.IsBonus ? 1 : 0)];
        var target = Display(round.Target);
        for (var j = 0; j < k; j++)
        {
            var kind = j < round.Accepted ? CellKind.Accepted : j == round.Accepted ? CellKind.Rejected : CellKind.Discarded;
            var chip = Display(round.Drafts[j]);
            var column = new Column { Chip = chip, ChipWidth = _text.Width(chip, Body, FontWeight.Normal), Kind = kind };
            var content = 0.0;
            if (kind == CellKind.Accepted)
            {
                content = 14;
            }
            else if (kind == CellKind.Rejected)
            {
                column.Cell = target;
                column.CellWidth = _text.Width(target, Body, FontWeight.Normal);
                content = 13 + 4 + column.CellWidth;
            }
            column.Width = Math.Min(_trackWidth, Math.Max(ColumnMin, Math.Max(column.ChipWidth, content) + 2 * ChipInset));
            columns[j] = column;
        }
        if (round.IsBonus)
        {
            var width = _text.Width(target, Body, FontWeight.Normal);
            columns[k] = new Column
            {
                Cell = target,
                CellWidth = width,
                Kind = CellKind.Bonus,
                Width = Math.Min(_trackWidth, Math.Max(ColumnMin, 13 + 4 + width + 2 * ChipInset))
            };
        }
        Wrap(columns);
        return columns;
    }

    // Columns flow left to right and wrap onto another row (8 below) when the next one does not fit.
    private void Wrap(Column[] columns)
    {
        double x = 0, y = 0;
        var rows = 1;
        foreach (var column in columns)
        {
            if (x > 0 && x + column.Width > _trackWidth + 0.01)
            {
                x = 0;
                y += LaneBlock + WrapGap;
                rows++;
            }
            column.X = x;
            column.Y = y;
            column.Rows = rows;
            x += column.Width + ColumnGap;
        }
    }

    // The whole answer is laid out once: tokens keep their leading space, a token without one joins the word before it,
    // and words wrap as units, so punctuation never starts a line and nothing moves when tokens land.
    private void LayoutAnswer(DecodePlan plan)
    {
        var count = plan.Tokens.Count;
        _tokText = new string[count];
        _tokX = new double[count];
        _tokW = new double[count];
        _tokLine = new int[count];
        var space = _text.Width(" ", Body, FontWeight.Normal);
        for (var q = 0; q < count; q++)
        {
            _tokText[q] = Display(plan.Tokens[q]);
            _tokW[q] = _text.Width(_tokText[q], Body, FontWeight.Normal);
        }
        double x = 0;
        var line = 0;
        var i = 0;
        while (i < count)
        {
            var end = i + 1;
            var word = _tokW[i];
            while (end < count && !StartsWithSpace(plan.Tokens[end]))
            {
                word += _tokW[end];
                end++;
            }
            var lead = LeadingSpaces(plan.Tokens[i]) * space;
            if (x > 0 && x + lead + word > _textWidth + 0.01)
            {
                line++;
                x = 0;
            }
            if (x > 0)
            {
                x += lead;
            }
            for (var q = i; q < end; q++)
            {
                _tokX[q] = x;
                _tokLine[q] = line;
                x += _tokW[q];
            }
            i = end;
        }
        _lines = count == 0 ? 1 : line + 1;
    }

    // The track's height steps at each column that wraps, at the bonus column and at each new round (its columns are
    // cleared), and rests on two rows for the summary at the end; the spring between steps is evaluated in BuildHeights.
    private void BuildHeights()
    {
        var timeline = _timeline;
        if (timeline is null || timeline.Count == 0)
        {
            _heights = [];
            return;
        }
        var list = new List<(double, double)>();
        void Add(double at, double height)
        {
            if (list.Count == 0 || Math.Abs(list[^1].Item2 - height) > 0.01)
            {
                list.Add((at, height));
            }
        }
        for (var r = 0; r < timeline.Count; r++)
        {
            var columns = _rounds[r];
            var k = timeline.Plan.Rounds[r].Count;
            Add(timeline.Start(r), RowsHeight(1));
            for (var j = 0; j < k; j++)
            {
                Add(timeline.ChipTime(r, j), RowsHeight(columns[j].Rows));
            }
            if (columns.Length > k)
            {
                Add(timeline.SweepEnd(r), RowsHeight(columns[k].Rows));
            }
        }
        Add(timeline.MainEnd, RowsHeight(1));
        _heights = list.ToArray();
    }

    /// <summary>The track's height at story time <paramref name="t"/>, following its steps on the smooth spring (full motion).</summary>
    private double TrackHeightAt(double t)
    {
        if (_showsSlots || _heights.Length == 0)
        {
            return _showsSlots && _slots.Length > 0 ? RowsHeight(_slots[^1].Rows) : LaneBlock;
        }
        var target = _heights[0].Height;
        if (!_isTravel)
        {
            foreach (var (at, height) in _heights)
            {
                if (at > t)
                {
                    break;
                }
                target = height;
            }
            return target;
        }
        // The spring is a function of time: carry its value and velocity from step to step up to t.
        var spec = ArcMotionPresets.Smooth;
        double value = target, velocity = 0, since = _heights[0].At;
        for (var i = 1; i < _heights.Length; i++)
        {
            var (at, height) = _heights[i];
            if (at > t)
            {
                break;
            }
            var (x, v) = SpringSolver.Evaluate(spec, value - target, velocity, at - since);
            value = target + x;
            velocity = v;
            target = height;
            since = at;
        }
        var (dx, _) = SpringSolver.Evaluate(spec, value - target, velocity, Math.Max(0, t - since));
        var result = target + dx;
        return Math.Abs(result - target) < 0.05 ? target : result;
    }

    private static bool StartsWithSpace(string text) => text.Length > 0 && char.IsWhiteSpace(text[0]);

    private static int LeadingSpaces(string text)
    {
        var n = 0;
        while (n < text.Length && char.IsWhiteSpace(text[n]))
        {
            n++;
        }
        return n;
    }

    /// <summary>A token as a chip or the answer shows it: without its leading and trailing spaces.</summary>
    internal static string Display(string text) => text.Trim();
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

// Targets and motion. Every moving number of the bar (edges, dimming, fills, label widths, lane labels, the lane, the
// readout lines and the refusal shake) is a SplitBarValue on the bar's one LiveLoop: a control theme cannot express
// shares that retarget mid-flight from their current speed, nor geometry that the track draws itself.
public partial class SplitBar
{
    /// <summary>The spring every edge, lane label and digit rides (Arc Split bar, design reference: k 250, c 30).</summary>
    internal static readonly SpringSpec EdgeSpring = SpringSpec.FromPhysical(250, 30);

    /// <summary>The spring a label's name opens and closes on.</summary>
    internal static readonly SpringSpec NameSpring = SpringSpec.FromPhysical(300, 35);

    /// <summary>The spring the label lane under the bar opens and closes on.</summary>
    internal static readonly SpringSpec LaneSpring = SpringSpec.FromPhysical(250, 32);

    /// <summary>The digits of shares and values roll on the edge spring.</summary>
    internal static readonly MotionSpec DigitMotion = MotionSpec.Spring(EdgeSpring);

    private const double DimSeconds = 0.2;
    private const double FillSeconds = 0.15;
    private const double FadeSeconds = 0.2;
    private const double ShakeSeconds = 0.32;
    private static readonly double[] s_shake = [0, -3, 3, -2, 0];

    private SplitBarValue _lane;
    private int _laneTarget;

    /// <summary>How tall the label lane is now (px; 0 when every label fits inside its part).</summary>
    internal double LaneHeight => Math.Max(0, _lane.Value(_wall));

    /// <summary>True while anything is still moving.</summary>
    internal bool IsMoving
    {
        get
        {
            if (_lane.IsMoving(_wall) || IsReadoutMoving)
            {
                return true;
            }
            foreach (var slot in _slots)
            {
                if (slot.Share.IsMoving(_wall) || slot.Dim.IsMoving(_wall) || slot.Fill.IsMoving(_wall) ||
                    slot.NameOpen.IsMoving(_wall) || slot.Inside.IsMoving(_wall) || slot.Outside.IsMoving(_wall) ||
                    slot.OutsideX.IsMoving(_wall) || slot.IsShaking)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Works out every part's share and label, and points the springs at them.</summary>
    private void Retarget()
    {
        double sum = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsLeaving && !slot.Segment.IsHidden)
            {
                sum += Math.Max(0, slot.Segment.Value);
            }
        }
        _sum = sum;
        var total = Total;
        _scale = double.IsFinite(total) && total > sum ? total : sum;
        _remainder = _scale > 0 ? Math.Max(0, 1 - sum / _scale) : 0;
        foreach (var slot in _slots)
        {
            slot.BaseShare = slot.IsLeaving ? 0 : ShareOf(slot.Segment);
            slot.TargetShare = slot.BaseShare;
        }
        var highlighted = HighlightedSlot();
        var width = _track?.BarWidth ?? 0;
        if (highlighted is not null && width > 0 && _track is { } track && highlighted.BaseShare < 1)
        {
            // The highlighted part gains a little of the bar, or enough to show its own name and share; the others give
            // way in proportion, so the bar stays at 100%.
            var label = MeasureName(highlighted) + track.LabelNameGap + MeasureShare(highlighted.BaseShare);
            var fit = (label + track.LabelFitPadding + 2 * track.SegmentGap + 1) / width;
            var grown = Math.Min(Math.Max(highlighted.BaseShare + track.HighlightGrow, fit), 1);
            var rest = (1 - grown) / (1 - highlighted.BaseShare);
            foreach (var slot in _slots)
            {
                slot.TargetShare = ReferenceEquals(slot, highlighted) ? grown : slot.BaseShare * rest;
            }
        }
        var animate = CanAnimate;
        PlaceLabels(width, animate);
        foreach (var slot in _slots)
        {
            if (animate)
            {
                slot.Share.Spring(slot.TargetShare, _wall, EdgeSpring);
            }
            else
            {
                slot.Share.Jump(slot.TargetShare);
            }
        }
        if (!animate && _slots.RemoveAll(s => s.IsLeaving && Release(s)) > 0)
        {
            RebuildLegend();
        }
        _primed = _primed || _attached;
        PseudoClasses.Set(EmptyPseudoClass, sum <= 0);
        var count = Current.Count;
        if (HighlightedIndex >= count)
        {
            SetCurrentValue(HighlightedIndexProperty, -1);
        }
        UpdateTexts();
        UpdateDimming(animate);
        Kick();
    }

    private bool Release(SplitBarSlot slot)
    {
        _track?.Release(slot);
        return true;
    }

    private SplitBarSlot? HighlightedSlot()
    {
        var slot = SlotAt(HighlightedIndex);
        return slot is { Segment.IsHidden: false } ? slot : null;
    }

    /// <summary>Decides, for the target widths, which labels fit inside their part and where the lane labels go.</summary>
    private void PlaceLabels(double width, bool animate)
    {
        var track = _track;
        var anyLane = false;
        if (track is null || width <= 0)
        {
            foreach (var slot in _slots)
            {
                slot.Mode = slot.IsLeaving || slot.Segment.IsHidden ? SplitBarLabelMode.None : SplitBarLabelMode.Share;
                SetLabelValues(slot, animate);
            }
            return;
        }
        var gap = track.SegmentGap;
        var start = 0.0;
        var last = _slots.Count - 1;
        var lane = new List<(SplitBarSlot Slot, double Center, double Width)>();
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var w = slot.TargetShare * width;
            if (slot.IsLeaving || slot.Segment.IsHidden || slot.BaseShare <= 0)
            {
                slot.Mode = SplitBarLabelMode.None;
            }
            else
            {
                var visible = w - (i == 0 ? 0 : gap / 2) - (i == last ? 0 : gap / 2);
                var share = MeasureShare(slot.BaseShare);
                var name = MeasureName(slot);
                var padding = track.LabelFitPadding + (i == last ? track.LastLabelExtra : 0);
                if (visible >= name + track.LabelNameGap + share + padding)
                {
                    slot.Mode = SplitBarLabelMode.Full;
                }
                else if (visible >= share + track.LabelFitPadding)
                {
                    slot.Mode = SplitBarLabelMode.Share;
                }
                else
                {
                    slot.Mode = SplitBarLabelMode.Lane;
                    lane.Add((slot, (start + w / 2), name + track.LaneNameGap + share));
                }
            }
            start += w;
        }
        // Lane labels: centred under their part, pushed apart so they never overlap, kept inside the bar.
        var count = lane.Count;
        if (count > 0)
        {
            anyLane = true;
            var xs = new double[count];
            var right = width - track.LaneEdgeInset;
            var labelGap = track.LaneLabelGap;
            if (count > 1)
            {
                // On a narrow bar the labels close up (down to a quarter of the gap) before they run out of room.
                double widths = 0;
                foreach (var entry in lane)
                {
                    widths += entry.Width;
                }
                labelGap = Math.Clamp((right - widths) / (count - 1), labelGap / 4, labelGap);
            }
            for (var i = 0; i < count; i++)
            {
                var x = lane[i].Center - lane[i].Width / 2;
                if (i > 0)
                {
                    x = Math.Max(x, xs[i - 1] + lane[i - 1].Width + labelGap);
                }
                xs[i] = Math.Max(0, x);
            }
            for (var i = count - 1; i >= 0; i--)
            {
                var limit = i == count - 1 ? right - lane[i].Width : xs[i + 1] - labelGap - lane[i].Width;
                xs[i] = Math.Min(xs[i], limit);
            }
            for (var i = 0; i < count; i++)
            {
                xs[i] = Math.Max(xs[i], i == 0 ? 0 : xs[i - 1] + lane[i - 1].Width + labelGap);
            }
            for (var i = 0; i < count; i++)
            {
                var slot = lane[i].Slot;
                if (!slot.IsOutside || !animate)
                {
                    slot.OutsideX.Jump(xs[i]);
                }
                else
                {
                    slot.OutsideX.Spring(xs[i], _wall, EdgeSpring);
                }
            }
        }
        foreach (var slot in _slots)
        {
            SetLabelValues(slot, animate);
        }
        var laneTarget = anyLane ? (int)Math.Round(track.LaneHeight) : 0;
        if (laneTarget != _laneTarget || !animate)
        {
            _laneTarget = laneTarget;
            if (animate)
            {
                _lane.Spring(laneTarget, _wall, LaneSpring);
            }
            else
            {
                _lane.Jump(laneTarget);
            }
            track.InvalidateMeasure();
        }
    }

    private void SetLabelValues(SplitBarSlot slot, bool animate)
    {
        var name = slot.Mode == SplitBarLabelMode.Full ? 1 : 0;
        var inside = slot.Mode is SplitBarLabelMode.Full or SplitBarLabelMode.Share ? 1 : 0;
        var outside = slot.Mode == SplitBarLabelMode.Lane ? 1 : 0;
        slot.IsOutside = outside > 0;
        if (animate)
        {
            if (slot.NameOpen.Target != name)
            {
                slot.NameOpen.Spring(name, _wall, NameSpring);
            }
            if (slot.Inside.Target != inside)
            {
                slot.Inside.Tween(inside, _wall, FadeSeconds, ArcEasings.Standard);
            }
            if (slot.Outside.Target != outside)
            {
                slot.Outside.Tween(outside, _wall, FadeSeconds, ArcEasings.Standard);
            }
        }
        else
        {
            slot.NameOpen.Jump(name);
            slot.Inside.Jump(inside);
            slot.Outside.Jump(outside);
        }
    }

    /// <summary>Dims every part but the highlighted one and lights the highlighted fill.</summary>
    private void UpdateDimming(bool animate)
    {
        var highlighted = HighlightedSlot();
        foreach (var slot in _slots)
        {
            var dim = highlighted is not null && !ReferenceEquals(slot, highlighted) ? 1 : 0;
            var fill = ReferenceEquals(slot, highlighted) ? 1 : 0;
            if (animate)
            {
                if (slot.Dim.Target != dim)
                {
                    slot.Dim.Tween(dim, _wall, DimSeconds, ArcEasings.Standard);
                }
                if (slot.Fill.Target != fill)
                {
                    slot.Fill.Tween(fill, _wall, FillSeconds, ArcEasings.Standard);
                }
            }
            else
            {
                slot.Dim.Jump(dim);
                slot.Fill.Jump(fill);
            }
            slot.LegendItem?.SetHighlighted(fill > 0);
        }
    }

    private void UpdateHighlight()
    {
        PseudoClasses.Set(HighlightedPseudoClass, HighlightedSlot() is not null);
        Retarget();
        if (CanAnimate)
        {
            _loop.Start();
        }
    }

    private bool OnFrame(double dt)
    {
        _wall += dt;
        if (!ArcMotion.IsFull(this))
        {
            SettleAll();
        }
        foreach (var slot in _slots)
        {
            if (slot.IsShaking)
            {
                var t = (_wall - slot.ShakeStart) / ShakeSeconds;
                if (t >= 1)
                {
                    slot.IsShaking = false;
                    slot.LegendItem?.SetShake(0);
                }
                else
                {
                    var scaled = Math.Max(0, t) * (s_shake.Length - 1);
                    var k = Math.Min((int)scaled, s_shake.Length - 2);
                    var p = ArcEasings.InOut.Ease(scaled - k);
                    slot.LegendItem?.SetShake(s_shake[k] + (s_shake[k + 1] - s_shake[k]) * p);
                }
            }
        }
        var moving = IsMoving;
        if (!moving && _slots.RemoveAll(s => s.IsLeaving && Release(s)) > 0)
        {
            UpdateTrackName();
        }
        AnimateReadout();
        _track?.Refresh();
        return moving;
    }

    private void SettleAll()
    {
        foreach (var slot in _slots)
        {
            slot.Share.Jump(slot.Share.Target);
            slot.Dim.Jump(slot.Dim.Target);
            slot.Fill.Jump(slot.Fill.Target);
            slot.NameOpen.Jump(slot.NameOpen.Target);
            slot.Inside.Jump(slot.Inside.Target);
            slot.Outside.Jump(slot.Outside.Target);
            slot.OutsideX.Jump(slot.OutsideX.Target);
            if (slot.IsShaking)
            {
                slot.IsShaking = false;
                slot.LegendItem?.SetShake(0);
            }
        }
        _lane.Jump(_lane.Target);
        SettleReadout();
    }

    /// <summary>Width of a part's name at the label size (cached per label text).</summary>
    internal double MeasureName(SplitBarSlot slot)
    {
        var label = slot.Segment.Label ?? string.Empty;
        if (double.IsNaN(slot.NameWidth) || !string.Equals(slot.MeasuredLabel, label, StringComparison.Ordinal))
        {
            slot.NameWidth = Measure(label, FontWeight.Normal);
            slot.MeasuredLabel = label;
        }
        return slot.NameWidth;
    }

    /// <summary>Width of a share as the labels write it ("41.6%", tabular figures, medium weight).</summary>
    internal double MeasureShare(double share) => Measure(FormatShare(share), FontWeight.Medium);

    private double Measure(string text, FontWeight weight)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var size = _track?.LabelFontSize ?? 12;
        using var layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, weight), size, null,
            TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection.LeftToRight, double.PositiveInfinity,
            double.PositiveInfinity, double.NaN, 0, 1, SplitBarText.Tabular, null);
        return layout.WidthIncludingTrailingWhitespace;
    }

    /// <summary>The track's width changed: labels are fitted again. The first width shows at once.</summary>
    internal void OnBarWidthChanged(bool first)
    {
        if (_slots.Count == 0)
        {
            return;
        }
        _settling = first;
        try
        {
            Retarget();
        }
        finally
        {
            _settling = false;
        }
    }
}

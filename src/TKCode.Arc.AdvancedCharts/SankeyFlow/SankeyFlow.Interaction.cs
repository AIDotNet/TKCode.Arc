using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class SankeyFlow
{
    private const double TipGap = 14;
    private const int MaxSpokenDestinations = 5;

    /// <summary>What a trace starts from: a node index, or a link index when <see cref="Node"/> is negative.</summary>
    internal readonly record struct TraceTarget(int Node, int Link);

    private TraceTarget? _pointerTarget;
    private Point? _pointerAt;
    private int _keyNode = -1;
    private bool _focusVisible;
    private TraceTarget? _traced;
    private SankeyFlowTrace? _trace;
    private Point _pressedAt;
    private ChartsBAutomationPeer? _peer;
    private string? _tipSignature;

    /// <summary>The traced node or ribbon, keyboard or pointer.</summary>
    internal TraceTarget? Traced => _traced;

    /// <summary>The share being traced, or null at rest.</summary>
    internal SankeyFlowTrace? ActiveTrace => _traced is null ? null : _trace;

    /// <summary>The node the keyboard is on, or -1.</summary>
    internal int KeyNode => _keyNode;

    private TraceTarget? Effective => _pointerTarget ?? (_keyNode >= 0 ? new TraceTarget(_keyNode, -1) : null);

    private void ApplyTrace()
    {
        var next = Effective;
        if (next == _traced)
        {
            SyncTooltip();
            return;
        }
        _traced = next;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        if (next is { } target)
        {
            _trace = target.Node >= 0 ? _model.TraceNode(target.Node) : _model.TraceLink(target.Link);
            if (full)
            {
                // The traced share pours outward from what was picked.
                _pour.Jump(0);
                _ = _pour.AnimateTo(1, MotionSpec.Spring(ChartsBMath.Physical(0.6, 0)));
            }
            else
            {
                _pour.Jump(1);
            }
        }
        if (full)
        {
            _ = _dim.AnimateTo(next is null ? 0 : 1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _dim.Jump(next is null ? 0 : 1);
        }
        _traceStamp++;
        SyncTooltip();
        UpdateAccessibility();
        Repaint();
        _particleLayer?.InvalidateVisual();
    }

    private void SetPointerTarget(TraceTarget? target, Point? at)
    {
        _pointerAt = at;
        if (_pointerTarget != target)
        {
            _pointerTarget = target;
            ApplyTrace();
        }
        else
        {
            SyncTooltip();
        }
    }

    private void SetKeyNode(int node)
    {
        if (_keyNode != node)
        {
            _keyNode = node;
            ApplyTrace();
        }
    }

    // ---- flow coordinates ---------------------------------------------------------------------------------------

    private Point At(double main, double cross) => _upright ? new Point(cross, main) : new Point(main, cross);

    private Rect RectOf(in SankeyNodePose pose)
        => _upright ? new Rect(pose.Cross, pose.Main, pose.Extent, pose.Thickness) : new Rect(pose.Main, pose.Cross, pose.Thickness, pose.Extent);

    /// <summary>The node or ribbon under a point; a node wins over a ribbon, and the thinnest ribbon wins over a thicker one.</summary>
    internal TraceTarget? Pick(Point point)
    {
        if (_model.IsEmpty || _placement is null)
        {
            return null;
        }
        var main = _upright ? point.Y : point.X;
        var cross = _upright ? point.X : point.Y;
        for (var i = 0; i < _model.Nodes.Count; i++)
        {
            var pose = _nodeItems[i].Drawn;
            var slack = Math.Max(0, (8 - pose.Extent) / 2);
            if (main >= pose.Main - 4 && main <= pose.Main + pose.Thickness + 4 && cross >= pose.Cross - slack && cross <= pose.Cross + pose.Extent + slack)
            {
                return new TraceTarget(i, -1);
            }
        }
        var best = -1;
        var bestThick = double.PositiveInfinity;
        for (var i = 0; i < _model.Links.Count; i++)
        {
            var pose = _linkItems[i].Drawn;
            if (pose.Thick < bestThick && OnRibbon(pose, main, cross))
            {
                best = i;
                bestThick = pose.Thick;
            }
        }
        return best >= 0 ? new TraceTarget(-1, best) : null;
    }

    private static bool OnRibbon(in SankeyLinkPose pose, double main, double cross)
    {
        var half = Math.Max(pose.Thick / 2, 3);
        if (pose.M1 >= pose.M0)
        {
            if (main < pose.M0 || main > pose.M1)
            {
                return false;
            }
            // The main coordinate rises along the curve: bisect for it.
            double lo = 0, hi = 1;
            for (var i = 0; i < 12; i++)
            {
                var mid = (lo + hi) / 2;
                if (pose.MainAt(mid) < main)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return Math.Abs(pose.CrossAt((lo + hi) / 2) - cross) <= half;
        }
        // A ribbon that loops back: walk its samples.
        for (var i = 0; i <= 24; i++)
        {
            var t = i / 24.0;
            if (Math.Abs(pose.MainAt(t) - main) <= half && Math.Abs(pose.CrossAt(t) - cross) <= half)
            {
                return true;
            }
        }
        return false;
    }

    // ---- pointer ------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            var point = e.GetPosition(this);
            SetPointerTarget(Pick(point), point);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetPointerTarget(null, null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressedAt = e.GetPosition(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            return;
        }
        var point = e.GetPosition(this);
        var moved = point - _pressedAt;
        if (Math.Abs(moved.X) <= 10 && Math.Abs(moved.Y) <= 10)
        {
            Tap(point);
        }
    }

    /// <summary>A touch tap: traces what is under it; tapping the same thing, or nothing, clears the trace.</summary>
    internal void Tap(Point point)
    {
        var target = Pick(point);
        SetPointerTarget(target == _pointerTarget ? null : target, point);
    }

    // ---- keyboard -----------------------------------------------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _focusVisible = true;
            if (_keyNode < 0)
            {
                SetKeyNode(FirstNodeOf(0, 1));
            }
            Repaint();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        SetKeyNode(-1);
        Repaint();
    }

    /// <summary>The first node of the first column with nodes, searching from a column in a direction.</summary>
    private int FirstNodeOf(int column, int direction)
    {
        if (_placement is null)
        {
            return -1;
        }
        var columns = _placement.Columns;
        for (var c = column; c >= 0 && c < columns.Length; c += direction)
        {
            if (columns[c].Length > 0)
            {
                return columns[c][0];
            }
        }
        return -1;
    }

    /// <summary>Arrows walk a column and step between columns (rows when upright); Home and End jump to the first and last; Escape clears.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _model.IsEmpty)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Up:
                Move(e, _upright ? -1 : 0, _upright ? 0 : -1);
                break;
            case Key.Down:
                Move(e, _upright ? 1 : 0, _upright ? 0 : 1);
                break;
            case Key.Left:
                Move(e, _upright ? 0 : -1, _upright ? -1 : 0);
                break;
            case Key.Right:
                Move(e, _upright ? 0 : 1, _upright ? 1 : 0);
                break;
            case Key.Home:
                e.Handled = true;
                _focusVisible = true;
                SetKeyNode(FirstNodeOf(0, 1));
                break;
            case Key.End:
                e.Handled = true;
                _focusVisible = true;
                SetKeyNode(FirstNodeOf(_model.ColumnCount - 1, -1));
                break;
            case Key.Escape when _keyNode >= 0 || _pointerTarget is not null:
                e.Handled = true;
                _pointerTarget = null;
                _keyNode = -1;
                ApplyTrace();
                break;
        }
    }

    private void Move(KeyEventArgs e, int columns, int rows)
    {
        e.Handled = true;
        _focusVisible = true;
        if (_placement is null)
        {
            return;
        }
        if (_keyNode < 0)
        {
            SetKeyNode(FirstNodeOf(0, 1));
            return;
        }
        var column = _model.Nodes[_keyNode].Column;
        var members = _placement.Columns[column];
        if (rows != 0)
        {
            var at = Array.IndexOf(members, _keyNode);
            SetKeyNode(members[Math.Clamp(at + rows, 0, members.Length - 1)]);
            return;
        }
        // The neighbouring column's node nearest across the flow.
        for (var c = column + columns; c >= 0 && c < _placement.Columns.Length; c += columns)
        {
            var next = _placement.Columns[c];
            if (next.Length == 0)
            {
                continue;
            }
            var center = _placement.Nodes[_keyNode].CrossCenter;
            var best = next[0];
            foreach (var candidate in next)
            {
                if (Math.Abs(_placement.Nodes[candidate].CrossCenter - center) < Math.Abs(_placement.Nodes[best].CrossCenter - center))
                {
                    best = candidate;
                }
            }
            SetKeyNode(best);
            return;
        }
    }

    // ---- text ---------------------------------------------------------------------------------------------------

    private string Format(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        return ChartsBMath.Grouped(value).Replace("-", "−") + Unit;
    }

    private string ShareOf(double value)
    {
        var share = _model.Total > 0 ? Math.Min(1, value / _model.Total) : 0;
        return ChartsBMath.Percent(share, 1);
    }

    /// <summary>The spoken reading of a node: value, share and destinations.</summary>
    internal string DescribeNode(int index)
    {
        var node = _model.Nodes[index];
        var text = new StringBuilder();
        text.Append(node.Label).Append(": ").Append(Format(node.Value)).Append(", ").Append(ShareOf(node.Value)).Append(" of ").Append(TotalLabel);
        if (node.OutLinks.Count == 0)
        {
            return text.Append(", no further destinations").ToString();
        }
        text.Append(", flows to ");
        var spoken = 0;
        foreach (var l in node.OutLinks.OrderByDescending(l => _model.Links[l].Value).ThenBy(l => l))
        {
            if (spoken == MaxSpokenDestinations)
            {
                text.Append(", and ").Append(node.OutLinks.Count - spoken).Append(" more");
                break;
            }
            if (spoken > 0)
            {
                text.Append(", ");
            }
            text.Append(_model.Nodes[_model.Links[l].Target].Label).Append(' ').Append(Format(_model.Links[l].Value));
            spoken++;
        }
        return text.ToString();
    }

    /// <summary>The spoken reading of a ribbon: its ends, value and share.</summary>
    internal string DescribeLink(int index)
    {
        var link = _model.Links[index];
        return $"{_model.Nodes[link.Source].Label} to {_model.Nodes[link.Target].Label}: {Format(link.Value)}, {ShareOf(link.Value)} of {TotalLabel}";
    }

    /// <summary>The reading of what is traced, or null at rest.</summary>
    internal string? Reading => _traced switch
    {
        { Node: >= 0 } target => DescribeNode(target.Node),
        { } target => DescribeLink(target.Link),
        _ => null
    };

    /// <summary>Accessible summary: the total and every node's value and share, or the empty message.</summary>
    public string Summary
    {
        get
        {
            EnsureModel();
            if (_model.IsEmpty)
            {
                return $"{Label}. {EmptyLabel}.";
            }
            var nodes = string.Join("; ", _model.Nodes.Take(24).Select(n => $"{n.Label} {Format(n.Value)}, {ShareOf(n.Value)}"));
            return $"{Label}. {_model.Nodes.Count} nodes and {_model.Links.Count} flows, {Format(_model.Total)} in all. {nodes}.";
        }
    }

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Reading ?? Label);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "flow chart";

    string IChartsBAccessible.ChartName => Reading ?? Label ?? string.Empty;

    string IChartsBAccessible.ChartHelp
        => $"{Reading ?? Summary} Tab highlights the first node, arrow keys move between nodes and columns, Home and End jump to the first and last column, Escape clears.";

    // ---- tooltip ------------------------------------------------------------------------------------------------

    private void SyncTooltip()
    {
        if (_traced is not { } target || _placement is null || Bounds.Width <= 0)
        {
            _tooltip.Hide();
            return;
        }
        var title = target.Node >= 0
            ? _model.Nodes[target.Node].Label
            : $"{_model.Nodes[_model.Links[target.Link].Source].Label} → {_model.Nodes[_model.Links[target.Link].Target].Label}";
        var amount = _trace?.Amount ?? 0;
        var signature = title + "|" + amount;
        if (signature != _tipSignature)
        {
            _tipSignature = signature;
            var p = _palette;
            var xs = p.TypeXS;
            _tooltip.SetRows(new[]
            {
                new ChartsBTipRow { Left = ChartsBText.Make(title, p, xs, p.TextSecondary), MinHeight = xs * 1.5 },
                new ChartsBTipRow
                {
                    MarginTop = 2, MinHeight = p.TypeSM * 1.4,
                    Left = ChartsBText.Make("Value", p, xs, p.TextMuted),
                    Right = ChartsBText.Make(Format(amount), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                },
                new ChartsBTipRow
                {
                    MinHeight = xs * 1.5,
                    Left = ChartsBText.Make($"{ShareOf(amount)} of {TotalLabel}", p, xs, p.TextMuted)
                }
            });
        }
        Point anchor;
        if (_pointerTarget is not null && _pointerAt is { } pointer)
        {
            anchor = pointer;
        }
        else if (target.Node >= 0)
        {
            var pose = _placement.Nodes[target.Node];
            anchor = At(pose.Main + pose.Thickness, pose.CrossCenter);
        }
        else
        {
            var pose = _placement.Links[target.Link];
            anchor = At(pose.MainAt(0.5), pose.CrossAt(0.5));
        }
        var size = _tooltip.Size;
        var width = Bounds.Width;
        var left = anchor.X + TipGap;
        if (left + size.Width > width)
        {
            left = anchor.X - TipGap - size.Width;
        }
        var top = anchor.Y - size.Height - TipGap;
        if (top < 0)
        {
            top = anchor.Y + TipGap;
        }
        _tooltip.Show(new Point(ChartsBMath.Clamp(left, 0, Math.Max(0, width - size.Width)), ChartsBMath.Clamp(top, 0, Math.Max(0, Bounds.Height - size.Height))));
    }
}

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || FromCardContent(e.Source))
        {
            return;
        }
        var card = CardOf(e.Source);
        var node = card?.Node;
        // Editing keys first: the move mode takes the arrow keys.
        if (HandleEditKey(e, node))
        {
            e.Handled = true;
            return;
        }
        var modifiers = e.KeyModifiers;
        var command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down when node is not null && !command && !shift:
                FocusNode(alt ? AlongEdges(node, e.Key) : Nearest(node, e.Key));
                // Moving off the edge of the workflow still uses up the key, so the page does not scroll.
                e.Handled = true;
                break;
            case Key.Home or Key.End when node is not null && !command && !alt:
            {
                var order = ReadingOrder();
                e.Handled = order.Count > 0 && FocusNode(e.Key == Key.Home ? order[0] : order[^1]);
                break;
            }
            case Key.Enter when node is not null && !command && !alt:
                RaiseEvent(new WorkflowNodeEventArgs(NodeActivatedEvent, node));
                e.Handled = true;
                break;
            case Key.Apps or Key.F10 when card is not null && (e.Key == Key.Apps || (shift && !command && !alt)):
                // The keyboard's context menu request, from the focused card (Avalonia raises it for right clicks only).
                card.RaiseEvent(new ContextRequestedEventArgs());
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add when IsPanZoomEnabled && !command:
                ZoomIn();
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract when IsPanZoomEnabled && !command:
                ZoomOut();
                e.Handled = true;
                break;
            case Key.D1 when IsPanZoomEnabled && shift && !command && !alt:
                FitToContent();
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0 when IsPanZoomEnabled && command:
                ZoomTo(1, ZoomAnchor());
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdatePorts();
        // A card reached with the keyboard is brought into view.
        if (IsPanZoomEnabled && e.NavigationMethod != NavigationMethod.Pointer && CardOf(e.Source) is { Node: { } node })
        {
            ScrollIntoView(node);
        }
    }

    /// <summary>
    /// True when <paramref name="source"/> is a control in a card's content that takes input itself (a text box, a
    /// switch): its keys and double clicks are its own.
    /// </summary>
    private bool FromCardContent(object? source)
    {
        if (source is not Visual visual || CardOf(visual) is not { } card || ReferenceEquals(visual, card))
        {
            return false;
        }
        for (var v = visual; v is not null && !ReferenceEquals(v, card); v = v.GetVisualParent())
        {
            if (v is InputElement { Focusable: true })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The card <paramref name="source"/> is, or is inside, when it is one of this canvas's cards.</summary>
    private WorkflowNodeCard? CardOf(object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is WorkflowNodeCard { Node: { } node } card && _cards.TryGetValue(node, out var own) && ReferenceEquals(own, card))
            {
                return card;
            }
        }
        return null;
    }

    private bool FocusNode(WorkflowNode? node)
    {
        if (node is null || !_cards.TryGetValue(node, out var card))
        {
            return false;
        }
        Realize(node);
        return card.Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// The nearest step in the arrow's direction: the candidates within 45° of it, scored by distance along the
    /// direction plus twice the distance across it; when none lies in the cone, the nearest one on that side.
    /// </summary>
    internal WorkflowNode? Nearest(WorkflowNode from, Key key)
    {
        var origin = DisplayRect(from).Center;
        WorkflowNode? best = null;
        var bestScore = double.MaxValue;
        var bestInCone = false;
        foreach (var node in _cards.Keys)
        {
            if (ReferenceEquals(node, from))
            {
                continue;
            }
            var d = DisplayRect(node).Center - origin;
            var (along, across) = key switch
            {
                Key.Left => (-d.X, Math.Abs(d.Y)),
                Key.Right => (d.X, Math.Abs(d.Y)),
                Key.Up => (-d.Y, Math.Abs(d.X)),
                _ => (d.Y, Math.Abs(d.X))
            };
            if (along <= 0.5)
            {
                continue;
            }
            var inCone = across <= along;
            var score = along + 2 * across;
            if ((inCone && !bestInCone) || (inCone == bestInCone && score < bestScore))
            {
                best = node;
                bestScore = score;
                bestInCone = inCone;
            }
        }
        return best;
    }

    /// <summary>
    /// Follows the connections: along the workflow's direction to the nearest next step, against it to the nearest
    /// previous one, and across it to the neighbouring step that shares a previous step (wrapping around).
    /// </summary>
    internal WorkflowNode? AlongEdges(WorkflowNode from, Key key)
    {
        if (Graph is not { } graph)
        {
            return null;
        }
        var vertical = LayoutDirection == WorkflowLayoutDirection.TopToBottom;
        var (forward, backward) = vertical ? (Key.Down, Key.Up) : (Key.Right, Key.Left);
        var center = DisplayRect(from).Center;
        double Across(WorkflowNode n) => vertical ? DisplayRect(n).Center.X : DisplayRect(n).Center.Y;
        double AcrossDistance(WorkflowNode n) => Math.Abs(Across(n) - (vertical ? center.X : center.Y));
        if (key == forward)
        {
            return graph.OutgoingEdges(from).Select(e => e.Target).Where(_cards.ContainsKey).OrderBy(AcrossDistance).FirstOrDefault();
        }
        if (key == backward)
        {
            return graph.IncomingEdges(from).Select(e => e.Source).Where(_cards.ContainsKey).OrderBy(AcrossDistance).FirstOrDefault();
        }
        var siblings = graph.IncomingEdges(from)
            .SelectMany(e => graph.OutgoingEdges(e.Source))
            .Select(e => e.Target)
            .Where(_cards.ContainsKey)
            .Distinct()
            .OrderBy(Across)
            .ToList();
        if (siblings.Count < 2)
        {
            return null;
        }
        var index = siblings.IndexOf(from);
        var step = key is Key.Right or Key.Down ? 1 : -1;
        return siblings[(index + step + siblings.Count) % siblings.Count];
    }

    /// <summary>The steps in reading order: by rank along the workflow's direction, then across it.</summary>
    internal List<WorkflowNode> ReadingOrder()
    {
        var vertical = LayoutDirection == WorkflowLayoutDirection.TopToBottom;
        return _cards.Keys
            .OrderBy(n => vertical ? DisplayPosition(n).Y : DisplayPosition(n).X)
            .ThenBy(n => vertical ? DisplayPosition(n).X : DisplayPosition(n).Y)
            .ToList();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WorkflowCanvasAutomationPeer(this);

    /// <summary>The canvas reads as a group of steps (Avalonia has no graph pattern).</summary>
    private sealed class WorkflowCanvasAutomationPeer(WorkflowCanvas owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

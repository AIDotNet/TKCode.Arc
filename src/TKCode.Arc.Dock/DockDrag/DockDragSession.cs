using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The stage of a dock drag (spec 9.1).</summary>
internal enum DockDragPhase
{
    /// <summary>The tab moves along its strip and the other tabs make room.</summary>
    Reorder,

    /// <summary>The pane (or group) is being docked: ghost, compass and preview.</summary>
    Dock,

    /// <summary>A floating window or card follows the pointer while the other hosts show their compass.</summary>
    Move
}

/// <summary>What the pointer is over during a dock drag.</summary>
/// <param name="Host">The host under the pointer; null outside every host.</param>
/// <param name="Target">Where a drop would dock; null when nothing accepts the drag here.</param>
/// <param name="NoOp">The pointer is over the drag's own place: a drop changes nothing (and does not float).</param>
/// <param name="Preview">The preview rectangle in the host layer's coordinates.</param>
/// <param name="Line">The insertion line in a tab strip, in the host layer's coordinates.</param>
/// <param name="Slot">The compass or edge button under the pointer.</param>
/// <param name="LayerPoint">The pointer in the host layer's coordinates.</param>
internal readonly record struct DockHit(IDockHost? Host, DockTarget? Target, bool NoOp, Rect? Preview, Rect? Line,
    DockCompassSlot? Slot, Point LayerPoint);

/// <summary>The state of the dock drag in progress.</summary>
internal sealed class DockDragSession
{
    public DockDragSession(DockDragRequest request, DockDragGesture gesture, TopLevel top, Point start)
    {
        Request = request;
        Gesture = gesture;
        Top = top;
        Start = start;
        Last = start;
    }

    public DockDragRequest Request { get; }

    public DockDragGesture Gesture { get; }

    /// <summary>The window the drag started in; pointer positions are in its coordinates.</summary>
    public TopLevel Top { get; }

    public Point Start { get; }

    public Point Last { get; set; }

    public PixelPoint StartScreen { get; set; }

    public DockDragPhase Phase { get; set; }

    public IReadOnlyList<DockPane> Panes => Request.Panes;

    public DockPane Lead => Request.Lead;

    public DockGroup SourceGroup => Request.SourceGroup;

    public bool IsGroupDrag => Request.IsGroupDrag;

    public IDockFloatHost? MovingHost => Request.MovingHost;

    // Reorder

    public DockTabStrip? Strip { get; set; }

    public List<DockTab> Tabs { get; } = [];

    public double[] Slots { get; set; } = [];

    public double[] Widths { get; set; } = [];

    public int From { get; set; }

    public int Index { get; set; }

    public double PointerStartX { get; set; }

    public double Offset { get; set; }

    // Dock and move

    public IDockHost? HoverHost { get; set; }

    public DockGroup? CompassGroup { get; set; }

    public DockHit Hit { get; set; }

    public DockGroupView? DropView { get; set; }

    public PixelPoint HostStartPixel { get; set; }

    public Point HostStartPoint { get; set; }
}

/// <summary>
/// The horizontal shift of a tab while another tab is dragged past it: a translate transform driven by a snappy spring,
/// as in <c>ReorderableList</c> (spec 8). Reduced and off jump.
/// </summary>
internal sealed class DockTabShift
{
    private static readonly ConditionalWeakTable<DockTab, DockTabShift> s_shifts = new();

    private readonly TranslateTransform _transform = new();
    private readonly MotionValue<double> _value;

    private DockTabShift(DockTab tab)
    {
        _value = new MotionValue<double>(tab, 0, MotionVectors.Double);
        _value.Changed += x => _transform.X = x;
        tab.RenderTransform = _transform;
    }

    public static DockTabShift Of(DockTab tab) => s_shifts.GetValue(tab, static t => new DockTabShift(t));

    /// <summary>The shift of a tab without creating one (tests).</summary>
    public static double Current(DockTab tab) => s_shifts.TryGetValue(tab, out var shift) ? shift.Value : 0;

    public double Value => _value.Value;

    public void Jump(double x) => _value.Jump(x);

    public void SpringTo(double x) => _ = _value.AnimateTo(x, MotionSpec.Spring(ArcSpring.Snappy));
}

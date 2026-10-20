using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A temporary side surface for focused work that keeps the page in context: filters, settings, record details, forms
/// too long for a dialog. Declare it anywhere; while <see cref="ModalOverlay.IsOpen"/> its panel springs in from
/// <see cref="Side"/> above a lightly blurred scrim.
/// <code>
/// &lt;arc:Drawer Title="Filters" Side="Right" IsOpen="{Binding ShowFilters}"&gt; … &lt;/arc:Drawer&gt;
/// </code>
/// The header is a drag handle: it follows the pointer toward the edge and rubber-bands the other way; past a third
/// of the panel, or on a flick toward the edge faster than 500px/s, the drawer closes and keeps the flick's velocity;
/// otherwise it springs back. A click, Escape or the scrim closes it with a quicker tween. The close button is always
/// there; under reduced motion dragging is off and the panel only fades.
/// </summary>
public class Drawer : ModalOverlay
{
    /// <summary>Distance fraction of the panel that closes it on release.</summary>
    private const double DismissFraction = 1.0 / 3.0;

    /// <summary>Release speed toward the edge (px/s) that closes it.</summary>
    private const double DismissVelocity = 500;

    public static readonly StyledProperty<DrawerSide> SideProperty =
        DrawerSurface.SideProperty.AddOwner<Drawer>();

    /// <summary>Edge the panel is anchored to (default Right).</summary>
    public DrawerSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    private DragTracker? _drag;
    private double _panStart;

    static Drawer()
    {
        CloseButtonNameProperty.OverrideDefaultValue<Drawer>("Close drawer");
    }

    /// <summary>True while the header is being dragged.</summary>
    public bool IsDragging => _drag?.IsDragging == true;

    protected override ModalSurface CreateSurface()
    {
        var surface = new DrawerSurface();
        surface.Bind(DrawerSurface.SideProperty, this.GetObservable(SideProperty));
        surface.TemplateApplied += (_, _) => AttachDrag();
        return surface;
    }

    protected override void ConfigurePresence(ModalPresence presence)
    {
        presence.Entrance = ModalEntrance.Slide;
        presence.Side = ToModalSide(Side);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty)
        {
            if (Presence is { } presence)
            {
                presence.Side = ToModalSide(Side);
            }
            AttachDrag();
        }
    }

    private static ModalSide ToModalSide(DrawerSide side) => side switch
    {
        DrawerSide.Left => ModalSide.Left,
        DrawerSide.Top => ModalSide.Top,
        DrawerSide.Bottom => ModalSide.Bottom,
        _ => ModalSide.Right
    };

    private int Sign => Side is DrawerSide.Right or DrawerSide.Bottom ? 1 : -1;

    private bool Horizontal => Side is DrawerSide.Left or DrawerSide.Right;

    private void AttachDrag()
    {
        _drag?.Dispose();
        _drag = null;
        if ((Surface as DrawerSurface)?.Header is not { } header)
        {
            return;
        }
        _drag = new DragTracker(header, new DragOptions
        {
            Axis = Horizontal ? DragAxis.Horizontal : DragAxis.Vertical,
            RejectCrossAxis = false,
            Threshold = 3
        })
        {
            // Buttons and links in the header keep their clicks; reduced motion turns dragging off.
            CanBegin = e => IsOpen && Surface is { } surface && ArcMotion.IsFull(surface) && !IsOnButton(e.Source as Visual, header)
        };
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += (_, _) => Settle(0);
    }

    private static bool IsOnButton(Visual? source, Visual header)
    {
        for (var v = source; v is not null && !ReferenceEquals(v, header); v = v.GetVisualParent())
        {
            if (v is AvaloniaButton or TextBox or ToggleButton)
            {
                return true;
            }
        }
        return false;
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        if (Presence is not { } presence)
        {
            return;
        }
        // Catch the panel wherever it is, even mid-entrance.
        _panStart = presence.PanelOffset;
        presence.SetPanelOffset(_panStart);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (Presence is not { } presence)
        {
            return;
        }
        var along = Horizontal ? e.Offset.X : e.Offset.Y;
        var toward = _panStart + along * Sign;
        // Toward the edge follows the pointer; the other way decays with a square root.
        presence.SetPanelOffset(toward >= 0 ? toward : RubberBand.Sqrt(toward));
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        if (Presence is not { } presence)
        {
            return;
        }
        var toward = presence.PanelOffset;
        var velocity = (Horizontal ? e.Velocity.X : e.Velocity.Y) * Sign;
        if (toward > presence.PanelExtent * DismissFraction || (toward > 0 && velocity > DismissVelocity))
        {
            if (RequestClose(DismissReason.Gesture, null, Math.Max(0, velocity)))
            {
                return;
            }
        }
        Settle(velocity);
    }

    private void Settle(double velocity)
    {
        if (Presence is { IsOpen: true } presence)
        {
            _ = presence.SettlePanelAsync(MotionSpec.Spring(ArcSpring.Snappy), velocity);
        }
    }
}

using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Anchored popup for Arc floating surfaces (S8): Avalonia's <see cref="Popup"/> with Arc's placement (the requested
/// side, <see cref="MarginToAnchor"/> kept whichever way the positioner flips), Arc's floating shadow drawn by an
/// <see cref="ArcPopupFrame"/> around the surface (with transparent room for it in a native popup window), and Arc enter / exit motion (P10) played on that
/// frame. Works in the window's overlay layer (the default; also browser and headless) and in native popup windows.
/// The child should be a <see cref="Border"/> or templated surface so the shadow follows its corner radius.
/// </summary>
/// <remarks>
/// <see cref="Popup.Placement"/> is rewritten to <see cref="PlacementMode.Custom"/>; the requested side is kept in
/// <see cref="RequestedPlacement"/> and resolved by Arc (code that reads the side reads
/// <c>RequestedPlacement ?? Placement</c>). Closing with an exit motion keeps <see cref="Popup.IsOpen"/> true until
/// the motion finished (the value is coerced), so the host only goes away once the surface is gone; opening again
/// during the exit reverses it from where the surface is.
/// </remarks>
public class ArcPopup : Popup
{
    /// <summary>Distance the surface travels toward its anchor while entering (Arc popover: 5px, tooltip: 3px).</summary>
    public static readonly StyledProperty<double> EnterOffsetProperty =
        AvaloniaProperty.Register<ArcPopup, double>(nameof(EnterOffset), 4);

    /// <summary>Scale the surface grows from while entering.</summary>
    public static readonly StyledProperty<double> EnterScaleProperty =
        AvaloniaProperty.Register<ArcPopup, double>(nameof(EnterScale), 0.97);

    /// <summary>When true the next open only fades (warm-up window of tooltips and hover cards).</summary>
    public static readonly StyledProperty<bool> IsInstantProperty =
        AvaloniaProperty.Register<ArcPopup, bool>(nameof(IsInstant));

    /// <summary>
    /// When true the popup closes without an exit motion at the Full motion level (a select's list vanishes as the
    /// value is committed); Reduced still fades out.
    /// </summary>
    public static readonly StyledProperty<bool> IsExitInstantProperty =
        AvaloniaProperty.Register<ArcPopup, bool>(nameof(IsExitInstant));

    /// <summary>Gap between the surface and its anchor along the placement side (Arc's <c>sideOffset</c>).</summary>
    public static readonly StyledProperty<double> MarginToAnchorProperty =
        AvaloniaProperty.Register<ArcPopup, double>(nameof(MarginToAnchor), 8);

    /// <summary>
    /// Whether the popup plays its <see cref="OpenMotion"/> and <see cref="CloseMotion"/>. Code that animates the
    /// <see cref="ArcPopupFrame"/> itself sets it false (with animation priority, which wins over the value the popup
    /// derives from the motion level).
    /// </summary>
    public static readonly StyledProperty<bool> IsMotionEnabledProperty =
        AvaloniaProperty.Register<ArcPopup, bool>(nameof(IsMotionEnabled), true);

    /// <summary>The side the popup was asked to open on (<see cref="Popup.Placement"/> itself is Custom).</summary>
    public static readonly StyledProperty<PlacementMode?> RequestedPlacementProperty =
        AvaloniaProperty.Register<ArcPopup, PlacementMode?>(nameof(RequestedPlacement));

    /// <summary>Shadow drawn around the surface when the popup is a native popup window.</summary>
    public static readonly StyledProperty<BoxShadows> PopupRootShadowProperty =
        AvaloniaProperty.Register<ArcPopup, BoxShadows>(nameof(PopupRootShadow));

    /// <summary>Shadow drawn around the surface when the popup is hosted in the window's overlay layer.</summary>
    public static readonly StyledProperty<BoxShadows> OverlayHostShadowProperty =
        AvaloniaProperty.Register<ArcPopup, BoxShadows>(nameof(OverlayHostShadow));

    /// <inheritdoc cref="EnterOffsetProperty"/>
    public double EnterOffset
    {
        get => GetValue(EnterOffsetProperty);
        set => SetValue(EnterOffsetProperty, value);
    }

    /// <inheritdoc cref="EnterScaleProperty"/>
    public double EnterScale
    {
        get => GetValue(EnterScaleProperty);
        set => SetValue(EnterScaleProperty, value);
    }

    /// <inheritdoc cref="IsInstantProperty"/>
    public bool IsInstant
    {
        get => GetValue(IsInstantProperty);
        set => SetValue(IsInstantProperty, value);
    }

    /// <inheritdoc cref="IsExitInstantProperty"/>
    public bool IsExitInstant
    {
        get => GetValue(IsExitInstantProperty);
        set => SetValue(IsExitInstantProperty, value);
    }

    /// <inheritdoc cref="MarginToAnchorProperty"/>
    public double MarginToAnchor
    {
        get => GetValue(MarginToAnchorProperty);
        set => SetValue(MarginToAnchorProperty, value);
    }

    /// <inheritdoc cref="IsMotionEnabledProperty"/>
    public bool IsMotionEnabled
    {
        get => GetValue(IsMotionEnabledProperty);
        set => SetValue(IsMotionEnabledProperty, value);
    }

    /// <inheritdoc cref="RequestedPlacementProperty"/>
    public PlacementMode? RequestedPlacement
    {
        get => GetValue(RequestedPlacementProperty);
        set => SetValue(RequestedPlacementProperty, value);
    }

    /// <inheritdoc cref="PopupRootShadowProperty"/>
    public BoxShadows PopupRootShadow
    {
        get => GetValue(PopupRootShadowProperty);
        set => SetValue(PopupRootShadowProperty, value);
    }

    /// <inheritdoc cref="OverlayHostShadowProperty"/>
    public BoxShadows OverlayHostShadow
    {
        get => GetValue(OverlayHostShadowProperty);
        set => SetValue(OverlayHostShadowProperty, value);
    }

    /// <summary>Motion played on the frame when the popup opens (null: it appears at once).</summary>
    public ArcPopupMotion? OpenMotion { get; set; }

    /// <summary>Motion played on the frame before the popup closes (null: it closes at once).</summary>
    public ArcPopupMotion? CloseMotion { get; set; }

    /// <summary>True when the last placement put the surface on the opposite vertical side of the requested one.</summary>
    public bool IsVerticalFlipped { get; private set; }

    /// <summary>True when the last placement put the surface on the opposite horizontal side of the requested one.</summary>
    public bool IsHorizontalFlipped { get; private set; }

    /// <summary>Raised when <see cref="IsVerticalFlipped"/> or <see cref="IsHorizontalFlipped"/> changes.</summary>
    public event EventHandler? PositionFlipped;

    static ArcPopup()
    {
        // Defaults, not local values: popups declared in a ControlTemplate set these with template priority, which a
        // local value set in the constructor would silently override.
        IsLightDismissEnabledProperty.OverrideDefaultValue<ArcPopup>(true);
        // Arc surfaces sit a few pixels from their anchor while their floating shadow spreads 28–68px. In a native
        // popup window that shadow is part of the window, so it would cover the anchor and take its pointer input
        // (hover popups flicker, the anchor stops receiving clicks). In the window's overlay layer the shadow is only
        // paint, as on the web. Native windows remain available per popup (ShouldUseOverlayLayer = false).
        ShouldUseOverlayLayerProperty.OverrideDefaultValue<ArcPopup>(true);
        // Coerce rather than react: Popup makes the assigned child its logical child before a changed handler runs, so
        // a handler could no longer move that child into a frame.
        ChildProperty.OverrideMetadata<ArcPopup>(new StyledPropertyMetadata<Control?>(coerce: static (popup, child) =>
            popup is ArcPopup arcPopup ? arcPopup.HostChild(child) : child));
        // Closing with an exit motion keeps the popup open until the motion finished.
        IsOpenProperty.OverrideMetadata<ArcPopup>(new StyledPropertyMetadata<bool>(coerce: static (popup, open) =>
            popup is ArcPopup arcPopup ? arcPopup.CoerceIsOpen(open) : open));
        // Pointer placements open at the last pointer position of the window.
        InputElement.PointerMovedEvent.AddClassHandler<TopLevel>(TrackPointer, RoutingStrategies.Tunnel, true);
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>(TrackPointer, RoutingStrategies.Tunnel, true);
    }

    private ArcPopupFrame? _frame;
    private MotionValue<double>? _opacity;
    private MotionValue<Vector>? _translate;
    private MotionValue<double>? _scale;
    private readonly MatrixTransform _motionTransform = new();
    private ArcPopupMotion? _playing;
    private bool _frameMoved;
    private bool _exiting;
    private bool _finishingExit;
    private bool _awaitingRest;
    private int _motionGeneration;
    private TopLevel? _dismissTopLevel;

    public ArcPopup()
    {
        ArcTokens.Bind(this, PopupRootShadowProperty, ArcDesignTokenKind.ShadowFloating, Avalonia.Data.BindingPriority.Style);
        ArcTokens.Bind(this, OverlayHostShadowProperty, ArcDesignTokenKind.ShadowFloating, Avalonia.Data.BindingPriority.Style);
        SetCurrentValue(RequestedPlacementProperty, PlacementMode.Bottom);
        SetCurrentValue(PlacementProperty, PlacementMode.Custom);
        CustomPopupPlacementCallback = HandlePlacement;
        Opened += HandleOpened;
        Closed += HandleClosed;
        UpdateMotions();
    }

    /// <summary>
    /// The surface that was assigned to <see cref="Popup.Child"/>. Assigned surfaces are hosted in an
    /// <see cref="ArcPopupFrame"/>; code that needs the surface itself reads this instead of <see cref="Popup.Child"/>.
    /// </summary>
    public Control? Surface => Child is ArcPopupFrame frame ? frame.Surface : Child;

    /// <summary>Runs the placement again (after the surface or its anchor changed while open).</summary>
    public void Reposition()
    {
        if (!IsOpen)
        {
            return;
        }
        // Offset changes are the public way to make the popup run its placement again.
        var offset = VerticalOffset;
        SetCurrentValue(VerticalOffsetProperty, offset + 1);
        SetCurrentValue(VerticalOffsetProperty, offset);
    }

    #region Frame

    /// <summary>The element <see cref="Popup.Child"/> actually holds for an assigned surface (reused while it is the same surface).</summary>
    private Control? HostChild(Control? surface)
    {
        if (surface is null or ArcPopupFrame)
        {
            // Release the previous surface so another popup (or the same one later) can host it.
            if (surface is null && _frame is not null)
            {
                _frame.Surface = null;
            }
            return surface;
        }
        if (_frame is null)
        {
            _frame = new ArcPopupFrame { Scale = ArcZoom.GetScale(this) };
            _opacity = new MotionValue<double>(_frame, 1, MotionVectors.Double);
            _translate = new MotionValue<Vector>(_frame, default, MotionVectors.Vector);
            _scale = new MotionValue<double>(_frame, 1, MotionVectors.Double);
            _opacity.Changed += _ => ApplyMotion();
            _translate.Changed += _ => ApplyMotion();
            _scale.Changed += _ => ApplyMotion();
            SyncShadow(ShouldUseOverlayLayer);
        }
        if (!ReferenceEquals(_frame.Surface, surface))
        {
            _frame.Surface = null;
            _frame.Surface = surface;
        }
        return _frame;
    }

    private void SyncZoom()
    {
        if (_frame is not null)
        {
            _frame.Scale = ArcZoom.GetScale(this);
        }
    }

    /// <summary>
    /// Gives the frame the shadow of the host it is (about to be) in. A native popup window clips to its size, so there
    /// the frame also takes the room the shadow needs as its margin. In the overlay layer nothing clips the popup host:
    /// the shadow is painted past the host's bounds and takes no layout room, so a surface that fills the window's
    /// height is not squeezed by its own shadow.
    /// </summary>
    private void SyncShadow(bool overlay)
    {
        if (_frame is null)
        {
            return;
        }
        var shadow = overlay ? OverlayHostShadow : PopupRootShadow;
        _frame.BoxShadow = shadow;
        _frame.Margin = overlay ? default : ArcPopupFrame.RoomFor(shadow);
    }

    #endregion

    #region Placement

    /// <summary>
    /// Runs after Arc resolved the placement (requested side, margin to anchor, flips). Subclasses adjust the final
    /// <paramref name="placement"/> here (custom anchors, motion origins from the flipped side). Offsets and the anchor
    /// rectangle are in the window's client coordinates and place the surface itself;
    /// <see cref="CustomPopupPlacement.PopupSize"/> includes the shadow room <paramref name="shadow"/> around it (use
    /// <paramref name="shadow"/>, not <see cref="CustomPopupPlacement.Deflate"/>, which is the room the host leaves out).
    /// </summary>
    protected virtual void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                               bool isOverlayHost, bool flipX, bool flipY)
    {
    }

    /// <summary>
    /// Runs first, before the margin to the anchor is applied and flips are predicted: subclasses that anchor somewhere
    /// else (a point, a menu-wide rectangle) change <see cref="CustomPopupPlacement.AnchorRectangle"/> here.
    /// </summary>
    protected virtual void AdjustPlacementAnchor(CustomPopupPlacement placement)
    {
    }

    /// <summary>True when the surface sits in a native popup window (headless and overlay hosting report false).</summary>
    protected bool IsHostedInPopupWindow(bool isOverlayHost) =>
        !isOverlayHost && Child is { } child && TopLevel.GetTopLevel(child) is PopupRoot;

    private void HandlePlacement(CustomPopupPlacement placement)
    {
        var requested = RequestedPlacement is { } side && side != PlacementMode.Custom ? side : PlacementMode.Bottom;
        var topLevel = TopLevel.GetTopLevel(placement.Target);
        var isOverlayHost = !(Child is { } child && TopLevel.GetTopLevel(child) is PopupRoot);
        // The shadow room is the frame's margin. The host may or may not leave its child's margin out when it places
        // the popup (that depends on the host's template), so Arc places the surface itself and only hands the host
        // a position (see the end of this method).
        var room = Child is ArcPopupFrame frame ? frame.Margin : placement.Deflate;
        var margin = MarginToAnchor;
        if (requested == PlacementMode.Pointer)
        {
            placement.AnchorRectangle = new Rect(PointerPosition(topLevel, placement.AnchorRectangle), new Size(1, 1));
        }
        (placement.Anchor, placement.Gravity) = AnchorAndGravity(requested);
        AdjustPlacementAnchor(placement);
        GrowAnchor(placement, requested, margin, isOverlayHost ? default : room);
        var surfaceSize = placement.PopupSize.Deflate(room);
        var bounds = topLevel is null ? (Rect?)null : ConstraintBounds(topLevel, isOverlayHost);
        var (_, flipX, flipY) = bounds is { } predicted ? Resolve(placement, surfaceSize, predicted) : (default, false, false);
        OnPlacementResolved(placement, room, margin, isOverlayHost, flipX, flipY);
        if (bounds is { } final)
        {
            // Flip and slide the surface (without its shadow room) the way the positioner would, then pin the host
            // there: whatever room the host leaves out, the surface lands on the resolved rectangle and the shadow
            // spreads around it. Only resizing a surface that does not fit is left to the positioner.
            (var surface, flipX, flipY) = Resolve(placement, surfaceSize, final);
            var hostRoom = placement.Deflate;
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomRight;
            placement.AnchorRectangle = new Rect(surface.X + hostRoom.Left - room.Left, surface.Y + hostRoom.Top - room.Top, 1, 1);
            placement.Offset = default;
            // The host (shadow room included) may reach past the bounds while the surface fits: the positioner
            // resizes only when the surface itself does not fit.
            var resize = PopupPositionerConstraintAdjustment.None;
            if ((placement.ConstraintAdjustment & PopupPositionerConstraintAdjustment.ResizeX) != 0 && !FitsX(surface, final))
            {
                resize |= PopupPositionerConstraintAdjustment.ResizeX;
            }
            if ((placement.ConstraintAdjustment & PopupPositionerConstraintAdjustment.ResizeY) != 0 && !FitsY(surface, final))
            {
                resize |= PopupPositionerConstraintAdjustment.ResizeY;
            }
            placement.ConstraintAdjustment = resize;
        }
        if (flipX != IsHorizontalFlipped || flipY != IsVerticalFlipped)
        {
            IsHorizontalFlipped = flipX;
            IsVerticalFlipped = flipY;
            PositionFlipped?.Invoke(this, EventArgs.Empty);
        }
    }

    private (PopupAnchor Anchor, PopupGravity Gravity) AnchorAndGravity(PlacementMode placement) => placement switch
    {
        PlacementMode.Top => (PopupAnchor.Top, PopupGravity.Top),
        PlacementMode.Left => (PopupAnchor.Left, PopupGravity.Left),
        PlacementMode.Right => (PopupAnchor.Right, PopupGravity.Right),
        PlacementMode.Center => (PopupAnchor.None, PopupGravity.None),
        PlacementMode.Pointer => (PopupAnchor.TopLeft, PopupGravity.BottomRight),
        PlacementMode.AnchorAndGravity => (PlacementAnchor, PlacementGravity),
        PlacementMode.TopEdgeAlignedLeft => (PopupAnchor.TopLeft, PopupGravity.TopRight),
        PlacementMode.TopEdgeAlignedRight => (PopupAnchor.TopRight, PopupGravity.TopLeft),
        PlacementMode.BottomEdgeAlignedLeft => (PopupAnchor.BottomLeft, PopupGravity.BottomRight),
        PlacementMode.BottomEdgeAlignedRight => (PopupAnchor.BottomRight, PopupGravity.BottomLeft),
        PlacementMode.LeftEdgeAlignedTop => (PopupAnchor.TopLeft, PopupGravity.BottomLeft),
        PlacementMode.LeftEdgeAlignedBottom => (PopupAnchor.BottomLeft, PopupGravity.TopLeft),
        PlacementMode.RightEdgeAlignedTop => (PopupAnchor.TopRight, PopupGravity.BottomRight),
        PlacementMode.RightEdgeAlignedBottom => (PopupAnchor.BottomRight, PopupGravity.TopRight),
        _ => (PopupAnchor.Bottom, PopupGravity.Bottom)
    };

    private static bool IsVertical(PlacementMode placement) => placement is PlacementMode.Top or PlacementMode.Bottom or
        PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight or
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight;

    private static bool IsHorizontal(PlacementMode placement) => placement is PlacementMode.Left or PlacementMode.Right or
        PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom or
        PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom;

    /// <summary>
    /// The anchor grows by the margin on both sides of the placement axis and the offset along that axis stays the plain
    /// popup offset, so whichever side the positioner picks, the surface lands MarginToAnchor away. A native popup
    /// window carries its shadow inside the window, so there the anchor also grows by the shadow facing it: the window
    /// must not cover the anchor (it would take its pointer input), and the visible gap is margin + shadow.
    /// </summary>
    private static void GrowAnchor(CustomPopupPlacement placement, PlacementMode requested, double margin, Thickness windowShadow)
    {
        var anchor = placement.AnchorRectangle;
        if (IsVertical(requested))
        {
            var above = margin + windowShadow.Bottom;
            var below = margin + windowShadow.Top;
            placement.AnchorRectangle = new Rect(anchor.X, anchor.Y - above, anchor.Width, anchor.Height + above + below);
        }
        else if (IsHorizontal(requested))
        {
            var before = margin + windowShadow.Right;
            var after = margin + windowShadow.Left;
            placement.AnchorRectangle = new Rect(anchor.X - before, anchor.Y, anchor.Width + before + after, anchor.Height);
        }
        else if (requested == PlacementMode.Pointer && margin != 0)
        {
            placement.AnchorRectangle = anchor.Inflate(margin);
        }
    }

    /// <summary>The area the positioner keeps the surface in, in the window's client coordinates.</summary>
    private static Rect ConstraintBounds(TopLevel topLevel, bool isOverlayHost)
    {
        if (!isOverlayHost && topLevel.Screens?.ScreenFromTopLevel(topLevel) is { } screen)
        {
            var area = screen.WorkingArea;
            var topLeft = topLevel.PointToClient(area.TopLeft);
            var bottomRight = topLevel.PointToClient(area.BottomRight);
            return new Rect(topLeft, bottomRight);
        }
        var insets = topLevel.InsetsManager?.SafeAreaPadding ?? default;
        return new Rect(topLevel.ClientSize).Deflate(insets);
    }

    /// <summary>
    /// Where the positioner's rules put a surface of <paramref name="size"/>: on the requested side, flipped to the
    /// opposite side when it does not fit there but fits there, then slid inside <paramref name="bounds"/> (each as the
    /// constraint adjustment allows; horizontal first, as the positioner does).
    /// </summary>
    private static (Rect Surface, bool FlipX, bool FlipY) Resolve(CustomPopupPlacement placement, Size size, Rect bounds)
    {
        var adjustment = placement.ConstraintAdjustment;
        var rect = Unconstrained(placement, placement.Anchor, placement.Gravity, size);
        var flipX = false;
        var flipY = false;
        if ((adjustment & PopupPositionerConstraintAdjustment.FlipX) != 0 && !FitsX(rect, bounds))
        {
            var flipped = Unconstrained(placement, FlipX(placement.Anchor), FlipX(placement.Gravity), size);
            if (FitsX(flipped, bounds))
            {
                rect = rect.WithX(flipped.X);
                flipX = true;
            }
        }
        if ((adjustment & PopupPositionerConstraintAdjustment.SlideX) != 0)
        {
            rect = rect.WithX(Math.Max(rect.X, bounds.X));
            if (rect.Right > bounds.Right)
            {
                rect = rect.WithX(bounds.Right - rect.Width);
            }
        }
        if ((adjustment & PopupPositionerConstraintAdjustment.FlipY) != 0 && !FitsY(rect, bounds))
        {
            var flipped = Unconstrained(placement, FlipY(placement.Anchor), FlipY(placement.Gravity), size);
            if (FitsY(flipped, bounds))
            {
                rect = rect.WithY(flipped.Y);
                flipY = true;
            }
        }
        if ((adjustment & PopupPositionerConstraintAdjustment.SlideY) != 0)
        {
            rect = rect.WithY(Math.Max(rect.Y, bounds.Y));
            if (rect.Bottom > bounds.Bottom)
            {
                rect = rect.WithY(bounds.Bottom - rect.Height);
            }
        }
        return (rect, flipX, flipY);
    }

    private static bool FitsX(Rect rect, Rect bounds) => rect.X >= bounds.X && rect.Right <= bounds.Right;

    private static bool FitsY(Rect rect, Rect bounds) => rect.Y >= bounds.Y && rect.Bottom <= bounds.Bottom;

    private static Rect Unconstrained(CustomPopupPlacement placement, PopupAnchor anchor, PopupGravity gravity, Size size)
    {
        var rect = placement.AnchorRectangle;
        var x = (anchor & PopupAnchor.Left) != 0 ? rect.X : (anchor & PopupAnchor.Right) != 0 ? rect.Right : rect.X + rect.Width / 2;
        var y = (anchor & PopupAnchor.Top) != 0 ? rect.Y : (anchor & PopupAnchor.Bottom) != 0 ? rect.Bottom : rect.Y + rect.Height / 2;
        x += (gravity & PopupGravity.Left) != 0 ? -size.Width : (gravity & PopupGravity.Right) != 0 ? 0 : -size.Width / 2;
        y += (gravity & PopupGravity.Top) != 0 ? -size.Height : (gravity & PopupGravity.Bottom) != 0 ? 0 : -size.Height / 2;
        return new Rect(new Point(x, y) + placement.Offset, size);
    }

    private static PopupAnchor FlipX(PopupAnchor anchor) => (anchor & PopupAnchor.HorizontalMask) switch
    {
        PopupAnchor.Left => (anchor & ~PopupAnchor.HorizontalMask) | PopupAnchor.Right,
        PopupAnchor.Right => (anchor & ~PopupAnchor.HorizontalMask) | PopupAnchor.Left,
        _ => anchor
    };

    private static PopupAnchor FlipY(PopupAnchor anchor) => (anchor & PopupAnchor.VerticalMask) switch
    {
        PopupAnchor.Top => (anchor & ~PopupAnchor.VerticalMask) | PopupAnchor.Bottom,
        PopupAnchor.Bottom => (anchor & ~PopupAnchor.VerticalMask) | PopupAnchor.Top,
        _ => anchor
    };

    private static PopupGravity FlipX(PopupGravity gravity) =>
        (gravity & (PopupGravity.Left | PopupGravity.Right)) switch
        {
            PopupGravity.Left => (gravity & ~PopupGravity.Left) | PopupGravity.Right,
            PopupGravity.Right => (gravity & ~PopupGravity.Right) | PopupGravity.Left,
            _ => gravity
        };

    private static PopupGravity FlipY(PopupGravity gravity) =>
        (gravity & (PopupGravity.Top | PopupGravity.Bottom)) switch
        {
            PopupGravity.Top => (gravity & ~PopupGravity.Top) | PopupGravity.Bottom,
            PopupGravity.Bottom => (gravity & ~PopupGravity.Bottom) | PopupGravity.Top,
            _ => gravity
        };

    private sealed class PointerSlot
    {
        public Point Position;
    }

    private static readonly ConditionalWeakTable<TopLevel, PointerSlot> s_pointers = new();

    private static void TrackPointer(TopLevel topLevel, PointerEventArgs e) =>
        s_pointers.GetOrCreateValue(topLevel).Position = e.GetPosition(topLevel);

    /// <summary>The last pointer position over <paramref name="topLevel"/>, or the anchor's centre when none was seen.</summary>
    private static Point PointerPosition(TopLevel? topLevel, Rect anchor) =>
        topLevel is not null && s_pointers.TryGetValue(topLevel, out var slot) ? slot.Position : anchor.Center;

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PlacementProperty && change.GetNewValue<PlacementMode>() is var placement &&
            placement != PlacementMode.Custom)
        {
            // Arc resolves every side itself (margin, flips): keep the requested one and let the positioner call back.
            SetCurrentValue(RequestedPlacementProperty, placement);
            SetCurrentValue(PlacementProperty, PlacementMode.Custom);
        }
        if (change.Property == PlacementProperty || change.Property == RequestedPlacementProperty || change.Property == EnterOffsetProperty ||
            change.Property == EnterScaleProperty || change.Property == IsInstantProperty || change.Property == IsExitInstantProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == PlacementTargetProperty)
        {
            UpdateMotions();
        }
        if (change.Property == IsOpenProperty && change.GetNewValue<bool>())
        {
            // Motion level can change while closed (inherited from the placement target); so can the zoom, which
            // the surface must have before the popup measures it.
            UpdateMotions();
            SyncZoom();
            SyncShadow(ShouldUseOverlayLayer);
        }
        else if (change.Property == ArcZoom.ScaleProperty)
        {
            SyncZoom();
        }
        else if (change.Property == PopupRootShadowProperty || change.Property == OverlayHostShadowProperty ||
                 change.Property == ShouldUseOverlayLayerProperty)
        {
            SyncShadow(IsOpen ? IsUsingOverlayLayer : ShouldUseOverlayLayer);
        }
        else if (change.Property == MarginToAnchorProperty || change.Property == RequestedPlacementProperty)
        {
            Reposition();
        }
        base.OnPropertyChanged(change);
    }

    private void UpdateMotions()
    {
        var level = MotionLevel();
        IsMotionEnabled = level != ArcMotionLevel.Off;
        var (origin, direction) = OriginFor(RequestedPlacement ?? Placement);
        var fadeOnly = level == ArcMotionLevel.Reduced || IsInstant;
        var offset = direction * EnterOffset;
        OpenMotion = new ArcPopupEnterMotion(origin, offset, EnterScale, fadeOnly,
            duration: fadeOnly ? ArcMotionPresets.Instant : ArcMotionPresets.Fast);
        CloseMotion = IsExitInstant && level == ArcMotionLevel.Full
            ? null
            : new ArcPopupExitMotion(origin, 0.98, level == ArcMotionLevel.Reduced);
    }

    private ArcMotionLevel MotionLevel() =>
        PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);

    /// <summary>Transform origin on the anchor side and the direction the surface travels from.</summary>
    internal static (RelativePoint Origin, Vector Direction) OriginFor(PlacementMode placement) => placement switch
    {
        PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight
            => (new RelativePoint(0.5, 1, RelativeUnit.Relative), new Vector(0, 1)),
        PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom
            => (new RelativePoint(1, 0.5, RelativeUnit.Relative), new Vector(1, 0)),
        PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom
            => (new RelativePoint(0, 0.5, RelativeUnit.Relative), new Vector(-1, 0)),
        PlacementMode.Pointer => (new RelativePoint(0, 0, RelativeUnit.Relative), new Vector(0, -1)),
        _ => (new RelativePoint(0.5, 0, RelativeUnit.Relative), new Vector(0, -1))
    };

    #region Motion

    // Code-driven (MotionValue) because ControlTheme transitions cannot express it: the motion runs on the frame the
    // popup creates around its surface (outside any template), the popup may only close after the exit played, and an
    // open during the exit reverses from the values on screen.

    /// <summary>True when <paramref name="motion"/> can play now (motion on, frame on screen).</summary>
    private bool CanAnimate(ArcPopupMotion? motion) =>
        motion is not null && IsMotionEnabled && _frame is { } frame && TopLevel.GetTopLevel(frame) is not null &&
        MotionLevel() != ArcMotionLevel.Off;

    private bool CoerceIsOpen(bool open)
    {
        if (open)
        {
            if (_exiting)
            {
                // Opened again during the exit: the popup never closed, the surface comes back from where it is.
                _exiting = false;
                var generation = ++_motionGeneration;
                Dispatcher.UIThread.Post(() =>
                {
                    if (generation == _motionGeneration && IsOpen)
                    {
                        PlayEnter(fromCurrent: true);
                    }
                });
            }
            return true;
        }
        // The host is already gone (light dismiss, Close(), a detached anchor) or the exit finished.
        if (_finishingExit || _frame is not { } frame || !IsInsidePopup(frame))
        {
            return false;
        }
        if (_exiting)
        {
            return true;
        }
        if (!CanAnimate(CloseMotion))
        {
            return false;
        }
        _exiting = true;
        var exit = ++_motionGeneration;
        // Started outside the coercion: the motion values write the frame and may finish at once.
        Dispatcher.UIThread.Post(() => PlayExit(exit));
        return true;
    }

    private void HandleOpened(object? sender, EventArgs e)
    {
        SyncShadow(IsUsingOverlayLayer);
        HookLightDismiss();
        HookAnchorTracking();
        if (!CanAnimate(OpenMotion))
        {
            StopMotion();
            return;
        }
        PlayEnter(fromCurrent: false);
    }

    private void HandleClosed(object? sender, EventArgs e)
    {
        _exiting = false;
        _motionGeneration++;
        UnhookLightDismiss();
        UnhookAnchorTracking();
        StopMotion();
    }

    private void PlayEnter(bool fromCurrent)
    {
        if (OpenMotion is not { } motion || !CanAnimate(motion) || _opacity is null || _translate is null || _scale is null)
        {
            StopMotion();
            return;
        }
        _motionGeneration++;
        _playing = motion;
        if (!fromCurrent)
        {
            _opacity.Jump(motion.Fades ? 0 : 1);
            _translate.Jump(motion.FadeOnly ? default : motion.Offset);
            _scale.Jump(motion.FadeOnly ? 1 : motion.Scale);
        }
        _opacity.AnimateTo(1, motion.OpacitySpec);
        _translate.AnimateTo(default, motion.TransformSpec);
        _scale.AnimateTo(1, motion.TransformSpec);
        AwaitRest();
    }

    private void PlayExit(int generation)
    {
        if (generation != _motionGeneration || !_exiting)
        {
            return;
        }
        if (CloseMotion is not { } motion || !CanAnimate(motion) || _opacity is null || _translate is null || _scale is null)
        {
            FinishExit();
            return;
        }
        _playing = motion;
        _opacity.AnimateTo(motion.Fades ? 0 : 1, motion.OpacitySpec);
        _translate.AnimateTo(motion.FadeOnly ? default : motion.Offset, motion.TransformSpec);
        _scale.AnimateTo(motion.FadeOnly ? 1 : motion.Scale, motion.TransformSpec);
        AwaitRest();
    }

    // The end of a motion is seen on the frame that writes the final values (all three at their targets), not through
    // the values' tasks: those complete on the thread pool, which may run late, while frames (and tests' manual
    // frame clocks) keep going. What follows is posted, so the frame's last values are written first.

    private void AwaitRest()
    {
        _awaitingRest = true;
        CheckRest();
    }

    private void CheckRest()
    {
        if (!_awaitingRest || _opacity is null || _translate is null || _scale is null ||
            _opacity.Value != _opacity.Target || _translate.Value != _translate.Target || _scale.Value != _scale.Target)
        {
            return;
        }
        _awaitingRest = false;
        var generation = _motionGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _motionGeneration)
            {
                return;
            }
            if (_exiting)
            {
                FinishExit();
            }
            else
            {
                // At rest: hand the frame back without a transform (P10 leaves nothing behind).
                ResetFrame();
            }
        });
    }

    /// <summary>Lets the coerced IsOpen fall to its requested value (false), which closes the host.</summary>
    private void FinishExit()
    {
        _exiting = false;
        _finishingExit = true;
        try
        {
            CoerceValue(IsOpenProperty);
        }
        finally
        {
            _finishingExit = false;
        }
    }

    private void StopMotion()
    {
        _awaitingRest = false;
        if (_frameMoved)
        {
            _opacity?.Jump(1);
            _translate?.Jump(default);
            _scale?.Jump(1);
        }
        ResetFrame();
    }

    private void ApplyMotion()
    {
        if (_frame is not { } frame || _opacity is null || _translate is null || _scale is null)
        {
            return;
        }
        var scale = _scale.Value;
        var translate = _translate.Value;
        frame.Opacity = Math.Clamp(_opacity.Value, 0, 1);
        frame.RenderTransformOrigin = _playing?.Origin ?? RelativePoint.TopLeft;
        _motionTransform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(translate.X, translate.Y);
        if (!ReferenceEquals(frame.RenderTransform, _motionTransform))
        {
            frame.RenderTransform = _motionTransform;
        }
        _frameMoved = true;
        CheckRest();
    }

    /// <summary>Clears what the popup's own motion wrote on the frame (frames animated by other code are left alone).</summary>
    private void ResetFrame()
    {
        if (!_frameMoved || _frame is not { } frame)
        {
            return;
        }
        _frameMoved = false;
        frame.Opacity = 1;
        if (ReferenceEquals(frame.RenderTransform, _motionTransform))
        {
            frame.RenderTransform = null;
        }
    }

    #endregion

    #region Anchor tracking

    // Avalonia places the popup again when its anchor changes size, not when the anchor moves in the window (a page
    // scrolling under an open menu). The surface stays on its anchor: after every layout pass the anchor's place in
    // the window is compared with the last one.

    private Control? _trackedAnchor;
    private Rect _trackedRect;

    private void HookAnchorTracking()
    {
        UnhookAnchorTracking();
        if (PlacementTarget is not { } anchor)
        {
            return;
        }
        _trackedAnchor = anchor;
        _trackedRect = AnchorRect(anchor);
        anchor.LayoutUpdated += HandleAnchorLayoutUpdated;
    }

    private void UnhookAnchorTracking()
    {
        if (_trackedAnchor is { } anchor)
        {
            anchor.LayoutUpdated -= HandleAnchorLayoutUpdated;
            _trackedAnchor = null;
        }
    }

    private void HandleAnchorLayoutUpdated(object? sender, EventArgs e)
    {
        if (_trackedAnchor is not { } anchor || !IsOpen)
        {
            return;
        }
        var rect = AnchorRect(anchor);
        if (rect == _trackedRect)
        {
            return;
        }
        _trackedRect = rect;
        Reposition();
    }

    private static Rect AnchorRect(Control anchor) =>
        TopLevel.GetTopLevel(anchor) is { } topLevel && anchor.TransformToVisual(topLevel) is { } transform
            ? new Rect(anchor.Bounds.Size).TransformToAABB(transform)
            : default;

    #endregion

    #region Light dismiss

    // Avalonia dismisses a light-dismiss popup from a transparent layer under the popups, closing the host at once. Arc
    // handles presses on that layer first, so the popup leaves with its exit motion (it closes through IsOpen).
    // Popups that pass the dismissing press through to the content below keep Avalonia's handling.

    private void HookLightDismiss()
    {
        UnhookLightDismiss();
        if (!IsLightDismissEnabled || OverlayDismissEventPassThrough)
        {
            return;
        }
        var anchor = PlacementTarget ?? Parent as Visual;
        if (anchor is null || TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            return;
        }
        _dismissTopLevel = topLevel;
        topLevel.AddHandler(InputElement.PointerPressedEvent, HandleTopLevelPressed, RoutingStrategies.Tunnel, true);
    }

    private void UnhookLightDismiss()
    {
        if (_dismissTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.PointerPressedEvent, HandleTopLevelPressed);
            _dismissTopLevel = null;
        }
    }

    private void HandleTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || !IsLightDismissEnabled || OverlayDismissEventPassThrough || !IsLightDismissLayer(e.Source))
        {
            return;
        }
        e.Handled = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>
    /// The light-dismiss layer is the transparent border a <see cref="VisualLayerManager"/> shows over its content
    /// while a light-dismiss popup is open (the content child is the only other border it hosts directly).
    /// </summary>
    private static bool IsLightDismissLayer(object? source) =>
        source is Border border && border.GetVisualParent() is VisualLayerManager manager && !ReferenceEquals(manager.Child, border);

    #endregion
}

using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using AtomPopup = AtomUI.Desktop.Controls.Popup;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Anchored popup for Arc floating surfaces (S8). Reuses AtomUI's popup (placement, flipping, shadow-aware
/// hosting in window or overlay mode) with Arc's floating shadow and Arc enter / exit motion (P10).
/// The child should be a <see cref="Border"/> or templated surface so the shadow follows its corner radius.
/// </summary>
public class ArcPopup : AtomPopup
{
    /// <summary>Distance the surface travels toward its anchor while entering (Arc popover: 5px, tooltip: 3px).</summary>
    public static readonly StyledProperty<double> EnterOffsetProperty =
        AvaloniaProperty.Register<ArcPopup, double>(nameof(EnterOffset), 4);

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

    public double EnterOffset
    {
        get => GetValue(EnterOffsetProperty);
        set => SetValue(EnterOffsetProperty, value);
    }

    public double EnterScale
    {
        get => GetValue(EnterScaleProperty);
        set => SetValue(EnterScaleProperty, value);
    }

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

    static ArcPopup()
    {
        // Defaults, not local values: popups declared in a ControlTemplate set these with template priority, which a
        // local value set in the constructor would silently override.
        MarginToAnchorProperty.OverrideDefaultValue<ArcPopup>(8);
        IsLightDismissEnabledProperty.OverrideDefaultValue<ArcPopup>(true);
        // Arc surfaces sit a few pixels from their anchor while their floating shadow spreads 28–68px. In a native
        // popup window that shadow is part of the window, so it would cover the anchor and take its pointer input
        // (hover popups flicker, the anchor stops receiving clicks). In the window's overlay layer the shadow is only
        // paint, as on the web. Native windows remain available per popup (ShouldUseOverlayLayer = false).
        ShouldUseOverlayLayerProperty.OverrideDefaultValue<ArcPopup>(true);
        // Coerce rather than react: Popup makes the assigned child its logical child before a changed handler runs, so
        // a handler could no longer move that child into a wrapper.
        ChildProperty.OverrideMetadata<ArcPopup>(new StyledPropertyMetadata<Control?>(coerce: static (popup, child) =>
            popup is ArcPopup arcPopup ? arcPopup.HostChild(child) : child));
    }

    private ArcZoomHost? _zoomHost;

    /// <summary>
    /// The surface that was assigned to <see cref="Popup.Child"/>. Assigned surfaces are hosted in an
    /// <see cref="ArcZoomHost"/> so they follow <see cref="ArcZoom"/>; code that needs the surface itself reads this
    /// instead of <see cref="Popup.Child"/>.
    /// </summary>
    public Control? Surface => Child is ArcZoomHost host ? host.Surface : Child;

    /// <summary>The element <see cref="Popup.Child"/> actually holds for an assigned surface (reused while it is the same surface).</summary>
    private Control? HostChild(Control? surface)
    {
        if (surface is null or ArcZoomHost)
        {
            // Release the previous surface so another popup (or the same one later) can host it.
            if (surface is null && _zoomHost is not null)
            {
                _zoomHost.Surface = null;
            }
            return surface;
        }
        if (_zoomHost is null)
        {
            _zoomHost = new ArcZoomHost { Scale = ArcZoom.GetScale(this) };
        }
        if (!ReferenceEquals(_zoomHost.Surface, surface))
        {
            _zoomHost.Surface = null;
            _zoomHost.Surface = surface;
        }
        return _zoomHost;
    }

    private void SyncZoom()
    {
        if (_zoomHost is not null)
        {
            _zoomHost.Scale = ArcZoom.GetScale(this);
        }
    }

    public ArcPopup()
    {
        ArcTokens.Bind(this, PopupRootShadowProperty, ArcDesignTokenKind.ShadowFloating, Avalonia.Data.BindingPriority.Style);
        ArcTokens.Bind(this, OverlayHostShadowProperty, ArcDesignTokenKind.ShadowFloating, Avalonia.Data.BindingPriority.Style);
        // Always go through AtomUI's placement (margin to anchor, flipping): AtomUI only enters it when the placement
        // changes, so a popup that keeps the default Bottom would otherwise ignore MarginToAnchor.
        SetCurrentValue(RequestedPlacementProperty, PlacementMode.Bottom);
        CustomPlacementCallback = HandlePlacement;
        UpdateMotions();
    }

    /// <summary>
    /// Runs after AtomUI positioned the popup (and after Arc removed the shadow margin). Subclasses adjust the final
    /// <paramref name="placement"/> here (custom anchors, motion origins from the flipped side); offsets are in the
    /// popup host's coordinates.
    /// </summary>
    protected virtual void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                               bool isOverlayHost, bool flipX, bool flipY)
    {
    }

    /// <summary>
    /// Runs first, right after AtomUI computed the placement: subclasses that anchor somewhere else (a point instead of
    /// the last pointer position) change the anchor rectangle here and report the flips the positioner will apply,
    /// so the shadow margin is removed on the correct sides.
    /// </summary>
    protected virtual void AdjustPlacementAnchor(CustomPopupPlacement placement, ref bool flipX, ref bool flipY)
    {
    }

    private void HandlePlacement(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor, bool isOverlayHost,
                                 bool flipX, bool flipY)
    {
        AdjustPlacementAnchor(placement, ref flipX, ref flipY);
        if (isOverlayHost)
        {
            PlaceInOverlay(placement, marginToAnchor);
        }
        else if (IsHostedInPopupWindow(isOverlayHost))
        {
            PlaceInPopupWindow(placement, shadow, marginToAnchor);
        }
        OnPlacementResolved(placement, shadow, marginToAnchor, isOverlayHost, flipX, flipY);
    }

    /// <summary>True when the surface sits in a native popup window (headless and overlay hosting report false).</summary>
    protected bool IsHostedInPopupWindow(bool isOverlayHost) =>
        !isOverlayHost && Child is { } child && TopLevel.GetTopLevel(child) is PopupRoot;

    private static bool IsVertical(PlacementMode placement) => placement is PlacementMode.Top or PlacementMode.Bottom or
        PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight or
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.BottomEdgeAlignedRight;

    private static bool IsHorizontal(PlacementMode placement) => placement is PlacementMode.Left or PlacementMode.Right or
        PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom or
        PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom;

    /// <summary>
    /// Overlay hosting: AtomUI applies MarginToAnchor as an offset in the direction it predicts, but predicts flips
    /// with the shadow-inflated size while the positioner flips with the real one, so near a window edge the margin
    /// can point the wrong way and the surface overlaps its anchor. Instead the anchor grows by the margin along the
    /// placement axis and the offset along that axis is the plain popup offset: whichever side the positioner picks,
    /// the surface lands MarginToAnchor away.
    /// </summary>
    private void PlaceInOverlay(CustomPopupPlacement placement, double margin)
    {
        var requested = RequestedPlacement ?? Placement;
        var anchor = placement.AnchorRectangle;
        if (IsVertical(requested))
        {
            placement.AnchorRectangle = new Rect(anchor.X, anchor.Y - margin, anchor.Width, anchor.Height + 2 * margin);
            placement.Offset = placement.Offset.WithY(VerticalOffset);
        }
        else if (IsHorizontal(requested))
        {
            placement.AnchorRectangle = new Rect(anchor.X - margin, anchor.Y, anchor.Width + 2 * margin, anchor.Height);
            placement.Offset = placement.Offset.WithX(HorizontalOffset);
        }
    }

    /// <summary>
    /// Native popup window (opt-in): the window carries the shadow as a margin, and moving it closer than AtomUI does
    /// would cover the anchor, so directional placements keep AtomUI's position (visible gap = margin + shadow).
    /// Pointer placements are corrected: AtomUI adds the shadow to the offset instead of removing it, which puts the
    /// surface two shadow widths past the point; growing the 1px anchor by the shadow on every side lands the
    /// surface on the point in whichever direction the positioner flips.
    /// </summary>
    private void PlaceInPopupWindow(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor)
    {
        if ((RequestedPlacement ?? Placement) is not PlacementMode.Pointer)
        {
            return;
        }
        var anchor = placement.AnchorRectangle;
        placement.AnchorRectangle = new Rect(anchor.X - shadow.Left, anchor.Y - shadow.Top,
            anchor.Width + shadow.Left + shadow.Right, anchor.Height + shadow.Top + shadow.Bottom);
        placement.Offset = new Point(HorizontalOffset + marginToAnchor, VerticalOffset + marginToAnchor);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
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
        }
        else if (change.Property == ArcZoom.ScaleProperty)
        {
            SyncZoom();
        }
        base.OnPropertyChanged(change);
    }

    private void UpdateMotions()
    {
        var level = PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);
        IsMotionEnabled = level != ArcMotionLevel.Off;
        // AtomUI's popup rewrites Placement to Custom and keeps the requested side in RequestedPlacement.
        var (origin, direction) = OriginFor(RequestedPlacement ?? Placement);
        var fadeOnly = level == ArcMotionLevel.Reduced || IsInstant;
        var offset = direction * EnterOffset;
        OpenMotion = new ArcPopupEnterMotion(origin, offset, EnterScale, fadeOnly,
            duration: fadeOnly ? ArcMotionPresets.Instant : ArcMotionPresets.Fast);
        CloseMotion = IsExitInstant && level == ArcMotionLevel.Full
            ? null
            : new ArcPopupExitMotion(origin, 0.98, level == ArcMotionLevel.Reduced);
    }

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
}

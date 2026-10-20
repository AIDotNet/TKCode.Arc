using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using AtomPopup = AtomUI.Desktop.Controls.Popup;

namespace TKCode.Arc;

/// <summary>
/// The popup that hosts a <see cref="PickerMorphSurface"/>: its content's top-left corner is pinned to the trigger's
/// top-left corner (minus the surface's shadow room and region), so the surface starts exactly over the trigger and
/// grows in place (Arc's sideOffset is 0 here). It never slides or flips, also not while the page scrolls under it:
/// a moved popup would grow from, and fold back onto, a place the trigger is not. Window edges are met by the shapes
/// themselves (<see cref="PickerMorphPlacement.Fit"/>). The surface draws its own shadow and plays its own motion, so
/// the popup adds neither, and closing is driven by the picker (outside presses, Escape) so the shape can fold back
/// first.
/// </summary>
internal sealed class PickerMorphPopup : ArcPopup
{
    static PickerMorphPopup()
    {
        MarginToAnchorProperty.OverrideDefaultValue<PickerMorphPopup>(0);
        IsLightDismissEnabledProperty.OverrideDefaultValue<PickerMorphPopup>(false);
    }

    public PickerMorphPopup()
    {
        PopupRootShadow = default;
        OverlayHostShadow = default;
        // The surface morphs itself: animation priority wins over ArcPopup's own motion setting.
        SetValue(AtomPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
    }

    /// <summary>Offset of the popup content's top-left corner from the trigger's (the negative of the surface's Origin).</summary>
    public Point ContentOffset { get; set; }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        // The anchor is already the trigger's place in the window's client area (MarginToAnchor is 0, so ArcPopup
        // does not grow it); a rectangle in the trigger's own coordinates would pin the surface to the window corner.
        placement.Anchor = PopupAnchor.TopLeft;
        placement.Gravity = PopupGravity.BottomRight;
        placement.Offset = ContentOffset;
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
    }
}

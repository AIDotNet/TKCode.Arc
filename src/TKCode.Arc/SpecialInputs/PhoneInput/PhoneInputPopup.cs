using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace TKCode.Arc;

/// <summary>
/// The phone picker's popup: an <see cref="ArcPopup"/> whose content (the morphing surface plus room for its shadow)
/// starts exactly over the closed country trigger. No margin to the anchor, no shadow of its own (the surface draws it
/// as it morphs), no enter offset or scale, no light dismiss (the field closes on an outside press and folds the
/// surface back). The positioner may slide it into view but never flips it.
/// </summary>
internal sealed class PhoneInputPopup : ArcPopup
{
    static PhoneInputPopup()
    {
        MarginToAnchorProperty.OverrideDefaultValue<PhoneInputPopup>(0);
        IsLightDismissEnabledProperty.OverrideDefaultValue<PhoneInputPopup>(false);
        EnterOffsetProperty.OverrideDefaultValue<PhoneInputPopup>(0);
        EnterScaleProperty.OverrideDefaultValue<PhoneInputPopup>(1);
        IsInstantProperty.OverrideDefaultValue<PhoneInputPopup>(true);
    }

    public PhoneInputPopup()
    {
        PopupRootShadow = default;
        OverlayHostShadow = default;
    }

    /// <summary>Room around the shape inside the popup content (the surface's shadow room).</summary>
    internal Thickness ShadowRoom { get; set; }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        // The anchor is the trigger's place in the shell; the content's top-left sits one shadow room above-left of it.
        placement.Anchor = PopupAnchor.TopLeft;
        placement.Gravity = PopupGravity.BottomRight;
        placement.Offset = new Point(-ShadowRoom.Left, -ShadowRoom.Top);
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY;
    }
}

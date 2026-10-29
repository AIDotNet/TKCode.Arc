using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;

namespace TKCode.Arc;

/// <summary>
/// The anchored popup behind a <see cref="MenuSurface"/>: Core <see cref="ArcPopup"/> (placement, flipping, floating
/// shadow on the popup frame) with the menu families' own enter and exit curves. ArcPopup's P10 motion runs one
/// duration and easing for opacity and transform; Arc menus fade in 160ms while the transform rides a spring
/// (dropdown: 580ms CSS spring, context: snappy, account: smooth without overshoot), so the motions are replaced
/// after ArcPopup configures its own. The transform origin and the travel direction are set from the placement that
/// the positioner actually used (flips included), so the menu always grows from its anchor.
/// </summary>
internal sealed class MenuSurfacePopup : ArcPopup
{
    private static readonly Easing s_accountExit = new SplineEasing(0.4, 0, 1, 1);
    private static readonly Easing s_linear = new LinearEasing();
    private static readonly ArcSpringEasing s_accountSpring = new(SpringSpec.FromVisual(0.3, 0));
    private static readonly ArcSpringEasing s_sheetSpring = new(SpringSpec.FromVisual(0.36, 0));

    private readonly MenuSurfaceEnterMotion _enter = new();
    private readonly MenuSurfaceExitMotion _exit = new();
    private MenuSurfaceVariant _variant;
    private MenuSide _side = MenuSide.Bottom;
    private MenuAlignment _alignment = MenuAlignment.End;
    private Point? _anchorPoint;

    public MenuSurfacePopup()
    {
        IsLightDismissEnabled = true;
        WindowManagerAddShadowHint = false;
        ApplyMotions();
    }

    /// <summary>Point in placement-target coordinates the menu opens at (context menus), or null.</summary>
    public Point? AnchorPoint => _anchorPoint;

    public void Configure(MenuSurfaceVariant variant, MenuSide side, MenuAlignment alignment, double gap, Point? anchorPoint)
    {
        _variant = variant;
        _side = side;
        _alignment = alignment;
        _anchorPoint = anchorPoint;
        MarginToAnchor = anchorPoint is null ? gap : 0;
        HorizontalOffset = 0;
        VerticalOffset = 0;
        var placement = anchorPoint is null ? PlacementFor(side, alignment) : PlacementMode.Pointer;
        if (Placement != PlacementMode.Custom || RequestedPlacement != placement)
        {
            Placement = placement;
        }
        ApplyMotions();
    }

    internal static PlacementMode PlacementFor(MenuSide side, MenuAlignment alignment) => (side, alignment) switch
    {
        (MenuSide.Bottom, MenuAlignment.Start) => PlacementMode.BottomEdgeAlignedLeft,
        (MenuSide.Bottom, MenuAlignment.End) => PlacementMode.BottomEdgeAlignedRight,
        (MenuSide.Bottom, _) => PlacementMode.Bottom,
        (MenuSide.Top, MenuAlignment.Start) => PlacementMode.TopEdgeAlignedLeft,
        (MenuSide.Top, MenuAlignment.End) => PlacementMode.TopEdgeAlignedRight,
        (MenuSide.Top, _) => PlacementMode.Top,
        (MenuSide.Left, MenuAlignment.Start) => PlacementMode.LeftEdgeAlignedTop,
        (MenuSide.Left, MenuAlignment.End) => PlacementMode.LeftEdgeAlignedBottom,
        (MenuSide.Left, _) => PlacementMode.Left,
        (MenuSide.Right, MenuAlignment.Start) => PlacementMode.RightEdgeAlignedTop,
        (MenuSide.Right, MenuAlignment.End) => PlacementMode.RightEdgeAlignedBottom,
        _ => PlacementMode.Right
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // ArcPopup rebuilds its own motions on these changes (and right before every open); put the menu motions back.
        if (change.Property == PlacementProperty || change.Property == EnterOffsetProperty ||
            change.Property == EnterScaleProperty || change.Property == IsInstantProperty ||
            change.Property == ArcMotion.LevelProperty || change.Property == PlacementTargetProperty ||
            (change.Property == IsOpenProperty && change.GetNewValue<bool>()))
        {
            ApplyMotions();
        }
    }

    private void ApplyMotions()
    {
        var level = PlacementTarget is { } target ? ArcMotion.GetEffectiveLevel(target) : ArcMotion.GetEffectiveLevel(this);
        var reduced = level == ArcMotionLevel.Reduced;
        IsMotionEnabled = level != ArcMotionLevel.Off;
        switch (_variant)
        {
            case MenuSurfaceVariant.Context:
            {
                var spring = ArcSpringEasing.For(ArcSpring.Snappy);
                _enter.Set(0.96, spring.SettleDuration, spring, ArcMotionPresets.Fast, ArcEasings.Enter);
                _exit.Set(0.98, 0, ArcMotionPresets.Instant, ArcEasings.Standard);
                break;
            }
            case MenuSurfaceVariant.Account:
                _enter.Set(0.94, s_accountSpring.SettleDuration, s_accountSpring, ArcMotionPresets.Fast, ArcEasings.Enter);
                _exit.Set(0.97, 0, ArcMotionPresets.Instant, s_accountExit);
                break;
            case MenuSurfaceVariant.Sheet:
                // The sheet rises from the bottom edge on a smooth spring and slides back down; it never fades.
                _enter.Set(1, s_sheetSpring.SettleDuration, s_sheetSpring, TimeSpan.FromMilliseconds(1), s_linear);
                _exit.Set(1, 1, TimeSpan.FromMilliseconds(220), s_accountExit);
                break;
            default:
                _enter.Set(0.97, ArcMotionPresets.CssSpring, ArcEasings.CssSpring, ArcMotionPresets.Fast, ArcEasings.Enter);
                _exit.Set(0.985, 0.5, TimeSpan.FromMilliseconds(130), ArcEasings.Standard);
                break;
        }
        if (reduced)
        {
            // Reduced motion: no transform, a short linear fade both ways.
            _enter.Set(1, ArcMotionPresets.Instant, s_linear, ArcMotionPresets.Instant, s_linear);
            _exit.Set(1, 0, ArcMotionPresets.Instant, s_linear);
        }
        _enter.FadeOnly = reduced;
        _exit.FadeOnly = reduced;
        _exit.FadesOut = reduced || _variant != MenuSurfaceVariant.Sheet;
        OpenMotion = _enter;
        CloseMotion = _exit;
    }

    protected override void AdjustPlacementAnchor(CustomPopupPlacement placement)
    {
        if (_variant != MenuSurfaceVariant.Sheet && _anchorPoint is { } point && PlacementTarget is { } target &&
            TopLevel.GetTopLevel(target) is { } topLevel && target.TranslatePoint(point, topLevel) is { } client)
        {
            // Open at the requested point (keyboard or programmatic) instead of the last pointer position.
            placement.AnchorRectangle = new Rect(client, new Size(1, 1));
        }
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool overlay, bool flipX, bool flipY)
    {
        if (_variant == MenuSurfaceVariant.Sheet && PlacementTarget is { } sheetTarget &&
            TopLevel.GetTopLevel(sheetTarget) is { } sheetTopLevel)
        {
            // Full width along the bottom edge of the window; the shadow room hangs below the edge (the positioner
            // places the surface without it).
            var clientSize = sheetTopLevel.ClientSize;
            placement.AnchorRectangle = new Rect(0, clientSize.Height, clientSize.Width, 1);
            placement.Anchor = PopupAnchor.Top;
            placement.Gravity = PopupGravity.Top;
            placement.Offset = default;
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
            var height = placement.PopupSize.Height - shadow.Top - shadow.Bottom;
            _enter.Origin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
            _enter.Offset = new Vector(0, Math.Max(height, 0));
            _exit.Origin = _enter.Origin;
            _exit.Offset = _enter.Offset;
            LastOrigin = _enter.Origin;
            LastTravelDirection = new Vector(0, 1);
            return;
        }
        // Grow from the anchor: the trigger edge (dropdown), the pointer (context) or the avatar (account).
        double originX;
        double originY;
        var offset = default(Vector);
        if (_anchorPoint is not null)
        {
            originX = flipX ? 1 : 0;
            originY = flipY ? 1 : 0;
        }
        else
        {
            var side = _side;
            if (flipY && side is MenuSide.Bottom or MenuSide.Top)
            {
                side = side == MenuSide.Bottom ? MenuSide.Top : MenuSide.Bottom;
            }
            if (flipX && side is MenuSide.Left or MenuSide.Right)
            {
                side = side == MenuSide.Left ? MenuSide.Right : MenuSide.Left;
            }
            var along = _alignment switch
            {
                MenuAlignment.Start => 0.0,
                MenuAlignment.Center => 0.5,
                _ => 1.0
            };
            if (_variant == MenuSurfaceVariant.Account && placement.PopupSize.Width > 0)
            {
                // Arc user menu: the panel scales out of the avatar's centre, 20px in from the aligned edge.
                var width = placement.PopupSize.Width - shadow.Left - shadow.Right;
                along = _alignment switch
                {
                    MenuAlignment.Start => 20 / Math.Max(width, 1),
                    MenuAlignment.Center => 0.5,
                    _ => 1 - 20 / Math.Max(width, 1)
                };
            }
            switch (side)
            {
                case MenuSide.Top:
                    originX = flipX ? 1 - along : along;
                    originY = 1;
                    offset = new Vector(0, 1);
                    break;
                case MenuSide.Left:
                    originX = 1;
                    originY = flipY ? 1 - along : along;
                    offset = new Vector(1, 0);
                    break;
                case MenuSide.Right:
                    originX = 0;
                    originY = flipY ? 1 - along : along;
                    offset = new Vector(-1, 0);
                    break;
                default:
                    originX = flipX ? 1 - along : along;
                    originY = 0;
                    offset = new Vector(0, -1);
                    break;
            }
        }
        var origin = new RelativePoint(Math.Clamp(originX, 0, 1), Math.Clamp(originY, 0, 1), RelativeUnit.Relative);
        // Only the dropdown family travels toward its trigger (5px enter, half of it on exit).
        var travel = _variant == MenuSurfaceVariant.Dropdown ? offset * 5 : default;
        _enter.Origin = origin;
        _enter.Offset = travel;
        _exit.Origin = origin;
        _exit.Offset = travel * _exit.OffsetFactor;
        LastOrigin = origin;
        LastTravelDirection = offset;
    }

    /// <summary>Origin chosen by the last placement (tests and stagger direction).</summary>
    internal RelativePoint LastOrigin { get; private set; }

    /// <summary>Unit vector from the menu toward its anchor (0 for point anchors).</summary>
    internal Vector LastTravelDirection { get; private set; }
}

/// <summary>Menu enter: scale (and, for dropdowns, 5px of travel) on a spring curve while the opacity fades quickly.</summary>
internal sealed class MenuSurfaceEnterMotion : ArcPopupMotion
{
    public MenuSurfaceEnterMotion()
    {
        Scale = 0.97;
    }

    public TimeSpan TransformDuration { get; private set; } = ArcMotionPresets.CssSpring;
    public Easing TransformEasing { get; private set; } = ArcEasings.CssSpring;
    public TimeSpan OpacityDuration { get; private set; } = ArcMotionPresets.Fast;
    public Easing OpacityEasing { get; private set; } = ArcEasings.Enter;

    public void Set(double scale, TimeSpan transformDuration, Easing transformEasing, TimeSpan opacityDuration, Easing opacityEasing)
    {
        Scale = scale;
        TransformDuration = transformDuration;
        TransformEasing = transformEasing;
        OpacityDuration = opacityDuration;
        OpacityEasing = opacityEasing;
    }

    public override MotionSpec OpacitySpec => MotionSpec.Tween(OpacityDuration, OpacityEasing);

    public override MotionSpec TransformSpec => MotionSpec.Tween(FadeOnly ? OpacityDuration : TransformDuration, TransformEasing);
}

/// <summary>Menu exit: shorter than the enter; fades and shrinks slightly toward the anchor.</summary>
internal sealed class MenuSurfaceExitMotion : ArcPopupMotion
{
    public MenuSurfaceExitMotion()
    {
        Scale = 0.98;
    }

    public double OffsetFactor { get; private set; }

    /// <summary>The exit ends transparent (false: a sheet slides away opaque).</summary>
    public bool FadesOut
    {
        get => Fades;
        set => Fades = value;
    }

    public TimeSpan ExitDuration { get; private set; } = ArcMotionPresets.Instant;
    public Easing ExitEasing { get; private set; } = ArcEasings.Standard;

    public void Set(double scale, double offsetFactor, TimeSpan duration, Easing easing)
    {
        Scale = scale;
        OffsetFactor = offsetFactor;
        ExitDuration = duration;
        ExitEasing = easing;
    }

    public override MotionSpec OpacitySpec => MotionSpec.Tween(ExitDuration, ExitEasing);

    public override MotionSpec TransformSpec => OpacitySpec;
}

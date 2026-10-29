using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The popup of the answer's menus and popover (Core <see cref="ArcPopup"/>: overlay hosting, floating shadow, flipping).
/// A menu or popover opens against its button on the side the owner asks for (above, left edges aligned, for the answer);
/// a submenu is placed against its parent menu's edge instead of its own row: <see cref="SubmenuAnchor"/> gives the
/// rectangle (the parent menu's width, the row's height), so the submenu lands right of the menu (left when there is no
/// room) with its top a fixed distance above the row.
/// </summary>
internal sealed class AiActionsPopup : ArcPopup
{
    public AiActionsPopup(bool isSubmenu)
    {
        IsSubmenu = isSubmenu;
        IsLightDismissEnabled = false;
        WindowManagerAddShadowHint = false;
        EnterOffset = 0;
        EnterScale = 1;
        // The presence plays the motion; animation priority wins over the local value ArcPopup sets for its own.
        SetValue(ArcPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
    }

    /// <summary>True for a submenu.</summary>
    public bool IsSubmenu { get; }

    /// <summary>The rectangle (in top-level coordinates) a submenu is placed against; null keeps the target's bounds.</summary>
    public Func<TopLevel, Rect?>? SubmenuAnchor { get; set; }

    /// <summary>The submenu opens on the menu's left side.</summary>
    public bool OpensLeft { get; private set; }

    /// <summary>Places a submenu against <paramref name="target"/> on the chosen side, <paramref name="verticalOffset"/> from the row's top.</summary>
    public void ConfigureSubmenu(Control target, bool opensLeft, double verticalOffset)
    {
        OpensLeft = opensLeft;
        PlacementTarget = target;
        MarginToAnchor = 0;
        HorizontalOffset = 0;
        VerticalOffset = verticalOffset;
        var placement = opensLeft ? PlacementMode.LeftEdgeAlignedTop : PlacementMode.RightEdgeAlignedTop;
        if (Placement != PlacementMode.Custom || RequestedPlacement != placement)
        {
            Placement = placement;
        }
    }

    /// <summary>−1 when the surface lines up with its anchor's left edge, 1 with its right edge, 0 centered.</summary>
    public int HorizontalAlign => (RequestedPlacement ?? Placement) switch
    {
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.TopEdgeAlignedLeft => -1,
        PlacementMode.BottomEdgeAlignedRight or PlacementMode.TopEdgeAlignedRight => 1,
        _ => 0
    };

    /// <summary>True when the surface sits above its anchor: it asked for the top and fitted, or asked for the bottom and flipped.</summary>
    public bool IsAbove => (RequestedPlacement ?? Placement) is PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or
        PlacementMode.TopEdgeAlignedRight
        ? !IsVerticalFlipped
        : IsVerticalFlipped;

    protected override void AdjustPlacementAnchor(CustomPopupPlacement placement)
    {
        if (IsSubmenu && PlacementTarget is { } target && TopLevel.GetTopLevel(target) is { } topLevel &&
            SubmenuAnchor?.Invoke(topLevel) is { } rect)
        {
            placement.AnchorRectangle = rect;
        }
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        if (IsSubmenu)
        {
            // The side is chosen before opening (right, else left); near the window's bottom the submenu slides up to fit
            // instead of flipping to end at its row, and in a window too narrow for either side it slides back inside,
            // over the menu, instead of leaving the window.
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.SlideY |
                                             PopupPositionerConstraintAdjustment.ResizeY;
        }
    }
}

/// <summary>
/// Shows a surface in its <see cref="AiActionsPopup"/> with the motion of Arc's AI menus and popovers (ai.md 4.3), played on
/// the popup's motion actor so the floating shadow moves with the surface. Enter: opacity 0 → 1 in 160ms on the enter curve
/// while the transform goes from <see cref="Travel"/> px toward the anchor at scale 0.97 to rest on Arc's 580ms CSS spring
/// curve. Exit: 130ms standard to 2.5px and scale 0.985 while fading out. A menu travels vertically from the edge facing
/// its anchor (origin: that edge's corner on the aligned side); a submenu travels horizontally from the edge facing its
/// menu. A close during the enter (or an open during the exit) continues from the values on screen. Reduced motion: a
/// 120ms linear fade, no transform; Off: instant.
/// <para>
/// Code-driven (MotionValue) rather than the popup's own motions: opacity and transform run on two different curves and
/// durations, a reversal must continue from the values on screen (the popup's motions restart from fixed start values), and the
/// popup may only close after the exit has played.
/// </para>
/// </summary>
internal sealed class AiActionsPopupPresence : IDisposable
{
    internal const double EnterScale = 0.97;
    internal const double ExitTravel = 2.5;
    internal const double ExitScale = 0.985;
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(130);
    private static readonly Easing s_linear = new LinearEasing();

    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<Point> _pose;
    private readonly MatrixTransform _transform = new();
    private readonly MatrixTransform _fallbackTransform = new();
    private ArcPopupFrame? _actor;
    private bool _closing;
    private int _generation;

    /// <param name="surface">The surface the popup shows.</param>
    /// <param name="popup">The popup (placed by the owner before <see cref="Show"/>).</param>
    /// <param name="travel">How far the surface starts toward its anchor (5 for a menu or popover, 6 for a submenu).</param>
    public AiActionsPopupPresence(Control surface, AiActionsPopup popup, double travel)
    {
        Surface = surface;
        Popup = popup;
        Travel = travel;
        Popup.Child = surface;
        Popup.Opened += OnPopupOpened;
        _opacity = new MotionValue<double>(surface, 0, MotionVectors.Double);
        // X: travel toward the anchor in px, Y: scale.
        _pose = new MotionValue<Point>(surface, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        surface.SizeChanged += (_, _) => Apply();
    }

    /// <summary>The popup that shows the surface.</summary>
    public AiActionsPopup Popup { get; }

    /// <summary>The surface (a menu or the popover).</summary>
    public Control Surface { get; }

    /// <summary>How far the surface starts toward its anchor.</summary>
    public double Travel { get; }

    /// <summary>Open and not leaving.</summary>
    public bool IsShown => Popup.IsOpen && !_closing;

    /// <summary>Playing its exit.</summary>
    public bool IsClosing => Popup.IsOpen && _closing;

    /// <summary>Current opacity of the surface (tests).</summary>
    internal double CurrentOpacity => _opacity.Value;

    /// <summary>Current travel (px toward the anchor) and scale of the surface (tests).</summary>
    internal Point CurrentPose => _pose.Value;

    /// <summary>The exit finished and the popup closed.</summary>
    public event EventHandler? Hidden;

    /// <summary>Opens the popup (already placed by the owner) and plays the enter; reverses a running exit.</summary>
    public void Show()
    {
        if (Popup.PlacementTarget is not { } target || TopLevel.GetTopLevel(target) is null)
        {
            return;
        }
        var reopening = Popup.IsOpen && _closing;
        if (Popup.IsOpen && !_closing)
        {
            return;
        }
        _generation++;
        _closing = false;
        var level = ArcMotion.GetEffectiveLevel(target);
        if (!reopening)
        {
            _opacity.Jump(level == ArcMotionLevel.Off ? 1 : 0);
            _pose.Jump(level == ArcMotionLevel.Full ? new Point(Travel, EnterScale) : new Point(0, 1));
            Popup.IsOpen = true;
            if (!Popup.IsOpen)
            {
                return;
            }
        }
        Apply();
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _ = _pose.AnimateTo(new Point(0, 1), MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
                break;
            case ArcMotionLevel.Reduced:
                _pose.Jump(new Point(0, 1));
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                _opacity.Jump(1);
                _pose.Jump(new Point(0, 1));
                break;
        }
    }

    /// <summary>Plays the exit and closes the popup afterwards (at once when <paramref name="immediate"/>).</summary>
    public void Hide(bool immediate = false)
    {
        if (!Popup.IsOpen)
        {
            return;
        }
        var target = Popup.PlacementTarget;
        var level = target is not null && TopLevel.GetTopLevel(target) is not null ? ArcMotion.GetEffectiveLevel(target) : ArcMotionLevel.Off;
        if (immediate || level == ArcMotionLevel.Off || TopLevel.GetTopLevel(Surface) is null)
        {
            Finish();
            return;
        }
        if (_closing)
        {
            return;
        }
        _closing = true;
        var generation = ++_generation;
        Task<bool> task;
        if (level == ArcMotionLevel.Full)
        {
            var exit = MotionSpec.Tween(ExitDuration, ArcEasings.Standard);
            _ = _pose.AnimateTo(new Point(ExitTravel, ExitScale), exit);
            task = _opacity.AnimateTo(0, exit);
        }
        else
        {
            task = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
        }
        if (task.IsCompleted)
        {
            Finish();
        }
        else
        {
            _ = FinishWhenDone(task, generation);
        }
    }

    private async Task FinishWhenDone(Task<bool> task, int generation)
    {
        var done = await task;
        if (done && generation == _generation && _closing)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _generation++;
        _closing = false;
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
        }
        _opacity.Jump(0);
        _pose.Jump(new Point(0, 1));
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        _actor = Surface.FindAncestorOfType<ArcPopupFrame>();
        if (_actor is not null)
        {
            _actor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _actor.RenderTransform = _transform;
        }
        Apply();
    }

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity.Value, 0, 1);
        var pose = _pose.Value;
        var matrix = Matrix.Identity;
        if (pose != new Point(0, 1))
        {
            var (origin, toward) = OriginAndDirection();
            // The surface starts toward its anchor: a menu above its button starts lower, a submenu on the right starts left.
            var shift = toward * pose.X;
            matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(pose.Y, pose.Y) *
                     Matrix.CreateTranslation(origin.X + shift.X, origin.Y + shift.Y);
        }
        if (_actor is { } actor && TopLevel.GetTopLevel(actor) is not null)
        {
            actor.Opacity = opacity;
            _transform.Matrix = matrix;
            if (Surface.RenderTransform is not null || Surface.Opacity < 1)
            {
                Surface.Opacity = 1;
                Surface.RenderTransform = null;
            }
        }
        else
        {
            // Before the popup frame exists the surface carries the motion itself.
            Surface.Opacity = opacity;
            Surface.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _fallbackTransform.Matrix = matrix;
            Surface.RenderTransform = _fallbackTransform;
        }
    }

    /// <summary>Transform origin in actor coordinates (the corner facing the anchor) and the unit vector toward the anchor.</summary>
    private (Point Origin, Vector Toward) OriginAndDirection()
    {
        var size = Surface.Bounds.Size;
        var topLeft = _actor is { } actor && Surface.TranslatePoint(default, actor) is { } p ? p : default;
        if (Popup.IsSubmenu)
        {
            var left = Popup.OpensLeft;
            return (new Point(topLeft.X + (left ? size.Width : 0), topLeft.Y), new Vector(left ? 1 : -1, 0));
        }
        var above = Popup.IsAbove;
        var x = Popup.HorizontalAlign switch
        {
            < 0 => topLeft.X,
            > 0 => topLeft.X + size.Width,
            _ => topLeft.X + size.Width / 2
        };
        return (new Point(x, topLeft.Y + (above ? size.Height : 0)), new Vector(0, above ? 1 : -1));
    }

    public void Dispose()
    {
        Hide(immediate: true);
        _opacity.Dispose();
        _pose.Dispose();
    }
}

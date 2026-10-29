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
/// The popup of the model menu or of one of its submenus (Core <see cref="ArcPopup"/>: overlay hosting, floating shadow).
/// The menu sits on the side of its anchor that <c>ModelMenu.Placement</c> asks for (below and centered by default; the
/// composer's: above, right edges aligned) and keeps <see cref="CollisionPadding"/> from the window's left and right
/// edges. A submenu is placed against the menu's edge instead of its own anchor row: <see cref="SubmenuAnchor"/> gives
/// the rectangle (the menu's width, the row's height), so the submenu lands right of the menu (or left, when
/// <see cref="OpensLeft"/>) with its top 7px above the row (list padding and border), and its first row lines up.
/// </summary>
internal sealed class ModelMenuPopup : ArcPopup
{
    public ModelMenuPopup(bool isSubmenu)
    {
        IsSubmenu = isSubmenu;
        IsLightDismissEnabled = false;
        WindowManagerAddShadowHint = false;
        EnterOffset = 0;
        EnterScale = 1;
        // The presence plays the motion; animation priority wins over the local value ArcPopup sets for its own.
        SetValue(ArcPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
    }

    public bool IsSubmenu { get; }

    /// <summary>Space the menu keeps from the window's left and right edges (12).</summary>
    public double CollisionPadding { get; set; } = 12;

    /// <summary>The rectangle (in top-level coordinates) a submenu is placed against; null keeps the target's bounds.</summary>
    public Func<TopLevel, Rect?>? SubmenuAnchor { get; set; }

    /// <summary>The submenu opens on the menu's left side (no room on the right).</summary>
    public bool OpensLeft { get; private set; }

    /// <summary>Places a submenu against <paramref name="target"/> on the chosen side.</summary>
    public void ConfigureSubmenu(Control target, bool opensLeft, double gap, double verticalOffset)
    {
        OpensLeft = opensLeft;
        PlacementTarget = target;
        MarginToAnchor = gap;
        HorizontalOffset = 0;
        VerticalOffset = verticalOffset;
        var placement = opensLeft ? PlacementMode.LeftEdgeAlignedTop : PlacementMode.RightEdgeAlignedTop;
        if (Placement != PlacementMode.Custom || RequestedPlacement != placement)
        {
            Placement = placement;
        }
    }

    /// <summary>
    /// How the menu lines up with its anchor along the anchor's edge, from the requested placement: −1 left edges
    /// aligned, 1 right edges aligned, 0 centered.
    /// </summary>
    public int HorizontalAlign => (RequestedPlacement ?? Placement) switch
    {
        PlacementMode.BottomEdgeAlignedLeft or PlacementMode.TopEdgeAlignedLeft => -1,
        PlacementMode.BottomEdgeAlignedRight or PlacementMode.TopEdgeAlignedRight => 1,
        _ => 0
    };

    /// <summary>True when the menu sits above its anchor: it asked for the top and fitted, or asked for the bottom and flipped.</summary>
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
            // instead of flipping to end at its row (Radix shifts submenus along their side).
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideY | PopupPositionerConstraintAdjustment.ResizeY;
            return;
        }
        if (!isOverlayHost || PlacementTarget is not { } target || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }
        var width = placement.PopupSize.Width - shadow.Left - shadow.Right;
        var client = topLevel.ClientSize.Width;
        if (width <= 0 || width + 2 * CollisionPadding > client + 0.5)
        {
            return;
        }
        // Centered on the anchor (or edge-aligned with it), then slid back inside the padded window (Radix collisionPadding).
        var anchor = placement.AnchorRectangle;
        var left = HorizontalAlign switch
        {
            < 0 => anchor.X,
            > 0 => anchor.Right - width,
            _ => anchor.X + anchor.Width / 2 - width / 2
        } + placement.Offset.X;
        var clamped = Math.Clamp(left, CollisionPadding, client - CollisionPadding - width);
        if (Math.Abs(clamped - left) > 0.01)
        {
            placement.Offset = placement.Offset.WithX(placement.Offset.X + clamped - left);
        }
    }
}

/// <summary>
/// Shows a <see cref="ModelMenuPanel"/> in its <see cref="ModelMenuPopup"/> with the model menu's motion, played on the
/// popup's motion actor so the floating shadow moves with the surface. Enter: opacity 0 → 1 in 160ms on the enter curve
/// while the transform goes from 6px toward the anchor at scale 0.97 to rest on Arc's 580ms CSS spring curve. Exit:
/// 130ms standard to 3px and scale 0.985 while fading out. The menu travels vertically from the edge facing its anchor
/// (origin: that edge's middle, or its corner on the anchor's side when edge-aligned, like the composer's bottom-right);
/// a submenu travels horizontally from the edge facing the menu (top corner as origin). A close during the enter (or an
/// open during the exit) continues from the current values. Reduced motion: a 120ms linear fade, no transform; Off:
/// instant.
/// <para>
/// Code-driven (MotionValue) rather than the popup's own motions: opacity and transform run on two different curves and
/// durations, a reversal must continue from the values on screen (the popup's motions restart from fixed start values), and
/// the popup may only close after the exit has played.
/// </para>
/// </summary>
internal sealed class ModelMenuPresence : IDisposable
{
    internal const double EnterTravel = 6;
    internal const double EnterScale = 0.97;
    internal const double ExitTravel = 3;
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

    public ModelMenuPresence(ModelMenuPanel panel, ModelMenuPopup popup)
    {
        Panel = panel;
        Popup = popup;
        Popup.Child = panel;
        Popup.Opened += OnPopupOpened;
        _opacity = new MotionValue<double>(panel, 0, MotionVectors.Double);
        // X: travel toward the anchor in px, Y: scale.
        _pose = new MotionValue<Point>(panel, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        panel.SizeChanged += (_, _) => Apply();
    }

    public ModelMenuPopup Popup { get; }

    public ModelMenuPanel Panel { get; }

    /// <summary>Open and not leaving.</summary>
    public bool IsShown => Popup.IsOpen && !_closing;

    /// <summary>Playing its exit.</summary>
    public bool IsClosing => Popup.IsOpen && _closing;

    /// <summary>Current opacity of the surface (tests).</summary>
    internal double CurrentOpacity => _opacity.Value;

    /// <summary>Current travel (px toward the anchor) and scale of the surface (tests).</summary>
    internal Point CurrentPose => _pose.Value;

    /// <summary>The popup opened (the surface is in the window).</summary>
    public event EventHandler? Shown;

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
            _pose.Jump(level == ArcMotionLevel.Full ? new Point(EnterTravel, EnterScale) : new Point(0, 1));
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
        if (immediate || level == ArcMotionLevel.Off || TopLevel.GetTopLevel(Panel) is null)
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
        _actor = Panel.FindAncestorOfType<ArcPopupFrame>();
        if (_actor is not null)
        {
            _actor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _actor.RenderTransform = _transform;
        }
        Apply();
        Shown?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity.Value, 0, 1);
        var pose = _pose.Value;
        var matrix = Matrix.Identity;
        if (pose != new Point(0, 1))
        {
            var (origin, toward) = OriginAndDirection();
            // The surface starts toward its anchor: a menu below the pill starts higher, a submenu on the right starts left.
            var shift = toward * pose.X;
            matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(pose.Y, pose.Y) *
                     Matrix.CreateTranslation(origin.X + shift.X, origin.Y + shift.Y);
        }
        if (_actor is { } actor && TopLevel.GetTopLevel(actor) is not null)
        {
            actor.Opacity = opacity;
            _transform.Matrix = matrix;
            if (Panel.RenderTransform is not null || Panel.Opacity < 1)
            {
                Panel.Opacity = 1;
                Panel.RenderTransform = null;
            }
        }
        else
        {
            // Before the popup frame exists the panel carries the motion itself.
            Panel.Opacity = opacity;
            Panel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _fallbackTransform.Matrix = matrix;
            Panel.RenderTransform = _fallbackTransform;
        }
    }

    /// <summary>Transform origin in actor coordinates (the edge facing the anchor) and the unit vector toward the anchor.</summary>
    private (Point Origin, Vector Toward) OriginAndDirection()
    {
        var size = Panel.Bounds.Size;
        var topLeft = _actor is { } actor && Panel.TranslatePoint(default, actor) is { } p ? p : default;
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

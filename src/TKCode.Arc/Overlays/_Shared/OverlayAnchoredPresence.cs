using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Which Arc anchored motion an <see cref="OverlayAnchoredPresence"/> plays.</summary>
internal enum OverlayAnchoredMotion
{
    /// <summary>Popover: 5px toward the trigger from scale 0.97, CSS spring transform (580ms) and a fast fade; leaves at half travel / 0.98.</summary>
    Popover,

    /// <summary>Hover card: 4px and scale 0.96 on one smooth spring with a separate fade; leaves in 120ms.</summary>
    HoverCard
}

/// <summary>Where the surface sits relative to its anchor.</summary>
internal enum OverlayAnchorSide
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>Alignment of the surface along the anchor edge.</summary>
internal enum OverlayAnchorAlign
{
    Start,
    Center,
    End
}

/// <summary>
/// Shared anchored-surface presence for Popover and HoverCard (P10). Hosts the surface in an <see cref="ArcPopup"/>
/// (placement, flipping, Arc floating shadow) but plays Arc's motion itself on the popup's motion actor, so the shadow
/// moves with the surface, the transform origin follows the side the popup actually landed on after flipping, and an
/// open and a close interrupt each other and reverse from where the surface is. ControlTheme transitions cannot express
/// this: the motion has to run on the popup's frame (outside the surface's template), keep separate opacity and
/// transform timings, and gate the popup removal on the exit.
/// </summary>
internal sealed class OverlayAnchoredPresence : IDisposable
{

    private readonly OverlayAnchoredMotion _motion;
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<Point> _pose;
    private readonly MatrixTransform _transform = new();
    private ArcPopupFrame? _actor;
    private Control? _anchor;
    private TopLevel? _anchorTopLevel;
    private OverlayAnchorSide _side;
    private OverlayAnchorAlign _align;
    private bool _still;
    private bool _closing;
    private int _generation;

    public OverlayAnchoredPresence(Control surface, OverlayAnchoredMotion motion)
    {
        Surface = surface;
        _motion = motion;
        Popup = new ArcPopup
        {
            Child = surface,
            IsLightDismissEnabled = false,
            WindowManagerAddShadowHint = false
        };
        // The motion runs here, not in the popup's own motion: animation priority wins over ArcPopup's own setting.
        Popup.SetValue(ArcPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
        Popup.Opened += OnPopupOpened;
        Popup.PositionFlipped += (_, _) => UpdateSide();
        _opacity = new MotionValue<double>(surface, 0, MotionVectors.Double);
        _pose = new MotionValue<Point>(surface, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        surface.SizeChanged += (_, _) =>
        {
            UpdateAlignmentOffset();
            Apply();
        };
    }

    public ArcPopup Popup { get; }

    public Control Surface { get; }

    /// <summary>The anchor of the current (or last) showing.</summary>
    public Control? Anchor => _anchor;

    /// <summary>Open and not leaving.</summary>
    public bool IsShown => Popup.IsOpen && !_closing;

    /// <summary>The exit finished and the popup closed.</summary>
    public event EventHandler? Hidden;

    /// <summary>A press landed outside the surface and the anchor while shown.</summary>
    public event EventHandler<PointerPressedEventArgs>? OutsidePressed;

    /// <summary>Escape was pressed while shown (from the surface or the anchor's window).</summary>
    public event EventHandler<KeyEventArgs>? EscapePressed;

    /// <summary>
    /// Shows the surface anchored to <paramref name="anchor"/> on <paramref name="side"/> with <paramref name="align"/>,
    /// <paramref name="margin"/> px away; reverses a running exit. <paramref name="instant"/> (hover card warm-up)
    /// fades only, without travel or scale.
    /// </summary>
    public void Show(Control anchor, OverlayAnchorSide side, OverlayAnchorAlign align, double margin, bool instant = false)
    {
        if (TopLevel.GetTopLevel(anchor) is null)
        {
            return;
        }
        var reopening = Popup.IsOpen && _closing && ReferenceEquals(anchor, _anchor);
        var alreadyShown = Popup.IsOpen && !_closing && ReferenceEquals(anchor, _anchor);
        _generation++;
        _closing = false;
        _side = side;
        _align = align;
        if (!ReferenceEquals(anchor, _anchor) && Popup.IsOpen)
        {
            Popup.IsOpen = false;
        }
        _anchor = anchor;
        Popup.PlacementTarget = anchor;
        Popup.MarginToAnchor = margin;
        // Start / End alignment is applied as an offset from the centred placement (the surface's size along the edge
        // is known here, and a centred placement flips only across the anchor).
        // RequestedPlacement: the side ArcPopup resolves (margin to anchor, flipping).
        Popup.RequestedPlacement = ToPlacement(side, OverlayAnchorAlign.Center);
        UpdateAlignmentOffset();
        HookAnchor(anchor);
        var level = ArcMotion.GetEffectiveLevel(anchor);
        if (!reopening && !alreadyShown)
        {
            // Decided at mount: a surface revived mid-exit keeps the path it came in on.
            _still = instant || level != ArcMotionLevel.Full;
            _opacity.Jump(0);
            _pose.Jump(_still ? new Point(0, 1) : StartPose());
            Popup.IsOpen = true;
        }
        UpdateAlignmentOffset();
        UpdateSide();
        Apply();
        if (alreadyShown)
        {
            // Placement changed while open: reposition only.
            return;
        }
        var fade = level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(_motion == OverlayAnchoredMotion.HoverCard ? TimeSpan.FromMilliseconds(150) : ArcMotionPresets.Instant, ArcEasings.Standard)
            : _motion == OverlayAnchoredMotion.HoverCard && _still
                ? MotionSpec.Tween(TimeSpan.FromMilliseconds(90), ArcEasings.Enter)
                : MotionSpec.Tween(_motion == OverlayAnchoredMotion.HoverCard ? TimeSpan.FromMilliseconds(160) : ArcMotionPresets.Fast, ArcEasings.Enter);
        _ = _opacity.AnimateTo(1, fade);
        if (_still)
        {
            _pose.Jump(new Point(0, 1));
        }
        else
        {
            var spec = _motion == OverlayAnchoredMotion.HoverCard
                ? MotionSpec.Spring(ArcSpring.Smooth)
                : MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring);
            _ = _pose.AnimateTo(new Point(0, 1), spec);
        }
    }

    /// <summary>Plays the exit and closes the popup afterwards (at once when <paramref name="immediate"/>).</summary>
    public void Hide(bool immediate = false)
    {
        if (!Popup.IsOpen)
        {
            return;
        }
        if (immediate || _anchor is null || TopLevel.GetTopLevel(_anchor) is null)
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
        var level = ArcMotion.GetEffectiveLevel(_anchor);
        var hover = _motion == OverlayAnchoredMotion.HoverCard;
        var duration = level == ArcMotionLevel.Reduced
            ? hover ? TimeSpan.FromMilliseconds(100) : ArcMotionPresets.Instant
            : hover ? TimeSpan.FromMilliseconds(120) : ArcMotionPresets.Fast;
        var fade = MotionSpec.Tween(duration, ArcEasings.Standard);
        if (!_still && level == ArcMotionLevel.Full)
        {
            var end = hover ? StartPose() : new Point(StartPose().X / 2, 0.98);
            _ = _pose.AnimateTo(end, MotionSpec.Tween(duration, ArcEasings.Standard));
        }
        var task = _opacity.AnimateTo(0, fade);
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
        UnhookAnchor();
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
        }
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shifts the centred popup so its start or end edge lines up with the anchor's.</summary>
    private void UpdateAlignmentOffset()
    {
        if (_anchor is not { } anchor)
        {
            return;
        }
        var size = Surface.Bounds.Size;
        if (size.Width <= 0 && size.Height <= 0)
        {
            size = Surface.DesiredSize;
        }
        var direction = _align switch { OverlayAnchorAlign.Start => 1, OverlayAnchorAlign.End => -1, _ => 0 };
        if (_side is OverlayAnchorSide.Top or OverlayAnchorSide.Bottom)
        {
            var dx = direction * (size.Width - anchor.Bounds.Width) / 2;
            if (Math.Abs(Popup.HorizontalOffset - dx) > 0.5)
            {
                Popup.HorizontalOffset = dx;
            }
            Popup.VerticalOffset = 0;
        }
        else
        {
            var dy = direction * (size.Height - anchor.Bounds.Height) / 2;
            if (Math.Abs(Popup.VerticalOffset - dy) > 0.5)
            {
                Popup.VerticalOffset = dy;
            }
            Popup.HorizontalOffset = 0;
        }
    }

    private Point StartPose() => _motion == OverlayAnchoredMotion.HoverCard ? new Point(4, 0.96) : new Point(5, 0.97);

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        _actor = Surface.FindAncestorOfType<ArcPopupFrame>();
        if (_actor is not null)
        {
            _actor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _actor.RenderTransform = _transform;
        }
        UpdateSide();
        Apply();
    }

    private void UpdateSide()
    {
        // The positioner flips the requested side when it does not fit; the motion follows the side it landed on.
        var side = _side;
        if (Popup.IsVerticalFlipped)
        {
            side = side switch { OverlayAnchorSide.Top => OverlayAnchorSide.Bottom, OverlayAnchorSide.Bottom => OverlayAnchorSide.Top, _ => side };
        }
        if (Popup.IsHorizontalFlipped)
        {
            side = side switch { OverlayAnchorSide.Left => OverlayAnchorSide.Right, OverlayAnchorSide.Right => OverlayAnchorSide.Left, _ => side };
        }
        ActualSide = side;
    }

    /// <summary>The side the popup actually landed on.</summary>
    public OverlayAnchorSide ActualSide { get; private set; }

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity.Value, 0, 1);
        var pose = _pose.Value;
        var travel = pose.X;
        var scale = pose.Y;
        // The surface starts toward its trigger: a card below the trigger starts higher.
        var offset = ActualSide switch
        {
            OverlayAnchorSide.Top => new Vector(0, travel),
            OverlayAnchorSide.Left => new Vector(travel, 0),
            OverlayAnchorSide.Right => new Vector(-travel, 0),
            _ => new Vector(0, -travel)
        };
        var origin = Origin();
        var matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(scale, scale) *
                     Matrix.CreateTranslation(origin.X + offset.X, origin.Y + offset.Y);
        if (_actor is { } actor && TopLevel.GetTopLevel(actor) is not null)
        {
            actor.Opacity = opacity;
            _transform.Matrix = matrix;
            if (Surface.RenderTransform is not null || Surface.Opacity < 1)
            {
                // Values written before the popup frame existed.
                Surface.Opacity = 1;
                Surface.RenderTransform = null;
            }
        }
        else
        {
            Surface.Opacity = opacity;
            Surface.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            Surface.RenderTransform = new MatrixTransform(matrix);
        }
    }

    /// <summary>Transform origin in actor coordinates: the surface corner or edge nearest the trigger.</summary>
    private Point Origin()
    {
        var bounds = Surface.Bounds;
        var topLeft = new Point(0, 0);
        if (_actor is { } actor && Surface.TranslatePoint(default, actor) is { } p)
        {
            topLeft = p;
        }
        var along = _align switch { OverlayAnchorAlign.Center => 0.5, OverlayAnchorAlign.End => 1.0, _ => 0.0 };
        var (fx, fy) = ActualSide switch
        {
            OverlayAnchorSide.Top => (along, 1.0),
            OverlayAnchorSide.Left => (1.0, along),
            OverlayAnchorSide.Right => (0.0, along),
            _ => (along, 0.0)
        };
        return new Point(topLeft.X + bounds.Width * fx, topLeft.Y + bounds.Height * fy);
    }

    internal static PlacementMode ToPlacement(OverlayAnchorSide side, OverlayAnchorAlign align) => (side, align) switch
    {
        (OverlayAnchorSide.Top, OverlayAnchorAlign.Start) => PlacementMode.TopEdgeAlignedLeft,
        (OverlayAnchorSide.Top, OverlayAnchorAlign.End) => PlacementMode.TopEdgeAlignedRight,
        (OverlayAnchorSide.Top, _) => PlacementMode.Top,
        (OverlayAnchorSide.Left, OverlayAnchorAlign.Start) => PlacementMode.LeftEdgeAlignedTop,
        (OverlayAnchorSide.Left, OverlayAnchorAlign.End) => PlacementMode.LeftEdgeAlignedBottom,
        (OverlayAnchorSide.Left, _) => PlacementMode.Left,
        (OverlayAnchorSide.Right, OverlayAnchorAlign.Start) => PlacementMode.RightEdgeAlignedTop,
        (OverlayAnchorSide.Right, OverlayAnchorAlign.End) => PlacementMode.RightEdgeAlignedBottom,
        (OverlayAnchorSide.Right, _) => PlacementMode.Right,
        (_, OverlayAnchorAlign.Start) => PlacementMode.BottomEdgeAlignedLeft,
        (_, OverlayAnchorAlign.End) => PlacementMode.BottomEdgeAlignedRight,
        _ => PlacementMode.Bottom
    };

    #region Outside press and Escape

    private void HookAnchor(Control anchor)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (ReferenceEquals(topLevel, _anchorTopLevel))
        {
            return;
        }
        UnhookAnchor();
        _anchorTopLevel = topLevel;
        if (topLevel is null)
        {
            return;
        }
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel, true);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
        Surface.AddHandler(InputElement.KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Bubble);
    }

    private void UnhookAnchor()
    {
        if (_anchorTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPressed);
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
            Surface.RemoveHandler(InputElement.KeyDownEvent, OnSurfaceKeyDown);
            _anchorTopLevel = null;
        }
    }

    private void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsShown || e.Source is not Visual source)
        {
            return;
        }
        if (IsWithin(Surface, source) || (_anchor is { } anchor && IsWithin(anchor, source)))
        {
            return;
        }
        // A press on a modal's scrim only dismisses this (topmost) layer, not the modal underneath.
        if (source.GetVisualParent() is ModalHost host && ReferenceEquals(host.Scrim, source))
        {
            e.Handled = true;
        }
        OutsidePressed?.Invoke(this, e);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && IsShown)
        {
            EscapePressed?.Invoke(this, e);
        }
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the surface stay within the popup host, so Escape is caught here first.
        if (!e.Handled && e.Key == Key.Escape && IsShown)
        {
            EscapePressed?.Invoke(this, e);
        }
    }

    private static bool IsWithin(Visual root, Visual element)
    {
        if (ReferenceEquals(root, element) || root.IsVisualAncestorOf(element))
        {
            return true;
        }
        // Nested popups opened from the surface are its logical descendants.
        return element is ILogical logical && ((ILogical)root).IsLogicalAncestorOf(logical);
    }

    #endregion

    public void Dispose()
    {
        Hide(immediate: true);
        UnhookAnchor();
        _opacity.Dispose();
        _pose.Dispose();
    }
}

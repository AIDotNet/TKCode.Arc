// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AtomUI.MotionScene;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using AtomPopup = AtomUI.Desktop.Controls.Popup;

namespace TKCode.Arc;

/// <summary>
/// The popup under the bell: Core <see cref="ArcPopup"/> (placement below the bell, flipping, Arc's floating shadow)
/// that also keeps the panel <see cref="CollisionPadding"/> px inside the window horizontally (Radix
/// <c>collisionPadding</c>), sliding it along the bell edge instead of letting it touch the window edge.
/// </summary>
internal sealed class NotificationCenterPopup : ArcPopup
{
    /// <summary>Space kept between the panel and the window's left and right edges (Arc: 12).</summary>
    public double CollisionPadding { get; set; } = 12;

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        if (!isOverlayHost || PlacementTarget is not { } target || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }
        var width = placement.PopupSize.Width - placement.Deflate.Left - placement.Deflate.Right;
        var client = topLevel.ClientSize.Width;
        if (width <= 0 || width + 2 * CollisionPadding > client)
        {
            return;
        }
        // Centered on the bell, then slid back inside the padded window.
        var anchor = placement.AnchorRectangle;
        var left = anchor.X + anchor.Width / 2 - width / 2 + placement.Offset.X;
        var clamped = Math.Clamp(left, CollisionPadding, client - CollisionPadding - width);
        if (Math.Abs(clamped - left) > 0.01)
        {
            placement.Offset = placement.Offset.WithX(placement.Offset.X + clamped - left);
        }
    }
}

/// <summary>
/// Shows the <see cref="NotificationCenterPanel"/> in a <see cref="NotificationCenterPopup"/> with Arc's panel motion,
/// played on the popup's motion actor so the floating shadow moves with the panel: it grows out of the bell from 6px
/// toward it at scale 0.97 while fading in (standard 240ms, enter curve), and leaves in 120ms at half the travel and
/// scale 0.98. The origin is the panel edge nearest the bell, following a flip to the top. An open and a close
/// reverse each other from where the panel is. Code-driven because the motion has to run on AtomUI's popup frame
/// (outside any template) and gate the popup's removal on the exit. Reduced motion and Off show and hide the panel at
/// once (Arc turns the panel animation off for reduced motion). Also reports presses outside the panel and the bell,
/// and Escape.
/// </summary>
internal sealed class NotificationCenterPresence : IDisposable
{
    private const double Travel = 6;
    private const double EnterScale = 0.97;
    private const double ExitScale = 0.98;

    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<Point> _pose;
    private readonly MatrixTransform _transform = new();
    private readonly MatrixTransform _fallbackTransform = new();
    private BaseMotionActor? _actor;
    private Control? _anchor;
    private TopLevel? _topLevel;
    private bool _closing;
    private int _generation;
    private Point _origin;
    private Size _originSize;
    private bool _originAbove;
    private bool _originKnown;
    private BaseMotionActor? _originActor;

    public NotificationCenterPresence(NotificationCenterPanel panel)
    {
        Panel = panel;
        Popup = new NotificationCenterPopup
        {
            Child = panel,
            IsLightDismissEnabled = false,
            WindowManagerAddShadowHint = false,
            EnterOffset = 0,
            EnterScale = 1
        };
        // The motion runs here, not in AtomUI's popup motion: animation priority wins over ArcPopup's own setting.
        Popup.SetValue(AtomPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
        Popup.Opened += OnPopupOpened;
        _opacity = new MotionValue<double>(panel, 0, MotionVectors.Double);
        _pose = new MotionValue<Point>(panel, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        panel.SizeChanged += (_, _) => Apply();
    }

    public NotificationCenterPopup Popup { get; }

    public NotificationCenterPanel Panel { get; }

    /// <summary>Open and not leaving.</summary>
    public bool IsShown => Popup.IsOpen && !_closing;

    /// <summary>The exit finished and the popup closed.</summary>
    public event EventHandler? Hidden;

    /// <summary>A press landed outside the panel and the bell while shown.</summary>
    public event EventHandler<PointerPressedEventArgs>? OutsidePressed;

    /// <summary>Escape was pressed while shown.</summary>
    public event EventHandler<KeyEventArgs>? EscapePressed;

    /// <summary>Opens the popup below <paramref name="anchor"/>, <paramref name="gap"/> px away; reverses a running exit.</summary>
    public void Show(Control anchor, double gap)
    {
        if (TopLevel.GetTopLevel(anchor) is null)
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
        _anchor = anchor;
        Popup.PlacementTarget = anchor;
        Popup.MarginToAnchor = gap;
        Popup.RequestedPlacement = PlacementMode.Bottom;
        Hook(anchor);
        var level = ArcMotion.GetEffectiveLevel(anchor);
        if (!reopening)
        {
            _opacity.Jump(level == ArcMotionLevel.Full ? 0 : 1);
            _pose.Jump(level == ArcMotionLevel.Full ? new Point(Travel, EnterScale) : new Point(0, 1));
            Popup.IsOpen = true;
        }
        Apply();
        if (level != ArcMotionLevel.Full)
        {
            _opacity.Jump(1);
            _pose.Jump(new Point(0, 1));
            return;
        }
        var enter = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
        _ = _opacity.AnimateTo(1, enter);
        _ = _pose.AnimateTo(new Point(0, 1), enter);
    }

    /// <summary>Plays the exit and closes the popup afterwards (at once when <paramref name="immediate"/>).</summary>
    public void Hide(bool immediate = false)
    {
        if (!Popup.IsOpen)
        {
            return;
        }
        if (immediate || _anchor is null || TopLevel.GetTopLevel(_anchor) is null || ArcMotion.GetEffectiveLevel(_anchor) != ArcMotionLevel.Full)
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
        var exit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
        _ = _pose.AnimateTo(new Point(Travel / 2, ExitScale), exit);
        var task = _opacity.AnimateTo(0, exit);
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
        Unhook();
        if (Popup.IsOpen)
        {
            Popup.IsOpen = false;
        }
        _opacity.Jump(0);
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        _actor = Panel.FindAncestorOfType<BaseMotionActor>();
        if (_actor is not null)
        {
            _actor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _actor.MotionTransform = _transform;
        }
        Apply();
    }

    /// <summary>The side the panel landed on after AtomUI's flip.</summary>
    private bool IsAbove => Popup.IsVerticalFlipped;

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity.Value, 0, 1);
        var pose = _pose.Value;
        // The panel starts toward the bell: below the bell it starts higher, above it lower.
        var dy = IsAbove ? pose.X : -pose.X;
        var origin = Origin();
        var matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(pose.Y, pose.Y) *
                     Matrix.CreateTranslation(origin.X, origin.Y + dy);
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
            Panel.Opacity = opacity;
            // Before the popup frame exists the panel carries the motion itself.
            Panel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _fallbackTransform.Matrix = matrix;
            Panel.RenderTransform = _fallbackTransform;
        }
    }

    /// <summary>Transform origin in actor coordinates: the middle of the panel edge facing the bell (Radix's origin).</summary>
    private Point Origin()
    {
        // Both motion values call Apply every frame; the origin only moves with the panel's size, its flip or its frame.
        var bounds = Panel.Bounds;
        var above = IsAbove;
        if (_originKnown && _originSize == bounds.Size && _originAbove == above && ReferenceEquals(_originActor, _actor))
        {
            return _origin;
        }
        var topLeft = default(Point);
        if (_actor is { } actor && Panel.TranslatePoint(default, actor) is { } p)
        {
            topLeft = p;
        }
        _origin = new Point(topLeft.X + bounds.Width / 2, topLeft.Y + (above ? bounds.Height : 0));
        _originSize = bounds.Size;
        _originAbove = above;
        _originActor = _actor;
        _originKnown = true;
        return _origin;
    }

    #region Outside press and Escape

    private void Hook(Control anchor)
    {
        var topLevel = TopLevel.GetTopLevel(anchor);
        if (ReferenceEquals(topLevel, _topLevel))
        {
            return;
        }
        Unhook();
        _topLevel = topLevel;
        if (topLevel is null)
        {
            return;
        }
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel, true);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Panel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void Unhook()
    {
        if (_topLevel is { } topLevel)
        {
            topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPressed);
            topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            Panel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _topLevel = null;
        }
    }

    private void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsShown || e.Source is not Visual source)
        {
            return;
        }
        if (IsWithin(Panel, source) || (_anchor is { } anchor && IsWithin(anchor, source)))
        {
            return;
        }
        OutsidePressed?.Invoke(this, e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Keys from focus inside the panel stay within the popup host, so the panel listens as well as the window.
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
        // Nested popups opened from the panel are its logical descendants.
        return element is ILogical logical && ((ILogical)root).IsLogicalAncestorOf(logical);
    }

    #endregion

    public void Dispose()
    {
        Hide(immediate: true);
        Unhook();
        _opacity.Dispose();
        _pose.Dispose();
    }
}

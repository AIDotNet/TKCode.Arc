using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AiSuggestionPop"/> goes relative to its anchor (see <see cref="AiSuggestionPlacement"/>).</summary>
/// <param name="Left">The surface's left edge, in anchor coordinates.</param>
/// <param name="Width">The surface's width: its own, or less inside a narrow anchor.</param>
/// <param name="Above">True above the line (the usual side), false below it.</param>
/// <param name="MaxHeight">The room left for the surface when neither side fits it; infinite otherwise.</param>
internal readonly record struct AiSuggestionPlacementResult(double Left, double Width, bool Above, double MaxHeight);

/// <summary>
/// The placement rule of the "/" and "@" list (pure, for tests). Arc hangs the list above the current line with its bottom
/// edge 6px over the line's top, its left edge a fixed distance left of the trigger character, pushed back left so it
/// keeps 6px from the anchor's right edge. It goes below the line only when the window has no room above; when neither
/// side has room for all of it, it stays above while that leaves a useful height (else takes the roomier side) and
/// scrolls in what is left.
/// </summary>
internal static class AiSuggestionPlacement
{
    /// <summary>Space between the surface and the line (Arc: 6).</summary>
    public const double LineGap = 6;

    /// <summary>Space kept from the anchor's right edge (Arc: about 6), and from both edges in a narrow anchor.</summary>
    public const double EdgeInset = 6;

    /// <summary>Space kept from the window's top and bottom edges.</summary>
    public const double ViewportInset = 6;

    /// <summary>The default distance from the trigger's left edge to the surface's left edge (−7).</summary>
    public const double DefaultShift = -7;

    /// <summary>
    /// When the list fits on neither side, it stays above the line as long as that leaves it this much room (a heading
    /// and about two rows), or at least as much as below.
    /// </summary>
    public const double MinUsefulHeight = 150;

    /// <summary>
    /// Places a surface of <paramref name="width"/> × <paramref name="height"/> for the trigger at <paramref name="caret"/>
    /// (anchor coordinates: X is the trigger's left edge, Y and Height the current line).
    /// </summary>
    /// <param name="anchorWidth">The anchor's width; the surface stays inside it.</param>
    /// <param name="caret">The trigger character's rectangle in the anchor: its left edge and the current line's top and height.</param>
    /// <param name="anchorTop">The anchor's top edge in the window (for the room above and below).</param>
    /// <param name="viewportHeight">The window's client height.</param>
    /// <param name="width">The surface's own width (300).</param>
    /// <param name="height">The surface's natural height (capped at its maximum, 280).</param>
    /// <param name="shift">The surface's left edge relative to <paramref name="caret"/>.X.</param>
    public static AiSuggestionPlacementResult Compute(double anchorWidth, Rect caret, double anchorTop, double viewportHeight,
                                                      double width, double height, double shift = DefaultShift)
    {
        var shown = Math.Max(0, Math.Min(width, anchorWidth - 2 * EdgeInset));
        var left = Math.Max(0, Math.Min(caret.X + shift, anchorWidth - EdgeInset - shown));
        var lineTop = anchorTop + caret.Y;
        var roomAbove = lineTop - LineGap - ViewportInset;
        var roomBelow = viewportHeight - (lineTop + caret.Height) - LineGap - ViewportInset;
        const double tolerance = 0.5;
        if (height <= roomAbove + tolerance)
        {
            return new AiSuggestionPlacementResult(left, shown, true, double.PositiveInfinity);
        }
        if (height <= roomBelow + tolerance)
        {
            return new AiSuggestionPlacementResult(left, shown, false, double.PositiveInfinity);
        }
        // Neither side fits: Arc's side while it still holds a useful part of the list, else the roomier side.
        var above = roomAbove >= Math.Min(MinUsefulHeight, roomBelow);
        return new AiSuggestionPlacementResult(left, shown, above, Math.Max(0, above ? roomAbove : roomBelow));
    }
}

/// <summary>
/// The Core <see cref="ArcPopup"/> of the list (overlay hosting, floating shadow): hung from a one-pixel rectangle at the
/// list's left edge on the current line, above it (<c>TopEdgeAlignedLeft</c>) or below it, 6px away. The side is chosen
/// by <see cref="AiSuggestionPlacement"/> before opening, so the positioner only slides along the line and never flips.
/// Never light-dismissed and never focused: the prompt keeps focus and its owner closes the list.
/// </summary>
internal sealed class AiSuggestionPopupHost : ArcPopup
{
    static AiSuggestionPopupHost()
    {
        MarginToAnchorProperty.OverrideDefaultValue<AiSuggestionPopupHost>(AiSuggestionPlacement.LineGap);
        IsLightDismissEnabledProperty.OverrideDefaultValue<AiSuggestionPopupHost>(false);
    }

    public AiSuggestionPopupHost()
    {
        TakesFocusFromNativeControl = false;
        WindowManagerAddShadowHint = false;
        EnterOffset = 0;
        EnterScale = 1;
        // The presence plays the motion; animation priority wins over the local value ArcPopup sets for its own.
        SetValue(ArcPopup.IsMotionEnabledProperty, false, BindingPriority.Animation);
        Placement = PlacementMode.TopEdgeAlignedLeft;
    }

    /// <summary>Hangs the surface above (or below) <paramref name="line"/>, a rectangle in the placement target's coordinates.</summary>
    public void Place(bool above, Rect line)
    {
        var placement = above ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        if (Placement != PlacementMode.Custom || RequestedPlacement != placement)
        {
            Placement = placement;
        }
        if (PlacementRect != line)
        {
            PlacementRect = line;
        }
    }

    protected override void OnPlacementResolved(CustomPopupPlacement placement, Thickness shadow, double marginToAnchor,
                                                bool isOverlayHost, bool flipX, bool flipY)
    {
        placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX;
    }
}

/// <summary>
/// Shows an <see cref="AiSuggestionPop"/> in an <see cref="AiSuggestionPopupHost"/> against the composer: <see cref="Open"/>
/// places it for the current line (and moves it there while open), <see cref="Close"/> plays the exit. Arc's motion,
/// played on the popup's motion actor so the floating shadow moves with the surface: in, the surface rises from 6px
/// toward the line at scale 0.98 to rest on a critically damped spring (visual duration 0.29s) while its opacity follows
/// the same spring; out, 120ms ease-out to 4px toward the line, scale 0.99 and opacity 0. The scale origin is the edge
/// facing the line, at the trigger. Reduced motion fades in and out in 120ms (linear) without moving; Off is instant.
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>) rather than the popup's own motions: the enter is a spring, a close while
/// entering (or an open while leaving, as when "/" is erased and typed again) continues from the values on screen, and
/// the popup may only close after the exit has played. The surface's height follows the rows on a spring inside the pop;
/// above the line the popup is placed again on every size change, so its bottom edge stays on the line.
/// </para>
/// </summary>
internal sealed class AiSuggestionPopup : IDisposable
{
    internal const double EnterTravel = 6;
    internal const double EnterScale = 0.98;
    internal const double ExitTravel = 4;
    internal const double ExitScale = 0.99;
    internal static readonly SpringSpec EnterSpring = SpringSpec.FromVisual(0.29, 0);
    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>CSS <c>ease-out</c>.</summary>
    internal static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    private static readonly Easing s_linear = new LinearEasing();

    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<Point> _pose;
    private readonly MatrixTransform _transform = new();
    private readonly MatrixTransform _fallbackTransform = new();
    private ArcPopupFrame? _actor;
    private Control? _parented;
    private bool _closing;
    private int _generation;
    private double _originX;
    private Rect _caret;

    public AiSuggestionPopup(AiSuggestionPop? pop = null)
    {
        Pop = pop ?? new AiSuggestionPop();
        Pop.Host = this;
        Popup = new AiSuggestionPopupHost { Child = Pop };
        Popup.Opened += OnPopupOpened;
        _opacity = new MotionValue<double>(Pop, 0, MotionVectors.Double);
        // X: travel toward the line in px, Y: scale.
        _pose = new MotionValue<Point>(Pop, new Point(0, 1), MotionVectors.Point);
        _opacity.Changed += _ => Apply();
        _pose.Changed += _ => Apply();
        Pop.SizeChanged += OnPopSizeChanged;
    }

    /// <summary>The list.</summary>
    public AiSuggestionPop Pop { get; }

    /// <summary>
    /// The popup. The owner may add it to its logical children (as other Blocks do with their popups); otherwise the
    /// first <see cref="Open"/> makes the anchor its logical parent, so styles, resources and the motion level flow in.
    /// </summary>
    public AiSuggestionPopupHost Popup { get; }

    /// <summary>Shown and not leaving.</summary>
    public bool IsOpen => Popup.IsOpen && !_closing;

    /// <summary>Playing its exit.</summary>
    public bool IsClosing => Popup.IsOpen && _closing;

    /// <summary>The last placement (side, left edge, width).</summary>
    public AiSuggestionPlacementResult Placement { get; private set; }

    /// <summary>
    /// Where the surface's left edge sits relative to the trigger's left edge (<see cref="AiSuggestionPlacement.DefaultShift"/>).
    /// Arc puts the list 7px from the composer's edge plus the trigger's x in the text, whose first character is 19px in
    /// (border 1, padding 10, text padding 8): −12 from the trigger's glyph when the anchor is the composer.
    /// </summary>
    public double HorizontalShift { get; set; } = AiSuggestionPlacement.DefaultShift;

    /// <summary>The anchor of the last <see cref="Open"/>.</summary>
    public Control? Anchor { get; private set; }

    /// <summary>Current opacity of the surface (tests).</summary>
    internal double CurrentOpacity => _opacity.Value;

    /// <summary>Current travel (px toward the line) and scale of the surface (tests).</summary>
    internal Point CurrentPose => _pose.Value;

    /// <summary>The exit finished (or the popup closed at once) and the popup is gone.</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Opens the list for the trigger at <paramref name="caretRect"/> (in <paramref name="anchor"/>'s coordinates: X is the
    /// trigger character's left edge, Y and Height are the current line), or moves it there when open; an exit in progress
    /// turns back. Nothing opens (and an open list closes) when <see cref="Pop"/> has no rows: call
    /// <see cref="AiSuggestionPop.Show"/> first. Returns whether the list is shown.
    /// </summary>
    public bool Open(Control anchor, Rect caretRect)
    {
        if (Pop.IsEmpty)
        {
            Close();
            return false;
        }
        if (TopLevel.GetTopLevel(anchor) is not { } topLevel || anchor.TranslatePoint(default, topLevel) is not { } anchorOrigin)
        {
            return false;
        }
        EnsureLogicalParent(anchor);
        Anchor = anchor;
        _caret = caretRect;
        var result = AiSuggestionPlacement.Compute(anchor.Bounds.Width, caretRect, anchorOrigin.Y, topLevel.ClientSize.Height,
            Pop.SurfaceWidth, Pop.NaturalHeight, HorizontalShift);
        Placement = result;
        Pop.Width = result.Width;
        Pop.AvailableHeight = result.MaxHeight;
        _originX = Math.Clamp(caretRect.X - result.Left, 0, result.Width);
        if (!ReferenceEquals(Popup.PlacementTarget, anchor))
        {
            Popup.PlacementTarget = anchor;
        }
        Popup.Place(result.Above, new Rect(result.Left, caretRect.Y, 1, Math.Max(1, caretRect.Height)));
        if (Popup.IsOpen && !_closing)
        {
            Popup.Reposition();
            Apply();
            return true;
        }
        Show(anchor);
        return Popup.IsOpen;
    }

    /// <summary>
    /// New rows while shown: the side and the room are chosen again for their height (the same trigger and line), so a
    /// list that grew past the room above moves below instead of leaving the window.
    /// </summary>
    internal void OnRowsChanged()
    {
        if (IsOpen && Anchor is { } anchor)
        {
            Open(anchor, _caret);
        }
    }

    /// <summary>Plays the exit and closes the popup afterwards (at once when <paramref name="immediate"/>).</summary>
    public void Close(bool immediate = false)
    {
        if (!Popup.IsOpen)
        {
            return;
        }
        var target = Popup.PlacementTarget;
        var level = target is not null && TopLevel.GetTopLevel(target) is not null ? ArcMotion.GetEffectiveLevel(target) : ArcMotionLevel.Off;
        if (immediate || level == ArcMotionLevel.Off || TopLevel.GetTopLevel(Pop) is null)
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
            var exit = MotionSpec.Tween(ExitDuration, EaseOut);
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

    private void Show(Control anchor)
    {
        var reopening = Popup.IsOpen && _closing;
        _generation++;
        _closing = false;
        var level = ArcMotion.GetEffectiveLevel(anchor);
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
        else
        {
            Popup.Reposition();
        }
        Apply();
        switch (level)
        {
            case ArcMotionLevel.Full:
                var enter = MotionSpec.Spring(EnterSpring);
                _ = _opacity.AnimateTo(1, enter);
                _ = _pose.AnimateTo(new Point(0, 1), enter);
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
        Pop.OnHidden();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureLogicalParent(Control anchor)
    {
        var parent = ((ILogical)Popup).LogicalParent;
        if (parent is not null && !ReferenceEquals(parent, _parented))
        {
            // The owner adopted the popup.
            return;
        }
        if (ReferenceEquals(_parented, anchor) || (_parented is not null && Popup.IsOpen))
        {
            // Same anchor, or a new one while shown: re-parenting an open popup would restyle it mid-flight.
            return;
        }
        if (_parented is not null)
        {
            ((ISetLogicalParent)Popup).SetParent(null);
        }
        ((ISetLogicalParent)Popup).SetParent(anchor);
        _parented = anchor;
    }

    private void OnPopSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // The positioner does not follow size changes: above the line the bottom edge must stay on it while the height
        // springs.
        if (Popup.IsOpen && Placement.Above && Math.Abs(e.NewSize.Height - e.PreviousSize.Height) > 0.01)
        {
            Popup.Reposition();
        }
        Apply();
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        _actor = Pop.FindAncestorOfType<ArcPopupFrame>();
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
            // Scaled about the edge facing the line, at the trigger; shifted toward the line.
            var size = Pop.Bounds.Size;
            var topLeft = _actor is { } a && Pop.TranslatePoint(default, a) is { } p ? p : default;
            var above = Placement.Above;
            var origin = new Point(topLeft.X + Math.Min(_originX, size.Width), topLeft.Y + (above ? size.Height : 0));
            var shift = above ? pose.X : -pose.X;
            matrix = Matrix.CreateTranslation(-origin.X, -origin.Y) * Matrix.CreateScale(pose.Y, pose.Y) *
                     Matrix.CreateTranslation(origin.X, origin.Y + shift);
        }
        if (_actor is { } actor && TopLevel.GetTopLevel(actor) is not null)
        {
            actor.Opacity = opacity;
            _transform.Matrix = matrix;
            if (Pop.RenderTransform is not null || Pop.Opacity < 1)
            {
                Pop.Opacity = 1;
                Pop.RenderTransform = null;
            }
        }
        else
        {
            // Before the popup frame exists the surface carries the motion itself.
            Pop.Opacity = opacity;
            Pop.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
            _fallbackTransform.Matrix = matrix;
            Pop.RenderTransform = _fallbackTransform;
        }
    }

    public void Dispose()
    {
        Close(immediate: true);
        Pop.SizeChanged -= OnPopSizeChanged;
        Popup.Opened -= OnPopupOpened;
        if (_parented is not null && ReferenceEquals(((ILogical)Popup).LogicalParent, _parented))
        {
            ((ISetLogicalParent)Popup).SetParent(null);
        }
        _parented = null;
        if (ReferenceEquals(Pop.Host, this))
        {
            Pop.Host = null;
        }
        _opacity.Dispose();
        _pose.Dispose();
    }
}

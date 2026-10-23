using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

internal sealed partial class AiSourcesRow
{
    /// <summary>The right-edge fade of the narrow row (Arc's mask: black until 28px before the end).</summary>
    public const double EdgeFade = 28;

    /// <summary>Room around the cards inside the scroller for their focus rings (2px ring 2px out).</summary>
    internal const double RingRoom = 4;

    /// <summary>A scroll is over when no new offset came for this long; then the row snaps.</summary>
    internal static readonly TimeSpan SnapIdle = TimeSpan.FromMilliseconds(150);

    private MotionValue<double>? _scroll;
    private DispatcherTimer? _idle;
    private bool _pointerDown;
    private (bool Fade, double Width) _maskState = (false, -1);
    private IDisposable? _bringIntoView;
    private bool _revealPosted;

    /// <summary>How far the scroller reaches past the row on each side: the bleed when narrow (at least the ring room).</summary>
    internal double ScrollPadding => _isNarrow ? Math.Max(Bleed, RingRoom) : RingRoom;

    /// <summary>Whether the scroll is moving on its own (a snap or a reveal).</summary>
    internal bool IsScrolling => _scroll?.IsAnimating == true;

    /// <summary>The offset the running scroll is heading to.</summary>
    internal double ScrollTarget => _scroll?.Target ?? _scroller?.Offset.X ?? 0;

    /// <summary>Whether the right-edge fade is on.</summary>
    internal bool HasEdgeFade => _maskState.Fade;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateNarrow(e.NewSize.Width);
            ApplyMode();
        }
    }

    /// <summary>
    /// The row is narrow when it and its bleed on both sides are at most <see cref="NarrowWidth"/> wide: with the column's
    /// side padding as the bleed that is Arc's container width, so the switch happens where Arc's does.
    /// </summary>
    private void UpdateNarrow(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var narrow = width + 2 * Math.Max(0, Bleed) <= NarrowWidth + 0.01;
        if (narrow == _isNarrow)
        {
            return;
        }
        _isNarrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        foreach (var card in _cards)
        {
            card.IsNarrow = narrow;
        }
    }

    /// <summary>
    /// Puts the scroller and the panel in the current mode: the scroller reaches past the row by <see cref="ScrollPadding"/>
    /// on each side (and 2px up and down, for the focus rings) and the panel pads the cards back into the column; the narrow
    /// row scrolls sideways with cards <c>min(248, 76%)</c> of the row wide.
    /// </summary>
    private void ApplyMode()
    {
        if (_scroller is null || _panel is null)
        {
            return;
        }
        var pad = ScrollPadding;
        var extra = RingRoom - 2;
        _scroller.Margin = new Thickness(-pad, -extra, -pad, 14 - extra);
        _panel.Padding = new Thickness(pad, 2 + extra, pad, 4 + extra);
        _panel.IsRow = _isNarrow;
        _panel.CardWidth = AiSourceSnap.CardWidth(Bounds.Width);
        _scroller.HorizontalScrollBarVisibility = _isNarrow ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        if (_body is not null)
        {
            _body.CrossClipOverflow = pad;
        }
        if (!_isNarrow)
        {
            StopScroll();
            _scroller.Offset = default;
        }
        UpdateMask();
        // Turning narrow with a citation active: its card comes into view once the row is laid out as a row.
        RevealActiveAfterLayout();
    }

    private void AttachScroller()
    {
        if (_scroller is null)
        {
            return;
        }
        _scroller.ScrollChanged += OnScrollChanged;
        _scroller.AddHandler(PointerPressedEvent, OnScrollerPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _scroller.AddHandler(PointerReleasedEvent, OnScrollerPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _scroller.AddHandler(PointerCaptureLostEvent, OnScrollerPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _bringIntoView = _panel?.AddDisposableHandler(RequestBringIntoViewEvent, OnRequestBringIntoView);
    }

    private void DetachScroller()
    {
        StopScroll();
        _bringIntoView?.Dispose();
        _bringIntoView = null;
        if (_scroller is null)
        {
            return;
        }
        _scroller.ScrollChanged -= OnScrollChanged;
        _scroller.RemoveHandler(PointerPressedEvent, OnScrollerPointerPressed);
        _scroller.RemoveHandler(PointerReleasedEvent, OnScrollerPointerReleased);
        _scroller.RemoveHandler(PointerCaptureLostEvent, OnScrollerPointerReleased);
        _scroller.OpacityMask = null;
        _maskState = (false, -1);
    }

    private void OnScrollerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A finger or a pen on the row takes over: the running snap or reveal stops, and no snap starts until it lifts.
        _pointerDown = true;
        _idle?.Stop();
        StopScroll();
    }

    private void OnScrollerPointerReleased(object? sender, RoutedEventArgs e)
    {
        if (!_pointerDown)
        {
            return;
        }
        _pointerDown = false;
        RestartIdle();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateMask();
        if (_scroller is not { } viewer || e.OffsetDelta.X == 0)
        {
            return;
        }
        if (_scroll is { IsAnimating: true } scroll)
        {
            if (Math.Abs(viewer.Offset.X - scroll.Value) <= 0.5)
            {
                // Our own step.
                return;
            }
            // The person scrolled (wheel, touch, a clamp) during a snap or a reveal: theirs wins.
            scroll.Stop();
        }
        if (_isNarrow)
        {
            RestartIdle();
        }
    }

    private void RestartIdle()
    {
        if (!_isNarrow || _pointerDown)
        {
            return;
        }
        if (_idle is null)
        {
            _idle = new DispatcherTimer { Interval = SnapIdle };
            _idle.Tick += (_, _) =>
            {
                _idle.Stop();
                SettleScroll();
            };
        }
        _idle.Stop();
        _idle.Start();
    }

    /// <summary>Snaps the narrow row to the card start nearest to where the scroll came to rest (Arc's mandatory snap).</summary>
    internal void SettleScroll()
    {
        if (!_isNarrow || _scroller is not { } viewer || _pointerDown || _cards.Count == 0)
        {
            return;
        }
        var pad = ScrollPadding;
        Span<double> starts = _cards.Count <= 32 ? stackalloc double[_cards.Count] : new double[_cards.Count];
        for (var i = 0; i < _cards.Count; i++)
        {
            starts[i] = _cards[i].Bounds.X;
        }
        var max = Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width);
        var target = AiSourceSnap.Target(viewer.Offset.X, max, starts, pad);
        ScrollTo(target, animate: true);
    }

    /// <summary>In the narrow row, brings the lit card into view (Arc scrolls it smoothly; the person can take over).</summary>
    private void RevealActive()
    {
        var number = ActiveNumber;
        if (number == 0 || !_isNarrow || !IsExpanded)
        {
            return;
        }
        foreach (var card in _cards)
        {
            if (card.Number == number)
            {
                Reveal(card, animate: true);
                return;
            }
        }
    }

    /// <summary>
    /// <see cref="RevealActive"/> once the pending layout pass has run: after the cards were built or the row changed mode
    /// their bounds are stale (still the grid's) until then.
    /// </summary>
    private void RevealActiveAfterLayout()
    {
        if (ActiveNumber == 0 || _revealPosted)
        {
            return;
        }
        _revealPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _revealPosted = false;
            RevealActive();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Scrolls the narrow row so <paramref name="card"/> shows whole, its start at the column when it was not.</summary>
    internal void Reveal(AiSourceCard card, bool animate, bool retry = true)
    {
        if (!_isNarrow || _scroller is not { } viewer)
        {
            return;
        }
        if (card.Bounds.Width <= 0 || _panel is { IsArrangeValid: false } || !viewer.IsArrangeValid)
        {
            // Not laid out yet (just created, just opened, the mode just changed): once more after the layout pass.
            if (retry && this.IsAttachedToVisualTree())
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_cards.Contains(card))
                    {
                        Reveal(card, animate, retry: false);
                    }
                }, DispatcherPriority.Loaded);
            }
            return;
        }
        var max = Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width);
        var offset = _scroll is { IsAnimating: true } running ? running.Target : viewer.Offset.X;
        var target = AiSourceSnap.Reveal(offset, viewer.Viewport.Width, max, card.Bounds.Left, card.Bounds.Right, ScrollPadding);
        ScrollTo(target, animate);
    }

    /// <summary>
    /// Moves the scroller to <paramref name="target"/> on <see cref="ArcSpring.Smooth"/> (about Arc's 500ms smooth scroll).
    /// Code-driven because the target is measured from the laid-out cards and a new target must keep the current velocity;
    /// Reduced and Off jump (the motion value does).
    /// </summary>
    private void ScrollTo(double target, bool animate)
    {
        if (_scroller is not { } viewer)
        {
            return;
        }
        if (Math.Abs(target - viewer.Offset.X) < 0.5 && _scroll is not { IsAnimating: true })
        {
            return;
        }
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, viewer.Offset.X, MotionVectors.Double);
            _scroll.Changed += x =>
            {
                if (_scroller is { } current)
                {
                    current.Offset = new Vector(x, current.Offset.Y);
                }
            };
        }
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(viewer.Offset.X);
        }
        if (animate && IsLoaded)
        {
            _ = _scroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _scroll.Jump(target);
        }
    }

    private void StopScroll()
    {
        _idle?.Stop();
        _scroll?.Stop();
    }

    /// <summary>
    /// Keyboard focus on a card of the narrow row: the row scrolls it into view itself (smoothly, its start at the column)
    /// instead of the scroller's instant jump, and the request goes on from the scroller so an outer scroller still brings
    /// the row into view vertically.
    /// </summary>
    private void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (!_isNarrow || _scroller is not { } viewer || e.TargetObject is not Visual target)
        {
            return;
        }
        AiSourceCard? card = null;
        foreach (var candidate in _cards)
        {
            if (ReferenceEquals(candidate, target) || candidate.IsVisualAncestorOf(target))
            {
                card = candidate;
                break;
            }
        }
        if (card is null)
        {
            return;
        }
        e.Handled = true;
        Reveal(card, animate: true);
        viewer.BringIntoView(new Rect(0, card.Bounds.Top, viewer.Bounds.Width, card.Bounds.Height));
    }

    /// <summary>
    /// The narrow row fades out over its last 28px while more cards follow on the right; at the end, and in the grid, there
    /// is no mask at all. A new immutable brush is published only when that state or the width changes.
    /// </summary>
    private void UpdateMask()
    {
        if (_scroller is not { } viewer)
        {
            return;
        }
        var width = viewer.Bounds.Width;
        var fade = _isNarrow && width > EdgeFade && viewer.Extent.Width - viewer.Viewport.Width - viewer.Offset.X > 0.5;
        var state = (fade, fade ? width : -1);
        if (state == _maskState)
        {
            return;
        }
        _maskState = state;
        if (!fade)
        {
            viewer.OpacityMask = null;
            return;
        }
        var stop = Math.Clamp(1 - EdgeFade / width, 0, 1);
        viewer.OpacityMask = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(stop, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
    }
}

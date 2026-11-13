using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

public partial class KanbanBoard
{
    private readonly MatrixTransform _proxyTransform = new();
    private KanbanCardView? _proxy;
    private ProjectsItemMotion? _proxySource;
    private Point _proxyOrigin;
    private bool _proxyOriginDirty = true;
    private bool _proxyLingering;
    private Action<Vector>? _syncOnVector;
    private Action<double>? _syncOnDouble;

    /// <summary>The copy of the lifted card drawn in the drag layer (null while nothing is lifted).</summary>
    internal KanbanCardView? DragProxy => _proxy;

    /// <summary>The proxy's transform in drag-layer coordinates (tests).</summary>
    internal Matrix DragProxyMatrix => _proxyTransform.Matrix;

    /// <summary>
    /// Draws the lifted card above the whole block, Arc's overlay: the real card stays in the panel for layout, hit
    /// testing, its flights and the focus, but turns transparent; a live copy in the drag layer (above the scroller and
    /// its sticky heads, under the scrim and drawer) follows its transform exactly, so it is not clipped at the scroll
    /// viewport when it rides over the column heads or the toolbar. A live copy rather than a bitmap stays crisp at any
    /// scaling and only its matrix changes per frame.
    /// </summary>
    private void ShowProxy(KanbanDragSession session)
    {
        HideProxy();
        if (_dragLayer is null || _panel is null)
        {
            return;
        }
        var view = session.View;
        var proxy = new KanbanCardView(session.Card, this) { IsProxy = true, Focusable = false, IsHitTestVisible = false };
        AutomationProperties.SetAccessibilityView(proxy, AccessibilityView.Raw);
        proxy.RenderTransformOrigin = RelativePoint.Center;
        proxy.RenderTransform = _proxyTransform;
        proxy.SetFocusRing(view.IsFocusRingVisible);
        // The copy appears with the card's state at once (no easing from rest).
        proxy.SetState(view.IsLifted, view.IsDragged, view.IsLanding, instant: true);
        view.Mirror = proxy;
        _dragLayer.Children.Add(proxy);
        var motion = _panel.Flip.Get(view);
        _syncOnVector ??= _ => SyncProxy();
        _syncOnDouble ??= _ => SyncProxy();
        motion.Offset.Changed += _syncOnVector;
        motion.Scale.Changed += _syncOnDouble;
        motion.Rotation.Changed += _syncOnDouble;
        _proxy = proxy;
        _proxySource = motion;
        _proxyLingering = false;
        _proxyOriginDirty = true;
        motion.Opacity.Jump(0);
        SyncProxy();
    }

    /// <summary>Removes the copy and shows the real card again, in the same pass (no frame without either).</summary>
    private void HideProxy()
    {
        var proxy = _proxy;
        var motion = _proxySource;
        _proxy = null;
        _proxySource = null;
        _proxyLingering = false;
        if (motion is not null)
        {
            if (_syncOnVector is not null)
            {
                motion.Offset.Changed -= _syncOnVector;
            }
            if (_syncOnDouble is not null)
            {
                motion.Scale.Changed -= _syncOnDouble;
                motion.Rotation.Changed -= _syncOnDouble;
            }
            motion.Opacity.Jump(1);
            if (motion.Target is KanbanCardView view && ReferenceEquals(view.Mirror, proxy))
            {
                view.Mirror = null;
            }
        }
        if (proxy is not null)
        {
            proxy.Detach();
            _dragLayer?.Children.Remove(proxy);
        }
    }

    /// <summary>A keyboard drag ended: the copy stays until the card's last glide has settled.</summary>
    private void ReleaseProxyWhenSettled()
    {
        if (_proxy is null)
        {
            return;
        }
        _proxyLingering = true;
        _dragLoop?.Start();
    }

    /// <summary>True when a lingering copy can go (its card is at rest).</summary>
    private bool TryReleaseLingeringProxy()
    {
        if (!_proxyLingering || _proxySource is not { } motion)
        {
            return true;
        }
        if (motion.Offset.IsAnimating || motion.Scale.IsAnimating || motion.Rotation.IsAnimating)
        {
            return false;
        }
        HideProxy();
        return true;
    }

    /// <summary>
    /// Puts the copy where the card is drawn: the card's layout rect in the panel, minus the scroll offset, plus the
    /// scroller's place in the block, then the card's own offset, scale and tilt about its centre (as
    /// <c>ProjectsItemMotion</c> applies them).
    /// </summary>
    internal void SyncProxy()
    {
        if (_proxy is null || _proxySource is not { } motion || _scroller is null || _dragLayer is null)
        {
            return;
        }
        if (_proxyOriginDirty && _scroller.TranslatePoint(default, _dragLayer) is { } origin)
        {
            _proxyOrigin = origin;
            _proxyOriginDirty = false;
        }
        var layout = motion.Layout ?? motion.Target.Bounds;
        if (double.IsNaN(_proxy.Width) || Math.Abs(_proxy.Width - layout.Width) > 0.01)
        {
            _proxy.Width = layout.Width;
        }
        var offset = motion.Offset.Value;
        var scroll = ScrollOffset;
        var x = _proxyOrigin.X + layout.X - scroll.X + offset.X;
        var y = _proxyOrigin.Y + layout.Y - scroll.Y + offset.Y;
        var scale = motion.Scale.Value;
        _proxyTransform.Matrix = Matrix.CreateScale(scale, scale)
                                 * Matrix.CreateRotation(motion.Rotation.Value * Math.PI / 180)
                                 * Matrix.CreateTranslation(x, y);
    }
}

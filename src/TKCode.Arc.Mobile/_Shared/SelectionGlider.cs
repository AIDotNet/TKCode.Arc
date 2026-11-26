using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Keeps one <see cref="GlidingIndicator"/> (P7) under the selected container of a list box (or any selecting items
/// control). A selection change glides; anything else that moves the target (the container resizing, the first layout)
/// snaps, and the very first show fades in place. A selected container that is hidden has no indicator. The glider is
/// drawn in render, so the glide never relayouts. Under reduced motion or Off the indicator jumps.
/// </summary>
internal sealed class SelectionGlider
{
    private readonly SelectingItemsControl _owner;
    private readonly GlidingIndicator _glider;
    private readonly Func<Control, Control?> _target;
    private readonly bool _insetByHalfStroke;
    private Rect? _last;
    private bool _attached;

    /// <param name="owner">The control whose selection the indicator follows.</param>
    /// <param name="glider">The indicator, in the same coordinate space as the items (a sibling of the items presenter).</param>
    /// <param name="target">Picks the control whose bounds the indicator covers, given the selected container.</param>
    /// <param name="insetByHalfStroke">Insets the indicator by half its stroke, so a ring is drawn inside the target's bounds.</param>
    public SelectionGlider(SelectingItemsControl owner, GlidingIndicator glider, Func<Control, Control?> target, bool insetByHalfStroke = false)
    {
        _owner = owner;
        _glider = glider;
        _target = target;
        _insetByHalfStroke = insetByHalfStroke;
    }

    /// <summary>The indicator being driven.</summary>
    public GlidingIndicator Glider => _glider;

    /// <summary>Starts following selection and layout; call when the owner is attached.</summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }
        _attached = true;
        _owner.SelectionChanged += OnSelectionChanged;
        _glider.Loaded += OnGliderLoaded;
        Update(false);
    }

    /// <summary>Stops following; call when the owner is detached or its template changes.</summary>
    public void Detach()
    {
        if (!_attached)
        {
            return;
        }
        _attached = false;
        _owner.SelectionChanged -= OnSelectionChanged;
        Track(null, null);
        _glider.Loaded -= OnGliderLoaded;
        _last = null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => Update(true);

    /// <summary>The owner's ArrangeOverride calls this after arranging its children (instead of a LayoutUpdated subscription).</summary>
    public void OnOwnerArranged()
    {
        if (_attached)
        {
            Update(false);
        }
    }

    // A panel inside the owner can move the selected item without the owner arranging again, so those two are watched too.
    private void Track(Control? container, Control? host)
    {
        if (ReferenceEquals(container, _trackedContainer) && ReferenceEquals(host, _trackedHost))
        {
            return;
        }
        if (_trackedContainer is not null)
        {
            _trackedContainer.PropertyChanged -= OnTrackedChanged;
        }
        if (_trackedHost is not null && !ReferenceEquals(_trackedHost, _trackedContainer))
        {
            _trackedHost.PropertyChanged -= OnTrackedChanged;
        }
        _trackedContainer = container;
        _trackedHost = host;
        if (container is not null)
        {
            container.PropertyChanged += OnTrackedChanged;
        }
        if (host is not null && !ReferenceEquals(host, container))
        {
            host.PropertyChanged += OnTrackedChanged;
        }
    }

    private void OnTrackedChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty || e.Property == Visual.IsVisibleProperty)
        {
            Update(false);
        }
    }

    private Control? _trackedContainer;
    private Control? _trackedHost;

    private void OnGliderLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Update(false);

    private void Update(bool animate)
    {
        if (!_glider.IsLoaded)
        {
            // The first placement waits for Loaded so that it can fade in (an indicator that is not loaded jumps).
            return;
        }
        var index = _owner.SelectedIndex;
        var container = index >= 0 ? _owner.ContainerFromIndex(index) : null;
        var host = container is null ? null : _target(container);
        Track(container, host);
        if (container is null || host is null || !container.IsVisible || !host.IsVisible)
        {
            if (_last is not null)
            {
                _last = null;
                _glider.Hide(animate);
            }
            return;
        }
        if (host.Bounds.Width <= 0 || host.Bounds.Height <= 0
            || !host.IsAttachedToVisualTree() || !_glider.IsAttachedToVisualTree())
        {
            return;
        }
        var origin = host.TranslatePoint(default, _glider);
        if (origin is null)
        {
            return;
        }
        var rect = new Rect(origin.Value, host.Bounds.Size);
        if (_insetByHalfStroke)
        {
            rect = rect.Deflate(_glider.StrokeThickness / 2);
        }
        if (_last == rect)
        {
            return;
        }
        _last = rect;
        // Appearing from nothing lands in place and fades in (P7); the indicator snaps instead when asked not to animate.
        _glider.MoveTo(rect, animate || !_glider.HasTarget);
    }
}

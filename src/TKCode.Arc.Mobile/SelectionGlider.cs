using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Keeps one <see cref="GlidingIndicator"/> (P7) under the selected container of a list box. A selection change glides;
/// anything else that moves the target (the container resizing, the first layout) snaps, and the very first show fades in
/// place. The glider is drawn in render, so the glide never relayouts. Under reduced motion or Off the indicator jumps.
/// </summary>
internal sealed class SelectionGlider
{
    private readonly ListBox _owner;
    private readonly GlidingIndicator _glider;
    private readonly Func<Control, Control?> _target;
    private Rect? _last;
    private bool _attached;

    /// <param name="owner">The list box whose selection the indicator follows.</param>
    /// <param name="glider">The indicator, in the same coordinate space as the items (a sibling of the items presenter).</param>
    /// <param name="target">Picks the control whose bounds the indicator covers, given the selected container.</param>
    public SelectionGlider(ListBox owner, GlidingIndicator glider, Func<Control, Control?> target)
    {
        _owner = owner;
        _glider = glider;
        _target = target;
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
        _owner.LayoutUpdated += OnLayoutUpdated;
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
        _owner.LayoutUpdated -= OnLayoutUpdated;
        _glider.Loaded -= OnGliderLoaded;
        _last = null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => Update(true);

    private void OnLayoutUpdated(object? sender, EventArgs e) => Update(false);

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
        if (host is null)
        {
            if (_last is not null)
            {
                _last = null;
                _glider.Hide(animate);
            }
            return;
        }
        if (!host.IsVisible || host.Bounds.Width <= 0 || host.Bounds.Height <= 0
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
        if (_last == rect)
        {
            return;
        }
        _last = rect;
        // Appearing from nothing lands in place and fades in (P7); the indicator snaps instead when asked not to animate.
        _glider.MoveTo(rect, animate || !_glider.HasTarget);
    }
}

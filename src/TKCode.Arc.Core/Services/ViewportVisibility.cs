using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Primitives;

/// <summary>
/// S6: reports when a control becomes visible inside its scroll viewport (first reveal, P18). Uses
/// <see cref="Layoutable.EffectiveViewportChanged"/>; with <c>once</c> it detaches after the first reveal.
/// </summary>
public sealed class ViewportVisibility : IDisposable
{
    private readonly Control _target;
    private readonly double _threshold;
    private readonly bool _once;
    private bool _visible;
    private bool _disposed;

    public ViewportVisibility(Control target, double threshold = 0.5, bool once = true)
    {
        _target = target;
        _threshold = threshold;
        _once = once;
        target.EffectiveViewportChanged += OnViewportChanged;
    }

    public event EventHandler<bool>? VisibilityChanged;

    public bool IsVisible => _visible;

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var bounds = new Rect(_target.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var intersection = e.EffectiveViewport.Intersect(bounds);
        var ratio = intersection.Width <= 0 || intersection.Height <= 0
            ? 0
            : intersection.Width * intersection.Height / (bounds.Width * bounds.Height);
        var visible = ratio >= _threshold;
        if (visible == _visible)
        {
            return;
        }
        _visible = visible;
        VisibilityChanged?.Invoke(_target, visible);
        if (visible && _once)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _target.EffectiveViewportChanged -= OnViewportChanged;
    }
}

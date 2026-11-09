using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Calls back after the layout pass in which a watched control's bounds changed, for an indicator that follows a row. A
/// permanent <c>LayoutUpdated</c> handler runs on every layout pass of the window, whatever it laid out; this one listens
/// only for the pass that matters, and is called at the moment that handler was: the layout is complete.
/// </summary>
internal sealed class GalleryBoundsWatch
{
    private readonly Control _host;
    private readonly Action _changed;
    private readonly EventHandler<AvaloniaPropertyChangedEventArgs> _onProperty;
    private readonly EventHandler _onLayout;
    private Control[] _watched = [];
    private bool _pending;

    /// <param name="host">The control whose <c>LayoutUpdated</c> carries the call; subscribed only while one is due.</param>
    /// <param name="changed">Called after the layout pass.</param>
    public GalleryBoundsWatch(Control host, Action changed)
    {
        _host = host;
        _changed = changed;
        _onProperty = OnProperty;
        _onLayout = OnLayout;
    }

    /// <summary>Watches <paramref name="controls"/> instead of the ones watched so far.</summary>
    public void Watch(params Control[] controls)
    {
        foreach (var control in _watched)
        {
            control.PropertyChanged -= _onProperty;
        }
        _watched = controls;
        foreach (var control in _watched)
        {
            control.PropertyChanged += _onProperty;
        }
    }

    private void OnProperty(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty && !_pending)
        {
            _pending = true;
            _host.LayoutUpdated += _onLayout;
        }
    }

    private void OnLayout(object? sender, EventArgs e)
    {
        _pending = false;
        _host.LayoutUpdated -= _onLayout;
        _changed();
    }
}

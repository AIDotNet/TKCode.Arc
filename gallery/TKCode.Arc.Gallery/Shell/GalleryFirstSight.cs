using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Runs an action the first time a control's box overlaps the visible part of the window, for content that is built or
/// fetched only once it is seen (demos below the first screen, screenshots, the home page's live cards).
/// </summary>
/// <remarks>
/// <see cref="Layoutable.EffectiveViewportChanged"/> reports the visible area of the control's ancestors in the control's
/// coordinates, not cut to the control itself: a control far below the fold still gets a non-empty viewport, so the test
/// is whether that area overlaps the control's own bounds. The event is raised only when the area moves, and a control
/// that grows in place (its first arrange at the origin) does not move it, so a change of size is checked as well.
/// </remarks>
internal sealed class GalleryFirstSight
{
    private readonly Control _control;
    private readonly Action _seen;
    private Rect? _viewport;
    private bool _listening;

    public GalleryFirstSight(Control control, Action seen)
    {
        _control = control;
        _seen = seen;
        Listen();
    }

    /// <summary>Whether the control has been seen since it was created or <see cref="Reset"/>.</summary>
    public bool IsSeen { get; private set; }

    /// <summary>Waits for the control to be seen again (a view that released its content when it left the tree).</summary>
    public void Reset()
    {
        IsSeen = false;
        _viewport = null;
        Listen();
    }

    private void Listen()
    {
        if (_listening)
        {
            return;
        }
        _listening = true;
        _control.EffectiveViewportChanged += OnViewportChanged;
        _control.PropertyChanged += OnPropertyChanged;
    }

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        _viewport = e.EffectiveViewport;
        // Raised after the arrange pass: what is built now is laid out in the same pass, before the frame is drawn.
        Check(deferred: false);
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty)
        {
            // Bounds change in the middle of arranging: build after it.
            Check(deferred: true);
        }
    }

    private void Check(bool deferred)
    {
        if (IsSeen || _viewport is not { } viewport || !viewport.Intersects(new Rect(_control.Bounds.Size)))
        {
            return;
        }
        IsSeen = true;
        // The layout manager is walking its listeners: leave the list after it.
        Dispatcher.UIThread.Post(StopListening);
        if (deferred)
        {
            Dispatcher.UIThread.Post(_seen);
        }
        else
        {
            _seen();
        }
    }

    private void StopListening()
    {
        if (!_listening || !IsSeen)
        {
            return;
        }
        _listening = false;
        _control.EffectiveViewportChanged -= OnViewportChanged;
        _control.PropertyChanged -= OnPropertyChanged;
    }
}

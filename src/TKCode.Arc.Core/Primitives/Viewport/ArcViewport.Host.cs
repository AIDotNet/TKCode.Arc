using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

// The viewport a control lives in: the nearest ArcViewportHost, otherwise the window's client area. Controls that size
// or shape themselves by "the window" (bottom sheets on narrow screens, menus capped to the screen height, toasts sized
// to the viewport) read it here instead of TopLevel.ClientSize, so they behave the same inside a narrow-viewport
// preview as on a phone. (The class summary is on the other part, Services/ArcViewport.cs.)
public static partial class ArcViewport
{
    /// <summary>The default of <see cref="CompactWidth"/>.</summary>
    public const double DefaultCompactWidth = 640;

    /// <summary>
    /// Viewports narrower than this are compact (<see cref="IsCompact"/>): popups that have a sheet form use it there.
    /// 640 by default; an app may change it at startup.
    /// </summary>
    public static double CompactWidth { get; set; } = DefaultCompactWidth;

    /// <summary>The nearest <see cref="ArcViewportHost"/> that contains <paramref name="visual"/> (or is it), or null.</summary>
    public static ArcViewportHost? FindHost(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (visual is ArcViewportHost self)
        {
            return self;
        }
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ancestor is ArcViewportHost host)
            {
                return host;
            }
        }
        return null;
    }

    /// <summary>
    /// The size of <paramref name="visual"/>'s viewport: its <see cref="ArcViewportHost"/>'s, otherwise the window's client
    /// size; null when it is not attached to a window.
    /// </summary>
    public static Size? GetSize(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (FindHost(visual) is { } host)
        {
            return SizeOf(host);
        }
        return TopLevel.GetTopLevel(visual)?.ClientSize;
    }

    /// <summary>
    /// The viewport's rectangle in the coordinates of <paramref name="visual"/>'s window (the host's bounds after any
    /// transforms, or the whole client area); null when it is not attached to a window.
    /// </summary>
    public static Rect? GetBounds(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (TopLevel.GetTopLevel(visual) is not { } topLevel)
        {
            return null;
        }
        if (FindHost(visual) is { } host && host.TransformToVisual(topLevel) is { } transform)
        {
            return new Rect(SizeOf(host)).TransformToAABB(transform);
        }
        return new Rect(topLevel.ClientSize);
    }

    /// <summary>Whether <paramref name="visual"/>'s viewport is narrower than <see cref="CompactWidth"/>.</summary>
    public static bool IsCompact(Visual visual) => GetSize(visual) is { } size && size.Width < CompactWidth;

    /// <summary>
    /// Calls <paramref name="changed"/> with the viewport size now (when attached) and whenever it changes while
    /// <paramref name="visual"/> is in a window, including when it moves under another host. Dispose the result to stop.
    /// </summary>
    public static IDisposable Observe(Visual visual, Action<Size> changed)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(changed);
        return new Subscription(visual, changed);
    }

    // Before the host was arranged its bounds are empty; an explicit size is the best answer then.
    private static Size SizeOf(ArcViewportHost host)
    {
        var bounds = host.Bounds.Size;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            return bounds;
        }
        return new Size(double.IsNaN(host.Width) ? bounds.Width : host.Width, double.IsNaN(host.Height) ? bounds.Height : host.Height);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Visual _visual;
        private readonly Action<Size> _changed;
        private AvaloniaObject? _source;
        private Size? _last;
        private bool _disposed;

        public Subscription(Visual visual, Action<Size> changed)
        {
            _visual = visual;
            _changed = changed;
            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;
            if (TopLevel.GetTopLevel(visual) is not null)
            {
                Attach();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _visual.AttachedToVisualTree -= OnAttached;
            _visual.DetachedFromVisualTree -= OnDetached;
            Detach();
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Attach();

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Detach();

        private void Attach()
        {
            Detach();
            _source = (AvaloniaObject?)FindHost(_visual) ?? TopLevel.GetTopLevel(_visual);
            if (_source is null)
            {
                return;
            }
            _source.PropertyChanged += OnSourceChanged;
            Raise();
        }

        private void Detach()
        {
            if (_source is not null)
            {
                _source.PropertyChanged -= OnSourceChanged;
                _source = null;
            }
        }

        private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty || e.Property == TopLevel.ClientSizeProperty)
            {
                Raise();
            }
        }

        private void Raise()
        {
            if (GetSize(_visual) is not { } size || size == _last)
            {
                return;
            }
            _last = size;
            _changed(size);
        }
    }
}

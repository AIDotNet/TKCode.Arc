using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Priority tier for mobile system back handling (<see cref="TopLevel.BackRequested"/>): modal overlays intercept
/// before navigation stacks so Android hardware/gesture back dismisses a dialog or sheet first.
/// </summary>
internal enum MobileBackPriority
{
    Navigation = 0,
    Overlay = 1,
}

/// <summary>
/// Coordinates <see cref="TopLevel.BackRequested"/> across mobile overlays (<see cref="Dialog"/>, <see cref="Sheet"/>)
/// and <see cref="NavigationStack"/> instances hosted in the same <see cref="TopLevel"/>. Highest priority wins;
/// within a tier, the most recently registered handler runs first (LIFO). Handlers unsubscribe cleanly when detached.
/// </summary>
internal static class MobileBackNavigation
{
    private static readonly ConditionalWeakTable<TopLevel, Host> s_hosts = new();

    /// <summary>
    /// Registers <paramref name="handler"/> on the <see cref="TopLevel"/> of <paramref name="visual"/>.
    /// Returns a handle that unregisters it when disposed, or null if <paramref name="visual"/> is not attached.
    /// </summary>
    public static IDisposable? Register(Visual visual, MobileBackPriority priority, Func<bool> handler)
    {
        if (TopLevel.GetTopLevel(visual) is not { } topLevel)
        {
            return null;
        }
        var host = s_hosts.GetValue(topLevel, static tl => new Host(tl));
        return host.Add(priority, handler);
    }

    private sealed class Host
    {
        private readonly TopLevel _topLevel;
        private readonly List<Entry> _entries = [];
        private bool _subscribed;

        public Host(TopLevel topLevel)
        {
            _topLevel = topLevel;
        }

        public IDisposable Add(MobileBackPriority priority, Func<bool> handler)
        {
            var entry = new Entry(this, priority, handler);
            _entries.Add(entry);
            if (!_subscribed)
            {
                _subscribed = true;
                _topLevel.BackRequested += OnBackRequested;
            }
            return entry;
        }

        private void Remove(Entry entry)
        {
            if (!_entries.Remove(entry))
            {
                return;
            }
            if (_entries.Count == 0 && _subscribed)
            {
                _subscribed = false;
                _topLevel.BackRequested -= OnBackRequested;
            }
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            if (e.Handled || _entries.Count == 0)
            {
                return;
            }
            // Snapshot so handlers that close an overlay or pop a page can unregister safely during dispatch.
            var snapshot = _entries.ToArray();
            if (TryDispatch(snapshot, MobileBackPriority.Overlay) || TryDispatch(snapshot, MobileBackPriority.Navigation))
            {
                e.Handled = true;
            }
        }

        private static bool TryDispatch(Entry[] snapshot, MobileBackPriority tier)
        {
            for (var i = snapshot.Length - 1; i >= 0; i--)
            {
                var entry = snapshot[i];
                if (entry.Priority == tier && !entry.Disposed && entry.Handler())
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class Entry : IDisposable
        {
            private readonly Host _host;

            public Entry(Host host, MobileBackPriority priority, Func<bool> handler)
            {
                _host = host;
                Priority = priority;
                Handler = handler;
            }

            public MobileBackPriority Priority { get; }

            public Func<bool> Handler { get; }

            public bool Disposed { get; private set; }

            public void Dispose()
            {
                if (Disposed)
                {
                    return;
                }
                Disposed = true;
                _host.Remove(this);
            }
        }
    }
}

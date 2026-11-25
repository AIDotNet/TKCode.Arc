// Ported from vscode (src/vs/base/common/event.ts: Emitter.fire, _deliver, _deliverQueue and EventDeliveryQueuePrivate,
// commit b57d5c9e), MIT license. Only the delivery is ported: no leak monitor, no performance monitor, no options.
//
// Why not a .NET event: a .NET event calls its handlers one after the other, so when a handler raises the event again
// the second event reaches the remaining handlers before the first one does. Upstream's emitter first finishes
// delivering the event in flight to the remaining listeners, then delivers the new one, so every listener sees the
// events in the order they were raised (a listener that edits the model from DidChangeContent relies on it). A listener
// that throws is reported to UnexpectedErrors and the others still get the event, as upstream's onUnexpectedError.

namespace TKCode.Arc.Editor;

/// <summary>An event source with upstream's delivery order; exposed through a .NET event's add and remove.</summary>
internal sealed class Emitter<T>
{
    // Removed listeners become null so that a delivery in progress keeps its indices; compacted when nothing is delivered.
    private readonly List<Entry?> _listeners = new();

    // The delivery in progress: which event, the next listener index and the end (listeners added later do not get it).
    private bool _delivering;
    private int _index;
    private int _end;
    private T _value = default!;
    private int _size;

    public bool HasListeners => _size > 0;

    /// <summary>Adds a listener; <paramref name="key"/> is what <see cref="Remove"/> finds it by (the listener itself by default).</summary>
    public void Add(Action<T> listener, object? key = null)
    {
        _listeners.Add(new Entry(listener, key ?? listener));
        _size++;
    }

    /// <summary>Removes the last listener added with <paramref name="key"/>, like removing a .NET event handler.</summary>
    public void Remove(object? key)
    {
        if (key is null)
        {
            return;
        }

        for (var i = _listeners.Count - 1; i >= 0; i--)
        {
            if (_listeners[i] is { } entry && Equals(entry.Key, key))
            {
                _listeners[i] = null;
                _size--;
                Compact();
                return;
            }
        }
    }

    /// <summary>Removes every listener.</summary>
    public void Clear()
    {
        for (var i = 0; i < _listeners.Count; i++)
        {
            _listeners[i] = null;
        }

        _size = 0;
        _index = _end; // force any current emission loop to stop, mainly for during dispose
        Compact();
    }

    public void Fire(T value)
    {
        if (_delivering)
        {
            // finish the event in flight before this one
            DeliverQueue();
        }

        if (_size == 0)
        {
            return;
        }

        _delivering = true;
        _index = 0;
        _end = _listeners.Count;
        _value = value;
        DeliverQueue();
    }

    private void DeliverQueue()
    {
        while (_index < _end)
        {
            // important: the index is incremented before calling the listener because it might reenter DeliverQueue()
            var entry = _listeners[_index++];
            if (entry is not null)
            {
                Deliver(entry.Listener, _value);
            }
        }

        _delivering = false;
        _value = default!;
        Compact();
    }

    private static void Deliver(Action<T> listener, T value)
    {
        try
        {
            listener(value);
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    private void Compact()
    {
        if (!_delivering && _listeners.Count != _size)
        {
            _listeners.RemoveAll(static entry => entry is null);
        }
    }

    private sealed record Entry(Action<T> Listener, object Key);
}

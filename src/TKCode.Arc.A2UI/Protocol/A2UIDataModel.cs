using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>
/// The JSON document behind one surface. Paths are JSON Pointers (<see cref="A2UIJsonPointer"/>). Writes create missing
/// objects and lists on the way, pad lists with nulls, and refuse to write through a string, number or boolean. A
/// subscriber of a path is told when the value at that path changes: a write to the path itself, to a parent (the
/// value under it is replaced) or to a child (the value contains it). Rewriting an equal value notifies nobody.
/// </summary>
public sealed class A2UIDataModel : IDisposable
{
    private readonly List<Subscription> _subscriptions = new();
    private JsonNode? _root;
    private int _batchDepth;
    private bool _disposed;
    private int _version;

    public A2UIDataModel(JsonNode? initial = null)
    {
        _root = initial is null ? new JsonObject() : initial.DeepClone();
    }

    /// <summary>Grows by one on every write that changed something.</summary>
    public int Version => _version;

    /// <summary>Raised after every write that changed something, with the normalized path written.</summary>
    public event Action<string>? Changed;

    /// <summary>The value at <paramref name="path"/> (a deep copy), or null when absent.</summary>
    public JsonNode? Get(string? path) => Peek(path)?.DeepClone();

    /// <summary>The live value at <paramref name="path"/>, or null when absent. Callers must not change it.</summary>
    public JsonNode? Peek(string? path) => Find(A2UIJsonPointer.Parse(path));

    /// <summary>True when a non-null value exists at <paramref name="path"/>.</summary>
    public bool Contains(string? path) => Peek(path) is not null;

    /// <summary>Writes a copy of <paramref name="value"/> at <paramref name="path"/> (<c>/</c> replaces the whole document).</summary>
    public void Set(string? path, JsonNode? value)
    {
        var segments = A2UIJsonPointer.Parse(path);
        var copy = value?.DeepClone();
        var normalized = A2UIJsonPointer.Format(segments);
        if (A2UIJson.DeepEquals(Find(segments), copy))
        {
            return;
        }

        var snapshot = CaptureAffected(normalized);
        if (segments.Length == 0)
        {
            _root = copy;
        }
        else
        {
            var parent = Walk(segments, normalized);
            Assign(parent, segments[^1], copy, normalized);
        }

        Commit(normalized, snapshot);
    }

    /// <summary>
    /// Removes the value at <paramref name="path"/>: an object key is removed, a list element becomes null (so later
    /// indices keep their place), the root becomes an empty object. A missing path is not an error.
    /// </summary>
    public void Delete(string? path)
    {
        var segments = A2UIJsonPointer.Parse(path);
        var normalized = A2UIJsonPointer.Format(segments);
        if (segments.Length == 0)
        {
            var snapshotRoot = CaptureAffected(normalized);
            if (_root is JsonObject { Count: 0 })
            {
                return;
            }

            _root = new JsonObject();
            Commit(normalized, snapshotRoot);
            return;
        }

        var parent = Find(segments.AsSpan(0, segments.Length - 1));
        var last = segments[^1];
        switch (parent)
        {
            case JsonObject obj when obj.ContainsKey(last):
            {
                var snapshot = CaptureAffected(normalized);
                obj.Remove(last);
                Commit(normalized, snapshot);
                break;
            }
            case JsonArray array when A2UIJsonPointer.TryParseIndex(last, out var index) && index < array.Count && array[index] is not null:
            {
                var snapshot = CaptureAffected(normalized);
                array[index] = null;
                Commit(normalized, snapshot);
                break;
            }
        }
    }

    /// <summary>
    /// Calls <paramref name="onChange"/> with the new value whenever the value at <paramref name="path"/> changes.
    /// Dispose the result to stop.
    /// </summary>
    public IDisposable Subscribe(string? path, Action<JsonNode?> onChange)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        var subscription = new Subscription(this, A2UIJsonPointer.Normalize(path), onChange);
        if (!_disposed)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>The number of live subscriptions (for tests and diagnostics).</summary>
    public int SubscriptionCount => _subscriptions.Count;

    /// <summary>
    /// Holds notifications until the returned scope is disposed; each subscriber is then told once, with its current
    /// value, if any write inside the scope changed it.
    /// </summary>
    public IDisposable BeginBatch()
    {
        _batchDepth++;
        return new BatchScope(this);
    }

    /// <summary>Stops all notifications; the document stays readable and writable.</summary>
    public void Dispose()
    {
        _disposed = true;
        foreach (var subscription in _subscriptions)
        {
            subscription.Detached = true;
        }

        _subscriptions.Clear();
        Changed = null;
    }

    /// <summary>The live root (tests and serialization).</summary>
    internal JsonNode? Root => _root;

    private JsonNode? Find(ReadOnlySpan<string> segments)
    {
        var node = _root;
        foreach (var segment in segments)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (!obj.TryGetPropertyValue(segment, out node))
                    {
                        return null;
                    }

                    break;
                case JsonArray array:
                    if (!A2UIJsonPointer.TryParseIndex(segment, out var index) || index >= array.Count)
                    {
                        return null;
                    }

                    node = array[index];
                    break;
                default:
                    return null;
            }
        }

        return node;
    }

    private JsonNode Walk(string[] segments, string pointer)
    {
        if (_root is null)
        {
            _root = new JsonObject();
        }

        if (_root is not (JsonObject or JsonArray))
        {
            throw A2UIException.DataError($"Cannot set path '{pointer}': the document root is not an object or a list.");
        }

        var node = _root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            var next = Child(node, segment, pointer);
            if (next is null)
            {
                next = A2UIJsonPointer.TryParseIndex(segments[i + 1], out _) ? new JsonArray() : new JsonObject();
                Assign(node, segment, next, pointer);
            }
            else if (next is not (JsonObject or JsonArray))
            {
                throw A2UIException.DataError($"Cannot set path '{pointer}': '{segment}' holds a {A2UIJson.TypeName(next)}.");
            }

            node = next;
        }

        return node;
    }

    private static JsonNode? Child(JsonNode node, string segment, string pointer)
    {
        if (node is JsonObject obj)
        {
            return obj.TryGetPropertyValue(segment, out var value) ? value : null;
        }

        var array = (JsonArray)node;
        var index = ListIndex(segment, pointer);
        return index < array.Count ? array[index] : null;
    }

    private static void Assign(JsonNode container, string segment, JsonNode? value, string pointer)
    {
        if (container is JsonObject obj)
        {
            obj[segment] = value;
            return;
        }

        if (container is not JsonArray array)
        {
            throw A2UIException.DataError($"Cannot set path '{pointer}'.");
        }

        var index = ListIndex(segment, pointer);
        while (array.Count < index)
        {
            array.Add(null);
        }

        if (index == array.Count)
        {
            array.Add(value);
        }
        else
        {
            array[index] = value;
        }
    }

    private static int ListIndex(string segment, string pointer)
    {
        if (segment == "-")
        {
            throw A2UIException.DataError($"Cannot set path '{pointer}': '-' is not supported, use an index.");
        }

        if (!A2UIJsonPointer.TryParseIndex(segment, out var index))
        {
            throw A2UIException.DataError($"Cannot set path '{pointer}': non-numeric segment '{segment}' in a list.");
        }

        if (index > A2UIJsonPointer.MaxListIndex)
        {
            throw A2UIException.DataError($"Cannot set path '{pointer}': list index {index} is above {A2UIJsonPointer.MaxListIndex}.");
        }

        return index;
    }

    // Values of the subscriptions at or under the written path, taken before the write. Subscriptions above it
    // (ancestors) change whenever the written value changes.
    private List<(Subscription Sub, JsonNode? Old, bool Ancestor)>? CaptureAffected(string pointer)
    {
        if (_disposed || _subscriptions.Count == 0)
        {
            return null;
        }

        List<(Subscription, JsonNode?, bool)>? list = null;
        foreach (var sub in _subscriptions)
        {
            if (A2UIJsonPointer.IsAncestorOrSelf(pointer, sub.Path))
            {
                list ??= new();
                list.Add((sub, Peek(sub.Path), false));
            }
            else if (A2UIJsonPointer.IsAncestorOrSelf(sub.Path, pointer))
            {
                list ??= new();
                list.Add((sub, null, true));
            }
        }

        return list;
    }

    private void Commit(string pointer, List<(Subscription Sub, JsonNode? Old, bool Ancestor)>? affected)
    {
        _version++;
        if (affected is not null)
        {
            foreach (var (sub, old, ancestor) in affected)
            {
                if (!ancestor)
                {
                    var now = Peek(sub.Path);
                    if (ReferenceEquals(old, now) || A2UIJson.DeepEquals(old, now))
                    {
                        continue;
                    }
                }

                sub.Dirty = true;
            }
        }

        if (_batchDepth == 0)
        {
            Flush();
        }

        Changed?.Invoke(pointer);
    }

    private void Flush()
    {
        if (_subscriptions.Count == 0)
        {
            return;
        }

        // Copy first: a callback may subscribe or unsubscribe.
        var pending = new List<Subscription>();
        foreach (var sub in _subscriptions)
        {
            if (sub.Dirty)
            {
                sub.Dirty = false;
                pending.Add(sub);
            }
        }

        foreach (var sub in pending)
        {
            if (!sub.Detached)
            {
                sub.Callback(Peek(sub.Path));
            }
        }
    }

    private void EndBatch()
    {
        if (_batchDepth > 0 && --_batchDepth == 0)
        {
            Flush();
        }
    }

    private void Remove(Subscription subscription)
    {
        subscription.Detached = true;
        _subscriptions.Remove(subscription);
    }

    private sealed class Subscription(A2UIDataModel owner, string path, Action<JsonNode?> callback) : IDisposable
    {
        public string Path { get; } = path;
        public Action<JsonNode?> Callback { get; } = callback;
        public bool Dirty { get; set; }
        public bool Detached { get; set; }

        public void Dispose()
        {
            if (!Detached)
            {
                owner.Remove(this);
            }
        }
    }

    private sealed class BatchScope(A2UIDataModel owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (!_done)
            {
                _done = true;
                owner.EndBatch();
            }
        }
    }
}

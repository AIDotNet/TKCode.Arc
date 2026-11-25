// Ported from vscode (src/vs/platform/contextkey/browser/contextKeyService.ts, commit b57d5c9e), MIT license; the
// PauseableEmitter inside AbstractContextKeyService is ported from src/vs/base/common/event.ts (PauseableEmitter, same
// commit; only the merging mode, which is the one the service uses). Only the parts without a DOM or a configuration service. Differences from upstream:
//  - No DOM: a scoped service is created without a target element, nothing writes or reads a data-keybinding-context
//    attribute (findContextAttr is gone), and GetContext() returns the service's own context; a host maps its focused
//    element to a scoped service itself. The root service does not track whether an input element has focus (upstream
//    binds InputFocusedContext, 'inputFocus', to the window's focus events).
//  - No configuration context: upstream's root context (ConfigAwareContextValuesContainer) answers `config.*` keys from
//    the configuration service and reports configuration changes as context changes; here the root context is a plain
//    Context, so a `config.*` key is just another key an application sets.
//  - The `_setContext`, `getContextKeyInfo` and `_generateContextKeyInfo` commands are not registered (commands are not
//    part of this port; RawContextKey.Info replaces the info registry).
//  - Values are compared with ContextKeyValues.DeepEquals (objects.equals); null stands for undefined, so setting a key
//    to null leaves it own but lets reads fall through to the parent, as setting it to undefined does upstream.
//  - Using a disposed service throws ObjectDisposedException (upstream: Error); UpdateParent with a service that is not
//    an AbstractContextKeyService (an overlay) throws ArgumentException.

namespace TKCode.Arc.Editor.Platform;

/// <summary>The values of one context; reads fall through to the parent context.</summary>
internal class Context : IContext
{
    protected Context? _parent;
    protected readonly Dictionary<string, object?> _value;
    protected readonly int _id;

    public Context(int id, Context? parent)
    {
        _id = id;
        _parent = parent;
        _value = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["_contextId"] = id,
        };
    }

    /// <summary>A copy of this context's own values.</summary>
    public IReadOnlyDictionary<string, object?> Value => new Dictionary<string, object?>(_value, StringComparer.Ordinal);

    public virtual bool SetValue(string key, object? value)
    {
        _value.TryGetValue(key, out var current);
        if (!ContextKeyValues.DeepEquals(current, value))
        {
            _value[key] = value;
            return true;
        }
        return false;
    }

    public virtual bool RemoveValue(string key)
    {
        return _value.Remove(key);
    }

    public virtual object? GetValue(string key)
    {
        _value.TryGetValue(key, out var ret);
        if (ret is null && _parent is not null)
        {
            return _parent.GetValue(key);
        }
        return ret;
    }

    public bool HasOwnValue(string key)
    {
        return _value.ContainsKey(key);
    }

    /// <summary>
    /// Sets a new parent and returns the keys whose value in <see cref="CollectAllValues"/> changed. The ancestors that
    /// the old and the new parent share provide the same values before and after, so only the values of the ancestors
    /// that they don't share are compared.
    /// </summary>
    public virtual List<string> UpdateParent(Context parent)
    {
        var oldParent = _parent;
        _parent = parent;

        var oldAncestors = GetAncestorsOrSelf(oldParent);
        var newAncestors = GetAncestorsOrSelf(parent);
        var sharedAncestor = oldAncestors.Find(context => newAncestors.Contains(context));

        var candidateKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void AddCandidate(string key)
        {
            if (seen.Add(key))
            {
                candidateKeys.Add(key);
            }
        }

        if (sharedAncestor is not null)
        {
            var notShared = oldAncestors.Take(oldAncestors.IndexOf(sharedAncestor))
                .Concat(newAncestors.Take(newAncestors.IndexOf(sharedAncestor)));
            foreach (var context in notShared)
            {
                foreach (var key in context._value.Keys)
                {
                    AddCandidate(key);
                }
            }
        }
        else
        {
            foreach (var context in new[] { oldParent, parent })
            {
                if (context is not null)
                {
                    foreach (var key in context.CollectAllValues().Keys)
                    {
                        AddCandidate(key);
                    }
                }
            }
        }

        var changedKeys = new List<string>();
        foreach (var key in candidateKeys)
        {
            if (key == "_contextId" || _value.ContainsKey(key))
            {
                continue;
            }
            if (!ContextKeyValues.DeepEquals(oldParent?.GetCollectedValue(key), parent.GetCollectedValue(key)))
            {
                changedKeys.Add(key);
            }
        }
        return changedKeys;
    }

    /// <summary>The values of this context and its ancestors, the nearest winning; without <c>_contextId</c>.</summary>
    public virtual Dictionary<string, object?> CollectAllValues()
    {
        var result = _parent is not null
            ? _parent.CollectAllValues()
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in _value)
        {
            result[key] = value;
        }
        result.Remove("_contextId");
        return result;
    }

    /// <summary>Returns the value of <paramref name="key"/> in <see cref="CollectAllValues"/> without collecting all values.</summary>
    protected virtual object? GetCollectedValue(string key)
    {
        if (_value.TryGetValue(key, out var value))
        {
            return value;
        }
        return _parent?.GetCollectedValue(key);
    }

    private static List<Context> GetAncestorsOrSelf(Context? context)
    {
        var result = new List<Context>();
        for (var candidate = context; candidate is not null; candidate = candidate._parent)
        {
            result.Add(candidate);
        }
        return result;
    }
}

/// <summary>The context of a disposed service or an unknown context id: empty and unchangeable.</summary>
internal sealed class NullContext : Context
{
    // Immutable: every method that would change it is overridden to do nothing.
    public static readonly NullContext INSTANCE = new();

    private NullContext()
        : base(-1, null)
    {
    }

    public override bool SetValue(string key, object? value)
    {
        return false;
    }

    public override bool RemoveValue(string key)
    {
        return false;
    }

    public override object? GetValue(string key)
    {
        return null;
    }

    public override List<string> UpdateParent(Context parent)
    {
        return new List<string>();
    }

    public override Dictionary<string, object?> CollectAllValues()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    protected override object? GetCollectedValue(string key)
    {
        return null;
    }
}

internal sealed class ContextKey<T> : IContextKey<T>
{
    private readonly AbstractContextKeyService _service;
    private readonly string _key;
    private readonly T? _defaultValue;

    public ContextKey(AbstractContextKeyService service, string key, T? defaultValue)
    {
        _service = service;
        _key = key;
        _defaultValue = defaultValue;
        Reset();
    }

    public void Set(T value)
    {
        _service.SetContext(_key, value);
    }

    public void Reset()
    {
        if (_defaultValue is null)
        {
            _service.RemoveContext(_key);
        }
        else
        {
            _service.SetContext(_key, _defaultValue);
        }
    }

    public T? Get()
    {
        return _service.GetContextKeyValue(_key) is T value ? value : default;
    }
}

internal sealed class SimpleContextKeyChangeEvent(string key) : IContextKeyChangeEvent
{
    public string Key { get; } = key;

    public bool AffectsSome(IReadableSet<string> keys)
    {
        return keys.Has(Key);
    }

    public bool AllKeysContainedIn(IReadableSet<string> keys)
    {
        return AffectsSome(keys);
    }
}

internal sealed class ArrayContextKeyChangeEvent(IReadOnlyList<string> keys) : IContextKeyChangeEvent
{
    public IReadOnlyList<string> Keys { get; } = keys;

    public bool AffectsSome(IReadableSet<string> keys)
    {
        foreach (var key in Keys)
        {
            if (keys.Has(key))
            {
                return true;
            }
        }
        return false;
    }

    public bool AllKeysContainedIn(IReadableSet<string> keys)
    {
        return Keys.All(keys.Has);
    }
}

internal sealed class CompositeContextKeyChangeEvent(IReadOnlyList<IContextKeyChangeEvent> events) : IContextKeyChangeEvent
{
    public IReadOnlyList<IContextKeyChangeEvent> Events { get; } = events;

    public bool AffectsSome(IReadableSet<string> keys)
    {
        foreach (var e in Events)
        {
            if (e.AffectsSome(keys))
            {
                return true;
            }
        }
        return false;
    }

    public bool AllKeysContainedIn(IReadableSet<string> keys)
    {
        return Events.All(evt => evt.AllKeysContainedIn(keys));
    }
}

/// <summary>What an overlay needs from the service it overlays.</summary>
internal interface IContextValuesProvider
{
    int ContextId { get; }

    IContext GetContextValuesContainer(int contextId);
}

internal abstract class AbstractContextKeyService : IContextKeyService, IContextValuesProvider, IDisposable
{
    protected bool _isDisposed;
    protected readonly int _myContextId;

    protected readonly PauseableEmitter<IContextKeyChangeEvent> _onDidChangeContext =
        new(static input => new CompositeContextKeyChangeEvent(input));

    protected AbstractContextKeyService(int myContextId)
    {
        _isDisposed = false;
        _myContextId = myContextId;
    }

    public event Action<IContextKeyChangeEvent>? DidChangeContext
    {
        add
        {
            if (value is not null)
            {
                _onDidChangeContext.Add(value);
            }
        }
        remove => _onDidChangeContext.Remove(value);
    }

    public int ContextId => _myContextId;

    public IContextKey<T> CreateKey<T>(string key, T? defaultValue)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AbstractContextKeyService), "AbstractContextKeyService has been disposed");
        }
        return new ContextKey<T>(this, key, defaultValue);
    }

    public void BufferChangeEvents(Action callback)
    {
        _onDidChangeContext.Pause();
        try
        {
            callback();
        }
        finally
        {
            _onDidChangeContext.Resume();
        }
    }

    public IScopedContextKeyService CreateScoped()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AbstractContextKeyService), "AbstractContextKeyService has been disposed");
        }
        return new ScopedContextKeyService(this);
    }

    public IContextKeyService CreateOverlay(IEnumerable<KeyValuePair<string, object?>> overlay)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AbstractContextKeyService), "AbstractContextKeyService has been disposed");
        }
        return new OverlayContextKeyService(this, this, overlay);
    }

    public bool ContextMatchesRules(ContextKeyExpression? rules)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AbstractContextKeyService), "AbstractContextKeyService has been disposed");
        }
        var context = GetContextValuesContainer(_myContextId);
        var result = rules?.Evaluate(context) ?? true;
        return result;
    }

    public object? GetContextKeyValue(string key)
    {
        if (_isDisposed)
        {
            return null;
        }
        return GetContextValuesContainer(_myContextId).GetValue(key);
    }

    public void SetContext(string key, object? value)
    {
        if (_isDisposed)
        {
            return;
        }
        var myContext = GetContextValuesContainer(_myContextId);
        if (myContext.SetValue(key, value))
        {
            _onDidChangeContext.Fire(new SimpleContextKeyChangeEvent(key));
        }
    }

    public void RemoveContext(string key)
    {
        if (_isDisposed)
        {
            return;
        }
        if (GetContextValuesContainer(_myContextId).RemoveValue(key))
        {
            _onDidChangeContext.Fire(new SimpleContextKeyChangeEvent(key));
        }
    }

    public IContext GetContext()
    {
        if (_isDisposed)
        {
            return NullContext.INSTANCE;
        }
        return GetContextValuesContainer(_myContextId);
    }

    IContext IContextValuesProvider.GetContextValuesContainer(int contextId) => GetContextValuesContainer(contextId);

    public abstract Context GetContextValuesContainer(int contextId);

    public abstract int CreateChildContext(int parentContextId);

    /// <summary>Creates a context whose parent is this service's context.</summary>
    public int CreateChildContext() => CreateChildContext(_myContextId);

    public abstract void DisposeContext(int contextId);

    public abstract void UpdateParent(IContextKeyService parentContextKeyService);

    public virtual void Dispose()
    {
        _onDidChangeContext.Clear();
        _isDisposed = true;
    }

    /// <summary>
    /// An emitter that can hold events back: while paused, fired events queue up and are merged into one when it
    /// resumes. Events fired while nobody listens are dropped, as upstream.
    /// </summary>
    protected sealed class PauseableEmitter<TEvent>(Func<IReadOnlyList<TEvent>, TEvent> mergeFn)
    {
        private readonly Emitter<TEvent> _emitter = new();
        private readonly Func<IReadOnlyList<TEvent>, TEvent> _mergeFn = mergeFn;
        private readonly List<TEvent> _eventQueue = new();
        private int _isPaused;

        public void Add(Action<TEvent> listener) => _emitter.Add(listener);

        public void Remove(Action<TEvent>? listener) => _emitter.Remove(listener);

        public void Clear() => _emitter.Clear();

        public void Pause()
        {
            _isPaused++;
        }

        public void Resume()
        {
            if (_isPaused != 0 && --_isPaused == 0)
            {
                // use the merge function to create a single composite
                // event. make a copy in case firing pauses this emitter
                if (_eventQueue.Count > 0)
                {
                    var events = _eventQueue.ToArray();
                    _eventQueue.Clear();
                    _emitter.Fire(_mergeFn(events));
                }
            }
        }

        public void Fire(TEvent e)
        {
            if (_emitter.HasListeners)
            {
                if (_isPaused != 0)
                {
                    _eventQueue.Add(e);
                }
                else
                {
                    _emitter.Fire(e);
                }
            }
        }
    }
}

/// <summary>The root context key service; it owns every context, scoped services' included.</summary>
internal sealed class ContextKeyService : AbstractContextKeyService
{
    private int _lastContextId;
    private readonly Dictionary<int, Context> _contexts = new();

    public ContextKeyService()
        : base(0)
    {
        _lastContextId = 0;

        var myContext = new Context(_myContextId, null);
        _contexts[_myContextId] = myContext;
    }

    public override Context GetContextValuesContainer(int contextId)
    {
        if (_isDisposed)
        {
            return NullContext.INSTANCE;
        }
        return _contexts.TryGetValue(contextId, out var context) ? context : NullContext.INSTANCE;
    }

    public override int CreateChildContext(int parentContextId)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ContextKeyService), "ContextKeyService has been disposed");
        }
        var id = ++_lastContextId;
        _contexts[id] = new Context(id, GetContextValuesContainer(parentContextId));
        return id;
    }

    public override void DisposeContext(int contextId)
    {
        if (!_isDisposed)
        {
            _contexts.Remove(contextId);
        }
    }

    public override void UpdateParent(IContextKeyService parentContextKeyService)
    {
        throw new InvalidOperationException("Cannot update parent of root ContextKeyService");
    }
}

internal sealed class ScopedContextKeyService : AbstractContextKeyService, IScopedContextKeyService
{
    private AbstractContextKeyService _parent;
    private Action<IContextKeyChangeEvent>? _parentChangeListener;

    public ScopedContextKeyService(AbstractContextKeyService parent)
        : base(parent.CreateChildContext())
    {
        _parent = parent;
        UpdateParentChangeListener();
    }

    private void UpdateParentChangeListener()
    {
        ClearParentChangeListener();

        // Forward parent events to this listener. Parent will change.
        var listener = new Action<IContextKeyChangeEvent>(e =>
        {
            var thisContainer = _parent.GetContextValuesContainer(_myContextId);
            if (!e.AllKeysContainedIn(ReadableSet.From<string>(thisContainer.HasOwnValue)))
            {
                _onDidChangeContext.Fire(e);
            }
        });
        _parentChangeListener = listener;
        _parent.DidChangeContext += listener;
    }

    private void ClearParentChangeListener()
    {
        if (_parentChangeListener is not null)
        {
            _parent.DidChangeContext -= _parentChangeListener;
            _parentChangeListener = null;
        }
    }

    public override void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // Clear the parent change listener before disposeContext to avoid
        // forwarding parent events after this service has begun tearing down.
        ClearParentChangeListener();
        _parent.DisposeContext(_myContextId);
        base.Dispose();
    }

    public override Context GetContextValuesContainer(int contextId)
    {
        if (_isDisposed)
        {
            return NullContext.INSTANCE;
        }
        return _parent.GetContextValuesContainer(contextId);
    }

    public override int CreateChildContext(int parentContextId)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ScopedContextKeyService), "ScopedContextKeyService has been disposed");
        }
        return _parent.CreateChildContext(parentContextId);
    }

    public override void DisposeContext(int contextId)
    {
        // Always forward to parent even after disposal — a child context may
        // be disposed after us and must still reach the root ContextKeyService
        // to delete its entry from _contexts.
        _parent.DisposeContext(contextId);
    }

    public override void UpdateParent(IContextKeyService parentContextKeyService)
    {
        if (parentContextKeyService is not AbstractContextKeyService newParent)
        {
            throw new ArgumentException("The parent must be a context key service or a scoped one.", nameof(parentContextKeyService));
        }
        if (ReferenceEquals(_parent, newParent))
        {
            return;
        }

        var thisContainer = _parent.GetContextValuesContainer(_myContextId);
        ClearParentChangeListener();
        _parent = newParent;
        UpdateParentChangeListener();
        var newParentContainer = _parent.GetContextValuesContainer(_parent.ContextId);
        var changedKeys = thisContainer.UpdateParent(newParentContainer);

        _onDidChangeContext.Fire(new ArrayContextKeyChangeEvent(changedKeys));
    }
}

internal sealed class OverlayContext(IContext parent, IReadOnlyDictionary<string, object?> overlay) : IContext
{
    public object? GetValue(string key)
    {
        return overlay.TryGetValue(key, out var value) ? value : parent.GetValue(key);
    }
}

internal sealed class OverlayContextKeyService : IContextKeyService, IContextValuesProvider
{
    private readonly IContextKeyService _parent;
    private readonly IContextValuesProvider _parentValues;
    private readonly Dictionary<string, object?> _overlay;

    public OverlayContextKeyService(IContextKeyService parent, IContextValuesProvider parentValues, IEnumerable<KeyValuePair<string, object?>> overlay)
    {
        _parent = parent;
        _parentValues = parentValues;
        _overlay = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in overlay)
        {
            _overlay[key] = value;
        }
    }

    public int ContextId => _parentValues.ContextId;

    public event Action<IContextKeyChangeEvent>? DidChangeContext
    {
        add => _parent.DidChangeContext += value;
        remove => _parent.DidChangeContext -= value;
    }

    public void BufferChangeEvents(Action callback)
    {
        _parent.BufferChangeEvents(callback);
    }

    public IContextKey<T> CreateKey<T>(string key, T? defaultValue)
    {
        throw new NotSupportedException("Not supported.");
    }

    public IContext GetContext()
    {
        return new OverlayContext(_parent.GetContext(), _overlay);
    }

    public IContext GetContextValuesContainer(int contextId)
    {
        var parentContext = _parentValues.GetContextValuesContainer(contextId);
        return new OverlayContext(parentContext, _overlay);
    }

    public bool ContextMatchesRules(ContextKeyExpression? rules)
    {
        var context = GetContextValuesContainer(ContextId);
        var result = rules?.Evaluate(context) ?? true;
        return result;
    }

    public object? GetContextKeyValue(string key)
    {
        return _overlay.TryGetValue(key, out var value) ? value : _parent.GetContextKeyValue(key);
    }

    public IScopedContextKeyService CreateScoped()
    {
        throw new NotSupportedException("Not supported.");
    }

    public IContextKeyService CreateOverlay(IEnumerable<KeyValuePair<string, object?>> overlay)
    {
        return new OverlayContextKeyService(this, this, overlay);
    }

    public void UpdateParent(IContextKeyService parentContextKeyService)
    {
        throw new NotSupportedException("Not supported.");
    }
}

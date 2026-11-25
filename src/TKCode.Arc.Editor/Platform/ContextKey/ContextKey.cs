// Ported from vscode (src/vs/platform/contextkey/common/contextkey.ts: ContextKeyInfo, RawContextKey, ContextKeyValue,
// IContext, IContextKey, IReadableSet, IContextKeyChangeEvent, IContextKeyService, IScopedContextKeyService, commit
// b57d5c9e), MIT license. Differences from upstream:
//  - RawContextKey keeps its ContextKeyInfo in Info (null for a hidden key) instead of adding it to the process-wide
//    RawContextKey._info list (and RawContextKey.all() is gone): an application that wants the list of keys collects the
//    infos of the keys it defines (EditorContextKeys.All lists the editor's).
//  - A value is object? (ContextKeyValues.cs describes the JavaScript semantics); null stands for undefined. For a value
//    type T, an undefined key reads as default(T) through IContextKey<T>.Get and RawContextKey<T>.GetValue; use a
//    nullable T (RawContextKey<bool?>) to tell undefined apart.
//  - No DOM: CreateScoped takes no target element and GetContext() returns the service's own context, where upstream
//    finds the context of a DOM element through its data-keybinding-context attribute. A host keeps the mapping from
//    its focused element to the scoped service itself. IContextKeyServiceTarget is left out.
//  - onDidChangeContext is the DidChangeContext event; an IReadableSet can be made with ReadableSet.Of or ReadableSet.From.

namespace TKCode.Arc.Editor.Platform;

/// <summary>A context key's name, value type and description, for documentation and tooling.</summary>
internal sealed record ContextKeyInfo(string Key, string? Type = null, string? Description = null);

/// <summary>A context key: an expression testing that the key is set, and a way to create, read and test its value.</summary>
internal sealed class RawContextKey<T> : ContextKeyDefinedExpr
{
    private readonly T? _defaultValue;

    /// <summary>A key described by <paramref name="description"/>; its info's type is the default value's JavaScript type.</summary>
    public RawContextKey(string key, T? defaultValue, string? description = null)
        : base(key, null)
    {
        _defaultValue = defaultValue;
        Info = new ContextKeyInfo(key, TypeOf(defaultValue), description);
    }

    /// <summary>A key with an explicit <paramref name="type"/> and <paramref name="description"/>.</summary>
    public RawContextKey(string key, T? defaultValue, string type, string description)
        : base(key, null)
    {
        _defaultValue = defaultValue;
        Info = new ContextKeyInfo(key, type, description);
    }

    /// <summary>A key that, when <paramref name="hide"/> is true, has no info (upstream passes <c>true</c> to hide it).</summary>
    public RawContextKey(string key, T? defaultValue, bool hide)
        : base(key, null)
    {
        _defaultValue = defaultValue;
        Info = hide ? null : new ContextKeyInfo(key, TypeOf(defaultValue));
    }

    /// <summary>The key's description, or null for a hidden key.</summary>
    public ContextKeyInfo? Info { get; }

    private static string? TypeOf(T? value)
    {
        if (value is null)
        {
            return null;
        }
        if (value is bool)
        {
            return "boolean";
        }
        if (value is string)
        {
            return "string";
        }
        return ContextKeyValues.TryGetNumber(value, out _) ? "number" : "object";
    }

    public IContextKey<T> BindTo(IContextKeyService target)
    {
        return target.CreateKey(Key, _defaultValue);
    }

    public T? GetValue(IContextKeyService target)
    {
        return target.GetContextKeyValue(Key) is T value ? value : default;
    }

    public ContextKeyExpression ToNegated()
    {
        return Negate();
    }

    public ContextKeyExpression IsEqualTo(object? value)
    {
        return ContextKeyEqualsExpr.Create(Key, value);
    }

    public ContextKeyExpression NotEqualsTo(object? value)
    {
        return ContextKeyNotEqualsExpr.Create(Key, value);
    }

    public ContextKeyExpression Greater(object? value)
    {
        return ContextKeyGreaterExpr.Create(Key, value);
    }
}

/// <summary>The values an expression is evaluated against.</summary>
internal interface IContext
{
    /// <summary>The value of <paramref name="key"/>; null when it is not set.</summary>
    object? GetValue(string key);
}

/// <summary>A key created in a context key service.</summary>
internal interface IContextKey<T>
{
    void Set(T value);

    void Reset();

    T? Get();
}

/// <summary>A set that can only be asked whether it contains a value.</summary>
internal interface IReadableSet<in T>
{
    bool Has(T value);
}

/// <summary>Makes <see cref="IReadableSet{T}"/>s.</summary>
internal static class ReadableSet
{
    public static IReadableSet<string> Of(params string[] values) => new SetAdapter(new HashSet<string>(values, StringComparer.Ordinal));

    public static IReadableSet<T> From<T>(Func<T, bool> has) => new PredicateAdapter<T>(has);

    private sealed class SetAdapter(HashSet<string> set) : IReadableSet<string>
    {
        public bool Has(string value) => set.Contains(value);
    }

    private sealed class PredicateAdapter<T>(Func<T, bool> has) : IReadableSet<T>
    {
        public bool Has(T value) => has(value);
    }
}

/// <summary>Which keys a change of context touched.</summary>
internal interface IContextKeyChangeEvent
{
    bool AffectsSome(IReadableSet<string> keys);

    bool AllKeysContainedIn(IReadableSet<string> keys);
}

/// <summary>Holds context keys in a tree of contexts and evaluates expressions against them.</summary>
internal interface IContextKeyService
{
    /// <summary>A key changed in this service's context or in a parent's (upstream's onDidChangeContext).</summary>
    event Action<IContextKeyChangeEvent>? DidChangeContext;

    /// <summary>Runs <paramref name="callback"/> and delivers the change events it causes as one event afterwards.</summary>
    void BufferChangeEvents(Action callback);

    /// <summary>Creates a key in this service's context, set to <paramref name="defaultValue"/> (removed when it is null).</summary>
    IContextKey<T> CreateKey<T>(string key, T? defaultValue);

    /// <summary>Whether <paramref name="rules"/> hold in this service's context; true for null.</summary>
    bool ContextMatchesRules(ContextKeyExpression? rules);

    object? GetContextKeyValue(string key);

    /// <summary>A child service whose context inherits this one's values.</summary>
    IScopedContextKeyService CreateScoped();

    /// <summary>A view of this service with some keys overridden.</summary>
    IContextKeyService CreateOverlay(IEnumerable<KeyValuePair<string, object?>> overlay);

    /// <summary>This service's context (upstream: <c>getContext(target)</c> for the service's own element).</summary>
    IContext GetContext();

    /// <summary>Moves this (scoped) service under <paramref name="parentContextKeyService"/>.</summary>
    void UpdateParent(IContextKeyService parentContextKeyService);
}

/// <summary>A scoped context key service; disposing it removes its context.</summary>
internal interface IScopedContextKeyService : IContextKeyService, IDisposable
{
}

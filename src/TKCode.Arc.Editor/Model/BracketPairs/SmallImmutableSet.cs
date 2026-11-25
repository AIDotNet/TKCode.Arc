// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/smallImmutableSet.ts,
// commit b57d5c9e), MIT license.
//
// Differences: the cache of the sets of up to 7 dense keys is static as upstream's, but it holds immutable values only
// and is filled with Interlocked.CompareExchange (a memo cache, the exception AGENTS.md allows); the key providers are
// interfaces and classes.

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Gives each value a small, dense integer key.</summary>
internal interface IDenseKeyProvider<in T>
{
    int GetKey(T value);
}

/// <summary>Keys that are already dense integers.</summary>
internal sealed class IdentityKeyProvider : IDenseKeyProvider<int>
{
    public static readonly IdentityKeyProvider Instance = new();

    public int GetKey(int value) => value;
}

/// <summary>Assigns keys in order of first use (one provider per bracket configuration, not shared between models).</summary>
internal sealed class DenseKeyProvider<T> : IDenseKeyProvider<T>
    where T : notnull
{
    private readonly Dictionary<T, int> _items = new();

    public int GetKey(T value)
    {
        if (!_items.TryGetValue(value, out var existing))
        {
            existing = _items.Count;
            _items[value] = existing;
        }

        return existing;
    }

    public T? ReverseLookup(int value)
    {
        foreach (var (key, v) in _items)
        {
            if (v == value)
            {
                return key;
            }
        }

        return default;
    }

    public List<T> ReverseLookupSet(SmallImmutableSet<T> set)
    {
        var result = new List<T>();
        foreach (var key in _items.Keys)
        {
            if (set.Has(key, this))
            {
                result.Add(key);
            }
        }

        return result;
    }

    public IEnumerable<T> Keys() => _items.Keys;
}

/// <summary>An immutable set of values with dense keys: a bit set, small and fast for up to 32 keys.</summary>
internal sealed class SmallImmutableSet<T>
{
    private static readonly int[] s_emptyArr = [];
    private static readonly SmallImmutableSet<T>?[] s_cache = new SmallImmutableSet<T>?[129];
    private static readonly SmallImmutableSet<T> s_empty = Create(0, s_emptyArr);

    private readonly int _items;
    private readonly int[] _additionalItems;

    private SmallImmutableSet(int items, int[] additionalItems)
    {
        _items = items;
        _additionalItems = additionalItems;
    }

    private static SmallImmutableSet<T> Create(int items, int[] additionalItems)
    {
        if (items is >= 0 and <= 128 && additionalItems.Length == 0)
        {
            // We create a cache of 128=2^7 elements to cover all sets with up to 7 (dense) elements.
            var cached = Volatile.Read(ref s_cache[items]);
            if (cached is null)
            {
                cached = new SmallImmutableSet<T>(items, additionalItems);
                cached = Interlocked.CompareExchange(ref s_cache[items], cached, null) ?? cached;
            }

            return cached;
        }

        return new SmallImmutableSet<T>(items, additionalItems);
    }

    public static SmallImmutableSet<T> GetEmpty() => s_empty;

    public SmallImmutableSet<T> Add(T value, IDenseKeyProvider<T> keyProvider)
    {
        var key = keyProvider.GetKey(value);
        var idx = key >> 5; // divided by 32
        if (idx == 0)
        {
            // fast path
            var newItem = (1 << key) | _items;
            if (newItem == _items)
            {
                return this;
            }

            return Create(newItem, _additionalItems);
        }

        idx--;

        var newItems = new List<int>(_additionalItems);
        while (newItems.Count <= idx)
        {
            newItems.Add(0);
        }

        newItems[idx] |= 1 << (key & 31);

        return Create(_items, [.. newItems]);
    }

    public bool Has(T value, IDenseKeyProvider<T> keyProvider)
    {
        var key = keyProvider.GetKey(value);
        var idx = key >> 5; // divided by 32
        if (idx == 0)
        {
            // fast path
            return (_items & (1 << key)) != 0;
        }

        idx--;

        return ((idx < _additionalItems.Length ? _additionalItems[idx] : 0) & (1 << (key & 31))) != 0;
    }

    public SmallImmutableSet<T> Merge(SmallImmutableSet<T> other)
    {
        var merged = _items | other._items;

        if (ReferenceEquals(_additionalItems, s_emptyArr) && ReferenceEquals(other._additionalItems, s_emptyArr))
        {
            // fast path
            if (merged == _items)
            {
                return this;
            }

            if (merged == other._items)
            {
                return other;
            }

            return Create(merged, s_emptyArr);
        }

        // This can be optimized, but it's not a common case
        var newItems = new int[Math.Max(_additionalItems.Length, other._additionalItems.Length)];
        for (var i = 0; i < newItems.Length; i++)
        {
            var item1 = i < _additionalItems.Length ? _additionalItems[i] : 0;
            var item2 = i < other._additionalItems.Length ? other._additionalItems[i] : 0;
            newItems[i] = item1 | item2;
        }

        return Create(merged, newItems);
    }

    public bool Intersects(SmallImmutableSet<T> other)
    {
        if ((_items & other._items) != 0)
        {
            return true;
        }

        for (var i = 0; i < Math.Min(_additionalItems.Length, other._additionalItems.Length); i++)
        {
            if ((_additionalItems[i] & other._additionalItems[i]) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool Equals(SmallImmutableSet<T> other) =>
        _items == other._items && _additionalItems.AsSpan().SequenceEqual(other._additionalItems);
}

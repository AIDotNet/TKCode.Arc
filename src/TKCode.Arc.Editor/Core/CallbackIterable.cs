// Ported from vscode (src/vs/base/common/arrays.ts: CallbackIterable, commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor;

/// <summary>
/// A sequence that calls a callback for every item and stops when the callback returns false (cheaper than an
/// enumerator for recursive producers, such as a tree walk).
/// </summary>
internal sealed class CallbackIterable<T>(Action<Func<T, bool>> iterate)
{
    public static readonly CallbackIterable<T> Empty = new(static _ => { });

    /// <summary>Calls the callback for every item. Stops when the callback returns false.</summary>
    public void Iterate(Func<T, bool> callback) => iterate(callback);

    public void ForEach(Action<T> handler) => iterate(item =>
    {
        handler(item);
        return true;
    });

    public List<T> ToArray()
    {
        var result = new List<T>();
        iterate(item =>
        {
            result.Add(item);
            return true;
        });
        return result;
    }

    public CallbackIterable<T> Filter(Func<T, bool> predicate) =>
        new(cb => iterate(item => !predicate(item) || cb(item)));

    public CallbackIterable<TResult> Map<TResult>(Func<T, TResult> mapFn) =>
        new(cb => iterate(item => cb(mapFn(item))));

    public bool Some(Func<T, bool> predicate)
    {
        var result = false;
        iterate(item =>
        {
            result = predicate(item);
            return !result;
        });
        return result;
    }

    public T? FindFirst(Func<T, bool> predicate)
    {
        T? result = default;
        iterate(item =>
        {
            if (predicate(item))
            {
                result = item;
                return false;
            }

            return true;
        });
        return result;
    }

    public T? FindLast(Func<T, bool> predicate)
    {
        T? result = default;
        iterate(item =>
        {
            if (predicate(item))
            {
                result = item;
            }

            return true;
        });
        return result;
    }

    public T? FindLastMaxBy(Comparison<T> comparator)
    {
        T? result = default;
        var first = true;
        iterate(item =>
        {
            if (first || comparator(item, result!) > 0)
            {
                first = false;
                result = item;
            }

            return true;
        });
        return result;
    }
}

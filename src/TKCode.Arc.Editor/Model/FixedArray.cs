// Ported from vscode (src/vs/editor/common/model/fixedArray.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the store is a List; replace, delete and insert splice it in place instead of building a
// new array.

namespace TKCode.Arc.Editor.Model;

/// <summary>An array that avoids being sparse by always filling up unused indices with a default value.</summary>
internal sealed class FixedArray<T>(T defaultValue)
{
    private readonly List<T> _store = new();

    public T Get(int index)
    {
        if (index < _store.Count)
        {
            return _store[index];
        }

        return defaultValue;
    }

    public void Set(int index, T value)
    {
        while (index >= _store.Count)
        {
            _store.Add(defaultValue);
        }

        _store[index] = value;
    }

    public void Replace(int index, int oldLength, int newLength)
    {
        if (index >= _store.Count)
        {
            return;
        }

        if (oldLength == 0)
        {
            Insert(index, newLength);
            return;
        }
        else if (newLength == 0)
        {
            Delete(index, oldLength);
            return;
        }

        // Upstream's slice: the part removed stops at the end of the store.
        _store.RemoveRange(index, Math.Min(oldLength, _store.Count - index));
        _store.InsertRange(index, Enumerable.Repeat(defaultValue, newLength));
    }

    public void Delete(int deleteIndex, int deleteCount)
    {
        if (deleteCount == 0 || deleteIndex >= _store.Count)
        {
            return;
        }

        _store.RemoveRange(deleteIndex, Math.Min(deleteCount, _store.Count - deleteIndex));
    }

    public void Insert(int insertIndex, int insertCount)
    {
        if (insertCount == 0 || insertIndex >= _store.Count)
        {
            return;
        }

        _store.InsertRange(insertIndex, Enumerable.Repeat(defaultValue, insertCount));
    }
}

using System.Collections;

namespace TKCode.Arc;

/// <summary>The numbers 0 to count - 1 as a list that takes no memory: a thumbnail list of ten thousand pages pays only for what it shows.</summary>
internal sealed class PdfPageIndexList : IList
{
    public PdfPageIndexList(int count)
    {
        Count = count;
    }

    public int Count { get; }

    public bool IsFixedSize => true;

    public bool IsReadOnly => true;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public object? this[int index]
    {
        get => index >= 0 && index < Count ? index : throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new NotSupportedException();
    }

    public int Add(object? value) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(object? value) => IndexOf(value) >= 0;

    public int IndexOf(object? value) => value is int index && index >= 0 && index < Count ? index : -1;

    public void Insert(int index, object? value) => throw new NotSupportedException();

    public void Remove(object? value) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();

    public void CopyTo(Array array, int index)
    {
        for (var i = 0; i < Count; i++)
        {
            array.SetValue(i, index + i);
        }
    }

    public IEnumerator GetEnumerator() => Enumerable.Range(0, Count).GetEnumerator();
}

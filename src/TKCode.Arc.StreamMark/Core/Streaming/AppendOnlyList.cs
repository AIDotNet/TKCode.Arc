namespace StreamMark.Core.Streaming;

/// <summary>
/// 只追加的分块列表：单写多读安全。已写入的元素和分块数组永不移动，
/// 快照只需记住 (列表, 数量) 就能在之后的追加中继续安全读取前缀。
/// </summary>
internal sealed class AppendOnlyList<T>
{
    private const int ChunkShift = 8;
    private const int ChunkSize = 1 << ChunkShift;

    private T[][] _chunks = [new T[ChunkSize]];
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return Volatile.Read(ref _chunks)[index >> ChunkShift][index & (ChunkSize - 1)];
        }
    }

    public void Add(T item)
    {
        var chunk = _count >> ChunkShift;
        if (chunk == _chunks.Length)
        {
            var grown = new T[_chunks.Length * 2][];
            Array.Copy(_chunks, grown, _chunks.Length);
            Volatile.Write(ref _chunks, grown);
        }
        _chunks[chunk] ??= new T[ChunkSize];
        _chunks[chunk][_count & (ChunkSize - 1)] = item;
        Volatile.Write(ref _count, _count + 1);
    }
}

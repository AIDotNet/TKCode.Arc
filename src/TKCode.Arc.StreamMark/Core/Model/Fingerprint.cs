namespace StreamMark.Core.Model;

/// <summary>
/// 64 位 FNV-1a 内容指纹。块模型的结构化比较只用于判断"渲染结果是否需要更新"，
/// 用指纹代替逐字段深比较，避免在不可变数组上反复遍历。
/// </summary>
internal struct Fingerprint
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    private ulong _hash;

    public static Fingerprint Create() => new() { _hash = Offset };

    public readonly ulong Value => _hash;

    public void Add(int value)
    {
        unchecked
        {
            for (var i = 0; i < 4; i++)
            {
                _hash ^= (byte)(value >> (i * 8));
                _hash *= Prime;
            }
        }
    }

    public void Add(string? value)
    {
        if (value is null)
        {
            Add(-1);
            return;
        }
        Add(value.Length);
        unchecked
        {
            foreach (var c in value)
            {
                _hash ^= c;
                _hash *= Prime;
            }
        }
    }
}

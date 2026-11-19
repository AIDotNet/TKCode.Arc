using System.Collections.Immutable;
using StreamMark.Core.Model;

namespace StreamMark.Core.Streaming;

/// <summary>
/// 某一时刻的文档快照：已冻结的块（之后的快照中不再变化）+ 尾部块。
/// 快照不可变，可以跨线程传递；同一 <see cref="Generation"/> 内，后一快照的前 <see cref="FrozenCount"/> 个块
/// 与前一快照完全相同（同一对象）。
/// </summary>
public sealed class DocumentSnapshot
{
    private readonly AppendOnlyList<MarkdownBlock> _frozen;
    private readonly ImmutableArray<MarkdownBlock> _tail;

    internal DocumentSnapshot(AppendOnlyList<MarkdownBlock> frozen, int frozenCount, ImmutableArray<MarkdownBlock> tail,
        long version, int generation, int sourceLength, int frozenSourceEnd, bool isComplete)
    {
        _frozen = frozen;
        _tail = tail;
        FrozenCount = frozenCount;
        Version = version;
        Generation = generation;
        SourceLength = sourceLength;
        FrozenSourceEnd = frozenSourceEnd;
        IsComplete = isComplete;
    }

    public static DocumentSnapshot Empty { get; } = new(new AppendOnlyList<MarkdownBlock>(), 0, [], 0, 0, 0, 0, false);

    /// <summary>对应的源码版本号（每次追加递增）。</summary>
    public long Version { get; }

    /// <summary>重置或流结束后的整篇重解析会开启新的一代；跨代比较时不能假设冻结前缀相同。</summary>
    public int Generation { get; }

    /// <summary>生成快照时的源码长度。</summary>
    public int SourceLength { get; }

    /// <summary>已冻结的源码边界。</summary>
    public int FrozenSourceEnd { get; }

    /// <summary>流已结束，这是最终结果。</summary>
    public bool IsComplete { get; }

    public int FrozenCount { get; }

    public int Count => FrozenCount + _tail.Length;

    public MarkdownBlock this[int index] => index < FrozenCount ? _frozen[index] : _tail[index - FrozenCount];

    /// <summary>尾部（仍可能变化的）块。</summary>
    public ImmutableArray<MarkdownBlock> Tail => _tail;

    internal bool SharesFrozenWith(DocumentSnapshot other) => ReferenceEquals(_frozen, other._frozen) && Generation == other.Generation;

    public IEnumerable<MarkdownBlock> Blocks
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }
    }
}

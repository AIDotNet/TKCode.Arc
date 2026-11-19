using StreamMark.Core.Model;

namespace StreamMark.Core.Streaming;

/// <summary>旧快照里的一个顶层块在新快照里的去向。</summary>
public enum BlockFate : byte
{
    /// <summary>内容与源码位置都没变。</summary>
    Unchanged,

    /// <summary>内容没变，只是前面的文字增减让它整体平移（块内部的所有源码偏移按这个块自己的位移量平移）。</summary>
    Shifted,

    /// <summary>内容变了：新块是在同一位置上与它配对的替换者。</summary>
    Replaced,

    /// <summary>被删除（没有配对的新块）。</summary>
    Deleted,
}

/// <summary>
/// 一个旧块的去向。<see cref="New"/>：除 <see cref="BlockFate.Deleted"/> 外是对应的新块；
/// 被删除时是紧跟在它后面的新块，文档末尾被删除时为 null。
/// </summary>
public readonly record struct BlockMove(MarkdownBlock Old, MarkdownBlock? New, BlockFate Fate);

/// <summary>
/// 编辑前后两份快照的对齐：<see cref="Compute"/> 先取公共前缀（位置与内容都相同）与公共后缀（内容相同、整体平移同样的距离），
/// 中间部分再按内容做最长公共子序列配对（编辑不止一处时，两处编辑之间还有没变的块），配对之间的空隙按位置替换，其余视为删除 / 插入。快照之间不共享冻结前缀（例如宿主整篇替换了文本），
/// <see cref="SnapshotDiff"/> 此时只能按源码起点归并，在前面增删文字就会让后面所有块被当成删除再插入；
/// 对齐之后这些块是 <see cref="BlockFate.Shifted"/>，视图可以原地更新，状态和选区也能跟着搬。
/// </summary>
/// <remarks>
/// <see cref="Ops"/> 与 <see cref="SnapshotDiff.Compute"/> 的语义相同（按顺序应用，<see cref="DiffOp.Index"/> 是正在变换的列表中的位置），
/// 平移的块以 <see cref="DiffKind.Update"/> 表示，所以不需要新的操作类型。
/// </remarks>
public sealed class SnapshotEdit
{
    private readonly BlockMove[] _moves;

    private SnapshotEdit(DocumentSnapshot next, List<DiffOp> ops, BlockMove[] moves)
    {
        Next = next;
        Ops = ops;
        _moves = moves;
    }

    /// <summary>编辑之后的快照。</summary>
    public DocumentSnapshot Next { get; }

    /// <summary>把旧块列表变成新块列表的操作。</summary>
    public IReadOnlyList<DiffOp> Ops { get; }

    /// <summary>每个旧顶层块的去向，顺序与旧快照相同。</summary>
    public IReadOnlyList<BlockMove> Moves => _moves;

    public static SnapshotEdit Compute(DocumentSnapshot? previous, DocumentSnapshot next)
    {
        previous ??= DocumentSnapshot.Empty;
        var old = previous.Blocks.ToArray();
        var cur = next.Blocks.ToArray();
        int n = old.Length, m = cur.Length;

        var prefix = 0;
        while (prefix < n && prefix < m && old[prefix].Range == cur[prefix].Range && SameContent(old[prefix], cur[prefix]))
        {
            prefix++;
        }

        // 后缀：内容相同、长度相同，且源码起点平移的距离始终一样（编辑只发生在它们前面）
        var suffix = 0;
        var delta = 0;
        while (suffix < n - prefix && suffix < m - prefix)
        {
            var o = old[n - 1 - suffix];
            var c = cur[m - 1 - suffix];
            var shift = c.Range.Start - o.Range.Start;
            if (o.Range.Length != c.Range.Length || suffix > 0 && shift != delta || !SameContent(o, c))
            {
                break;
            }
            delta = shift;
            suffix++;
        }

        int oldMid = n - prefix - suffix, newMid = m - prefix - suffix;
        var ops = new List<DiffOp>();
        var moves = new BlockMove[n];

        for (var i = 0; i < prefix; i++)
        {
            moves[i] = new BlockMove(old[i], cur[i], BlockFate.Unchanged);
        }

        // 中间：先按内容配对（编辑不止一处时，前缀 / 后缀之间还有没变的块），配对之间的空隙再按位置替换
        var position = prefix;
        int oldNext = prefix, newNext = prefix;
        foreach (var (matchOld, matchNew) in Match(old, cur, prefix, oldMid, newMid))
        {
            Gap(old, cur, oldNext, matchOld, newNext, matchNew, ref position, ops, moves);
            var o = old[matchOld];
            var c = cur[matchNew];
            var moved = o.Range != c.Range;
            moves[matchOld] = new BlockMove(o, c, moved ? BlockFate.Shifted : BlockFate.Unchanged);
            if (moved)
            {
                ops.Add(new DiffOp(DiffKind.Update, position, c));
            }
            position++;
            oldNext = matchOld + 1;
            newNext = matchNew + 1;
        }
        Gap(old, cur, oldNext, prefix + oldMid, newNext, prefix + newMid, ref position, ops, moves);

        for (var t = 0; t < suffix; t++)
        {
            var o = old[n - suffix + t];
            var c = cur[m - suffix + t];
            moves[n - suffix + t] = new BlockMove(o, c, delta == 0 ? BlockFate.Unchanged : BlockFate.Shifted);
            if (delta != 0)
            {
                // 视图原地更新到新的源码位置，而不是删掉重建
                ops.Add(new DiffOp(DiffKind.Update, position, c));
            }
            position++;
        }
        return new SnapshotEdit(next, ops, moves);
    }

    // 两处配对之间的空隙：旧块 [oldFrom, oldTo) 与新块 [newFrom, newTo) 按位置替换，多出的旧块删除、多出的新块插入
    private static void Gap(MarkdownBlock[] old, MarkdownBlock[] cur, int oldFrom, int oldTo, int newFrom, int newTo,
        ref int position, List<DiffOp> ops, BlockMove[] moves)
    {
        int oldCount = oldTo - oldFrom, newCount = newTo - newFrom;
        var paired = Math.Min(oldCount, newCount);
        for (var k = 0; k < paired; k++)
        {
            var o = old[oldFrom + k];
            var c = cur[newFrom + k];
            var same = o.Range == c.Range && SameContent(o, c);
            moves[oldFrom + k] = new BlockMove(o, c, same ? BlockFate.Unchanged : BlockFate.Replaced);
            if (!same)
            {
                ops.Add(new DiffOp(DiffKind.Update, position + k, c));
            }
        }
        // 被删除的块：记下新列表里紧跟在这段空隙后面的块，选区之类的位置可以退到那里
        var after = newTo < cur.Length ? cur[newTo] : null;
        for (var k = paired; k < oldCount; k++)
        {
            moves[oldFrom + k] = new BlockMove(old[oldFrom + k], after, BlockFate.Deleted);
            ops.Add(new DiffOp(DiffKind.Remove, position + paired, null));
        }
        for (var k = paired; k < newCount; k++)
        {
            ops.Add(new DiffOp(DiffKind.Insert, position + k, cur[newFrom + k]));
        }
        position += newCount;
    }

    // 配对的上限：中间区域的块数乘积超过它就不做内容配对（整篇都改了，位置替换已经够用），避免二次方的开销
    private const int MaxMatchCells = 1_000_000;

    // 最长公共子序列：内容（类型 + 指纹 + 源码长度）相同的块配对，返回升序的 (旧下标, 新下标)
    private static List<(int Old, int New)> Match(MarkdownBlock[] old, MarkdownBlock[] cur, int start, int oldCount, int newCount)
    {
        var matches = new List<(int Old, int New)>();
        if (oldCount == 0 || newCount == 0 || (long)oldCount * newCount > MaxMatchCells)
        {
            return matches;
        }
        var oldPrint = new ulong[oldCount];
        var newPrint = new ulong[newCount];
        for (var i = 0; i < oldCount; i++)
        {
            oldPrint[i] = old[start + i].ComputeFingerprint();
        }
        for (var j = 0; j < newCount; j++)
        {
            newPrint[j] = cur[start + j].ComputeFingerprint();
        }
        bool Equal(int i, int j) =>
            oldPrint[i] == newPrint[j] && old[start + i].GetType() == cur[start + j].GetType() && old[start + i].Range.Length == cur[start + j].Range.Length;

        var width = newCount + 1;
        var table = new int[(oldCount + 1) * width];
        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                table[i * width + j] = Equal(i, j) ? table[(i + 1) * width + j + 1] + 1 : Math.Max(table[(i + 1) * width + j], table[i * width + j + 1]);
            }
        }
        int a = 0, b = 0;
        while (a < oldCount && b < newCount)
        {
            if (Equal(a, b))
            {
                matches.Add((start + a, start + b));
                a++;
                b++;
            }
            else if (table[(a + 1) * width + b] >= table[a * width + b + 1])
            {
                a++;
            }
            else
            {
                b++;
            }
        }
        return matches;
    }

    /// <summary>旧顶层块（按起点）的去向；没有这个块时返回 null。</summary>
    public BlockMove? FindMove(int oldBlockKey)
    {
        var index = IndexOfLastStartingAtOrBefore(oldBlockKey);
        return index >= 0 && _moves[index].Old.Key == oldBlockKey ? _moves[index] : null;
    }

    /// <summary>
    /// 旧源码偏移 → 新源码偏移。块没变或整体平移时，块内任意偏移都能映射；
    /// 内容变了的块只有它的起点能映射（内部的偏移已经不可信）；被删除的块、块之间的空隙以及不认识的偏移返回 null。
    /// </summary>
    public int? MapKey(int oldOffset)
    {
        var index = IndexOfLastStartingAtOrBefore(oldOffset);
        if (index < 0)
        {
            return null;
        }
        var move = _moves[index];
        if (move.Fate == BlockFate.Deleted || move.New is null)
        {
            return null;
        }
        if (oldOffset == move.Old.Key)
        {
            return move.New.Key;
        }
        return move.Fate is BlockFate.Unchanged or BlockFate.Shifted && oldOffset < move.Old.Range.End
            ? oldOffset + (move.New.Key - move.Old.Key)
            : null;
    }

    private int IndexOfLastStartingAtOrBefore(int offset)
    {
        int low = 0, high = _moves.Length - 1, found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (_moves[mid].Old.Key <= offset)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return found;
    }

    private static bool SameContent(MarkdownBlock a, MarkdownBlock b) =>
        ReferenceEquals(a, b) || a.GetType() == b.GetType() && a.ComputeFingerprint() == b.ComputeFingerprint();
}

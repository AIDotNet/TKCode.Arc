using StreamMark.Core.Model;

namespace StreamMark.Core.Streaming;

public enum DiffKind : byte
{
    Insert,
    Update,
    Remove,
}

/// <summary>
/// 一个差异操作。按顺序依次应用到旧块列表上即可得到新列表：
/// <see cref="Index"/> 是应用该操作时在"正在变换的列表"中的位置。
/// </summary>
public readonly record struct DiffOp(DiffKind Kind, int Index, MarkdownBlock? Block);

public static class SnapshotDiff
{
    /// <summary>
    /// 计算从 <paramref name="previous"/> 到 <paramref name="next"/> 的差异。
    /// 同一代内跳过共享的冻结前缀，只比较其后的块；按 key（源码起点）归并，指纹不同的视为更新。
    /// </summary>
    public static List<DiffOp> Compute(DocumentSnapshot? previous, DocumentSnapshot next)
    {
        var ops = new List<DiffOp>();
        previous ??= DocumentSnapshot.Empty;
        var start = previous.SharesFrozenWith(next) ? Math.Min(previous.FrozenCount, next.FrozenCount) : 0;

        int i = start, j = start, k = start;
        while (i < previous.Count && j < next.Count)
        {
            var oldBlock = previous[i];
            var newBlock = next[j];
            if (oldBlock.Key == newBlock.Key)
            {
                if (!ReferenceEquals(oldBlock, newBlock) &&
                    (oldBlock.GetType() != newBlock.GetType() || oldBlock.Range != newBlock.Range || oldBlock.ComputeFingerprint() != newBlock.ComputeFingerprint()))
                {
                    ops.Add(new DiffOp(DiffKind.Update, k, newBlock));
                }
                i++;
                j++;
                k++;
            }
            else if (oldBlock.Key < newBlock.Key)
            {
                ops.Add(new DiffOp(DiffKind.Remove, k, null));
                i++;
            }
            else
            {
                ops.Add(new DiffOp(DiffKind.Insert, k, newBlock));
                j++;
                k++;
            }
        }
        for (; i < previous.Count; i++)
        {
            ops.Add(new DiffOp(DiffKind.Remove, k, null));
        }
        for (; j < next.Count; j++)
        {
            ops.Add(new DiffOp(DiffKind.Insert, k++, next[j]));
        }
        return ops;
    }
}

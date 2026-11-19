using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Selection;

/// <summary>
/// 文档被编辑后把选区坐标搬到新快照。规则：
/// 平移的块（前面增删了文字）精确平移；
/// 被替换的块映射到配对的新块，片段序号与偏移钳到新文本的范围内；
/// 被删除的块退到后面第一个存活块的开头，没有时退到文档末尾。
/// </summary>
internal static class SelectionRemap
{
    public static TextPoint Map(SnapshotEdit edit, TextPoint point, Plugins.PluginRegistry? plugins = null)
    {
        if (edit.FindMove(point.BlockKey) is not { } move)
        {
            return point;
        }
        switch (move.Fate)
        {
            case BlockFate.Unchanged:
            case BlockFate.Shifted:
                return point with { BlockKey = move.New!.Key };
            case BlockFate.Replaced:
                return Clamp(point with { BlockKey = move.New!.Key }, move.New, plugins);
            default:
                return move.New is { } next ? new TextPoint(point.Entry, next.Key, 0, 0) : EndOfDocument(edit, point.Entry, plugins);
        }
    }

    private static TextPoint Clamp(TextPoint point, Core.Model.MarkdownBlock block, Plugins.PluginRegistry? plugins)
    {
        var segments = BlockText.Segments(block, plugins);
        if (segments.Count == 0)
        {
            return point with { Segment = 0, Offset = 0 };
        }
        if (point.Segment >= segments.Count)
        {
            return point with { Segment = segments.Count - 1, Offset = segments[^1].Length };
        }
        return point.Segment < 0 ? point : point with { Offset = Math.Clamp(point.Offset, 0, segments[point.Segment].Length) };
    }

    private static TextPoint EndOfDocument(SnapshotEdit edit, int entry, Plugins.PluginRegistry? plugins)
    {
        if (edit.Next.Count == 0)
        {
            return new TextPoint(entry, 0, 0, 0);
        }
        var last = edit.Next[^1];
        var segments = BlockText.Segments(last, plugins);
        return segments.Count == 0 ? new TextPoint(entry, last.Key, 0, 0) : new TextPoint(entry, last.Key, segments.Count - 1, segments[^1].Length);
    }
}

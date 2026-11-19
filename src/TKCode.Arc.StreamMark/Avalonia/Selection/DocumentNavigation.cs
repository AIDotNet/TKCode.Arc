using StreamMark.Core.Model;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Selection;

/// <summary>目录中的一项：标题所在位置（可直接交给 <c>ScrollToPoint</c>）、级别、文字与锚点 id。</summary>
public sealed record OutlineItem(TextPoint Target, int Level, string Text, string? Id);

/// <summary>基于块模型的导航：目录、文内锚点（<c>#标题id</c>、脚注 <c>#fn:标签</c>）解析为选区坐标，以及源码偏移与渲染位置的互相换算。</summary>
public static class DocumentNavigation
{
    /// <summary>文档中的标题（含 HTML 盒子、引用、列表等容器内的标题），按阅读顺序。</summary>
    public static List<OutlineItem> Outline(DocumentSnapshot snapshot, int entry, Plugins.PluginRegistry? plugins = null)
    {
        var items = new List<OutlineItem>();
        foreach (var top in snapshot.Blocks)
        {
            foreach (var heading in Descendants(top).OfType<HeadingBlock>())
            {
                if (SegmentOf(top, heading, plugins) is { } segment)
                {
                    items.Add(new OutlineItem(new TextPoint(entry, top.Key, segment, 0), heading.Level, BlockText.PlainText(heading.Content), heading.Id));
                }
            }
        }
        return items;
    }

    /// <summary>
    /// 把文内链接解析为目标位置：<c>#fn:标签</c> 指向脚注定义，<c>#id</c> 指向 id 相同（或文字转成的锚点相同）的标题。
    /// 找不到时返回 null（交给宿主处理）。
    /// </summary>
    public static TextPoint? ResolveAnchor(DocumentSnapshot snapshot, int entry, string url, Plugins.PluginRegistry? plugins = null)
    {
        if (!url.StartsWith('#') || url.Length < 2)
        {
            return null;
        }
        var anchor = Uri.UnescapeDataString(url[1..]);
        if (anchor.StartsWith("fn:", StringComparison.Ordinal))
        {
            var label = anchor[3..];
            foreach (var top in snapshot.Blocks)
            {
                if (top is FootnotesBlock footnotes)
                {
                    foreach (var footnote in footnotes.Footnotes)
                    {
                        // Markdig 的脚注标签带 "^"（链接为 #fn:^note），也接受不带的写法
                        if ((footnote.Label == label || footnote.Label == "^" + label || "^" + footnote.Label == label) && footnote.Children.Length > 0 && SegmentOf(top, footnote.Children[0], plugins) is { } segment)
                        {
                            return new TextPoint(entry, top.Key, segment, 0);
                        }
                    }
                }
            }
            return null;
        }
        var outline = Outline(snapshot, entry, plugins);
        var match = outline.FirstOrDefault(o => string.Equals(o.Id, anchor, StringComparison.OrdinalIgnoreCase))
                    ?? outline.FirstOrDefault(o => string.Equals(Slug(o.Text), Slug(anchor), StringComparison.OrdinalIgnoreCase));
        return match?.Target;
    }

    /// <summary>
    /// 源码偏移（UTF-16 下标，见 <see cref="SourceText"/>）→ 渲染位置，块级精度：返回包含该偏移的最深的块，
    /// 列表项、引用、提示块等容器里的段落也能单独命中；偏移落在列表项的项目符号上时指向该项的第一个子块。
    /// 偏移在块与块之间（空行）或末尾之后时返回它之后最近的块（<see cref="SourcePrecision.Nearest"/>）。
    /// 快照没有块，或偏移不在 [0, <see cref="DocumentSnapshot.SourceLength"/>] 内时返回 null。
    /// </summary>
    public static SourceTarget? ResolveSourceOffset(DocumentSnapshot snapshot, int entry, int offset, Plugins.PluginRegistry? plugins = null)
    {
        if (snapshot.Count == 0 || offset < 0 || offset > snapshot.SourceLength)
        {
            return null;
        }
        // 最后一个起点不超过偏移的顶层块（块按起点升序排列）
        int low = 0, high = snapshot.Count - 1, found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (snapshot[mid].Range.Start <= offset)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        if (found >= 0 && Contains(snapshot[found].Range, offset))
        {
            var top = snapshot[found];
            var (block, range) = Locate(top, offset);
            var segment = SegmentOf(top, block, plugins) ?? 0;
            return new SourceTarget(new TextPoint(entry, top.Key, segment, 0), range, SourcePrecision.Block);
        }
        var nearest = snapshot[Math.Min(found + 1, snapshot.Count - 1)];
        return new SourceTarget(new TextPoint(entry, nearest.Key, 0, 0), nearest.Range, SourcePrecision.Nearest);
    }

    /// <summary>
    /// 渲染位置 → 源码区间，块级精度：返回承载该文本片段的最深的块的完整区间，不细分到行或字符
    /// （<see cref="TextPoint.Offset"/> 是片段纯文本里的偏移，不能直接当源码偏移）。找不到对应的顶层块时返回 null。
    /// </summary>
    public static SourceRange? SourceRangeOf(DocumentSnapshot snapshot, TextPoint point, Plugins.PluginRegistry? plugins = null)
    {
        MarkdownBlock? top = null;
        for (var i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i].Key == point.BlockKey)
            {
                top = snapshot[i];
                break;
            }
        }
        if (top is null)
        {
            return null;
        }
        var result = top.Range;
        if (point.Segment >= 0)
        {
            // 先序遍历里越靠后的命中越深（兄弟块的片段区间互不重叠）
            foreach (var block in Descendants(top))
            {
                if (SegmentOf(top, block, plugins) is { } first && point.Segment >= first && point.Segment < first + BlockText.Segments(block, plugins).Count && block.Range.Length > 0)
                {
                    result = block.Range;
                }
            }
        }
        return result;
    }

    private static bool Contains(SourceRange range, int offset) => range.Length > 0 && offset >= range.Start && offset < range.End;

    // 偏移所在的最深的块及其区间。列表多一层列表项：偏移在项目符号上（不在任何子块里）时用项的第一个子块代表，区间取整个列表项
    private static (MarkdownBlock Block, SourceRange Range) Locate(MarkdownBlock top, int offset)
    {
        var current = top;
        var range = top.Range;
        while (true)
        {
            if (current is ListBlock list)
            {
                var item = list.Items.FirstOrDefault(i => Contains(i.Range, offset));
                if (item is null || item.Children.Length == 0)
                {
                    return (current, range);
                }
                var inner = item.Children.FirstOrDefault(c => Contains(c.Range, offset));
                if (inner is null)
                {
                    return (item.Children[0], item.Range);
                }
                current = inner;
                range = inner.Range;
                continue;
            }
            IEnumerable<MarkdownBlock> children = current switch
            {
                HtmlBoxBlock box => box.Children,
                QuoteBlock quote => quote.Children,
                AlertBlock alert => alert.Children,
                ComponentBlock component => component.Children,
                CustomContainerBlock container => container.Children,
                _ => [],
            };
            var next = children.FirstOrDefault(c => Contains(c.Range, offset));
            if (next is null)
            {
                return (current, range);
            }
            current = next;
            range = next.Range;
        }
    }

    /// <summary>GitHub 风格的锚点：小写，空白变连字符，去掉标点（保留字母、数字、CJK、连字符与下划线）。</summary>
    public static string Slug(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append('-');
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<MarkdownBlock> Descendants(MarkdownBlock block)
    {
        yield return block;
        IEnumerable<MarkdownBlock> children = block switch
        {
            HtmlBoxBlock box => box.Children,
            QuoteBlock quote => quote.Children,
            AlertBlock alert => alert.Children,
            ComponentBlock component => component.Children,
            CustomContainerBlock container => container.Children,
            ListBlock list => list.Items.SelectMany(i => i.Children),
            _ => [],
        };
        foreach (var child in children)
        {
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    // 目标块的第一个片段在顶层块片段列表中的序号（按内容引用匹配，与视图分配片段编号的顺序一致）
    private static int? SegmentOf(MarkdownBlock top, MarkdownBlock target, Plugins.PluginRegistry? plugins)
    {
        var targetSegments = BlockText.Segments(target, plugins);
        if (targetSegments.Count == 0)
        {
            return null;
        }
        var first = targetSegments[0];
        var segments = BlockText.Segments(top, plugins);
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (first.Content is not null ? ReferenceEquals(segment.Content, first.Content) : ReferenceEquals(segment.Raw, first.Raw))
            {
                return i;
            }
        }
        return null;
    }
}

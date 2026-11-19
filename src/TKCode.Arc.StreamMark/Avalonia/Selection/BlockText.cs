using System.Text;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Selection;

/// <summary>
/// 从块模型提取文本片段，用于复制。遍历顺序必须与块视图分配片段编号的顺序一致：
/// 段落 / 标题 / 代码 / 公式 / HTML 各 1 个；表格按行优先的单元格；列表、引用、提示块、容器、脚注按子块；
/// 定义列表每项先术语后释义。
/// </summary>
public static class BlockText
{
    /// <summary>
    /// 一个片段：行内内容（或纯文本）、前面的分隔符、整段被选中时加在前面的前缀（如列表标记）。
    /// 偏移与视图中片段文本的偏移一致。
    /// </summary>
    public readonly record struct Segment(InlineContent? Content, string? Raw, string Separator, string Prefix = "")
    {
        public int Length => Content is not null ? Content.Text.Length : Raw?.Length ?? 0;

        public string Slice(int start, int end, Func<InlineObject, string?>? components = null)
        {
            if (Content is not null)
            {
                return PlainText(Content, start, end, components);
            }
            var raw = Raw ?? "";
            return raw[Math.Clamp(start, 0, raw.Length)..Math.Clamp(end, 0, raw.Length)];
        }
    }

    public static List<Segment> Segments(MarkdownBlock block) => Segments(block, null);

    /// <summary>
    /// 带插件注册表的版本：组件登记了复制文本（<see cref="Plugins.MarkdownPluginBuilder.AddBlockComponent(string, Func{ComponentBlock, Rendering.BlockView}, Plugins.ComponentTextCollector, bool)"/>）
    /// 时按它收集。视图、选区、搜索与导航都要用同一个注册表调用，否则片段编号对不上。
    /// </summary>
    public static List<Segment> Segments(MarkdownBlock block, Plugins.PluginRegistry? plugins)
    {
        var list = new List<Segment>();
        Collect(block, list, "", plugins);
        return list;
    }

    internal static void CollectInto(MarkdownBlock block, List<Segment> list, Plugins.PluginRegistry? plugins) => Collect(block, list, "", plugins);

    private static void Collect(MarkdownBlock block, List<Segment> list, string prefix, Plugins.PluginRegistry? plugins)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                list.Add(new Segment(paragraph.Content, null, "\n", prefix));
                break;
            case HeadingBlock heading:
                list.Add(new Segment(heading.Content, null, "\n"));
                break;
            case CodeBlock code:
                list.Add(new Segment(null, code.Code, "\n", prefix));
                break;
            case MathBlock math:
                list.Add(new Segment(null, math.Latex, "\n", prefix));
                break;
            case HtmlBlock html:
                list.Add(new Segment(null, html.Html, "\n", prefix));
                break;
            case TableBlock table:
                foreach (var row in table.Rows)
                {
                    var columns = Math.Max(row.Cells.Length, table.Columns.Length);
                    for (var c = 0; c < columns; c++)
                    {
                        list.Add(new Segment(c < row.Cells.Length ? row.Cells[c] : InlineContent.Empty, null, c == 0 ? "\n" : "\t"));
                    }
                }
                break;
            case AlertBlock alert:
                foreach (var child in alert.Children)
                {
                    Collect(child, list, "", plugins);
                }
                break;
            case HtmlBoxBlock box:
                foreach (var child in box.Children)
                {
                    Collect(child, list, prefix, plugins);
                }
                break;
            case QuoteBlock quote:
                foreach (var child in quote.Children)
                {
                    Collect(child, list, "", plugins);
                }
                break;
            case ComponentBlock component when plugins?.FindComponentText(component) is { } text:
                text(component, new Plugins.ComponentTextContext(list, plugins));
                break;
            case ComponentBlock component:
                // summary 在前（折叠块的标题），其余子块按顺序
                foreach (var child in component.Children.Where(c => c is ComponentBlock { Name: "summary" }))
                {
                    Collect(child, list, "", plugins);
                }
                foreach (var child in component.Children.Where(c => c is not ComponentBlock { Name: "summary" }))
                {
                    Collect(child, list, "", plugins);
                }
                break;
            case CustomContainerBlock container:
                foreach (var child in container.Children)
                {
                    Collect(child, list, "", plugins);
                }
                break;
            case ListBlock items:
                for (var i = 0; i < items.Items.Length; i++)
                {
                    var item = items.Items[i];
                    var marker = item.Task switch
                    {
                        true => "[x] ",
                        false => "[ ] ",
                        _ => items.IsOrdered ? $"{items.StartNumber + i}. " : "- ",
                    };
                    for (var c = 0; c < item.Children.Length; c++)
                    {
                        Collect(item.Children[c], list, c == 0 ? marker : "", plugins);
                    }
                }
                break;
            case FootnotesBlock footnotes:
                foreach (var footnote in footnotes.Footnotes)
                {
                    for (var c = 0; c < footnote.Children.Length; c++)
                    {
                        Collect(footnote.Children[c], list, c == 0 ? $"[{footnote.Order}] " : "", plugins);
                    }
                }
                break;
            case DefinitionListBlock definitions:
                foreach (var item in definitions.Items)
                {
                    foreach (var term in item.Terms)
                    {
                        list.Add(new Segment(term, null, "\n"));
                    }
                    foreach (var definition in item.Definitions)
                    {
                        Collect(definition, list, "", plugins);
                    }
                }
                break;
        }
    }

    /// <summary>行内内容的纯文本：公式替换为 <c>$latex$</c>，图片替换为替代文字。</summary>
    /// <remarks><paramref name="components"/> 提供行内组件的纯文本（通常来自插件的 <c>ToPlainText</c>）；返回 null 时使用内置规则。</remarks>
    public static string PlainText(InlineContent content, int start = 0, int end = int.MaxValue, Func<InlineObject, string?>? components = null)
    {
        end = Math.Min(end, content.Text.Length);
        if (content.Objects.IsEmpty)
        {
            return content.Text[start..end];
        }
        var sb = new StringBuilder(end - start);
        var objectIndex = 0;
        for (var i = start; i < end; i++)
        {
            var c = content.Text[i];
            if (c == InlineObject.Placeholder)
            {
                while (objectIndex < content.Objects.Length && content.Objects[objectIndex].Offset < i)
                {
                    objectIndex++;
                }
                if (objectIndex < content.Objects.Length && content.Objects[objectIndex].Offset == i)
                {
                    var obj = content.Objects[objectIndex];
                    sb.Append(obj.Kind switch
                    {
                        InlineObjectKind.Math => obj.Flag ? $"$${obj.Value}$$" : $"${obj.Value}$",
                        InlineObjectKind.Image => obj.Alt ?? "",
                        InlineObjectKind.TaskCheckbox => obj.Flag ? "[x]" : "[ ]",
                        InlineObjectKind.Component => components?.Invoke(obj) ?? (obj.Value == "citation" ? $"[{obj.Attributes["label"] ?? obj.Attributes["id"]}]" : ""),
                        _ => "",
                    });
                    continue;
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}

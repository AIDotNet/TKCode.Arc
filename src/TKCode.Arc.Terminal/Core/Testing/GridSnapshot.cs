using System.Globalization;
using System.Text;

namespace TKCode.Terminal.Core.Testing;

public sealed record GridSnapshotOptions
{
    /// <summary>额外写出视口之上的回滚行数（行号为负）。</summary>
    public int Scrollback { get; init; }

    /// <summary>写出 modes / title / cwd 行。</summary>
    public bool IncludeState { get; init; } = true;
}

/// <summary>
/// 网格快照的文本格式 <c>.grid</c>（11-testing-benchmarks.md §2.2）：纯文本、LF、UTF-8，可逐字节比较与 diff，
/// 只依赖 <see cref="FrameSnapshot"/>，与 libghostty 的内部布局无关。黄金样本、L3/L4 测试、自检共用；AOT 安全。
/// <para>
/// 与 §2.2 的差异：不写 <c>margins</c>（libghostty 没有导出滚动区域）、不写 <c>e:</c>（render state 不区分"从未写入"）；
/// 语义区间按行写游程（<c>sem:p0-1,i2-13</c>，输出区不写）。
/// </para>
/// </summary>
public static class GridSnapshot
{
    public const string Header = "tkcode-grid 1";

    /// <summary>宽字符第二列的占位符（不可见分隔符），使文本列与网格列对齐。</summary>
    public const char WideTail = '⁣';

    public static string Capture(TerminalCore core, GridSnapshotOptions? options = null)
    {
        options ??= new GridSnapshotOptions();
        var frame = core.CaptureFresh();
        var scrollback = Array.Empty<RowSnapshot>();
        if (options.Scrollback > 0 && frame.Scrollbar.Offset > 0)
        {
            var n = (int)Math.Min(options.Scrollback, frame.Scrollbar.Offset);
            core.ScrollViewport(ViewportScroll.ToRow(frame.Scrollbar.Offset - n));
            scrollback = core.CaptureFresh().Rows[..n];
            core.ScrollViewport(ViewportScroll.Bottom);
        }

        var sb = new StringBuilder();
        var size = core.Size;
        sb.Append(Header).Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"size {frame.Columns}x{frame.Rows.Length} cell {size.CellWidth}x{size.CellHeight}\n");
        var c = frame.Cursor;
        sb.Append(CultureInfo.InvariantCulture,
            $"cursor {c.Column},{c.Row} shape={Shape(c.Style)} visible={B(c.Visible)} blink={B(c.Blinking)} wide={B(c.WideTail)}\n");
        if (options.IncludeState)
        {
            var m = core.ReadInputModes();
            sb.Append("screen ").Append(m.AlternateScreen ? "alternate" : "primary").Append('\n');
            sb.Append(CultureInfo.InvariantCulture,
                $"modes decckm={B(m.CursorKeyApplication)} deckpam={B(m.KeypadApplication)} 2004={B(m.BracketedPaste)} 1004={B(m.FocusEvents)} " +
                $"mouse={m.MouseTracking.ToString().ToLowerInvariant()} kitty={m.KittyFlags} 2026={B(core.GetMode(2026))} 2027={B(m.GraphemeCluster)} " +
                $"origin={B(core.GetMode(6))} autowrap={B(core.GetMode(7))} insert={B(core.GetMode(4, ansi: true))} 9001={B(m.Win32Input)}\n");
            sb.Append("title ").Append(Quote(core.Title)).Append('\n');
            if (core.WorkingDirectoryRaw is { Length: > 0 } cwd)
            {
                sb.Append("cwd ").Append(cwd).Append('\n');
            }
        }

        // 样式与链接按首次出现的顺序编号（与捕获器的内部编号无关）
        var styleIds = new Dictionary<string, int>(StringComparer.Ordinal) { ["default"] = 0 };
        var styleList = new List<string> { "default" };
        var linkIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var rowLines = new List<string>();
        for (var i = 0; i < scrollback.Length; i++)
        {
            rowLines.Add(Row(i - scrollback.Length, scrollback[i], frame, styleIds, styleList, linkIds));
        }
        for (var y = 0; y < frame.Rows.Length; y++)
        {
            rowLines.Add(Row(y, frame.Rows[y], frame, styleIds, styleList, linkIds));
        }

        sb.Append("styles\n");
        for (var i = 0; i < styleList.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  s{i} {styleList[i]}\n");
        }
        if (linkIds.Count > 0)
        {
            sb.Append("links\n");
            foreach (var (uri, id) in linkIds.OrderBy(p => p.Value))
            {
                sb.Append(CultureInfo.InvariantCulture, $"  l{id} uri={Quote(uri)}\n");
            }
        }
        if (scrollback.Length > 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"scrollback {scrollback.Length}\n");
        }
        sb.Append("rows\n");
        foreach (var line in rowLines)
        {
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    private static string Row(int index, RowSnapshot row, FrameSnapshot frame, Dictionary<string, int> styleIds, List<string> styleList,
        Dictionary<string, int> linkIds)
    {
        var cols = row.Cells.Length;
        var text = new StringBuilder(cols + 8);
        var wide = new List<int>();
        var graphemes = new List<string>();
        var runs = new List<(int Style, int From, int To)>();
        var semantic = new List<(char Kind, int From, int To)>();
        for (var x = 0; x < cols; x++)
        {
            var cell = row.Cells[x];
            var width = TCell.Width(cell);
            var cp = TCell.Codepoint(cell);
            if (width == CellWidth.SpacerTail)
            {
                text.Append(WideTail);
            }
            else if (cp == 0 || width == CellWidth.SpacerHead)
            {
                text.Append(' ');
            }
            else
            {
                // 文本列只放基字符以保持对齐；多码点字形簇的码点列表单独记录
                text.Append(char.ConvertFromUtf32((int)cp));
                if (TCell.HasGrapheme(cell) && row.Graphemes is not null && row.Graphemes.TryGetValue(x, out var extra))
                {
                    graphemes.Add(GraphemeEntry(x, cp, extra));
                }
            }
            if (width == CellWidth.Wide)
            {
                wide.Add(x);
            }

            var style = StyleId(row.Styles[TCell.StyleIndex(cell)], frame, styleIds, styleList, linkIds);
            if (runs.Count > 0 && runs[^1].Style == style)
            {
                runs[^1] = runs[^1] with { To = x };
            }
            else
            {
                runs.Add((style, x, x));
            }

            var sem = TCell.Semantic(cell) switch { SemanticContent.Prompt => 'p', SemanticContent.Input => 'i', _ => '\0' };
            if (sem != '\0')
            {
                if (semantic.Count > 0 && semantic[^1].Kind == sem && semantic[^1].To == x - 1)
                {
                    semantic[^1] = semantic[^1] with { To = x };
                }
                else
                {
                    semantic.Add((sem, x, x));
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append(index.ToString(CultureInfo.InvariantCulture).PadLeft(4)).Append(" |").Append(text).Append("| ");
        sb.Append(runs.Count == 1 ? $"s{runs[0].Style}" : string.Join(' ', runs.Select(r => r.From == r.To ? $"s{r.Style}:{r.From}" : $"s{r.Style}:{r.From}-{r.To}")));
        if (wide.Count > 0)
        {
            sb.Append("  w:").Append(string.Join(',', wide));
        }
        if (graphemes.Count > 0)
        {
            sb.Append("  ").Append(string.Join(' ', graphemes));
        }
        if (semantic.Count > 0)
        {
            sb.Append("  sem:").Append(string.Join(',', semantic.Select(s => s.From == s.To ? $"{s.Kind}{s.From}" : $"{s.Kind}{s.From}-{s.To}")));
        }
        if (row.Flags.HasFlag(RowFlags.PromptLine))
        {
            sb.Append("  prompt");
        }
        if (row.Flags.HasFlag(RowFlags.SoftWrapped))
        {
            sb.Append("  wrap");
        }
        return sb.ToString();
    }

    private static string GraphemeEntry(int x, uint cp, int[]? extra)
    {
        var sb = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"g:{x}=U+{cp:X4}"));
        if (extra is not null)
        {
            foreach (var e in extra)
            {
                sb.Append(CultureInfo.InvariantCulture, $",U+{e:X4}");
            }
        }
        return sb.ToString();
    }

    private static int StyleId(in ResolvedStyle style, FrameSnapshot frame, Dictionary<string, int> styleIds, List<string> styleList,
        Dictionary<string, int> linkIds)
    {
        var parts = new List<string>(6);
        if (!style.Foreground.IsDefault) parts.Add("fg=" + Color(style.Foreground));
        if (!style.Background.IsDefault) parts.Add("bg=" + Color(style.Background));
        foreach (var flag in Enum.GetValues<StyleFlags>())
        {
            if (flag != StyleFlags.None && style.Flags.HasFlag(flag))
            {
                parts.Add(flag.ToString().ToLowerInvariant());
            }
        }
        if (style.Underline != UnderlineStyle.None) parts.Add("underline=" + style.Underline.ToString().ToLowerInvariant());
        if (!style.UnderlineColor.IsDefault) parts.Add("ul-color=" + Color(style.UnderlineColor));
        if (frame.LinkOf(style) is { } uri)
        {
            if (!linkIds.TryGetValue(uri, out var link))
            {
                link = linkIds.Count + 1;
                linkIds[uri] = link;
            }
            parts.Add($"link={link}");
        }
        var key = parts.Count == 0 ? "default" : string.Join(' ', parts);
        if (!styleIds.TryGetValue(key, out var id))
        {
            id = styleList.Count;
            styleList.Add(key);
            styleIds[key] = id;
        }
        return id;
    }

    private static string Color(TerminalColor c)
    {
        if (c.IsPalette)
        {
            return c.PaletteIndex.ToString(CultureInfo.InvariantCulture);
        }
        var rgb = c.ToRgb();
        return string.Create(CultureInfo.InvariantCulture, $"#{rgb.R:x2}{rgb.G:x2}{rgb.B:x2}");
    }

    private static string Shape(CursorStyle style) => style switch
    {
        CursorStyle.Bar => "bar",
        CursorStyle.Underline => "underline",
        CursorStyle.BlockHollow => "hollow",
        _ => "block",
    };

    private static int B(bool value) => value ? 1 : 0;

    private static string Quote(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case < ' ': sb.Append(CultureInfo.InvariantCulture, $"\\x{(int)ch:x2}"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.Append('"').ToString();
    }
}

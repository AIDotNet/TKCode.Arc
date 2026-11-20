using System.Text;
using BenchmarkDotNet.Attributes;
using StreamMark.Core.Parsing;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using RemendHealer = StreamMark.Core.Remend.Remend;

namespace StreamMark.Benchmarks;

/// <summary>
/// 把约 20KB 的聊天式文档按 4 字符一个 token 流完：对比"每个 token 整篇重解析"与 StreamMark 的增量解析器（AppendDelta）。
/// </summary>
[MemoryDiagnoser]
public class StreamingBenchmarks
{
    private string _document = "";

    [GlobalSetup]
    public void Setup() => _document = BenchmarkDocuments.Chat(20 * 1024);

    [Benchmark(Baseline = true, Description = "Full reparse per token (remend + MarkdownParser.Parse)")]
    public int FullReparsePerToken()
    {
        var blocks = 0;
        for (var i = 4; i <= _document.Length; i += 4)
        {
            var healed = RemendHealer.Complete(_document[..i]);
            blocks += MarkdownParser.Parse(healed).Length;
        }
        return blocks;
    }

    [Benchmark(Description = "StreamMark incremental AppendDelta (frozen prefix + container cache)")]
    public int ComposerPerToken()
    {
        var composer = new DocumentComposer();
        var blocks = 0;
        var pos = 0;
        var version = 0L;
        while (pos < _document.Length)
        {
            var len = Math.Min(4, _document.Length - pos);
            blocks += composer.AppendDelta(_document.AsSpan(pos, len), ++version).Count;
            pos += len;
        }
        return blocks + composer.FinishDelta(ReadOnlySpan<char>.Empty, ++version).Count;
    }

    [Benchmark(Description = "Static single-shot MarkdownParser.Parse (20KB)")]
    public int StaticFullParseOnce() => MarkdownParser.Parse(_document).Length;
}

/// <summary>
/// 长容器场景压测：100 行 GFM 表格与 100 项列表按 4 字符一个 token 流式到达，
/// 验证容器内行级/项级缓存将 O(R²) 降为 O(R)。
/// </summary>
[MemoryDiagnoser]
public class LargeContainerBenchmarks
{
    private string _tableDoc = "";
    private string _listDoc = "";

    [GlobalSetup]
    public void Setup()
    {
        var tb = new StringBuilder();
        tb.Append("| Metric | Value | Description |\n|:-------|------:|:------------|\n");
        for (var i = 1; i <= 100; i++)
        {
            tb.Append("| metric_").Append(i).Append(" | **").Append(i * 17).Append("** | `sample` with *emphasis* row ").Append(i).Append(" |\n");
        }
        _tableDoc = tb.ToString();

        var lb = new StringBuilder();
        for (var i = 1; i <= 100; i++)
        {
            lb.Append("- Item **").Append(i).Append("**: details with `code_").Append(i).Append("` and [link](https://example.com/").Append(i).Append(")\n");
        }
        _listDoc = lb.ToString();
    }

    [Benchmark(Description = "100-row table streamed 4 chars/token (incremental)")]
    public int Stream100RowTable() => StreamIncremental(_tableDoc);

    [Benchmark(Description = "100-item list streamed 4 chars/token (incremental)")]
    public int Stream100ItemList() => StreamIncremental(_listDoc);

    private static int StreamIncremental(string doc)
    {
        var composer = new DocumentComposer();
        var blocks = 0;
        var pos = 0;
        var version = 0L;
        while (pos < doc.Length)
        {
            var len = Math.Min(4, doc.Length - pos);
            blocks += composer.AppendDelta(doc.AsSpan(pos, len), ++version).Count;
            pos += len;
        }
        return blocks + composer.FinishDelta(ReadOnlySpan<char>.Empty, ++version).Count;
    }
}

/// <summary>
/// 含工具调用的聊天文档（每节后面一个 &lt;tool-call&gt;，内容是带 ** / 反引号 / 下划线的 JSON）：
/// 同一段文本分别按默认方言（未登记，HTML 块）、Markdown 内容组件、原样内容组件流完，看解析成本。
/// </summary>
[MemoryDiagnoser]
public class RawComponentBenchmarks
{
    private string _document = "";
    private MarkdownProfile _markdown = MarkdownProfile.Default;
    private MarkdownProfile _raw = MarkdownProfile.Default;

    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDocuments.ChatWithToolCalls(20 * 1024);
        _markdown = new MarkdownProfileBuilder().AddComponent(new ComponentSpec("tool-call", ComponentKind.Block)).Build();
        _raw = new MarkdownProfileBuilder().AddComponent(new ComponentSpec("tool-call", ComponentKind.Block) { Content = ComponentContent.Raw }).Build();
    }

    [Benchmark(Baseline = true, Description = "tool-call not registered (HTML block)")]
    public int Unregistered() => Stream(MarkdownProfile.Default);

    [Benchmark(Description = "tool-call as Markdown component")]
    public int MarkdownComponent() => Stream(_markdown);

    [Benchmark(Description = "tool-call as Raw component")]
    public int RawComponent() => Stream(_raw);

    private int Stream(MarkdownProfile profile)
    {
        var composer = new DocumentComposer(profile);
        var blocks = 0;
        var pos = 0;
        var version = 0L;
        while (pos < _document.Length)
        {
            var len = Math.Min(4, _document.Length - pos);
            blocks += composer.AppendDelta(_document.AsSpan(pos, len), ++version).Count;
            pos += len;
        }
        return blocks + composer.FinishDelta(ReadOnlySpan<char>.Empty, ++version).Count;
    }
}

[MemoryDiagnoser]
public class RemendBenchmarks
{
    private string _paragraph = "";

    [GlobalSetup]
    public void Setup()
    {
        // 约 58k 字符的单个尾部块（vercel changelog 中的回归用例规模）
        var words = string.Concat(Enumerable.Repeat("some **bold** and *italic* text with `code` and [link](https://example.com) ", 800));
        _paragraph = words[..58_000] + " **unclosed";
    }

    [Benchmark]
    public string Heal58kTail() => RemendHealer.Complete(_paragraph);
}

internal static class BenchmarkDocuments
{
    public static string Chat(int targetLength)
    {
        const string unit = """
            ## Section heading

            This is a paragraph with **bold**, *italic*, `inline code`, and a [link](https://example.com).
            It continues on a second line with 中文内容和 $x^2$ inline math.

            - first item
            - second item with **emphasis**
              - nested item

            ```csharp
            public int Add(int a, int b)
            {
                return a + b;
            }
            ```

            | Name | Value |
            |------|------:|
            | a    | 1     |
            | b    | 2     |

            > A quote with some text.


            """;
        var sb = new StringBuilder();
        while (sb.Length < targetLength)
        {
            sb.Append(unit);
        }
        return sb.ToString(0, targetLength);
    }

    public static string ChatWithToolCalls(int targetLength)
    {
        const string call = """
            <tool-call name="read_file">
            {"file_path": "src/app_main.cs", "query": "**bold** and `code`",
             "limit": 40, "pattern": "a_b_c ~ 20~25 $x$"}
            </tool-call>


            """;
        var unit = Chat(1200) + "\n\n" + call;
        var sb = new StringBuilder();
        while (sb.Length < targetLength)
        {
            sb.Append(unit);
        }
        return sb.ToString(0, targetLength);
    }
}

using BenchmarkDotNet.Attributes;
using Markdig;
using StreamMark.Core.Parsing;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using RemendHealer = StreamMark.Core.Remend.Remend;

namespace StreamMark.Benchmarks;

/// <summary>
/// 把约 20KB 的聊天式文档按 4 字符一个 token 流完：对比"每个 token 整篇重解析"与 StreamMark 的冻结 + 尾部解析。
/// </summary>
[MemoryDiagnoser]
public class StreamingBenchmarks
{
    private string _document = "";

    [GlobalSetup]
    public void Setup() => _document = BenchmarkDocuments.Chat(20 * 1024);

    [Benchmark(Baseline = true, Description = "Full reparse per token (remend + Markdig)")]
    public int FullReparsePerToken()
    {
        var blocks = 0;
        for (var i = 4; i <= _document.Length; i += 4)
        {
            var healed = RemendHealer.Complete(_document[..i]);
            blocks += Markdown.Parse(healed, StreamMarkPipeline.Default).Count;
        }
        return blocks;
    }

    [Benchmark(Description = "StreamMark composer (frozen prefix + tail)")]
    public int ComposerPerToken()
    {
        var composer = new DocumentComposer();
        var blocks = 0;
        for (var i = 4; i <= _document.Length; i += 4)
        {
            var from = composer.FrozenEnd;
            blocks += composer.Update(_document[from..i], from, i).Count;
        }
        return blocks + composer.Finish(_document, long.MaxValue).Count;
    }
}

/// <summary>
/// 含工具调用的聊天文档（每节后面一个 &lt;tool-call&gt;，内容是带 ** / 反引号 / 下划线的 JSON）：
/// 同一段文本分别按默认方言（未登记，HTML 块）、Markdown 内容组件、原样内容组件流完，看遮盖带来的成本。
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

    [Benchmark(Description = "tool-call as Raw component (masked)")]
    public int RawComponent() => Stream(_raw);

    private int Stream(MarkdownProfile profile)
    {
        var composer = new DocumentComposer(profile);
        var blocks = 0;
        for (var i = 4; i <= _document.Length; i += 4)
        {
            var from = composer.FrozenEnd;
            blocks += composer.Update(_document[from..i], from, i).Count;
        }
        return blocks + composer.Finish(_document, long.MaxValue).Count;
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
        var sb = new System.Text.StringBuilder();
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
        var sb = new System.Text.StringBuilder();
        while (sb.Length < targetLength)
        {
            sb.Append(unit);
        }
        return sb.ToString(0, targetLength);
    }
}

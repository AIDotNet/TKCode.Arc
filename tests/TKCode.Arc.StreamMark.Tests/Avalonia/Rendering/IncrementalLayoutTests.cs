using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>逐行增量排版：流式追加时只重排末尾几行，结果与整段排版一致；渐显在渲染线程完成并按时结束。</summary>
public sealed class IncrementalLayoutTests(ITestOutputHelper output)
{
    private const string Paragraph =
        "流式渲染的核心问题在于每一帧都要重新排版整个段落，这会带来线性增长的开销。The renderer reshapes the **whole paragraph** on every frame, " +
        "which costs time proportional to its length。禁则处理：行首不能出现句号、逗号，也不能出现右括号）。Mixed `inline code` and *italic* words, " +
        "加上一些较长的英文单词 internationalization、characterization，以及中文标点，“引号”与（括号）。";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamedLineBreaks_MatchFullLayout_AtEveryStep(bool styled)
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false);
            var live = new MarkdownView { Stream = stream, Width = 360 };
            var window = new Window { Width = 400, Height = 800, Content = live, Background = Brushes.Black };
            window.Show();
            var paragraph = styled ? Paragraph : Plain(Paragraph);
            var text = paragraph + paragraph;
            var compared = 0;
            for (var i = 0; i < text.Length; i += 7)
            {
                stream.Push(text.Substring(i, Math.Min(7, text.Length - i)));
                await stream.Document.WhenIdle();
                live.ApplyLatest();
                await HeadlessApp.Settle(1);

                var streamed = await SettledSegment(live);
                var reference = new MarkdownView { Markdown = text[..Math.Min(text.Length, i + 7)], Width = 360 };
                var referenceWindow = new Window { Width = 400, Height = 800, Content = reference };
                referenceWindow.Show();
                reference.ApplyLatest();
                await HeadlessApp.Settle(1);
                var full = await SettledSegment(reference);
                // 尾部补全可能让流式内容与整篇解析不同：只比较内容相同的步骤
                if (full.Content == streamed.Content)
                {
                    compared++;
                    Assert.Equal(Breaks(full), Breaks(streamed));
                }
                referenceWindow.Close();
            }
            var steps = (text.Length + 6) / 7;
            Assert.True(compared >= (styled ? 5 : steps * 3 / 4), $"only {compared} of {steps} steps compared");
            window.Close();
        });
    }

    /// <summary>
    /// 回退字体与取 run 的起点无关：逐行重排从行首取 run（"。禁则…"从"首"开始），按行首的字匹配回退字体时
    /// （韩文与汉字连写，起点不同会落到不同字体）流式结果会与整段排版不同。
    /// </summary>
    [Theory]
    [InlineData(161)]
    [InlineData(238)]
    [InlineData(271)]
    public async Task StreamedMixedScripts_MatchFullLayout(double width)
    {
        await HeadlessApp.Run(async () =>
        {
            var paragraph = Plain(Paragraph).Replace("加上一些", "한국어로작성된문장과中文混排的句子没有空格한국어로작성된문장과中文混排的句子。加上一些");
            var text = paragraph + paragraph;
            var stream = new MarkdownStream(smooth: false);
            var live = new MarkdownView { Stream = stream, Width = width };
            var window = new Window { Width = width + 40, Height = 800, Content = live };
            window.Show();
            for (var i = 0; i < text.Length; i += 7)
            {
                stream.Push(text.Substring(i, Math.Min(7, text.Length - i)));
                await stream.Document.WhenIdle();
                live.ApplyLatest();
                await HeadlessApp.Settle(1);
            }
            var reference = new MarkdownView { Markdown = text, Width = width };
            var referenceWindow = new Window { Width = width + 40, Height = 800, Content = reference };
            referenceWindow.Show();
            reference.ApplyLatest();
            await HeadlessApp.Settle(1);
            Assert.Equal(Breaks(await SettledSegment(reference)), Breaks(await SettledSegment(live)));
            referenceWindow.Close();
            window.Close();
        });
    }

    [Fact]
    public async Task AppendCost_DoesNotGrowWithParagraphLength()
    {
        await HeadlessApp.Run(async () =>
        {
            var shortCost = await MeasureAppendCost(1_000);
            var longCost = await MeasureAppendCost(8_000);
            output.WriteLine($"median append layout: 1KB {shortCost:F3} ms, 8KB {longCost:F3} ms");
            // 整段重排时 8KB 的成本约为 1KB 的 8 倍；逐行排版应基本持平
            var limit = Environment.GetEnvironmentVariable("CI") is null ? 3.0 : 5.0;
            Assert.True(longCost < shortCost * limit + 0.2, $"1KB {shortCost:F3} ms vs 8KB {longCost:F3} ms");
        });
    }

    [Fact]
    public async Task TailFade_RunsOnCompositor_AndSettles()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.FromMilliseconds(300));
            var view = new MarkdownView { Stream = stream, Width = 600 };
            var window = new Window { Width = 640, Height = 300, Content = view, Background = Brushes.Black };
            window.Show();
            stream.Push("The quick brown fox jumps over the lazy dog. ");
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle(1);
            stream.Push("Newest words are fading in");
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle(1);

            var segment = await SettledSegment(view);
            Assert.True(segment.IsDocumentTail);
            Assert.NotNull(ElementComposition.GetElementChildVisual(segment));
            Assert.Contains(Lines(segment), l => l.ClipRight is not null);
            HeadlessApp.Save(window, "tail-fade-midway");

            await Task.Delay(500);
            await HeadlessApp.Settle(3);
            Assert.All(Lines(segment), l => Assert.Null(l.ClipRight));
            HeadlessApp.Save(window, "tail-fade-settled");
            stream.Complete();
            await HeadlessApp.Settle();
            window.Close();
        });
    }

    private static async Task<double> MeasureAppendCost(int length)
    {
        var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.Zero);
        var view = new MarkdownView { Stream = stream, Width = 700 };
        var window = new Window { Width = 740, Height = 2000, Content = view };
        window.Show();
        var text = string.Concat(Enumerable.Repeat(Plain(Paragraph), length / Paragraph.Length + 2))[..length];
        stream.Push(text);
        await stream.Document.WhenIdle();
        view.ApplyLatest();
        await HeadlessApp.Settle(1);
        var segment = await SettledSegment(view);
        var samples = new List<double>();
        for (var i = 0; i < 40; i++)
        {
            stream.Push(i % 5 == 4 ? "，" : "追加文字 ");
            await stream.Document.WhenIdle();
            var watch = Stopwatch.StartNew();
            view.ApplyLatest();
            view.Measure(new global::Avalonia.Size(700, double.PositiveInfinity));
            segment.Measure(new global::Avalonia.Size(700, double.PositiveInfinity));
            watch.Stop();
            samples.Add(watch.Elapsed.TotalMilliseconds);
            await HeadlessApp.Settle(1);
        }
        window.Close();
        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static string Plain(string markdown) => markdown.Replace("**", "").Replace("`", "").Replace("*", "");

    private static TextSegmentView Segment(Control view) => view.GetVisualDescendants().OfType<TextSegmentView>().Last();

    /// <summary>
    /// 等到文本片段出现再取：进程里第一个测试冷启动时（CI 的 x64 Linux / Windows），一帧之内块视图可能还没建好。
    /// </summary>
    private static async Task<TextSegmentView> SettledSegment(Control view)
    {
        for (var i = 0; i < 50 && !view.GetVisualDescendants().OfType<TextSegmentView>().Any(); i++)
        {
            await HeadlessApp.Settle(1);
        }
        return Segment(view);
    }

    private static IEnumerable<SegmentLineVisual> Lines(TextSegmentView segment) => segment.GetVisualChildren().OfType<SegmentLineVisual>();

    private static List<(int Start, int Length)> Breaks(TextSegmentView segment)
    {
        var layout = segment.Layout!;
        return Enumerable.Range(0, layout.LineCount).Select(i => (layout.LineStart(i), layout.LineLength(i))).ToList();
    }
}

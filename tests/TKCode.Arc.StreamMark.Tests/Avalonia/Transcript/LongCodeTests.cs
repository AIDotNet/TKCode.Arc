using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

public sealed class LongCodeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ThreeThousandLineCode_IsChunked_AndCopiesExactly()
    {
        await HeadlessApp.Run(async () =>
        {
            var code = new StringBuilder();
            for (var i = 0; i < 3000; i++)
            {
                code.Append("var line").Append(i).Append(" = ").Append(i).Append(";\n");
            }
            var source = code.ToString().TrimEnd('\n');
            var markdown = "Before.\n\n```cs\n" + source + "\n```\n\nAfter.";
            var view = new TranscriptView();
            // 本测试验证分段：关闭默认折叠
            Extensions.MarkdownServices.SetServices(view, new Extensions.MarkdownServices { CodeBlocks = Extensions.CodeBlockOptions.Default with { CollapseAfterLines = 0 } });
            var window = new Window { Width = 900, Height = 700, Content = view };
            window.Show();
            var entry = MarkdownEntry.FromText(markdown);
            view.Entries.Add(entry);
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();

            var panel = view.Panel;
            // 3000 行 / 128 = 24 段，加上前后两个段落
            Assert.Equal(26, panel.RowCount);
            Assert.InRange(panel.RealizedCount, 1, 12);

            var times = new List<double>();
            foreach (var fraction in new[] { 0.0, 0.33, 0.66, 0.5, 0.9 })
            {
                var watch = Stopwatch.StartNew();
                view.Offset = new(0, panel.Extent.Height * fraction);
                panel.InvalidateMeasure();
                panel.Measure(panel.Bounds.Size);
                panel.Arrange(new(panel.Bounds.Size));
                times.Add(watch.Elapsed.TotalMilliseconds);
                Assert.InRange(panel.RealizedCount, 1, 12);
            }
            output.WriteLine("jump layout ms: " + string.Join(", ", times.Select(t => t.ToString("F1"))));
            // CI 共享虚拟机比本机慢一个数量级，只防止退化成整块排版（数百毫秒）
            var ci = Environment.GetEnvironmentVariable("CI") is not null;
            var jumps = times.Skip(1).Order().ToList();
            // 中位数衡量常态；最大值只防止退化成整块排版（偶发的 GC / 机器繁忙不算失败）
            Assert.True(jumps[jumps.Count / 2] < (ci ? 100 : 20), "chunk jump layout too slow (median)");
            Assert.True(jumps[^1] < (ci ? 300 : 100), "chunk jump layout too slow (max)");

            // 中段视图只显示自己的行窗口，头部只在第一段
            view.Offset = new(0, panel.Extent.Height * 0.5);
            await HeadlessApp.Settle();
            var chunks = panel.RowViews.OfType<CodeBlockView>().ToList();
            Assert.NotEmpty(chunks);
            Assert.All(chunks, c => Assert.True(c.Chunk.Count == 24));
            HeadlessApp.Save(window, "long-code-middle");

            // 全选复制：与源码完全一致；跨分段的局部选区
            var snapshot = entry.Document.Latest;
            var codeKey = snapshot[1].Key;
            view.Selection.Set(new TextPoint(0, codeKey, 0, 0), new TextPoint(0, codeKey, 0, source.Length));
            Assert.Equal(source, view.SelectionController.GetSelectedText());
            var lineStart = source.IndexOf("var line127 ", StringComparison.Ordinal);
            var lineEnd = source.IndexOf("var line129 ", StringComparison.Ordinal);
            view.Selection.Set(new TextPoint(0, codeKey, 0, lineStart), new TextPoint(0, codeKey, 0, lineEnd));
            Assert.Equal("var line127 = 127;\nvar line128 = 128;\n", view.SelectionController.GetSelectedText());
            window.Close();
        });
    }

    [Fact]
    public async Task StreamingTailInserts_AndCodeStrips_PreserveRowCountAndHitTest()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            Extensions.MarkdownServices.SetServices(view, new Extensions.MarkdownServices { CodeBlocks = Extensions.CodeBlockOptions.Default with { CollapseAfterLines = 0 } });
            var window = new Window { Width = 800, Height = 700, Content = view };
            window.Show();

            var stream = new MarkdownStream(smooth: false);
            var entry = new MarkdownEntry(stream);
            view.Entries.Add(entry);
            await HeadlessApp.Settle();

            // 逐步追加多个顶层块（走 O(1) 尾部直插路径）与 40 行代码块（3 个 16 行分条）
            for (var p = 0; p < 5; p++)
            {
                stream.Push($"Paragraph {p} text.\n\n");
                await entry.Document.WhenIdle();
                view.Panel.ApplyLatest();
                await HeadlessApp.Settle();
            }

            var sb = new StringBuilder("```cs\n");
            for (var i = 0; i < 40; i++)
            {
                sb.Append("var x").Append(i).Append(" = ").Append(i).Append(";\n");
            }
            sb.Append("```\n");
            stream.Push(sb.ToString());
            stream.Complete();
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();

            Assert.Equal(6, view.Panel.RowCount);
            var codeView = Assert.Single(view.Panel.RowViews.OfType<CodeBlockView>());
            var textView = Assert.IsType<CodeTextView>(Assert.Single(codeView.Segments()));
            // 40 行 / 16 行每条 = 3 个分条视觉
            Assert.Equal(3, textView.StripCount);
            Assert.Equal(40, textView.VisibleLineCount);

            // 跨分条边界的 GetCharacterBounds / HitTest 一致
            var targetOffset = textView.Text.IndexOf("var x20", StringComparison.Ordinal);
            var bounds = textView.GetCharacterBounds(targetOffset);
            var hit = textView.HitTest(bounds.Center);
            Assert.InRange(hit, targetOffset - 1, targetOffset + 2);

            // 切换换行后分条视觉数量与内容保持一致
            codeView.Wrap = true;
            await HeadlessApp.Settle();
            Assert.Equal(3, textView.StripCount);
            codeView.Wrap = false;
            await HeadlessApp.Settle();
            Assert.Equal(3, textView.StripCount);

            window.Close();
        });
    }
}

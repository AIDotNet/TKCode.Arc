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
            var chunks = panel.Children.OfType<CodeBlockView>().ToList();
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
}

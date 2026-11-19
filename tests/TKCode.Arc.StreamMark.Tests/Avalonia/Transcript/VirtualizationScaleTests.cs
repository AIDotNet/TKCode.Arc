using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

/// <summary>规模测试：10 万个顶层块的会话，滚动与追加只处理视口附近的行。</summary>
public sealed class VirtualizationScaleTests(ITestOutputHelper output)
{
    [Fact]
    public async Task HundredThousandBlocks_ScrollAndAppend_StayCheap()
    {
        await HeadlessApp.Run(async () =>
        {
            var sb = new StringBuilder();
            for (var i = 0; i < 100_000; i++)
            {
                sb.Append("Paragraph ").Append(i).Append(" with some **bold** text and `code`.\n\n");
            }
            var view = new TranscriptView();
            var window = new Window { Width = 900, Height = 800, Content = view };
            window.Show();
            var entry = MarkdownEntry.FromText(sb.ToString());
            view.Entries.Add(entry);
            await entry.Document.WhenIdle();
            var watch = Stopwatch.StartNew();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle(2);
            output.WriteLine($"initial apply + layout: {watch.ElapsedMilliseconds} ms");
            Assert.Equal(100_000, view.Panel.RowCount);
            Assert.InRange(view.Panel.RealizedCount, 1, 120);

            // 跳到不同位置：每次只实例化视口附近的行
            var times = new List<double>();
            foreach (var fraction in new[] { 0.1, 0.5, 0.9, 0.3, 0.7 })
            {
                watch.Restart();
                view.Offset = new(0, view.Panel.Extent.Height * fraction);
                view.Panel.InvalidateMeasure();
                view.Panel.Measure(view.Panel.Bounds.Size);
                view.Panel.Arrange(new(view.Panel.Bounds.Size));
                times.Add(watch.Elapsed.TotalMilliseconds);
                Assert.InRange(view.Panel.RealizedCount, 1, 120);
            }
            output.WriteLine($"jump-scroll layout: {string.Join(", ", times.Select(t => t.ToString("F1")))} ms");
            Assert.True(times.Skip(1).Max() < 250, "scroll layout too slow");

            // 在 10 万块之后继续流式追加
            var stream = new MarkdownStream(smooth: false);
            view.Entries.Add(new MarkdownEntry(stream));
            view.ScrollToEnd();
            watch.Restart();
            for (var i = 0; i < 50; i++)
            {
                stream.Push($"token{i} ");
                await HeadlessApp.Settle(1);
                await stream.Document.WhenIdle();
                view.Panel.ApplyLatest();
                view.Panel.Measure(view.Panel.Bounds.Size);
                view.Panel.Arrange(new(view.Panel.Bounds.Size));
            }
            output.WriteLine($"50 appends after 100k blocks: {watch.ElapsedMilliseconds} ms total (includes 20ms settle per append)");
            Assert.True(view.Panel.IsPinnedToBottom);
            window.Close();
        });
    }
}

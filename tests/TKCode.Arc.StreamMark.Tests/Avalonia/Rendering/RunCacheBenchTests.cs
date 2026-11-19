using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>对比流式追加长段落时，使用 / 不使用整形缓存的排版耗时（输出数字，断言只检查正确性）。</summary>
public sealed class RunCacheBenchTests(ITestOutputHelper output)
{
    private static async Task<double> StreamParagraph(bool useCache)
    {
        TextSegmentView.UseRunCache = useCache;
        try
        {
            var document = new StreamingDocument();
            var view = new MarkdownView { Document = document };
            var window = new Window { Width = 700, Height = 900, Content = view };
            window.Show();
            var sentence = "这是一段很长的流式段落，包含 **粗体**、`code` 与 English words mixed together. ";
            var text = new StringBuilder();
            for (var i = 0; i < 40; i++)
            {
                text.Append(sentence);
            }
            var full = text.ToString();
            double total = 0;
            for (var i = 0; i < full.Length; i += 12)
            {
                document.Append(full.AsSpan(i, Math.Min(12, full.Length - i)));
                await document.WhenIdle();
                view.ApplyLatest();
                var watch = Stopwatch.StartNew();
                view.Measure(new global::Avalonia.Size(680, double.PositiveInfinity));
                total += watch.Elapsed.TotalMilliseconds;
            }
            var segment = view.GetVisualDescendantsOfType<TextSegmentView>().Single();
            Assert.Equal(full.Replace("**", "").Replace("`", "").TrimEnd(), segment.Content.Text.TrimEnd());
            window.Close();
            return total;
        }
        finally
        {
            TextSegmentView.UseRunCache = true;
        }
    }

    [Fact]
    public async Task StreamingParagraph_LayoutCost_WithAndWithoutRunCache()
    {
        await HeadlessApp.Run(async () =>
        {
            await StreamParagraph(true); // 预热
            var without = await StreamParagraph(false);
            var with = await StreamParagraph(true);
            output.WriteLine($"streaming paragraph measure total: without cache {without:F1} ms, with cache {with:F1} ms");
        });
    }
}

internal static class VisualTreeHelpers
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this global::Avalonia.Visual visual) =>
        global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(visual).OfType<T>();
}

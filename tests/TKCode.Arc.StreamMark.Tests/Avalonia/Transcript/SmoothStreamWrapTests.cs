using Avalonia.Controls;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

public sealed class SmoothStreamWrapTests
{
    // 回归：平滑渐显时，每次放出的边界不应变成硬换行（排版缓存不能保留旧文本末尾的段落结束）
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SmoothReveal_WrapsAsOneParagraph(bool smooth)
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 700, Height = 400, Content = view };
            window.Show();
            var stream = new MarkdownStream(smooth: smooth);
            view.Entries.Add(new MarkdownEntry(stream));
            const string text = "我先看仓库结构、技术栈和当前改动状态。然后定位：AI 模型 API 网关与计费平台。";
            for (var i = 0; i < text.Length; i += 3)
            {
                stream.Push(text.Substring(i, Math.Min(3, text.Length - i)));
                await HeadlessApp.Settle(1);
                await Task.Delay(16);
            }
            stream.Complete();
            for (var i = 0; i < 60; i++)
            {
                await HeadlessApp.Settle(1);
                await Task.Delay(10);
            }
            var segment = view.Panel.GetVisualDescendants().OfType<TextSegmentView>().Single();
            Assert.Equal(text, segment.Content.Text);
            // 700px 宽度下这一句放得下一行
            Assert.Single(segment.Layout!.TextLines);
            window.Close();
        });
    }
}

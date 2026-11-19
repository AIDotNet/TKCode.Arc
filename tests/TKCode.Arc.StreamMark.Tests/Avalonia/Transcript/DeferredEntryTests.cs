using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

public sealed class DeferredEntryTests
{
    private static string Message(int i) =>
        $"Message {i} heading\n\nParagraph of message {i} with some text that wraps a little.\n\n- item a{i}\n- item b{i}\n";

    private static TextSegmentView? Find(TranscriptView view, string text) =>
        view.Panel.GetVisualDescendants().OfType<TextSegmentView>().FirstOrDefault(s => s.Content.Text.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task DeferredEntries_ParseOnlyNearViewport()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            var entries = Enumerable.Range(0, 200).Select(i => MarkdownEntry.FromTextDeferred(Message(i), tag: i)).ToList();
            foreach (var entry in entries)
            {
                view.Entries.Add(entry);
            }
            await HeadlessApp.Settle();

            // 贴底：只有末尾附近的条目被解析
            var parsed = entries.Count(e => e.IsParsed);
            Assert.InRange(parsed, 1, 40);
            Assert.True(entries[^1].IsParsed);
            Assert.False(entries[0].IsParsed);
            Assert.Equal(0, entries[0].Tag);
            Assert.NotNull(Find(view, "Paragraph of message 199"));

            // 跳到顶部：顶部条目按需解析并显示
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();
            Assert.True(entries[0].IsParsed);
            Assert.NotNull(Find(view, "Paragraph of message 0 "));

            // 全选复制：跨过尚未解析的条目也能得到完整文本
            view.Panel.SelectionController.SelectAll();
            var text = view.Panel.SelectionController.GetSelectedText();
            Assert.Contains("Paragraph of message 100 ", text);
            Assert.Contains("item b150", text);
            window.Close();
        });
    }

    [Fact]
    public async Task InsertAboveViewport_KeepsVisibleContentInPlace()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            for (var i = 100; i < 140; i++)
            {
                view.Entries.Add(MarkdownEntry.FromTextDeferred(Message(i)));
            }
            await HeadlessApp.Settle();
            // 离开底部（解除贴底），停在中间某条消息
            view.Offset = new(0, view.Panel.Extent.Height / 2);
            await HeadlessApp.Settle();
            var target = view.Panel.GetVisualDescendants().OfType<TextSegmentView>()
                .Where(s => s.Content.Text.StartsWith("Paragraph of message", StringComparison.Ordinal))
                .OrderBy(s => s.TranslatePoint(default, view.Panel)!.Value.Y)
                .First(s => s.TranslatePoint(default, view.Panel)!.Value.Y > 0);
            var label = target.Content.Text;
            var before = target.TranslatePoint(default, view.Panel)!.Value.Y;

            // 模拟加载更早的一页：在开头插入 20 条（按需解析，估算高度）
            for (var i = 99; i >= 80; i--)
            {
                view.Entries.Insert(0, MarkdownEntry.FromTextDeferred(Message(i)));
            }
            await HeadlessApp.Settle();
            var after = Find(view, label);
            Assert.NotNull(after);
            Assert.Equal(before, after!.TranslatePoint(default, view.Panel)!.Value.Y, 1.0);
            Assert.False(view.Panel.IsPinnedToBottom);
            window.Close();
        });
    }
}

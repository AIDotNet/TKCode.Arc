using Avalonia.Controls;
using Avalonia.Media;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Selection;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

public sealed class TranscriptViewTests
{
    private static (Window Window, TranscriptView View) Create(double height = 700)
    {
        var view = new TranscriptView { Padding = new(24, 16) };
        var window = new Window { Width = 860, Height = height, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
        window.Show();
        return (window, view);
    }

    private static Border UserBubble(string text) => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x33)),
        CornerRadius = new(10),
        Padding = new(14, 10),
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
        Child = new TextBlock { Text = text, Foreground = Brushes.White },
    };

    private static async Task Converge(TranscriptView view)
    {
        foreach (var entry in view.Entries.OfType<MarkdownEntry>())
        {
            await entry.Document.WhenIdle();
        }
        await HeadlessApp.Settle();
        view.Panel.ApplyLatest();
        await HeadlessApp.Settle();
    }

    [Fact]
    public async Task LongTranscript_RealizesOnlyVisibleRows_AndStartsPinnedToBottom()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = Create();
            var markdown = HeadlessApp.Fixture("chat-mixed.md");
            for (var i = 0; i < 60; i++)
            {
                view.Entries.Add(new ContentEntry(UserBubble($"问题 {i + 1}：请给我一个示例")));
                view.Entries.Add(MarkdownEntry.FromText(markdown));
            }
            await Converge(view);

            var panel = view.Panel;
            Assert.True(panel.RowCount > 1000, $"rows {panel.RowCount}");
            Assert.InRange(panel.RealizedCount, 1, 80);
            Assert.True(panel.IsPinnedToBottom);
            Assert.True(panel.Extent.Height > 50_000);
            Assert.Equal(panel.Extent.Height - panel.Viewport.Height, panel.Offset.Y, 1.0);
            HeadlessApp.Save(window, "transcript-bottom");

            // 滚到顶部：解除贴底，只实例化顶部附近的行
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();
            Assert.False(panel.IsPinnedToBottom);
            Assert.InRange(panel.RealizedCount, 1, 80);
            HeadlessApp.Save(window, "transcript-top");
            window.Close();
        });
    }

    [Fact]
    public async Task StreamingEntry_StaysPinned_AndOlderContentDoesNotMove()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = Create();
            view.Entries.Add(MarkdownEntry.FromText(HeadlessApp.Fixture("lists-quotes.md")));
            var stream = new MarkdownStream(smooth: false);
            view.Entries.Add(new MarkdownEntry(stream));
            await Converge(view);

            var text = HeadlessApp.Fixture("code-heavy.md");
            for (var i = 0; i < text.Length; i += 40)
            {
                stream.Push(text.Substring(i, Math.Min(40, text.Length - i)));
                await HeadlessApp.Settle(1);
                await Converge(view);
                Assert.True(view.Panel.IsPinnedToBottom);
                Assert.Equal(Math.Max(0, view.Panel.Extent.Height - view.Panel.Viewport.Height), view.Panel.Offset.Y, 1.0);
            }
            HeadlessApp.Save(window, "transcript-streaming");

            // 用户上滚后继续流式：视口内容保持不动
            view.Offset = new(0, 100);
            await HeadlessApp.Settle();
            var before = view.Panel.Offset.Y;
            stream.Push("\n\nMore text arriving while the user reads older content.");
            stream.Complete();
            await Converge(view);
            Assert.False(view.Panel.IsPinnedToBottom);
            Assert.Equal(before, view.Panel.Offset.Y, 1.0);
            window.Close();
        });
    }

    [Fact]
    public async Task Selection_SpansMessages_AndCopiesVirtualizedRows()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = Create(height: 400);
            view.Entries.Add(MarkdownEntry.FromText("# First\n\nAlpha paragraph."));
            view.Entries.Add(new ContentEntry(UserBubble("user")));
            for (var i = 0; i < 30; i++)
            {
                view.Entries.Add(MarkdownEntry.FromText($"Message {i} paragraph.\n\n- item a\n- item b"));
            }
            await Converge(view);

            // 从第一条消息的标题开头选到最后一条消息末尾（中间大部分行未实例化）
            var last = view.Entries.Count - 1;
            view.Selection.Set(new TextPoint(0, 0, 0, 0), new TextPoint(last, int.MaxValue, 0, 0));
            var text = view.SelectionController.GetSelectedText();
            Assert.StartsWith("First\n\nAlpha paragraph.", text);
            Assert.Contains("Message 15 paragraph.", text);
            Assert.EndsWith("- item b", text);

            var markdown = view.SelectionController.GetSelectedMarkdown();
            Assert.StartsWith("# First", markdown);
            Assert.Contains("- item a\n- item b", markdown);

            // 片段内部分选择
            view.Selection.Set(new TextPoint(0, "# First\n\n".Length, 0, 2), new TextPoint(0, "# First\n\n".Length, 0, 7));
            Assert.Equal("pha p", view.SelectionController.GetSelectedText());
            await HeadlessApp.Settle();
            window.Close();
        });
    }

    [Fact]
    public async Task ContentEntry_ThatGrows_PushesFollowingRowsDown()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = Create();
            // 模拟"思考过程"折叠块：展开后高度变大
            var details = new TextBlock { Text = string.Join("\n", Enumerable.Range(1, 8).Select(i => $"thought line {i}")), IsVisible = false, Foreground = Brushes.Gray };
            var thinking = new StackPanel { Children = { new TextBlock { Text = "思考过程", Foreground = Brushes.White }, details } };
            view.Entries.Add(new ContentEntry(thinking));
            var answer = MarkdownEntry.FromText("Answer paragraph that must stay below the thinking block.");
            view.Entries.Add(answer);
            await Converge(view);

            var answerView = view.Panel.Children.OfType<global::StreamMark.Avalonia.Rendering.BlockView>().Single();
            var collapsedTop = answerView.Bounds.Top;
            Assert.True(collapsedTop >= thinking.Bounds.Bottom - 0.5, $"overlap before expand: {collapsedTop} < {thinking.Bounds.Bottom}");

            details.IsVisible = true;
            await HeadlessApp.Settle();
            Assert.True(thinking.Bounds.Height > 100, $"thinking height {thinking.Bounds.Height}");
            Assert.True(answerView.Bounds.Top >= thinking.Bounds.Bottom - 0.5, $"overlap after expand: {answerView.Bounds.Top} < {thinking.Bounds.Bottom}");
            HeadlessApp.Save(window, "transcript-content-grows");

            details.IsVisible = false;
            await HeadlessApp.Settle();
            Assert.Equal(collapsedTop, answerView.Bounds.Top, 1.0);
            window.Close();
        });
    }
}

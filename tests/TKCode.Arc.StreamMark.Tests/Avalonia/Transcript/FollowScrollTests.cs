using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Transcript;

/// <summary>
/// 贴底跟随：新内容把视口往下推时，已在视口里、内容没变的行不应改变 Bounds
/// （Avalonia 在 Bounds 变化时重新录制该视觉，滚动应只移动承载各行的图层）。
/// </summary>
public sealed class FollowScrollTests(ITestOutputHelper output)
{
    [Fact]
    public async Task FollowingTheTail_DoesNotMoveSettledRows()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 860, Height = 700, Content = view };
            window.Show();
            var history = HeadlessApp.Fixture("chat-mixed.md");
            for (var i = 0; i < 4; i++)
            {
                view.Entries.Add(MarkdownEntry.FromText(history));
            }
            var stream = new MarkdownStream(smooth: false);
            view.Entries.Add(new MarkdownEntry(stream));
            await HeadlessApp.Settle();

            // 记录已实例化顶层视图的 Bounds 变化（不含流式消息本身的行）
            var moves = 0;
            var watched = new HashSet<Control>();
            void Watch()
            {
                foreach (var row in TopLevelViews(view.Panel))
                {
                    if (watched.Add(row))
                    {
                        row.PropertyChanged += (_, e) =>
                        {
                            if (e.Property == Visual.BoundsProperty && e.OldValue is Rect old && e.NewValue is Rect now && old.Size == now.Size && old.Position != now.Position)
                            {
                                moves++;
                            }
                        };
                    }
                }
            }
            Watch();
            var before = view.Panel.Offset.Y;

            for (var i = 0; i < 40; i++)
            {
                stream.Push($"第 {i} 段：贴底跟随时，新出现的行会把视口往下推。\n\n");
                await HeadlessApp.Settle(2);
                Watch();
            }
            Assert.True(view.Panel.IsPinnedToBottom);
            Assert.True(view.Panel.Offset.Y > before + 500, $"offset {before} -> {view.Panel.Offset.Y}");
            output.WriteLine($"watched {watched.Count} views, moved {moves} times");
            // 只允许偶尔的图层基准重设
            Assert.True(moves <= watched.Count, $"{moves} moves for {watched.Count} views");
            window.Close();
        });
    }

    // 会话面板里每行一个顶层视图（块视图或宿主内容），嵌套的块视图不算
    internal static IEnumerable<Control> TopLevelViews(TranscriptPanel panel) =>
        panel.GetVisualDescendants().OfType<BlockView>().Where(v => v.FindAncestorOfType<BlockView>() is null);
}

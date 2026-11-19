using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class RefreshMarkdownTests
{
    private const string Source = "# Title\n\nFirst paragraph here.\n\nSecond one.\n\nLast paragraph.\n";

    private static async Task<(Window Window, MarkdownView View)> Show(string markdown)
    {
        var view = new MarkdownView { Markdown = markdown, Margin = new(12) };
        var window = new Window { Width = 520, Height = 420, Content = view, Background = Brushes.Black };
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        return (window, view);
    }

    // 刷新并等到新快照上屏
    private static async Task Refresh(MarkdownView view, string markdown)
    {
        var before = view.Snapshot!.Version;
        view.RefreshMarkdown(markdown);
        for (var i = 0; i < 100 && view.Snapshot!.Version == before; i++)
        {
            await HeadlessApp.Settle(2);
        }
        await HeadlessApp.Settle();
        Assert.NotEqual(before, view.Snapshot!.Version);
    }

    private static List<BlockView> Blocks(MarkdownView view) => view.GetVisualChildren().OfType<BlockView>().ToList();

    [Fact]
    public async Task EditingTheLastParagraph_KeepsSelectionInFirstParagraphAndReusesViews()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[1].Key, 0, 0), new TextPoint(0, snapshot[1].Key, 0, 5));
            Assert.Equal("First", view.SelectionController.GetSelectedText());
            var documentId = view.DocumentId;
            var viewsBefore = Blocks(view);

            await Refresh(view, Source.Replace("Last paragraph.", "Changed last paragraph, a bit longer."));

            Assert.Equal("First", view.SelectionController.GetSelectedText());
            Assert.Equal(documentId, view.DocumentId);
            var viewsAfter = Blocks(view);
            Assert.Equal(viewsBefore.Count, viewsAfter.Count);
            // 没变的块与被改写的块都是同一个视图实例（原地更新）
            Assert.All(viewsBefore.Zip(viewsAfter), pair => Assert.Same(pair.First, pair.Second));
            Assert.Contains("Changed last paragraph", ((ParagraphBlock)view.Snapshot![^1]).Content.Text);
            window.Close();
        });
    }

    [Fact]
    public async Task InsertingBeforeTheSelection_RemapsPositionsAndKeepsViews()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[2].Key, 0, 0), new TextPoint(0, snapshot[3].Key, 0, 4));
            var selectedBefore = view.SelectionController.GetSelectedText();
            Assert.Equal("Second one.\n\nLast", selectedBefore);
            var viewsBefore = Blocks(view);
            var oldKeys = viewsBefore.Select(v => v.TopKey).ToList();

            const string inserted = "Brand new opening paragraph.\n\n";
            await Refresh(view, Source.Insert(Source.IndexOf("First", StringComparison.Ordinal), inserted));

            // 选区跟着搬，选中的文字不变
            Assert.Equal(selectedBefore, view.SelectionController.GetSelectedText());
            Assert.Equal(view.Snapshot![3].Key, view.Selection.Anchor!.Value.BlockKey);
            Assert.Equal(oldKeys[2] + inserted.Length, view.Selection.Anchor!.Value.BlockKey);
            // 旧块的视图全部保留，只是源码位置变了；插入的那一块是新视图
            var viewsAfter = Blocks(view);
            Assert.Equal(viewsBefore.Count + 1, viewsAfter.Count);
            Assert.All(viewsBefore.Skip(1), v => Assert.Contains(v, viewsAfter));
            Assert.Equal(oldKeys.Skip(1).Select(k => k + inserted.Length), viewsBefore.Skip(1).Select(v => v.TopKey));
            window.Close();
        });
    }

    [Fact]
    public async Task DeletingBeforeTheSelection_ShiftsItBackToTheSameText()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[3].Key, 0, 5), new TextPoint(0, snapshot[3].Key, 0, 14));
            Assert.Equal("paragraph", view.SelectionController.GetSelectedText());

            await Refresh(view, Source.Replace("First paragraph here.\n\n", ""));

            Assert.Equal("paragraph", view.SelectionController.GetSelectedText());
            Assert.Equal(view.Snapshot![^1].Key, view.Selection.Anchor!.Value.BlockKey);
            window.Close();
        });
    }

    [Fact]
    public async Task DeletingTheSelectedBlock_FallsBackToTheNextBlock_AndClearsAnEmptiedSelection()
    {
        await HeadlessApp.Run(async () =>
        {
            // 选区完全在被删除的块里：两端退到同一点，选区清空
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[2].Key, 0, 0), new TextPoint(0, snapshot[2].Key, 0, 6));
            await Refresh(view, Source.Replace("Second one.\n\n", ""));
            Assert.True(view.Selection.IsEmpty);
            Assert.Null(view.Selection.Anchor);

            // 起点在存活的块里、终点在被删除的块里：终点退到后面第一个存活块的开头
            view.Selection.Set(new TextPoint(0, view.Snapshot![1].Key, 0, 2), new TextPoint(0, view.Snapshot![2].Key, 0, 4));
            Assert.Equal("rst paragraph here.\n\nLast", view.SelectionController.GetSelectedText());
            await Refresh(view, Source.Replace("Second one.\n\n", "").Replace("Last paragraph.\n", ""));
            Assert.False(view.Selection.IsEmpty);
            Assert.Equal("rst paragraph here.", view.SelectionController.GetSelectedText());
            window.Close();
        });
    }

    [Fact]
    public async Task EditingInsideTheSelectedBlock_ClampsTheSelectionToTheNewText()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[1].Key, 0, 6), new TextPoint(0, snapshot[1].Key, 0, 20));

            await Refresh(view, Source.Replace("First paragraph here.", "First one"));

            // 终点超出新文本：钳到段落末尾
            Assert.Equal("one", view.SelectionController.GetSelectedText());
            window.Close();
        });
    }

    [Fact]
    public async Task RapidRefreshes_NeverShowIntermediateVersions()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            const string a = "# Title A\n\nParagraph from A.\n";
            const string b = "# Title B\n\nParagraph from B.\n";
            const string c = "# Title C\n\nParagraph from C.\n";
            var original = ((HeadingBlock)view.Snapshot![0]).Content.Text;

            view.RefreshMarkdown(a);
            view.RefreshMarkdown(b);
            view.RefreshMarkdown(c);
            var seen = new HashSet<string>();
            for (var i = 0; i < 60; i++)
            {
                await HeadlessApp.Settle(1);
                seen.Add(((HeadingBlock)view.Snapshot![0]).Content.Text);
            }

            // 旧画面保持到 C 就绪；A、B 从来没有上屏
            Assert.Subset(new HashSet<string> { original, "Title C" }, seen);
            Assert.Contains("Title C", seen);
            Assert.Equal("Title C", ((HeadingBlock)view.Snapshot![0]).Content.Text);
            window.Close();
        });
    }

    [Fact]
    public async Task OldContentStaysVisibleUntilTheReplacementIsApplied()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var viewsBefore = Blocks(view);

            view.RefreshMarkdown(Source.Replace("Last paragraph.", "Other."));
            // 提交刷新的这一刻，画面没有被清空
            Assert.Equal(viewsBefore.Count, Blocks(view).Count);
            Assert.NotNull(view.Snapshot);
            window.Close();
        });
    }

    [Fact]
    public async Task AssigningMarkdown_StillStartsANewDocument()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(Source);
            var snapshot = view.Snapshot!;
            view.Selection.Set(new TextPoint(0, snapshot[1].Key, 0, 0), new TextPoint(0, snapshot[1].Key, 0, 5));
            var documentId = view.DocumentId;
            var viewsBefore = Blocks(view);

            view.Markdown = Source.Replace("Last paragraph.", "Other.");
            await HeadlessApp.Settle(10);
            view.ApplyLatest();
            await HeadlessApp.Settle();

            Assert.NotEqual(documentId, view.DocumentId);
            Assert.True(view.Selection.IsEmpty);
            Assert.All(Blocks(view), v => Assert.DoesNotContain(v, viewsBefore));
            window.Close();
        });
    }

    [Fact]
    public async Task BlockState_FollowsShiftedBlocks_AndIsDroppedForDeletedOnes()
    {
        await HeadlessApp.Run(async () =>
        {
            const string text = "Intro.\n\n```js\nlet a = 1;\n```\n\nMiddle.\n\n```js\nlet b = 2;\n```\n";
            var (window, view) = await Show(text);
            var codeViews = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(2, codeViews.Count);
            var state = codeViews[0].CurrentContext!.State;
            state.Set(codeViews[0].TopKey, "code.wrap", true);
            state.Set(codeViews[1].TopKey, "code.wrap", true);
            int firstKey = codeViews[0].TopKey, secondKey = codeViews[1].TopKey;

            // 第一个代码块之前增加文字，第二个代码块被删除
            const string prefix = "Brand new text before everything.\n\n";
            await Refresh(view, prefix + text[..text.IndexOf("Middle.", StringComparison.Ordinal)]);

            Assert.True(state.Get(firstKey + prefix.Length, "code.wrap", false));
            Assert.False(state.Get(firstKey, "code.wrap", false));
            Assert.False(state.Get(secondKey, "code.wrap", false));
            Assert.False(state.Get(secondKey + prefix.Length, "code.wrap", false));
            window.Close();
        });
    }

    [Fact]
    public void BlockStateStore_Remap_MovesDropsAndDoesNotCollide()
    {
        var store = new BlockStateStore();
        store.Set(10, "a", 1);
        store.Set(20, "a", 2);
        store.Set(30, "b", 3);
        store.Set(20, "c", 4);

        // 10 → 20 与 20 → 10 同时发生，不能互相覆盖；30 被删除
        store.Remap(key => key switch { 10 => 20, 20 => 10, _ => null });

        Assert.Equal(1, store.Get(20, "a", 0));
        Assert.Equal(2, store.Get(10, "a", 0));
        Assert.Equal(4, store.Get(10, "c", 0));
        Assert.Equal(0, store.Get(30, "b", 0));
    }

    [Fact]
    public async Task ReadingPosition_StaysPut_WhenContentIsInsertedAboveTheViewport()
    {
        await HeadlessApp.Run(async () =>
        {
            var text = string.Concat(Enumerable.Range(0, 60).Select(i => $"Paragraph number {i} " + string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 8)) + "\n\n"));
            var view = new MarkdownView { Markdown = text, Margin = new(12) };
            var scroll = new ScrollViewer { Content = view };
            var window = new Window { Width = 420, Height = 320, Content = scroll, Background = Brushes.Black };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            view.ScrollToSourceOffset(text.IndexOf("Paragraph number 40 ", StringComparison.Ordinal));
            await HeadlessApp.Settle();
            double TopOf(string prefix)
            {
                var segment = view.GetVisualDescendants().OfType<TextSegmentView>().First(s => s.Content.Text.StartsWith(prefix, StringComparison.Ordinal));
                return segment.TranslatePoint(default, scroll)!.Value.Y;
            }
            var before = TopOf("Paragraph number 40 ");
            Assert.InRange(before, -300, 400);
            var offsetBefore = scroll.Offset.Y;

            // 视口上方插入一大段：如果阅读位置不跟着走，画面会跳走
            var inserted = string.Concat(Enumerable.Repeat("An inserted paragraph that is long enough to take a few lines of the preview pane. ", 12)) + "\n\n";
            await Refresh(view, inserted + text);
            await HeadlessApp.Settle(8);

            Assert.True(scroll.Offset.Y > offsetBefore);
            Assert.InRange(TopOf("Paragraph number 40 "), before - 3, before + 3);
            window.Close();
        });
    }

    [Fact]
    public async Task RefreshMarkdown_ThrowsWhenTheContentComesFromADocument()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new MarkdownView { Document = new Core.Streaming.StreamingDocument() };
            Assert.Throws<InvalidOperationException>(() => view.RefreshMarkdown("text"));
            await Task.CompletedTask;
        });
    }
}

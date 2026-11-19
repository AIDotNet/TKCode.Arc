using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class SourceNavigationTests
{
    private const string Sample =
        "# Intro\n\nFirst **bold** [link](https://x.y) end.\n\n- one\n- two item\n\n> quote para\n\n```js\nlet a = 1;\n```\n\n# Intro\n";

    private static async Task<DocumentSnapshot> Parse(string text)
    {
        var document = new StreamingDocument();
        document.Load(text);
        await document.WhenIdle();
        return document.Latest;
    }

    [Fact]
    public async Task ResolveSourceOffset_FindsDeepestBlock()
    {
        var snapshot = await Parse(Sample);

        // 段落中间（粗体标记之内）
        var paragraph = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.IndexOf("bold", StringComparison.Ordinal))!.Value;
        Assert.Equal(SourcePrecision.Block, paragraph.Precision);
        Assert.Equal(new TextPoint(0, snapshot[1].Key, 0, 0), paragraph.Point);
        Assert.Equal(snapshot[1].Range, paragraph.Range);

        // 列表第二项的文字：命中那一项的段落（片段序号 1），而不是整个列表
        var twoItem = Sample.IndexOf("two item", StringComparison.Ordinal);
        var item = DocumentNavigation.ResolveSourceOffset(snapshot, 0, twoItem)!.Value;
        Assert.Equal(new TextPoint(0, snapshot[2].Key, 1, 0), item.Point);
        Assert.True(item.Range.Start <= twoItem && twoItem < item.Range.End);
        Assert.True(item.Range.Length < snapshot[2].Range.Length);

        // 项目符号上：同样指向第二项，区间是整个列表项
        var marker = Sample.IndexOf("- two", StringComparison.Ordinal);
        var onMarker = DocumentNavigation.ResolveSourceOffset(snapshot, 0, marker)!.Value;
        Assert.Equal(new TextPoint(0, snapshot[2].Key, 1, 0), onMarker.Point);
        Assert.Equal(marker, onMarker.Range.Start);

        // 引用里的段落
        var quote = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.IndexOf("para", StringComparison.Ordinal))!.Value;
        Assert.Equal(snapshot[3].Key, quote.Point.BlockKey);
        Assert.True(quote.Range.Length < snapshot[3].Range.Length || snapshot[3].Range == quote.Range);

        // 代码块内部：块级精度，指向整个代码块
        var code = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.IndexOf("let a", StringComparison.Ordinal))!.Value;
        Assert.Equal(SourcePrecision.Block, code.Precision);
        Assert.Equal(snapshot[4].Key, code.Point.BlockKey);
        Assert.Equal(snapshot[4].Range, code.Range);
    }

    [Fact]
    public async Task ResolveSourceOffset_GapsAndEnds_ResolveToNearestBlock()
    {
        var snapshot = await Parse(Sample);

        // 段落与列表之间的空行
        var gap = Sample.IndexOf("end.\n\n- one", StringComparison.Ordinal) + "end.\n".Length;
        var between = DocumentNavigation.ResolveSourceOffset(snapshot, 0, gap)!.Value;
        Assert.Equal(SourcePrecision.Nearest, between.Precision);
        Assert.Equal(snapshot[2].Key, between.Point.BlockKey);

        // 文档末尾（最后一个换行之后）：最后一块
        var end = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.Length)!.Value;
        Assert.Equal(SourcePrecision.Nearest, end.Precision);
        Assert.Equal(snapshot[^1].Key, end.Point.BlockKey);

        Assert.Null(DocumentNavigation.ResolveSourceOffset(snapshot, 0, -1));
        Assert.Null(DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.Length + 1));
        Assert.Null(DocumentNavigation.ResolveSourceOffset(DocumentSnapshot.Empty, 0, 0));
    }

    [Fact]
    public async Task ResolveSourceOffset_RepeatedHeadings_AreDistinguishedByOffset()
    {
        var snapshot = await Parse(Sample);
        var first = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.IndexOf("# Intro", StringComparison.Ordinal) + 3)!.Value;
        var second = DocumentNavigation.ResolveSourceOffset(snapshot, 0, Sample.LastIndexOf("# Intro", StringComparison.Ordinal) + 3)!.Value;
        Assert.NotEqual(first.Point.BlockKey, second.Point.BlockKey);
        Assert.Equal(snapshot[0].Key, first.Point.BlockKey);
        Assert.Equal(snapshot[^1].Key, second.Point.BlockKey);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ResolveSourceOffset_OffsetsStayInSourceCoordinates_ForCrlfAndCjk(string newline)
    {
        // 中文按 1 个单位、emoji（代理对）按 2 个单位；\r\n 占 2 个单位
        var text = "# 标题😀" + newline + newline + "第一段 **粗体😀** 与 [链接](https://x.y) 结尾。" + newline + newline +
                   "- 项目一" + newline + "- 项目二😀 内容" + newline + newline + "最后一段";
        var snapshot = await Parse(text);
        foreach (var needle in new[] { "标题", "粗体", "链接", "项目二", "内容", "最后一段" })
        {
            var offset = text.IndexOf(needle, StringComparison.Ordinal);
            var target = DocumentNavigation.ResolveSourceOffset(snapshot, 0, offset)!.Value;
            Assert.Equal(SourcePrecision.Block, target.Precision);
            Assert.True(target.Range.Start <= offset && offset < target.Range.End, $"{needle}: {offset} not in {target.Range}");
            Assert.Equal(target.Range, DocumentNavigation.SourceRangeOf(snapshot, target.Point));
        }
    }

    [Fact]
    public async Task SourceRangeOf_PlainTextOffsetIsNotTakenAsSourceOffset()
    {
        var snapshot = await Parse(Sample);
        var paragraph = (ParagraphBlock)snapshot[1];

        // 段落纯文本比源码短（粗体与链接的标记不在纯文本里），所以片段内偏移不能当源码偏移
        Assert.True(BlockText.PlainText(paragraph.Content).Length < paragraph.Range.Length);
        var range = DocumentNavigation.SourceRangeOf(snapshot, new TextPoint(0, paragraph.Key, 0, 3));
        Assert.Equal(paragraph.Range, range);
        Assert.Equal(paragraph.Range, DocumentNavigation.SourceRangeOf(snapshot, new TextPoint(0, paragraph.Key, 0, 12)));

        // 列表第二项的片段 → 那一项的段落，而不是整个列表
        var list = snapshot[2];
        var second = DocumentNavigation.SourceRangeOf(snapshot, new TextPoint(0, list.Key, 1, 0))!.Value;
        Assert.True(second.Length < list.Range.Length);
        Assert.Equal(Sample.IndexOf("two item", StringComparison.Ordinal), second.Start);

        Assert.Null(DocumentNavigation.SourceRangeOf(snapshot, new TextPoint(0, 99999, 0, 0)));
    }

    private static string LongDocument()
    {
        var text = new StringBuilder("# Title\n\n");
        for (var i = 0; i < 40; i++)
        {
            text.Append($"## Section {i}\n\nParagraph {i} with **bold** and [link](https://example.com) text, ")
                .Append(string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 12)))
                .Append("\n\n");
        }
        text.Append("- first item\n- second item with more words\n- third item\n\n```csharp\nvar x = 1;\nvar y = 2;\n```\n");
        return text.ToString();
    }

    private static async Task<(Window Window, ScrollViewer Scroll, MarkdownView View)> Show(string markdown, double width = 420, double height = 320)
    {
        var view = new MarkdownView { Markdown = markdown, Margin = new(12) };
        var scroll = new ScrollViewer { Content = view };
        var window = new Window { Width = width, Height = height, Content = scroll, Background = Brushes.Black };
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        return (window, scroll, view);
    }

    private static bool IsVisible(ScrollViewer scroll, MarkdownView view, string textStart) =>
        view.GetVisualDescendants().OfType<TextSegmentView>().Where(s => s.Content.Text.StartsWith(textStart, StringComparison.Ordinal))
            .Any(s => s.TranslatePoint(default, scroll) is { } p && p.Y >= -1 && p.Y < scroll.Viewport.Height);

    [Fact]
    public async Task ScrollToSourceOffset_NavigatesToParagraphsAndListItems_NotOnlyHeadings()
    {
        await HeadlessApp.Run(async () =>
        {
            var text = LongDocument();
            var view = new MarkdownView { Markdown = text, Margin = new(12) };
            var scroll = new ScrollViewer { Content = view };
            var window = new Window { Width = 420, Height = 320, Content = scroll, Background = Brushes.Black };
            window.Show();

            // 还没有应用快照
            Assert.Equal(SourceNavigationStatus.NoDocument, view.ScrollToSourceOffset(0).Status);
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var inParagraph = text.IndexOf("Paragraph 30 with", StringComparison.Ordinal) + 20;
            var result = view.ScrollToSourceOffset(inParagraph);
            Assert.Equal(SourceNavigationStatus.Navigated, result.Status);
            Assert.Equal(SourcePrecision.Block, result.Target!.Value.Precision);
            await HeadlessApp.Settle();
            Assert.True(scroll.Offset.Y > 0);
            Assert.True(IsVisible(scroll, view, "Paragraph 30"));

            // 列表第二项
            var item = view.ScrollToSourceOffset(text.IndexOf("second item", StringComparison.Ordinal) + 3);
            Assert.Equal(SourceNavigationStatus.Navigated, item.Status);
            await HeadlessApp.Settle();
            Assert.True(IsVisible(scroll, view, "second item"));

            // 代码块内部（块级）
            var code = view.ScrollToSourceOffset(text.IndexOf("var y", StringComparison.Ordinal));
            Assert.Equal(SourceNavigationStatus.Navigated, code.Status);
            Assert.Equal(view.Snapshot![^1].Range, code.Target!.Value.Range);

            Assert.Equal(SourceNavigationStatus.OutOfRange, view.ScrollToSourceOffset(-1).Status);
            Assert.Equal(SourceNavigationStatus.OutOfRange, view.ScrollToSourceOffset(text.Length + 1).Status);
            window.Close();
        });
    }

    [Fact]
    public async Task ScrollToSourceOffset_BeforeLayout_ReportsLayoutPending_WithoutScrolling()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, scroll, view) = await Show(LongDocument());
            view.Width = 300;
            var result = view.ScrollToSourceOffset(5);
            Assert.Equal(SourceNavigationStatus.LayoutPending, result.Status);
            Assert.NotNull(result.Target);
            Assert.Equal(0, scroll.Offset.Y);
            window.Close();
        });
    }

    [Fact]
    public async Task GetSourceRange_FromRenderedPointAndSelection()
    {
        await HeadlessApp.Run(async () =>
        {
            var text = Sample;
            var (window, _, view) = await Show(text);
            var snapshot = view.Snapshot!;

            Assert.Equal(snapshot[1].Range, view.GetSourceRange(new TextPoint(0, snapshot[1].Key, 0, 4)));
            Assert.Null(view.GetSourceRange(new TextPoint(0, 99999, 0, 0)));
            Assert.Null(view.GetSelectionSourceRange());

            // 选区从第一段中间到代码块中间：覆盖第一段起点到代码块终点
            view.Selection.Set(new TextPoint(0, snapshot[1].Key, 0, 2), new TextPoint(0, snapshot[4].Key, 0, 3));
            var range = view.GetSelectionSourceRange()!.Value;
            Assert.Equal(snapshot[1].Range.Start, range.Start);
            Assert.Equal(snapshot[4].Range.End, range.End);
            window.Close();
        });
    }

    [Fact]
    public async Task ReadingAnchor_SurvivesWidthChange_WithinSameDocument()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, scroll, view) = await Show(LongDocument());
            view.ScrollToSourceOffset(LongDocument().IndexOf("Paragraph 25 with", StringComparison.Ordinal));
            await HeadlessApp.Settle();
            scroll.Offset = new Vector(0, scroll.Offset.Y + 37);
            await HeadlessApp.Settle();

            var anchor = view.CaptureAnchor()!;
            Assert.NotNull(anchor);
            Assert.Equal(view.DocumentId, anchor.DocumentId);
            Assert.True(anchor.Segment >= 0);
            var before = scroll.Offset.Y;

            // 变窄后重新折行，滚动位置若不修正就会指向别处
            window.Width = 260;
            await HeadlessApp.Settle();
            Assert.Equal(AnchorRestoreStatus.Restored, view.RestoreAnchor(anchor));
            await HeadlessApp.Settle();
            Assert.NotEqual(before, scroll.Offset.Y);

            // 锚点字符仍然在可视区域顶部（相对顶边的位移保持不变）
            var segment = view.GetVisualDescendants().OfType<TextSegmentView>().First(s => s.Key.BlockKey == anchor.BlockKey && s.Key.Segment == anchor.Segment);
            var top = scroll.TranslatePoint(default, view)!.Value.Y;
            var charTop = segment.TranslatePoint(default, view)!.Value.Y + segment.GetCharacterBounds(anchor.Offset).Top;
            Assert.InRange(top - charTop, anchor.Displacement - 1.5, anchor.Displacement + 1.5);

            var after = view.CaptureAnchor()!;
            Assert.Equal((anchor.BlockKey, anchor.Segment), (after.BlockKey, after.Segment));
            window.Close();
        });
    }

    [Fact]
    public async Task ReadingAnchor_IsNotRestoredOntoADifferentDocument()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, scroll, view) = await Show(LongDocument());
            view.ScrollToSourceOffset(LongDocument().IndexOf("Paragraph 20 with", StringComparison.Ordinal));
            await HeadlessApp.Settle();
            var anchor = view.CaptureAnchor()!;

            view.Markdown = LongDocument().Replace("Paragraph", "Chapter");
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            var offset = scroll.Offset.Y;

            Assert.NotEqual(anchor.DocumentId, view.DocumentId);
            Assert.Equal(AnchorRestoreStatus.WrongDocument, view.RestoreAnchor(anchor));
            Assert.Equal(offset, scroll.Offset.Y);
            window.Close();
        });
    }

    [Fact]
    public async Task ReadingAnchor_FallsBackToNearestBlock_WhenTargetIsGone()
    {
        await HeadlessApp.Run(async () =>
        {
            var document = new StreamingDocument();
            document.Load(LongDocument());
            var view = new MarkdownView { Document = document, Margin = new(12) };
            var scroll = new ScrollViewer { Content = view };
            var window = new Window { Width = 420, Height = 320, Content = scroll, Background = Brushes.Black };
            window.Show();
            await document.WhenIdle();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            view.ScrollToSourceOffset(LongDocument().IndexOf("Paragraph 30 with", StringComparison.Ordinal));
            await HeadlessApp.Settle();
            var anchor = view.CaptureAnchor()!;

            // 外部重置文档（视图的 DocumentId 不变）：原来的块已经不存在
            document.Load("# Short\n\nOnly a little text.\n");
            await document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            Assert.Equal(AnchorRestoreStatus.Approximate, view.RestoreAnchor(anchor));
            window.Close();
        });
    }

    [Fact]
    public async Task ReadingAnchor_WithoutScrollViewer_OrBeforeLayout_ReportsWhy()
    {
        await HeadlessApp.Run(async () =>
        {
            // 没有外层 ScrollViewer
            var bare = new MarkdownView { Markdown = Sample };
            var bareWindow = new Window { Width = 400, Height = 300, Content = bare };
            bareWindow.Show();
            await HeadlessApp.Settle();
            bare.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.Null(bare.CaptureAnchor());
            Assert.Equal(AnchorRestoreStatus.NoViewport, bare.RestoreAnchor(new ReadingAnchor(bare.DocumentId, 0, 0, 0, 0)));
            bareWindow.Close();

            // 还没有快照
            var empty = new MarkdownView { Markdown = Sample };
            Assert.Equal(AnchorRestoreStatus.NoDocument, empty.RestoreAnchor(new ReadingAnchor(empty.DocumentId, 0, 0, 0, 0)));

            // 刚改过宽度、版面还没有稳定
            var (window, _, view) = await Show(LongDocument());
            var anchor = view.CaptureAnchor()!;
            view.Width = 280;
            Assert.Equal(AnchorRestoreStatus.LayoutPending, view.RestoreAnchor(anchor));
            window.Close();
        });
    }
}

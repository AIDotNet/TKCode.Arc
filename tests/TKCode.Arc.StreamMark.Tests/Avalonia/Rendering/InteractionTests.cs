using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Model;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>阶段 5：导航（目录 / 锚点 / 脚注）、查找、键盘选择、右键菜单、全屏、无障碍。</summary>
public sealed class InteractionTests
{
    private static string LongDocument()
    {
        var sb = new System.Text.StringBuilder("# Guide\n\nIntro with a footnote[^note] and a [jump](#deep-section).\n\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append("## Section ").Append(i).Append("\n\nParagraph ").Append(i).Append(" talks about needle").Append(i % 7 == 0 ? " needle" : "").Append(".\n\n");
        }
        sb.Append("## Deep Section\n\nThe deep target.\n\n[^note]: The footnote definition.\n");
        return sb.ToString();
    }

    private static async Task<(Window Window, TranscriptView View)> Transcript(params string[] messages)
    {
        var view = new TranscriptView();
        var window = new Window { Width = 800, Height = 500, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
        window.Show();
        foreach (var message in messages)
        {
            view.Entries.Add(MarkdownEntry.FromTextDeferred(message));
        }
        await HeadlessApp.Settle();
        return (window, view);
    }

    private static bool IsVisible(TranscriptView view, Func<TextSegmentView, bool> match) =>
        view.Panel.GetVisualDescendants().OfType<TextSegmentView>().Where(match).Any(s =>
            s.TranslatePoint(default, view.Panel) is { } p && p.Y >= -1 && p.Y < view.Panel.Bounds.Height);

    [Fact]
    public async Task Outline_And_Anchors_ScrollToTargets()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Transcript(LongDocument());
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();

            var outline = view.GetOutline();
            Assert.Equal(62, outline.Count);
            Assert.Equal((1, "Guide"), (outline[0].Level, outline[0].Text));
            Assert.Equal("deep-section", outline[^1].Id);

            // 目录跳转
            view.ScrollToPoint(outline[40].Target);
            await HeadlessApp.Settle();
            Assert.True(IsVisible(view, s => s.Content.Text == "Section 39"));

            // 文内锚点与脚注
            var snapshot = ((MarkdownEntry)view.Entries[0]).Document.Latest;
            var deep = DocumentNavigation.ResolveAnchor(snapshot, 0, "#deep-section");
            Assert.NotNull(deep);
            view.ScrollToPoint(deep!.Value);
            await HeadlessApp.Settle();
            Assert.True(IsVisible(view, s => s.Content.Text == "Deep Section"));

            // 正文中脚注引用的真实链接地址
            var reference = ((ParagraphBlock)snapshot.Blocks.ElementAt(1)).Content.Links.First(l => l.Url.StartsWith("#fn:", StringComparison.Ordinal)).Url;
            Assert.NotNull(DocumentNavigation.ResolveAnchor(snapshot, 0, "#fn:note"));
            var footnote = DocumentNavigation.ResolveAnchor(snapshot, 0, reference);
            Assert.NotNull(footnote);
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();
            view.ScrollToPoint(footnote!.Value);
            var afterCall = view.Panel.Offset.Y;
            await HeadlessApp.Settle();
            Assert.True(afterCall > 0, $"after call {afterCall}, target {footnote}");
            var segs = view.Panel.GetVisualDescendants().OfType<TextSegmentView>().Select(x => $"{x.Content.Text[..Math.Min(20, x.Content.Text.Length)]}@{x.TranslatePoint(default, view.Panel)?.Y:F0}").ToList();
            Assert.True(IsVisible(view, s => s.Content.Text.Contains("The footnote definition", StringComparison.Ordinal)),
                $"offset {view.Panel.Offset.Y} extent {view.Panel.Extent.Height} viewport {view.Panel.Viewport.Height}: {string.Join(" | ", segs)}");
            Assert.Null(DocumentNavigation.ResolveAnchor(snapshot, 0, "#missing"));
            window.Close();
        });
    }

    [Fact]
    public async Task Search_FindsAcrossEntries_HighlightsAndScrolls()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Transcript("First message with NEEDLE.", LongDocument(), "```cs\nvar needle = 1;\n```");
            var search = view.Search;
            // 1 + 60 段落中的 needle（其中 9 段有两个）+ 代码中的 1 个
            var count = search.Find("needle");
            Assert.Equal(1 + 60 + 9 + 1, count);
            Assert.Equal(0, search.CurrentIndex);
            Assert.Equal(1 + 60 + 9 + 1 - 1, search.Find("needle", matchCase: true));

            search.Find("needle");
            search.MoveTo(40);
            await HeadlessApp.Settle();
            var target = search.Matches[40].Start;
            var segment = view.Panel.GetVisualDescendants().OfType<TextSegmentView>().Single(s => s.Key == new SegmentKey(target.Entry, target.BlockKey, target.Segment));
            Assert.Contains(view.Selection.GetHighlights(segment.Key, segment.Content.Text.Length), h => h.Current);
            HeadlessApp.Save(window, "interaction-search");

            search.Previous();
            Assert.Equal(39, search.CurrentIndex);
            search.MoveTo(search.Count - 1);
            search.Next();
            Assert.Equal(0, search.CurrentIndex);
            search.Clear();
            Assert.Equal(0, search.Count);
            Assert.Empty(view.Selection.GetHighlights(segment.Key, segment.Content.Text.Length));
            window.Close();
        });
    }

    [Fact]
    public async Task Keyboard_ExtendsSelection_AcrossSegments()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new MarkdownView();
            view.Markdown = "Hello world\n\nSecond para";
            var window = new Window { Width = 600, Height = 300, Content = view };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            var blocks = view.Snapshot!.Blocks.ToList();
            var start = new TextPoint(0, blocks[0].Key, 0, 6);
            view.Selection.Set(start, start);
            var controller = view.SelectionController;
            Assert.True(controller.ExtendByKey(Key.End));
            Assert.Equal("world", controller.GetSelectedText());
            Assert.True(controller.ExtendByKey(Key.Right));
            Assert.Equal(blocks[1].Key, view.Selection.Active!.Value.BlockKey);
            controller.ExtendByKey(Key.Right, KeyModifiers.Shift | KeyModifiers.Alt);
            Assert.Equal("world\n\nSecond", controller.GetSelectedText());
            controller.ExtendByKey(Key.Up);
            Assert.Equal(blocks[0].Key, view.Selection.Active!.Value.BlockKey);
            controller.ExtendByKey(Key.Home);
            Assert.Equal("Hello ", controller.GetSelectedText());
            window.Close();
        });
    }

    [Fact]
    public async Task ContextMenu_And_Fullscreen_And_Automation()
    {
        await HeadlessApp.Run(async () =>
        {
            var rows = string.Concat(Enumerable.Range(0, 12).Select(i => $"| item {i} | value {i} | description of item {i} |\n"));
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, new MarkdownServices
            {
                MenuText = command => command switch
                {
                    MarkdownMenuCommand.Copy => "复制",
                    MarkdownMenuCommand.CopyAsMarkdown => "复制为 Markdown",
                    MarkdownMenuCommand.SelectAll => "全选",
                    _ => "查找…",
                },
            });
            view.Markdown = "# Title\n\nSome text.\n\n| A | B | C |\n|---|---|---|\n" + rows + "\n```cs\nvar x = 1;\n```";
            var window = new Window { Width = 760, Height = 700, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            // 右键菜单：按宿主文字生成，没有选区时复制不可用
            var flyout = Assert.IsType<MenuFlyout>(view.ContextFlyout);
            flyout.ShowAt(view);
            await HeadlessApp.Settle();
            var items = flyout.Items.OfType<MenuItem>().ToList();
            Assert.Equal(["复制", "复制为 Markdown", "全选", "查找…"], items.Select(i => (string)i.Header!));
            Assert.False(items[0].IsEnabled);
            var findRequested = false;
            view.AddHandler(MarkdownContextMenu.FindRequestedEvent, (_, _) => findRequested = true);
            items[3].RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(findRequested);
            flyout.Hide();

            // 表格全屏
            var table = view.GetVisualDescendants().OfType<TableBlockView>().Single();
            var overlay = table.OpenFullscreen();
            Assert.NotNull(overlay);
            await HeadlessApp.Settle();
            Assert.Contains(OverlayLayer.GetOverlayLayer(view)!.Children, c => c is BlockOverlay);
            HeadlessApp.Save(window, "interaction-table-fullscreen");
            overlay!.Close();

            // 无障碍：文档 → 标题（级别 1）、段落文本、表格、代码块按钮可 Invoke
            var peer = ControlAutomationPeer.CreatePeerForElement(view);
            Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
            var all = Flatten(peer).ToList();
            var title = all.Single(p => p.GetName() == "Title");
            Assert.Equal(AutomationControlType.Text, title.GetAutomationControlType());
            Assert.Equal(1, title.GetHeadingLevel());
            Assert.Contains(all, p => p.GetName() == "Some text." && p.GetAutomationControlType() == AutomationControlType.Text);
            Assert.Contains(all, p => p.GetAutomationControlType() == AutomationControlType.Table);
            var copy = all.First(p => p.GetAutomationControlType() == AutomationControlType.Button && p.GetName() == "Copy");
            Assert.NotNull(copy.GetProvider<IInvokeProvider>());
            Assert.Contains(all, p => p.GetName() == "var x = 1;");
            window.Close();
        });
    }

    [Fact]
    public async Task TableActions_SitOutsideTheGrid()
    {
        await HeadlessApp.Run(async () =>
        {
            var markdown = """
                | 运行/轮次 | 日志打印 y_found | 实际参与计算 |
                |---|---|---|
                | 66/1 | 89.0287 | 88.7778 |
                | 66/2 | 89.7578 | 89.7613 |
                """;
            var view = new MarkdownView { Markdown = markdown, Margin = new(16) };
            var window = new Window { Width = 420, Height = 280, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var table = view.GetVisualDescendants().OfType<TableBlockView>().Single();
            var cells = table.GetVisualChildren().OfType<TextSegmentView>().ToList();
            var copy = table.GetVisualChildren().OfType<IconButton>().Single(b => AutomationProperties.GetName(b) == "Copy");
            var fullscreen = table.GetVisualChildren().OfType<IconButton>().Single(b => AutomationProperties.GetName(b) == "Fullscreen");
            Assert.NotEmpty(cells);
            var gridTop = cells.Min(c => c.Bounds.Top);
            // 复制 / 全屏在边框上方的操作条里，不与任何单元格重叠（issue：按钮在表头里会被文字盖住）
            Assert.True(copy.Bounds.Bottom <= gridTop + 0.5);
            Assert.True(fullscreen.Bounds.Bottom <= gridTop + 0.5);
            Assert.All(cells, cell =>
            {
                Assert.False(cell.Bounds.Intersects(copy.Bounds));
                Assert.False(cell.Bounds.Intersects(fullscreen.Bounds));
            });
            window.Close();
        });
    }

    private static IEnumerable<AutomationPeer> Flatten(AutomationPeer peer)
    {
        yield return peer;
        foreach (var child in peer.GetChildren())
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }
}

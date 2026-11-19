using Avalonia.Controls;
using Avalonia.Media;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class SelectionAndFadeTests
{
    [Fact]
    public async Task ProgrammaticSelection_AcrossBlocks_IsPaintedAndCopied()
    {
        await HeadlessApp.Run(async () =>
        {
            const string markdown = "# Heading\n\nFirst paragraph with **bold** text.\n\n- item one\n- item two\n\n```js\nconst x = 1;\nconsole.log(x);\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |";
            var view = new MarkdownView { Markdown = markdown, Margin = new(16) };
            var window = new Window { Width = 640, Height = 520, Content = view, Background = Brushes.Black };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var snapshot = view.Snapshot!;
            // 从第一段第 6 个字符选到代码块第 5 个字符
            var paragraph = snapshot[1];
            var code = snapshot[3];
            view.Selection.Set(new TextPoint(0, paragraph.Key, 0, 6), new TextPoint(0, code.Key, 0, 5));
            await HeadlessApp.Settle();
            var text = view.SelectionController.GetSelectedText();
            Assert.Equal("paragraph with bold text.\n\n- item one\n- item two\n\nconst", text);
            HeadlessApp.Save(window, "selection-across-blocks");

            // 全选覆盖到表格最后一个单元格
            view.SelectionController.SelectAll();
            var all = view.SelectionController.GetSelectedText();
            Assert.StartsWith("Heading", all);
            Assert.EndsWith("1\t2", all);
            Assert.StartsWith("# Heading", view.SelectionController.GetSelectedMarkdown());
            window.Close();
        });
    }

    [Fact]
    public async Task StreamingTail_FadesIn_AndShowsCaret()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.FromSeconds(10));
            var view = new MarkdownView { Stream = stream, Margin = new(16) };
            var window = new Window { Width = 640, Height = 200, Content = view, Background = Brushes.Black };
            window.Show();
            stream.Push("The quick brown fox jumps over the lazy dog. ");
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            stream.Push("Newest words are still fading in");
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            Assert.True(stream.State.IsActive);
            var (count, _, newest) = stream.State.GetFadingTail(StreamClock.Now);
            Assert.True(count > 0);
            Assert.True(newest < 1);
            HeadlessApp.Save(window, "stream-fade-caret");

            stream.Complete();
            await HeadlessApp.Settle();
            Assert.False(stream.State.IsActive);
            await stream.Document.WhenIdle();
            Assert.True(stream.Document.Latest.IsComplete);
            window.Close();
        });
    }
}

using Avalonia.Controls;
using Avalonia.Media;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class MarkdownViewSnapshotTests
{
    private static async Task<(Window Window, MarkdownView View)> Show(string markdown, double width = 820, double height = 2600)
    {
        var view = new MarkdownView { Markdown = markdown, Margin = new(24) };
        var window = new Window { Width = width, Height = height, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        return (window, view);
    }

    [Fact]
    public async Task MixedDocument_RendersAllBlockKinds()
    {
        await HeadlessApp.Run(async () =>
        {
            var (window, view) = await Show(HeadlessApp.Fixture("chat-mixed.md"));
            var snapshot = view.Snapshot;
            Assert.NotNull(snapshot);
            Assert.True(snapshot.IsComplete);
            Assert.Equal(snapshot.Count, view.GetVisualChildren().Count());
            Assert.Contains(view.GetVisualChildren(), c => c is CodeBlockView);
            Assert.Contains(view.GetVisualChildren(), c => c is TableBlockView);
            Assert.Contains(view.GetVisualChildren(), c => c is ListBlockView);
            Assert.Contains(view.GetVisualChildren(), c => c is AlertBlockView);
            Assert.True(view.Bounds.Height > 600);
            HeadlessApp.Save(window, "markdown-mixed");
            window.Close();
        });
    }

    [Fact]
    public async Task Streaming_UpdatesOnlyTail_AndReusesFrozenViews()
    {
        await HeadlessApp.Run(async () =>
        {
            var document = new StreamingDocument();
            var view = new MarkdownView { Document = document, Margin = new(24) };
            var window = new Window { Width = 820, Height = 900, Content = view };
            window.Show();
            var text = HeadlessApp.Fixture("code-heavy.md");
            Control? firstView = null;
            for (var i = 0; i < text.Length; i += 9)
            {
                document.Append(text.AsSpan(i, Math.Min(9, text.Length - i)));
                await document.WhenIdle();
                view.ApplyLatest();
                firstView ??= view.GetVisualChildren().OfType<Control>().FirstOrDefault();
            }
            await HeadlessApp.Settle();
            // 第一段在流式全过程中只创建一次
            Assert.Same(firstView, view.GetVisualChildren().First());
            document.Complete();
            await document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.Same(firstView, view.GetVisualChildren().First());
            HeadlessApp.Save(window, "markdown-streamed-code");
            window.Close();
        });
    }
}

internal static class VisualExtensions
{
    public static IEnumerable<global::Avalonia.Visual> GetVisualChildren(this global::Avalonia.Visual visual) =>
        global::Avalonia.VisualTree.VisualExtensions.GetVisualChildren(visual);
}

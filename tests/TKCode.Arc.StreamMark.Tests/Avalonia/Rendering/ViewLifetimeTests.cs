using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>比视图活得久的流或文档（应用保留的聊天消息）不能留住已经离开的视图；视图回来后照常显示与跟随。</summary>
public sealed class ViewLifetimeTests
{
    [Fact]
    public async Task A_View_Is_Collected_While_Its_Stream_Lives_On()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false);
            stream.Push("Hello **world**");
            await stream.Document.WhenIdle();
            var weak = await ShowAndClose(stream);
            for (var i = 0; i < 6 && weak.IsAlive; i++)
            {
                await HeadlessApp.Settle(1);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.False(weak.IsAlive);
            // 没有视图时流照常工作
            stream.Push(" and more");
            await stream.Document.WhenIdle();
        });
    }

    [Fact]
    public async Task A_View_Put_Back_Shows_What_Arrived_While_It_Was_Away()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false);
            stream.Push("First paragraph.");
            var view = new MarkdownView { Stream = stream, Width = 360 };
            var host = new Border { Child = view };
            var window = new Window { Width = 400, Height = 300, Content = host };
            window.Show();
            await stream.Document.WhenIdle();
            Assert.True(await Shows(view, "First paragraph."));

            host.Child = null;
            stream.Push("\n\nSecond paragraph.");
            await stream.Document.WhenIdle();
            host.Child = view;
            Assert.True(await Shows(view, "Second paragraph."));

            // 回来后继续跟随
            stream.Push("\n\nThird paragraph.");
            await stream.Document.WhenIdle();
            Assert.True(await Shows(view, "Third paragraph."));
            window.Close();
        });
    }

    /// <summary>视图在若干帧内显示出 <paramref name="text"/>（快照在下一帧应用，排版再晚一帧）。</summary>
    private static async Task<bool> Shows(MarkdownView view, string text)
    {
        for (var i = 0; i < 40; i++)
        {
            if (Text(view).Contains(text, StringComparison.Ordinal))
            {
                return true;
            }
            await HeadlessApp.Settle(1);
        }
        return false;
    }

    private static string Text(MarkdownView view) =>
        string.Concat(((Selection.ISelectionHost)view).RealizedSegments().Select(s => s.Text));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ShowAndClose(MarkdownStream stream)
    {
        var view = new MarkdownView { Stream = stream, Width = 360 };
        var window = new Window { Width = 400, Height = 300, Content = view };
        window.Show();
        view.ApplyLatest();
        await HeadlessApp.Settle(1);
        window.Content = null;
        window.Close();
        await HeadlessApp.Settle(1);
        return new WeakReference(view);
    }

    [Fact]
    public async Task A_TranscriptView_Is_Collected_While_Its_Entries_And_Stream_Live_On()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false);
            stream.Push("Hello **transcript**\n\n```csharp\nvar x = 1;\n```");
            await stream.Document.WhenIdle();
            var entries = new ObservableCollection<TranscriptEntry> { new MarkdownEntry(stream) };
            var weak = await ShowAndCloseTranscript(entries);
            for (var i = 0; i < 6 && weak.IsAlive; i++)
            {
                await HeadlessApp.Settle(1);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.False(weak.IsAlive);
            stream.Push("\n\nMore text after close.");
            await stream.Document.WhenIdle();
            GC.KeepAlive(entries);
        });
    }

    [Fact]
    public async Task Detaching_Releases_TextSegment_Layouts_And_Reattaching_Rebuilds_Them()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new MarkdownView
            {
                Markdown = "Paragraph one.\n\n```csharp\nvar answer = 42;\n```\n\n```diff\n-old\n+new\n```",
                Width = 420
            };
            var host = new Border { Child = view };
            var window = new Window { Width = 460, Height = 400, Content = host };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle(2);

            var segment = view.GetVisualDescendants().OfType<TextSegmentView>().First();
            Assert.NotNull(segment.Layout);

            host.Child = null;
            await HeadlessApp.Settle(1);
            Assert.Null(segment.Layout);

            host.Child = view;
            await HeadlessApp.Settle(2);
            var reattached = view.GetVisualDescendants().OfType<TextSegmentView>().First();
            Assert.NotNull(reattached.Layout);
            Assert.True(reattached.Bounds.Height > 0);
            window.Close();
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ShowAndCloseTranscript(ObservableCollection<TranscriptEntry> entries)
    {
        var panel = new TranscriptPanel { Width = 400, Height = 300 };
        panel.SetEntries(entries);
        var window = new Window { Width = 420, Height = 320, Content = panel };
        window.Show();
        panel.ApplyLatest();
        await HeadlessApp.Settle(2);
        window.Content = null;
        window.Close();
        await HeadlessApp.Settle(1);
        return new WeakReference(panel);
    }
}

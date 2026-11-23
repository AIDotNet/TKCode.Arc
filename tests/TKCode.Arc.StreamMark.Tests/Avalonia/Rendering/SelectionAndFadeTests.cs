using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
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

    [Fact]
    public async Task ShowCaret_DefaultsToTrue_AndCanBeDisabledWithoutAffectingPacingOrFade()
    {
        await HeadlessApp.Run(async () =>
        {
            var defaultStream = new MarkdownStream();
            Assert.True(defaultStream.ShowCaret);
            Assert.True(defaultStream.State.ShowCaret);

            var services = new MarkdownServices();
            var serviceStream = services.CreateStream();
            Assert.True(serviceStream.ShowCaret);
            Assert.True(serviceStream.State.ShowCaret);

            serviceStream.ShowCaret = false;
            Assert.False(serviceStream.ShowCaret);
            Assert.False(serviceStream.State.ShowCaret);

            var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.FromSeconds(10)) { ShowCaret = false };
            Assert.False(stream.ShowCaret);
            Assert.False(stream.State.ShowCaret);

            stream.Push("Hello fading world");
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();

            Assert.True(stream.State.IsActive);
            var (count, _, newest) = stream.State.GetFadingTail(StreamClock.Now);
            Assert.True(count > 0);
            Assert.True(newest < 1);

            stream.Complete();
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            Assert.False(stream.State.IsActive);
            Assert.True(stream.Document.Latest.IsComplete);
        });
    }

    [Fact]
    public async Task ShowCaret_ControlsRenderedCaret_AndTogglesMidStream()
    {
        await HeadlessApp.Run(async () =>
        {
            var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.Zero) { ShowCaret = false };
            var view = new MarkdownView { Stream = stream, Margin = new(16) };
            var window = new Window { Width = 640, Height = 200, Content = view, Background = Brushes.Black };
            window.Show();

            stream.Push("Streaming caret probe");
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            Assert.True(stream.State.IsActive);
            var tail = ((ISelectionHost)view).RealizedSegments().OfType<TextSegmentView>().Single(s => s.IsDocumentTail);
            var caretRect = GetCaretProbeRect(tail, window);

            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.Equal(0, CountNonBlackPixels(frame, caretRect));
            }

            // 流式期间在 UI 线程打开光标，无需再次 Push
            stream.ShowCaret = true;
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.True(CountNonBlackPixels(frame, caretRect) > 0);
            }

            // 流式期间在后台线程关闭光标，无需再次 Push
            await Task.Run(() => stream.ShowCaret = false);
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.Equal(0, CountNonBlackPixels(frame, caretRect));
            }

            // 再次打开并在流结束时自动消失
            stream.ShowCaret = true;
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.True(CountNonBlackPixels(frame, caretRect) > 0);
            }

            stream.Complete();
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.Equal(0, CountNonBlackPixels(frame, caretRect));
            }

            window.Close();
        });
    }

    [Fact]
    public async Task ShowCaret_DetachedView_DoesNotLeak_AndReattachTracksChanges()
    {
        await HeadlessApp.Run(async () =>
        {
            // 1. 离开可视树后不泄漏 MarkdownView 与尾部 TextSegmentView
            var leakStream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.Zero);
            leakStream.Push("Hello caret lifetime");
            await HeadlessApp.Settle();
            await leakStream.Document.WhenIdle();

            var (weakView, weakSegment) = await ShowAndDetachCaretView(leakStream);
            leakStream.ShowCaret = false;
            leakStream.ShowCaret = true;

            for (var i = 0; i < 6 && (weakView.IsAlive || weakSegment.IsAlive); i++)
            {
                await HeadlessApp.Settle(1);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.False(weakView.IsAlive);
            Assert.False(weakSegment.IsAlive);

            // 2. 脱离可视树期间切换 ShowCaret，重新挂回后状态一致且继续响应后续切换
            var stream = new MarkdownStream(smooth: false, fadeDuration: TimeSpan.Zero);
            var view = new MarkdownView { Stream = stream, Margin = new(16) };
            var host = new Border { Child = view };
            var window = new Window { Width = 640, Height = 200, Content = host, Background = Brushes.Black };
            window.Show();

            stream.Push("Reattach caret probe");
            await HeadlessApp.Settle();
            await stream.Document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var tail = ((ISelectionHost)view).RealizedSegments().OfType<TextSegmentView>().Single(s => s.IsDocumentTail);
            var caretRect = GetCaretProbeRect(tail, window);
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.True(CountNonBlackPixels(frame, caretRect) > 0);
            }

            host.Child = null;
            await HeadlessApp.Settle(1);
            stream.ShowCaret = false;

            host.Child = view;
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.Equal(0, CountNonBlackPixels(frame, caretRect));
            }

            stream.ShowCaret = true;
            await HeadlessApp.Settle();
            using (var frame = HeadlessApp.Capture(window))
            {
                Assert.True(CountNonBlackPixels(frame, caretRect) > 0);
            }

            window.Close();
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference View, WeakReference TailSegment)> ShowAndDetachCaretView(MarkdownStream stream)
    {
        var view = new MarkdownView { Stream = stream, Width = 360 };
        var window = new Window { Width = 400, Height = 200, Content = view };
        window.Show();
        view.ApplyLatest();
        await HeadlessApp.Settle(1);
        var tail = ((ISelectionHost)view).RealizedSegments().OfType<TextSegmentView>().Single(s => s.IsDocumentTail);
        var result = (new WeakReference(view), new WeakReference(tail));
        window.Content = null;
        window.Close();
        await HeadlessApp.Settle(1);
        return result;
    }

    private static PixelRect GetCaretProbeRect(TextSegmentView tail, Window window)
    {
        var end = tail.GetCharacterBounds(tail.Content.Text.Length);
        var origin = tail.TranslatePoint(default, window)!.Value;
        var scale = window.RenderScaling;
        // TextSegmentView.Render 在 end.Right + 2 处画宽 >= 2、高 <= fontSize * 1.25 的块状光标
        var x = (int)Math.Ceiling((origin.X + end.Right + 2.5) * scale);
        var y = (int)Math.Ceiling((origin.Y + end.Top + 2) * scale);
        var width = Math.Max(2, (int)Math.Floor(4 * scale));
        var height = Math.Max(2, (int)Math.Floor((end.Height - 4) * scale));
        return new PixelRect(x, y, width, height);
    }

    private static int CountNonBlackPixels(Bitmap bitmap, PixelRect region)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        var count = 0;
        var left = Math.Clamp(region.X, 0, size.Width);
        var right = Math.Clamp(region.Right, 0, size.Width);
        var top = Math.Clamp(region.Y, 0, size.Height);
        var bottom = Math.Clamp(region.Bottom, 0, size.Height);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var i = y * stride + x * 4;
                if (buffer[i] > 16 || buffer[i + 1] > 16 || buffer[i + 2] > 16)
                {
                    count++;
                }
            }
        }
        return count;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class InteractionRegressionTests
{
    // 回归：覆盖层第一次使用前是 0×0，查看器不能固定为打开时的尺寸；窗口变化时跟随
    [Fact]
    public async Task ImageViewer_FillsOverlay_AndFollowsResize()
    {
        await HeadlessApp.Run(async () =>
        {
            var anchor = new Border();
            var window = new Window { Width = 800, Height = 600, Content = anchor };
            window.Show();
            await HeadlessApp.Settle();
            var image = new WriteableBitmap(new PixelSize(40, 30), new Vector(96, 96));
            var viewer = ImageViewer.Show(anchor, image, "caption");
            Assert.NotNull(viewer);
            await HeadlessApp.Settle();
            Assert.Equal(800, viewer!.Bounds.Width, 1);
            Assert.Equal(600, viewer.Bounds.Height, 1);

            window.Width = 1000;
            window.Height = 700;
            await HeadlessApp.Settle();
            Assert.Equal(1000, viewer.Bounds.Width, 1);
            Assert.Equal(700, viewer.Bounds.Height, 1);
            viewer.Close();
            Assert.DoesNotContain(OverlayLayer.GetOverlayLayer(anchor)!.Children, c => c is ImageViewer);
            window.Close();
        });
    }

    // 回归：按在会话中宿主内容（ContentEntry）的控件上时不开始选择，不抢占指针
    [Fact]
    public async Task Selection_IgnoresPressesOnHostContent()
    {
        await HeadlessApp.Run(async () =>
        {
            var clickable = new Border { Width = 120, Height = 40, Background = Brushes.SteelBlue, Child = new TextBlock { Text = "card" } };
            var view = new TranscriptView();
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            var entry = MarkdownEntry.FromText("Some **markdown** text.");
            view.Entries.Add(entry);
            view.Entries.Add(new ContentEntry(clickable));
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();

            var controller = view.Panel.SelectionController;
            Assert.True(controller.IsHostContent((TextBlock)clickable.Child!));
            Assert.True(controller.IsHostContent(clickable));
            var segment = view.Panel.GetVisualDescendants().OfType<TextSegmentView>().Single();
            Assert.False(controller.IsHostContent(segment));
            Assert.False(controller.IsHostContent(view.Panel));
            window.Close();
        });
    }
}

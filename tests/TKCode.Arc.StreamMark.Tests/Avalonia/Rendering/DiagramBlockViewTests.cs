using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>图表块：传给渲染器的颜色要叠到真实底色上、缓存键含颜色、高图只按宽度适配。</summary>
public sealed class DiagramBlockViewTests
{
    private const string Fence = "```mermaid\nflowchart TD\n  A-->B\n```\n";

    /// <summary>记录收到的渲染环境，返回指定大小的假图表。</summary>
    private sealed class RecordingRenderer(Size size) : IDiagramRenderer
    {
        private readonly List<DiagramRenderContext> _contexts = [];

        public IReadOnlyList<DiagramRenderContext> Contexts
        {
            get
            {
                lock (_contexts)
                {
                    return [.. _contexts];
                }
            }
        }

        public bool CanRender(string? language) => language == "mermaid";

        public Task<DiagramResult?> RenderAsync(string language, string code, DiagramRenderContext context, CancellationToken cancellationToken)
        {
            lock (_contexts)
            {
                _contexts.Add(context);
            }

            return Task.FromResult<DiagramResult?>(new FakeDiagram(size));
        }
    }

    private sealed class FakeDiagram(Size size) : DiagramResult
    {
        public override Size Size => size;

        public override void Draw(DrawingContext context, Rect destination) =>
            context.DrawRectangle(Brushes.SteelBlue, null, destination);
    }

    private static async Task<(Window Window, DiagramBlockView Block)> Show(RecordingRenderer renderer, Action<Window>? configure = null, Control? host = null)
    {
        var view = new MarkdownView();
        MarkdownServices.SetServices(view, new MarkdownServices { Diagrams = [renderer] });
        view.Markdown = Fence;
        Control content = view;
        if (host is Border border)
        {
            border.Child = view;
            content = border;
        }

        var window = new Window
        {
            Width = 1000,
            Height = 600,
            Content = content,
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
        };
        configure?.Invoke(window);
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        var block = await WaitFor(window, b => b.IsShowingDiagram);
        return (window, block);
    }

    private static async Task<DiagramBlockView> WaitFor(Window window, Func<DiagramBlockView, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await HeadlessApp.Settle(2);
            if (window.GetVisualDescendants().OfType<DiagramBlockView>().FirstOrDefault(condition) is { } block)
            {
                return block;
            }
        }

        throw new TimeoutException("diagram block did not reach the expected state");
    }

    [Fact]
    public async Task SemiTransparentThemeColors_AreFlattenedOntoTheRealBackdrop()
    {
        await HeadlessApp.Run(async () =>
        {
            var renderer = new RecordingRenderer(new Size(400, 200));
            // 暗色主题的典型取值：文字 85% 的白、代码底 4% 的白；窗口底 #141414，中间还有一层 5% 的白卡片
            var foreground = Color.FromArgb(0xD8, 0xF0, 0xF0, 0xF0);
            var codeBackground = Color.FromArgb(0x0A, 0xF0, 0xF0, 0xF0);
            var card = Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF);
            var (window, _) = await Show(renderer,
                w =>
                {
                    w.Resources[MarkdownResourceKeys.Foreground] = new SolidColorBrush(foreground);
                    w.Resources[MarkdownResourceKeys.CodeBackground] = new SolidColorBrush(codeBackground);
                },
                new Border { Background = new SolidColorBrush(card) });

            var context = Assert.Single(renderer.Contexts);
            var backdrop = DiagramRenderContext.Flatten(card, Color.FromRgb(0x14, 0x14, 0x14));
            var surface = DiagramRenderContext.Flatten(codeBackground, backdrop);
            Assert.Equal(surface, context.Background);
            Assert.Equal(DiagramRenderContext.Flatten(foreground, surface), context.Foreground);
            Assert.Equal(byte.MaxValue, context.Background.A);
            Assert.Equal(byte.MaxValue, context.Foreground.A);
            Assert.Equal(byte.MaxValue, context.Accent.A);
            // 丢掉透明度时前景和背景都是 #F0F0F0
            Assert.NotEqual(context.Background, context.Foreground);
            Assert.True(Mermaid.MermaidThemeTests.Contrast(context.Background, context.Foreground) > 7);
            window.Close();
        });
    }

    [Fact]
    public async Task ThemeColorChange_RendersAgain_NotServedFromTheCache()
    {
        await HeadlessApp.Run(async () =>
        {
            var renderer = new RecordingRenderer(new Size(400, 200));
            var (window, _) = await Show(renderer, w => w.Resources[MarkdownResourceKeys.LinkForeground] = new SolidColorBrush(Color.FromRgb(0x11, 0x88, 0xFF)));
            Assert.Single(renderer.Contexts);

            window.Resources[MarkdownResourceKeys.LinkForeground] = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x11));
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (renderer.Contexts.Count < 2 && DateTime.UtcNow < deadline)
            {
                await HeadlessApp.Settle(2);
            }

            Assert.Equal(2, renderer.Contexts.Count);
            Assert.NotEqual(renderer.Contexts[0].Accent, renderer.Contexts[1].Accent);
            window.Close();
        });
    }

    [Theory]
    [InlineData(800, 4000, 4000)] // 窄而高：原来被压到 900 高，现在保持自然大小
    [InlineData(2000, 400, 194.4)] // 宽图：按宽度适配（972 / 2000）
    [InlineData(800, 20000, 6000)] // 极端高图：只受 6000 的安全上限约束
    public async Task DiagramHeight_FollowsWidthFit_NotAFixedCap(double width, double height, double expectedDiagramHeight)
    {
        await HeadlessApp.Run(async () =>
        {
            var renderer = new RecordingRenderer(new Size(width, height));
            var (window, block) = await Show(renderer);

            // 头部 34 + 图表 + 底部内边距 14（布局取整最多差 1）
            Assert.Equal(34 + expectedDiagramHeight + 14, block.DesiredSize.Height, 1.0);
            window.Close();
        });
    }
}

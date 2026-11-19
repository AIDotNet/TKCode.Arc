using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Images;
using StreamMark.Avalonia.Rendering;
using StreamMark.Mermaid;
using Xunit;

namespace StreamMark.Avalonia.Tests.Images;

public sealed class ImageRenderingTests : IDisposable
{
    private const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="40" viewBox="0 0 120 40">
          <rect width="120" height="40" rx="6" fill="#1677ff"/>
          <circle cx="20" cy="20" r="12" fill="#ffffff"/>
        </svg>
        """;

    // 与用户反馈的内容一致：工作区相对路径、表格里的图片、SVG
    private const string Markdown = """
        ## 项目标识

        ![AtomUI Logo](resources/images/readme/ATOMUI.png)

        | QQ | 微信 |
        |:---:|:---:|
        | ![QQ 群](resources/images/readme/atomui-qq.png) | ![微信群](resources/images/readme/atomui-wechat.png) |

        ![Ant Design](resources/images/readme/AntDesign.svg)

        ![缺失](resources/images/readme/missing.png)

        ![工作区外](../outside.png)
        """;

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "streammark-img-" + Guid.NewGuid().ToString("N"));

    public ImageRenderingTests()
    {
        var dir = Path.Combine(_workspace, "resources", "images", "readme");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "ATOMUI.png"), TestPng.Create(320, 120, 22, 119, 255));
        File.WriteAllBytes(Path.Combine(dir, "atomui-qq.png"), TestPng.Create(96, 96, 40, 180, 90));
        File.WriteAllBytes(Path.Combine(dir, "atomui-wechat.png"), TestPng.Create(96, 96, 200, 160, 40));
        File.WriteAllText(Path.Combine(dir, "AntDesign.svg"), Svg);
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "outside.png"), TestPng.Create(8, 8));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workspace, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ResolveImageUrl_HandlesRelativeRootedAndAbsolute()
    {
        var root = _workspace;
        var resolved = MarkdownServices.ResolveImageUrl("resources/a.png", root);
        Assert.Equal(new Uri(Path.Combine(root, "resources", "a.png")).AbsoluteUri, resolved);
        Assert.Equal("https://example.com/a.png", MarkdownServices.ResolveImageUrl("https://example.com/a.png", root));
        Assert.StartsWith("data:", MarkdownServices.ResolveImageUrl("data:image/png;base64,AAAA", root));
        // 没有基准地址时相对路径保持原样（由管线拒绝）
        Assert.Equal("resources/a.png", MarkdownServices.ResolveImageUrl("resources/a.png", null));
    }

    [Fact]
    public async Task WorkspaceRelativeImages_PngAndSvg_Render()
    {
        await HeadlessApp.Run(async () =>
        {
            using var pipeline = new ImagePipeline(new()
            {
                Policy = ImagePolicy.Default with { AllowedFileRoots = [_workspace] },
                SvgRasterizer = new SvgImageRasterizer(),
            });
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, new MarkdownServices { Images = pipeline });
            MarkdownServices.SetImageBaseUri(view, _workspace);
            view.Markdown = Markdown;
            var window = new Window { Width = 720, Height = 900, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            view.ApplyLatest();

            var segments = () => view.GetVisualDescendants().OfType<TextSegmentView>().ToList();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && segments().Sum(s => s.LoadedImageCount) < 4)
            {
                await HeadlessApp.Settle();
                await Task.Delay(20);
            }
            await HeadlessApp.Settle();
            HeadlessApp.Save(window, "images-workspace");

            var all = segments();
            // 加载完成后行高随图片增长（固定行高不会裁掉图片）
            Assert.Contains(all, s => s.LoadedImageCount == 1 && s.Bounds.Height >= 110);
            Assert.Equal(4, all.Sum(s => s.LoadedImageCount));
            // 缺失的文件显示失败标签，工作区外的文件被策略拒绝
            Assert.Equal(2, all.Sum(s => s.FailedImageCount));
            window.Close();
        });
    }

    [Fact]
    public async Task DecodeDensity_ShrinksDocumentImage_ViewerLoadsOriginalAndEvictsOnClose()
    {
        await HeadlessApp.Run(async () =>
        {
            File.WriteAllBytes(Path.Combine(_workspace, "wide.png"), TestPng.Create(1600, 400, 22, 119, 255));
            using var pipeline = new ImagePipeline(new()
            {
                Policy = ImagePolicy.Default with { AllowedFileRoots = [_workspace] },
                DecodeDensity = 0.5,
            });
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, new MarkdownServices { Images = pipeline });
            MarkdownServices.SetImageBaseUri(view, _workspace);
            view.Markdown = "![wide](wide.png)";
            var window = new Window { Width = 720, Height = 400, Content = view };
            window.Show();
            view.ApplyLatest();
            TextSegmentView? segment = null;
            for (var i = 0; i < 200 && segment is null; i++)
            {
                await HeadlessApp.Settle();
                await Task.Delay(10);
                segment = view.GetVisualDescendants().OfType<TextSegmentView>().FirstOrDefault(s => s.LoadedImageCount == 1);
            }
            Assert.NotNull(segment);
            var slot = Assert.Single(segment.ImageSlots);
            var decoded = Assert.IsAssignableFrom<global::Avalonia.Media.Imaging.Bitmap>(slot.Image).PixelSize;
            // 显示宽度约 688，密度 0.5 → 344 → 档位 512；保持原图宽高比 4:1
            Assert.Equal(512, decoded.Width);
            Assert.Equal(128, decoded.Height);
            Assert.Equal(new global::Avalonia.PixelSize(1600, 400), slot.PixelSize);

            // 查看器按原始尺寸打开并加载原图；关闭后原图移出缓存
            var viewer = segment.ShowViewer(slot, slot.Image!, null);
            Assert.NotNull(viewer);
            ImageLease? original = null;
            for (var i = 0; i < 200 && original is null; i++)
            {
                await HeadlessApp.Settle();
                await Task.Delay(10);
                original = pipeline.Memory.TryAcquire(slot.Url, 0);
            }
            Assert.NotNull(original);
            Assert.Equal(1600, original.DecodedSize.Width);
            original.Dispose();
            viewer.Close();
            Assert.Null(pipeline.Memory.TryAcquire(slot.Url, 0));
            // 文档内的缩小图不受影响
            Assert.NotNull(slot.Image);
            window.Close();
        });
    }

    [Fact]
    public async Task RelativeImage_WithoutBaseUri_FailsInsteadOfHanging()
    {
        await HeadlessApp.Run(async () =>
        {
            using var pipeline = new ImagePipeline();
            var view = new MarkdownView();
            MarkdownServices.SetServices(view, new MarkdownServices { Images = pipeline });
            view.Markdown = "![logo](resources/logo.png)";
            var window = new Window { Width = 400, Height = 200, Content = view };
            window.Show();
            view.ApplyLatest();
            for (var i = 0; i < 20 && view.GetVisualDescendants().OfType<TextSegmentView>().Sum(s => s.FailedImageCount) == 0; i++)
            {
                await HeadlessApp.Settle();
                await Task.Delay(10);
            }
            Assert.Equal(1, view.GetVisualDescendants().OfType<TextSegmentView>().Sum(s => s.FailedImageCount));
            window.Close();
        });
    }
}

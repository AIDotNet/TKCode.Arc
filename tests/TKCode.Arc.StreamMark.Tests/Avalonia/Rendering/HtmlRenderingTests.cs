using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Images;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Tests.Images;
using StreamMark.Core.Model;
using StreamMark.Mermaid;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class HtmlRenderingTests : IDisposable
{
    // AtomUI README.zh-CN.md 中的写法（图片换成本地文件）
    private const string Readme = """
        <div align="center">
          <img height="64" src="resources/logo.png" alt="AtomUI">
          <h1>AtomUI</h1>
          <p>基于 <strong>Avalonia</strong> 的 Ant Design 风格组件库</p>
        </div>

        <p align="center">
          <a href="https://github.com/AtomUI/AtomUI"><img src="resources/badge.png" alt="license"></a>
          <a href="https://www.nuget.org/packages/AtomUI"><img src="resources/badge2.png" alt="nuget"></a>
        </p>

        #### 赞助商

        <table>
          <tr>
            <td align="center" width="200">
              <a href="https://example.com/a"><img src="./resources/sponsor.png" width="120"></a>
              <p>火数云提供云服务器、高防与 GPU 算力服务。</p>
            </td>
            <td align="center">
              <a href="https://example.com/b"><img src="./resources/sponsor.png" width="120"></a>
              <p>第二个赞助商</p>
            </td>
          </tr>
        </table>

        #### 致谢

        <div>
            <div align="center">
              <h1>Ant Design</h1>
              <img height="80" src="resources/ant.svg">
            </div>
        Ant Design 是由蚂蚁集团推出的企业级 UI 设计语言和 React 组件库。
        </div>

        <div style="margin-top: 30px; padding: 12px 16px; background: #f5f5f5; border-radius: 8px; color: #333">
            <div align="center">
                <h1>Avalonia OSS</h1>
            </div>
        Avalonia 是一个跨平台的 .NET UI 框架，使用 <span style="color: #1677ff; font-size: 18px">XAML</span> 语言设计界面。
        </div>

        使用 AtomUI OSS 的项目需要遵循 LGPL v3 协议。<strong>商业应用，包括公司内部项目、个人商业项目和外包项目，
        在使用已发布二进制包链接的情况下可以免费使用</strong>。<font color="#333">暗色主题里看不清的颜色</font>

        <custom-widget data-x="1">
        kept as source
        </custom-widget>
        """;

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "streammark-html-" + Guid.NewGuid().ToString("N"));

    public HtmlRenderingTests()
    {
        var dir = Path.Combine(_workspace, "resources");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "logo.png"), TestPng.Create(64, 64, 22, 119, 255));
        File.WriteAllBytes(Path.Combine(dir, "badge.png"), TestPng.Create(90, 20, 70, 160, 80));
        File.WriteAllBytes(Path.Combine(dir, "badge2.png"), TestPng.Create(80, 20, 0, 120, 200));
        File.WriteAllBytes(Path.Combine(dir, "sponsor.png"), TestPng.Create(240, 80, 230, 120, 40));
        File.WriteAllText(Path.Combine(dir, "ant.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"80\" height=\"80\"><circle cx=\"40\" cy=\"40\" r=\"36\" fill=\"#ff4d4f\"/></svg>");
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

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task ReadmeHtml_Renders(string theme)
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
            view.Markdown = Readme;
            var light = theme == "light";
            var window = new Window
            {
                Width = 760,
                Height = 1250,
                Content = view,
                RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark,
                Background = light ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)),
            };
            window.Show();
            view.ApplyLatest();
            var segments = () => view.GetVisualDescendants().OfType<TextSegmentView>().ToList();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && segments().Sum(s => s.LoadedImageCount) < 6)
            {
                await HeadlessApp.Settle();
                await Task.Delay(20);
            }
            await HeadlessApp.Settle();
            HeadlessApp.Save(window, "html-readme-" + theme);

            var blocks = view.Snapshot!.Blocks.ToList();
            Assert.IsType<HtmlBoxBlock>(blocks[0]);
            Assert.Contains(blocks, b => b is TableBlock);
            Assert.Contains(blocks, b => b is HtmlBlock { Html: var h } && h.Contains("custom-widget", StringComparison.Ordinal));
            Assert.Equal(6, segments().Sum(s => s.LoadedImageCount));
            Assert.True(view.GetVisualDescendants().OfType<HtmlBoxBlockView>().Count() >= 4);

            // 居中：标题文字位于中线附近
            var title = segments().First(s => s.Content.Text == "AtomUI");
            var center = title.TranslatePoint(new(title.Bounds.Width / 2, 0), view)!.Value.X;
            Assert.InRange(center, view.Bounds.Width / 2 - 40, view.Bounds.Width / 2 + 40);

            // 复制：盒子里的文字按块输出，标签不出现
            view.SelectionController.SelectAll();
            var text = view.SelectionController.GetSelectedText();
            Assert.Contains("基于 Avalonia 的 Ant Design 风格组件库", text);
            Assert.Contains("商业应用，包括公司内部项目、个人商业项目和外包项目，在使用已发布二进制包链接的情况下可以免费使用", text);
            Assert.DoesNotContain("<strong>", text);
            Assert.DoesNotContain("<div", text);
            window.Close();
        });
    }
}

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using StreamMark.Highlighting.TextMate;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

public sealed class DiffRenderingTests
{
    private const string Diff = """
        diff --git a/src/App.cs b/src/App.cs
        index 83db48f..bf269f4 100644
        --- a/src/App.cs
        +++ b/src/App.cs
        @@ -10,7 +10,8 @@ public sealed class App
             public void Run()
             {
        -        var name = "old";
        -        Console.WriteLine(name);
        +        var name = "new";
        +        var greeting = $"Hello {name}";
        +        Console.WriteLine(greeting);
             }

             // end
        diff --git a/README.md b/README.md
        --- a/README.md
        +++ b/README.md
        @@ -1,3 +1,3 @@
        -# StreamMark
        +# StreamMark for Avalonia

         Streaming markdown.
        """;

    private static async Task<(Window Window, MarkdownView View, DiffBlockView Diff)> Render(MarkdownServices services, double width)
    {
        var view = new MarkdownView { Margin = new(16) };
        MarkdownServices.SetServices(view, services);
        view.Markdown = "```diff\n" + Diff + "\n```";
        var window = new Window { Width = width, Height = 560, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
        window.Show();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        // 等后台着色完成
        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(20);
            await HeadlessApp.Settle(1);
        }
        return (window, view, view.GetVisualDescendants().OfType<DiffBlockView>().Single());
    }

    [Fact]
    public async Task Default_IsGitHubMarkdownStyle_TogglesToSplit()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new TextMatePlugin()] };
            var (window, view, diff) = await Render(services, 980);
            Assert.Equal(DiffViewMode.Markdown, diff.EffectiveMode);
            Assert.Equal(2, diff.Document.Files.Length);
            var markdownHeight = diff.Bounds.Height;
            HeadlessApp.Save(window, "diff-markdown");

            diff.Mode = DiffViewMode.Split;
            await HeadlessApp.Settle();
            Assert.Equal(DiffViewMode.Split, diff.EffectiveMode);
            // 并排：删除与新增配对、文件头折叠为标题栏，行数更少
            Assert.True(diff.Bounds.Height < markdownHeight);
            HeadlessApp.Save(window, "diff-split");

            // 复制：整块是原始 diff（Windows 检出时原始字符串字面量带 \r\n，渲染管线统一为 \n）
            view.SelectionController.SelectAll();
            Assert.Equal(Diff.Replace("\r\n", "\n"), view.SelectionController.GetSelectedText());
            window.Close();
        });
    }

    [Fact]
    public async Task UnifiedMode_WithLineNumbers_ByOption()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new TextMatePlugin()], CodeBlocks = CodeBlockOptions.Default with { DiffMode = DiffViewMode.Unified } };
            var (window, _, diff) = await Render(services, 980);
            Assert.Equal(DiffViewMode.Unified, diff.EffectiveMode);
            HeadlessApp.Save(window, "diff-unified");
            // 并排后切回：回到宿主默认的统一视图
            diff.Mode = DiffViewMode.Split;
            await HeadlessApp.Settle();
            Assert.Equal(DiffViewMode.Split, diff.EffectiveMode);
            window.Close();
        });
    }

    [Fact]
    public async Task Split_FallsBackToUnified_WhenNarrow_AndModeSurvivesRecreation()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { CodeBlocks = CodeBlockOptions.Default with { DiffMode = DiffViewMode.Split } };
            var (window, _, diff) = await Render(services, 520);
            Assert.Equal(DiffViewMode.Split, diff.Mode);
            Assert.Equal(DiffViewMode.Markdown, diff.EffectiveMode);
            window.Width = 1000;
            await HeadlessApp.Settle();
            Assert.Equal(DiffViewMode.Split, diff.EffectiveMode);
            window.Close();
        });
    }

    [Fact]
    public async Task Transcript_ModeIsKeptAcrossVirtualization_AndStreams()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 1000, Height = 500, Content = view };
            window.Show();
            var stream = new MarkdownStream(smooth: false);
            view.Entries.Add(new MarkdownEntry(stream));
            stream.Push("```diff\n");
            // 流式：边写边显示为 diff 视图
            foreach (var line in Diff.Split('\n'))
            {
                stream.Push(line + "\n");
            }
            await stream.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            var diff = view.Panel.Children.OfType<DiffBlockView>().Single();
            Assert.Equal(2, diff.Document.Files.Length);
            diff.Mode = DiffViewMode.Split;
            stream.Push("```\n\n" + string.Concat(Enumerable.Range(0, 80).Select(i => $"Paragraph {i}.\n\n")));
            stream.Complete();
            await stream.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            view.ScrollToEnd();
            await HeadlessApp.Settle();
            Assert.Empty(view.Panel.Children.OfType<DiffBlockView>());
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();
            Assert.Equal(DiffViewMode.Split, view.Panel.Children.OfType<DiffBlockView>().Single().Mode);
            window.Close();
        });
    }
}

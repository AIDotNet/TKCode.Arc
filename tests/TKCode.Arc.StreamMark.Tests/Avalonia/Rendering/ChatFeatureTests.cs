using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Model;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>阶段 4：链接策略、引用来源卡片、代码块行号 / 折叠 / 下载 / diff。</summary>
public sealed class ChatFeatureTests
{
    [Theory]
    [InlineData("https://example.com", null, LinkDecision.Allow)]
    [InlineData("http://example.com/a?b", null, LinkDecision.Allow)]
    [InlineData("mailto:a@b.com", null, LinkDecision.Allow)]
    [InlineData("javascript:alert(1)", null, LinkDecision.Block)]
    [InlineData("JavaScript:alert(1)", null, LinkDecision.Block)]
    [InlineData("java\tscript:alert(1)", null, LinkDecision.Block)]
    [InlineData("file:///etc/passwd", null, LinkDecision.Block)]
    [InlineData("data:text/html,<b>x</b>", null, LinkDecision.Block)]
    [InlineData("vscode://open", null, LinkDecision.Block)]
    [InlineData("#section", null, LinkDecision.Allow)]
    [InlineData("docs/readme.md", null, LinkDecision.Allow)]
    [InlineData("./a:b.md", null, LinkDecision.Allow)]
    [InlineData("", null, LinkDecision.Block)]
    [InlineData("https://evil.example/login", "https://github.com", LinkDecision.Confirm)]
    [InlineData("https://www.github.com/x", "github.com", LinkDecision.Allow)]
    [InlineData("https://github.com/x", "the GitHub repo", LinkDecision.Allow)]
    public void LinkPolicy_Default(string url, string? text, LinkDecision expected) =>
        Assert.Equal(expected, LinkPolicy.Default.Evaluate(url, text));

    [Fact]
    public void LinkPolicy_ConfirmAndOverride()
    {
        var confirm = LinkPolicy.Default with { RequireConfirmation = true };
        Assert.Equal(LinkDecision.Confirm, confirm.Evaluate("https://a.com"));
        Assert.Equal(LinkDecision.Block, confirm.Evaluate("javascript:x"));
        var custom = LinkPolicy.Default with
        {
            AllowedSchemes = ["https", "vscode"],
            Override = (url, decision) => url.Contains("tracker", StringComparison.Ordinal) ? LinkDecision.Block : decision,
        };
        Assert.Equal(LinkDecision.Allow, custom.Evaluate("vscode://file/a"));
        Assert.Equal(LinkDecision.Block, custom.Evaluate("http://a.com"));
        Assert.Equal(LinkDecision.Block, custom.Evaluate("https://tracker.com"));
        Assert.Equal(LinkDecision.Block, (LinkPolicy.Default with { AllowRelative = false }).Evaluate("a.md"));
    }

    [Fact]
    public void CodeFileNames_Suggest()
    {
        Assert.Equal("App.cs", CodeFileNames.Suggest("cs", "src/App.cs"));
        Assert.Equal("snippet.py", CodeFileNames.Suggest("python", null));
        Assert.Equal("Dockerfile", CodeFileNames.Suggest("dockerfile", null));
        Assert.Equal("snippet.txt", CodeFileNames.Suggest("unknown-lang", null));
    }

    [Fact]
    public async Task Links_BlockedAreNotClickable_CitationCardShows()
    {
        await HeadlessApp.Run(async () =>
        {
            var sources = new DictionarySourceProvider(new Dictionary<string, CitationSource>
            {
                ["1"] = new("Avalonia UI — Cross-platform .NET UI framework", "https://avaloniaui.net/docs", "Avalonia Docs",
                    "Avalonia is a cross-platform UI framework for .NET, providing a flexible styling system and supporting Windows, macOS, Linux, iOS, Android and WebAssembly."),
            });
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, new MarkdownServices { Sources = sources });
            view.Markdown = "Safe [docs](https://avaloniaui.net), unsafe [click me](javascript:alert(1)), spoofed [https://github.com](https://evil.example), source<Citation id=\"1\" />.";
            var window = new Window { Width = 720, Height = 360, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var segment = view.GetVisualDescendants().OfType<TextSegmentView>().Single();
            var content = segment.Content;
            var decisions = Enumerable.Range(0, content.Links.Length).Select(segment.LinkDecisionFor).ToList();
            Assert.Equal([LinkDecision.Allow, LinkDecision.Block, LinkDecision.Confirm], decisions);

            // 来源卡片（直接显示，不依赖合成输入）
            var citation = content.Objects.Single(o => o.Kind == InlineObjectKind.Component);
            var card = segment.ShowCard((citation.Offset, 1), sources.GetSource("1")!);
            Assert.NotNull(card);
            await HeadlessApp.Settle();
            Assert.Contains(OverlayLayer.GetOverlayLayer(view)!.Children, c => c is SourceCard);
            HeadlessApp.Save(window, "chat-links-citation");
            card!.Close();
            window.Close();
        });
    }

    [Fact]
    public async Task CodeBlock_LineNumbers_Diff_Collapse_Download()
    {
        await HeadlessApp.Run(async () =>
        {
            CodeSaveRequest? saved = null;
            var services = new MarkdownServices
            {
                CodeBlocks = CodeBlockOptions.Default with
                {
                    ShowLineNumbers = true,
                    CollapseAfterLines = 12,
                    CollapsedLines = 6,
                    Save = request =>
                    {
                        saved = request;
                        return Task.CompletedTask;
                    },
                },
            };
            var longCode = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"var line{i} = {i};"));
            var markdown = "```cs src/App.cs#L10-39\n" + longCode + "\n```\n\n```diff\n@@ -1,3 +1,3 @@\n context\n-old line\n+new line\n```";
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, services);
            view.Markdown = markdown;
            var window = new Window { Width = 720, Height = 640, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Single(blocks);
            Assert.Single(view.GetVisualDescendants().OfType<DiffBlockView>());
            var code = blocks[0];
            Assert.True(code.IsCollapsed);
            var collapsedHeight = code.Bounds.Height;
            HeadlessApp.Save(window, "chat-code-collapsed");

            code.ToggleExpanded();
            await HeadlessApp.Settle();
            Assert.False(code.IsCollapsed);
            Assert.True(code.Bounds.Height > collapsedHeight + 100);
            HeadlessApp.Save(window, "chat-code-expanded");

            // 复制整块不受折叠影响
            Assert.Equal(longCode, code.Code);

            await code.DownloadAsync();
            Assert.NotNull(saved);
            Assert.Equal("App.cs", saved!.SuggestedFileName);
            Assert.Equal(longCode, saved.Code);
            window.Close();
        });
    }

    [Fact]
    public async Task Transcript_CollapsedLongCode_IsOneRow_ExpandRechunks()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            var code = string.Join("\n", Enumerable.Range(0, 600).Select(i => $"line {i}"));
            var entry = MarkdownEntry.FromText("```text\n" + code + "\n```");
            view.Entries.Add(entry);
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.Equal(1, view.Panel.RowCount);
            var block = view.Panel.Children.OfType<CodeBlockView>().Single();
            Assert.True(block.IsCollapsed);

            block.ToggleExpanded();
            await HeadlessApp.Settle();
            // 600 行 / 128 = 5 段
            Assert.Equal(5, view.Panel.RowCount);
            window.Close();
        });
    }

    [Fact]
    public async Task StreamedCode_IsNotCollapsedAfterCompletion()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new TranscriptView();
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            var stream = new MarkdownStream(smooth: false);
            view.Entries.Add(new MarkdownEntry(stream));
            stream.Push("```text\n");
            for (var i = 0; i < 60; i++)
            {
                stream.Push($"line {i}\n");
                if (i % 10 == 0)
                {
                    await stream.Document.WhenIdle();
                    view.Panel.ApplyLatest();
                    await HeadlessApp.Settle(1);
                }
            }
            stream.Push("```\n");
            stream.Complete();
            await stream.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.False(view.Panel.Children.OfType<CodeBlockView>().Single().IsCollapsed);
            window.Close();
        });
    }
}

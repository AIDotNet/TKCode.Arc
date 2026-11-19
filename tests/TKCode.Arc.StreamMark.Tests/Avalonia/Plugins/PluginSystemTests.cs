using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;
using StreamMark.RaTeX;
using Xunit;

namespace StreamMark.Avalonia.Tests.Plugins;

public sealed class PluginSystemTests
{
    /// <summary>测试插件：块组件 &lt;Callout&gt;、行内组件 &lt;badge/&gt;、代码块头部操作、diff 围栏视图。</summary>
    private sealed class TestPlugin : IMarkdownPlugin
    {
        public int ActionRuns;

        public string Name => "test";

        public void Configure(MarkdownPluginBuilder builder)
        {
            builder.AddBlockComponent("callout", _ => new CalloutView());
            builder.AddInlineComponent("badge", new BadgeRenderer());
            builder.AddBlockAction(new BlockActionDescriptor("apply", "Apply", MarkdownIcons.Check,
                block => block is CodeBlock { Language: "cs" }, _ =>
                {
                    ActionRuns++;
                    return Task.CompletedTask;
                }));
            builder.AddFenceView("diff", _ => new DiffStubView());
        }
    }

    private sealed class CalloutView : StackBlockView
    {
        protected override Thickness ContentPadding => new(12, 8, 12, 8);

        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
            UpdateChildren(context, ((ComponentBlock)block).Children, topKey, ref segment);

        public override void Render(DrawingContext context) =>
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x40, 0x8B, 0x7C, 0xF6)), new Pen(Brushes.MediumPurple, 1), new Rect(Bounds.Size).Deflate(0.5), 6, 6);
    }

    private sealed class BadgeRenderer : IInlineComponentRenderer
    {
        public DrawableTextRun CreateRun(InlineObject component, InlineComponentContext context) =>
            new ChipLikeRun(component.Attributes["text"] ?? "", context);

        public string ToPlainText(InlineObject component) => component.Attributes["text"] ?? "";
    }

    private sealed class ChipLikeRun(string text, InlineComponentContext context) : DrawableTextRun
    {
        private readonly TextLayout _layout = new(text, Typeface.Default, context.FontSize * 0.8, Brushes.White);

        public override TextRunProperties Properties => context.Properties;

        public override Size Size => new(_layout.WidthIncludingTrailingWhitespace + 12, _layout.Height + 2);

        public override double Baseline => Size.Height * 0.8;

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
            drawingContext.DrawRectangle(Brushes.SeaGreen, null, new Rect(origin, Size), 8, 8);
            _layout.Draw(drawingContext, new Point(origin.X + 6, origin.Y + 1));
        }
    }

    private sealed class DiffStubView : BlockView
    {
        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
            segment++;

        protected override Size MeasureOverride(Size availableSize) => new(availableSize.Width, 40);

        public override void Render(DrawingContext context) => context.FillRectangle(Brushes.DarkRed, new Rect(Bounds.Size));
    }

    private const string Markdown = """
        Keys <kbd>Ctrl</kbd>+<kbd>K</kbd>, H<sub>2</sub>O, x<sup>2</sup>, <mark>marked</mark>, <u>under</u>, source<Citation id="1" /> and <cite href="https://example.com">a cited title</cite>. Status <badge text="beta"/>.

        <details>
        <summary>Collapsed details</summary>

        Hidden **content**.
        </details>

        <details open>
        <summary>Open details</summary>

        Visible content with a list:

        - one
        - two
        </details>

        <think>
        Model reasoning goes here.
        </think>

        <Callout>
        Custom **block** component from a plugin.
        </Callout>

        ```cs src/App.cs#L10-20
        var x = 1;
        ```

        ```diff
        -old
        +new
        ```
        """;

    [Fact]
    public async Task BuiltInAndPluginComponents_Render()
    {
        await HeadlessApp.Run(async () =>
        {
            var plugin = new TestPlugin();
            var services = new MarkdownServices { Plugins = [plugin, new RaTeXPlugin()] };
            var view = new MarkdownView { Margin = new(16) };
            MarkdownServices.SetServices(view, services);
            view.Markdown = Markdown;
            var window = new Window { Width = 720, Height = 900, Content = view, Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x18)) };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var blocks = view.Snapshot!.Blocks.ToList();
            Assert.Contains(blocks, b => b is ComponentBlock { Name: "callout" });
            var children = view.GetVisualChildren().ToList();
            Assert.Contains(children, c => c is CalloutView);
            Assert.Contains(children, c => c is DiffStubView);
            var collapsibles = children.OfType<CollapsibleBlockView>().ToList();
            Assert.Equal(3, collapsibles.Count);
            Assert.False(collapsibles[0].IsOpen);
            Assert.True(collapsibles[1].IsOpen);

            // 头部操作：只出现在 cs 代码块上（复制、换行、下载、插件操作，加上隐藏的折叠栏按钮）
            var code = children.OfType<CodeBlockView>().Single();
            var actionButtons = code.GetVisualChildren().OfType<IconButton>().Count();
            Assert.Equal(5, actionButtons);

            // 复制：组件输出纯文本
            view.SelectionController.SelectAll();
            var text = view.SelectionController.GetSelectedText();
            Assert.Contains("Keys Ctrl+K", text);
            Assert.Contains("source[1]", text);
            Assert.Contains("Status beta", text);
            HeadlessApp.Save(window, "plugins-components");
            window.Close();
        });
    }

    [Fact]
    public async Task CompatibilityProperties_AndPlugins_RenderTheSameModel()
    {
        await HeadlessApp.Run(async () =>
        {
            var legacy = new MarkdownServices { Math = RaTeXMathRenderer.Shared };
            var plugin = new MarkdownServices { Plugins = [new RaTeXPlugin()] };
            Assert.Same(legacy.Registry.Math, plugin.Registry.Math);
            Assert.Equal(legacy.Profile.Components.Length, plugin.Profile.Components.Length);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task BlockState_SurvivesVirtualization()
    {
        await HeadlessApp.Run(async () =>
        {
            var sb = new StringBuilder("```text\nfirst code block\n```\n\n");
            for (var i = 0; i < 200; i++)
            {
                sb.Append("Filler paragraph ").Append(i).Append(".\n\n");
            }
            var view = new TranscriptView();
            var window = new Window { Width = 800, Height = 500, Content = view };
            window.Show();
            var entry = MarkdownEntry.FromText(sb.ToString());
            view.Entries.Add(entry);
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            // 新内容默认贴底：先完成首次布局，再回到顶部
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();

            var code = view.Panel.Children.OfType<CodeBlockView>().Single();
            Assert.False(code.Wrap);
            // 用户切换"换行"：视图状态写回块状态存储
            code.Wrap = true;
            code.CurrentContext!.State.Set(code.TopKey, "code.wrap", true);

            // 滚到底部（代码块被回收），再滚回顶部（重新实例化）
            view.ScrollToEnd();
            await HeadlessApp.Settle();
            Assert.DoesNotContain(view.Panel.Children, c => c is CodeBlockView);
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();
            var recreated = view.Panel.Children.OfType<CodeBlockView>().Single();
            Assert.NotSame(code, recreated);
            Assert.True(recreated.Wrap);
            window.Close();
        });
    }

    private sealed class FailingPlugin : IMarkdownPlugin
    {
        public int Runs;

        public string Name => "failing";

        public void Configure(MarkdownPluginBuilder builder)
        {
            Runs++;
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class DetailsOverridePlugin : IMarkdownPlugin
    {
        public string Name => "details-override";

        public void Configure(MarkdownPluginBuilder builder) =>
            builder.AddBlockComponent("details", _ => new CalloutView(), registerTag: false);
    }

    [Fact]
    public void ConfigureFailure_NamesThePlugin_AndIsNotRetried()
    {
        var plugin = new FailingPlugin();
        var services = new MarkdownServices { Plugins = [plugin] };
        var first = Assert.Throws<PluginConfigurationException>(() => services.Registry);
        Assert.Equal("failing", first.PluginName);
        Assert.IsType<InvalidOperationException>(first.InnerException);
        var second = Assert.Throws<PluginConfigurationException>(() => services.Profile);
        Assert.Same(first, second);
        Assert.Equal(1, plugin.Runs);
    }

    // TestPlugin 用到图标几何，需要平台已初始化，所以这两个用例在 HeadlessApp 里跑
    [Fact]
    public async Task Overrides_AreRecordedWithOwners()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new DetailsOverridePlugin()] };
            var diagnostic = Assert.Single(services.Registry.Diagnostics);
            Assert.Equal(PluginDiagnosticKind.Replaced, diagnostic.Kind);
            Assert.Equal("block:details", diagnostic.Key);
            Assert.Equal("details-override", diagnostic.Plugin);
            Assert.Equal("builtin-components", diagnostic.Previous);
            // TestPlugin 的 diff 围栏视图覆盖了内置的那个；新组件不算覆盖
            var replaced = Assert.Single(new MarkdownServices { Plugins = [new TestPlugin()] }.Registry.Diagnostics);
            Assert.Equal("fence:diff", replaced.Key);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PluginsWithoutSyntax_ShareTheDefaultProfile()
    {
        await HeadlessApp.Run(async () =>
        {
            Assert.Same(MarkdownProfile.Default, MarkdownServices.Default.Profile);
            Assert.Same(MarkdownProfile.Default, new MarkdownServices { Plugins = [new RaTeXPlugin()] }.Profile);
            Assert.Same(MarkdownProfile.Default, new MarkdownServices { Plugins = [new DetailsOverridePlugin()] }.Profile);
            var custom = new MarkdownServices { Plugins = [new TestPlugin()] };
            Assert.NotSame(MarkdownProfile.Default, custom.Profile);
            Assert.Same(custom.Profile, custom.CreateDocument().Profile);
            Assert.Same(custom.Profile, custom.CreateStream().Document.Profile);
            Assert.Same(custom.Profile, custom.CreateEntry("x").Document.Profile);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task MarkdownView_ServicesSetAfterMarkdown_ReparsesWithPluginProfile()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new TestPlugin()] };
            // 顺序反过来：先设文本（按默认方言解析），再设服务
            var view = new MarkdownView { Markdown = "<Callout>\nInside.\n</Callout>\n" };
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            Assert.DoesNotContain(view.Snapshot!.Blocks, b => b is ComponentBlock);

            var documentId = view.DocumentId;
            MarkdownServices.SetServices(view, services);
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            Assert.Contains(view.Snapshot!.Blocks, b => b is ComponentBlock { Name: "callout" });
            Assert.Contains(view.GetVisualChildren(), c => c is CalloutView);
            Assert.Equal(documentId, view.DocumentId);
            window.Close();
        });
    }

    [Fact]
    public async Task HostDocumentWithOtherProfile_ReportsMismatchOnce()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new TestPlugin()] };
            var reported = new List<PluginDiagnostic>();
            services.Diagnostic += reported.Add;
            var view = new MarkdownView();
            MarkdownServices.SetServices(view, services);
            var document = new StreamingDocument();
            document.Load("<Callout>\nInside.\n</Callout>\n");
            view.Document = document;
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            await document.WhenIdle();
            view.ApplyLatest();
            await HeadlessApp.Settle();
            // 再触发一次上下文重建：同一文档不重复报告
            window.RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark;
            await HeadlessApp.Settle();
            view.ApplyLatest();
            var diagnostic = Assert.Single(reported);
            Assert.Equal(PluginDiagnosticKind.ProfileMismatch, diagnostic.Kind);

            // 用服务创建的文档不报告
            reported.Clear();
            view.Document = services.CreateDocument();
            view.ApplyLatest();
            Assert.Empty(reported);
            window.Close();
        });
    }

    private sealed class StubDiagramRenderer(string language) : IDiagramRenderer
    {
        public bool CanRender(string? lang) => lang == language;

        public Task<DiagramResult?> RenderAsync(string lang, string code, DiagramRenderContext context, CancellationToken cancellationToken) =>
            Task.FromResult<DiagramResult?>(null);
    }

    /// <summary>表格与图表上的操作，外加一个 stub 图表渲染器。</summary>
    private sealed class HeaderActionsPlugin(IDiagramRenderer diagrams) : IMarkdownPlugin
    {
        public List<MarkdownBlock> AppliedTo { get; } = [];

        public string Name => "header-actions";

        public void Configure(MarkdownPluginBuilder builder)
        {
            builder.AddDiagramRenderer(diagrams);
            builder.AddBlockAction(new BlockActionDescriptor("inspect", "Inspect", MarkdownIcons.Check,
                block =>
                {
                    AppliedTo.Add(block);
                    return true;
                }, _ => Task.CompletedTask)
            {
                Targets = BlockActionTargets.Table | BlockActionTargets.Diagram,
            });
        }
    }

    [Fact]
    public async Task BlockActions_AppearOnTheirTargetsOnly()
    {
        await HeadlessApp.Run(async () =>
        {
            var plugin = new HeaderActionsPlugin(new StubDiagramRenderer("stub"));
            var services = new MarkdownServices { Plugins = [plugin] };
            var view = new MarkdownView();
            MarkdownServices.SetServices(view, services);
            view.Markdown = """
                | a | b |
                |---|---|
                | 1 | 2 |

                ```stub
                graph
                ```

                ```cs
                var x = 1;
                ```
                """;
            var window = new Window { Width = 720, Height = 600, Content = view };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var children = view.GetVisualChildren().ToList();
            // 表格：复制、全屏 + 插件操作；图表：切换、复制、全屏 + 插件操作；代码块：没有插件操作（复制、换行、下载、折叠栏）
            Assert.Equal(3, children.OfType<TableBlockView>().Single().GetVisualChildren().OfType<IconButton>().Count());
            Assert.Equal(4, children.OfType<DiagramBlockView>().Single().GetVisualChildren().OfType<IconButton>().Count());
            Assert.Equal(4, children.OfType<CodeBlockView>().Single().GetVisualChildren().OfType<IconButton>().Count());
            Assert.Contains(plugin.AppliedTo, b => b is TableBlock);
            Assert.Contains(plugin.AppliedTo, b => b is CodeBlock { Language: "stub" });
            Assert.DoesNotContain(plugin.AppliedTo, b => b is CodeBlock { Language: "cs" });
            Assert.Single(services.Registry.ActionsFor(BlockActionTargets.Table));
            Assert.Empty(services.Registry.ActionsFor(BlockActionTargets.Code));
            window.Close();
        });
    }

    [Fact]
    public async Task PluginDiagramRenderer_WinsOverCompatibilityProperty()
    {
        await HeadlessApp.Run(async () =>
        {
            var legacy = new StubDiagramRenderer("stub");
            var fromPlugin = new StubDiagramRenderer("stub");
            var services = new MarkdownServices { Diagrams = [legacy], Plugins = [new HeaderActionsPlugin(fromPlugin)] };
            Assert.Same(fromPlugin, services.FindDiagramRenderer("stub"));
            await Task.CompletedTask;
        });
    }

    /// <summary>标题来自属性、显示在子块之前的块组件：视图先消耗一个片段号给标题。</summary>
    private sealed class CardView : StackBlockView
    {
        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
        {
            segment++;
            UpdateChildren(context, ((ComponentBlock)block).Children, topKey, ref segment);
        }
    }

    private sealed class CardPlugin : IMarkdownPlugin
    {
        public string Name => "card";

        public void Configure(MarkdownPluginBuilder builder) =>
            builder.AddBlockComponent("card", _ => new CardView(), (component, text) =>
            {
                text.AddText(component.Attributes["title"] ?? "");
                text.AddBlocks(component.Children);
            });
    }

    [Fact]
    public async Task ComponentText_DrivesSegmentsAndCopy()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new CardPlugin()] };
            var view = new MarkdownView();
            MarkdownServices.SetServices(view, services);
            view.Markdown = "<Card title=\"Hello title\">\nBody **text**.\n</Card>\n\nAfter.\n";
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var card = view.Snapshot!.Blocks.OfType<ComponentBlock>().Single();
            var segments = Selection.BlockText.Segments(card, services.Registry);
            Assert.Equal(2, segments.Count);
            Assert.Equal("Hello title", segments[0].Raw);
            // 没有注册表时按默认规则：只有子块
            Assert.Single(Selection.BlockText.Segments(card));

            view.SelectionController.SelectAll();
            var text = view.SelectionController.GetSelectedText();
            Assert.Matches("Hello title\\s+Body text\\.\\s+After\\.", text);
            window.Close();
        });
    }

    [Fact]
    public void FenceInfo_ParsesPathAndLines()
    {
        var info = CodeFenceInfo.Parse("cs src/App.cs#L10-20");
        Assert.Equal("cs", info.Language);
        Assert.Equal("src/App.cs", info.Path);
        Assert.Equal(10, info.StartLine);
        Assert.Equal(20, info.EndLine);
        Assert.Null(CodeFenceInfo.Parse("python").Path);
    }

    /// <summary>原样内容的组件视图：记下收到的数据，并用后台任务"处理"它。</summary>
    private sealed class ToolCallView : BlockView
    {
        public string? Raw { get; private set; }

        public bool Open { get; private set; }

        public List<int> Applied { get; } = [];

        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
        {
            var component = (ComponentBlock)block;
            Raw = component.Raw;
            Open = component.IsOpen;
            segment++;
            if (!ReferenceEquals(previous, block))
            {
                var raw = component.Raw ?? "";
                RunInBackground(block.ComputeFingerprint(), _ => Task.FromResult(raw.Length), Applied.Add);
            }
        }

        protected override Size MeasureOverride(Size availableSize) => new(availableSize.Width, 24);
    }

    private sealed class RawPlugin : IMarkdownPlugin
    {
        public string Name => "raw";

        public void Configure(MarkdownPluginBuilder builder)
        {
            builder.AddBlockComponent("tool-call", _ => new ToolCallView(), new BlockComponentOptions
            {
                Content = ComponentContent.Raw,
                Text = (component, text) => text.AddText(component.Raw ?? ""),
            });
            builder.AddBlockComponent("chart", _ => new ToolCallView(), new BlockComponentOptions
            {
                Content = ComponentContent.Raw,
                WhileStreaming = false,
                Text = (component, text) => text.AddText(component.Raw ?? ""),
            });
        }
    }

    private static async Task<MarkdownView> ShowDocument(MarkdownServices services, StreamingDocument document)
    {
        var view = new MarkdownView();
        MarkdownServices.SetServices(view, services);
        view.Document = document;
        var window = new Window { Width = 600, Height = 400, Content = view };
        window.Show();
        await HeadlessApp.Settle();
        return view;
    }

    private static async Task Apply(MarkdownView view, StreamingDocument document)
    {
        await document.WhenIdle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
    }

    [Fact]
    public async Task RawComponent_StreamsDataIntoPluginView_AndCopiesIt()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new RawPlugin()] };
            var document = services.CreateDocument();
            var view = await ShowDocument(services, document);

            document.Append("Before.\n\n<tool-call name=\"search\">\n{\"q\": \"**x");
            await Apply(view, document);
            var tool = Assert.Single(view.GetVisualDescendants().OfType<ToolCallView>());
            Assert.True(tool.Open);
            // 数据原样到达视图：remend 不会补出 "**"
            Assert.Equal("{\"q\": \"**x", tool.Raw);

            document.Append("\"}\n</tool-call>\n\nAfter **b**.");
            document.Complete();
            await Apply(view, document);
            Assert.Same(tool, Assert.Single(view.GetVisualDescendants().OfType<ToolCallView>()));
            Assert.False(tool.Open);
            Assert.Equal("{\"q\": \"**x\"}", tool.Raw);
            Assert.Equal(tool.Raw!.Length, tool.Applied[^1]);
            // "After" 段落没有被前面数据里的 ** 影响
            var after = view.Snapshot!.Blocks.OfType<ParagraphBlock>().Last();
            Assert.Equal("After b.", after.Content.Text);

            view.SelectionController.SelectAll();
            Assert.Matches("Before\\.\\s+\\{\"q\": \"\\*\\*x\"\\}\\s+After b\\.", view.SelectionController.GetSelectedText());
            ((Window)view.Parent!).Close();
        });
    }

    [Fact]
    public async Task ComponentNotWhileStreaming_SwitchesToPluginViewWhenClosed()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new RawPlugin()] };
            var document = services.CreateDocument();
            var view = await ShowDocument(services, document);

            document.Append("<chart>\nbar 1 2 3\n");
            await Apply(view, document);
            var open = view.Snapshot!.Blocks.OfType<ComponentBlock>().Single();
            Assert.True(open.IsOpen);
            Assert.Empty(view.GetVisualDescendants().OfType<ToolCallView>());
            // 视图还是默认容器（原样内容不显示），复制文本也按默认规则：两边片段数一致
            Assert.Empty(Selection.BlockText.Segments(open, services.Registry));

            document.Append("</chart>\n");
            document.Complete();
            await Apply(view, document);
            var chart = Assert.Single(view.GetVisualDescendants().OfType<ToolCallView>());
            Assert.Equal("bar 1 2 3", chart.Raw);
            Assert.Single(Selection.BlockText.Segments(view.Snapshot!.Blocks.OfType<ComponentBlock>().Single(), services.Registry));
            ((Window)view.Parent!).Close();
        });
    }

    private sealed class WorkView : BlockView
    {
        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
        {
        }

        public void Run(ulong key, Func<CancellationToken, Task<string>> work, List<string> applied) => RunInBackground(key, work, applied.Add);
    }

    [Fact]
    public async Task RunInBackground_SkipsSameKey_AndDropsStaleResults()
    {
        await HeadlessApp.Run(async () =>
        {
            var view = new WorkView();
            var applied = new List<string>();
            var started = 0;
            var slow = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            view.Run(1, _ => { Interlocked.Increment(ref started); return slow.Task; }, applied);
            view.Run(1, _ => { Interlocked.Increment(ref started); return slow.Task; }, applied);
            await HeadlessApp.Settle();
            Assert.Equal(1, started);

            view.Run(2, _ => Task.FromResult("second"), applied);
            slow.SetResult("first");
            await HeadlessApp.Settle();
            Assert.Equal(["second"], applied);

            // 已应用过的 key 不再执行
            view.Run(2, _ => { Interlocked.Increment(ref started); return Task.FromResult("again"); }, applied);
            await HeadlessApp.Settle();
            Assert.Equal(1, started);
            Assert.Equal(["second"], applied);
        });
    }

    [Fact]
    public void BlockResultCache_EvictsLeastRecentlyUsed()
    {
        var cache = new BlockResultCache<int, string>(capacity: 2);
        cache.Set(1, "a");
        cache.Set(2, "b");
        Assert.True(cache.TryGet(1, out _));
        cache.Set(3, "c");
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out var one));
        Assert.Equal("a", one);
        Assert.Equal(2, cache.Count);
    }
}

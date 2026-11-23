using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Highlighting;
using StreamMark.Core.Streaming;
using StreamMark.Highlighting.TextMate;
using Xunit;

namespace StreamMark.Avalonia.Tests.Rendering;

/// <summary>回归 #179：同一顶层容器（列表、引用、HTML 盒子、折叠块）内多个子块不能共享状态槽。</summary>
public sealed class NestedBlockStateTests
{
    private const string Issue179Repro = """
        1. First:

           ```csharp
           var first = 1; // block A
           ```

           Then:

           ```bash
           echo block B
           ```
        2. Item two:

           ```csharp
           var third = 3; // block C
           ```

        > A quote:
        >
        > ```csharp
        > var fourth = 4; // block D
        > ```
        >
        > ```csharp
        > var fifth = 5; // block E
        > ```

        ```csharp
        var sixth = 6; // block F, top level
        ```
        """;

    private sealed class DeterministicHighlighter : ISyntaxHighlighter
    {
        private readonly object _lock = new();
        private readonly List<(string Language, bool Disposed)> _sessions = [];

        public int DisposedCount
        {
            get
            {
                lock (_lock)
                {
                    return _sessions.Count(s => s.Disposed);
                }
            }
        }

        public static uint ColorFor(string language) => language switch
        {
            "csharp" or "cs" => 0xFF569CD6u,
            "bash" or "sh" => 0xFF4EC9B0u,
            "py" or "python" => 0xFFDCDCAAu,
            _ => 0xFFCE9178u,
        };

        public bool SupportsLanguage(string? language) => !string.IsNullOrEmpty(language);

        public (uint Foreground, uint Background) GetThemeColors(HighlightThemeKind theme) =>
            theme == HighlightThemeKind.Dark ? (0xFFD4D4D4u, 0xFF1E1E1Eu) : (0xFF000000u, 0xFFFFFFFFu);

        public IHighlightSession CreateSession(string language, HighlightThemeKind theme)
        {
            int index;
            lock (_lock)
            {
                index = _sessions.Count;
                _sessions.Add((language, false));
            }
            return new Session(this, index, language);
        }

        private void MarkDisposed(int index)
        {
            lock (_lock)
            {
                _sessions[index] = (_sessions[index].Language, true);
            }
        }

        private sealed class Session(DeterministicHighlighter owner, int index, string language) : IHighlightSession
        {
            public IReadOnlyList<ImmutableArray<HighlightToken>> Update(string code)
            {
                var color = ColorFor(language);
                var lines = code.Split('\n');
                var result = new List<ImmutableArray<HighlightToken>>(lines.Length);
                foreach (var raw in lines)
                {
                    var line = raw.EndsWith('\r') ? raw[..^1] : raw;
                    result.Add(line.Length == 0
                        ? []
                        : [new HighlightToken(0, line.Length, color, HighlightFontStyle.None)]);
                }
                return result;
            }

            public void Dispose() => owner.MarkDisposed(index);
        }
    }

    private sealed class StubDiagramRenderer : IDiagramRenderer
    {
        public bool CanRender(string? language) => language == "mermaid";

        public Task<DiagramResult?> RenderAsync(string language, string code, DiagramRenderContext context, CancellationToken cancellationToken) =>
            Task.FromResult<DiagramResult?>(new FakeDiagram());

        private sealed class FakeDiagram : DiagramResult
        {
            public override Size Size => new(200, 100);

            public override void Draw(DrawingContext context, Rect destination) =>
                context.DrawRectangle(Brushes.SteelBlue, null, destination);
        }
    }

    private static async Task<(Window Window, MarkdownView View)> ShowDocument(string markdown, MarkdownServices? services = null, double width = 900)
    {
        var doc = new StreamingDocument();
        doc.Load(markdown);
        await doc.WhenIdle();
        var view = new MarkdownView { Document = doc, Margin = new(12) };
        if (services is not null)
        {
            MarkdownServices.SetServices(view, services);
        }
        var window = new Window { Width = width, Height = 800, Content = view, Background = Brushes.Black };
        window.Show();
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
        return (window, view);
    }

    private static async Task WaitUntil(Func<bool> predicate, string message, int timeoutSeconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await HeadlessApp.Settle(2);
            if (predicate())
            {
                return;
            }
        }
        Assert.True(predicate(), message);
    }

    private static async Task ForceContextRefresh(Window window, MarkdownView view)
    {
        window.RequestedThemeVariant = window.RequestedThemeVariant == ThemeVariant.Light
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
        await HeadlessApp.Settle();
        view.ApplyLatest();
        await HeadlessApp.Settle();
    }

    private static async Task Refresh(MarkdownView view, string markdown)
    {
        var before = view.Snapshot!.Version;
        view.RefreshMarkdown(markdown);
        for (var i = 0; i < 100 && view.Snapshot!.Version == before; i++)
        {
            await HeadlessApp.Settle(2);
        }
        await HeadlessApp.Settle();
        Assert.NotEqual(before, view.Snapshot!.Version);
    }

    [Fact]
    public async Task Issue179_TextMate_AllSixCodeBlocksKeepOwnCodeAndHighlight()
    {
        await HeadlessApp.Run(async () =>
        {
            var services = new MarkdownServices { Plugins = [new TextMatePlugin()] };
            var (window, view) = await ShowDocument(Issue179Repro, services);
            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(6, blocks.Count);
            var texts = blocks.Select(b => b.GetVisualChildren().OfType<CodeTextView>().Single()).ToList();

            await WaitUntil(
                () => texts.All(t => t.VisibleLineCount > 0 && !t.VisibleLineTokens(0).IsDefaultOrEmpty),
                "all six code blocks should finish TextMate syntax highlighting");

            string[] expectedLines =
            [
                "var first = 1; // block A",
                "echo block B",
                "var third = 3; // block C",
                "var fourth = 4; // block D",
                "var fifth = 5; // block E",
                "var sixth = 6; // block F, top level",
            ];

            // 每个块持有独立的 CodeBlockState、渲染自己的代码行，并且都拿到非空着色
            var states = texts.Select(t => t.State).ToList();
            Assert.Equal(6, states.Distinct(ReferenceEqualityComparer.Instance).Count());
            for (var i = 0; i < 6; i++)
            {
                Assert.Equal(expectedLines[i], blocks[i].Code);
                Assert.Equal(expectedLines[i], texts[i].VisibleLineText(0));
                var tokens = texts[i].VisibleLineTokens(0);
                Assert.False(tokens.IsDefaultOrEmpty, $"block {i} should have highlight tokens");
                Assert.Contains(tokens, tok => tok.Foreground != 0);
            }

            window.Close();
        });
    }

    [Fact]
    public async Task DeterministicHighlighter_NestedBlocksKeepLanguageColorAndNeverDisposeSiblings()
    {
        await HeadlessApp.Run(async () =>
        {
            var highlighter = new DeterministicHighlighter();
            var services = new MarkdownServices { Highlighter = highlighter };
            var (window, view) = await ShowDocument(Issue179Repro, services);
            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(6, blocks.Count);
            var texts = blocks.Select(b => b.GetVisualChildren().OfType<CodeTextView>().Single()).ToList();

            await WaitUntil(
                () => texts.All(t => t.VisibleLineCount > 0 && !t.VisibleLineTokens(0).IsDefaultOrEmpty),
                "all six code blocks should finish deterministic highlighting");

            Assert.Equal(0, highlighter.DisposedCount);
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[0].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("bash"), texts[1].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[2].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[3].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[4].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[5].VisibleLineTokens(0)[0].Foreground);

            Assert.Equal("var first = 1; // block A", texts[0].VisibleLineText(0));
            Assert.Equal("echo block B", texts[1].VisibleLineText(0));
            Assert.Equal("var fourth = 4; // block D", texts[3].VisibleLineText(0));
            Assert.Equal("var fifth = 5; // block E", texts[4].VisibleLineText(0));

            window.Close();
        });
    }

    [Fact]
    public async Task HtmlBox_MultiplePreCodeBlocks_KeepIndependentStateAndHighlight()
    {
        await HeadlessApp.Run(async () =>
        {
            const string html = """
                <div>
                <pre><code class="language-csharp">var a = 1;</code></pre>
                <pre><code class="language-bash">echo b</code></pre>
                </div>
                """;
            var highlighter = new DeterministicHighlighter();
            var services = new MarkdownServices { Highlighter = highlighter };
            var (window, view) = await ShowDocument(html, services);
            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(2, blocks.Count);
            var texts = blocks.Select(b => b.GetVisualChildren().OfType<CodeTextView>().Single()).ToList();

            await WaitUntil(
                () => texts.All(t => t.VisibleLineCount > 0 && !t.VisibleLineTokens(0).IsDefaultOrEmpty),
                "both HTML pre/code blocks should be highlighted");

            Assert.NotSame(texts[0].State, texts[1].State);
            Assert.Equal("var a = 1;", texts[0].VisibleLineText(0));
            Assert.Equal("echo b", texts[1].VisibleLineText(0));
            Assert.Equal(DeterministicHighlighter.ColorFor("csharp"), texts[0].VisibleLineTokens(0)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("bash"), texts[1].VisibleLineTokens(0)[0].Foreground);

            window.Close();
        });
    }

    [Fact]
    public async Task CodeBlock_WrapAndExpand_AreIndependentForSiblingsInListAndQuote()
    {
        await HeadlessApp.Run(async () =>
        {
            var longA = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"var a{i} = {i};"));
            var longB = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"echo b{i}"));
            var longD = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"> var d{i} = {i};"));
            var longE = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"> var e{i} = {i};"));
            var markdown =
                "1. Item:\n\n"
                + "   ```csharp\n" + string.Join("\n", longA.Split('\n').Select(l => "   " + l)) + "\n   ```\n\n"
                + "   ```bash\n" + string.Join("\n", longB.Split('\n').Select(l => "   " + l)) + "\n   ```\n\n"
                + "> Quote:\n>\n"
                + "> ```csharp\n" + longD + "\n> ```\n>\n"
                + "> ```csharp\n" + longE + "\n> ```\n";

            var services = new MarkdownServices
            {
                CodeBlocks = CodeBlockOptions.Default with { CollapseAfterLines = 10, CollapsedLines = 5 },
            };
            var (window, view) = await ShowDocument(markdown, services);
            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(4, blocks.Count);
            CodeBlockView blockA = blocks[0], blockB = blocks[1], blockD = blocks[2], blockE = blocks[3];

            Assert.All(blocks, b =>
            {
                Assert.False(b.Wrap);
                Assert.True(b.IsCollapsed);
            });

            // 点击 A 的换行按钮、展开 D
            var wrapA = blockA.GetVisualChildren().OfType<IconButton>()
                .Single(b => AutomationProperties.GetName(b) == "Wrap lines");
            wrapA.PerformClick();
            blockD.ToggleExpanded();
            await HeadlessApp.Settle();

            Assert.True(blockA.Wrap);
            Assert.False(blockB.Wrap);
            Assert.False(blockD.IsCollapsed);
            Assert.True(blockE.IsCollapsed);

            // 触发整树重刷（主题变化）：每个块重新从 BlockStateStore 读取状态，不能互相串味
            await ForceContextRefresh(window, view);

            Assert.True(blockA.Wrap);
            Assert.False(blockB.Wrap);
            Assert.False(blockD.Wrap);
            Assert.False(blockE.Wrap);

            Assert.True(blockA.IsCollapsed);
            Assert.True(blockB.IsCollapsed);
            Assert.False(blockD.IsCollapsed);
            Assert.True(blockE.IsCollapsed);

            window.Close();
        });
    }

    [Fact]
    public async Task DiagramBlock_TwoInQuote_HaveIndependentCodeToggleAndSource()
    {
        await HeadlessApp.Run(async () =>
        {
            const string markdown = """
                > Diagrams:
                >
                > ```mermaid
                > flowchart TD
                >   A-->B
                > ```
                >
                > ```mermaid
                > flowchart LR
                >   X-->Y
                > ```
                """;

            var services = new MarkdownServices
            {
                Diagrams = [new StubDiagramRenderer()],
                Highlighter = new DeterministicHighlighter(),
            };
            var (window, view) = await ShowDocument(markdown, services);
            var diagrams = view.GetVisualDescendants().OfType<DiagramBlockView>().ToList();
            Assert.Equal(2, diagrams.Count);

            await WaitUntil(
                () => diagrams.All(d => d.IsShowingDiagram),
                "both nested diagram blocks should render");

            var code0 = diagrams[0].GetVisualChildren().OfType<CodeTextView>().Single();
            var code1 = diagrams[1].GetVisualChildren().OfType<CodeTextView>().Single();
            Assert.NotSame(code0.State, code1.State);
            Assert.Contains("A-->B", code0.Text);
            Assert.Contains("X-->Y", code1.Text);
            Assert.Equal("flowchart TD", code0.VisibleLineText(0));
            Assert.Equal("flowchart LR", code1.VisibleLineText(0));

            // 切第一个为源码视图
            var toggle0 = diagrams[0].GetVisualChildren().OfType<IconButton>()
                .Single(b => ReferenceEquals(b.Icon, MarkdownIcons.Code));
            toggle0.PerformClick();
            await HeadlessApp.Settle();

            Assert.False(diagrams[0].IsShowingDiagram);
            Assert.True(diagrams[1].IsShowingDiagram);

            await ForceContextRefresh(window, view);

            Assert.False(diagrams[0].IsShowingDiagram);
            Assert.True(diagrams[1].IsShowingDiagram);
            Assert.Equal("flowchart TD", code0.VisibleLineText(0));
            Assert.Equal("flowchart LR", code1.VisibleLineText(0));

            window.Close();
        });
    }

    [Fact]
    public async Task DiffBlock_TwoInQuote_HaveIndependentModeAndHighlightState()
    {
        await HeadlessApp.Run(async () =>
        {
            const string markdown = """
                > Diffs:
                >
                > ```diff
                > diff --git a/src/First.cs b/src/First.cs
                > --- a/src/First.cs
                > +++ b/src/First.cs
                > @@ -1,2 +1,2 @@
                > -var oldA = 1;
                > +var newA = 2;
                > ```
                >
                > ```diff
                > diff --git a/src/Second.py b/src/Second.py
                > --- a/src/Second.py
                > +++ b/src/Second.py
                > @@ -1,2 +1,2 @@
                > -x = 1
                > +x = 2
                > ```
                """;

            var highlighter = new DeterministicHighlighter();
            var services = new MarkdownServices { Highlighter = highlighter };
            var (window, view) = await ShowDocument(markdown, services, width: 1000);
            var diffs = view.GetVisualDescendants().OfType<DiffBlockView>().ToList();
            Assert.Equal(2, diffs.Count);
            var bodies = diffs.Select(d => d.GetVisualChildren().OfType<DiffBodyView>().Single()).ToList();

            Assert.NotSame(bodies[0].HighlightState, bodies[1].HighlightState);

            var csAddedLine = diffs[0].Document.Files[0].Hunks[0].Lines.Single(l => l.Text.Contains("newA", StringComparison.Ordinal)).RawLine;
            var pyAddedLine = diffs[1].Document.Files[0].Hunks[0].Lines.Single(l => l.Text.Contains("x = 2", StringComparison.Ordinal)).RawLine;

            await WaitUntil(
                () => !bodies[0].HighlightState!.TokensFor(csAddedLine).IsDefaultOrEmpty
                    && !bodies[1].HighlightState!.TokensFor(pyAddedLine).IsDefaultOrEmpty,
                "both nested diff blocks should finish syntax highlighting");

            Assert.Equal(DeterministicHighlighter.ColorFor("cs"), bodies[0].HighlightState!.TokensFor(csAddedLine)[0].Foreground);
            Assert.Equal(DeterministicHighlighter.ColorFor("py"), bodies[1].HighlightState!.TokensFor(pyAddedLine)[0].Foreground);

            diffs[0].Mode = DiffViewMode.Split;
            await HeadlessApp.Settle();
            Assert.Equal(DiffViewMode.Split, diffs[0].Mode);
            Assert.Equal(DiffViewMode.Markdown, diffs[1].Mode);

            await ForceContextRefresh(window, view);

            Assert.Equal(DiffViewMode.Split, diffs[0].Mode);
            Assert.Equal(DiffViewMode.Markdown, diffs[1].Mode);

            window.Close();
        });
    }

    [Fact]
    public async Task CollapsibleBlock_TwoInQuoteAndNestedThink_ToggleIndependently()
    {
        await HeadlessApp.Run(async () =>
        {
            const string markdown = """
                > <details>
                > <summary>First details</summary>
                >
                > Body 1
                > </details>
                >
                > <details>
                > <summary>Second details</summary>
                >
                > Body 2
                > </details>
                >
                > <think>
                > First thought
                > </think>
                >
                > <think>
                > Second thought
                > </think>

                <think>
                <think>
                Nested thought
                </think>
                </think>
                """;

            var (window, view) = await ShowDocument(markdown);
            var collapsibles = view.GetVisualDescendants().OfType<CollapsibleBlockView>().ToList();
            Assert.Equal(6, collapsibles.Count);
            Assert.All(collapsibles, c => Assert.False(c.IsOpen));

            // 展开第一个 details、第二个 think、以及外层嵌套 think（不展开内层）
            collapsibles[0].Toggle();
            collapsibles[3].Toggle();
            collapsibles[4].Toggle();
            await HeadlessApp.Settle();

            Assert.True(collapsibles[0].IsOpen);
            Assert.False(collapsibles[1].IsOpen);
            Assert.False(collapsibles[2].IsOpen);
            Assert.True(collapsibles[3].IsOpen);
            Assert.True(collapsibles[4].IsOpen);
            Assert.False(collapsibles[5].IsOpen);

            await ForceContextRefresh(window, view);

            Assert.True(collapsibles[0].IsOpen);
            Assert.False(collapsibles[1].IsOpen);
            Assert.False(collapsibles[2].IsOpen);
            Assert.True(collapsibles[3].IsOpen);
            Assert.True(collapsibles[4].IsOpen);
            Assert.False(collapsibles[5].IsOpen);

            window.Close();
        });
    }

    [Fact]
    public async Task RefreshMarkdown_PreservesIndependentNestedWrapStateAcrossShiftAndInContainerEdit()
    {
        await HeadlessApp.Run(async () =>
        {
            const string initial = """
                Intro.

                > Quote intro:
                >
                > ```csharp
                > var first = 1;
                > ```
                >
                > ```bash
                > echo second
                > ```
                """;

            var view = new MarkdownView { Markdown = initial, Margin = new(12) };
            var window = new Window { Width = 720, Height = 500, Content = view, Background = Brushes.Black };
            window.Show();
            await HeadlessApp.Settle();
            view.ApplyLatest();
            await HeadlessApp.Settle();

            var blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(2, blocks.Count);
            var wrap1 = blocks[1].GetVisualChildren().OfType<IconButton>()
                .Single(b => AutomationProperties.GetName(b) == "Wrap lines");
            wrap1.PerformClick();
            await HeadlessApp.Settle();
            Assert.False(blocks[0].Wrap);
            Assert.True(blocks[1].Wrap);

            // 1. 整体平移（前面插入新段落）
            var shifted = "Added paragraph above.\n\n" + initial;
            await Refresh(view, shifted);
            blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.False(blocks[0].Wrap);
            Assert.True(blocks[1].Wrap);

            // 2. 同引用内前面段落文字被编辑（块数和片段数未变）
            var editedInside = shifted.Replace("Quote intro:", "Quote intro edited with more text:");
            await Refresh(view, editedInside);
            blocks = view.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.False(blocks[0].Wrap);
            Assert.True(blocks[1].Wrap);

            window.Close();
        });
    }

    [Fact]
    public async Task Transcript_TopLevelChunking_AndNestedWrapVirtualization_BothWork()
    {
        await HeadlessApp.Run(async () =>
        {
            var longCode = string.Join("\n", Enumerable.Range(0, 600).Select(i => $"var line{i} = {i};"));
            var filler = string.Concat(Enumerable.Range(0, 120).Select(i => $"Filler paragraph {i}.\n\n"));
            var markdown =
                "> Quote:\n>\n> ```csharp\n> var a = 1;\n> ```\n>\n> ```bash\n> echo b\n> ```\n\n"
                + "```csharp\n" + longCode + "\n```\n\n"
                + filler;

            var view = new TranscriptView();
            var window = new Window { Width = 900, Height = 600, Content = view };
            window.Show();
            var entry = MarkdownEntry.FromText(markdown);
            view.Entries.Add(entry);
            await entry.Document.WhenIdle();
            view.Panel.ApplyLatest();
            await HeadlessApp.Settle();
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();

            // 顶部：1 个引用 + 1 个折叠的长代码（单行）+ 120 个段落 = 122 行
            Assert.Equal(122, view.Panel.RowCount);

            var topCode = view.Panel.RowViews.OfType<CodeBlockView>().Single();
            Assert.True(topCode.IsCollapsed);

            var quote = view.Panel.RowViews.OfType<QuoteBlockView>().Single();
            var nestedCodes = quote.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(2, nestedCodes.Count);
            nestedCodes[0].GetVisualChildren().OfType<IconButton>()
                .Single(b => AutomationProperties.GetName(b) == "Wrap lines")
                .PerformClick();
            await HeadlessApp.Settle();
            Assert.True(nestedCodes[0].Wrap);
            Assert.False(nestedCodes[1].Wrap);

            // 展开顶层长代码：重新拆成 5 段（122 + 4 = 126 行）
            topCode.ToggleExpanded();
            await HeadlessApp.Settle();
            Assert.Equal(126, view.Panel.RowCount);

            // 滚到底部回收引用，再滚回顶部：嵌套代码块的独立 Wrap 状态仍保留
            view.ScrollToEnd();
            await HeadlessApp.Settle();
            Assert.Empty(view.Panel.RowViews.OfType<QuoteBlockView>());
            view.Offset = new(0, 0);
            await HeadlessApp.Settle();

            var recreatedQuote = view.Panel.RowViews.OfType<QuoteBlockView>().Single();
            var recreatedNested = recreatedQuote.GetVisualDescendants().OfType<CodeBlockView>().ToList();
            Assert.Equal(2, recreatedNested.Count);
            Assert.True(recreatedNested[0].Wrap);
            Assert.False(recreatedNested[1].Wrap);

            window.Close();
        });
    }
}

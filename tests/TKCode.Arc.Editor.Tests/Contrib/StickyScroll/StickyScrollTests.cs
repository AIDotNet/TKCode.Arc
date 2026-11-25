// Tests of sticky scroll, ported from vscode (src/vs/editor/contrib/stickyScroll/test/browser/stickyScroll.test.ts,
// commit b57d5c9e), MIT license: all four cases. Upstream runs them in a test code editor of height 500; here the
// editor is the engine's TestCodeEditor (OuterHeight 500, line height 19) seen through StickyScrollViewModelEditor, and
// findScrollWidgetState is StickyScrollState.Find with the controller's limit (25% of the height, at most maxLineCount).
// Own tests of the folding fallbacks follow.

using Shouldly;
using TKCode.Arc.Editor.Contrib.StickyScroll;
using TKCode.Arc.Editor.ViewModel;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.StickyScroll;

public sealed class StickyScrollTests
{
    private const int LineHeight = 19;

    private static readonly string Text = string.Join('\n',
        "function foo() {",
        "",
        "}",
        "/* comment related to TestClass",
        " end of the comment */",
        "@classDecorator",
        "class TestClass {",
        "// comment related to the function functionOfClass",
        "functionOfClass(){",
        "function function1(){",
        "}",
        "}}",
        "function bar() { function insideBar() {}",
        "}");

    private static readonly string TextWithScopesWithSameStartingLines = string.Join('\n',
        "class TestClass { foo() {",
        "function bar(){",
        "",
        "}}",
        "}",
        "");

    private static TextRange R(int startLineNumber, int endLineNumber) => new(startLineNumber, 1, endLineNumber, 1);

    private static DocumentSymbol Symbol(string name, SymbolKind kind, TextRange range, TextRange selectionRange, params DocumentSymbol[] children) =>
        new(name, name, kind, range, selectionRange) { Children = children };

    private static IReadOnlyList<DocumentSymbol> DocumentSymbolsForTestModel() =>
    [
        Symbol("foo", SymbolKind.Function, R(1, 3), R(1, 1)),
        Symbol("TestClass", SymbolKind.Class, R(4, 12), R(7, 7),
            Symbol("functionOfClass", SymbolKind.Function, R(8, 12), R(9, 9),
                Symbol("function1", SymbolKind.Function, R(10, 11), R(10, 10)))),
        Symbol("bar", SymbolKind.Function, R(13, 14), R(13, 13),
            Symbol("insideBar", SymbolKind.Function, R(13, 13), R(13, 13))),
    ];

    private static IReadOnlyList<DocumentSymbol> DocumentSymbolsForSecondTestModel() =>
    [
        Symbol("TestClass", SymbolKind.Class, R(1, 5), R(1, 1),
            Symbol("foo", SymbolKind.Function, R(1, 4), R(1, 1),
                Symbol("bar", SymbolKind.Function, R(2, 4), R(2, 2)))),
    ];

    private sealed class Fixture : IDisposable
    {
        public Fixture(string text, LanguageFeatures? features = null)
        {
            Editor = TestCodeEditor.Create(text, new TestCodeEditorOptions { OuterHeight = 500 });
            Features = features ?? new LanguageFeatures();
            StickyEditor = new StickyScrollViewModelEditor(() => Editor.Model, () => Editor.ViewModel);
            Provider = new StickyLineCandidateProvider(StickyEditor, Features, Editor.LanguageConfigurationService);
        }

        public TestCodeEditor Editor { get; }

        public LanguageFeatures Features { get; }

        public StickyScrollViewModelEditor StickyEditor { get; }

        public StickyLineCandidateProvider Provider { get; }

        // upstream's StickyScrollController: _maxStickyLines is 25% of the editor height in lines, then maxLineCount
        public int MaxStickyLines => Math.Min((int)Math.Round(500.0 / LineHeight * 0.25, MidpointRounding.AwayFromZero), 5);

        public void SetScrollTop(double scrollTop) =>
            Editor.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: scrollTop), ScrollType.Immediate);

        public IReadOnlyList<int> StartLineNumbers() => StickyScrollState.Find(StickyEditor, Provider, MaxStickyLines).StartLineNumbers;

        public void Dispose()
        {
            Provider.Dispose();
            Editor.Dispose();
        }
    }

    private sealed class SymbolProvider(Func<IReadOnlyList<DocumentSymbol>> symbols) : IDocumentSymbolProvider
    {
        public Task<IReadOnlyList<DocumentSymbol>?> ProvideDocumentSymbols(TextModel model, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<DocumentSymbol>?>(symbols());
    }

    [Fact]
    public async Task Testing_the_function_getCandidateStickyLinesIntersecting()
    {
        using var fixture = new Fixture(Text);
        using var registration = fixture.Features.DocumentSymbolProvider.Register("*", new SymbolProvider(DocumentSymbolsForTestModel));
        var provider = fixture.Provider;
        await provider.Update();
        provider.GetCandidateStickyLinesIntersecting(new StickyRange(1, 4)).ShouldBe([new StickyLineCandidate(1, 2, 0, 19)]);
        provider.GetCandidateStickyLinesIntersecting(new StickyRange(8, 10)).ShouldBe([new StickyLineCandidate(7, 11, 0, 19), new StickyLineCandidate(9, 11, 19, 19)]);
        provider.GetCandidateStickyLinesIntersecting(new StickyRange(10, 13)).ShouldBe([new StickyLineCandidate(7, 11, 0, 19), new StickyLineCandidate(9, 11, 19, 19)]);
    }

    [Fact]
    public async Task Issue_157180_render_the_correct_line_corresponding_to_the_scope_definition()
    {
        using var fixture = new Fixture(Text);
        using var registration = fixture.Features.DocumentSymbolProvider.Register("*", new SymbolProvider(DocumentSymbolsForTestModel));
        await fixture.Provider.Update();

        fixture.SetScrollTop(1);
        fixture.StartLineNumbers().ShouldBe([1]);

        fixture.SetScrollTop(LineHeight + 1);
        fixture.StartLineNumbers().ShouldBe([1]);

        fixture.SetScrollTop((4 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([]);

        fixture.SetScrollTop((8 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([7, 9]);

        fixture.SetScrollTop((9 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([7, 9]);

        fixture.SetScrollTop((10 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([7]);
    }

    [Fact]
    public async Task Issue_156268_do_not_reveal_sticky_lines_when_they_are_in_a_folded_region()
    {
        using var fixture = new Fixture(Text);
        using var registration = fixture.Features.DocumentSymbolProvider.Register("*", new SymbolProvider(DocumentSymbolsForTestModel));
        await fixture.Provider.Update();
        fixture.Editor.ViewModel.SetHiddenAreas([R(2, 2), R(10, 11)]);

        fixture.SetScrollTop(1);
        fixture.StartLineNumbers().ShouldBe([1]);

        fixture.SetScrollTop(LineHeight + 1);
        fixture.StartLineNumbers().ShouldBe([]);

        fixture.SetScrollTop((6 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([7, 9]);

        fixture.SetScrollTop((7 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([7]);

        fixture.SetScrollTop((10 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([]);
    }

    [Fact]
    public async Task Issue_159271_render_the_correct_widget_state_when_the_child_scope_starts_on_the_same_line_as_the_parent_scope()
    {
        using var fixture = new Fixture(TextWithScopesWithSameStartingLines);
        await fixture.Provider.Update();
        using var registration = fixture.Features.DocumentSymbolProvider.Register("*", new SymbolProvider(DocumentSymbolsForSecondTestModel));
        await fixture.Provider.Update();

        fixture.SetScrollTop(1);
        fixture.StartLineNumbers().ShouldBe([1, 2]);

        fixture.SetScrollTop(LineHeight + 1);
        fixture.StartLineNumbers().ShouldBe([1, 2]);

        fixture.SetScrollTop((2 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([1]);

        fixture.SetScrollTop((3 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([1]);

        fixture.SetScrollTop((4 * LineHeight) + 1);
        fixture.StartLineNumbers().ShouldBe([]);
    }

    // ---- own tests

    [Fact]
    public async Task Without_symbols_the_scopes_come_from_indentation()
    {
        using var fixture = new Fixture(string.Join('\n',
            "class A {",
            "    void foo() {",
            "        a();",
            "        b();",
            "        c();",
            "    }",
            "}"));
        await fixture.Provider.Update();
        fixture.Provider.Model!.OutlineProviderId.ShouldBeNull();

        fixture.SetScrollTop((2 * LineHeight) + 1);
        var state = StickyScrollState.Find(fixture.StickyEditor, fixture.Provider, fixture.MaxStickyLines);
        state.StartLineNumbers.ShouldBe([1, 2]);
        // the end lines are those of the scopes' last lines plus one (upstream's endLineNumbers)
        state.EndLineNumbers.ShouldBe([7, 6]);
    }

    [Fact]
    public async Task Folding_range_providers_come_before_indentation()
    {
        using var fixture = new Fixture(string.Join('\n', "a", "b", "c", "d", "e", "f"));
        using var registration = fixture.Features.FoldingRangeProvider.Register("*", new Folding([new FoldingRange(1, 5)]));
        await fixture.Provider.Update();

        fixture.Provider.GetCandidateStickyLinesIntersecting(new StickyRange(2, 3)).ShouldBe([new StickyLineCandidate(1, 5, 0, 19)]);
    }

    [Fact]
    public async Task The_number_of_sticky_lines_is_limited_and_the_last_one_moves_up_at_the_end_of_its_scope()
    {
        using var fixture = new Fixture(string.Join('\n',
            "a",
            " b",
            "  c",
            "   d",
            "    e",
            "     f",
            "      g",
            "       h",
            "       h",
            "      g",
            "x"));
        await fixture.Provider.Update();
        fixture.SetScrollTop((5 * LineHeight) + 1);
        var state = StickyScrollState.Find(fixture.StickyEditor, fixture.Provider, maxNumberStickyLines: 3);
        state.StartLineNumbers.ShouldBe([1, 2, 3]);

        // scrolled so that the end of the scope of line 1 (line 10) is just under the first sticky line
        fixture.SetScrollTop((9 * LineHeight) + 5);
        state = StickyScrollState.Find(fixture.StickyEditor, fixture.Provider, maxNumberStickyLines: 5);
        state.StartLineNumbers.ShouldBe([1]);
        state.LastLineRelativePosition.ShouldBe(-5);
    }

    private sealed class Folding(IReadOnlyList<FoldingRange> ranges) : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<FoldingRange>?>(ranges);
    }
}

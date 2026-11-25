// Ported from vscode (src/vs/editor/test/common/model/model.line.test.ts, commit b57d5c9e), MIT license.
// Differences: the cases that upstream generates in loops are one theory per table, keyed by the upstream test name; the
// ManualTokenizationSupport is an IBackgroundTokenizerFactory (upstream's optional createBackgroundTokenizer) and goes
// into the model's own registry; the expected tokens are compared as (end offset, class name), which is what upstream's
// TestLineTokenFactory.inflateArr and getType give. The suite 'ModelLine - getIndentLevel' is in
// Model/GuidesTextModelPartTests.cs.

using Shouldly;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelLinesTokensTests
{
    private sealed record TestToken(int StartOffset, int Color);

    private sealed record BufferLineState(string Text, TestToken[] Tokens);

    private sealed record Edit(TextRange Range, string Text);

    private sealed record LineEdit(int StartColumn, int EndColumn, string Text);

    private sealed record ApplyEditsCase(string Name, BufferLineState[] Initial, Edit[] Edits, BufferLineState[] Expected);

    private sealed record LineEditCase(string Name, string InitialText, TestToken[] InitialTokens, LineEdit[] Edits, string ExpectedText, TestToken[] ExpectedTokens);

    private sealed record LineSplitCase(string Name, int SplitColumn, string ExpectedText1, string ExpectedText2, TestToken[] ExpectedTokens);

    private sealed record LineAppendCase(string Name, string AText, TestToken[] ATokens, string BText, TestToken[] BTokens, string ExpectedText, TestToken[] ExpectedTokens);

    private static TestToken T(int startOffset, int color) => new(startOffset, color);

    private static BufferLineState L(string text, TestToken[] tokens) => new(text, tokens);

    private static Edit E(int startLine, int startColumn, int endLine, int endColumn, string text) =>
        new(new TextRange(startLine, startColumn, endLine, endColumn), text);

    private static LineEdit LE(int startColumn, int endColumn, string text) => new(startColumn, endColumn, text);

    private static ApplyEditsCase ApplyCase(string name, BufferLineState[] initial, Edit[] edits, BufferLineState[] expected) =>
        new(name, initial, edits, expected);

    private static LineEditCase EditCase(string name, string initialText, TestToken[] initialTokens, LineEdit[] edits, string expectedText, TestToken[] expectedTokens) =>
        new(name, initialText, initialTokens, edits, expectedText, expectedTokens);

    private static LineSplitCase SplitCase(string name, int splitColumn, string expectedText1, string expectedText2, TestToken[] expectedTokens) =>
        new(name, splitColumn, expectedText1, expectedText2, expectedTokens);

    private static LineAppendCase AppendCase(string name, string aText, TestToken[] aTokens, string bText, TestToken[] bTokens, string expectedText, TestToken[] expectedTokens) =>
        new(name, aText, aTokens, bText, bTokens, expectedText, expectedTokens);

    private static uint[] ToTokens(TestToken[] tokens)
    {
        var result = new uint[tokens.Length << 1];
        for (var i = 0; i < tokens.Length; i++)
        {
            result[i << 1] = (uint)tokens[i].StartOffset;
            result[(i << 1) + 1] = (uint)tokens[i].Color << MetadataConsts.ForegroundOffset;
        }

        return result;
    }

    private static void AssertLineTokens(LineTokens actual, TestToken[] expectedTokens)
    {
        var tmp = ToTokens(expectedTokens);
        LineTokens.ConvertToEndOffset(tmp, actual.GetLineContent().Length);
        var expected = new List<(int EndIndex, string Type)>();
        for (var i = 0; i < tmp.Length; i += 2)
        {
            expected.Add(((int)tmp[i], TokenMetadata.GetClassNameFromMetadata(tmp[i + 1])));
        }

        var inflated = actual.Inflate();
        var actualTokens = new List<(int EndIndex, string Type)>();
        for (int i = 0, len = inflated.GetCount(); i < len; i++)
        {
            actualTokens.Add((inflated.GetEndOffset(i), inflated.GetClassName(i)));
        }

        actualTokens.ShouldBe(expected);
    }

    /// <summary>Upstream's ManualTokenizationSupport: the tests set each line's tokens through the background store.</summary>
    private sealed class ManualTokenizationSupport : ITokenizationSupport, IBackgroundTokenizerFactory
    {
        private readonly Dictionary<int, uint[]> _tokens = new();
        private readonly HashSet<IBackgroundTokenizationStore> _stores = new();

        public void SetLineTokens(int lineNumber, uint[] tokens)
        {
            var b = new ContiguousMultilineTokensBuilder();
            b.Add(lineNumber, tokens);
            foreach (var s in _stores)
            {
                s.SetTokens(b.FinalizeTokens());
            }
        }

        public IState GetInitialState() => new LineState(1);

        public TokenizationResult Tokenize(string line, bool hasEOL, IState state) => throw new NotSupportedException();

        public EncodedTokenizationResult TokenizeEncoded(string line, bool hasEOL, IState state)
        {
            var s = (LineState)state;
            return new EncodedTokenizationResult(_tokens[s.LineNumber], new LineState(s.LineNumber + 1));
        }

        public IBackgroundTokenizer? CreateBackgroundTokenizer(TextModel textModel, IBackgroundTokenizationStore store)
        {
            _stores.Add(store);
            return new BackgroundTokenizer(() => _stores.Remove(store));
        }

        private sealed class BackgroundTokenizer(Action dispose) : IBackgroundTokenizer
        {
            public void Dispose() => dispose();

            public void RequestTokens(int startLineNumber, int endLineNumberExclusive)
            {
            }
        }
    }

    private sealed class LineState(int lineNumber) : IState
    {
        public int LineNumber { get; } = lineNumber;

        public IState Clone() => this;

        public bool Equals(IState other) => ((LineState)other).LineNumber == LineNumber;
    }

    private static void TestApplyEdits(BufferLineState[] initial, Edit[] edits, BufferLineState[] expected)
    {
        var initialText = string.Join('\n', initial.Select(el => el.Text));

        var s = new ManualTokenizationSupport();
        using var model = TestTextModel.Create(initialText, "test");
        using var d = TestTokenizationSupports.Register(model, s);

        model.OnBeforeAttached();
        for (var lineIndex = 0; lineIndex < initial.Length; lineIndex++)
        {
            var lineTokens = initial[lineIndex].Tokens;
            var lineTextLength = model.GetLineMaxColumn(lineIndex + 1) - 1;
            var tokens = ToTokens(lineTokens);
            LineTokens.ConvertToEndOffset(tokens, lineTextLength);
            s.SetLineTokens(lineIndex + 1, tokens);
        }

        model.ApplyEdits(edits.Select(ed => new SingleEditOperation(ed.Range, ed.Text)).ToArray());

        for (var lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            var actualLine = model.GetLineContent(lineIndex + 1);
            var actualTokens = model.Tokenization.GetLineTokens(lineIndex + 1);
            actualLine.ShouldBe(expected[lineIndex].Text);
            AssertLineTokens(actualTokens, expected[lineIndex].Tokens);
        }
    }

    private static readonly ApplyEditsCase[] ApplyEditsCases =
    [
        ApplyCase("single delete 1", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 1, 1, 2, "")], [L("ello world", [T(0, 1), T(4, 2), T(5, 3)])]),
        ApplyCase("single delete 2", [L("helloworld", [T(0, 1), T(5, 2)])], [E(1, 3, 1, 8, "")], [L("herld", [T(0, 1), T(2, 2)])]),
        ApplyCase("single delete 3", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 1, 1, 6, "")], [L(" world", [T(0, 2), T(1, 3)])]),
        ApplyCase("single delete 4", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 2, 1, 7, "")], [L("hworld", [T(0, 1), T(1, 3)])]),
        ApplyCase("single delete 5", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 1, 1, 12, "")], [L("", [T(0, 1)])]),
        ApplyCase("multi delete 6", [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)]),
            L("hello world", [T(0, 7), T(5, 8), T(6, 9)])
        ], [E(1, 6, 3, 6, "")], [L("hello world", [T(0, 1), T(5, 8), T(6, 9)])]),
        ApplyCase("multi delete 7", [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)]),
            L("hello world", [T(0, 7), T(5, 8), T(6, 9)])
        ], [E(1, 12, 3, 12, "")], [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])]),
        ApplyCase("multi delete 8", [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)]),
            L("hello world", [T(0, 7), T(5, 8), T(6, 9)])
        ], [E(1, 1, 3, 1, "")], [L("hello world", [T(0, 7), T(5, 8), T(6, 9)])]),
        ApplyCase("multi delete 9", [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)]),
            L("hello world", [T(0, 7), T(5, 8), T(6, 9)])
        ], [E(1, 12, 3, 1, "")], [L("hello worldhello world", [T(0, 1), T(5, 2), T(6, 3), T(11, 7), T(16, 8), T(17, 9)])]),
        ApplyCase("single insert 1", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 1, 1, 1, "xx")], [L("xxhello world", [T(0, 1), T(7, 2), T(8, 3)])]),
        ApplyCase("single insert 2", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 2, 1, 2, "xx")], [L("hxxello world", [T(0, 1), T(7, 2), T(8, 3)])]),
        ApplyCase("single insert 3", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 6, 1, 6, "xx")], [L("helloxx world", [T(0, 1), T(7, 2), T(8, 3)])]),
        ApplyCase("single insert 4", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 7, 1, 7, "xx")], [L("hello xxworld", [T(0, 1), T(5, 2), T(8, 3)])]),
        ApplyCase("single insert 5", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 12, 1, 12, "xx")], [L("hello worldxx", [T(0, 1), T(5, 2), T(6, 3)])]),
        ApplyCase("multi insert 6", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 1, 1, 1, "\n")], [
            L("", [T(0, 1)]),
            L("hello world", [T(0, 1)])
        ]),
        ApplyCase("multi insert 7", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 12, 1, 12, "\n")], [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("", [T(0, 1)])
        ]),
        ApplyCase("multi insert 8", [L("hello world", [T(0, 1), T(5, 2), T(6, 3)])], [E(1, 7, 1, 7, "\n")], [
            L("hello ", [T(0, 1), T(5, 2)]),
            L("world", [T(0, 1)])
        ]),
        ApplyCase("multi insert 9", [
            L("hello world", [T(0, 1), T(5, 2), T(6, 3)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)])
        ], [E(1, 7, 1, 7, "xx\nyy")], [
            L("hello xx", [T(0, 1), T(5, 2)]),
            L("yyworld", [T(0, 1)]),
            L("hello world", [T(0, 4), T(5, 5), T(6, 6)])
            ]),
        ];

    private static readonly LineEditCase[] LineEditCases =
    [
        EditCase("updates tokens on insertion 1", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 1, "a")], "aabcd efgh", [T(0, 1), T(5, 2), T(6, 3)]),
        EditCase("updates tokens on insertion 2", "aabcd efgh", [T(0, 1), T(5, 2), T(6, 3)], [LE(2, 2, "x")], "axabcd efgh", [T(0, 1), T(6, 2), T(7, 3)]),
        EditCase("updates tokens on insertion 3", "axabcd efgh", [T(0, 1), T(6, 2), T(7, 3)], [LE(3, 3, "stu")], "axstuabcd efgh", [T(0, 1), T(9, 2), T(10, 3)]),
        EditCase("updates tokens on insertion 4", "axstuabcd efgh", [T(0, 1), T(9, 2), T(10, 3)], [LE(10, 10, "\t")], "axstuabcd\t efgh", [T(0, 1), T(10, 2), T(11, 3)]),
        EditCase("updates tokens on insertion 5", "axstuabcd\t efgh", [T(0, 1), T(10, 2), T(11, 3)], [LE(12, 12, "dd")], "axstuabcd\t ddefgh", [T(0, 1), T(10, 2), T(13, 3)]),
        EditCase("updates tokens on insertion 6", "axstuabcd\t ddefgh", [T(0, 1), T(10, 2), T(13, 3)], [LE(18, 18, "xyz")], "axstuabcd\t ddefghxyz", [T(0, 1), T(10, 2), T(13, 3)]),
        EditCase("updates tokens on insertion 7", "axstuabcd\t ddefghxyz", [T(0, 1), T(10, 2), T(13, 3)], [LE(1, 1, "x")], "xaxstuabcd\t ddefghxyz", [T(0, 1), T(11, 2), T(14, 3)]),
        EditCase("updates tokens on insertion 8", "xaxstuabcd\t ddefghxyz", [T(0, 1), T(11, 2), T(14, 3)], [LE(22, 22, "x")], "xaxstuabcd\t ddefghxyzx", [T(0, 1), T(11, 2), T(14, 3)]),
        EditCase("updates tokens on insertion 9", "xaxstuabcd\t ddefghxyzx", [T(0, 1), T(11, 2), T(14, 3)], [LE(2, 2, "")], "xaxstuabcd\t ddefghxyzx", [T(0, 1), T(11, 2), T(14, 3)]),
        EditCase("updates tokens on insertion 10", "", [], [LE(1, 1, "a")], "a", [T(0, 1)]),
        EditCase("delete second token 2", "abcdefghij", [T(0, 1), T(3, 2), T(6, 3)], [LE(4, 7, "")], "abcghij", [T(0, 1), T(3, 3)]),
        EditCase("insert right before second token", "abcdefghij", [T(0, 1), T(3, 2), T(6, 3)], [LE(4, 4, "hello")], "abchellodefghij", [T(0, 1), T(8, 2), T(11, 3)]),
        EditCase("delete first char", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 2, "")], "bcd efgh", [T(0, 1), T(3, 2), T(4, 3)]),
        EditCase("delete 2nd and 3rd chars", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(2, 4, "")], "ad efgh", [T(0, 1), T(2, 2), T(3, 3)]),
        EditCase("delete first token", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 5, "")], " efgh", [T(0, 2), T(1, 3)]),
        EditCase("delete second token", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(5, 6, "")], "abcdefgh", [T(0, 1), T(4, 3)]),
        EditCase("delete second token + a bit of the third one", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(5, 7, "")], "abcdfgh", [T(0, 1), T(4, 3)]),
        EditCase("delete second and third token", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(5, 10, "")], "abcd", [T(0, 1)]),
        EditCase("delete everything", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 10, "")], "", [T(0, 1)]),
        EditCase("noop", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 1, "")], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)]),
        EditCase("equivalent to deleting first two chars", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(1, 3, "")], "cd efgh", [T(0, 1), T(2, 2), T(3, 3)]),
        EditCase("equivalent to deleting from 5 to the end", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], [LE(5, 10, "")], "abcd", [T(0, 1)]),
        EditCase("updates tokens on replace 1", "Hello world, ciao", [T(0, 1), T(5, 0), T(6, 2), T(11, 0), T(13, 0)], [LE(1, 6, "Hi")], "Hi world, ciao", [T(0, 0), T(3, 2), T(8, 0), T(10, 0)]),
        EditCase("updates tokens on replace 2", "Hello world, ciao", [T(0, 1), T(5, 0), T(6, 2), T(11, 0), T(13, 0)], [LE(1, 6, "Hi"), LE(8, 12, "my friends")], "Hi wmy friends, ciao", [T(0, 0), T(3, 2), T(14, 0), T(16, 0)]),
        ];

    private static readonly LineSplitCase[] LineSplitCases =
    [
        SplitCase("split at the beginning", 1, "", "abcd efgh", [T(0, 1)]),
        SplitCase("split at the end", 10, "abcd efgh", "", [T(0, 1), T(4, 2), T(5, 3)]),
        SplitCase("split inthe middle 1", 5, "abcd", " efgh", [T(0, 1)]),
        SplitCase("split inthe middle 2", 6, "abcd ", "efgh", [T(0, 1), T(4, 2)]),
        ];

    private static readonly LineAppendCase[] LineAppendCases =
    [
        AppendCase("append empty 1", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], "", [], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)]),
        AppendCase("append empty 2", "", [], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)]),
        AppendCase("append 1", "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)], "abcd efgh", [T(0, 4), T(4, 5), T(5, 6)], "abcd efghabcd efgh", [T(0, 1), T(4, 2), T(5, 3), T(9, 4), T(13, 5), T(14, 6)]),
        AppendCase("append 2", "abcd ", [T(0, 1), T(4, 2)], "efgh", [T(0, 3)], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)]),
        AppendCase("append 3", "abcd", [T(0, 1)], " efgh", [T(0, 2), T(1, 3)], "abcd efgh", [T(0, 1), T(4, 2), T(5, 3)]),
        ];

    public static TheoryData<string> ApplyEditsNames => [.. ApplyEditsCases.Select(c => c.Name)];

    public static TheoryData<string> LineEditNames => [.. LineEditCases.Select(c => c.Name)];

    public static TheoryData<string> LineSplitNames => [.. LineSplitCases.Select(c => c.Name)];

    public static TheoryData<string> LineAppendNames => [.. LineAppendCases.Select(c => c.Name)];

    [Theory]
    [MemberData(nameof(ApplyEditsNames))]
    public void Apply_edits(string name)
    {
        var c = ApplyEditsCases.Single(x => x.Name == name);
        TestApplyEdits(c.Initial, c.Edits, c.Expected);
    }

    [Theory]
    [MemberData(nameof(LineEditNames))]
    public void Line_edit_tokens(string name)
    {
        var c = LineEditCases.Single(x => x.Name == name);
        TestApplyEdits(
            [L(c.InitialText, c.InitialTokens)],
            c.Edits.Select(ed => new Edit(new TextRange(1, ed.StartColumn, 1, ed.EndColumn), ed.Text)).ToArray(),
            [L(c.ExpectedText, c.ExpectedTokens)]);
    }

    [Theory]
    [MemberData(nameof(LineSplitNames))]
    public void Line_split_tokens(string name)
    {
        var c = LineSplitCases.Single(x => x.Name == name);
        TestApplyEdits(
            [L("abcd efgh", [T(0, 1), T(4, 2), T(5, 3)])],
            [new Edit(new TextRange(1, c.SplitColumn, 1, c.SplitColumn), "\n")],
            [L(c.ExpectedText1, c.ExpectedTokens), L(c.ExpectedText2, [T(0, 1)])]);
    }

    [Theory]
    [MemberData(nameof(LineAppendNames))]
    public void Line_append_tokens(string name)
    {
        var c = LineAppendCases.Single(x => x.Name == name);
        TestApplyEdits(
            [L(c.AText, c.ATokens), L(c.BText, c.BTokens)],
            [new Edit(new TextRange(1, c.AText.Length + 1, 2, 1), string.Empty)],
            [L(c.ExpectedText, c.ExpectedTokens)]);
    }

    [Fact]
    public void Insertion_on_empty_line()
    {
        var s = new ManualTokenizationSupport();
        using var model = TestTextModel.Create("some text", "test");
        using var d = TestTokenizationSupports.Register(model, s);
        var tokens = ToTokens([T(0, 1)]);
        LineTokens.ConvertToEndOffset(tokens, model.GetLineMaxColumn(1) - 1);
        s.SetLineTokens(1, tokens);

        model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 10), string.Empty)]);

        s.SetLineTokens(1, []);

        model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "a")]);

        var actualTokens = model.Tokenization.GetLineTokens(1);
        AssertLineTokens(actualTokens, [T(0, 1)]);
    }
}

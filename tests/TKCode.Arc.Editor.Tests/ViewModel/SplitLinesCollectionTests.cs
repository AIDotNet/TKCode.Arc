// Ported from vscode (src/vs/editor/test/browser/viewModel/modelLineProjection.test.ts: suite 'SplitLinesCollection',
// commit b57d5c9e), MIT license. See ModelLineProjectionTests.cs for how the TestConfiguration is replaced. Upstream
// registers a tokenizer that hands out the tokens line by line; here the model's test tokenizer answers by line number.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SplitLinesCollectionTests : IDisposable
{
    private static readonly string[] Text =
    [
        "class Nice {",
        "\tfunction hi() {",
        "\t\tconsole.log(\"Hello world\");",
        "\t}",
        "\tfunction hello() {",
        "\t\tconsole.log(\"Hello world, this is a somewhat longer line\");",
        "\t}",
        "}",
    ];

    private static readonly (int StartIndex, int Value)[][] Tokens =
    [
        [(0, 1), (5, 2), (6, 3), (10, 4)],
        [(0, 5), (1, 6), (9, 7), (10, 8), (12, 9)],
        [(0, 10), (2, 11), (9, 12), (10, 13), (13, 14), (14, 15), (27, 16)],
        [(0, 17)],
        [(0, 18), (1, 19), (9, 20), (10, 21), (15, 22)],
        [(0, 23), (2, 24), (9, 25), (10, 26), (13, 27), (14, 28), (59, 29)],
        [(0, 30)],
        [(0, 31)],
    ];

    private readonly TextModel _model;

    public SplitLinesCollectionTests()
    {
        _model = TestTextModel.Create(string.Join('\n', Text));
        TestTokenizationSupports.RegisterByLine(_model, (lineNumber, line) =>
        {
            var tokens = Tokens[lineNumber - 1];
            var result = new uint[2 * tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                result[2 * i] = (uint)tokens[i].StartIndex;
                result[2 * i + 1] = (uint)tokens[i].Value << MetadataConsts.ForegroundOffset;
            }

            return result;
        });
        // force tokenization
        _model.Tokenization.ForceTokenization(_model.GetLineCount());
    }

    public void Dispose() => _model.Dispose();

    /// <summary>Upstream's ITestMinimapLineRenderingData: the content, columns and (endIndex, value) tokens of a view line.</summary>
    private sealed record Expected(string Content, int MinColumn, int MaxColumn, (int EndIndex, int Value)[] Tokens);

    private static void AssertViewLineTokens(IViewLineTokens actual, (int EndIndex, int Value)[] expected)
    {
        var actualTokens = new (int EndIndex, int Value)[actual.GetCount()];
        for (int i = 0, len = actual.GetCount(); i < len; i++)
        {
            actualTokens[i] = (actual.GetEndOffset(i), (int)actual.GetForeground(i));
        }

        actualTokens.ShouldBe(expected);
    }

    private static void AssertMinimapLineRenderingData(ViewLineData? actual, Expected? expected)
    {
        if (actual is null && expected is null)
        {
            return;
        }

        expected.ShouldNotBeNull();
        actual.ShouldNotBeNull();
        actual.Content.ShouldBe(expected.Content);
        actual.MinColumn.ShouldBe(expected.MinColumn);
        actual.MaxColumn.ShouldBe(expected.MaxColumn);
        AssertViewLineTokens(actual.Tokens, expected.Tokens);
    }

    private static void AssertMinimapLinesRenderingData(ViewLineData?[] actual, Expected?[] expected)
    {
        actual.Length.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertMinimapLineRenderingData(actual[i], expected[i]);
        }
    }

    private static void AssertAllMinimapLinesRenderingData(ViewModelLinesFromProjectedModel splitLinesCollection, Expected[] all)
    {
        var lineCount = all.Length;
        for (var line = 1; line <= lineCount; line++)
        {
            splitLinesCollection.GetViewLineData(line).Content.ShouldBe(splitLinesCollection.GetViewLineContent(line));
        }

        for (var start = 1; start <= lineCount; start++)
        {
            for (var end = start; end <= lineCount; end++)
            {
                var count = end - start + 1;
                // Upstream loops over every combination of needed lines and breaks after the first (all
                // needed); "Comment out next line to test all possible combinations". Here: the first only.
                var desired = (1 << count) - 1;
                {
                    var needed = new bool[count];
                    var expected = new Expected?[count];
                    for (var i = 0; i < count; i++)
                    {
                        needed[i] = (desired & (1 << i)) != 0;
                        expected[i] = needed[i] ? all[start - 1 + i] : null;
                    }

                    var actual = splitLinesCollection.GetViewLinesData(start, end, needed);

                    AssertMinimapLinesRenderingData(actual, expected);
                }
            }
        }
    }

    private static readonly Expected[] NoWrappingExpected =
    [
        new("class Nice {", 1, 13, [(5, 1), (6, 2), (10, 3), (12, 4)]),
        new("\tfunction hi() {", 1, 17, [(1, 5), (9, 6), (10, 7), (12, 8), (16, 9)]),
        new("\t\tconsole.log(\"Hello world\");", 1, 30, [(2, 10), (9, 11), (10, 12), (13, 13), (14, 14), (27, 15), (29, 16)]),
        new("\t}", 1, 3, [(2, 17)]),
        new("\tfunction hello() {", 1, 20, [(1, 18), (9, 19), (10, 20), (15, 21), (19, 22)]),
        new("\t\tconsole.log(\"Hello world, this is a somewhat longer line\");", 1, 62, [(2, 23), (9, 24), (10, 25), (13, 26), (14, 27), (59, 28), (61, 29)]),
        new("\t}", 1, 3, [(2, 30)]),
        new("}", 1, 2, [(1, 31)]),
    ];

    private static readonly Expected[] WrappingExpected =
    [
        new("class Nice {", 1, 13, [(5, 1), (6, 2), (10, 3), (12, 4)]),
        new("\tfunction hi() {", 1, 17, [(1, 5), (9, 6), (10, 7), (12, 8), (16, 9)]),
        new("\t\tconsole.log(\"Hello ", 1, 22, [(2, 10), (9, 11), (10, 12), (13, 13), (14, 14), (21, 15)]),
        new("            world\");", 13, 21, [(18, 15), (20, 16)]),
        new("\t}", 1, 3, [(2, 17)]),
        new("\tfunction hello() {", 1, 20, [(1, 18), (9, 19), (10, 20), (15, 21), (19, 22)]),
        new("\t\tconsole.log(\"Hello ", 1, 22, [(2, 23), (9, 24), (10, 25), (13, 26), (14, 27), (21, 28)]),
        new("            world, this is a ", 13, 30, [(29, 28)]),
        new("            somewhat longer ", 13, 29, [(28, 28)]),
        new("            line\");", 13, 20, [(17, 28), (19, 29)]),
        new("\t}", 1, 3, [(2, 30)]),
        new("}", 1, 2, [(1, 31)]),
    ];

    private static readonly Expected[] InjectedExpected =
    [
        new("class Nivery very long ", 1, 24, [(5, 1), (6, 2), (8, 3), (23, 1)]),
        new("    injected text that causes ", 5, 31, [(30, 1)]),
        new("    a line breakce {", 5, 21, [(16, 1), (18, 3), (20, 4)]),
        new("\tfunction hi() {", 1, 17, [(1, 5), (9, 6), (10, 7), (12, 8), (16, 9)]),
        new("\t\tconsole.log(\"Hello ", 1, 22, [(2, 10), (9, 11), (10, 12), (13, 13), (14, 14), (21, 15)]),
        new("            world\");", 13, 21, [(18, 15), (20, 16)]),
        new("\t}", 1, 3, [(2, 17)]),
        new("\tfunction hello() {", 1, 20, [(1, 18), (9, 19), (10, 20), (15, 21), (19, 22)]),
        new("\t\tconsole.log(\"Hello ", 1, 22, [(2, 23), (9, 24), (10, 25), (13, 26), (14, 27), (21, 28)]),
        new("            world, this is a ", 13, 30, [(29, 28)]),
        new("            somewhat longer ", 13, 29, [(28, 28)]),
        new("            line\");", 13, 20, [(17, 28), (19, 29)]),
        new("\t}", 1, 3, [(2, 30)]),
        new("}", 1, 2, [(1, 31)]),
    ];

    [Fact]
    public void GetViewLinesData_No_Wrapping()
    {
        WithSplitLinesCollection(_model, "off", 0, false, splitLinesCollection =>
        {
            splitLinesCollection.GetViewLineCount().ShouldBe(8);
            for (var lineNumber = 1; lineNumber <= 8; lineNumber++)
            {
                splitLinesCollection.ModelPositionIsVisible(lineNumber, 1).ShouldBeTrue();
            }

            var expected = NoWrappingExpected;
            AssertAllMinimapLinesRenderingData(splitLinesCollection,
            [
                expected[0],
                expected[1],
                expected[2],
                expected[3],
                expected[4],
                expected[5],
                expected[6],
                expected[7],
            ]);

            splitLinesCollection.SetHiddenAreas([new TextRange(2, 1, 4, 1)]);
            splitLinesCollection.GetViewLineCount().ShouldBe(5);
            splitLinesCollection.ModelPositionIsVisible(1, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(2, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(3, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(4, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(5, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(6, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(7, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(8, 1).ShouldBeTrue();

            AssertAllMinimapLinesRenderingData(splitLinesCollection,
            [
                expected[0],
                expected[4],
                expected[5],
                expected[6],
                expected[7],
            ]);
        });
    }

    [Fact]
    public void GetViewLinesData_With_Wrapping()
    {
        WithSplitLinesCollection(_model, "wordWrapColumn", 30, false, splitLinesCollection =>
        {
            splitLinesCollection.GetViewLineCount().ShouldBe(12);
            for (var lineNumber = 1; lineNumber <= 8; lineNumber++)
            {
                splitLinesCollection.ModelPositionIsVisible(lineNumber, 1).ShouldBeTrue();
            }

            var expected = WrappingExpected;
            AssertAllMinimapLinesRenderingData(splitLinesCollection,
            [
                expected[0],
                expected[1],
                expected[2],
                expected[3],
                expected[4],
                expected[5],
                expected[6],
                expected[7],
                expected[8],
                expected[9],
                expected[10],
                expected[11],
            ]);

            splitLinesCollection.SetHiddenAreas([new TextRange(2, 1, 4, 1)]);
            splitLinesCollection.GetViewLineCount().ShouldBe(8);
            splitLinesCollection.ModelPositionIsVisible(1, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(2, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(3, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(4, 1).ShouldBeFalse();
            splitLinesCollection.ModelPositionIsVisible(5, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(6, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(7, 1).ShouldBeTrue();
            splitLinesCollection.ModelPositionIsVisible(8, 1).ShouldBeTrue();

            AssertAllMinimapLinesRenderingData(splitLinesCollection,
            [
                expected[0],
                expected[5],
                expected[6],
                expected[7],
                expected[8],
                expected[9],
                expected[10],
                expected[11],
            ]);
        });
    }

    [Fact]
    public void GetViewLinesData_With_Wrapping_And_Injected_Text()
    {
        _model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(
                new TextRange(1, 9, 1, 9),
                new ModelDecorationOptions("example")
                {
                    After = new ModelDecorationInjectedTextOptions("very very long injected text that causes a line break")
                    {
                        InlineClassName = "myClassName",
                    },
                    ShowIfCollapsed = true,
                }),
        ]);

        WithSplitLinesCollection(_model, "wordWrapColumn", 30, false, splitLinesCollection =>
        {
            splitLinesCollection.GetViewLineCount().ShouldBe(14);

            splitLinesCollection.GetViewLineMaxColumn(1).ShouldBe(24);

            var expected = InjectedExpected;
            AssertAllMinimapLinesRenderingData(splitLinesCollection,
            [
                expected[0],
                expected[1],
                expected[2],
                expected[3],
                expected[4],
                expected[5],
                expected[6],
                expected[7],
                expected[8],
                expected[9],
                expected[10],
                expected[11],
            ]);

            var data = splitLinesCollection.GetViewLinesData(1, 14, Enumerable.Repeat(true, 14).ToArray());
            var actual = data
                .Select(d => d!.InlineDecorations?.Select(dec => (StartOffset: dec.Range.StartColumn - 1, EndOffset: dec.Range.EndColumn - 1)).ToArray())
                .ToArray();
            actual.Length.ShouldBe(14);
            actual[0].ShouldBe([(8, 23)]);
            actual[1].ShouldBe([(4, 30)]);
            actual[2].ShouldBe([(4, 16)]);
            for (var i = 3; i < 14; i++)
            {
                actual[i].ShouldBeNull();
            }
        });
    }

    private static void WithSplitLinesCollection(TextModel model, string wordWrap, int wordWrapColumn, bool wrapOnEscapedLineFeeds, Action<ViewModelLinesFromProjectedModel> callback)
    {
        // TestConfiguration({ wordWrap, wordWrapColumn, wrappingIndent: 'indent' }).
        var wrappingColumn = wordWrap switch
        {
            "off" => -1,
            "wordWrapColumn" => wordWrapColumn,
            _ => throw new ArgumentOutOfRangeException(nameof(wordWrap), wordWrap, "Only 'off' and 'wordWrapColumn' are used here."),
        };
        var linesCollection = ModelLineProjectionTestUtils.CreateLinesCollection(model, wrappingColumn, WrappingIndent.Indent, wrapOnEscapedLineFeeds);

        callback(linesCollection);
    }
}

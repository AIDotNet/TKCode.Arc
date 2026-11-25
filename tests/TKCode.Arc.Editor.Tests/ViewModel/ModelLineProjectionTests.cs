// Ported from vscode (src/vs/editor/test/browser/viewModel/modelLineProjection.test.ts, commit b57d5c9e), MIT license.
// Suites 'Editor ViewModel - SplitLinesCollection' (ModelLineProjectionTests) and 'SplitLinesCollection'
// (SplitLinesCollectionTests). Upstream reads the settings from a TestConfiguration; here they are its values: the
// mock font (10px half-width, 20px full-width characters), wrapping column -1 for wordWrap 'off' and wordWrapColumn for
// 'wordWrapColumn', wrapping indent Same (or the given one), word break normal, no two-cell full-width characters.
// The model has no guides part yet, so 'Invalid line numbers' gives the lines a stand-in that computes each line's
// indent level from its leading whitespace, which is what upstream's guides give for that text. The tokens of the
// second suite come from the model's test tokenizer (by line number; upstream's tokenizer hands them out in order).

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

internal static class ModelLineProjectionTestUtils
{
    /// <summary>TestConfiguration's mock font.</summary>
    public static FontInfo TestFontInfo() => new(
        pixelRatio: 1,
        fontFamily: "mockFont",
        fontWeight: "normal",
        fontSize: 14,
        fontFeatureSettings: "\"liga\" off, \"calt\" off",
        fontVariationSettings: "normal",
        lineHeight: 19,
        letterSpacing: 1.5,
        isMonospace: true,
        typicalHalfwidthCharacterWidth: 10,
        typicalFullwidthCharacterWidth: 20,
        canUseHalfwidthRightwardsArrow: true,
        spaceWidth: 10,
        middotWidth: 10,
        wsmiddotWidth: 10,
        maxDigitWidth: 10,
        isTrusted: true);

    public static ViewModelLinesFromProjectedModel CreateLinesCollection(
        TextModel model,
        int wrappingColumn,
        WrappingIndent wrappingIndent,
        bool wrapOnEscapedLineFeeds,
        IGuidesTextModelPart? guides = null)
    {
        var lineBreaksComputerFactory = new MonospaceLineBreaksComputerFactory(WrappingOptions.WordWrapBreakBeforeCharacters, WrappingOptions.WordWrapBreakAfterCharacters);
        return new ViewModelLinesFromProjectedModel(
            1,
            model,
            lineBreaksComputerFactory,
            lineBreaksComputerFactory,
            TestFontInfo(),
            model.GetOptions().TabSize,
            WrappingStrategy.Simple,
            wrappingColumn,
            wrappingIndent,
            WordBreak.Normal,
            wrapOnEscapedLineFeeds,
            false,
            guides);
    }
}

public sealed class ModelLineProjectionTests
{
    [Fact]
    public void SplitLine()
    {
        var model1 = CreateModel("My First LineMy Second LineAnd another one");
        var line1 = CreateSplitLine([13, 14, 15], [13, 13 + 14, 13 + 14 + 15], 0);

        line1.GetViewLineCount().ShouldBe(3);
        line1.GetViewLineContent(model1, 1, 0).ShouldBe("My First Line");
        line1.GetViewLineContent(model1, 1, 1).ShouldBe("My Second Line");
        line1.GetViewLineContent(model1, 1, 2).ShouldBe("And another one");
        line1.GetViewLineMaxColumn(model1, 1, 0).ShouldBe(14);
        line1.GetViewLineMaxColumn(model1, 1, 1).ShouldBe(15);
        line1.GetViewLineMaxColumn(model1, 1, 2).ShouldBe(16);
        new[] { 0, 1, 2 }.Select(lineIndex => line1.GetViewLineContinuesWithWrappedLine(lineIndex)).ToArray().ShouldBe([true, true, false]);
        for (var col = 1; col <= 14; col++)
        {
            line1.GetModelColumnOfViewPosition(0, col).ShouldBe(col, $"getInputColumnOfOutputPosition(0, {col})");
        }

        for (var col = 1; col <= 15; col++)
        {
            line1.GetModelColumnOfViewPosition(1, col).ShouldBe(13 + col, $"getInputColumnOfOutputPosition(1, {col})");
        }

        for (var col = 1; col <= 16; col++)
        {
            line1.GetModelColumnOfViewPosition(2, col).ShouldBe(13 + 14 + col, $"getInputColumnOfOutputPosition(2, {col})");
        }

        for (var col = 1; col <= 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(0, col), $"getOutputPositionOfInputPosition({col})");
        }

        for (var col = 1 + 13; col <= 14 + 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(1, col - 13), $"getOutputPositionOfInputPosition({col})");
        }

        for (var col = 1 + 13 + 14; col <= 15 + 14 + 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(2, col - 13 - 14), $"getOutputPositionOfInputPosition({col})");
        }

        model1 = CreateModel("My First LineMy Second LineAnd another one");
        line1 = CreateSplitLine([13, 14, 15], [13, 13 + 14, 13 + 14 + 15], 4);

        line1.GetViewLineCount().ShouldBe(3);
        line1.GetViewLineContent(model1, 1, 0).ShouldBe("My First Line");
        line1.GetViewLineContent(model1, 1, 1).ShouldBe("    My Second Line");
        line1.GetViewLineContent(model1, 1, 2).ShouldBe("    And another one");
        line1.GetViewLineMaxColumn(model1, 1, 0).ShouldBe(14);
        line1.GetViewLineMaxColumn(model1, 1, 1).ShouldBe(19);
        line1.GetViewLineMaxColumn(model1, 1, 2).ShouldBe(20);

        var actualViewColumnMapping = new List<int[]>();
        for (var lineIndex = 0; lineIndex < line1.GetViewLineCount(); lineIndex++)
        {
            var actualLineViewColumnMapping = new List<int>();
            for (var col = 1; col <= line1.GetViewLineMaxColumn(model1, 1, lineIndex); col++)
            {
                actualLineViewColumnMapping.Add(line1.GetModelColumnOfViewPosition(lineIndex, col));
            }

            actualViewColumnMapping.Add(actualLineViewColumnMapping.ToArray());
        }

        actualViewColumnMapping.Count.ShouldBe(3);
        actualViewColumnMapping[0].ShouldBe([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]);
        actualViewColumnMapping[1].ShouldBe([14, 14, 14, 14, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28]);
        actualViewColumnMapping[2].ShouldBe([28, 28, 28, 28, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43]);

        for (var col = 1; col <= 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(0, col), $"6.getOutputPositionOfInputPosition({col})");
        }

        for (var col = 1 + 13; col <= 14 + 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(1, 4 + col - 13), $"7.getOutputPositionOfInputPosition({col})");
        }

        for (var col = 1 + 13 + 14; col <= 15 + 14 + 13; col++)
        {
            line1.GetViewPositionOfModelPosition(0, col).ShouldBe(Pos(2, 4 + col - 13 - 14), $"8.getOutputPositionOfInputPosition({col})");
        }
    }

    private static void WithSplitLinesCollection(string text, Action<TextModel, ViewModelLinesFromProjectedModel> callback)
    {
        using var model = TestTextModel.Create(text);
        // TestConfiguration({}): wordWrap 'off' (wrapping column -1), wrappingIndent 'same'.
        var linesCollection = ModelLineProjectionTestUtils.CreateLinesCollection(model, -1, WrappingIndent.Same, false, new IndentLevelGuides(model));

        callback(model, linesCollection);

        linesCollection.Dispose();
    }

    [Fact]
    public void Invalid_Line_Numbers()
    {
        var text = string.Join('\n',
            "int main() {",
            "\tprintf(\"Hello world!\");",
            "}",
            "int main() {",
            "\tprintf(\"Hello world!\");",
            "}");

        WithSplitLinesCollection(text, (model, linesCollection) =>
        {
            linesCollection.GetViewLineCount().ShouldBe(6);

            // getOutputIndentGuide
            linesCollection.GetViewLinesIndentGuides(-1, -1).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(0, 0).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(1, 1).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(2, 2).ShouldBe([1]);
            linesCollection.GetViewLinesIndentGuides(3, 3).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(4, 4).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(5, 5).ShouldBe([1]);
            linesCollection.GetViewLinesIndentGuides(6, 6).ShouldBe([0]);
            linesCollection.GetViewLinesIndentGuides(7, 7).ShouldBe([0]);

            linesCollection.GetViewLinesIndentGuides(0, 7).ShouldBe([0, 1, 0, 0, 1, 0]);

            // getOutputLineContent
            linesCollection.GetViewLineContent(-1).ShouldBe("int main() {");
            linesCollection.GetViewLineContent(0).ShouldBe("int main() {");
            linesCollection.GetViewLineContent(1).ShouldBe("int main() {");
            linesCollection.GetViewLineContent(2).ShouldBe("\tprintf(\"Hello world!\");");
            linesCollection.GetViewLineContent(3).ShouldBe("}");
            linesCollection.GetViewLineContent(4).ShouldBe("int main() {");
            linesCollection.GetViewLineContent(5).ShouldBe("\tprintf(\"Hello world!\");");
            linesCollection.GetViewLineContent(6).ShouldBe("}");
            linesCollection.GetViewLineContent(7).ShouldBe("}");

            // getOutputLineMinColumn
            for (var lineNumber = -1; lineNumber <= 7; lineNumber++)
            {
                linesCollection.GetViewLineMinColumn(lineNumber).ShouldBe(1);
            }

            // getOutputLineMaxColumn
            linesCollection.GetViewLineMaxColumn(-1).ShouldBe(13);
            linesCollection.GetViewLineMaxColumn(0).ShouldBe(13);
            linesCollection.GetViewLineMaxColumn(1).ShouldBe(13);
            linesCollection.GetViewLineMaxColumn(2).ShouldBe(25);
            linesCollection.GetViewLineMaxColumn(3).ShouldBe(2);
            linesCollection.GetViewLineMaxColumn(4).ShouldBe(13);
            linesCollection.GetViewLineMaxColumn(5).ShouldBe(25);
            linesCollection.GetViewLineMaxColumn(6).ShouldBe(2);
            linesCollection.GetViewLineMaxColumn(7).ShouldBe(2);

            // convertOutputPositionToInputPosition
            linesCollection.ConvertViewPositionToModelPosition(-1, 1).ShouldBe(new TextPosition(1, 1));
            linesCollection.ConvertViewPositionToModelPosition(0, 1).ShouldBe(new TextPosition(1, 1));
            linesCollection.ConvertViewPositionToModelPosition(1, 1).ShouldBe(new TextPosition(1, 1));
            linesCollection.ConvertViewPositionToModelPosition(2, 1).ShouldBe(new TextPosition(2, 1));
            linesCollection.ConvertViewPositionToModelPosition(3, 1).ShouldBe(new TextPosition(3, 1));
            linesCollection.ConvertViewPositionToModelPosition(4, 1).ShouldBe(new TextPosition(4, 1));
            linesCollection.ConvertViewPositionToModelPosition(5, 1).ShouldBe(new TextPosition(5, 1));
            linesCollection.ConvertViewPositionToModelPosition(6, 1).ShouldBe(new TextPosition(6, 1));
            linesCollection.ConvertViewPositionToModelPosition(7, 1).ShouldBe(new TextPosition(6, 1));
            linesCollection.ConvertViewPositionToModelPosition(8, 1).ShouldBe(new TextPosition(6, 1));
        });
    }

    [Fact]
    public void Issue_3662()
    {
        var text = string.Join('\n',
            "int main() {",
            "\tprintf(\"Hello world!\");",
            "}",
            "int main() {",
            "\tprintf(\"Hello world!\");",
            "}");

        WithSplitLinesCollection(text, (model, linesCollection) =>
        {
            linesCollection.SetHiddenAreas([
                new TextRange(1, 1, 3, 1),
                new TextRange(5, 1, 6, 1),
            ]);

            var viewLineCount = linesCollection.GetViewLineCount();
            viewLineCount.ShouldBe(1, "getOutputLineCount()");

            var modelLineCount = model.GetLineCount();
            for (var lineNumber = 0; lineNumber <= modelLineCount + 1; lineNumber++)
            {
                var lineMinColumn = lineNumber >= 1 && lineNumber <= modelLineCount ? model.GetLineMinColumn(lineNumber) : 1;
                var lineMaxColumn = lineNumber >= 1 && lineNumber <= modelLineCount ? model.GetLineMaxColumn(lineNumber) : 1;
                for (var column = lineMinColumn - 1; column <= lineMaxColumn + 1; column++)
                {
                    var viewPosition = linesCollection.ConvertModelPositionToViewPosition(lineNumber, column);

                    // validate view position
                    var viewLineNumber = viewPosition.LineNumber;
                    var viewColumn = viewPosition.Column;
                    if (viewLineNumber < 1)
                    {
                        viewLineNumber = 1;
                    }

                    var lineCount = linesCollection.GetViewLineCount();
                    if (viewLineNumber > lineCount)
                    {
                        viewLineNumber = lineCount;
                    }

                    var viewMinColumn = linesCollection.GetViewLineMinColumn(viewLineNumber);
                    var viewMaxColumn = linesCollection.GetViewLineMaxColumn(viewLineNumber);
                    if (viewColumn < viewMinColumn)
                    {
                        viewColumn = viewMinColumn;
                    }

                    if (viewColumn > viewMaxColumn)
                    {
                        viewColumn = viewMaxColumn;
                    }

                    var validViewPosition = new TextPosition(viewLineNumber, viewColumn);
                    viewPosition.ToString().ShouldBe(validViewPosition.ToString(), $"model->view for {lineNumber}, {column}");
                }
            }

            for (var lineNumber = 0; lineNumber <= viewLineCount + 1; lineNumber++)
            {
                var lineMinColumn = linesCollection.GetViewLineMinColumn(lineNumber);
                var lineMaxColumn = linesCollection.GetViewLineMaxColumn(lineNumber);
                for (var column = lineMinColumn - 1; column <= lineMaxColumn + 1; column++)
                {
                    var modelPosition = linesCollection.ConvertViewPositionToModelPosition(lineNumber, column);
                    var validModelPosition = model.ValidatePosition(modelPosition);
                    modelPosition.ToString().ShouldBe(validModelPosition.ToString(), $"view->model for {lineNumber}, {column}");
                }
            }
        });
    }

    internal static TextPosition Pos(int lineNumber, int column) => new(lineNumber, column);

    private static IModelLineProjection CreateSplitLine(int[] splitLengths, double[] breakingOffsetsVisibleColumn, int wrappedTextIndentWidth, bool isVisible = true) =>
        ModelLineProjections.Create(CreateLineBreakData(splitLengths, breakingOffsetsVisibleColumn, wrappedTextIndentWidth), isVisible);

    private static ModelLineProjectionData CreateLineBreakData(int[] breakingLengths, double[] breakingOffsetsVisibleColumn, int wrappedTextIndentWidth)
    {
        var sums = new int[breakingLengths.Length];
        for (var i = 0; i < breakingLengths.Length; i++)
        {
            sums[i] = (i > 0 ? sums[i - 1] : 0) + breakingLengths[i];
        }

        return new ModelLineProjectionData(null, null, sums, breakingOffsetsVisibleColumn, wrappedTextIndentWidth);
    }

    private static ISimpleModel CreateModel(string text) => new SimpleModel(text);

    private sealed class SimpleModel(string text) : ISimpleModel
    {
        public LineTokens GetLineTokens(int lineNumber) => null!;

        public string GetLineContent(int lineNumber) => text;

        public int GetLineLength(int lineNumber) => text.Length;

        public int GetLineMinColumn(int lineNumber) => 1;

        public int GetLineMaxColumn(int lineNumber) => text.Length + 1;

        public string GetValueInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined) =>
            text[(range.StartColumn - 1)..(range.EndColumn - 1)];
    }

    /// <summary>A stand-in for the model's guides: the indent level of each line from its leading whitespace.</summary>
    private sealed class IndentLevelGuides(TextModel model) : IGuidesTextModelPart
    {
        public ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber) => new(lineNumber, lineNumber, 0);

        public IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber)
        {
            var tabSize = model.GetOptions().TabSize;
            var indentSize = model.GetOptions().IndentSize;
            var result = new int[endLineNumber - startLineNumber + 1];
            for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
            {
                var line = model.GetLineContent(lineNumber);
                var visibleColumn = 0;
                foreach (var ch in line)
                {
                    if (ch == ' ')
                    {
                        visibleColumn++;
                    }
                    else if (ch == '\t')
                    {
                        visibleColumn += tabSize - (visibleColumn % tabSize);
                    }
                    else
                    {
                        break;
                    }
                }

                result[lineNumber - startLineNumber] = (int)Math.Ceiling(visibleColumn / (double)indentSize);
            }

            return result;
        }

        public IReadOnlyList<IReadOnlyList<IndentGuide>> GetLinesBracketGuides(int startLineNumber, int endLineNumber, TextPosition? activePosition, BracketGuideOptions options) =>
            Enumerable.Range(0, endLineNumber - startLineNumber + 1).Select(_ => (IReadOnlyList<IndentGuide>)[]).ToArray();
    }
}

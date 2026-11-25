// Ported from vscode (src/vs/editor/test/common/model/bracketPairColorizer/beforeEditPositionMapper.test.ts, commit
// b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Model.BracketPairs;
using Xunit;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Tests;

public sealed class BeforeEditPositionMapperTests
{
    [Fact]
    public void Single_Line_1()
    {
        Compute(
            ["0123456789"],
            [Edit(0, 4, 0, 7, "xy")])
            .ShouldBe(
            [
                "0  1  2  3  x  y  7  8  9  ",
                "0  0  0  0  0  0  0  0  0  0  ",
                "0  1  2  3  4  5  7  8  9  10 ",
                "0  0  0  0  0  0  ∞  ∞  ∞  ∞  ",
                "4  3  2  1  0  0  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Single_Line_2()
    {
        Compute(
            ["0123456789"],
            [Edit(0, 2, 0, 4, "xxxx"), Edit(0, 6, 0, 6, "yy")])
            .ShouldBe(
            [
                "0  1  x  x  x  x  4  5  y  y  6  7  8  9  ",
                "0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  ",
                "0  1  2  3  4  5  4  5  6  7  6  7  8  9  10 ",
                "0  0  0  0  0  0  0  0  0  0  ∞  ∞  ∞  ∞  ∞  ",
                "2  1  0  0  0  0  2  1  0  0  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Replace_1()
    {
        Compute(
            ["₀₁₂₃₄₅₆₇₈₉", "0123456789", "⁰¹²³⁴⁵⁶⁷⁸⁹"],
            [Edit(0, 3, 1, 3, "xy")])
            .ShouldBe(
            [
                "₀  ₁  ₂  x  y  3  4  5  6  7  8  9  ",
                "0  0  0  0  0  1  1  1  1  1  1  1  1  ",
                "0  1  2  3  4  3  4  5  6  7  8  9  10 ",
                "0  0  0  0  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "3  2  1  0  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "⁰  ¹  ²  ³  ⁴  ⁵  ⁶  ⁷  ⁸  ⁹  ",
                "2  2  2  2  2  2  2  2  2  2  2  ",
                "0  1  2  3  4  5  6  7  8  9  10 ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Replace_2()
    {
        Compute(
            ["₀₁₂₃₄₅₆₇₈₉", "012345678", "⁰¹²³⁴⁵⁶⁷⁸⁹"],
            [Edit(0, 3, 1, 0, "ab"), Edit(1, 5, 1, 7, "c")])
            .ShouldBe(
            [
                "₀  ₁  ₂  a  b  0  1  2  3  4  c  7  8  ",
                "0  0  0  0  0  1  1  1  1  1  1  1  1  1  ",
                "0  1  2  3  4  0  1  2  3  4  5  7  8  9  ",
                "0  0  0  0  0  0  0  0  0  0  0  ∞  ∞  ∞  ",
                "3  2  1  0  0  5  4  3  2  1  0  ∞  ∞  ∞  ",
                "⁰  ¹  ²  ³  ⁴  ⁵  ⁶  ⁷  ⁸  ⁹  ",
                "2  2  2  2  2  2  2  2  2  2  2  ",
                "0  1  2  3  4  5  6  7  8  9  10 ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Replace_3()
    {
        Compute(
            ["₀₁₂₃₄₅₆₇₈₉", "012345678", "⁰¹²³⁴⁵⁶⁷⁸⁹"],
            [Edit(0, 3, 1, 0, "ab"), Edit(1, 5, 1, 7, "c"), Edit(1, 8, 2, 4, "d")])
            .ShouldBe(
            [
                "₀  ₁  ₂  a  b  0  1  2  3  4  c  7  d  ⁴  ⁵  ⁶  ⁷  ⁸  ⁹  ",
                "0  0  0  0  0  1  1  1  1  1  1  1  1  2  2  2  2  2  2  2  ",
                "0  1  2  3  4  0  1  2  3  4  5  7  8  4  5  6  7  8  9  10 ",
                "0  0  0  0  0  0  0  0  0  0  0  0  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "3  2  1  0  0  5  4  3  2  1  0  1  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Insert_1()
    {
        Compute(
            ["012345678"],
            [Edit(0, 3, 0, 5, "a\nb")])
            .ShouldBe(
            [
                "0  1  2  a  ",
                "0  0  0  0  0  ",
                "0  1  2  3  4  ",
                "0  0  0  0  0  ",
                "3  2  1  0  0  ",
                "b  5  6  7  8  ",
                "1  0  0  0  0  0  ",
                "0  5  6  7  8  9  ",
                "0  ∞  ∞  ∞  ∞  ∞  ",
                "0  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Insert_2()
    {
        Compute(
            ["012345678"],
            [Edit(0, 3, 0, 5, "a\nb"), Edit(0, 7, 0, 8, "x\ny")])
            .ShouldBe(
            [
                "0  1  2  a  ",
                "0  0  0  0  0  ",
                "0  1  2  3  4  ",
                "0  0  0  0  0  ",
                "3  2  1  0  0  ",
                "b  5  6  x  ",
                "1  0  0  0  0  ",
                "0  5  6  7  8  ",
                "0  0  0  0  0  ",
                "0  2  1  0  0  ",
                "y  8  ",
                "1  0  0  ",
                "0  8  9  ",
                "0  ∞  ∞  ",
                "0  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Replace_Insert_1()
    {
        Compute(
            ["₀₁₂₃₄₅₆₇₈₉", "012345678", "⁰¹²³⁴⁵⁶⁷⁸⁹"],
            [Edit(0, 3, 1, 1, "aaa\nbbb")])
            .ShouldBe(
            [
                "₀  ₁  ₂  a  a  a  ",
                "0  0  0  0  0  0  0  ",
                "0  1  2  3  4  5  6  ",
                "0  0  0  0  0  0  0  ",
                "3  2  1  0  0  0  0  ",
                "b  b  b  1  2  3  4  5  6  7  8  ",
                "1  1  1  1  1  1  1  1  1  1  1  1  ",
                "0  1  2  1  2  3  4  5  6  7  8  9  ",
                "0  0  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "0  0  0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "⁰  ¹  ²  ³  ⁴  ⁵  ⁶  ⁷  ⁸  ⁹  ",
                "2  2  2  2  2  2  2  2  2  2  2  ",
                "0  1  2  3  4  5  6  7  8  9  10 ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    [Fact]
    public void Multi_Line_Replace_Insert_2()
    {
        Compute(
            ["₀₁₂₃₄₅₆₇₈₉", "012345678", "⁰¹²³⁴⁵⁶⁷⁸⁹"],
            [Edit(0, 3, 1, 1, "aaa\nbbb"), Edit(1, 5, 1, 5, "x\ny"), Edit(1, 7, 2, 4, "k\nl")])
            .ShouldBe(
            [
                "₀  ₁  ₂  a  a  a  ",
                "0  0  0  0  0  0  0  ",
                "0  1  2  3  4  5  6  ",
                "0  0  0  0  0  0  0  ",
                "3  2  1  0  0  0  0  ",
                "b  b  b  1  2  3  4  x  ",
                "1  1  1  1  1  1  1  1  1  ",
                "0  1  2  1  2  3  4  5  6  ",
                "0  0  0  0  0  0  0  0  0  ",
                "0  0  0  4  3  2  1  0  0  ",
                "y  5  6  k  ",
                "2  1  1  1  1  ",
                "0  5  6  7  8  ",
                "0  0  0  0  0  ",
                "0  2  1  0  0  ",
                "l  ⁴  ⁵  ⁶  ⁷  ⁸  ⁹  ",
                "2  2  2  2  2  2  2  2  ",
                "0  4  5  6  7  8  9  10 ",
                "0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
                "0  ∞  ∞  ∞  ∞  ∞  ∞  ∞  ",
            ]);
    }

    private sealed class TextEdit(Length startOffset, Length endOffset, string newText) : TextEditInfoHolder(startOffset, endOffset, newText);

    private class TextEditInfoHolder(Length startOffset, Length endOffset, string newText)
    {
        public TextEditInfo Info { get; } = new(startOffset, endOffset, LengthOfString(newText));

        public string NewText { get; } = newText;
    }

    private static TextEdit Edit(int startLine, int startColumn, int endLine, int endColumn, string newText) =>
        new(ToLength(startLine, startColumn), ToLength(endLine, endColumn), newText);

    private static string[] Compute(string[] inputArr, TextEdit[] edits)
    {
        var newLines = PieceTreeStrings.SplitLines(ApplyLineColumnEdits(string.Join('\n', inputArr), edits));

        var mapper = new BeforeEditPositionMapper(edits.Select(e => e.Info).ToList());

        var result = new List<string>();

        var lineIdx = 0;
        foreach (var line in newLines)
        {
            var lineLine = string.Empty;
            var colLine = string.Empty;
            var lineStr = string.Empty;

            var colDist = string.Empty;
            var lineDist = string.Empty;

            for (var colIdx = 0; colIdx <= line.Length; colIdx++)
            {
                var before = mapper.GetOffsetBeforeChange(ToLength(lineIdx, colIdx));
                var beforeObj = LengthToObj(before);
                if (colIdx < line.Length)
                {
                    lineStr += RightPad(line[colIdx].ToString(), 3);
                }

                lineLine += RightPad(beforeObj.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture), 3);
                colLine += RightPad(beforeObj.ColumnCount.ToString(System.Globalization.CultureInfo.InvariantCulture), 3);

                var distLen = mapper.GetDistanceToNextChange(ToLength(lineIdx, colIdx));
                if (distLen is not { } d)
                {
                    lineDist += "\u221e  ";
                    colDist += "\u221e  ";
                }
                else
                {
                    var dist = LengthToObj(d);
                    lineDist += RightPad(dist.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture), 3);
                    colDist += RightPad(dist.ColumnCount.ToString(System.Globalization.CultureInfo.InvariantCulture), 3);
                }
            }

            result.Add(lineStr);

            result.Add(lineLine);
            result.Add(colLine);

            result.Add(lineDist);
            result.Add(colDist);

            lineIdx++;
        }

        return [.. result];
    }

    private static string ApplyLineColumnEdits(string text, TextEdit[] edits)
    {
        var lineStartOffsetByLineIdx = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineStartOffsetByLineIdx.Add(i + 1);
            }
        }

        int GetOffset(TextPosition position) => lineStartOffsetByLineIdx[position.LineNumber - 1] + position.Column - 1;

        var offsetEdits = edits
            .Select(e => (Start: GetOffset(LengthToPosition(e.Info.StartOffset)), End: GetOffset(LengthToPosition(e.Info.EndOffset)), e.NewText))
            .OrderByDescending(e => e.Start)
            .ToList();

        foreach (var edit in offsetEdits)
        {
            text = text[..edit.Start] + edit.NewText + text[edit.End..];
        }

        return text;
    }

    private static string RightPad(string str, int len) => str.PadRight(len);
}

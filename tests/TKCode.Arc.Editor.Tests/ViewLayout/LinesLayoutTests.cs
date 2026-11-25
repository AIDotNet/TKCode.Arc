// Ported from vscode (src/vs/editor/test/common/viewLayout/linesLayout.test.ts, commit b57d5c9e), MIT license.
// assert.strictEqual is StrictEqual and assert.deepStrictEqual is DeepStrictEqual. The test named 'LinesLayout' upstream
// is LinesLayout_Whitespaces here (a method named LinesLayout would hide the type in this class).

using Shouldly;
using TKCode.Arc.Editor.ViewLayout;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'Editor ViewLayout - LinesLayout'.</summary>
public sealed class LinesLayoutTests
{
    private static string InsertWhitespace(LinesLayout linesLayout, int afterLineNumber, int ordinal, double heightInPx, double minWidth)
    {
        string? id = null;
        linesLayout.ChangeWhitespace(accessor =>
        {
            id = accessor.InsertWhitespace(afterLineNumber, ordinal, heightInPx, minWidth);
        });
        return id!;
    }

    private static void ChangeOneWhitespace(LinesLayout linesLayout, string id, int newAfterLineNumber, double newHeight)
    {
        linesLayout.ChangeWhitespace(accessor =>
        {
            accessor.ChangeOneWhitespace(id, newAfterLineNumber, newHeight);
        });
    }

    private static void RemoveWhitespace(LinesLayout linesLayout, string id)
    {
        linesLayout.ChangeWhitespace(accessor =>
        {
            accessor.RemoveWhitespace(id);
        });
    }

    [Fact]
    public void LinesLayout_1()
    {

        // Start off with 10 lines
        var linesLayout = new LinesLayout(10, 10, 0, 0, []);

        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: -
        StrictEqual(linesLayout.GetLinesTotalHeight(), 100);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 30);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 50);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 60);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 70);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 80);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 90);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(1), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(5), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(9), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(11), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(15), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(19), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(20), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(21), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(29), 3);

        // Add whitespace of height 5px after 2nd line
        InsertWhitespace(linesLayout, 2, 0, 5, 0);
        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: a(2,5)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 105);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 25);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 35);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 45);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(1), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(9), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(20), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(21), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(24), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(25), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(35), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(45), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(104), 10);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(105), 10);

        // Add two more whitespaces of height 5px
        InsertWhitespace(linesLayout, 3, 0, 5, 0);
        InsertWhitespace(linesLayout, 4, 0, 5, 0);
        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: a(2,5), b(3, 5), c(4, 5)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 115);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 25);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 55);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 65);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(1), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(9), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(19), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(20), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(34), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(35), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(49), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(50), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(64), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(65), 6);

        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(0), 20); // 20 -> 25
        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(1), 35); // 35 -> 40
        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(2), 50);

        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(0), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(19), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(20), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(21), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(22), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(23), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(24), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(25), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(26), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(34), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(35), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(36), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(39), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(40), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(41), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(49), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(50), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(51), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(54), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(55), -1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(1000), -1);

    }

    [Fact]
    public void LinesLayout_2()
    {

        // Start off with 10 lines and one whitespace after line 2, of height 5
        var linesLayout = new LinesLayout(10, 1, 0, 0, []);
        var a = InsertWhitespace(linesLayout, 2, 0, 5, 0);

        // 10 lines
        // whitespace: - a(2,5)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 7);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 8);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 9);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 11);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 12);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 13);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 14);

        // Change whitespace height
        // 10 lines
        // whitespace: - a(2,10)
        ChangeOneWhitespace(linesLayout, a, 2, 10);
        StrictEqual(linesLayout.GetLinesTotalHeight(), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 12);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 13);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 14);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 19);

        // Change whitespace position
        // 10 lines
        // whitespace: - a(5,10)
        ChangeOneWhitespace(linesLayout, a, 5, 10);
        StrictEqual(linesLayout.GetLinesTotalHeight(), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 4);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 19);

        // Pretend that lines 5 and 6 were deleted
        // 8 lines
        // whitespace: - a(4,10)
        linesLayout.OnLinesDeleted(5, 6);
        StrictEqual(linesLayout.GetLinesTotalHeight(), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 14);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);

        // Insert two lines at the beginning
        // 10 lines
        // whitespace: - a(6,10)
        linesLayout.OnLinesInserted(1, 2);
        StrictEqual(linesLayout.GetLinesTotalHeight(), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 4);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 5);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 19);

        // Remove whitespace
        // 10 lines
        RemoveWhitespace(linesLayout, a);
        StrictEqual(linesLayout.GetLinesTotalHeight(), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 4);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 5);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 6);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 7);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 8);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 9);
    }

    [Fact]
    public void LinesLayout_Padding()
    {
        // Start off with 10 lines
        var linesLayout = new LinesLayout(10, 10, 15, 20, []);

        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: -
        StrictEqual(linesLayout.GetLinesTotalHeight(), 135);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 25);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 35);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 45);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 55);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 65);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 75);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 85);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 95);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 105);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(15), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(24), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(25), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(34), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(35), 3);

        // Add whitespace of height 5px after 2nd line
        InsertWhitespace(linesLayout, 2, 0, 5, 0);
        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: a(2,5)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 140);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 25);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 50);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(25), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(34), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(35), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(39), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(40), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(41), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(49), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(50), 4);

        // Add two more whitespaces of height 5px
        InsertWhitespace(linesLayout, 3, 0, 5, 0);
        InsertWhitespace(linesLayout, 4, 0, 5, 0);
        // lines: [0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        // whitespace: a(2,5), b(3, 5), c(4, 5)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 150);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 15);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 25);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 55);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 70);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 80);

        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(15), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(24), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(30), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(35), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(39), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(40), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(49), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(50), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(54), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(55), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(64), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(65), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(69), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(70), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(80), 6);

        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(0), 35); // 35 -> 40
        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(1), 50); // 50 -> 55
        StrictEqual(linesLayout.GetVerticalOffsetForWhitespaceIndex(2), 65);

        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(0), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(34), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(35), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(39), 0);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(40), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(49), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(50), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(54), 1);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(55), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(64), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(65), 2);
        StrictEqual(linesLayout.GetWhitespaceIndexAtOrAfterVerticallOffset(70), -1);
    }

    [Fact]
    public void LinesLayout_GetLineNumberAtOrAfterVerticalOffset()
    {
        var linesLayout = new LinesLayout(10, 1, 0, 0, []);
        InsertWhitespace(linesLayout, 6, 0, 10, 0);

        // 10 lines
        // whitespace: - a(6,10)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 4);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 5);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 19);

        // Do some hit testing
        // line      [1, 2, 3, 4, 5, 6,  7,  8,  9, 10]
        // vertical: [0, 1, 2, 3, 4, 5, 16, 17, 18, 19]
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(-100), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(-1), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(0), 1);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(1), 2);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(2), 3);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(3), 4);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(4), 5);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(5), 6);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(6), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(7), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(8), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(9), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(10), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(11), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(12), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(13), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(14), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(15), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(16), 7);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(17), 8);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(18), 9);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(19), 10);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(20), 10);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(21), 10);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(22), 10);
        StrictEqual(linesLayout.GetLineNumberAtOrAfterVerticalOffset(23), 10);
    }

    [Fact]
    public void LinesLayout_GetCenteredLineInViewport()
    {
        var linesLayout = new LinesLayout(10, 1, 0, 0, []);
        InsertWhitespace(linesLayout, 6, 0, 10, 0);

        // 10 lines
        // whitespace: - a(6,10)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 1);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 2);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 3);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 4);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 5);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 16);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 17);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 18);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 19);

        // Find centered line in viewport 1
        // line      [1, 2, 3, 4, 5, 6,  7,  8,  9, 10]
        // vertical: [0, 1, 2, 3, 4, 5, 16, 17, 18, 19]
        StrictEqual(linesLayout.GetLinesViewportData(0, 1).CenteredLineNumber, 1);
        StrictEqual(linesLayout.GetLinesViewportData(0, 2).CenteredLineNumber, 2);
        StrictEqual(linesLayout.GetLinesViewportData(0, 3).CenteredLineNumber, 2);
        StrictEqual(linesLayout.GetLinesViewportData(0, 4).CenteredLineNumber, 3);
        StrictEqual(linesLayout.GetLinesViewportData(0, 5).CenteredLineNumber, 3);
        StrictEqual(linesLayout.GetLinesViewportData(0, 6).CenteredLineNumber, 4);
        StrictEqual(linesLayout.GetLinesViewportData(0, 7).CenteredLineNumber, 4);
        StrictEqual(linesLayout.GetLinesViewportData(0, 8).CenteredLineNumber, 5);
        StrictEqual(linesLayout.GetLinesViewportData(0, 9).CenteredLineNumber, 5);
        StrictEqual(linesLayout.GetLinesViewportData(0, 10).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 11).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 12).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 13).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 14).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 15).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 16).CenteredLineNumber, 6);
        StrictEqual(linesLayout.GetLinesViewportData(0, 17).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 18).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 19).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 21).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 22).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 23).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 24).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 25).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 26).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 27).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 28).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 29).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 30).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 31).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 32).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(0, 33).CenteredLineNumber, 7);

        // Find centered line in viewport 2
        // line      [1, 2, 3, 4, 5, 6,  7,  8,  9, 10]
        // vertical: [0, 1, 2, 3, 4, 5, 16, 17, 18, 19]
        StrictEqual(linesLayout.GetLinesViewportData(0, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(1, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(2, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(3, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(4, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(5, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(6, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(7, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(8, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(9, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(10, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(11, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(12, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(13, 20).CenteredLineNumber, 7);
        StrictEqual(linesLayout.GetLinesViewportData(14, 20).CenteredLineNumber, 8);
        StrictEqual(linesLayout.GetLinesViewportData(15, 20).CenteredLineNumber, 8);
        StrictEqual(linesLayout.GetLinesViewportData(16, 20).CenteredLineNumber, 9);
        StrictEqual(linesLayout.GetLinesViewportData(17, 20).CenteredLineNumber, 9);
        StrictEqual(linesLayout.GetLinesViewportData(18, 20).CenteredLineNumber, 10);
        StrictEqual(linesLayout.GetLinesViewportData(19, 20).CenteredLineNumber, 10);
        StrictEqual(linesLayout.GetLinesViewportData(20, 23).CenteredLineNumber, 10);
        StrictEqual(linesLayout.GetLinesViewportData(21, 23).CenteredLineNumber, 10);
        StrictEqual(linesLayout.GetLinesViewportData(22, 23).CenteredLineNumber, 10);
    }

    [Fact]
    public void LinesLayout_GetLinesViewportData_1()
    {
        var linesLayout = new LinesLayout(10, 10, 0, 0, []);
        InsertWhitespace(linesLayout, 6, 0, 100, 0);

        // 10 lines
        // whitespace: - a(6,100)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 200);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 30);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 50);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 160);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 170);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 180);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 190);

        // viewport 0->50
        var viewportData = linesLayout.GetLinesViewportData(0, 50);
        StrictEqual(viewportData.StartLineNumber, 1);
        StrictEqual(viewportData.EndLineNumber, 5);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 1);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 5);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [0, 10, 20, 30, 40]);

        // viewport 1->51
        viewportData = linesLayout.GetLinesViewportData(1, 51);
        StrictEqual(viewportData.StartLineNumber, 1);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 2);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 5);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [0, 10, 20, 30, 40, 50]);

        // viewport 5->55
        viewportData = linesLayout.GetLinesViewportData(5, 55);
        StrictEqual(viewportData.StartLineNumber, 1);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 2);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 5);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [0, 10, 20, 30, 40, 50]);

        // viewport 10->60
        viewportData = linesLayout.GetLinesViewportData(10, 60);
        StrictEqual(viewportData.StartLineNumber, 2);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 2);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 6);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [10, 20, 30, 40, 50]);

        // viewport 50->100
        viewportData = linesLayout.GetLinesViewportData(50, 100);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 6);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50]);

        // viewport 60->110
        viewportData = linesLayout.GetLinesViewportData(60, 110);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160]);

        // viewport 65->115
        viewportData = linesLayout.GetLinesViewportData(65, 115);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160]);

        // viewport 50->159
        viewportData = linesLayout.GetLinesViewportData(50, 159);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 6);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50]);

        // viewport 50->160
        viewportData = linesLayout.GetLinesViewportData(50, 160);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 6);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50]);

        // viewport 51->161
        viewportData = linesLayout.GetLinesViewportData(51, 161);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50, 160]);


        // viewport 150->169
        viewportData = linesLayout.GetLinesViewportData(150, 169);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160]);

        // viewport 159->169
        viewportData = linesLayout.GetLinesViewportData(159, 169);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160]);

        // viewport 160->169
        viewportData = linesLayout.GetLinesViewportData(160, 169);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160]);


        // viewport 160->1000
        viewportData = linesLayout.GetLinesViewportData(160, 1000);
        StrictEqual(viewportData.StartLineNumber, 7);
        StrictEqual(viewportData.EndLineNumber, 10);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 10);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [160, 170, 180, 190]);
    }

    [Fact]
    public void LinesLayout_GetLinesViewportData_2_GetWhitespaceViewportData()
    {
        var linesLayout = new LinesLayout(10, 10, 0, 0, []);
        var a = InsertWhitespace(linesLayout, 6, 0, 100, 0);
        var b = InsertWhitespace(linesLayout, 7, 0, 50, 0);

        // 10 lines
        // whitespace: - a(6,100), b(7, 50)
        StrictEqual(linesLayout.GetLinesTotalHeight(), 250);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(1), 0);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(2), 10);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(3), 20);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(4), 30);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(5), 40);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(6), 50);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(7), 160);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(8), 220);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(9), 230);
        StrictEqual(linesLayout.GetVerticalOffsetForLineNumber(10), 240);

        // viewport 50->160
        var viewportData = linesLayout.GetLinesViewportData(50, 160);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 6);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50]);
        var whitespaceData = linesLayout.GetWhitespaceViewportData(50, 160);
        DeepStrictEqual(whitespaceData, [new ViewWhitespaceViewportData(a, 6, 60, 100)]);

        // viewport 50->219
        viewportData = linesLayout.GetLinesViewportData(50, 219);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50, 160]);
        whitespaceData = linesLayout.GetWhitespaceViewportData(50, 219);
        DeepStrictEqual(whitespaceData, [new ViewWhitespaceViewportData(a, 6, 60, 100), new ViewWhitespaceViewportData(b, 7, 170, 50)]);

        // viewport 50->220
        viewportData = linesLayout.GetLinesViewportData(50, 220);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 7);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 7);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50, 160]);

        // viewport 50->250
        viewportData = linesLayout.GetLinesViewportData(50, 250);
        StrictEqual(viewportData.StartLineNumber, 6);
        StrictEqual(viewportData.EndLineNumber, 10);
        StrictEqual(viewportData.CompletelyVisibleStartLineNumber, 6);
        StrictEqual(viewportData.CompletelyVisibleEndLineNumber, 10);
        DeepStrictEqual(viewportData.RelativeVerticalOffset, [50, 160, 220, 230, 240]);
    }

    [Fact]
    public void LinesLayout_GetWhitespaceAtVerticalOffset()
    {
        var linesLayout = new LinesLayout(10, 10, 0, 0, []);
        var a = InsertWhitespace(linesLayout, 6, 0, 100, 0);
        var b = InsertWhitespace(linesLayout, 7, 0, 50, 0);

        var whitespace = linesLayout.GetWhitespaceAtVerticalOffset(0);
        StrictEqual(whitespace, null);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(59);
        StrictEqual(whitespace, null);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(60);
        StrictEqual(whitespace!.Id, a);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(61);
        StrictEqual(whitespace!.Id, a);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(159);
        StrictEqual(whitespace!.Id, a);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(160);
        StrictEqual(whitespace, null);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(161);
        StrictEqual(whitespace, null);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(169);
        StrictEqual(whitespace, null);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(170);
        StrictEqual(whitespace!.Id, b);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(171);
        StrictEqual(whitespace!.Id, b);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(219);
        StrictEqual(whitespace!.Id, b);

        whitespace = linesLayout.GetWhitespaceAtVerticalOffset(220);
        StrictEqual(whitespace, null);
    }

    [Fact]
    public void LinesLayout_Whitespaces()
    {

        var linesLayout = new LinesLayout(100, 20, 0, 0, []);

        // Insert a whitespace after line number 2, of height 10
        var a = InsertWhitespace(linesLayout, 2, 0, 10, 0);
        // whitespaces: a(2, 10)
        StrictEqual(linesLayout.GetWhitespacesCount(), 1);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 10);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 10);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 10);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 10);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 10);

        // Insert a whitespace again after line number 2, of height 20
        var b = InsertWhitespace(linesLayout, 2, 0, 20, 0);
        // whitespaces: a(2, 10), b(2, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 10);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 10);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 30);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 30);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 30);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 30);

        // Change last inserted whitespace height to 30
        ChangeOneWhitespace(linesLayout, b, 2, 30);
        // whitespaces: a(2, 10), b(2, 30)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 10);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 30);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 10);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 40);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 40);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 40);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 40);

        // Remove last inserted whitespace
        RemoveWhitespace(linesLayout, b);
        // whitespaces: a(2, 10)
        StrictEqual(linesLayout.GetWhitespacesCount(), 1);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 10);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 10);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 10);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 10);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 10);

        // Add a whitespace before the first line of height 50
        b = InsertWhitespace(linesLayout, 0, 0, 50, 0);
        // whitespaces: b(0, 50), a(2, 10)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 50);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 10);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 50);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 60);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 60);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 60);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 60);

        // Add a whitespace after line 4 of height 20
        InsertWhitespace(linesLayout, 4, 0, 20, 0);
        // whitespaces: b(0, 50), a(2, 10), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 3);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 50);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 10);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(2), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 50);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 60);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(2), 80);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 80);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 60);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 60);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 80);

        // Add a whitespace after line 3 of height 30
        InsertWhitespace(linesLayout, 3, 0, 30, 0);
        // whitespaces: b(0, 50), a(2, 10), d(3, 30), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 4);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 50);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 10);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(2), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(3), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(3), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 50);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 60);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(2), 90);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(3), 110);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 110);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 60);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 90);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 110);

        // Change whitespace after line 2 to height of 100
        ChangeOneWhitespace(linesLayout, a, 2, 100);
        // whitespaces: b(0, 50), a(2, 100), d(3, 30), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 4);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 50);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 100);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(2), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(3), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(3), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 50);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 150);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(2), 180);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(3), 200);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 200);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 150);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 180);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 200);

        // Remove whitespace after line 2
        RemoveWhitespace(linesLayout, a);
        // whitespaces: b(0, 50), d(3, 30), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 3);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 50);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(2), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 50);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 80);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(2), 100);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 100);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 80);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 100);

        // Remove whitespace before line 1
        RemoveWhitespace(linesLayout, b);
        // whitespaces: d(3, 30), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 30);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 50);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 30);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 50);

        // Delete line 1
        linesLayout.OnLinesDeleted(1, 1);
        // whitespaces: d(2, 30), c(3, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 30);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 50);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 30);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 50);

        // Insert a line before line 1
        linesLayout.OnLinesInserted(1, 1);
        // whitespaces: d(3, 30), c(4, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 4);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 30);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 50);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 30);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 50);

        // Delete line 4
        linesLayout.OnLinesDeleted(4, 4);
        // whitespaces: d(3, 30), c(3, 20)
        StrictEqual(linesLayout.GetWhitespacesCount(), 2);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(0), 30);
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetHeightForWhitespaceIndex(1), 20);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(0), 30);
        StrictEqual(linesLayout.GetWhitespacesAccumulatedHeight(1), 50);
        StrictEqual(linesLayout.GetWhitespacesTotalHeight(), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(1), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(2), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(3), 0);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(4), 50);
        StrictEqual(linesLayout.GetWhitespaceAccumulatedHeightBeforeLineNumber(5), 50);
    }

    [Fact]
    public void LinesLayout_FindInsertionIndex()
    {
        static EditorWhitespace[] MakeInternalWhitespace(int[] afterLineNumbers, int ordinal = 0)
        {
            return afterLineNumbers.Select(afterLineNumber => new EditorWhitespace("", afterLineNumber, ordinal, 0, 0)).ToArray();
        }

        EditorWhitespace[] arr;

        arr = MakeInternalWhitespace([]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 0);

        arr = MakeInternalWhitespace([1]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);

        arr = MakeInternalWhitespace([1, 3]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);

        arr = MakeInternalWhitespace([1, 3, 5]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);

        arr = MakeInternalWhitespace([1, 3, 5], 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);

        arr = MakeInternalWhitespace([1, 3, 5, 7]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 7, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 8, 0), 4);

        arr = MakeInternalWhitespace([1, 3, 5, 7, 9]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 7, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 8, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 9, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 10, 0), 5);

        arr = MakeInternalWhitespace([1, 3, 5, 7, 9, 11]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 7, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 8, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 9, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 10, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 11, 0), 6);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 12, 0), 6);

        arr = MakeInternalWhitespace([1, 3, 5, 7, 9, 11, 13]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 7, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 8, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 9, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 10, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 11, 0), 6);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 12, 0), 6);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 13, 0), 7);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 14, 0), 7);

        arr = MakeInternalWhitespace([1, 3, 5, 7, 9, 11, 13, 15]);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 0, 0), 0);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 1, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 2, 0), 1);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 3, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 4, 0), 2);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 5, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 6, 0), 3);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 7, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 8, 0), 4);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 9, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 10, 0), 5);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 11, 0), 6);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 12, 0), 6);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 13, 0), 7);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 14, 0), 7);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 15, 0), 8);
        StrictEqual(LinesLayout.FindInsertionIndex(arr, 16, 0), 8);
    }

    [Fact]
    public void LinesLayout_ChangeWhitespaceAfterLineNumber_GetFirstWhitespaceIndexAfterLineNumber()
    {
        var linesLayout = new LinesLayout(100, 20, 0, 0, []);

        var a = InsertWhitespace(linesLayout, 0, 0, 1, 0);
        var b = InsertWhitespace(linesLayout, 7, 0, 1, 0);
        var c = InsertWhitespace(linesLayout, 3, 0, 1, 0);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), c); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --

        // Do not really move a
        ChangeOneWhitespace(linesLayout, a, 1, 1);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 1
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 1);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), c); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --


        // Do not really move a
        ChangeOneWhitespace(linesLayout, a, 2, 1);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 2
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 2);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), c); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 1); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --


        // Change a to conflict with c => a gets placed after c
        ChangeOneWhitespace(linesLayout, a, 3, 1);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), c); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), a); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --


        // Make a no-op
        ChangeOneWhitespace(linesLayout, c, 3, 1);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), c); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), a); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 0); // c
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 2); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --



        // Conflict c with b => c gets placed after b
        ChangeOneWhitespace(linesLayout, c, 7, 1);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 3
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(0), 3);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), b); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(1), 7);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), c); // 7
        StrictEqual(linesLayout.GetAfterLineNumberForWhitespaceIndex(2), 7);

        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(1), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(2), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(3), 0); // a
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(4), 1); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(5), 1); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(6), 1); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(7), 1); // b
        StrictEqual(linesLayout.GetFirstWhitespaceIndexAfterLineNumber(8), -1); // --
    }

    [Fact]
    public void LinesLayout_Bug()
    {
        var linesLayout = new LinesLayout(100, 20, 0, 0, []);

        var a = InsertWhitespace(linesLayout, 0, 0, 1, 0);
        var b = InsertWhitespace(linesLayout, 7, 0, 1, 0);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), b); // 7

        var c = InsertWhitespace(linesLayout, 3, 0, 1, 0);

        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), b); // 7

        var d = InsertWhitespace(linesLayout, 2, 0, 1, 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), d); // 2
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(3), b); // 7

        var e = InsertWhitespace(linesLayout, 8, 0, 1, 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), d); // 2
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(3), b); // 7
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(4), e); // 8

        var f = InsertWhitespace(linesLayout, 11, 0, 1, 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), d); // 2
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(3), b); // 7
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(4), e); // 8
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(5), f); // 11

        var g = InsertWhitespace(linesLayout, 10, 0, 1, 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), d); // 2
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(3), b); // 7
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(4), e); // 8
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(5), g); // 10
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(6), f); // 11

        var h = InsertWhitespace(linesLayout, 0, 0, 1, 0);
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(0), a); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(1), h); // 0
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(2), d); // 2
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(3), c); // 3
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(4), b); // 7
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(5), e); // 8
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(6), g); // 10
        StrictEqual(linesLayout.GetIdForWhitespaceIndex(7), f); // 11
    }

    private static void StrictEqual(int actual, int expected) => actual.ShouldBe(expected);

    private static void StrictEqual(double actual, double expected) => actual.ShouldBe(expected);

    private static void StrictEqual(string actual, string expected) => actual.ShouldBe(expected);

    private static void StrictEqual(object? actual, object? expected) => actual.ShouldBe(expected);

    private static void DeepStrictEqual(IReadOnlyList<double> actual, double[] expected) => actual.ToArray().ShouldBe(expected);

    private static void DeepStrictEqual(IViewWhitespaceViewportData[] actual, IViewWhitespaceViewportData[] expected) => actual.ShouldBe(expected);
}

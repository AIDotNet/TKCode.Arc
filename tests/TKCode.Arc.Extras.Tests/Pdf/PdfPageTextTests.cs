using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Pdf;

public class PdfPageTextTests
{
    /// <summary>Lines of 10-wide, 12-high characters; a space and a line break have empty boxes.</summary>
    private static PdfPageText Lay(params string[] lines)
    {
        var text = string.Join('\n', lines);
        var boxes = new Rect[text.Length];
        var (x, y) = (0, 0);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                x = 0;
                y++;
                continue;
            }
            if (text[i] != ' ')
            {
                boxes[i] = new Rect(x * 10, y * 20, 10, 12);
            }
            x++;
        }
        return new PdfPageText(text, boxes);
    }

    [Fact]
    public void Needs_One_Box_Per_Character()
    {
        Should.Throw<ArgumentException>(() => new PdfPageText("abc", new Rect[2]));
    }

    [Fact]
    public void HitTest_Takes_The_Character_Under_The_Point()
    {
        var text = Lay("hello", "world");
        text.HitTest(new Point(25, 5)).ShouldBe(2);
        text.Text[text.HitTest(new Point(15, 25))].ShouldBe('o');
    }

    [Fact]
    public void HitTest_Takes_The_Nearest_Outside_And_Skips_Empty_Boxes()
    {
        var text = Lay("ab cd");
        text.HitTest(new Point(500, 6)).ShouldBe(4);
        text.HitTest(new Point(-50, -50)).ShouldBe(0);
        // Over the space: a neighbor, never the space.
        text.Text[text.HitTest(new Point(25, 6))].ShouldNotBe(' ');
    }

    [Fact]
    public void HitTest_On_A_Page_Without_Text_Is_Minus_One()
    {
        new PdfPageText("", []).HitTest(new Point(1, 1)).ShouldBe(-1);
        new PdfPageText("  ", new Rect[2]).HitTest(new Point(1, 1)).ShouldBe(-1);
    }

    [Fact]
    public void HitTest_Lands_On_The_First_Half_Of_A_Surrogate_Pair()
    {
        var box = new Rect(0, 0, 10, 10);
        var text = new PdfPageText("\U0001F600", [box, box]);
        text.HitTest(new Point(5, 5)).ShouldBe(0);
    }

    [Fact]
    public void GetRects_Merges_A_Line_Across_Its_Spaces()
    {
        Lay("ab cd").GetRects(0, 5).ShouldBe([new Rect(0, 0, 50, 12)]);
    }

    [Fact]
    public void GetRects_Gives_One_Rectangle_Per_Line()
    {
        var text = Lay("hello", "wor");
        var rects = text.GetRects(3, 6);
        rects.ShouldBe([new Rect(30, 0, 20, 12), new Rect(0, 20, 30, 12)]);
    }

    [Fact]
    public void GetRects_Separates_Lines_Without_A_Line_Break_By_Position()
    {
        // Same text run, the second word on a lower line, no line break character between them.
        var text = new PdfPageText("ab", [new Rect(0, 0, 10, 12), new Rect(0, 20, 10, 12)]);
        text.GetRects(0, 2).Count.ShouldBe(2);
    }

    [Fact]
    public void GetRects_Separates_Columns_Across_A_Wide_Gap()
    {
        var text = new PdfPageText("ab", [new Rect(0, 0, 10, 12), new Rect(300, 0, 10, 12)]);
        text.GetRects(0, 2).Count.ShouldBe(2);
    }

    [Fact]
    public void GetRects_Merges_Slightly_Different_Baselines()
    {
        var text = new PdfPageText("ab", [new Rect(0, 0, 10, 12), new Rect(10, 3, 10, 12)]);
        text.GetRects(0, 2).ShouldBe([new Rect(0, 0, 20, 15)]);
    }

    [Fact]
    public void GetRects_Clamps_The_Range()
    {
        var text = Lay("abc");
        text.GetRects(-5, 100).ShouldBe([new Rect(0, 0, 30, 12)]);
        text.GetRects(2, int.MaxValue).ShouldBe([new Rect(20, 0, 10, 12)]);
        text.GetRects(99, 1).ShouldBeEmpty();
        text.GetRects(1, 0).ShouldBeEmpty();
        text.GetRects(1, -3).ShouldBeEmpty();
    }
}

public class PdfPageTextWordsTests
{
    private static PdfPageText Text(string text) => ScriptedPdfDocument.Layout(text);

    [Fact]
    public void Word_At_Takes_The_Run_Of_Letters()
    {
        var text = Text("one two-three don't");
        text.WordAt(1).ShouldBe((0, 3));
        text.WordAt(5).ShouldBe((4, 7));
        text.WordAt(8).ShouldBe((8, 13));
        text.WordAt(7).ShouldBe((7, 8));
        text.WordAt(15).ShouldBe((14, 19));
    }

    [Fact]
    public void Word_At_Keeps_Chinese_To_Itself_And_Surrogates_Whole()
    {
        var text = Text("abc\u4E2D\u6587\uFF0C\U00020BB7x");
        text.WordAt(0).ShouldBe((0, 3));
        text.WordAt(3).ShouldBe((3, 5));
        text.WordAt(5).ShouldBe((5, 6));
        // The ideograph of two units, from either unit.
        text.WordAt(6).ShouldBe((6, 8));
        text.WordAt(7).ShouldBe((6, 8));
        text.WordAt(8).ShouldBe((8, 9));
    }

    [Fact]
    public void Line_At_Stops_At_Breaks_And_Where_Boxes_Leave_The_Line()
    {
        var text = Text("first line\nsecond line\nthird");
        text.LineAt(3).ShouldBe((0, 10));
        text.LineAt(15).ShouldBe((11, 22));
        text.LineAt(25).ShouldBe((23, 28));

        // No line breaks in the text at all: only the boxes tell.
        var boxes = new Rect[] { new(0, 0, 5, 10), new(5, 0, 5, 10), new(0, 20, 5, 10), new(5, 20, 5, 10) };
        new PdfPageText("abcd", boxes).LineAt(0).ShouldBe((0, 2));
        new PdfPageText("abcd", boxes).LineAt(3).ShouldBe((2, 4));
    }

    [Fact]
    public void Caret_Goes_By_The_Side_Of_The_Character()
    {
        var text = Text("abc \U00020BB7z");
        var box = text.CharBoxes[1];
        text.CaretAt(new Point(box.Left + 1, box.Center.Y)).ShouldBe(1);
        text.CaretAt(new Point(box.Right - 1, box.Center.Y)).ShouldBe(2);
        // Behind a surrogate pair, not inside it.
        var pair = text.CharBoxes[4];
        text.CaretAt(new Point(pair.Right - 1, pair.Center.Y)).ShouldBe(6);
        text.CaretAt(new Point(pair.Left + 1, pair.Center.Y)).ShouldBe(4);
        new PdfPageText(string.Empty, []).CaretAt(default).ShouldBe(0);
    }

    [Fact]
    public void Is_Over_Text_Includes_The_Gaps_Of_A_Line_And_Nothing_Else()
    {
        var text = Text("red blue\nsecond");
        text.IsOverText(text.CharBoxes[1].Center).ShouldBeTrue();
        // The space has no box of its own.
        var gap = new Point((text.CharBoxes[2].Right + text.CharBoxes[4].Left) / 2, text.CharBoxes[2].Center.Y);
        text.IsOverText(gap).ShouldBeTrue();
        text.IsOverText(new Point(10, text.CharBoxes[1].Center.Y)).ShouldBeFalse();
        text.IsOverText(new Point(text.CharBoxes[0].Center.X, 5)).ShouldBeFalse();
        new PdfPageText(string.Empty, []).IsOverText(default).ShouldBeFalse();
    }
}

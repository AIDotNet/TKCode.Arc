using System.Text;
using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>
/// Tests for <see cref="PieceTreeTextBuffer"/>. Upstream covers <c>applyEdits</c> through its text model tests, which
/// come with the model port; these check the buffer on its own against a plain string.
/// </summary>
public sealed class PieceTreeTextBufferTests
{
    private static ValidAnnotatedEditOperation Op(TextRange range, string? text, bool autoWhitespace = false) =>
        new(null, range, text, forceMoveMarkers: false, isAutoWhitespaceEdit: autoWhitespace, isTracked: false);

    private static string ValueOf(PieceTreeTextBuffer buffer)
    {
        var lineCount = buffer.GetLineCount();
        return buffer.GetValueInRange(new TextRange(1, 1, lineCount, buffer.GetLineMaxColumn(lineCount)));
    }

    [Fact]
    public void Reads_Lines_Offsets_And_Ranges()
    {
        var buffer = PieceTreeTextBuffer.Create("first\nsecond line\n\tthird  ");

        buffer.GetLineCount().ShouldBe(3);
        buffer.GetEOL().ShouldBe("\n");
        buffer.GetLineContent(2).ShouldBe("second line");
        buffer.GetLineMaxColumn(2).ShouldBe(12);
        buffer.GetOffsetAt(2, 1).ShouldBe(6);
        buffer.GetPositionAt(6).ShouldBe(new TextPosition(2, 1));
        buffer.GetRangeAt(3, 5).ShouldBe(new TextRange(1, 4, 2, 3));
        buffer.GetLineFirstNonWhitespaceColumn(3).ShouldBe(2);
        buffer.GetLineLastNonWhitespaceColumn(3).ShouldBe(7);
        buffer.GetLineFirstNonWhitespaceColumn(1).ShouldBe(1);
        buffer.GetValueInRange(new TextRange(1, 4, 2, 3)).ShouldBe("st\nse");
        buffer.GetValueInRange(new TextRange(1, 4, 2, 3), EndOfLinePreference.CRLF).ShouldBe("st\r\nse");
        buffer.GetValueLengthInRange(new TextRange(1, 4, 2, 3), EndOfLinePreference.CRLF).ShouldBe(6);
    }

    [Fact]
    public void Counts_Characters_By_Code_Point_When_Not_Ascii()
    {
        var buffer = PieceTreeTextBuffer.Create("a😀b\nc");
        buffer.MightContainNonBasicASCII().ShouldBeTrue();
        buffer.GetValueLengthInRange(new TextRange(1, 1, 2, 2)).ShouldBe(6);
        buffer.GetCharacterCountInRange(new TextRange(1, 1, 2, 2)).ShouldBe(5);
    }

    [Fact]
    public void ApplyEdits_Reports_Changes_Bottom_Up_And_Reverse_Edits_In_Request_Order()
    {
        var buffer = PieceTreeTextBuffer.Create("abc\ndef\nghi");
        var raised = 0;
        buffer.DidChangeContent += () => raised++;

        var result = buffer.ApplyEdits(
            [Op(new TextRange(3, 1, 3, 2), "G"), Op(new TextRange(1, 2, 1, 2), "X\nY")],
            recordTrimAutoWhitespace: false,
            computeUndoEdits: true);

        ValueOf(buffer).ShouldBe("aX\nYbc\ndef\nGhi");
        raised.ShouldBe(1);
        result.Changes.Select(c => c.Range).ShouldBe([new TextRange(3, 1, 3, 2), new TextRange(1, 2, 1, 2)]);
        result.ReverseEdits!.Select(e => e.Range).ShouldBe([new TextRange(4, 1, 4, 2), new TextRange(1, 2, 2, 2)]);
        result.ReverseEdits!.Select(e => e.Text).ShouldBe(["g", ""]);
    }

    [Fact]
    public void ApplyEdits_Converts_Inserted_Line_Endings_To_The_Buffer_EOL()
    {
        var buffer = PieceTreeTextBuffer.Create("a\r\nb");
        buffer.GetEOL().ShouldBe("\r\n");

        buffer.ApplyEdits([Op(new TextRange(1, 2, 1, 2), "1\n2\r3")], false, false);

        ValueOf(buffer).ShouldBe("a1\r\n2\r\n3\r\nb");
        buffer.GetLineCount().ShouldBe(4);
    }

    [Fact]
    public void ApplyEdits_Rejects_Overlapping_Ranges()
    {
        var buffer = PieceTreeTextBuffer.Create("abcdef");
        Should.Throw<InvalidOperationException>(() => buffer.ApplyEdits(
            [Op(new TextRange(1, 1, 1, 4), "x"), Op(new TextRange(1, 3, 1, 5), "y")], false, false));
        ValueOf(buffer).ShouldBe("abcdef");
    }

    [Fact]
    public void ApplyEdits_Tracks_Rtl_And_Non_Ascii_Text()
    {
        var buffer = PieceTreeTextBuffer.Create("abc");
        buffer.MightContainRTL().ShouldBeFalse();
        buffer.MightContainNonBasicASCII().ShouldBeFalse();

        buffer.ApplyEdits([Op(new TextRange(1, 1, 1, 1), "שלום")], false, false);

        buffer.MightContainRTL().ShouldBeTrue();
        buffer.MightContainNonBasicASCII().ShouldBeTrue();
    }

    [Fact]
    public void ApplyEdits_Records_Lines_Left_With_Only_Auto_Whitespace()
    {
        var buffer = PieceTreeTextBuffer.Create("if (x) {\n}");

        var result = buffer.ApplyEdits(
            [Op(new TextRange(1, 9, 1, 9), "\n    ", autoWhitespace: true)],
            recordTrimAutoWhitespace: true,
            computeUndoEdits: false);

        ValueOf(buffer).ShouldBe("if (x) {\n    \n}");
        result.TrimAutoWhitespaceLineNumbers.ShouldBe([2]);
    }

    [Fact]
    public void ApplyEdits_Collapses_A_Thousand_Edits_Into_One_Change()
    {
        var text = new StringBuilder();
        for (var i = 0; i < 1200; i++)
        {
            text.Append("line\n");
        }
        var buffer = PieceTreeTextBuffer.Create(text.ToString());
        var edits = new List<ValidAnnotatedEditOperation>();
        for (var i = 1; i <= 1200; i++)
        {
            edits.Add(Op(new TextRange(i, 1, i, 1), "> "));
        }

        var result = buffer.ApplyEdits(edits, false, true);

        result.Changes.Count.ShouldBe(1);
        buffer.GetLineContent(1).ShouldBe("> line");
        buffer.GetLineContent(1200).ShouldBe("> line");

        buffer.ApplyEdits(result.ReverseEdits!.Select(e => Op(e.Range, e.Text)).ToList(), false, false);
        ValueOf(buffer).ShouldBe(text.ToString());
    }

    /// <summary>Random sets of edits match the same edits on a string, and their reverse edits restore the text.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Random_Edits_Match_A_String_And_Undo_Restores_It(string eol)
    {
        var random = new Random(eol.Length * 7919);
        for (var round = 0; round < 300; round++)
        {
            var initial = RandomText(random, eol, random.Next(0, 12));
            var buffer = PieceTreeTextBuffer.Create(initial, eol == "\n" ? DefaultEndOfLine.LF : DefaultEndOfLine.CRLF);
            if (buffer.GetEOL() != eol)
            {
                // a text without line endings takes the default; a text with them keeps its own
                buffer.SetEOL(eol);
            }
            var expected = ValueOf(buffer);
            expected.ShouldBe(initial);

            for (var step = 0; step < 6; step++)
            {
                var before = expected;
                var edits = new List<ValidAnnotatedEditOperation>();
                var builder = new StringBuilder();
                var cursor = 0;
                var count = random.Next(1, 5);
                for (var i = 0; i < count && cursor <= before.Length; i++)
                {
                    var start = SnapToCharBoundary(before, random.Next(cursor, before.Length + 1));
                    var end = SnapToCharBoundary(before, random.Next(start, Math.Min(before.Length, start + 8) + 1));
                    var insert = random.Next(3) == 0 ? string.Empty : RandomText(random, eol, random.Next(0, 3));
                    if (start == end && insert.Length == 0)
                    {
                        continue;
                    }
                    var startPosition = buffer.GetPositionAt(start);
                    var endPosition = buffer.GetPositionAt(end);
                    edits.Add(Op(new TextRange(startPosition, endPosition), insert));
                    builder.Append(before, cursor, start - cursor).Append(insert);
                    cursor = end;
                    if (cursor == before.Length)
                    {
                        break;
                    }
                    cursor++;
                    builder.Append(before[cursor - 1]);
                    if (eol == "\r\n" && before[cursor - 1] == '\r')
                    {
                        builder.Append('\n');
                        cursor++;
                    }
                }
                builder.Append(before, Math.Min(cursor, before.Length), before.Length - Math.Min(cursor, before.Length));
                if (edits.Count == 0)
                {
                    continue;
                }
                expected = builder.ToString();

                var result = buffer.ApplyEdits(edits, false, true);
                ValueOf(buffer).ShouldBe(expected);
                buffer.GetLength().ShouldBe(expected.Length);
                buffer.GetLineCount().ShouldBe(expected.Split(eol).Length);

                // undo, check, redo
                var undo = buffer.ApplyEdits(result.ReverseEdits!.Select(e => Op(e.Range, e.Text)).ToList(), false, true);
                ValueOf(buffer).ShouldBe(before);
                buffer.ApplyEdits(undo.ReverseEdits!.Select(e => Op(e.Range, e.Text)).ToList(), false, false);
                ValueOf(buffer).ShouldBe(expected);
            }
        }
    }

    /// <summary>Keeps an offset out of the middle of a <c>\r\n</c> pair.</summary>
    private static int SnapToCharBoundary(string text, int offset) =>
        offset > 0 && offset < text.Length && text[offset - 1] == '\r' && text[offset] == '\n' ? offset + 1 : offset;

    private static string RandomText(Random random, string eol, int lines)
    {
        var builder = new StringBuilder();
        for (var i = 0; i <= lines; i++)
        {
            if (i > 0)
            {
                builder.Append(eol);
            }
            var length = random.Next(0, 9);
            for (var j = 0; j < length; j++)
            {
                builder.Append((char)random.Next('a', 'z' + 1));
            }
        }
        return builder.ToString();
    }
}

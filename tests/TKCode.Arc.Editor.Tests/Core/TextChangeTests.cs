using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Ported from vscode's <c>src/vs/editor/test/common/model/textChange.test.ts</c>.</summary>
public sealed class TextChangeTests
{
    private readonly record struct Edit(int Offset, int Length, string Text);

    private static string GetResultingContent(string initialContent, IReadOnlyList<Edit> edits)
    {
        var content = initialContent;
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            content = content[..edits[i].Offset] + edits[i].Text + content[(edits[i].Offset + edits[i].Length)..];
        }
        return content;
    }

    private static List<TextChange> GetTextChanges(string initialContent, IReadOnlyList<Edit> edits)
    {
        var content = initialContent;
        var changes = new List<TextChange>(edits.Count);
        var deltaOffset = 0;

        foreach (var edit in edits)
        {
            var position = edit.Offset + deltaOffset;
            var oldText = content.Substring(position, edit.Length);
            content = content[..position] + edit.Text + content[(position + edit.Length)..];
            changes.Add(new TextChange(edit.Offset, oldText, position, edit.Text));
            deltaOffset += edit.Text.Length - edit.Length;
        }

        return changes;
    }

    private static void AssertCompression(string initialText, Edit[] edit1, Edit[] edit2)
    {
        var tmpText = GetResultingContent(initialText, edit1);
        var chg1 = GetTextChanges(initialText, edit1);

        var finalText = GetResultingContent(tmpText, edit2);
        var chg2 = GetTextChanges(tmpText, edit2);

        var compressed = TextChange.CompressConsecutiveTextChanges(chg1, chg2);

        // Check that the compression was correct
        var doEdits = compressed.Select(change => new Edit(change.OldPosition, change.OldLength, change.NewText)).ToList();
        GetResultingContent(initialText, doEdits).ShouldBe(finalText);

        var undoEdits = compressed.Select(change => new Edit(change.NewPosition, change.NewLength, change.OldText)).ToList();
        GetResultingContent(finalText, undoEdits).ShouldBe(initialText);
    }

    [Fact]
    public void Simple_1() => AssertCompression("", [new(0, 0, "h")], [new(1, 0, "e")]);

    [Fact]
    public void Simple_2() => AssertCompression("|", [new(0, 0, "h")], [new(2, 0, "e")]);

    [Fact]
    public void Complex_1() => AssertCompression(
        "abcdefghij",
        [new(0, 3, "qh"), new(5, 0, "1"), new(8, 2, "X")],
        [new(1, 0, "Z"), new(3, 3, "Y")]);

    [Fact]
    public void Gen_1() => AssertCompression("kxm", [new(0, 1, "tod_neu")], [new(1, 2, "sag_e")]);

    [Fact]
    public void Gen_2() => AssertCompression("kpb_r_v", [new(5, 2, "a_jvf_l")], [new(10, 2, "w")]);

    [Fact]
    public void Gen_3() => AssertCompression("slu_w", [new(4, 1, "_wfw")], [new(3, 5, "")]);

    [Fact]
    public void Gen_4() => AssertCompression("_e", [new(2, 0, "zo_b")], [new(1, 3, "tra")]);

    [Fact]
    public void Gen_5() => AssertCompression("ssn_", [new(0, 2, "tat_nwe")], [new(2, 6, "jm")]);

    [Fact]
    public void Gen_6() => AssertCompression("kl_nru", [new(4, 1, "")], [new(1, 4, "__ut")]);

    /// <summary>Upstream's generator loop (off by default there), run here for a fixed number of seeded cases.</summary>
    [Fact]
    public void Generated_Compressions_Round_Trip()
    {
        var random = new Random(20261124);
        for (var n = 0; n < 2000; n++)
        {
            var content = RandomBuffer(random, small: false).Replace('\n', '_');
            var edits1 = RandomEdits(random, content);
            var tmp = GetResultingContent(content, edits1);
            var edits2 = RandomEdits(random, tmp);
            AssertCompression(content, edits1, edits2);
        }
    }

    private static string RandomString(Random random, int minLength, int maxLength)
    {
        var length = random.Next(minLength, maxLength + 1);
        return string.Create(length, random, static (chars, r) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)r.Next('a', 'z' + 1);
            }
        });
    }

    private static string RandomBuffer(Random random, bool small)
    {
        var lineCount = random.Next(1, (small ? 3 : 10) + 1);
        var lines = new System.Text.StringBuilder();
        for (var i = 0; i < lineCount; i++)
        {
            lines.Append(RandomString(random, 0, small ? 3 : 10));
            lines.Append(random.Next(1, 4) switch { 1 => "\r", 2 => "\n", _ => "\r\n" });
        }
        return lines.ToString();
    }

    private static Edit[] RandomEdits(Random random, string content)
    {
        var result = new List<Edit>();
        var cnt = random.Next(1, 6);
        var maxOffset = content.Length;

        while (cnt > 0 && maxOffset > 0)
        {
            var offset = random.Next(0, maxOffset + 1);
            var length = random.Next(0, maxOffset - offset + 1);
            result.Add(new Edit(offset, length, RandomBuffer(random, small: true).Replace('\n', '_')));
            maxOffset = offset;
            cnt--;
        }

        result.Reverse();
        return result.ToArray();
    }

    [Fact]
    public void Issue_118041_Unicode_Character_Undo_Bug()
    {
        var textChange = new TextChange(428, "\uFEFF", 428, "");
        var buffer = new byte[textChange.WriteSize()];
        textChange.Write(buffer, 0).ShouldBe(buffer.Length);
        var actual = new List<TextChange>();
        TextChange.Read(buffer, 0, actual);
        actual[0].ShouldBe(textChange);
    }

    [Theory]
    [InlineData("", 0, 0, 0, StringEol.Unknown)]
    [InlineData("abc", 0, 3, 3, StringEol.Unknown)]
    [InlineData("ab\ncd\nefg", 2, 2, 3, StringEol.LF)]
    [InlineData("ab\r\ncd\r\n", 2, 2, 0, StringEol.CRLF)]
    [InlineData("ab\rcd", 1, 2, 2, StringEol.Invalid)]
    [InlineData("a\nb\r\nc", 2, 1, 1, StringEol.Invalid)]
    public void CountEOL(string text, int count, int firstLineLength, int lastLineLength, object eol)
    {
        EolCounter.CountEOL(text).ShouldBe(new EolCount(count, firstLineLength, lastLineLength, (StringEol)eol));
    }
}

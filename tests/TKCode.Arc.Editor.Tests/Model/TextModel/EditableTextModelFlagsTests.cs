// Ported from vscode (src/vs/editor/test/common/model/editableTextModel.test.ts, commit b57d5c9e), MIT license.
// The flag suites and the CRLF suite; the applyEdits suite is in EditableTextModelTests.cs.
// The mirror model is the TestMirrorTextModel of EditableTextModelTestUtils.cs; the reverse edits that ApplyEdits
// returns are turned back into SingleEditOperation before they are applied again.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.EditableTextModelTestUtils;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Upstream suite 'EditorModel - EditableTextModel.applyEdits updates mightContainRTL'.</summary>
public sealed class EditableTextModelMightContainRTLTests
{
    private static void TestApplyEdits(string[] original, SingleEditOperation[] edits, bool before, bool after)
    {
        using var model = TestTextModel.Create(string.Join('\n', original));
        model.SetEOL(EndOfLineSequence.LF);

        model.MightContainRTL().ShouldBe(before);

        model.ApplyEdits(edits);
        model.MightContainRTL().ShouldBe(after);
    }

    private static SingleEditOperation EditOp(int startLineNumber, int startColumn, int endLineNumber, int endColumn, string[] text) =>
        new(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), string.Join('\n', text));

    [Fact]
    public void Start_With_RTL_Insert_LTR()
    {
        TestApplyEdits(["Hello,\nזוהי עובדה מבוססת שדעתו"], [EditOp(1, 1, 1, 1, ["hello"])], true, true);
    }

    [Fact]
    public void Start_With_RTL_Delete_RTL()
    {
        TestApplyEdits(["Hello,\nזוהי עובדה מבוססת שדעתו"], [EditOp(1, 1, 10, 10, [""])], true, true);
    }

    [Fact]
    public void Start_With_RTL_Insert_RTL()
    {
        TestApplyEdits(["Hello,\nזוהי עובדה מבוססת שדעתו"], [EditOp(1, 1, 1, 1, ["هناك حقيقة مثبتة منذ زمن طويل"])], true, true);
    }

    [Fact]
    public void Start_With_LTR_Insert_LTR()
    {
        TestApplyEdits(["Hello,\nworld!"], [EditOp(1, 1, 1, 1, ["hello"])], false, false);
    }

    [Fact]
    public void Start_With_LTR_Insert_RTL_1()
    {
        TestApplyEdits(["Hello,\nworld!"], [EditOp(1, 1, 1, 1, ["هناك حقيقة مثبتة منذ زمن طويل"])], false, true);
    }

    [Fact]
    public void Start_With_LTR_Insert_RTL_2()
    {
        TestApplyEdits(["Hello,\nworld!"], [EditOp(1, 1, 1, 1, ["זוהי עובדה מבוססת שדעתו"])], false, true);
    }
}

/// <summary>Upstream suite 'EditorModel - EditableTextModel.applyEdits updates mightContainNonBasicASCII'.</summary>
public sealed class EditableTextModelMightContainNonBasicASCIITests
{
    private static void TestApplyEdits(string[] original, SingleEditOperation[] edits, bool before, bool after)
    {
        using var model = TestTextModel.Create(string.Join('\n', original));
        model.SetEOL(EndOfLineSequence.LF);

        model.MightContainNonBasicASCII().ShouldBe(before);

        model.ApplyEdits(edits);
        model.MightContainNonBasicASCII().ShouldBe(after);
    }

    private static SingleEditOperation EditOp(int startLineNumber, int startColumn, int endLineNumber, int endColumn, string[] text) =>
        new(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), string.Join('\n', text));

    [Fact]
    public void Start_With_NON_ASCII_Insert_ASCII()
    {
        TestApplyEdits(["Hello,\nZürich"], [EditOp(1, 1, 1, 1, ["hello", "second line"])], true, true);
    }

    [Fact]
    public void Start_With_NON_ASCII_Delete_NON_ASCII()
    {
        TestApplyEdits(["Hello,\nZürich"], [EditOp(1, 1, 10, 10, [""])], true, true);
    }

    [Fact]
    public void Start_With_NON_ASCII_Insert_NON_ASCII()
    {
        TestApplyEdits(["Hello,\nZürich"], [EditOp(1, 1, 1, 1, ["Zürich"])], true, true);
    }

    [Fact]
    public void Start_With_ASCII_Insert_ASCII()
    {
        TestApplyEdits(["Hello,\nworld!"], [EditOp(1, 1, 1, 1, ["hello", "second line"])], false, false);
    }

    [Fact]
    public void Start_With_ASCII_Insert_NON_ASCII()
    {
        TestApplyEdits(["Hello,\nworld!"], [EditOp(1, 1, 1, 1, ["Zürich", "Zürich"])], false, true);
    }
}

/// <summary>Upstream suite 'CRLF edit normalization'.</summary>
public sealed class CrlfEditNormalizationTests
{
    [Fact]
    public void Edit_Ending_With_CR_Followed_By_LF_In_Buffer_Should_Strip_Trailing_CR()
    {
        // Document: "abc\r\ndef\r\n"
        // Edit: Replace range (1,1)-(1,4) "abc" with "xyz\r"
        // The \r at end of replacement should be stripped since next char is \n
        using var model = TestTextModel.Create("abc\r\ndef\r\n");
        model.SetEOL(EndOfLineSequence.CRLF);

        model.GetEOL().ShouldBe("\r\n");
        model.GetLineCount().ShouldBe(3);
        model.GetLineContent(1).ShouldBe("abc");
        model.GetLineContent(2).ShouldBe("def");

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 4), "xyz\r"),
        ]);

        // The trailing \r should be stripped, so we get "xyz" not "xyz\r"
        model.GetLineContent(1).ShouldBe("xyz");
        model.GetLineContent(2).ShouldBe("def");
        model.GetLineCount().ShouldBe(3);
    }

    [Fact]
    public void Edit_Ending_With_CRLF_Should_NOT_Be_Modified()
    {
        // Document: "abc\r\ndef\r\n"
        // Edit: Replace range (1,1)-(1,4) "abc" with "xyz\r\n"
        // This is a proper CRLF so should not be modified
        using var model = TestTextModel.Create("abc\r\ndef\r\n");
        model.SetEOL(EndOfLineSequence.CRLF);

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 4), "xyz\r\n"),
        ]);

        // Should add a new line
        model.GetLineContent(1).ShouldBe("xyz");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("def");
        model.GetLineCount().ShouldBe(4);
    }

    [Fact]
    public void Edit_Ending_With_CR_NOT_Followed_By_LF_Should_NOT_Be_Modified()
    {
        // Document: "abcdef" (no newline after)
        // Edit: Replace range (1,1)-(1,4) "abc" with "xyz\r"
        // Since there's no \n after the range, the \r should stay
        using var model = TestTextModel.Create("abcdef");
        model.SetEOL(EndOfLineSequence.CRLF);

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 4), "xyz\r"),
        ]);

        // The \r should cause a new line since buffer normalizes EOL
        // Actually since buffer uses CRLF, the lone \r will be normalized to \r\n
        model.GetLineCount().ShouldBe(2);
    }

    [Fact]
    public void Edit_In_LF_Buffer_Should_NOT_Strip_Trailing_CR()
    {
        // Document with LF: "abc\ndef\n"
        // Edit: Replace range (1,1)-(1,4) "abc" with "xyz\r"
        // Since buffer is LF, no special handling needed
        using var model = TestTextModel.Create("abc\ndef\n");
        model.SetEOL(EndOfLineSequence.LF);

        model.GetEOL().ShouldBe("\n");
        model.GetLineCount().ShouldBe(3);

        model.ApplyEdits([
            new SingleEditOperation(new TextRange(1, 1, 1, 4), "xyz\r"),
        ]);

        // The \r will be normalized to \n (buffer's EOL)
        model.GetLineCount().ShouldBe(4);
    }

    [Fact]
    public void LSP_Include_Sorting_Scenario_Edit_Ending_With_CR_Should_Be_Normalized()
    {
        // This is the real-world scenario from the issue
        // Document: "#include \"a.h\"\r\n#include \"c.h\"\r\n#include \"b.h\"\r\n"
        // Edit: Replace lines 1-3 with reordered includes ending with \r
        using var model = TestTextModel.Create("#include \"a.h\"\r\n#include \"c.h\"\r\n#include \"b.h\"\r\n");
        model.SetEOL(EndOfLineSequence.CRLF);

        model.GetEOL().ShouldBe("\r\n");
        model.GetLineCount().ShouldBe(4);
        model.GetLineContent(1).ShouldBe("#include \"a.h\"");
        model.GetLineContent(2).ShouldBe("#include \"c.h\"");
        model.GetLineContent(3).ShouldBe("#include \"b.h\"");

        // Edit: replace range (1,1)-(3,16) with text ending in \r
        // Range covers: #include "a.h"\r\n#include "c.h"\r\n#include "b.h"
        // Note: line 3 col 16 is after the last char "h" but before the \r\n
        model.ApplyEdits([
            new SingleEditOperation(
                new TextRange(1, 1, 3, 16),
                "#include \"a.h\"\r\n#include \"b.h\"\r\n#include \"c.h\"\r"),
        ]);

        // The trailing \r should be stripped because the next char after range is \n
        model.GetLineCount().ShouldBe(4);
        model.GetLineContent(1).ShouldBe("#include \"a.h\"");
        model.GetLineContent(2).ShouldBe("#include \"b.h\"");
        model.GetLineContent(3).ShouldBe("#include \"c.h\"");
        model.GetLineContent(4).ShouldBe("");
    }
}

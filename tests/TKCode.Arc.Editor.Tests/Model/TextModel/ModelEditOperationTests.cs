// Ported from vscode (src/vs/editor/test/common/model/modelEditOperation.test.ts, commit b57d5c9e), MIT license.
// The inverse edits that ApplyEdits returns are ValidEditOperations; they are turned back into SingleEditOperations to
// apply them, and compared on range, text and forceMoveMarkers (which a ValidEditOperation does not carry: false).

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelEditOperationTests : IDisposable
{
    private const string Line1 = "My First Line";
    private const string Line2 = "\t\tMy Second Line";
    private const string Line3 = "    Third Line";
    private const string Line4 = "";
    private const string Line5 = "1";

    private readonly TextModel _model;

    public ModelEditOperationTests()
    {
        var text =
            Line1 + "\r\n" +
            Line2 + "\n" +
            Line3 + "\n" +
            Line4 + "\r\n" +
            Line5;
        _model = TestTextModel.Create(text);
    }

    public void Dispose() => _model.Dispose();

    private static SingleEditOperation CreateSingleEditOp(
        string text,
        int positionLineNumber,
        int positionColumn,
        int? selectionLineNumber = null,
        int? selectionColumn = null)
    {
        var range = new TextRange(
            selectionLineNumber ?? positionLineNumber,
            selectionColumn ?? positionColumn,
            positionLineNumber,
            positionColumn);

        return new SingleEditOperation(range, text, false);
    }

    private void AssertSingleEditOp(SingleEditOperation singleEditOp, string[] editedLines)
    {
        SingleEditOperation[] editOp = [singleEditOp];

        var inverseEditOp = _model.ApplyEdits(editOp, true);
        inverseEditOp.ShouldNotBeNull();

        _model.GetLineCount().ShouldBe(editedLines.Length);
        for (var i = 0; i < editedLines.Length; i++)
        {
            _model.GetLineContent(i + 1).ShouldBe(editedLines[i]);
        }

        var originalOp = _model.ApplyEdits(inverseEditOp.Select(op => new SingleEditOperation(op.Range, op.Text)).ToList(), true);
        originalOp.ShouldNotBeNull();

        _model.GetLineCount().ShouldBe(5);
        _model.GetLineContent(1).ShouldBe(Line1);
        _model.GetLineContent(2).ShouldBe(Line2);
        _model.GetLineContent(3).ShouldBe(Line3);
        _model.GetLineContent(4).ShouldBe(Line4);
        _model.GetLineContent(5).ShouldBe(Line5);

        originalOp.Select(edit => (edit.Range, Text: (string?)edit.Text, ForceMoveMarkers: false))
            .ShouldBe(editOp.Select(edit => (edit.Range, edit.Text, edit.ForceMoveMarkers)));
    }

    [Fact]
    public void Insert_Inline()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("a", 1, 1),
            [
                "aMy First Line",
                Line2,
                Line3,
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Inline_1()
    {
        AssertSingleEditOp(
            CreateSingleEditOp(" incredibly awesome", 1, 3),
            [
                "My incredibly awesome First Line",
                Line2,
                Line3,
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Inline_2()
    {
        AssertSingleEditOp(
            CreateSingleEditOp(" with text at the end.", 1, 14),
            [
                "My First Line with text at the end.",
                Line2,
                Line3,
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Inline_3()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("My new First Line.", 1, 1, 1, 14),
            [
                "My new First Line.",
                Line2,
                Line3,
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Multi_Line_1()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("My new First Line.", 1, 1, 3, 15),
            [
                "My new First Line.",
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Multi_Line_2()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("My new First Line.", 1, 2, 3, 15),
            [
                "MMy new First Line.",
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Inline_Multi_Line_3()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("My new First Line.", 1, 2, 3, 2),
            [
                "MMy new First Line.   Third Line",
                Line4,
                Line5,
            ]);
    }

    [Fact]
    public void Replace_Muli_Line_Multi_Line()
    {
        AssertSingleEditOp(
            CreateSingleEditOp("1\n2\n3\n4\n", 1, 1),
            [
                "1",
                "2",
                "3",
                "4",
                Line1,
                Line2,
                Line3,
                Line4,
                Line5,
            ]);
    }
}

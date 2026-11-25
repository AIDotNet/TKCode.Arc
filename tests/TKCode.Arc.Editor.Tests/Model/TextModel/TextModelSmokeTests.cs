// Not from vscode: a first check that the pieces of TextModel work together (edits, undo steps, decorations,
// events), before the upstream suites.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextModelSmokeTests
{
    [Fact]
    public async Task Edits_Undo_And_Redo_Through_The_Undo_Stack()
    {
        using var model = TestTextModel.Create("hello\nworld");
        var events = new List<ModelContentChangedEvent>();
        model.DidChangeContent += events.Add;

        model.PushEditOperations(null, [EditOperation.Insert(new TextPosition(1, 6), " there")], null);
        model.PushStackElement();
        model.PushEditOperations(null, [EditOperation.Delete(new TextRange(2, 1, 2, 6))], null);

        model.GetValue().ShouldBe("hello there\n");
        model.GetVersionId().ShouldBe(3);
        events.Count.ShouldBe(2);
        model.CanUndo().ShouldBeTrue();

        await model.Undo();
        model.GetValue().ShouldBe("hello there\nworld");
        model.GetAlternativeVersionId().ShouldBe(2);
        events[^1].IsUndoing.ShouldBeTrue();

        await model.Undo();
        model.GetValue().ShouldBe("hello\nworld");
        model.GetAlternativeVersionId().ShouldBe(1);
        model.CanUndo().ShouldBeFalse();

        await model.Redo();
        await model.Redo();
        model.GetValue().ShouldBe("hello there\n");
        model.GetAlternativeVersionId().ShouldBe(3);
        model.GetVersionId().ShouldBe(7);
    }

    [Fact]
    public void Decorations_Move_With_Edits()
    {
        using var model = TestTextModel.Create("abc def\nghi");
        var decorationEvents = 0;
        model.DidChangeDecorations += _ => decorationEvents++;

        var ids = model.DeltaDecorations(null, [new ModelDeltaDecoration(new TextRange(1, 5, 1, 8), new ModelDecorationOptions("test"))]);
        decorationEvents.ShouldBe(1);

        model.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "xx\n")]);
        model.GetDecorationRange(ids[0]).ShouldBe(new TextRange(2, 5, 2, 8));
        model.GetLineDecorations(2).Select(d => d.Id).ShouldBe(ids);

        model.SetEOL(EndOfLineSequence.CRLF);
        model.GetDecorationRange(ids[0]).ShouldBe(new TextRange(2, 5, 2, 8));

        model.DeltaDecorations(ids, []);
        model.GetAllDecorations().ShouldBeEmpty();
    }

    [Fact]
    public void A_Batch_Raises_One_Content_Event()
    {
        using var model = TestTextModel.Create("a\nb\nc");
        var events = new List<ModelContentChangedEvent>();
        model.DidChangeContent += events.Add;

        model.ApplyEdits(
        [
            EditOperation.Replace(new TextRange(1, 1, 1, 2), "A"),
            EditOperation.Replace(new TextRange(3, 1, 3, 2), "C"),
        ]);

        events.Count.ShouldBe(1);
        events[0].Changes.Count.ShouldBe(2);
        model.GetValue().ShouldBe("A\nb\nC");
    }

    [Fact]
    public void Disposed_Model_Refuses_Calls()
    {
        var model = TestTextModel.Create("x");
        var disposed = false;
        model.WillDispose += () => disposed = true;
        model.Dispose();

        disposed.ShouldBeTrue();
        model.IsDisposed().ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => model.GetValue());
    }
}

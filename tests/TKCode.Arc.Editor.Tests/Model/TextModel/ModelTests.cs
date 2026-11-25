// Ported from vscode (src/vs/editor/test/common/model/model.test.ts, commit b57d5c9e), MIT license.
// Suites 'Editor Model - Model' (ModelTests) and 'Editor Model - Model Line Separators' (ModelLineSeparatorsTests).
// The spy view model implements IViewModelContentListener directly; it records an unexpected event instead of failing inside the
// callback (the model reports a view model's exceptions instead of letting them through), and the raw events are
// compared change by change since the C# event classes have no structural equality. The 'Editor Model - Words' suite
// needs getWordAtPosition, the tokenization registry and language configuration, which the port does not have yet.

using Shouldly;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelTests : IDisposable
{
    private const string Line1 = "My First Line";
    private const string Line2 = "\t\tMy Second Line";
    private const string Line3 = "    Third Line";
    private const string Line4 = "";
    private const string Line5 = "1";

    private readonly TextModel _thisModel;

    public ModelTests()
    {
        var text =
            Line1 + "\r\n" +
            Line2 + "\n" +
            Line3 + "\n" +
            Line4 + "\r\n" +
            Line5;
        _thisModel = TestTextModel.Create(text);
    }

    public void Dispose() => _thisModel.Dispose();

    // --------- insert text

    [Fact]
    public void Model_GetValue()
    {
        _thisModel.GetValue().ShouldBe("My First Line\n\t\tMy Second Line\n    Third Line\n\n1");
    }

    [Fact]
    public void Model_Insert_Empty_Text()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "")]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("My First Line");
    }

    [Fact]
    public void Model_Insert_Text_Without_Newline_1()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "foo ")]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("foo My First Line");
    }

    [Fact]
    public void Model_Insert_Text_Without_Newline_2()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 3), " foo")]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("My foo First Line");
    }

    [Fact]
    public void Model_Insert_Text_With_One_Newline()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 3), " new line\nNo longer")]);
        _thisModel.GetLineCount().ShouldBe(6);
        _thisModel.GetLineContent(1).ShouldBe("My new line");
        _thisModel.GetLineContent(2).ShouldBe("No longer First Line");
    }

    [Fact]
    public void Model_Insert_Text_With_Two_Newlines()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 3), " new line\nOne more line in the middle\nNo longer")]);
        _thisModel.GetLineCount().ShouldBe(7);
        _thisModel.GetLineContent(1).ShouldBe("My new line");
        _thisModel.GetLineContent(2).ShouldBe("One more line in the middle");
        _thisModel.GetLineContent(3).ShouldBe("No longer First Line");
    }

    [Fact]
    public void Model_Insert_Text_With_Many_Newlines()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 3), "\n\n\n\n")]);
        _thisModel.GetLineCount().ShouldBe(9);
        _thisModel.GetLineContent(1).ShouldBe("My");
        _thisModel.GetLineContent(2).ShouldBe("");
        _thisModel.GetLineContent(3).ShouldBe("");
        _thisModel.GetLineContent(4).ShouldBe("");
        _thisModel.GetLineContent(5).ShouldBe(" First Line");
    }

    // --------- insert text eventing

    private sealed class SpyViewModel : IViewModelContentListener
    {
        public ModelRawContentChangedEvent? Event { get; private set; }

        public bool Unexpected { get; private set; }

        public void OnDidChangeContentOrInjectedText(IModelContentOrInjectedTextChangedEvent e)
        {
            if (Event is not null || e is not InternalModelContentChangeEvent contentEvent)
            {
                Unexpected = true;
                return;
            }

            Event = contentEvent.RawContentChangedEvent;
        }

        public void EmitContentChangeEvent(IModelContentOrInjectedTextChangedEvent e)
        {
        }
    }

    private ModelRawContentChangedEvent? WithEventCapturing(Action callback)
    {
        var spyViewModel = new SpyViewModel();
        _thisModel.RegisterViewModel(spyViewModel);
        callback();
        _thisModel.UnregisterViewModel(spyViewModel);
        spyViewModel.Unexpected.ShouldBeFalse("Unexpected assertion error");
        return spyViewModel.Event;
    }

    private sealed record RawFlush;

    private sealed record RawLineChanged(int LineNumber, int LineNumberPostEdit);

    private sealed record RawLinesDeleted(int FromLineNumber, int ToLineNumber, int LastUntouchedLinePostEdit);

    private sealed record RawLinesInserted(int FromLineNumber, int FromLineNumberPostEdit, int Count);

    private sealed record RawEolChanged;

    private static object Describe(ModelRawChange change) => change switch
    {
        ModelRawFlush => new RawFlush(),
        ModelRawLineChanged c => new RawLineChanged(c.LineNumber, c.LineNumberPostEdit),
        ModelRawLinesDeleted c => new RawLinesDeleted(c.FromLineNumber, c.ToLineNumber, c.LastUntouchedLinePostEdit),
        ModelRawLinesInserted c => new RawLinesInserted(c.FromLineNumber, c.FromLineNumberPostEdit, c.Count),
        ModelRawEOLChanged => new RawEolChanged(),
        _ => throw new InvalidOperationException($"Unknown change {change.ChangeType}"),
    };

    /// <summary>Upstream's assert.deepStrictEqual on two ModelRawContentChangedEvents.</summary>
    private static void AssertRawEvent(ModelRawContentChangedEvent? actual, ModelRawContentChangedEvent expected)
    {
        actual.ShouldNotBeNull();
        actual.Changes.Select(Describe).ShouldBe(expected.Changes.Select(Describe));
        actual.VersionId.ShouldBe(expected.VersionId);
        actual.IsUndoing.ShouldBe(expected.IsUndoing);
        actual.IsRedoing.ShouldBe(expected.IsRedoing);
        actual.ResultingSelection.ShouldBe(expected.ResultingSelection);
    }

    [Fact]
    public void Model_Insert_Empty_Text_Does_Not_Trigger_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "")]);
        });
        e.ShouldBeNull("was not expecting event");
    }

    [Fact]
    public void Model_Insert_Text_Without_Newline_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "foo ")]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
            ],
            2,
            false,
            false));
    }

    [Fact]
    public void Model_Insert_Text_With_One_Newline_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 3), " new line\nNo longer")]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
                new ModelRawLinesInserted(2, 2, 1),
            ],
            2,
            false,
            false));
    }

    // --------- delete text

    [Fact]
    public void Model_Delete_Empty_Text()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 1))]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("My First Line");
    }

    [Fact]
    public void Model_Delete_Text_From_One_Line()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 2))]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("y First Line");
    }

    [Fact]
    public void Model_Delete_Text_From_One_Line_2()
    {
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "a")]);
        _thisModel.GetLineContent(1).ShouldBe("aMy First Line");

        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 2, 1, 4))]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("a First Line");
    }

    [Fact]
    public void Model_Delete_All_Text_From_A_Line()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 14))]);
        _thisModel.GetLineCount().ShouldBe(5);
        _thisModel.GetLineContent(1).ShouldBe("");
    }

    [Fact]
    public void Model_Delete_Text_From_Two_Lines()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 4, 2, 6))]);
        _thisModel.GetLineCount().ShouldBe(4);
        _thisModel.GetLineContent(1).ShouldBe("My Second Line");
    }

    [Fact]
    public void Model_Delete_Text_From_Many_Lines()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 4, 3, 5))]);
        _thisModel.GetLineCount().ShouldBe(3);
        _thisModel.GetLineContent(1).ShouldBe("My Third Line");
    }

    [Fact]
    public void Model_Delete_Everything()
    {
        _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 5, 2))]);
        _thisModel.GetLineCount().ShouldBe(1);
        _thisModel.GetLineContent(1).ShouldBe("");
    }

    // --------- delete text eventing

    [Fact]
    public void Model_Delete_Empty_Text_Does_Not_Trigger_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 1))]);
        });
        e.ShouldBeNull("was not expecting event");
    }

    [Fact]
    public void Model_Delete_Text_From_One_Line_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 2))]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
            ],
            2,
            false,
            false));
    }

    [Fact]
    public void Model_Delete_All_Text_From_A_Line_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 1, 1, 14))]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
            ],
            2,
            false,
            false));
    }

    [Fact]
    public void Model_Delete_Text_From_Two_Lines_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 4, 2, 6))]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
                new ModelRawLinesDeleted(2, 2, 1),
            ],
            2,
            false,
            false));
    }

    [Fact]
    public void Model_Delete_Text_From_Many_Lines_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.ApplyEdits([EditOperation.Delete(new TextRange(1, 4, 3, 5))]);
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawLineChanged(1, 1),
                new ModelRawLinesDeleted(2, 3, 1),
            ],
            2,
            false,
            false));
    }

    // --------- getValueInRange

    [Fact]
    public void GetValueInRange()
    {
        _thisModel.GetValueInRange(new TextRange(1, 1, 1, 1)).ShouldBe("");
        _thisModel.GetValueInRange(new TextRange(1, 1, 1, 2)).ShouldBe("M");
        _thisModel.GetValueInRange(new TextRange(1, 2, 1, 3)).ShouldBe("y");
        _thisModel.GetValueInRange(new TextRange(1, 1, 1, 14)).ShouldBe("My First Line");
        _thisModel.GetValueInRange(new TextRange(1, 1, 2, 1)).ShouldBe("My First Line\n");
        _thisModel.GetValueInRange(new TextRange(1, 1, 2, 2)).ShouldBe("My First Line\n\t");
        _thisModel.GetValueInRange(new TextRange(1, 1, 2, 3)).ShouldBe("My First Line\n\t\t");
        _thisModel.GetValueInRange(new TextRange(1, 1, 2, 17)).ShouldBe("My First Line\n\t\tMy Second Line");
        _thisModel.GetValueInRange(new TextRange(1, 1, 3, 1)).ShouldBe("My First Line\n\t\tMy Second Line\n");
        _thisModel.GetValueInRange(new TextRange(1, 1, 4, 1)).ShouldBe("My First Line\n\t\tMy Second Line\n    Third Line\n");
    }

    // --------- getValueLengthInRange

    [Fact]
    public void GetValueLengthInRange()
    {
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 1, 1)).ShouldBe("".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 1, 2)).ShouldBe("M".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 2, 1, 3)).ShouldBe("y".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 1, 14)).ShouldBe("My First Line".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 2, 1)).ShouldBe("My First Line\n".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 2, 2)).ShouldBe("My First Line\n\t".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 2, 3)).ShouldBe("My First Line\n\t\t".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 2, 17)).ShouldBe("My First Line\n\t\tMy Second Line".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 3, 1)).ShouldBe("My First Line\n\t\tMy Second Line\n".Length);
        _thisModel.GetValueLengthInRange(new TextRange(1, 1, 4, 1)).ShouldBe("My First Line\n\t\tMy Second Line\n    Third Line\n".Length);
    }

    // --------- setValue

    [Fact]
    public void SetValue_Eventing()
    {
        var e = WithEventCapturing(() =>
        {
            _thisModel.SetValue("new value");
        });
        AssertRawEvent(e, new ModelRawContentChangedEvent(
            [
                new ModelRawFlush(),
            ],
            2,
            false,
            false));
    }

    [Fact]
    public void Issue_46342_Maintain_Edit_Operation_Order_In_ApplyEdits()
    {
        var res = _thisModel.ApplyEdits(
        [
            new SingleEditOperation(new TextRange(2, 1, 2, 1), "a"),
            new SingleEditOperation(new TextRange(1, 1, 1, 1), "b"),
        ], true);

        res.ShouldNotBeNull();
        res[0].Range.ShouldBe(new TextRange(2, 1, 2, 2));
        res[1].Range.ShouldBe(new TextRange(1, 1, 1, 2));
    }
}

// --------- Special Unicode LINE SEPARATOR character

public sealed class ModelLineSeparatorsTests : IDisposable
{
    private const string Line1 = "My First Line";
    private const string Line2 = "\t\tMy Second Line";
    private const string Line3 = "    Third Line";
    private const string Line4 = "";
    private const string Line5 = "1";

    private readonly TextModel _thisModel;

    public ModelLineSeparatorsTests()
    {
        var text =
            Line1 + "\u2028" +
            Line2 + "\n" +
            Line3 + "\u2028" +
            Line4 + "\r\n" +
            Line5;
        _thisModel = TestTextModel.Create(text);
    }

    public void Dispose() => _thisModel.Dispose();

    [Fact]
    public void Model_GetValue()
    {
        _thisModel.GetValue().ShouldBe("My First Line\u2028\t\tMy Second Line\n    Third Line\u2028\n1");
    }

    [Fact]
    public void Model_Lines()
    {
        _thisModel.GetLineCount().ShouldBe(3);
    }

    [Fact]
    public void Bug_13333_Model_Should_Line_Break_On_Lonely_CR_Too()
    {
        using var model = TestTextModel.Create("Hello\rWorld!\r\nAnother line");
        model.GetLineCount().ShouldBe(3);
        model.GetValue().ShouldBe("Hello\r\nWorld!\r\nAnother line");
    }
}

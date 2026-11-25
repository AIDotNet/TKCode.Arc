// Ported from vscode (src/vs/editor/test/common/model/modelDecorations.test.ts, commit b57d5c9e), MIT license.
// This part: the suites 'Editor Model - Model Decorations' and 'deltaDecorations'; 'Decorations and editing' is in
// DecorationsAndEditingTests.cs. Adapted: the table of editing cases becomes one method per case; hover messages are
// lists of MarkdownString (upstream also takes a single IMarkdownString); the decoration queries return copies
// (ModelDecoration), compared as (range, class name) pairs like upstream's light-weight decorations.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ModelDecorationsTests : IDisposable
{
    private const string Line1 = "My First Line";
    private const string Line2 = "\t\tMy Second Line";
    private const string Line3 = "    Third Line";
    private const string Line4 = "";
    private const string Line5 = "1";

    private readonly TextModel _thisModel;

    public ModelDecorationsTests()
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

    // --------- utils

    private static void ModelHasDecorations(TextModel model, (TextRange Range, string? ClassName)[] decorations)
    {
        var modelDecorations = new List<(TextRange Range, string? ClassName)>();
        var actualDecorations = model.GetAllDecorations();
        for (int i = 0, len = actualDecorations.Count; i < len; i++)
        {
            modelDecorations.Add((actualDecorations[i].Range, actualDecorations[i].Options.ClassName));
        }

        // A stable sort, like Array.prototype.sort.
        var sorted = modelDecorations
            .Select((d, index) => (d, index))
            .OrderBy(x => x.d.Range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .ThenBy(x => x.index)
            .Select(x => x.d)
            .ToList();
        sorted.ShouldBe(decorations);
    }

    private static void ModelHasDecoration(
        TextModel model,
        int startLineNumber,
        int startColumn,
        int endLineNumber,
        int endColumn,
        string className) =>
        ModelHasDecorations(model, [(new TextRange(startLineNumber, startColumn, endLineNumber, endColumn), className)]);

    private static void ModelHasNoDecorations(TextModel model) =>
        model.GetAllDecorations().Count.ShouldBe(0, "Model has no decoration");

    private static string AddDecoration(
        TextModel model,
        int startLineNumber,
        int startColumn,
        int endLineNumber,
        int endColumn,
        string className) =>
        model.ChangeDecorations(changeAccessor => changeAccessor.AddDecoration(
            new TextRange(startLineNumber, startColumn, endLineNumber, endColumn),
            new ModelDecorationOptions("test") { ClassName = className }))!;

    private static void LineHasDecorations(TextModel model, int lineNumber, (int Start, int End, string? ClassName)[] decorations)
    {
        var lineDecorations = new List<(int Start, int End, string? ClassName)>();
        var decs = model.GetLineDecorations(lineNumber);
        for (int i = 0, len = decs.Count; i < len; i++)
        {
            lineDecorations.Add((decs[i].Range.StartColumn, decs[i].Range.EndColumn, decs[i].Options.ClassName));
        }

        lineDecorations.ShouldBe(decorations, false, "Line decorations");
    }

    private static void LineHasNoDecorations(TextModel model, int lineNumber) => LineHasDecorations(model, lineNumber, []);

    private static void LineHasDecoration(TextModel model, int lineNumber, int start, int end, string className) =>
        LineHasDecorations(model, lineNumber, [(start, end, className)]);

    [Fact]
    public void Single_Character_Decoration()
    {
        AddDecoration(_thisModel, 1, 1, 1, 2, "myType");
        LineHasDecoration(_thisModel, 1, 1, 2, "myType");
        LineHasNoDecorations(_thisModel, 2);
        LineHasNoDecorations(_thisModel, 3);
        LineHasNoDecorations(_thisModel, 4);
        LineHasNoDecorations(_thisModel, 5);
    }

    [Fact]
    public void Injected_Text_Width_Must_Be_Finite_And_Non_Negative()
    {
        double[] widths = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 0, 1];
        var decorationIds = _thisModel.DeltaDecorations([], widths.Select(widthInEm => new ModelDeltaDecoration(
            new TextRange(1, 1, 1, 1),
            new ModelDecorationOptions("test")
            {
                After = new ModelDecorationInjectedTextOptions("x") { WidthInEm = widthInEm },
            })).ToList());

        decorationIds.Select(id => _thisModel.GetDecorationOptions(id)?.After?.WidthInEm).ToList().ShouldBe(
        [
            null,
            null,
            null,
            null,
            0,
            1,
        ]);
    }

    [Fact]
    public void Line_Decoration()
    {
        AddDecoration(_thisModel, 1, 1, 1, 14, "myType");
        LineHasDecoration(_thisModel, 1, 1, 14, "myType");
        LineHasNoDecorations(_thisModel, 2);
        LineHasNoDecorations(_thisModel, 3);
        LineHasNoDecorations(_thisModel, 4);
        LineHasNoDecorations(_thisModel, 5);
    }

    [Fact]
    public void Full_Line_Decoration()
    {
        AddDecoration(_thisModel, 1, 1, 2, 1, "myType");

        var line1Decorations = _thisModel.GetLineDecorations(1);
        line1Decorations.Count.ShouldBe(1);
        line1Decorations[0].Options.ClassName.ShouldBe("myType");

        // Upstream reads line 1 again here.
        var line2Decorations = _thisModel.GetLineDecorations(1);
        line2Decorations.Count.ShouldBe(1);
        line2Decorations[0].Options.ClassName.ShouldBe("myType");

        LineHasNoDecorations(_thisModel, 3);
        LineHasNoDecorations(_thisModel, 4);
        LineHasNoDecorations(_thisModel, 5);
    }

    [Fact]
    public void Multiple_Line_Decoration()
    {
        AddDecoration(_thisModel, 1, 2, 3, 2, "myType");

        var line1Decorations = _thisModel.GetLineDecorations(1);
        line1Decorations.Count.ShouldBe(1);
        line1Decorations[0].Options.ClassName.ShouldBe("myType");

        // Upstream reads line 1 again here and below.
        var line2Decorations = _thisModel.GetLineDecorations(1);
        line2Decorations.Count.ShouldBe(1);
        line2Decorations[0].Options.ClassName.ShouldBe("myType");

        var line3Decorations = _thisModel.GetLineDecorations(1);
        line3Decorations.Count.ShouldBe(1);
        line3Decorations[0].Options.ClassName.ShouldBe("myType");

        LineHasNoDecorations(_thisModel, 4);
        LineHasNoDecorations(_thisModel, 5);
    }

    // --------- removing, changing decorations

    [Fact]
    public void Decoration_Gets_Removed()
    {
        var decId = AddDecoration(_thisModel, 1, 2, 3, 2, "myType");
        ModelHasDecoration(_thisModel, 1, 2, 3, 2, "myType");
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.RemoveDecoration(decId);
            return 0;
        });
        ModelHasNoDecorations(_thisModel);
    }

    [Fact]
    public void Decorations_Get_Removed()
    {
        var decId1 = AddDecoration(_thisModel, 1, 2, 3, 2, "myType1");
        var decId2 = AddDecoration(_thisModel, 1, 2, 3, 1, "myType2");
        ModelHasDecorations(_thisModel,
        [
            (new TextRange(1, 2, 3, 1), "myType2"),
            (new TextRange(1, 2, 3, 2), "myType1"),
        ]);
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.RemoveDecoration(decId1);
            return 0;
        });
        ModelHasDecorations(_thisModel,
        [
            (new TextRange(1, 2, 3, 1), "myType2"),
        ]);
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.RemoveDecoration(decId2);
            return 0;
        });
        ModelHasNoDecorations(_thisModel);
    }

    [Fact]
    public void Decoration_Range_Can_Be_Changed()
    {
        var decId = AddDecoration(_thisModel, 1, 2, 3, 2, "myType");
        ModelHasDecoration(_thisModel, 1, 2, 3, 2, "myType");
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.ChangeDecoration(decId, new TextRange(1, 1, 1, 2));
            return 0;
        });
        ModelHasDecoration(_thisModel, 1, 1, 1, 2, "myType");
    }

    // --------- eventing

    [Fact]
    public void Decorations_Emit_Event_On_Add()
    {
        var listenerCalled = 0;
        Action<ModelDecorationsChangedEvent> listener = _ => listenerCalled++;
        _thisModel.DidChangeDecorations += listener;
        AddDecoration(_thisModel, 1, 2, 3, 2, "myType");
        listenerCalled.ShouldBe(1, "listener called");
        _thisModel.DidChangeDecorations -= listener;
    }

    [Fact]
    public void Decorations_Emit_Event_On_Change()
    {
        var listenerCalled = 0;
        var decId = AddDecoration(_thisModel, 1, 2, 3, 2, "myType");
        Action<ModelDecorationsChangedEvent> listener = _ => listenerCalled++;
        _thisModel.DidChangeDecorations += listener;
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.ChangeDecoration(decId, new TextRange(1, 1, 1, 2));
            return 0;
        });
        listenerCalled.ShouldBe(1, "listener called");
        _thisModel.DidChangeDecorations -= listener;
    }

    [Fact]
    public void Decorations_Emit_Event_On_Remove()
    {
        var listenerCalled = 0;
        var decId = AddDecoration(_thisModel, 1, 2, 3, 2, "myType");
        Action<ModelDecorationsChangedEvent> listener = _ => listenerCalled++;
        _thisModel.DidChangeDecorations += listener;
        _thisModel.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.RemoveDecoration(decId);
            return 0;
        });
        listenerCalled.ShouldBe(1, "listener called");
        _thisModel.DidChangeDecorations -= listener;
    }

    [Fact]
    public void Decorations_Emit_Event_When_Inserting_One_Line_Text_Before_It()
    {
        var listenerCalled = 0;
        AddDecoration(_thisModel, 1, 2, 3, 2, "myType");

        Action<ModelDecorationsChangedEvent> listener = _ => listenerCalled++;
        _thisModel.DidChangeDecorations += listener;

        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "Hallo ")]);
        listenerCalled.ShouldBe(1, "listener called");
        _thisModel.DidChangeDecorations -= listener;
    }

    [Fact]
    public void Decorations_Do_Not_Emit_Event_On_No_Op_DeltaDecorations()
    {
        var listenerCalled = 0;

        Action<ModelDecorationsChangedEvent> listener = _ => listenerCalled++;
        _thisModel.DidChangeDecorations += listener;

        _thisModel.DeltaDecorations([], []);
        _thisModel.ChangeDecorations(accessor => accessor.DeltaDecorations([], []));

        listenerCalled.ShouldBe(0, "listener not called");
        _thisModel.DidChangeDecorations -= listener;
    }

    // --------- editing text & effects on decorations

    private void RunEditingCase(
        (int StartLineNumber, int StartColumn, int EndLineNumber, int EndColumn) decoration,
        SingleEditOperation[] edits,
        (int StartLineNumber, int StartColumn, int EndLineNumber, int EndColumn) expected)
    {
        AddDecoration(_thisModel, decoration.StartLineNumber, decoration.StartColumn, decoration.EndLineNumber, decoration.EndColumn, "myType");
        ModelHasDecoration(_thisModel, decoration.StartLineNumber, decoration.StartColumn, decoration.EndLineNumber, decoration.EndColumn, "myType");
        _thisModel.ApplyEdits(edits);
        ModelHasDecoration(_thisModel, expected.StartLineNumber, expected.StartColumn, expected.EndLineNumber, expected.EndColumn, "myType");
    }

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_One_Line_Text_Before_It() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(1, 1), "Hallo ")], (1, 8, 3, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_One_Line_Text_Before_It_2() =>
        RunEditingCase((1, 1, 3, 2), [EditOperation.Replace(new TextRange(1, 1, 1, 1), "Hallo ")], (1, 1, 3, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_Multiple_Lines_Text_Before_It() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(1, 1), "Hallo\nI'm inserting multiple\nlines")], (3, 7, 5, 2));

    [Fact]
    public void Decorations_Change_When_Inserting_Text_After_Them() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(3, 2), "Hallo")], (1, 2, 3, 7));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_Text_Inside() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(1, 3), "Hallo ")], (1, 2, 3, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_Text_Inside_2() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(3, 1), "Hallo ")], (1, 2, 3, 8));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_Text_Inside_3() =>
        RunEditingCase((1, 1, 2, 16), [EditOperation.Insert(new TextPosition(2, 2), "\n")], (1, 1, 3, 15));

    [Fact]
    public void Decorations_Are_Updated_When_Inserting_Multiple_Lines_Text_Inside() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Insert(new TextPosition(1, 3), "Hallo\nI'm inserting multiple\nlines")], (1, 2, 5, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_One_Line_Text_Before_It() =>
        RunEditingCase((1, 2, 3, 2), [EditOperation.Delete(new TextRange(1, 1, 1, 2))], (1, 1, 3, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_Multiple_Lines_Text_Before_It() =>
        RunEditingCase((2, 2, 3, 2), [EditOperation.Delete(new TextRange(1, 1, 2, 1))], (1, 2, 2, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_Multiple_Lines_Text_Before_It_2() =>
        RunEditingCase((2, 3, 3, 2), [EditOperation.Delete(new TextRange(1, 1, 2, 2))], (1, 2, 2, 2));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_Text_Inside() =>
        RunEditingCase((1, 2, 4, 1), [EditOperation.Delete(new TextRange(1, 3, 2, 1))], (1, 2, 3, 1));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_Text_Inside_2() =>
        RunEditingCase(
            (1, 2, 4, 1),
            [EditOperation.Delete(new TextRange(1, 1, 1, 2)), EditOperation.Delete(new TextRange(4, 1, 4, 1))],
            (1, 1, 4, 1));

    [Fact]
    public void Decorations_Are_Updated_When_Deleting_Multiple_Lines_Text() =>
        RunEditingCase((1, 2, 4, 1), [EditOperation.Delete(new TextRange(1, 1, 3, 1))], (1, 1, 2, 1));

    [Fact]
    public void Decorations_Are_Updated_When_Changing_EOL()
    {
        AddDecoration(_thisModel, 1, 2, 4, 1, "myType1");
        AddDecoration(_thisModel, 1, 3, 4, 1, "myType2");
        AddDecoration(_thisModel, 1, 4, 4, 1, "myType3");
        AddDecoration(_thisModel, 1, 5, 4, 1, "myType4");
        AddDecoration(_thisModel, 1, 6, 4, 1, "myType5");
        AddDecoration(_thisModel, 1, 7, 4, 1, "myType6");
        AddDecoration(_thisModel, 1, 8, 4, 1, "myType7");
        AddDecoration(_thisModel, 1, 9, 4, 1, "myType8");
        AddDecoration(_thisModel, 1, 10, 4, 1, "myType9");
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "x")]);
        _thisModel.SetEOL(EndOfLineSequence.CRLF);
        _thisModel.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "x")]);
        ModelHasDecorations(_thisModel,
        [
            (new TextRange(1, 4, 4, 1), "myType1"),
            (new TextRange(1, 5, 4, 1), "myType2"),
            (new TextRange(1, 6, 4, 1), "myType3"),
            (new TextRange(1, 7, 4, 1), "myType4"),
            (new TextRange(1, 8, 4, 1), "myType5"),
            (new TextRange(1, 9, 4, 1), "myType6"),
            (new TextRange(1, 10, 4, 1), "myType7"),
            (new TextRange(1, 11, 4, 1), "myType8"),
            (new TextRange(1, 12, 4, 1), "myType9"),
        ]);
    }

    [Fact]
    public void An_Apparently_Simple_Edit()
    {
        AddDecoration(_thisModel, 1, 2, 4, 1, "myType1");
        _thisModel.ApplyEdits([EditOperation.Replace(new TextRange(1, 14, 2, 1), "x")]);
        ModelHasDecorations(_thisModel,
        [
            (new TextRange(1, 2, 3, 1), "myType1"),
        ]);
    }

    [Fact]
    public void RemoveAllDecorationsWithOwnerId_Can_Be_Called_After_Model_Dispose()
    {
        var model = TestTextModel.Create("asd");
        model.Dispose();
        model.RemoveAllDecorationsWithOwnerId(1);
    }

    [Fact]
    public void RemoveAllDecorationsWithOwnerId_Works()
    {
        _thisModel.DeltaDecorations(
            [],
            [new ModelDeltaDecoration(new TextRange(1, 2, 4, 1), new ModelDecorationOptions("test") { ClassName = "myType1" })],
            1);
        _thisModel.RemoveAllDecorationsWithOwnerId(1);
        ModelHasNoDecorations(_thisModel);
    }
}

public sealed class DeltaDecorationsTests
{
    private sealed record LightWeightDecoration(string Id, TextRange Range);

    private static LightWeightDecoration Decoration(string id, int startLineNumber, int startColumn, int endLineNumber, int endColum) =>
        new(id, new TextRange(startLineNumber, startColumn, endLineNumber, endColum));

    private static ModelDeltaDecoration ToModelDeltaDecoration(LightWeightDecoration dec) =>
        new(dec.Range, new ModelDecorationOptions("test") { ClassName = dec.Id });

    private static int Strcmp(string a, string b) => string.CompareOrdinal(a, b) switch
    {
        0 => 0,
        < 0 => -1,
        _ => 1,
    };

    private static List<LightWeightDecoration> ReadModelDecorations(TextModel model, string[] ids) =>
        ids.Select(id => new LightWeightDecoration(model.GetDecorationOptions(id)!.ClassName!, model.GetDecorationRange(id)!.Value)).ToList();

    private static void TestDeltaDecorations(string[] text, List<LightWeightDecoration> decorations, List<LightWeightDecoration> newDecorations)
    {
        using var model = TestTextModel.Create(string.Join('\n', text));

        // Add initial decorations & assert they are added
        var initialIds = model.DeltaDecorations([], decorations.Select(ToModelDeltaDecoration).ToList());
        var actualDecorations = ReadModelDecorations(model, initialIds);

        initialIds.Length.ShouldBe(decorations.Count, "returns expected cnt of ids");
        initialIds.Length.ShouldBe(model.GetAllDecorations().Count, "does not leak decorations");
        actualDecorations.Sort((a, b) => Strcmp(a.Id, b.Id));
        decorations.Sort((a, b) => Strcmp(a.Id, b.Id));
        actualDecorations.ShouldBe(decorations);

        var newIds = model.DeltaDecorations(initialIds, newDecorations.Select(ToModelDeltaDecoration).ToList());
        var actualNewDecorations = ReadModelDecorations(model, newIds);

        newIds.Length.ShouldBe(newDecorations.Count, "returns expected cnt of ids");
        newIds.Length.ShouldBe(model.GetAllDecorations().Count, "does not leak decorations");
        actualNewDecorations.Sort((a, b) => Strcmp(a.Id, b.Id));
        newDecorations.Sort((a, b) => Strcmp(a.Id, b.Id));
        // Upstream compares the initial decorations again here.
        actualDecorations.ShouldBe(decorations);
    }

    private static TextRange Range(int startLineNumber, int startColumn, int endLineNumber, int endColumn) =>
        new(startLineNumber, startColumn, endLineNumber, endColumn);

    [Fact]
    public void Result_Respects_Input()
    {
        using var model = TestTextModel.Create(string.Join('\n', "Hello world,", "How are you?"));

        var ids = model.DeltaDecorations([],
        [
            ToModelDeltaDecoration(Decoration("a", 1, 1, 1, 12)),
            ToModelDeltaDecoration(Decoration("b", 2, 1, 2, 13)),
        ]);

        model.GetDecorationRange(ids[0]).ShouldBe(Range(1, 1, 1, 12));
        model.GetDecorationRange(ids[1]).ShouldBe(Range(2, 1, 2, 13));
    }

    [Fact]
    public void DeltaDecorations_1()
    {
        TestDeltaDecorations(
            [
                "This is a text",
                "That has multiple lines",
                "And is very friendly",
                "Towards testing",
            ],
            [
                Decoration("a", 1, 1, 1, 2),
                Decoration("b", 1, 1, 1, 15),
                Decoration("c", 1, 1, 2, 1),
                Decoration("d", 1, 1, 2, 24),
                Decoration("e", 2, 1, 2, 24),
                Decoration("f", 2, 1, 4, 16),
            ],
            [
                Decoration("x", 1, 1, 1, 2),
                Decoration("b", 1, 1, 1, 15),
                Decoration("c", 1, 1, 2, 1),
                Decoration("d", 1, 1, 2, 24),
                Decoration("e", 2, 1, 2, 21),
                Decoration("f", 2, 17, 4, 16),
            ]);
    }

    [Fact]
    public void DeltaDecorations_2()
    {
        TestDeltaDecorations(
            [
                "This is a text",
                "That has multiple lines",
                "And is very friendly",
                "Towards testing",
            ],
            [
                Decoration("a", 1, 1, 1, 2),
                Decoration("b", 1, 2, 1, 3),
                Decoration("c", 1, 3, 1, 4),
                Decoration("d", 1, 4, 1, 5),
                Decoration("e", 1, 5, 1, 6),
            ],
            [
                Decoration("a", 1, 2, 1, 3),
                Decoration("b", 1, 3, 1, 4),
                Decoration("c", 1, 4, 1, 5),
                Decoration("d", 1, 5, 1, 6),
            ]);
    }

    [Fact]
    public void DeltaDecorations_3()
    {
        TestDeltaDecorations(
            [
                "This is a text",
                "That has multiple lines",
                "And is very friendly",
                "Towards testing",
            ],
            [
                Decoration("a", 1, 1, 1, 2),
                Decoration("b", 1, 2, 1, 3),
                Decoration("c", 1, 3, 1, 4),
                Decoration("d", 1, 4, 1, 5),
                Decoration("e", 1, 5, 1, 6),
            ],
            []);
    }

    [Fact]
    public void Issue_4317_Editor_SetDecorations_Doesnt_Update_The_Hover_Message()
    {
        using var model = TestTextModel.Create("Hello world!");

        var ids = model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(
                new TextRange(1, 1, 100, 1),
                new ModelDecorationOptions("test") { HoverMessage = [new MarkdownString("hello1")] }),
        ]);

        ids = model.DeltaDecorations(ids,
        [
            new ModelDeltaDecoration(
                new TextRange(1, 1, 100, 1),
                new ModelDecorationOptions("test") { HoverMessage = [new MarkdownString("hello2")] }),
        ]);

        var actualDecoration = model.GetDecorationOptions(ids[0]);

        actualDecoration!.HoverMessage.ShouldBe([new MarkdownString("hello2")]);
    }

    [Fact]
    public void Hover_Copy_Content_Survives_Decoration_Updates_And_Can_Be_Cleared()
    {
        using var model = TestTextModel.Create("Hello world!");
        var options = new ModelDecorationOptions("test") { HoverMessage = [new MarkdownString("Follow link")] };
        var id = model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(new TextRange(1, 1, 1, 5), options with { HoverMessageCopyContent = "https://example.com" }),
        ])[0];
        var copyContents = new List<string?> { model.GetDecorationOptions(id)?.HoverMessageCopyContent };

        model.ChangeDecorations(accessor =>
        {
            accessor.ChangeDecorationOptions(id, options with { HoverMessageCopyContent = "" });
            return 0;
        });
        copyContents.Add(model.GetDecorationOptions(id)?.HoverMessageCopyContent);

        model.ChangeDecorations(accessor =>
        {
            accessor.ChangeDecorationOptions(id, options);
            return 0;
        });
        copyContents.Add(model.GetDecorationOptions(id)?.HoverMessageCopyContent);

        copyContents.ShouldBe(["https://example.com", "", null]);
    }

    [Fact]
    public void Model_Doesnt_Get_Confused_With_Individual_Tracked_Ranges()
    {
        using var model = TestTextModel.Create(string.Join('\n', "Hello world,", "How are you?"));

        var trackedRangeId = model.ChangeDecorations(changeAcessor => changeAcessor.AddDecoration(
            new TextRange(1, 1, 1, 1),
            new ModelDecorationOptions("test") { Stickiness = TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges }));
        model.ChangeDecorations(changeAccessor =>
        {
            changeAccessor.RemoveDecoration(trackedRangeId!);
            return 0;
        });

        var ids = model.DeltaDecorations([],
        [
            ToModelDeltaDecoration(Decoration("a", 1, 1, 1, 12)),
            ToModelDeltaDecoration(Decoration("b", 2, 1, 2, 13)),
        ]);

        model.GetDecorationRange(ids[0]).ShouldBe(Range(1, 1, 1, 12));
        model.GetDecorationRange(ids[1]).ShouldBe(Range(2, 1, 2, 13));

        ids = model.DeltaDecorations(ids,
        [
            ToModelDeltaDecoration(Decoration("a", 1, 1, 1, 12)),
            ToModelDeltaDecoration(Decoration("b", 2, 1, 2, 13)),
        ]);

        model.GetDecorationRange(ids[0]).ShouldBe(Range(1, 1, 1, 12));
        model.GetDecorationRange(ids[1]).ShouldBe(Range(2, 1, 2, 13));
    }

    [Fact]
    public void Issue_16922_Clicking_On_Link_Doesnt_Seem_To_Do_Anything()
    {
        using var model = TestTextModel.Create(string.Join('\n', "Hello world,", "How are you?", "Fine.", "Good."));

        model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(new TextRange(1, 1, 1, 1), new ModelDecorationOptions("test") { ClassName = "1" }),
            new ModelDeltaDecoration(new TextRange(1, 13, 1, 13), new ModelDecorationOptions("test") { ClassName = "2" }),
            new ModelDeltaDecoration(new TextRange(2, 1, 2, 1), new ModelDecorationOptions("test") { ClassName = "3" }),
            new ModelDeltaDecoration(new TextRange(2, 1, 2, 4), new ModelDecorationOptions("test") { ClassName = "4" }),
            new ModelDeltaDecoration(new TextRange(2, 8, 2, 13), new ModelDecorationOptions("test") { ClassName = "5" }),
            new ModelDeltaDecoration(new TextRange(3, 1, 4, 6), new ModelDecorationOptions("test") { ClassName = "6" }),
            new ModelDeltaDecoration(new TextRange(1, 1, 3, 6), new ModelDecorationOptions("test") { ClassName = "x1" }),
            new ModelDeltaDecoration(new TextRange(2, 5, 2, 8), new ModelDecorationOptions("test") { ClassName = "x2" }),
            new ModelDeltaDecoration(new TextRange(1, 1, 2, 8), new ModelDecorationOptions("test") { ClassName = "x3" }),
            new ModelDeltaDecoration(new TextRange(2, 5, 3, 1), new ModelDecorationOptions("test") { ClassName = "x4" }),
        ]);

        var inRange = model.GetDecorationsInRange(new TextRange(2, 6, 2, 6));

        var inRangeClassNames = inRange.Select(d => d.Options.ClassName).ToList();
        inRangeClassNames.Sort(string.CompareOrdinal);
        inRangeClassNames.ShouldBe(["x1", "x2", "x3", "x4"]);
    }

    [Fact]
    public void Issue_41492_URL_Highlighting_Persists_After_Pasting_Over_Url()
    {
        using var model = TestTextModel.Create(string.Join('\n', "My First Line"));

        var id = model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(
                new TextRange(1, 2, 1, 14),
                new ModelDecorationOptions("test")
                {
                    Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
                    CollapseOnReplaceEdit = true,
                }),
        ])[0];
        model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 14), "Some new text that is longer than the previous one", false)]);
        var actual = model.GetDecorationRange(id);
        actual.ShouldBe(new TextRange(1, 1, 1, 1));
    }
}

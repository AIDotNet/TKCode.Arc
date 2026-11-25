// Ported from vscode (src/vs/editor/test/common/model/modelDecorations.test.ts, commit b57d5c9e), MIT license.
// This part: the suite 'Decorations and editing'. Its nested suites ('insert' > 'collapsed dec' > 'before') become
// one method each (Insert_Collapsed_Dec_Before); the comparison operators in the case names are spelled out
// (Lt, Le, Eq, Ge, Gt, And).

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class DecorationsAndEditingTests
{
    private static void RunSingleTest(
        TextRange decRange,
        TrackedRangeStickiness stickiness,
        TextRange editRange,
        string editText,
        bool editForceMoveMarkers,
        TextRange expectedDecRange,
        string msg)
    {
        using var model = TestTextModel.Create(string.Join('\n', "My First Line", "My Second Line", "Third Line"));

        var id = model.DeltaDecorations([], [new ModelDeltaDecoration(decRange, new ModelDecorationOptions("test") { Stickiness = stickiness })])[0];
        model.ApplyEdits([new SingleEditOperation(editRange, editText, editForceMoveMarkers)]);
        var actual = model.GetDecorationRange(id);
        actual.ShouldBe(expectedDecRange, msg);
    }

    private static void RunTest(TextRange decRange, TextRange editRange, string editText, TextRange[] expectedNoForce, TextRange[] expectedForce)
    {
        RunSingleTest(decRange, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, editRange, editText, false, expectedNoForce[0], "no-0-AlwaysGrowsWhenTypingAtEdges");
        RunSingleTest(decRange, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, editRange, editText, false, expectedNoForce[1], "no-1-NeverGrowsWhenTypingAtEdges");
        RunSingleTest(decRange, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, editRange, editText, false, expectedNoForce[2], "no-2-GrowsOnlyWhenTypingBefore");
        RunSingleTest(decRange, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, editRange, editText, false, expectedNoForce[3], "no-3-GrowsOnlyWhenTypingAfter");

        RunSingleTest(decRange, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, editRange, editText, true, expectedForce[0], "force-0-AlwaysGrowsWhenTypingAtEdges");
        RunSingleTest(decRange, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, editRange, editText, true, expectedForce[1], "force-1-NeverGrowsWhenTypingAtEdges");
        RunSingleTest(decRange, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, editRange, editText, true, expectedForce[2], "force-2-GrowsOnlyWhenTypingBefore");
        RunSingleTest(decRange, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, editRange, editText, true, expectedForce[3], "force-3-GrowsOnlyWhenTypingAfter");
    }

    // insert > collapsed dec > before
    [Fact]
    public void Insert_Collapsed_Dec_Before()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 3, 1, 3),
            "xx",
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)],
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)]);
    }

    // insert > collapsed dec > equal
    [Fact]
    public void Insert_Collapsed_Dec_Equal()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 4, 1, 4),
            "xx",
            [new TextRange(1, 4, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 4, 1, 4), new TextRange(1, 6, 1, 6)],
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)]);
    }

    // insert > collapsed dec > after
    [Fact]
    public void Insert_Collapsed_Dec_After()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 5, 1, 5),
            "xx",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // insert > non-collapsed dec > before
    [Fact]
    public void Insert_Non_Collapsed_Dec_Before()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 3),
            "xx",
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)],
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)]);
    }

    // insert > non-collapsed dec > start
    [Fact]
    public void Insert_Non_Collapsed_Dec_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 4),
            "xx",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 6, 1, 11)],
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)]);
    }

    // insert > non-collapsed dec > inside
    [Fact]
    public void Insert_Non_Collapsed_Dec_Inside()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 5),
            "xx",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)],
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)]);
    }

    // insert > non-collapsed dec > end
    [Fact]
    public void Insert_Non_Collapsed_Dec_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 9, 1, 9),
            "xx",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 11)],
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)]);
    }

    // insert > non-collapsed dec > after
    [Fact]
    public void Insert_Non_Collapsed_Dec_After()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 10, 1, 10),
            "xx",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // delete > collapsed dec > edit.end < range.start
    [Fact]
    public void Delete_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 1, 1, 3),
            "",
            [new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2)],
            [new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2)]);
    }

    // delete > collapsed dec > edit.end <= range.start
    [Fact]
    public void Delete_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 2, 1, 4),
            "",
            [new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2)],
            [new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2), new TextRange(1, 2, 1, 2)]);
    }

    // delete > collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Delete_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 3, 1, 5),
            "",
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)],
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)]);
    }

    // delete > collapsed dec > edit.start >= range.end
    [Fact]
    public void Delete_Collapsed_Dec_Edit_Start_Ge_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 4, 1, 6),
            "",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // delete > collapsed dec > edit.start > range.end
    [Fact]
    public void Delete_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 5, 1, 7),
            "",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // delete > non-collapsed dec > edit.end < range.start
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 1, 1, 3),
            "",
            [new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7)],
            [new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7)]);
    }

    // delete > non-collapsed dec > edit.end <= range.start
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 2, 1, 4),
            "",
            [new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7)],
            [new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7), new TextRange(1, 2, 1, 7)]);
    }

    // delete > non-collapsed dec > edit.start < range.start && edit.end < range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 5),
            "",
            [new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7)],
            [new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7), new TextRange(1, 3, 1, 7)]);
    }

    // delete > non-collapsed dec > edit.start < range.start && edit.end == range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 9),
            "",
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)],
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)]);
    }

    // delete > non-collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 10),
            "",
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)],
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)]);
    }

    // delete > non-collapsed dec > edit.start == range.start && edit.end < range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 6),
            "",
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)],
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)]);
    }

    // delete > non-collapsed dec > edit.start == range.start && edit.end == range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 9),
            "",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // delete > non-collapsed dec > edit.start == range.start && edit.end > range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 10),
            "",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // delete > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end < range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 7),
            "",
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)],
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)]);
    }

    // delete > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end == range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 9),
            "",
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)],
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)]);
    }

    // delete > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end > range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 10),
            "",
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)],
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)]);
    }

    // delete > non-collapsed dec > edit.start == range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 9, 1, 11),
            "",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // delete > non-collapsed dec > edit.start > range.end
    [Fact]
    public void Delete_Non_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 10, 1, 11),
            "",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // replace short > collapsed dec > edit.end < range.start
    [Fact]
    public void Replace_Short_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 1, 1, 3),
            "c",
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)],
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)]);
    }

    // replace short > collapsed dec > edit.end <= range.start
    [Fact]
    public void Replace_Short_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 2, 1, 4),
            "c",
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)],
            [new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3), new TextRange(1, 3, 1, 3)]);
    }

    // replace short > collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Replace_Short_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 3, 1, 5),
            "c",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // replace short > collapsed dec > edit.start >= range.end
    [Fact]
    public void Replace_Short_Collapsed_Dec_Edit_Start_Ge_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 4, 1, 6),
            "c",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5)]);
    }

    // replace short > collapsed dec > edit.start > range.end
    [Fact]
    public void Replace_Short_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 5, 1, 7),
            "c",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // replace short > non-collapsed dec > edit.end < range.start
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 1, 1, 3),
            "c",
            [new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8)],
            [new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8)]);
    }

    // replace short > non-collapsed dec > edit.end <= range.start
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 2, 1, 4),
            "c",
            [new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8)],
            [new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8), new TextRange(1, 3, 1, 8)]);
    }

    // replace short > non-collapsed dec > edit.start < range.start && edit.end < range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 5),
            "c",
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)],
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)]);
    }

    // replace short > non-collapsed dec > edit.start < range.start && edit.end == range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 9),
            "c",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // replace short > non-collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 10),
            "c",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // replace short > non-collapsed dec > edit.start == range.start && edit.end < range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 6),
            "c",
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)],
            [new TextRange(1, 5, 1, 8), new TextRange(1, 5, 1, 8), new TextRange(1, 5, 1, 8), new TextRange(1, 5, 1, 8)]);
    }

    // replace short > non-collapsed dec > edit.start == range.start && edit.end == range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 9),
            "c",
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)],
            [new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5)]);
    }

    // replace short > non-collapsed dec > edit.start == range.start && edit.end > range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 10),
            "c",
            [new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5), new TextRange(1, 4, 1, 5)],
            [new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5), new TextRange(1, 5, 1, 5)]);
    }

    // replace short > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end < range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 7),
            "c",
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)],
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)]);
    }

    // replace short > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end == range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 9),
            "c",
            [new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6)],
            [new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6)]);
    }

    // replace short > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end > range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 10),
            "c",
            [new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6)],
            [new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6), new TextRange(1, 4, 1, 6)]);
    }

    // replace short > non-collapsed dec > edit.start == range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 9, 1, 11),
            "c",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 10), new TextRange(1, 4, 1, 10), new TextRange(1, 4, 1, 10), new TextRange(1, 4, 1, 10)]);
    }

    // replace short > non-collapsed dec > edit.start > range.end
    [Fact]
    public void Replace_Short_Non_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 10, 1, 11),
            "c",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // replace long > collapsed dec > edit.end < range.start
    [Fact]
    public void Replace_Long_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 1, 1, 3),
            "cccc",
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)],
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)]);
    }

    // replace long > collapsed dec > edit.end <= range.start
    [Fact]
    public void Replace_Long_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 2, 1, 4),
            "cccc",
            [new TextRange(1, 4, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 4, 1, 4), new TextRange(1, 6, 1, 6)],
            [new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6), new TextRange(1, 6, 1, 6)]);
    }

    // replace long > collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Replace_Long_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 3, 1, 5),
            "cccc",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7)]);
    }

    // replace long > collapsed dec > edit.start >= range.end
    [Fact]
    public void Replace_Long_Collapsed_Dec_Edit_Start_Ge_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 4, 1, 6),
            "cccc",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8)]);
    }

    // replace long > collapsed dec > edit.start > range.end
    [Fact]
    public void Replace_Long_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 4),
            new TextRange(1, 5, 1, 7),
            "cccc",
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)],
            [new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4), new TextRange(1, 4, 1, 4)]);
    }

    // replace long > non-collapsed dec > edit.end < range.start
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_End_Lt_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 1, 1, 3),
            "cccc",
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)],
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)]);
    }

    // replace long > non-collapsed dec > edit.end <= range.start
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_End_Le_Range_Start()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 2, 1, 4),
            "cccc",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 6, 1, 11)],
            [new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11), new TextRange(1, 6, 1, 11)]);
    }

    // replace long > non-collapsed dec > edit.start < range.start && edit.end < range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 5),
            "cccc",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)],
            [new TextRange(1, 7, 1, 11), new TextRange(1, 7, 1, 11), new TextRange(1, 7, 1, 11), new TextRange(1, 7, 1, 11)]);
    }

    // replace long > non-collapsed dec > edit.start < range.start && edit.end == range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 9),
            "cccc",
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)],
            [new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7)]);
    }

    // replace long > non-collapsed dec > edit.start < range.start && edit.end > range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Lt_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 3, 1, 10),
            "cccc",
            [new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7), new TextRange(1, 4, 1, 7)],
            [new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7), new TextRange(1, 7, 1, 7)]);
    }

    // replace long > non-collapsed dec > edit.start == range.start && edit.end < range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 6),
            "cccc",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)],
            [new TextRange(1, 8, 1, 11), new TextRange(1, 8, 1, 11), new TextRange(1, 8, 1, 11), new TextRange(1, 8, 1, 11)]);
    }

    // replace long > non-collapsed dec > edit.start == range.start && edit.end == range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 9),
            "cccc",
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)],
            [new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8)]);
    }

    // replace long > non-collapsed dec > edit.start == range.start && edit.end > range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Eq_Range_Start_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 4, 1, 10),
            "cccc",
            [new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8), new TextRange(1, 4, 1, 8)],
            [new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8), new TextRange(1, 8, 1, 8)]);
    }

    // replace long > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end < range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Lt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 7),
            "cccc",
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)],
            [new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11), new TextRange(1, 4, 1, 11)]);
    }

    // replace long > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end == range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 9),
            "cccc",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // replace long > non-collapsed dec > edit.start > range.start && edit.start < range.end && edit.end > range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Gt_Range_Start_And_Edit_Start_Lt_Range_End_And_Edit_End_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 5, 1, 10),
            "cccc",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }

    // replace long > non-collapsed dec > edit.start == range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Eq_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 9, 1, 11),
            "cccc",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 13), new TextRange(1, 4, 1, 13), new TextRange(1, 4, 1, 13), new TextRange(1, 4, 1, 13)]);
    }

    // replace long > non-collapsed dec > edit.start > range.end
    [Fact]
    public void Replace_Long_Non_Collapsed_Dec_Edit_Start_Gt_Range_End()
    {
        RunTest(
            new TextRange(1, 4, 1, 9),
            new TextRange(1, 10, 1, 11),
            "cccc",
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)],
            [new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9), new TextRange(1, 4, 1, 9)]);
    }
}

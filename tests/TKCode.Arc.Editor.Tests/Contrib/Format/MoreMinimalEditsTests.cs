// Tests of EditorWorker.ComputeMoreMinimalEdits, ported from vscode (src/vs/editor/test/common/services/
// editorWebWorker.test.ts: the 'MoreMinimal' cases, commit b57d5c9e), MIT license: all nine. The worker's mirror model is
// a text model here. Own tests of StringDiff (upstream's stringDiff of base/common/diff/diff.ts) follow.

using Shouldly;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Format;

public sealed class MoreMinimalEditsTests
{
    private static TextModel Model(params string[] lines) => TestTextModel.Create(string.Join('\n', lines));

    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    private static void Apply(TextModel model, IReadOnlyList<TextEdit> edits) =>
        model.ApplyEdits([.. edits.Where(EditorWorker.HasRange).Select(TextEdit.AsEditOperation)]);

    [Fact]
    public void MoreMinimal()
    {
        using var model = Model("This is line one", "and this is line number two", "it is followed by #3", "and finished with the fourth.");
        var edits = EditorWorker.ComputeMoreMinimalEdits(model, [new TextEdit(R(1, 1, 1, 17), "This is line One")])!;
        edits.Count.ShouldBe(1);
        edits[0].Text.ShouldBe("O");
        edits[0].Range.ShouldBe(R(1, 14, 1, 15));
    }

    [Fact]
    public void MoreMinimal_merge_adjacent_edits()
    {
        using var model = Model("one", "two", "three", "four", "five");
        var newEdits = EditorWorker.ComputeMoreMinimalEdits(model,
        [
            new TextEdit(R(1, 1, 2, 1), "one\ntwo\nthree\n"),
            new TextEdit(R(2, 1, 3, 1), string.Empty),
            new TextEdit(R(3, 1, 4, 1), string.Empty),
            new TextEdit(R(4, 2, 4, 3), "4"),
            new TextEdit(R(5, 3, 5, 5), "5"),
        ])!;

        newEdits.Count.ShouldBe(2);
        newEdits[0].Text.ShouldBe("4");
        newEdits[1].Text.ShouldBe("5");
    }

    [Fact]
    public void MoreMinimal_issue_15385_newline_changes_only()
    {
        using var model = Model("{", "\t\"a\":1", "}");
        EditorWorker.ComputeMoreMinimalEdits(model, [new TextEdit(R(1, 1, 3, 2), "{\r\n\t\"a\":1\r\n}")])!.Count.ShouldBe(0);
    }

    [Fact]
    public void MoreMinimal_issue_15385_newline_changes_and_other()
    {
        using var model = Model("{", "\t\"a\":1", "}");
        var edits = EditorWorker.ComputeMoreMinimalEdits(model, [new TextEdit(R(1, 1, 3, 2), "{\r\n\t\"b\":1\r\n}")])!;
        edits.Count.ShouldBe(1);
        edits[0].Text.ShouldBe("b");
        edits[0].Range.ShouldBe(R(2, 3, 2, 4));
    }

    [Fact]
    public void MoreMinimal_issue_15385_newline_changes_and_other_2_of_2()
    {
        using var model = Model("package main", "func foo() {", "}");
        var edits = EditorWorker.ComputeMoreMinimalEdits(model, [new TextEdit(R(3, 2, 4, 1000), "\n")])!;
        edits.Count.ShouldBe(1);
        edits[0].Text.ShouldBe("\n");
        edits[0].Range.ShouldBe(R(3, 2, 3, 2));
    }

    [Fact]
    public void MoreMinimal_preserves_surrogate_pairs()
    {
        const string original = "𫚭齅5龯蝌䶱aＺr灋㊣珸琱璳AbD龪龫藏㒱㓥㓹3UxヅЁЖa偓兘㎜𫟹";
        const string modified = "㈢鿃2磷𬸚﹢㘥怐ND䶮ёㄆホ鿃龬鿍a6纷冬督㊣㈱㔾㖏㕽內冚7畫皗Э𫔍";
        using var workerModel = Model(original);
        var edits = EditorWorker.ComputeMoreMinimalEdits(workerModel, [new TextEdit(R(1, 1, 1, original.Length + 1), modified)])!;
        using var textModel = Model(original);
        Apply(textModel, edits);
        textModel.GetValue().ShouldBe(modified);
    }

    [Fact]
    public void MoreMinimal_falls_back_when_a_range_follows_an_unpaired_high_surrogate()
    {
        const string original = "aX\uD83Dy";
        const string modified = "bX😀y";
        using var workerModel = Model(original);
        var edits = EditorWorker.ComputeMoreMinimalEdits(workerModel, [new TextEdit(R(1, 1, 1, original.Length + 1), modified)])!;
        using var textModel = Model(original);
        Apply(textModel, edits);

        edits.ShouldBe([new TextEdit(R(1, 1, 1, original.Length + 1), modified)]);
        textModel.GetValue().ShouldBe(modified);
    }

    [Fact]
    public void MoreMinimal_can_delete_an_unpaired_high_surrogate_at_the_end_of_a_line()
    {
        const string original = "AB\uD83D";
        const string modified = "AB";
        using var workerModel = Model(original);
        var edits = EditorWorker.ComputeMoreMinimalEdits(workerModel, [new TextEdit(R(1, 1, 1, original.Length + 1), modified)])!;
        using var textModel = Model(original);
        Apply(textModel, edits);

        edits.ShouldBe([new TextEdit(R(1, 3, 1, 4), string.Empty)]);
        textModel.GetValue().ShouldBe(modified);
    }

    [Fact]
    public void MoreMinimal_falls_back_when_inserting_after_an_unpaired_high_surrogate_at_the_end_of_a_line()
    {
        const string original = "AB\uD83D";
        const string modified = "AB\uD83DX";
        using var workerModel = Model(original);
        var edits = EditorWorker.ComputeMoreMinimalEdits(workerModel, [new TextEdit(R(1, 1, 1, original.Length + 1), modified)])!;
        using var textModel = Model(original);
        Apply(textModel, edits);

        edits.ShouldBe([new TextEdit(R(1, 1, 1, original.Length + 1), modified)]);
        textModel.GetValue().ShouldBe(modified);
    }

    // ---- own tests

    [Fact]
    public void Null_and_empty_edits_come_back_as_they_were()
    {
        using var model = Model("a");
        EditorWorker.ComputeMoreMinimalEdits(model, null).ShouldBeNull();
        EditorWorker.ComputeMoreMinimalEdits(model, []).ShouldBeEmpty();
    }

    [Fact]
    public void An_eol_edit_goes_last_with_an_empty_range()
    {
        using var model = Model("a", "b");
        var edits = EditorWorker.ComputeMoreMinimalEdits(model,
        [
            new TextEdit(default, string.Empty) { Eol = EndOfLineSequence.CRLF },
            new TextEdit(R(1, 1, 1, 2), "x"),
        ])!;

        edits.Count.ShouldBe(2);
        edits[0].ShouldBe(new TextEdit(R(1, 1, 1, 2), "x"));
        edits[1].Eol.ShouldBe(EndOfLineSequence.CRLF);
        EditorWorker.HasRange(edits[1]).ShouldBeFalse();
    }

    [Fact]
    public void A_reindent_of_a_block_becomes_whitespace_edits_only()
    {
        using var model = Model("function f() {", "return 1;", "}");
        var edits = EditorWorker.ComputeMoreMinimalEdits(model, [new TextEdit(model.GetFullModelRange(), "function f() {\n    return 1;\n}")])!;
        edits.ShouldBe([new TextEdit(R(2, 1, 2, 1), "    ")]);
    }

    [Fact]
    public void String_diff_finds_the_changed_characters()
    {
        var changes = StringDiff.Compute("abcdef", "abXdeYf", pretty: false);
        changes.Select(c => (c.OriginalStart, c.OriginalLength, c.ModifiedStart, c.ModifiedLength))
            .ShouldBe([(2, 1, 2, 1), (5, 0, 5, 1)]);
        StringDiff.Compute("same", "same", pretty: true).ShouldBeEmpty();
        StringDiff.Compute(string.Empty, "new", pretty: false).Single().ModifiedLength.ShouldBe(3);
    }

    [Fact]
    public void String_diff_of_lines_prefers_whitespace_boundaries_when_pretty()
    {
        string[] original = ["a", "", "b", "c", ""];
        string[] modified = ["a", "", "b", "c", "", "b", "c", ""];
        var changes = new LcsDiff(new StringArrayDiffSequence(original), new StringArrayDiffSequence(modified)).ComputeDiff(pretty: true).Changes;
        changes.Count.ShouldBe(1);
        // the inserted block starts after an empty line rather than in the middle of one
        changes[0].ModifiedLength.ShouldBe(3);
        string.IsNullOrWhiteSpace(modified[changes[0].ModifiedStart - 1]).ShouldBeTrue();
    }
}

// Ported from vscode (src/vs/editor/test/common/diff/diffComputer.test.ts, commit b57d5c9e), MIT license.
// All 49 upstream tests of the legacy DiffComputer, converted one to one. Like upstream, each test also applies the
// line changes and the character changes to a text model of the original text and checks that it becomes the
// modified text. Line and character changes are compared as records instead of with deepStrictEqual.

using Shouldly;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Diff;

public sealed class DiffComputerTests
{
    private const int MaxSafeSmallInteger = (1 << 30) - 1;

    private sealed record ExpectedLineChange(
        int OriginalStartLineNumber,
        int OriginalEndLineNumber,
        int ModifiedStartLineNumber,
        int ModifiedEndLineNumber,
        CharChange[]? CharChanges)
    {
        public bool Equals(ExpectedLineChange? other) =>
            other is not null
            && OriginalStartLineNumber == other.OriginalStartLineNumber
            && OriginalEndLineNumber == other.OriginalEndLineNumber
            && ModifiedStartLineNumber == other.ModifiedStartLineNumber
            && ModifiedEndLineNumber == other.ModifiedEndLineNumber
            && (CharChanges is null ? other.CharChanges is null : other.CharChanges is not null && CharChanges.SequenceEqual(other.CharChanges));

        public override int GetHashCode() => HashCode.Combine(OriginalStartLineNumber, OriginalEndLineNumber, ModifiedStartLineNumber, ModifiedEndLineNumber);

        public override string ToString() =>
            $"({OriginalStartLineNumber},{OriginalEndLineNumber} -> {ModifiedStartLineNumber},{ModifiedEndLineNumber}"
            + (CharChanges is null ? ")" : ": " + string.Join(", ", CharChanges.Select(c => c.ToString())) + ")");
    }

    private static void AssertDiff(
        string[] originalLines,
        string[] modifiedLines,
        LineChange[] expectedChanges,
        bool shouldComputeCharChanges = true,
        bool shouldPostProcessCharChanges = false,
        bool shouldIgnoreTrimWhitespace = false)
    {
        var diffComputer = new DiffComputer(originalLines, modifiedLines, new DiffComputerOpts
        {
            ShouldComputeCharChanges = shouldComputeCharChanges,
            ShouldPostProcessCharChanges = shouldPostProcessCharChanges,
            ShouldIgnoreTrimWhitespace = shouldIgnoreTrimWhitespace,
            ShouldMakePrettyDiff = true,
            MaxComputationTime = 0,
        });
        var changes = diffComputer.ComputeDiff().Changes;

        static ExpectedLineChange Map(LineChange c) =>
            new(c.OriginalStartLineNumber, c.OriginalEndLineNumber, c.ModifiedStartLineNumber, c.ModifiedEndLineNumber, c.CharChanges?.ToArray());

        changes.Select(Map).ToArray().ShouldBe(expectedChanges.Select(Map).ToArray());

        if (!shouldIgnoreTrimWhitespace)
        {
            // The diffs should describe how to apply edits to the original text model to get to the modified text model.
            using var modifiedTextModel = TestTextModel.Create(string.Join('\n', modifiedLines));
            var expectedValue = modifiedTextModel.GetValue();

            {
                // Line changes:
                using var originalTextModel = TestTextModel.Create(string.Join('\n', originalLines));
                originalTextModel.ApplyEdits(changes.Select(c => GetLineEdit(c, modifiedTextModel)).ToList());
                originalTextModel.GetValue().ShouldBe(expectedValue);
            }

            if (shouldComputeCharChanges)
            {
                // Char changes:
                using var originalTextModel = TestTextModel.Create(string.Join('\n', originalLines));
                originalTextModel.ApplyEdits(changes.SelectMany(c => GetCharEdits(c, modifiedTextModel)).ToList());
                originalTextModel.GetValue().ShouldBe(expectedValue);
            }
        }
    }

    private static IEnumerable<SingleEditOperation> GetCharEdits(LineChange lineChange, TextModel modifiedTextModel)
    {
        if (lineChange.CharChanges is null)
        {
            return [GetLineEdit(lineChange, modifiedTextModel)];
        }

        return lineChange.CharChanges.Select(c =>
        {
            var originalRange = new TextRange(c.OriginalStartLineNumber, c.OriginalStartColumn, c.OriginalEndLineNumber, c.OriginalEndColumn);
            var modifiedRange = new TextRange(c.ModifiedStartLineNumber, c.ModifiedStartColumn, c.ModifiedEndLineNumber, c.ModifiedEndColumn);
            return new SingleEditOperation(originalRange, modifiedTextModel.GetValueInRange(modifiedRange));
        });
    }

    private static SingleEditOperation GetLineEdit(LineChange lineChange, TextModel modifiedTextModel)
    {
        var originalRange = lineChange.OriginalEndLineNumber == 0
            // Insertion
            ? new TestLineRange(lineChange.OriginalStartLineNumber + 1, 0)
            : new TestLineRange(lineChange.OriginalStartLineNumber, lineChange.OriginalEndLineNumber - lineChange.OriginalStartLineNumber + 1);

        var modifiedRange = lineChange.ModifiedEndLineNumber == 0
            // Deletion
            ? new TestLineRange(lineChange.ModifiedStartLineNumber + 1, 0)
            : new TestLineRange(lineChange.ModifiedStartLineNumber, lineChange.ModifiedEndLineNumber - lineChange.ModifiedStartLineNumber + 1);

        var (r1, r2) = DiffFromLineRanges(originalRange, modifiedRange);
        return new SingleEditOperation(r1, modifiedTextModel.GetValueInRange(r2));
    }

    private static (TextRange, TextRange) DiffFromLineRanges(TestLineRange originalRange, TestLineRange modifiedRange)
    {
        if (originalRange.StartLineNumber == 1 || modifiedRange.StartLineNumber == 1)
        {
            if (!originalRange.IsEmpty && !modifiedRange.IsEmpty)
            {
                return (
                    new TextRange(originalRange.StartLineNumber, 1, originalRange.EndLineNumberExclusive - 1, MaxSafeSmallInteger),
                    new TextRange(modifiedRange.StartLineNumber, 1, modifiedRange.EndLineNumberExclusive - 1, MaxSafeSmallInteger));
            }

            // When one of them is one and one of them is empty, the other cannot be the last line of the document
            return (
                new TextRange(originalRange.StartLineNumber, 1, originalRange.EndLineNumberExclusive, 1),
                new TextRange(modifiedRange.StartLineNumber, 1, modifiedRange.EndLineNumberExclusive, 1));
        }

        return (
            new TextRange(originalRange.StartLineNumber - 1, MaxSafeSmallInteger, originalRange.EndLineNumberExclusive - 1, MaxSafeSmallInteger),
            new TextRange(modifiedRange.StartLineNumber - 1, MaxSafeSmallInteger, modifiedRange.EndLineNumberExclusive - 1, MaxSafeSmallInteger));
    }

    private readonly record struct TestLineRange(int StartLineNumber, int LineCount)
    {
        public bool IsEmpty => LineCount == 0;

        public int EndLineNumberExclusive => StartLineNumber + LineCount;
    }

    private static LineChange CreateLineDeletion(int startLineNumber, int endLineNumber, int modifiedLineNumber) =>
        new(startLineNumber, endLineNumber, modifiedLineNumber, 0, null);

    private static LineChange CreateLineInsertion(int startLineNumber, int endLineNumber, int originalLineNumber) =>
        new(originalLineNumber, 0, startLineNumber, endLineNumber, null);

    private static LineChange CreateLineChange(int originalStartLineNumber, int originalEndLineNumber, int modifiedStartLineNumber, int modifiedEndLineNumber, List<CharChange>? charChanges = null) =>
        new(originalStartLineNumber, originalEndLineNumber, modifiedStartLineNumber, modifiedEndLineNumber, charChanges);

    private static CharChange CreateCharChange(
        int originalStartLineNumber, int originalStartColumn, int originalEndLineNumber, int originalEndColumn,
        int modifiedStartLineNumber, int modifiedStartColumn, int modifiedEndLineNumber, int modifiedEndColumn) =>
        new(originalStartLineNumber, originalStartColumn, originalEndLineNumber, originalEndColumn,
            modifiedStartLineNumber, modifiedStartColumn, modifiedEndLineNumber, modifiedEndColumn);

    [Fact(DisplayName = "one inserted line below")]
    public void One_Inserted_Line_Below()
    {
        string[] original = ["line"];
        string[] modified = ["line", "new line"];
        LineChange[] expected = [CreateLineInsertion(2, 2, 1)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two inserted lines below")]
    public void Two_Inserted_Lines_Below()
    {
        string[] original = ["line"];
        string[] modified = ["line", "new line", "another new line"];
        LineChange[] expected = [CreateLineInsertion(2, 3, 1)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one inserted line above")]
    public void One_Inserted_Line_Above()
    {
        string[] original = ["line"];
        string[] modified = ["new line", "line"];
        LineChange[] expected = [CreateLineInsertion(1, 1, 0)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two inserted lines above")]
    public void Two_Inserted_Lines_Above()
    {
        string[] original = ["line"];
        string[] modified = ["new line", "another new line", "line"];
        LineChange[] expected = [CreateLineInsertion(1, 2, 0)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one inserted line in middle")]
    public void One_Inserted_Line_In_Middle()
    {
        string[] original = ["line1", "line2", "line3", "line4"];
        string[] modified = ["line1", "line2", "new line", "line3", "line4"];
        LineChange[] expected = [CreateLineInsertion(3, 3, 2)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two inserted lines in middle")]
    public void Two_Inserted_Lines_In_Middle()
    {
        string[] original = ["line1", "line2", "line3", "line4"];
        string[] modified = ["line1", "line2", "new line", "another new line", "line3", "line4"];
        LineChange[] expected = [CreateLineInsertion(3, 4, 2)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two inserted lines in middle interrupted")]
    public void Two_Inserted_Lines_In_Middle_Interrupted()
    {
        string[] original = ["line1", "line2", "line3", "line4"];
        string[] modified = ["line1", "line2", "new line", "line3", "another new line", "line4"];
        LineChange[] expected = [CreateLineInsertion(3, 3, 2), CreateLineInsertion(5, 5, 3)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one deleted line below")]
    public void One_Deleted_Line_Below()
    {
        string[] original = ["line", "new line"];
        string[] modified = ["line"];
        LineChange[] expected = [CreateLineDeletion(2, 2, 1)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two deleted lines below")]
    public void Two_Deleted_Lines_Below()
    {
        string[] original = ["line", "new line", "another new line"];
        string[] modified = ["line"];
        LineChange[] expected = [CreateLineDeletion(2, 3, 1)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one deleted lines above")]
    public void One_Deleted_Lines_Above()
    {
        string[] original = ["new line", "line"];
        string[] modified = ["line"];
        LineChange[] expected = [CreateLineDeletion(1, 1, 0)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two deleted lines above")]
    public void Two_Deleted_Lines_Above()
    {
        string[] original = ["new line", "another new line", "line"];
        string[] modified = ["line"];
        LineChange[] expected = [CreateLineDeletion(1, 2, 0)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one deleted line in middle")]
    public void One_Deleted_Line_In_Middle()
    {
        string[] original = ["line1", "line2", "new line", "line3", "line4"];
        string[] modified = ["line1", "line2", "line3", "line4"];
        LineChange[] expected = [CreateLineDeletion(3, 3, 2)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two deleted lines in middle")]
    public void Two_Deleted_Lines_In_Middle()
    {
        string[] original = ["line1", "line2", "new line", "another new line", "line3", "line4"];
        string[] modified = ["line1", "line2", "line3", "line4"];
        LineChange[] expected = [CreateLineDeletion(3, 4, 2)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two deleted lines in middle interrupted")]
    public void Two_Deleted_Lines_In_Middle_Interrupted()
    {
        string[] original = ["line1", "line2", "new line", "line3", "another new line", "line4"];
        string[] modified = ["line1", "line2", "line3", "line4"];
        LineChange[] expected = [CreateLineDeletion(3, 3, 2), CreateLineDeletion(5, 5, 3)];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars inserted at the end")]
    public void One_Line_Changed_Chars_Inserted_At_The_End()
    {
        string[] original = ["line"];
        string[] modified = ["line changed"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 5, 1, 5, 1, 5, 1, 13)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars inserted at the beginning")]
    public void One_Line_Changed_Chars_Inserted_At_The_Beginning()
    {
        string[] original = ["line"];
        string[] modified = ["my line"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 1, 1, 1, 1, 1, 1, 4)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars inserted in the middle")]
    public void One_Line_Changed_Chars_Inserted_In_The_Middle()
    {
        string[] original = ["abba"];
        string[] modified = ["abzzba"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 3, 1, 3, 1, 3, 1, 5)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars inserted in the middle (two spots)")]
    public void One_Line_Changed_Chars_Inserted_In_The_Middle_Two_Spots()
    {
        string[] original = ["abba"];
        string[] modified = ["abzzbzza"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 3, 1, 3, 1, 3, 1, 5),
                CreateCharChange(1, 4, 1, 4, 1, 6, 1, 8)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars deleted 1")]
    public void One_Line_Changed_Chars_Deleted_1()
    {
        string[] original = ["abcdefg"];
        string[] modified = ["abcfg"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 4, 1, 6, 1, 4, 1, 4)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "one line changed: chars deleted 2")]
    public void One_Line_Changed_Chars_Deleted_2()
    {
        string[] original = ["abcdefg"];
        string[] modified = ["acfg"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 2, 1, 3, 1, 2, 1, 2),
                CreateCharChange(1, 4, 1, 6, 1, 3, 1, 3)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two lines changed 1")]
    public void Two_Lines_Changed_1()
    {
        string[] original = ["abcd", "efgh"];
        string[] modified = ["abcz"];
        LineChange[] expected = [
            CreateLineChange(1, 2, 1, 1, [
                CreateCharChange(1, 4, 2, 5, 1, 4, 1, 5)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two lines changed 2")]
    public void Two_Lines_Changed_2()
    {
        string[] original = ["foo", "abcd", "efgh", "BAR"];
        string[] modified = ["foo", "abcz", "BAR"];
        LineChange[] expected = [
            CreateLineChange(2, 3, 2, 2, [
                CreateCharChange(2, 4, 3, 5, 2, 4, 2, 5)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two lines changed 3")]
    public void Two_Lines_Changed_3()
    {
        string[] original = ["foo", "abcd", "efgh", "BAR"];
        string[] modified = ["foo", "abcz", "zzzzefgh", "BAR"];
        LineChange[] expected = [
            CreateLineChange(2, 3, 2, 3, [
                CreateCharChange(2, 4, 2, 5, 2, 4, 2, 5),
                CreateCharChange(3, 1, 3, 1, 3, 1, 3, 5)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "two lines changed 4")]
    public void Two_Lines_Changed_4()
    {
        string[] original = ["abc"];
        string[] modified = ["", "", "axc", ""];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 4, [
                CreateCharChange(1, 1, 1, 1, 1, 1, 3, 1),
                CreateCharChange(1, 2, 1, 3, 3, 2, 3, 3),
                CreateCharChange(1, 4, 1, 4, 3, 4, 4, 1)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "empty original sequence in char diff")]
    public void Empty_Original_Sequence_In_Char_Diff()
    {
        string[] original = ["abc", "", "xyz"];
        string[] modified = ["abc", "qwe", "rty", "xyz"];
        LineChange[] expected = [
            CreateLineChange(2, 2, 2, 3)
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "three lines changed")]
    public void Three_Lines_Changed()
    {
        string[] original = ["foo", "abcd", "efgh", "BAR"];
        string[] modified = ["foo", "zzzefgh", "xxx", "BAR"];
        LineChange[] expected = [
            CreateLineChange(2, 3, 2, 3, [
                CreateCharChange(2, 1, 3, 1, 2, 1, 2, 4),
                CreateCharChange(3, 5, 3, 5, 2, 8, 3, 4),
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "big change part 1")]
    public void Big_Change_Part_1()
    {
        string[] original = ["foo", "abcd", "efgh", "BAR"];
        string[] modified = ["hello", "foo", "zzzefgh", "xxx", "BAR"];
        LineChange[] expected = [
            CreateLineInsertion(1, 1, 0),
            CreateLineChange(2, 3, 3, 4, [
                CreateCharChange(2, 1, 3, 1, 3, 1, 3, 4),
                CreateCharChange(3, 5, 3, 5, 3, 8, 4, 4)
            ])
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "big change part 2")]
    public void Big_Change_Part_2()
    {
        string[] original = ["foo", "abcd", "efgh", "BAR", "RAB"];
        string[] modified = ["hello", "foo", "zzzefgh", "xxx", "BAR"];
        LineChange[] expected = [
            CreateLineInsertion(1, 1, 0),
            CreateLineChange(2, 3, 3, 4, [
                CreateCharChange(2, 1, 3, 1, 3, 1, 3, 4),
                CreateCharChange(3, 5, 3, 5, 3, 8, 4, 4)
            ]),
            CreateLineDeletion(5, 5, 5)
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "char change postprocessing merges")]
    public void Char_Change_Postprocessing_Merges()
    {
        string[] original = ["abba"];
        string[] modified = ["azzzbzzzbzzza"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, [
                CreateCharChange(1, 2, 1, 4, 1, 2, 1, 13)
            ])
        ];
        AssertDiff(original, modified, expected, true, true);
    }

    [Fact(DisplayName = "ignore trim whitespace")]
    public void Ignore_Trim_Whitespace()
    {
        string[] original = ["\t\t foo ", "abcd", "efgh", "\t\t BAR\t\t"];
        string[] modified = ["  hello\t", "\t foo   \t", "zzzefgh", "xxx", "   BAR   \t"];
        LineChange[] expected = [
            CreateLineInsertion(1, 1, 0),
            CreateLineChange(2, 3, 3, 4, [
                CreateCharChange(2, 1, 2, 5, 3, 1, 3, 4),
                CreateCharChange(3, 5, 3, 5, 4, 1, 4, 4)
            ])
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "issue #12122 r.hasOwnProperty is not a function")]
    public void Issue_12122_R_HasOwnProperty_Is_Not_A_Function()
    {
        string[] original = ["hasOwnProperty"];
        string[] modified = ["hasOwnProperty", "and another line"];
        LineChange[] expected = [
            CreateLineInsertion(2, 2, 1)
        ];
        AssertDiff(original, modified, expected);
    }

    [Fact(DisplayName = "empty diff 1")]
    public void Empty_Diff_1()
    {
        string[] original = [""];
        string[] modified = ["something"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, null)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "empty diff 2")]
    public void Empty_Diff_2()
    {
        string[] original = [""];
        string[] modified = ["something", "something else"];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 2, null)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "empty diff 3")]
    public void Empty_Diff_3()
    {
        string[] original = ["something", "something else"];
        string[] modified = [""];
        LineChange[] expected = [
            CreateLineChange(1, 2, 1, 1, null)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "empty diff 4")]
    public void Empty_Diff_4()
    {
        string[] original = ["something"];
        string[] modified = [""];
        LineChange[] expected = [
            CreateLineChange(1, 1, 1, 1, null)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "empty diff 5")]
    public void Empty_Diff_5()
    {
        string[] original = [""];
        string[] modified = [""];
        LineChange[] expected = [];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "pretty diff 1")]
    public void Pretty_Diff_1()
    {
        string[] original = [
            "suite(function () {",
            "\ttest1() {",
            "\t\tassert.ok(true);",
            "\t}",
            "",
            "\ttest2() {",
            "\t\tassert.ok(true);",
            "\t}",
            "});",
            "",
        ];
        string[] modified = [
            "// An insertion",
            "suite(function () {",
            "\ttest1() {",
            "\t\tassert.ok(true);",
            "\t}",
            "",
            "\ttest2() {",
            "\t\tassert.ok(true);",
            "\t}",
            "",
            "\ttest3() {",
            "\t\tassert.ok(true);",
            "\t}",
            "});",
            "",
        ];
        LineChange[] expected = [
            CreateLineInsertion(1, 1, 0),
            CreateLineInsertion(10, 13, 8)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "pretty diff 2")]
    public void Pretty_Diff_2()
    {
        string[] original = [
            "// Just a comment",
            "",
            "function compute(a, b, c, d) {",
            "\tif (a) {",
            "\t\tif (b) {",
            "\t\t\tif (c) {",
            "\t\t\t\treturn 5;",
            "\t\t\t}",
            "\t\t}",
            "\t\t// These next lines will be deleted",
            "\t\tif (d) {",
            "\t\t\treturn -1;",
            "\t\t}",
            "\t\treturn 0;",
            "\t}",
            "}",
        ];
        string[] modified = [
            "// Here is an inserted line",
            "// and another inserted line",
            "// and another one",
            "// Just a comment",
            "",
            "function compute(a, b, c, d) {",
            "\tif (a) {",
            "\t\tif (b) {",
            "\t\t\tif (c) {",
            "\t\t\t\treturn 5;",
            "\t\t\t}",
            "\t\t}",
            "\t\treturn 0;",
            "\t}",
            "}",
        ];
        LineChange[] expected = [
            CreateLineInsertion(1, 3, 0),
            CreateLineDeletion(10, 13, 12),
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "pretty diff 3")]
    public void Pretty_Diff_3()
    {
        string[] original = [
            "class A {",
            "\t/**",
            "\t * m1",
            "\t */",
            "\tmethod1() {}",
            "",
            "\t/**",
            "\t * m3",
            "\t */",
            "\tmethod3() {}",
            "}",
        ];
        string[] modified = [
            "class A {",
            "\t/**",
            "\t * m1",
            "\t */",
            "\tmethod1() {}",
            "",
            "\t/**",
            "\t * m2",
            "\t */",
            "\tmethod2() {}",
            "",
            "\t/**",
            "\t * m3",
            "\t */",
            "\tmethod3() {}",
            "}",
        ];
        LineChange[] expected = [
            CreateLineInsertion(7, 11, 6)
        ];
        AssertDiff(original, modified, expected, true, false, true);
    }

    [Fact(DisplayName = "issue #23636")]
    public void Issue_23636()
    {
        string[] original = [
            "if(!TextDrawLoad[playerid])",
            "{",
            "",
            "\tTextDrawHideForPlayer(playerid,TD_AppleJob[3]);",
            "\tTextDrawHideForPlayer(playerid,TD_AppleJob[4]);",
            "\tif(!AppleJobTreesType[AppleJobTreesPlayerNum[playerid]])",
            "\t{",
            "\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[5+i]);",
            "\t}",
            "\telse",
            "\t{",
            "\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[15+i]);",
            "\t}",
            "}",
            "else",
            "{",
            "\tTextDrawHideForPlayer(playerid,TD_AppleJob[3]);",
            "\tTextDrawHideForPlayer(playerid,TD_AppleJob[27]);",
            "\tif(!AppleJobTreesType[AppleJobTreesPlayerNum[playerid]])",
            "\t{",
            "\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[28+i]);",
            "\t}",
            "\telse",
            "\t{",
            "\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[38+i]);",
            "\t}",
            "}",
        ];
        string[] modified = [
            "\tif(!TextDrawLoad[playerid])",
            "\t{",
            "\t",
            "\t\tTextDrawHideForPlayer(playerid,TD_AppleJob[3]);",
            "\t\tTextDrawHideForPlayer(playerid,TD_AppleJob[4]);",
            "\t\tif(!AppleJobTreesType[AppleJobTreesPlayerNum[playerid]])",
            "\t\t{",
            "\t\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[5+i]);",
            "\t\t}",
            "\t\telse",
            "\t\t{",
            "\t\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[15+i]);",
            "\t\t}",
            "\t}",
            "\telse",
            "\t{",
            "\t\tTextDrawHideForPlayer(playerid,TD_AppleJob[3]);",
            "\t\tTextDrawHideForPlayer(playerid,TD_AppleJob[27]);",
            "\t\tif(!AppleJobTreesType[AppleJobTreesPlayerNum[playerid]])",
            "\t\t{",
            "\t\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[28+i]);",
            "\t\t}",
            "\t\telse",
            "\t\t{",
            "\t\t\tfor(new i=0;i<10;i++) if(StatusTD_AppleJobApples[playerid][i]) TextDrawHideForPlayer(playerid,TD_AppleJob[38+i]);",
            "\t\t}",
            "\t}",
        ];
        LineChange[] expected = [
            CreateLineChange(
                1, 27, 1, 27,
                [
                    CreateCharChange(1, 1, 1, 1, 1, 1, 1, 2),
                    CreateCharChange(2, 1, 2, 1, 2, 1, 2, 2),
                    CreateCharChange(3, 1, 3, 1, 3, 1, 3, 2),
                    CreateCharChange(4, 1, 4, 1, 4, 1, 4, 2),
                    CreateCharChange(5, 1, 5, 1, 5, 1, 5, 2),
                    CreateCharChange(6, 1, 6, 1, 6, 1, 6, 2),
                    CreateCharChange(7, 1, 7, 1, 7, 1, 7, 2),
                    CreateCharChange(8, 1, 8, 1, 8, 1, 8, 2),
                    CreateCharChange(9, 1, 9, 1, 9, 1, 9, 2),
                    CreateCharChange(10, 1, 10, 1, 10, 1, 10, 2),
                    CreateCharChange(11, 1, 11, 1, 11, 1, 11, 2),
                    CreateCharChange(12, 1, 12, 1, 12, 1, 12, 2),
                    CreateCharChange(13, 1, 13, 1, 13, 1, 13, 2),
                    CreateCharChange(14, 1, 14, 1, 14, 1, 14, 2),
                    CreateCharChange(15, 1, 15, 1, 15, 1, 15, 2),
                    CreateCharChange(16, 1, 16, 1, 16, 1, 16, 2),
                    CreateCharChange(17, 1, 17, 1, 17, 1, 17, 2),
                    CreateCharChange(18, 1, 18, 1, 18, 1, 18, 2),
                    CreateCharChange(19, 1, 19, 1, 19, 1, 19, 2),
                    CreateCharChange(20, 1, 20, 1, 20, 1, 20, 2),
                    CreateCharChange(21, 1, 21, 1, 21, 1, 21, 2),
                    CreateCharChange(22, 1, 22, 1, 22, 1, 22, 2),
                    CreateCharChange(23, 1, 23, 1, 23, 1, 23, 2),
                    CreateCharChange(24, 1, 24, 1, 24, 1, 24, 2),
                    CreateCharChange(25, 1, 25, 1, 25, 1, 25, 2),
                    CreateCharChange(26, 1, 26, 1, 26, 1, 26, 2),
                    CreateCharChange(27, 1, 27, 1, 27, 1, 27, 2),
                ]
            )
            // CreateLineInsertion(7, 11, 6)
        ];
        AssertDiff(original, modified, expected, true, true, false);
    }

    [Fact(DisplayName = "issue #43922")]
    public void Issue_43922()
    {
        string[] original = [
            " * `yarn [install]` -- Install project NPM dependencies. This is automatically done when you first create the project. You should only need to run this if you add dependencies in `package.json`.",
        ];
        string[] modified = [
            " * `yarn` -- Install project NPM dependencies. You should only need to run this if you add dependencies in `package.json`.",
        ];
        LineChange[] expected = [
            CreateLineChange(
                1, 1, 1, 1,
                [
                    CreateCharChange(1, 9, 1, 19, 1, 9, 1, 9),
                    CreateCharChange(1, 58, 1, 120, 1, 48, 1, 48),
                ]
            )
        ];
        AssertDiff(original, modified, expected, true, true, false);
    }

    [Fact(DisplayName = "issue #42751")]
    public void Issue_42751()
    {
        string[] original = [
            "    1",
            "  2",
        ];
        string[] modified = [
            "    1",
            "   3",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 2, 2, 2,
                [
                    CreateCharChange(2, 3, 2, 4, 2, 3, 2, 5)
                ]
            )
        ];
        AssertDiff(original, modified, expected, true, true, false);
    }

    [Fact(DisplayName = "does not give character changes")]
    public void Does_Not_Give_Character_Changes()
    {
        string[] original = [
            "    1",
            "  2",
            "A",
        ];
        string[] modified = [
            "    1",
            "   3",
            " A",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 3, 2, 3
            )
        ];
        AssertDiff(original, modified, expected, false, false, false);
    }

    [Fact(DisplayName = "issue #44422: Less than ideal diff results")]
    public void Issue_44422_Less_Than_Ideal_Diff_Results()
    {
        string[] original = [
            "export class C {",
            "",
            "\tpublic m1(): void {",
            "\t\t{",
            "\t\t//2",
            "\t\t//3",
            "\t\t//4",
            "\t\t//5",
            "\t\t//6",
            "\t\t//7",
            "\t\t//8",
            "\t\t//9",
            "\t\t//10",
            "\t\t//11",
            "\t\t//12",
            "\t\t//13",
            "\t\t//14",
            "\t\t//15",
            "\t\t//16",
            "\t\t//17",
            "\t\t//18",
            "\t\t}",
            "\t}",
            "",
            "\tpublic m2(): void {",
            "\t\tif (a) {",
            "\t\t\tif (b) {",
            "\t\t\t\t//A1",
            "\t\t\t\t//A2",
            "\t\t\t\t//A3",
            "\t\t\t\t//A4",
            "\t\t\t\t//A5",
            "\t\t\t\t//A6",
            "\t\t\t\t//A7",
            "\t\t\t\t//A8",
            "\t\t\t}",
            "\t\t}",
            "",
            "\t\t//A9",
            "\t\t//A10",
            "\t\t//A11",
            "\t\t//A12",
            "\t\t//A13",
            "\t\t//A14",
            "\t\t//A15",
            "\t}",
            "",
            "\tpublic m3(): void {",
            "\t\tif (a) {",
            "\t\t\t//B1",
            "\t\t}",
            "\t\t//B2",
            "\t\t//B3",
            "\t}",
            "",
            "\tpublic m4(): boolean {",
            "\t\t//1",
            "\t\t//2",
            "\t\t//3",
            "\t\t//4",
            "\t}",
            "",
            "}",
        ];
        string[] modified = [
            "export class C {",
            "",
            "\tconstructor() {",
            "",
            "",
            "",
            "",
            "\t}",
            "",
            "\tpublic m1(): void {",
            "\t\t{",
            "\t\t//2",
            "\t\t//3",
            "\t\t//4",
            "\t\t//5",
            "\t\t//6",
            "\t\t//7",
            "\t\t//8",
            "\t\t//9",
            "\t\t//10",
            "\t\t//11",
            "\t\t//12",
            "\t\t//13",
            "\t\t//14",
            "\t\t//15",
            "\t\t//16",
            "\t\t//17",
            "\t\t//18",
            "\t\t}",
            "\t}",
            "",
            "\tpublic m4(): boolean {",
            "\t\t//1",
            "\t\t//2",
            "\t\t//3",
            "\t\t//4",
            "\t}",
            "",
            "}",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 0, 3, 9
            ),
            CreateLineChange(
                25, 55, 31, 0
            )
        ];
        AssertDiff(original, modified, expected, false, false, false);
    }

    [Fact(DisplayName = "gives preference to matching longer lines")]
    public void Gives_Preference_To_Matching_Longer_Lines()
    {
        string[] original = [
            "A",
            "A",
            "BB",
            "C",
        ];
        string[] modified = [
            "A",
            "BB",
            "A",
            "D",
            "E",
            "A",
            "C",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 2, 1, 0
            ),
            CreateLineChange(
                3, 0, 3, 6
            )
        ];
        AssertDiff(original, modified, expected, false, false, false);
    }

    [Fact(DisplayName = "issue #119051: gives preference to fewer diff hunks")]
    public void Issue_119051_Gives_Preference_To_Fewer_Diff_Hunks()
    {
        string[] original = [
            "1",
            "",
            "",
            "2",
            "",
        ];
        string[] modified = [
            "1",
            "",
            "1.5",
            "",
            "",
            "2",
            "",
            "3",
            "",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 0, 3, 4
            ),
            CreateLineChange(
                5, 0, 8, 9
            )
        ];
        AssertDiff(original, modified, expected, false, false, false);
    }

    [Fact(DisplayName = "issue #121436: Diff chunk contains an unchanged line part 1")]
    public void Issue_121436_Diff_Chunk_Contains_An_Unchanged_Line_Part_1()
    {
        string[] original = [
            "if (cond) {",
            "    cmd",
            "}",
        ];
        string[] modified = [
            "if (cond) {",
            "    if (other_cond) {",
            "        cmd",
            "    }",
            "}",
        ];
        LineChange[] expected = [
            CreateLineChange(
                1, 0, 2, 2
            ),
            CreateLineChange(
                2, 0, 4, 4
            )
        ];
        AssertDiff(original, modified, expected, false, false, true);
    }

    [Fact(DisplayName = "issue #121436: Diff chunk contains an unchanged line part 2")]
    public void Issue_121436_Diff_Chunk_Contains_An_Unchanged_Line_Part_2()
    {
        string[] original = [
            "if (cond) {",
            "    cmd",
            "}",
        ];
        string[] modified = [
            "if (cond) {",
            "    if (other_cond) {",
            "        cmd",
            "    }",
            "}",
        ];
        LineChange[] expected = [
            CreateLineChange(
                1, 0, 2, 2
            ),
            CreateLineChange(
                2, 2, 3, 3
            ),
            CreateLineChange(
                2, 0, 4, 4
            )
        ];
        AssertDiff(original, modified, expected, false, false, false);
    }

    [Fact(DisplayName = "issue #169552: Assertion error when having both leading and trailing whitespace diffs")]
    public void Issue_169552_Assertion_Error_When_Having_Both_Leading_And_Trailing_Whitespace_Diffs()
    {
        string[] original = [
            "if True:",
            "    print(2)",
        ];
        string[] modified = [
            "if True:",
            "\tprint(2) ",
        ];
        LineChange[] expected = [
            CreateLineChange(
                2, 2, 2, 2,
                [
                    CreateCharChange(2, 1, 2, 5, 2, 1, 2, 2),
                    CreateCharChange(2, 13, 2, 13, 2, 10, 2, 11),
                ]
            ),
        ];
        AssertDiff(original, modified, expected, true, false, false);
    }
}

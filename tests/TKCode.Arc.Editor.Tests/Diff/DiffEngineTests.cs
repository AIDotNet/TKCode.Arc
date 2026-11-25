// Not in vscode: tests of the diff engine's parts that upstream tests only through the diff editor (the sequence
// algorithms, LineRangeSet, timeouts, the snapshot-based computation, UnchangedRegion of diffEditorViewModel.ts and
// computeRangeAlignment of diffEditorViewZones.ts).

using Shouldly;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Diff.Algorithms;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Diff;

public sealed class DiffEngineTests
{
    private sealed class CharSequence(string text) : ISequence
    {
        public int GetElement(int offset) => text[offset];

        public int Length => text.Length;

        public bool IsStronglyEqual(int offset1, int offset2) => text[offset1] == text[offset2];
    }

    // the characters both sequences keep, by walking the unchanged parts between the diffs
    private static (string Common1, string Common2) Unchanged(string a, string b, IReadOnlyList<SequenceDiff> diffs)
    {
        var equal = SequenceDiff.Invert(diffs, a.Length);
        var common1 = string.Concat(equal.Select(e => a.Substring(e.Seq1Range.Start, e.Seq1Range.Length)));
        var common2 = string.Concat(equal.Select(e => b.Substring(e.Seq2Range.Start, e.Seq2Range.Length)));
        return (common1, common2);
    }

    [Theory]
    [InlineData("abcabba", "cbabac", 4)]
    [InlineData("", "abc", 0)]
    [InlineData("abc", "", 0)]
    [InlineData("hello world", "hello brave new world", 11)]
    [InlineData("the quick brown fox", "the quick red fox jumps", 15)]
    public void Myers_Finds_A_Longest_Common_Subsequence(string a, string b, int lcsLength)
    {
        var result = new MyersDiffAlgorithm().Compute(new CharSequence(a), new CharSequence(b));

        result.HitTimeout.ShouldBeFalse();
        SequenceDiff.AssertSorted(result.Diffs);
        var (common1, common2) = Unchanged(a, b, result.Diffs);
        common1.ShouldBe(common2);
        common1.Length.ShouldBe(lcsLength);
    }

    [Theory]
    [InlineData("abcabba", "cbabac")]
    [InlineData("hello world", "hello brave new world")]
    [InlineData("aaaa", "aaaa")]
    public void Dynamic_Programming_Keeps_Equal_Parts_Equal(string a, string b)
    {
        var result = new DynamicProgrammingDiffing().Compute(new CharSequence(a), new CharSequence(b));

        var (common1, common2) = Unchanged(a, b, result.Diffs);
        common1.ShouldBe(common2);
        if (a == b)
        {
            result.Diffs.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Algorithms_Stop_When_The_Timeout_Ends()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var timeout = new CancellationTimeout(cancelled.Token);

        var myers = new MyersDiffAlgorithm().Compute(new CharSequence("abcdef"), new CharSequence("fedcba"), timeout);
        myers.HitTimeout.ShouldBeTrue();
        myers.Diffs.Count.ShouldBe(1);

        var dp = new DynamicProgrammingDiffing().Compute(new CharSequence("abcdef"), new CharSequence("fedcba"), timeout);
        dp.HitTimeout.ShouldBeTrue();
    }

    [Fact]
    public void Default_Computer_Reports_A_Cancelled_Computation_As_Timed_Out()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        string[] original = ["a", "b", "c"];
        string[] modified = ["a", "x", "c", "d"];

        var diff = new DefaultLinesDiffComputer().ComputeDiff(original, modified, new LinesDiffComputerOptions { CancellationToken = cancelled.Token });

        diff.HitTimeout.ShouldBeTrue();
        // the trivial diff still describes the change
        DocumentDiffComputer.VerifyLinesDiff(diff.Changes, original, modified, false).ShouldBe("valid");
    }

    [Fact]
    public void Date_Timeout_Must_Be_Positive() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DateTimeout(0));

    [Fact]
    public void LineRangeSet_Adds_Subtracts_And_Intersects()
    {
        var set = new LineRangeSet();
        set.AddRange(new LineRange(10, 20));
        set.AddRange(new LineRange(1, 3));
        set.AddRange(new LineRange(20, 22)); // touching: joined
        set.AddRange(new LineRange(5, 5)); // empty: ignored

        set.Ranges.ShouldBe([new LineRange(1, 3), new LineRange(10, 22)]);
        set.Contains(2).ShouldBeTrue();
        set.Contains(3).ShouldBeFalse();
        set.Contains(21).ShouldBeTrue();
        set.Intersects(new LineRange(3, 10)).ShouldBeFalse();
        set.Intersects(new LineRange(3, 11)).ShouldBeTrue();

        set.SubtractFrom(new LineRange(1, 30)).Ranges.ShouldBe([new LineRange(3, 10), new LineRange(22, 30)]);
        set.GetIntersection(new LineRangeSet([new LineRange(2, 12)])).Ranges.ShouldBe([new LineRange(2, 3), new LineRange(10, 12)]);
        set.GetWithDelta(1).Ranges.ShouldBe([new LineRange(2, 4), new LineRange(11, 23)]);

        set.AddRange(new LineRange(2, 15)); // spans both: one range
        set.Ranges.ShouldBe([new LineRange(1, 22)]);
    }

    [Fact]
    public void Inverse_Gives_The_Unchanged_Line_Ranges()
    {
        var changes = new[]
        {
            new LineRangeMapping(new LineRange(3, 4), new LineRange(3, 5)),
            new LineRangeMapping(new LineRange(8, 8), new LineRange(9, 11)),
        };

        var inverse = LineRangeMapping.Inverse(changes, 10, 13);

        inverse.Select(m => m.ToString()).ShouldBe(["{[1,3)->[1,3)}", "{[4,8)->[5,9)}", "{[8,11)->[11,14)}"]);
    }

    [Fact]
    public void Moved_Block_Is_Detected()
    {
        string[] block = ["function moved(a, b) {", "    const sum = a + b;", "    const product = a * b;", "    return sum + product;", "}"];
        var others = Enumerable.Range(0, 12).Select(i => $"const other{i} = compute({i}, \"text {i}\");").ToArray();

        var diff = DocumentDiffComputer.ComputeDiff([.. block, .. others], [.. others, .. block], new DocumentDiffProviderOptions { ComputeMoves = true });

        diff.Identical.ShouldBeFalse();
        diff.Moves.Count.ShouldBe(1);
        diff.Moves[0].LineRangeMapping.Original.ShouldBe(new LineRange(1, 6));
        diff.Moves[0].LineRangeMapping.Modified.ShouldBe(new LineRange(13, 18));
        diff.Moves[0].Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Identical_Documents_Have_No_Changes()
    {
        var diff = DocumentDiffComputer.ComputeDiff(["a", "b"], ["a", "b"], new DocumentDiffProviderOptions());

        diff.Identical.ShouldBeTrue();
        diff.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void Whitespace_Only_Changes_Are_Not_Identical_When_Ignored()
    {
        var diff = DocumentDiffComputer.ComputeDiff(["a", "  b"], ["a", "b"], new DocumentDiffProviderOptions { IgnoreTrimWhitespace = true });

        diff.Changes.ShouldBeEmpty();
        diff.Identical.ShouldBeFalse();
    }

    [Fact]
    public void Normalize_Extends_Whole_Line_Inner_Changes_To_The_Next_Line()
    {
        // line 2 replaced entirely: the inner change ends at the end of line 2, normalized to the start of line 3
        var diff = DocumentDiffComputer.ComputeDiff(["a", "bbb", "c"], ["a", "xyz", "c"], new DocumentDiffProviderOptions { IgnoreTrimWhitespace = false });

        var inner = diff.Changes.Single().InnerChanges!.Single();
        inner.OriginalRange.ShouldBe(new TextRange(2, 1, 3, 1));
        inner.ModifiedRange.ShouldBe(new TextRange(2, 1, 3, 1));
    }

    [Fact]
    public async Task Diff_Of_Snapshots_Runs_Off_The_Calling_Thread()
    {
        using var original = TestTextModel.Create("line 1\nline 2\nline 3");
        using var modified = TestTextModel.Create("line 1\nline two\nline 3\nline 4");
        var originalSnapshot = original.CreateTextSnapshot();
        var modifiedSnapshot = modified.CreateTextSnapshot();
        // editing after the snapshot does not change what is diffed
        modified.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "changed later ")]);

        var diff = await DocumentDiffComputer.ComputeDiffAsync(originalSnapshot, modifiedSnapshot, new DocumentDiffProviderOptions(), TestContext.Current.CancellationToken);

        diff.Changes.Select(c => c.ToString()).ShouldBe(["{[2,3)->[2,3)}", "{[4,4)->[4,5)}"]);
    }

    [Fact]
    public async Task Diff_Of_Snapshots_Can_Be_Cancelled()
    {
        using var original = TestTextModel.Create("a");
        using var modified = TestTextModel.Create("b");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            DocumentDiffComputer.ComputeDiffAsync(original.CreateTextSnapshot(), modified.CreateTextSnapshot(), new DocumentDiffProviderOptions(), cancelled.Token));
    }

    private static DetailedLineRangeMapping Change(int originalStart, int originalEnd, int modifiedStart, int modifiedEnd, params RangeMapping[] inner) =>
        new(new LineRange(originalStart, originalEnd), new LineRange(modifiedStart, modifiedEnd), inner.Length == 0 ? null : inner);

    [Fact]
    public void Unchanged_Regions_Keep_Context_Lines_Around_Changes()
    {
        var regions = UnchangedRegion.FromDiffs([Change(21, 22, 21, 22)], 40, 40, minHiddenLineCount: 3, minContext: 3);

        regions.Select(r => (r.OriginalLineNumber, r.ModifiedLineNumber, r.LineCount)).ShouldBe([(1, 1, 17), (25, 25, 16)]);
        regions[0].GetHiddenModifiedRange().ShouldBe(new LineRange(1, 18));
        regions[1].GetHiddenOriginalRange().ShouldBe(new LineRange(25, 41));
    }

    [Fact]
    public void Unchanged_Regions_Between_Changes_Need_Enough_Lines()
    {
        // 8 unchanged lines between the changes: 3 + 3 context leaves 2, fewer than 3 to hide
        var few = UnchangedRegion.FromDiffs([Change(2, 3, 2, 3), Change(11, 12, 11, 12)], 12, 12, 3, 3);
        few.ShouldBeEmpty();

        var many = UnchangedRegion.FromDiffs([Change(2, 3, 2, 4), Change(13, 14, 14, 15)], 14, 15, 3, 3);
        many.Select(r => (r.OriginalLineNumber, r.ModifiedLineNumber, r.LineCount)).ShouldBe([(6, 7, 4)]);
    }

    [Fact]
    public void Unchanged_Region_Shows_More_And_Reveals_Lines()
    {
        var region = new UnchangedRegion(1, 1, 17, 0, 0);
        var changes = 0;
        region.Changed += _ => changes++;

        region.ShowMoreAbove(10);
        region.GetHiddenModifiedRange().ShouldBe(new LineRange(11, 18));
        region.ShowMoreBelow(3);
        region.GetHiddenModifiedRange().ShouldBe(new LineRange(11, 15));
        region.IsFullyVisible.ShouldBeFalse();
        region.ShowAll();
        region.GetHiddenModifiedRange().IsEmpty.ShouldBeTrue();
        region.IsFullyVisible.ShouldBeTrue();
        region.CollapseAll();
        region.GetHiddenModifiedRange().ShouldBe(new LineRange(1, 18));
        changes.ShouldBe(4);

        region.ShowModifiedLine(5, RevealPreference.FromCloserSide);
        region.GetHiddenModifiedRange().ShouldBe(new LineRange(6, 18));
        region.CollapseAll();
        region.ShowModifiedLine(15, RevealPreference.FromCloserSide);
        region.GetHiddenModifiedRange().ShouldBe(new LineRange(1, 15));
    }

    [Fact]
    public void Unchanged_Region_Splits_At_Visible_Ranges()
    {
        var region = new UnchangedRegion(1, 1, 20, 0, 0);

        var parts = region.SetVisibleRanges([new LineRangeMapping(new LineRange(8, 10), new LineRange(8, 10))]);

        parts.Count.ShouldBe(2);
        parts[0].GetHiddenModifiedRange().ShouldBe(new LineRange(1, 8));
        parts[1].GetHiddenModifiedRange().ShouldBe(new LineRange(10, 21));
    }

    [Fact]
    public void Alignment_Of_A_Change_Uses_Both_Heights()
    {
        var change = Change(2, 3, 2, 4);

        var alignments = DiffAlignment.ComputeRangeAlignment([change], [], [], 10, 10, false, _ => 1, 4);

        alignments.Count.ShouldBe(1);
        alignments[0].OriginalRange.ShouldBe(new LineRange(2, 3));
        alignments[0].ModifiedRange.ShouldBe(new LineRange(2, 4));
        alignments[0].OriginalHeightInPx.ShouldBe(10);
        alignments[0].ModifiedHeightInPx.ShouldBe(20);
        alignments[0].Diff.ShouldBeSameAs(change);
    }

    [Fact]
    public void Alignment_Accounts_For_Extra_Line_Heights_Outside_Changes()
    {
        var change = Change(4, 5, 4, 6);

        var alignments = DiffAlignment.ComputeRangeAlignment([change], [new AdditionalLineHeight(1, 5)], [], 10, 10, false, _ => 1, 6);

        alignments.Count.ShouldBe(2);
        alignments[0].OriginalRange.ShouldBe(new LineRange(1, 2));
        alignments[0].ModifiedRange.ShouldBe(new LineRange(1, 2));
        alignments[0].OriginalHeightInPx.ShouldBe(15);
        alignments[0].ModifiedHeightInPx.ShouldBe(10);
        alignments[0].Diff.ShouldBeNull();
    }

    [Fact]
    public void Inner_Hunk_Alignment_Splits_A_Change_At_Unchanged_Text()
    {
        // original: "a", "keep x", "b"  modified: "a", "new", "keep y", "b": the change [2,3) -> [2,4) has an inner
        // change that starts after "keep " on line 2 of the original and line 3 of the modified
        string[] original = ["a", "keep x", "b"];
        string[] modified = ["a", "new", "keep y", "b"];
        var change = Change(2, 3, 2, 4,
            new RangeMapping(new TextRange(2, 1, 2, 1), new TextRange(2, 1, 3, 1)),
            new RangeMapping(new TextRange(2, 6, 2, 7), new TextRange(3, 6, 3, 7)));

        var alignments = DiffAlignment.ComputeRangeAlignment([change], [], [], 10, 10, true, line => original[line - 1].Length + 1, original.Length);

        alignments.Select(a => (a.OriginalRange, a.ModifiedRange)).ShouldBe(
        [
            (new LineRange(2, 2), new LineRange(2, 3)),
            (new LineRange(2, 3), new LineRange(3, 4)),
        ]);
        modified.Length.ShouldBe(4);
    }
}

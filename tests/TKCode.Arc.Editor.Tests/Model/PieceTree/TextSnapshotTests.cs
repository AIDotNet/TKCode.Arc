// Tests for TextSnapshot, which is not in vscode: a random-access snapshot of the piece tree that other threads read.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextSnapshotTests
{
    private static string[] SplitLines(string text) => text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

    private static void AssertMatches(TextSnapshot snapshot, string text)
    {
        var lines = SplitLines(text);
        snapshot.Length.ShouldBe(text.Length);
        snapshot.LineCount.ShouldBe(lines.Length);
        snapshot.GetText().ShouldBe(text);
        var cursor = default(TextSnapshot.LineCursor);
        for (var i = 0; i < lines.Length; i++)
        {
            snapshot.GetLineContentSpan(i + 1, ref cursor).ToString().ShouldBe(lines[i], $"line {i + 1} (cursor)");
            snapshot.GetLineContent(i + 1).ShouldBe(lines[i], $"line {i + 1}");
            snapshot.GetLineContentSpan(i + 1).ToString().ShouldBe(lines[i], $"line {i + 1} (span)");
            snapshot.GetLineLength(i + 1).ShouldBe(lines[i].Length);
        }
    }

    /// <summary>
    /// The raw tree without EOL normalization takes any offset; arbitrary offsets would break a normalized tree's
    /// invariant (deleting the \n of a \r\n), so that case goes through the model, which edits by position.
    /// </summary>
    [Fact]
    public void Random_Tree_Edits_Match_The_Text_At_The_Time_Of_The_Snapshot()
    {
        var random = new Random(12);
        var tree = PieceTreeTestHelpers.CreateTextBuffer([PieceTreeTestHelpers.RandomStr(random, 200)], normalizeEol: false).GetPieceTree();
        var taken = new List<(TextSnapshot Snapshot, string Text)>();

        for (var i = 0; i < 300; i++)
        {
            var length = tree.GetLength();
            if (random.Next(3) == 0 && length > 0)
            {
                var offset = random.Next(length);
                tree.Delete(offset, Math.Min(random.Next(1, 10), length - offset));
            }
            else
            {
                tree.Insert(random.Next(length + 1), PieceTreeTestHelpers.RandomStr(random, random.Next(1, 12)), false);
            }

            if (i % 10 == 0)
            {
                taken.Add((tree.CreateTextSnapshot(i), tree.GetLinesRawContent()));
            }

            // the live tree and a fresh snapshot agree line by line
            if (i % 50 == 0)
            {
                var fresh = tree.CreateTextSnapshot(i);
                fresh.LineCount.ShouldBe(tree.GetLineCount());
                for (var line = 1; line <= tree.GetLineCount(); line++)
                {
                    fresh.GetLineContent(line).ShouldBe(tree.GetLineContent(line), $"edit {i}, line {line}");
                }
            }
        }

        // every snapshot still shows its own version after all the edits that followed
        foreach (var (snapshot, text) in taken)
        {
            AssertMatches(snapshot, text);
        }
    }

    [Theory]
    [InlineData(EndOfLineSequence.LF)]
    [InlineData(EndOfLineSequence.CRLF)]
    public void Random_Model_Edits_Match_The_Text_At_The_Time_Of_The_Snapshot(EndOfLineSequence eol)
    {
        var random = new Random((int)eol + 20);
        using var model = TestTextModel.Create(PieceTreeTestHelpers.RandomStr(random, 200));
        model.SetEOL(eol);
        var taken = new List<(TextSnapshot Snapshot, string Text)>();

        for (var i = 0; i < 300; i++)
        {
            var start = RandomPosition(model, random);
            var end = random.Next(3) == 0 ? RandomPosition(model, random) : start;
            model.ApplyEdits([EditOperation.Replace(new TextRange(start, end), PieceTreeTestHelpers.RandomStr(random, random.Next(0, 12)))]);

            if (i % 10 == 0)
            {
                taken.Add((model.CreateTextSnapshot(), model.GetValue()));
            }
        }

        foreach (var (snapshot, text) in taken)
        {
            AssertMatches(snapshot, text);
        }
    }

    private static TextPosition RandomPosition(TextModel model, Random random)
    {
        var line = random.Next(1, model.GetLineCount() + 1);
        return new TextPosition(line, random.Next(1, model.GetLineMaxColumn(line) + 1));
    }

    [Fact]
    public void Offsets_And_Ranges()
    {
        using var model = TestTextModel.Create("first line\r\nsecond\r\n\r\nlast");
        model.GetEOL().ShouldBe("\r\n");
        var snapshot = model.CreateTextSnapshot();

        snapshot.Eol.ShouldBe("\r\n");
        snapshot.VersionId.ShouldBe(model.GetVersionId());
        snapshot.GetOffsetAt(1, 1).ShouldBe(0);
        snapshot.GetOffsetAt(2, 1).ShouldBe(12);
        snapshot.GetOffsetAt(2, 100).ShouldBe(18); // clamped to the end of the line
        snapshot.GetOffsetAt(4, 5).ShouldBe(model.GetOffsetAt(new TextPosition(4, 5)));
        snapshot.GetValueInRange(new TextRange(1, 7, 2, 4)).ShouldBe("line\r\nsec");
        snapshot.GetLineContent(3).ShouldBe(string.Empty);
        Should.Throw<ArgumentOutOfRangeException>(() => snapshot.GetLineContent(5));
    }

    [Fact]
    public void A_Cursor_Works_In_Any_Order()
    {
        var random = new Random(5);
        using var model = TestTextModel.Create(string.Join('\n', Enumerable.Range(0, 500).Select(i => "line " + i)));
        for (var i = 0; i < 200; i++)
        {
            var line = random.Next(1, model.GetLineCount() + 1);
            model.ApplyEdits([EditOperation.Insert(new TextPosition(line, 1), "x\ny")]);
        }

        var snapshot = model.CreateTextSnapshot();
        var cursor = default(TextSnapshot.LineCursor);
        for (var i = 0; i < 2000; i++)
        {
            var line = random.Next(1, snapshot.LineCount + 1);
            snapshot.GetLineContentSpan(line, ref cursor).ToString().ShouldBe(model.GetLineContent(line), $"line {line}");
        }
    }

    [Fact]
    public void Empty_Text()
    {
        using var model = TestTextModel.Create(string.Empty);
        var snapshot = model.CreateTextSnapshot();
        snapshot.LineCount.ShouldBe(1);
        snapshot.Length.ShouldBe(0);
        snapshot.GetLineContent(1).ShouldBe(string.Empty);

        model.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "a\nb")]);
        model.ApplyEdits([EditOperation.Delete(model.GetFullModelRange())]);
        AssertMatches(model.CreateTextSnapshot(), string.Empty);
        AssertMatches(snapshot, string.Empty);
    }

    [Fact]
    public async Task A_Snapshot_Read_On_Another_Thread_While_The_Model_Is_Edited_Stays_Unchanged()
    {
        var lines = Enumerable.Range(0, 2000).Select(i => $"line {i} of the original text").ToArray();
        using var model = TestTextModel.Create(string.Join('\n', lines));
        var snapshot = model.CreateTextSnapshot();
        using var stop = new CancellationTokenSource();
        var failures = 0;
        var rounds = 0;

        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!snapshot.GetLineContentSpan(i + 1).SequenceEqual(lines[i]))
                    {
                        Interlocked.Increment(ref failures);
                    }
                }

                Interlocked.Increment(ref rounds);
            }
        }, TestContext.Current.CancellationToken);

        // edit hard enough to grow the change buffer (its array is replaced while the reader runs) and to rebuild
        // the line start arrays many times
        var random = new Random(3);
        for (var i = 0; i < 5000; i++)
        {
            var line = random.Next(1, model.GetLineCount() + 1);
            model.ApplyEdits([EditOperation.Insert(new TextPosition(line, 1), new string('x', random.Next(1, 40)) + "\n")]);
            if (i % 3 == 0)
            {
                var deleteLine = random.Next(1, model.GetLineCount());
                model.ApplyEdits([EditOperation.Delete(new TextRange(deleteLine, 1, deleteLine + 1, 1))]);
            }
        }

        // let the reader finish at least two full passes over the edited state
        var target = Volatile.Read(ref rounds) + 2;
        SpinWait.SpinUntil(() => Volatile.Read(ref rounds) >= target, TimeSpan.FromSeconds(30)).ShouldBeTrue();
        await stop.CancelAsync();
        await reader;

        failures.ShouldBe(0);
        snapshot.LineCount.ShouldBe(lines.Length);
        model.GetLineCount().ShouldNotBe(lines.Length);
    }
}

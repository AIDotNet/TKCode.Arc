using Shouldly;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Core.Tests.Primitives;

public class WordPacerTests
{
    private static string Words(int count, string prefix = "w") => string.Join(" ", Enumerable.Range(0, count).Select(i => $"{prefix}{i}")) + " ";

    private static int WordCount(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Ticks at <paramref name="hz"/> frames a second until the pacer is dry; returns each release with its time.</summary>
    private static List<(TimeSpan Time, string Text)> Run(WordPacer pacer, double hz, double maxSeconds = 20)
    {
        var releases = new List<(TimeSpan, string)>();
        var frames = (int)(maxSeconds * hz);
        for (var frame = 0; frame < frames; frame++)
        {
            var now = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * frame / hz));
            if (pacer.Tick(now) is { } text)
            {
                releases.Add((now, text));
            }
            if (!pacer.HasPending)
            {
                break;
            }
        }
        return releases;
    }

    private static double[] Gaps(List<(TimeSpan Time, string Text)> releases)
        => releases.Zip(releases.Skip(1), (a, b) => (b.Time - a.Time).TotalMilliseconds).ToArray();

    [Fact]
    public void At_60Hz_It_Releases_Every_Third_Frame_In_Batches_Of_Four_Or_Five_Words()
    {
        // Up to one second of reading (95 words) is shown at reading speed; more would trigger the catch-up.
        var pacer = new WordPacer();
        pacer.Push(Words(90));
        var releases = Run(pacer, 60);
        Gaps(releases).ShouldAllBe(gap => Math.Abs(gap - 50) < 0.5);
        // The first batch is a little shorter (it counts one clock interval, about 3 words); then 4-5 a batch.
        WordCount(releases[0].Text).ShouldBe(3);
        foreach (var batch in releases.Skip(1).Take(releases.Count - 2))
        {
            WordCount(batch.Text).ShouldBeInRange(4, 5);
        }
        string.Concat(releases.Select(r => r.Text)).ShouldBe(Words(90));
    }

    [Fact]
    public void At_120Hz_It_Releases_Every_Fifth_Frame()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(90));
        var releases = Run(pacer, 120);
        Gaps(releases).ShouldAllBe(gap => Math.Abs(gap - 41.667) < 0.5);
        foreach (var batch in releases.Skip(1).Take(releases.Count - 2))
        {
            WordCount(batch.Text).ShouldBeInRange(3, 5);
        }
    }

    [Fact]
    public void The_Average_Speed_Matches_WordsPerSecond()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(90));
        var releases = Run(pacer, 60);
        var seconds = (releases[^1].Time - releases[0].Time).TotalSeconds;
        var words = releases.Skip(1).Sum(r => WordCount(r.Text));
        (words / seconds).ShouldBe(95, 8);

        // A producer that keeps ahead at a steady 80 words a second is shown at reading speed too, never in a rush.
        var steady = new WordPacer();
        var shown = 0;
        var biggest = 0;
        for (var frame = 0; frame < 600; frame++)
        {
            var now = TimeSpan.FromTicks(TimeSpan.TicksPerSecond * frame / 60);
            if (frame % 3 == 0)
            {
                steady.Push(Words(4, $"s{frame}x"));
            }
            if (steady.Tick(now) is { } text)
            {
                shown += WordCount(text);
                biggest = Math.Max(biggest, WordCount(text));
            }
        }
        biggest.ShouldBeLessThanOrEqualTo(8);
        shown.ShouldBeGreaterThan(700);
    }

    [Fact]
    public void The_First_Release_Does_Not_Wait()
    {
        var pacer = new WordPacer();
        pacer.Push("Hello world ");
        pacer.Tick(TimeSpan.FromSeconds(5)).ShouldBe("Hello world ");
    }

    [Fact]
    public void Nothing_Is_Released_Before_A_Whole_Word_Has_Arrived()
    {
        var pacer = new WordPacer();
        pacer.Push("Hel");
        pacer.PendingWords.ShouldBe(0);
        pacer.HasPending.ShouldBeTrue();
        pacer.Tick(TimeSpan.Zero).ShouldBeNull();
        pacer.Push("lo");
        pacer.Tick(TimeSpan.FromMilliseconds(100)).ShouldBeNull();
        pacer.Push(" wor");
        // "Hello " is whole; "wor" is held.
        pacer.Tick(TimeSpan.FromMilliseconds(200)).ShouldBe("Hello ");
        pacer.Tick(TimeSpan.FromMilliseconds(300)).ShouldBeNull();
        pacer.Push("ld");
        pacer.Tick(TimeSpan.FromMilliseconds(400)).ShouldBeNull();
        pacer.Complete();
        pacer.Tick(TimeSpan.FromMilliseconds(500)).ShouldBe("world");
        pacer.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void Chunks_Of_Any_Size_Reproduce_The_Text_Exactly()
    {
        var text = "Leading space, then  words\twith\nodd   spacing, 你好世界, 😀 emoji and é accents.  ";
        var random = new Random(7);
        for (var round = 0; round < 50; round++)
        {
            var pacer = new WordPacer();
            var released = new System.Text.StringBuilder();
            var now = TimeSpan.Zero;
            var index = 0;
            while (index < text.Length)
            {
                var size = random.Next(1, 12);
                pacer.Push(text.Substring(index, Math.Min(size, text.Length - index)));
                index += size;
                now += TimeSpan.FromMilliseconds(random.Next(5, 80));
                released.Append(pacer.Tick(now));
            }
            pacer.Complete();
            while (!pacer.IsFinished)
            {
                now += TimeSpan.FromMilliseconds(17);
                released.Append(pacer.Tick(now));
            }
            released.ToString().ShouldBe(text, $"round {round}");
        }
    }

    [Fact]
    public void A_Big_Backlog_Is_Caught_Up_Within_About_The_Catch_Up_Window()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(500));
        var releases = Run(pacer, 60);
        // Everything is shown within ~1 s (reading speed alone would take over 5 s).
        var seconds = (releases[^1].Time - releases[0].Time).TotalSeconds;
        seconds.ShouldBeLessThan(1.15);
        releases.Max(r => WordCount(r.Text)).ShouldBeGreaterThan(8);
        releases.Sum(r => WordCount(r.Text)).ShouldBe(500);
        string.Concat(releases.Select(r => r.Text)).ShouldBe(Words(500));
    }

    [Fact]
    public void Complete_Drains_The_Rest_Within_The_Drain_Time()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(70));
        pacer.Tick(TimeSpan.Zero);
        pacer.Complete();
        var start = TimeSpan.FromMilliseconds(50);
        var releases = new List<(TimeSpan, string)>();
        for (var frame = 0; frame < 120 && !pacer.IsFinished; frame++)
        {
            var now = start + TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * frame / 60.0));
            if (pacer.Tick(now) is { } text)
            {
                releases.Add((now, text));
            }
        }
        pacer.IsFinished.ShouldBeTrue();
        (releases[^1].Item1 - start).TotalMilliseconds.ShouldBeLessThanOrEqualTo(600 + 50);
        releases.Sum(r => WordCount(r.Item2)).ShouldBe(WordCount(Words(70)) - 3);
    }

    [Fact]
    public void Each_Cjk_Element_Is_A_Word_And_The_Last_One_Waits_For_A_Follower()
    {
        var pacer = new WordPacer();
        pacer.Push("你好世界");
        pacer.PendingWords.ShouldBe(3);
        pacer.Tick(TimeSpan.Zero).ShouldBe("你好世");
        pacer.Tick(TimeSpan.FromMilliseconds(100)).ShouldBeNull();
        pacer.Push("，");
        pacer.PendingWords.ShouldBe(1);
        pacer.Tick(TimeSpan.FromMilliseconds(200)).ShouldBe("界");
    }

    [Fact]
    public void Leading_Whitespace_Goes_With_The_First_Word()
    {
        var pacer = new WordPacer();
        pacer.Push("\n\n  Start here ");
        pacer.Tick(TimeSpan.Zero).ShouldBe("\n\n  Start here ");
    }

    [Fact]
    public void Reset_Starts_A_New_Stream_That_Does_Not_Wait()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(20));
        pacer.Tick(TimeSpan.Zero);
        pacer.Complete();
        pacer.Reset();
        pacer.HasPending.ShouldBeFalse();
        pacer.IsCompleted.ShouldBeFalse();
        pacer.PendingWords.ShouldBe(0);
        pacer.Push("Fresh start ");
        pacer.Tick(TimeSpan.FromMilliseconds(1)).ShouldBe("Fresh start ");
    }

    [Fact]
    public void Text_Pushed_After_Complete_Is_Ignored()
    {
        var pacer = new WordPacer();
        pacer.Push("One ");
        pacer.Complete();
        pacer.Push("Two ");
        pacer.Tick(TimeSpan.Zero).ShouldBe("One ");
        pacer.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void A_Longer_Gap_Between_Frames_Releases_More_But_Never_Above_The_Batch_Limit()
    {
        var pacer = new WordPacer();
        pacer.Push(Words(40));
        pacer.Tick(TimeSpan.Zero);
        var batch = pacer.Tick(TimeSpan.FromSeconds(2));
        WordCount(batch!).ShouldBe(8);
    }

    [Fact]
    public void Invalid_Options_Are_Rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new WordPacer(new WordPacerOptions { WordsPerSecond = 0 }));
        Should.Throw<ArgumentOutOfRangeException>(() => new WordPacer(new WordPacerOptions { MaxWordsPerBatch = 0 }));
        Should.Throw<ArgumentOutOfRangeException>(() => new WordPacer(new WordPacerOptions { MinInterval = TimeSpan.FromMilliseconds(-1) }));
        Should.Throw<ArgumentOutOfRangeException>(() => new WordPacer(new WordPacerOptions { CompletionDrain = TimeSpan.Zero }));
    }
}

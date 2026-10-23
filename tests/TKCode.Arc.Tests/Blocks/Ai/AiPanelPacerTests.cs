using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The side panel's cadence: whole words, steps of 4–7 every 50ms, catching up, the held last word.</summary>
public class AiPanelPacerTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromTicks(166_667);

    private static AiPanelPacer Pacer() => new(TimeSpan.FromMilliseconds(50), 4, 7);

    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => "w" + i)) + " ";

    private static int Count(string? text) => text?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;

    [Fact]
    public void The_first_step_does_not_wait_and_shows_the_fewest_words()
    {
        var pacer = Pacer();
        pacer.Push(Words(20));
        Count(pacer.Step(TimeSpan.Zero)).ShouldBe(4);
    }

    [Fact]
    public void Steps_come_on_the_third_60Hz_frame_with_four_to_seven_words()
    {
        var pacer = Pacer();
        pacer.Push(Words(60));
        var now = TimeSpan.Zero;
        pacer.Step(now).ShouldNotBeNull();
        var steps = new List<int>();
        for (var frame = 1; frame <= 30; frame++)
        {
            now += Frame;
            if (pacer.Step(now) is { } step)
            {
                frame.ShouldBeOneOf(3, 6, 9, 12, 15, 18, 21, 24, 27, 30);
                steps.Add(Count(step));
            }
        }
        steps.Count.ShouldBe(10);
        steps.ShouldAllBe(n => n >= 4 && n <= 7);
        // 5.5 words a step on average.
        steps.Sum().ShouldBeInRange(54, 56);
    }

    [Fact]
    public void A_long_backlog_is_released_at_the_most_words_a_step()
    {
        var pacer = Pacer();
        pacer.CatchUpWords.ShouldBe(110);
        pacer.Push(Words(300));
        pacer.Step(TimeSpan.Zero);
        Count(pacer.Step(TimeSpan.FromMilliseconds(50))).ShouldBe(7);
    }

    [Fact]
    public void The_last_word_waits_for_what_follows_or_the_end()
    {
        var pacer = Pacer();
        pacer.Push("one two thr");
        pacer.Step(TimeSpan.Zero).ShouldBe("one two ");
        pacer.Step(TimeSpan.FromMilliseconds(100)).ShouldBeNull();
        pacer.Push("ee four");
        pacer.Step(TimeSpan.FromMilliseconds(150)).ShouldBe("three ");
        pacer.IsFinished.ShouldBeFalse();
        pacer.Complete();
        pacer.Step(TimeSpan.FromMilliseconds(200)).ShouldBe("four");
        pacer.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void Line_breaks_stay_with_the_words()
    {
        var pacer = Pacer();
        pacer.Push("First paragraph.\n\nSecond one here now.");
        pacer.Complete();
        var text = "";
        for (var t = 0; t < 10 && !pacer.IsFinished; t++)
        {
            text += pacer.Step(TimeSpan.FromMilliseconds(50 * t));
        }
        text.ShouldBe("First paragraph.\n\nSecond one here now.");
    }
}

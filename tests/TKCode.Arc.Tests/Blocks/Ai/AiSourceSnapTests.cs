using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiSourceSnap"/>: the narrow row's scroll arithmetic without a window: the snap target after a scroll, the
/// reveal of a lit card, and the card width.
/// </summary>
public class AiSourceSnapTests
{
    // Eight 248px cards 8px apart behind a 16px scroll padding: starts at 16, 272, 528, ...
    private static double[] Starts(int count = 8, double width = 248, double pad = 16) =>
        Enumerable.Range(0, count).Select(i => pad + i * (width + 8)).ToArray();

    private const double Pad = 16;

    // Content: 16 + 8 × 248 + 7 × 8 + 16 = 2072; viewport 390 → max 1682.
    private const double Max = 2072 - 390;

    [Fact]
    public void Snaps_To_The_Nearest_Card_Start()
    {
        AiSourceSnap.Target(0, Max, Starts(), Pad).ShouldBe(0);
        AiSourceSnap.Target(100, Max, Starts(), Pad).ShouldBe(0);
        AiSourceSnap.Target(150, Max, Starts(), Pad).ShouldBe(256);
        AiSourceSnap.Target(256, Max, Starts(), Pad).ShouldBe(256);
        AiSourceSnap.Target(700, Max, Starts(), Pad).ShouldBe(768);
    }

    [Fact]
    public void A_Tie_Goes_To_The_Earlier_Card()
    {
        AiSourceSnap.Target(128, Max, Starts(), Pad).ShouldBe(0);
    }

    [Fact]
    public void The_End_Of_The_Row_Is_A_Snap_Position()
    {
        // The last cards cannot reach the padding: past the last reachable start the row rests at its end.
        AiSourceSnap.Target(1650, Max, Starts(), Pad).ShouldBe(Max);
        AiSourceSnap.Target(5000, Max, Starts(), Pad).ShouldBe(Max);
        // Between the last reachable start (1536) and the end (1682) the nearer one wins.
        AiSourceSnap.Target(1560, Max, Starts(), Pad).ShouldBe(1536);
        AiSourceSnap.Target(1620, Max, Starts(), Pad).ShouldBe(Max);
    }

    [Fact]
    public void Without_Room_To_Scroll_It_Stays_At_Zero()
    {
        AiSourceSnap.Target(0, 0, Starts(1), Pad).ShouldBe(0);
        AiSourceSnap.Target(12, -5, Starts(1), Pad).ShouldBe(0);
        AiSourceSnap.Target(-20, Max, [], Pad).ShouldBe(0);
    }

    [Fact]
    public void A_Card_Already_In_View_Is_Not_Scrolled_To()
    {
        // Card 1 at rest: in view.
        AiSourceSnap.Reveal(0, 390, Max, 16, 264, Pad).ShouldBe(0);
        // Card 2 (272–520) does not fit a 390 viewport at 0: its start goes to the padding.
        AiSourceSnap.Reveal(0, 390, Max, 272, 520, Pad).ShouldBe(256);
        // Already there.
        AiSourceSnap.Reveal(256, 390, Max, 272, 520, Pad).ShouldBe(256);
    }

    [Fact]
    public void A_Card_Before_The_View_Comes_Back_To_The_Padding()
    {
        AiSourceSnap.Reveal(768, 390, Max, 272, 520, Pad).ShouldBe(256);
    }

    [Fact]
    public void A_Reveal_Stops_At_The_End_Of_The_Row()
    {
        var last = Starts()[7];
        AiSourceSnap.Reveal(0, 390, Max, last, last + 248, Pad).ShouldBe(Max);
    }

    [Theory]
    [InlineData(288, 218.88)]
    [InlineData(326.3, 247.988)]
    [InlineData(358, 248)]
    [InlineData(700, 248)]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    public void A_Narrow_Card_Is_The_Lesser_Of_248_And_76_Percent(double content, double expected)
    {
        AiSourceSnap.CardWidth(content).ShouldBe(expected, 0.001);
    }
}

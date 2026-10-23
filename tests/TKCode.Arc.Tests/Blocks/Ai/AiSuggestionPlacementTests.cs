using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The placement rule of the "/" and "@" list, as numbers.</summary>
public class AiSuggestionPlacementTests
{
    // A composer 695 wide whose top is 400px down a 560px window; the trigger at the start of the first line.
    private static readonly Rect s_lineStart = new(19, 19, 6, 24);

    [Fact]
    public void The_List_Hangs_Above_The_Line_From_7px_Left_Of_The_Trigger()
    {
        var result = AiSuggestionPlacement.Compute(695, s_lineStart, 400, 560, 300, 197.6);
        result.ShouldBe(new AiSuggestionPlacementResult(12, 300, true, double.PositiveInfinity));
        // A trigger further along the line moves the list with it.
        AiSuggestionPlacement.Compute(695, new Rect(87.5, 19, 9, 24), 400, 560, 300, 197.6).Left.ShouldBe(80.5);
        // The owner can set its own distance.
        AiSuggestionPlacement.Compute(695, s_lineStart, 400, 560, 300, 197.6, shift: -12).Left.ShouldBe(7);
    }

    [Fact]
    public void Near_The_Right_Edge_The_List_Is_Pushed_Back_To_Keep_6px()
    {
        var result = AiSuggestionPlacement.Compute(695, new Rect(600, 19, 9, 24), 400, 560, 300, 197.6);
        result.Left.ShouldBe(695 - 6 - 300);
        result.Width.ShouldBe(300);
        // Just inside the limit it is not moved.
        AiSuggestionPlacement.Compute(695, new Rect(396, 19, 9, 24), 400, 560, 300, 197.6).Left.ShouldBe(389);
    }

    [Fact]
    public void A_Narrow_Anchor_Narrows_The_List_To_Keep_6px_On_Both_Sides()
    {
        var result = AiSuggestionPlacement.Compute(286, new Rect(120, 19, 9, 24), 400, 560, 300, 197.6);
        result.Width.ShouldBe(274);
        result.Left.ShouldBe(6);
        // Never left of the anchor.
        AiSuggestionPlacement.Compute(695, new Rect(2, 19, 9, 24), 400, 560, 300, 197.6).Left.ShouldBe(0);
        AiSuggestionPlacement.Compute(8, new Rect(2, 19, 9, 24), 400, 560, 300, 197.6).ShouldBe(
            new AiSuggestionPlacementResult(0, 0, true, double.PositiveInfinity));
    }

    [Fact]
    public void It_Flips_Below_Only_When_There_Is_No_Room_Above()
    {
        // Line top at 40 + 19 = 59: room above is 59 − 6 − 6 = 47, not enough for 197.6.
        var below = AiSuggestionPlacement.Compute(695, s_lineStart, 40, 560, 300, 197.6);
        below.Above.ShouldBeFalse();
        below.MaxHeight.ShouldBe(double.PositiveInfinity);
        // A short list fits above even there.
        AiSuggestionPlacement.Compute(695, s_lineStart, 40, 560, 300, 47).Above.ShouldBeTrue();
        AiSuggestionPlacement.Compute(695, s_lineStart, 40, 560, 300, 47.4).Above.ShouldBeTrue();
        AiSuggestionPlacement.Compute(695, s_lineStart, 40, 560, 300, 48).Above.ShouldBeFalse();
        // Room above wins even when there is more room below.
        AiSuggestionPlacement.Compute(695, s_lineStart, 220, 900, 300, 197.6).Above.ShouldBeTrue();
    }

    [Fact]
    public void When_Neither_Side_Fits_It_Stays_Above_While_That_Is_Useful_And_Scrolls_In_What_Is_Left()
    {
        // Line 24 high at 100 + 19 in a 300px window: 107 above, 300 − 143 − 12 = 145 below.
        var result = AiSuggestionPlacement.Compute(695, s_lineStart, 100, 300, 300, 280);
        result.Above.ShouldBeFalse();
        result.MaxHeight.ShouldBe(145, 0.001);
        var up = AiSuggestionPlacement.Compute(695, s_lineStart, 160, 300, 300, 280);
        up.Above.ShouldBeTrue();
        up.MaxHeight.ShouldBe(167, 0.001);
        // Above keeps the list while it holds at least 150px, even with a little more room below (a centered composer).
        var centered = AiSuggestionPlacement.Compute(695, s_lineStart, 246, 560, 300, 280);
        centered.Above.ShouldBeTrue();
        centered.MaxHeight.ShouldBe(253, 0.001);
        AiSuggestionPlacement.Compute(695, s_lineStart, 140, 560, 300, 400).Above.ShouldBeFalse();
    }
}

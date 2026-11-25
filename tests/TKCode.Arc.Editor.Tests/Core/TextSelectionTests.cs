using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TextSelectionTests
{
    [Fact]
    public void Direction_Follows_Anchor_And_Cursor()
    {
        new TextSelection(1, 1, 2, 3).Direction.ShouldBe(SelectionDirection.LeftToRight);
        new TextSelection(2, 3, 1, 1).Direction.ShouldBe(SelectionDirection.RightToLeft);
        new TextSelection(1, 1, 1, 1).Direction.ShouldBe(SelectionDirection.LeftToRight);
    }

    [Fact]
    public void Range_Is_Ordered_Whatever_The_Direction()
    {
        var selection = new TextSelection(4, 7, 2, 3);
        TextRange range = selection;
        range.ShouldBe(new TextRange(2, 3, 4, 7));
        selection.Position.ShouldBe(new TextPosition(2, 3));
        selection.SelectionStart.ShouldBe(new TextPosition(4, 7));
    }

    [Fact]
    public void SetEndPosition_And_SetStartPosition_Keep_The_Direction()
    {
        new TextSelection(1, 1, 2, 3).SetEndPosition(5, 5).ShouldBe(new TextSelection(1, 1, 5, 5));
        new TextSelection(2, 3, 1, 1).SetEndPosition(5, 5).ShouldBe(new TextSelection(5, 5, 1, 1));
        new TextSelection(2, 2, 4, 4).SetStartPosition(1, 1).ShouldBe(new TextSelection(1, 1, 4, 4));
        new TextSelection(4, 4, 2, 2).SetStartPosition(1, 1).ShouldBe(new TextSelection(4, 4, 1, 1));
    }

    [Fact]
    public void FromRange_Uses_The_Direction()
    {
        var range = new TextRange(1, 2, 3, 4);
        TextSelection.FromRange(range).ShouldBe(new TextSelection(1, 2, 3, 4));
        TextSelection.FromRange(range, SelectionDirection.RightToLeft).ShouldBe(new TextSelection(3, 4, 1, 2));
    }

    [Fact]
    public void Position_Compares_By_Line_Then_Column()
    {
        (new TextPosition(1, 9) < new TextPosition(2, 1)).ShouldBeTrue();
        (new TextPosition(2, 1) < new TextPosition(2, 2)).ShouldBeTrue();
        new TextPosition(2, 2).IsBefore(new TextPosition(2, 2)).ShouldBeFalse();
        new TextPosition(2, 2).IsBeforeOrEqual(new TextPosition(2, 2)).ShouldBeTrue();
        new TextPosition(1, 1).Delta(-5, -5).ShouldBe(new TextPosition(1, 1));
        new TextPosition(3, 4).With(column: 9).ShouldBe(new TextPosition(3, 9));
    }
}

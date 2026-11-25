using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Ported from vscode's <c>src/vs/editor/test/common/core/range.test.ts</c>.</summary>
public sealed class TextRangeTests
{
    [Theory]
    [InlineData(1, 1, 1, 1, 1, 1, 1, 1, true)]
    [InlineData(1, 2, 1, 1, 1, 1, 1, 2, false)]
    [InlineData(2, 1, 1, 2, 1, 2, 2, 1, false)]
    [InlineData(1, 1, 1, 2, 1, 1, 1, 2, false)]
    [InlineData(1, 1, 2, 1, 1, 1, 2, 1, false)]
    public void Constructor_Orders_Start_Before_End(int sl, int sc, int el, int ec, int expectedSl, int expectedSc, int expectedEl, int expectedEc, bool empty)
    {
        var range = new TextRange(sl, sc, el, ec);
        range.StartLineNumber.ShouldBe(expectedSl);
        range.StartColumn.ShouldBe(expectedSc);
        range.EndLineNumber.ShouldBe(expectedEl);
        range.EndColumn.ShouldBe(expectedEc);
        range.IsEmpty.ShouldBe(empty);
    }

    [Theory]
    [InlineData(1, 1, 1, 3, 1, 2, 1, 4, -1)]
    [InlineData(1, 1, 1, 3, 1, 1, 1, 4, -1)]
    [InlineData(1, 2, 1, 3, 1, 1, 1, 4, -1)]
    [InlineData(1, 1, 1, 4, 1, 2, 1, 4, -1)]
    [InlineData(1, 1, 1, 4, 1, 1, 1, 4, 0)]
    [InlineData(1, 2, 1, 4, 1, 1, 1, 4, 1)]
    [InlineData(1, 1, 1, 5, 1, 2, 1, 4, 1)]
    [InlineData(1, 1, 2, 4, 1, 1, 1, 4, 1)]
    [InlineData(1, 2, 5, 1, 1, 1, 1, 4, 1)]
    public void CompareRangesUsingEnds(int asl, int asc, int ael, int aec, int bsl, int bsc, int bel, int bec, int sign)
    {
        var result = TextRange.CompareRangesUsingEnds(new TextRange(asl, asc, ael, aec), new TextRange(bsl, bsc, bel, bec));
        Math.Sign(result).ShouldBe(sign);
    }

    [Theory]
    [InlineData(1, 3, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 1, true)]
    [InlineData(5, 9, true)]
    [InlineData(5, 10, true)]
    [InlineData(5, 11, false)]
    [InlineData(6, 1, false)]
    public void ContainsPosition(int line, int column, bool expected)
    {
        new TextRange(2, 2, 5, 10).ContainsPosition(new TextPosition(line, column)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 3, 2, 2, false)]
    [InlineData(2, 1, 2, 2, false)]
    [InlineData(2, 2, 5, 11, false)]
    [InlineData(2, 2, 6, 1, false)]
    [InlineData(5, 9, 6, 1, false)]
    [InlineData(5, 10, 6, 1, false)]
    [InlineData(2, 2, 5, 10, true)]
    [InlineData(2, 3, 5, 9, true)]
    [InlineData(3, 100, 4, 100, true)]
    public void ContainsRange(int sl, int sc, int el, int ec, bool expected)
    {
        new TextRange(2, 2, 5, 10).ContainsRange(new TextRange(sl, sc, el, ec)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(2, 2, 3, 2, 4, 2, 5, 2, false)]
    [InlineData(4, 2, 5, 2, 2, 2, 3, 2, false)]
    [InlineData(4, 2, 5, 2, 5, 2, 6, 2, false)]
    [InlineData(5, 2, 6, 2, 4, 2, 5, 2, false)]
    [InlineData(2, 2, 2, 7, 2, 4, 2, 6, true)]
    [InlineData(2, 2, 2, 7, 2, 4, 2, 9, true)]
    [InlineData(2, 4, 2, 9, 2, 2, 2, 7, true)]
    public void AreIntersecting(int asl, int asc, int ael, int aec, int bsl, int bsc, int bel, int bec, bool expected)
    {
        TextRange.AreIntersecting(new TextRange(asl, asc, ael, aec), new TextRange(bsl, bsc, bel, bec)).ShouldBe(expected);
    }

    [Fact]
    public void IntersectRanges_Returns_Null_When_Apart_And_Empty_When_Touching()
    {
        new TextRange(1, 1, 1, 5).IntersectRanges(new TextRange(2, 1, 2, 5)).ShouldBeNull();
        new TextRange(1, 1, 1, 5).IntersectRanges(new TextRange(1, 5, 1, 9)).ShouldBe(new TextRange(1, 5, 1, 5));
        new TextRange(1, 1, 3, 5).IntersectRanges(new TextRange(2, 2, 4, 1)).ShouldBe(new TextRange(2, 2, 3, 5));
    }

    [Fact]
    public void PlusRange_Covers_Both()
    {
        new TextRange(2, 4, 3, 1).PlusRange(new TextRange(1, 9, 2, 6)).ShouldBe(new TextRange(1, 9, 3, 1));
        new TextRange(2, 4, 2, 6).PlusRange(new TextRange(2, 1, 2, 9)).ShouldBe(new TextRange(2, 1, 2, 9));
    }
}

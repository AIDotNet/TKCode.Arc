// Tests for ArraysFind (findFirstMax, findLastMax, findFirstMin) and Arrays.CompareBy; upstream has no tests for
// them (src/vs/base/test/common/arraysFind.test.ts covers the monotonous searches), so these are not upstream's.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ArraysFindTests
{
    private static readonly Func<(int Key, string Name), (int Key, string Name), int> s_byKey =
        Arrays.CompareBy<(int Key, string Name), int>(x => x.Key, (a, b) => a.CompareTo(b));

    private static readonly (int Key, string Name)[] s_items = [(2, "a"), (5, "b"), (1, "c"), (5, "d"), (1, "e")];

    [Fact]
    public void FindFirstMax_Returns_The_First_Of_Equal_Maxima() => ArraysFind.FindFirstMax(s_items, s_byKey).Name.ShouldBe("b");

    [Fact]
    public void FindLastMax_Returns_The_Last_Of_Equal_Maxima() => ArraysFind.FindLastMax(s_items, s_byKey).Name.ShouldBe("d");

    [Fact]
    public void FindFirstMin_Returns_The_First_Of_Equal_Minima() => ArraysFind.FindFirstMin(s_items, s_byKey).Name.ShouldBe("c");

    [Fact]
    public void An_Empty_List_Gives_The_Default()
    {
        ArraysFind.FindFirstMax(Array.Empty<string>(), string.CompareOrdinal).ShouldBeNull();
        ArraysFind.FindLastMax(Array.Empty<string>(), string.CompareOrdinal).ShouldBeNull();
        ArraysFind.FindFirstMin(Array.Empty<string>(), string.CompareOrdinal).ShouldBeNull();
    }
}

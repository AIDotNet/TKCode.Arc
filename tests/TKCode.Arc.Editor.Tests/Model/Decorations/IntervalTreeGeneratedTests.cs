// Ported from vscode's src/vs/editor/test/common/model/intervalTree.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The generated operation scripts and the Cormen searching examples.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class IntervalTreeGeneratedTests
{
    [Fact]
    public void Gen01() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(28, 35),
        TestOperation.Insert(52, 54),
        TestOperation.Insert(63, 69),
    });

    [Fact]
    public void Gen02() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(80, 89),
        TestOperation.Insert(92, 100),
        TestOperation.Insert(99, 99),
    });

    [Fact]
    public void Gen03() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(89, 96),
        TestOperation.Insert(71, 74),
        TestOperation.Delete(1),
    });

    [Fact]
    public void Gen04() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(44, 46),
        TestOperation.Insert(85, 88),
        TestOperation.Delete(0),
    });

    [Fact]
    public void Gen05() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(82, 90),
        TestOperation.Insert(69, 73),
        TestOperation.Delete(0),
        TestOperation.Delete(1),
    });

    [Fact]
    public void Gen06() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(41, 63),
        TestOperation.Insert(98, 98),
        TestOperation.Insert(47, 51),
        TestOperation.Delete(2),
    });

    [Fact]
    public void Gen07() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(24, 26),
        TestOperation.Insert(11, 28),
        TestOperation.Insert(27, 30),
        TestOperation.Insert(80, 85),
        TestOperation.Delete(1),
    });

    [Fact]
    public void Gen08() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(100, 100),
        TestOperation.Insert(100, 100),
    });

    [Fact]
    public void Gen09() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(58, 65),
        TestOperation.Insert(82, 96),
        TestOperation.Insert(58, 65),
    });

    [Fact]
    public void Gen10() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(32, 40),
        TestOperation.Insert(25, 29),
        TestOperation.Insert(24, 32),
    });

    [Fact]
    public void Gen11() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(25, 70),
        TestOperation.Insert(99, 100),
        TestOperation.Insert(46, 51),
        TestOperation.Insert(57, 57),
        TestOperation.Delete(2),
    });

    [Fact]
    public void Gen12() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(20, 26),
        TestOperation.Insert(10, 18),
        TestOperation.Insert(99, 99),
        TestOperation.Insert(37, 59),
        TestOperation.Delete(2),
    });

    [Fact]
    public void Gen13() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(3, 91),
        TestOperation.Insert(57, 57),
        TestOperation.Insert(35, 44),
        TestOperation.Insert(72, 81),
        TestOperation.Delete(2),
    });

    [Fact]
    public void Gen14() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(58, 61),
        TestOperation.Insert(34, 35),
        TestOperation.Insert(56, 62),
        TestOperation.Insert(69, 78),
        TestOperation.Delete(0),
    });

    [Fact]
    public void Gen15() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(63, 69),
        TestOperation.Insert(17, 24),
        TestOperation.Insert(3, 13),
        TestOperation.Insert(84, 94),
        TestOperation.Insert(18, 23),
        TestOperation.Insert(96, 98),
        TestOperation.Delete(1),
    });

    [Fact]
    public void Gen16() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(27, 27),
        TestOperation.Insert(42, 87),
        TestOperation.Insert(42, 49),
        TestOperation.Insert(69, 71),
        TestOperation.Insert(20, 27),
        TestOperation.Insert(8, 9),
        TestOperation.Insert(42, 49),
        TestOperation.Delete(1),
    });

    [Fact]
    public void Gen17() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(21, 23),
        TestOperation.Insert(83, 87),
        TestOperation.Insert(56, 58),
        TestOperation.Insert(1, 55),
        TestOperation.Insert(56, 59),
        TestOperation.Insert(58, 60),
        TestOperation.Insert(56, 65),
        TestOperation.Delete(1),
        TestOperation.Delete(0),
        TestOperation.Delete(6),
    });

    [Fact]
    public void Gen18() => IntervalTreeTestRunner.TestIntervalTree(new[]
    {
        TestOperation.Insert(25, 25),
        TestOperation.Insert(67, 79),
        TestOperation.Delete(0),
        TestOperation.Search(65, 75),
    });

    [Fact]
    public void ForceDeltaOverflow()
    {
        // The upstream values are ~1e14, which do not fit in an int. They are scaled down by 1e6; their order
        // and the small interval widths are kept. The upstream script only inserts, so no delta ever grows here;
        // it exercises the augmented tree with large coordinates, as upstream does with the constructor's
        // FORCE_OVERFLOWING_TEST line commented out.
        IntervalTreeTestRunner.TestIntervalTree(new[]
        {
            TestOperation.Insert(570_298_874, 851_871_181),
            TestOperation.Insert(0, 3),
            TestOperation.Insert(2_094_038_139, 2_094_038_146),
            TestOperation.Insert(44_293_889, 44_293_897),
            TestOperation.Insert(1_257_196_982, 1_257_196_989),
            TestOperation.Insert(1_819_005_788, 1_819_005_798),
        });
    }
}

public sealed class IntervalTreeSearchTests
{
    private static IntervalTree CreateCormenTree()
    {
        IntervalTree r = new IntervalTree();
        int[][] data =
        {
            new[] { 16, 21 },
            new[] { 8, 9 },
            new[] { 25, 30 },
            new[] { 5, 8 },
            new[] { 15, 23 },
            new[] { 17, 19 },
            new[] { 26, 26 },
            new[] { 0, 3 },
            new[] { 6, 10 },
            new[] { 19, 20 },
        };
        for (int i = 0, len = data.Length; i < len; i++)
        {
            IntervalNode node = new IntervalNode(null, data[i][0], data[i][1]);
            r.Insert(node);
        }
        return r;
    }

    private static void AssertIntervalSearch(IntervalTree T, int start, int end, TestInterval[] expected)
    {
        List<IntervalNode> actualNodes = T.IntervalSearch(start, end, 0, false, false, 0, false);
        List<TestInterval> actual = new List<TestInterval>(actualNodes.Count);
        for (int i = 0, len = actualNodes.Count; i < len; i++)
        {
            actual.Add(new TestInterval(actualNodes[i].CachedAbsoluteStart, actualNodes[i].CachedAbsoluteEnd));
        }
        actual.ShouldBe(expected);
    }

    [Fact]
    public void Cormen_1_To_2()
    {
        AssertIntervalSearch(CreateCormenTree(), 1, 2, new[]
        {
            new TestInterval(0, 3),
        });
    }

    [Fact]
    public void Cormen_4_To_8()
    {
        AssertIntervalSearch(CreateCormenTree(), 4, 8, new[]
        {
            new TestInterval(5, 8),
            new TestInterval(6, 10),
            new TestInterval(8, 9),
        });
    }

    [Fact]
    public void Cormen_10_To_15()
    {
        AssertIntervalSearch(CreateCormenTree(), 10, 15, new[]
        {
            new TestInterval(6, 10),
            new TestInterval(15, 23),
        });
    }

    [Fact]
    public void Cormen_21_To_25()
    {
        AssertIntervalSearch(CreateCormenTree(), 21, 25, new[]
        {
            new TestInterval(15, 23),
            new TestInterval(16, 21),
            new TestInterval(25, 30),
        });
    }

    [Fact]
    public void Cormen_24_To_24()
    {
        AssertIntervalSearch(CreateCormenTree(), 24, 24, Array.Empty<TestInterval>());
    }
}

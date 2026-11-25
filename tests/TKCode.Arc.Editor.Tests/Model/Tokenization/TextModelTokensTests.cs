// Ported from vscode (src/vs/editor/test/common/model/textModelTokens.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class RangePriorityQueueImplTests
{
    [Fact]
    public void AddRange()
    {
        var ranges = new List<OffsetRange>();

        OffsetRange.AddRange(new OffsetRange(0, 2), ranges);
        OffsetRange.AddRange(new OffsetRange(10, 13), ranges);
        OffsetRange.AddRange(new OffsetRange(20, 24), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[0, 2)", "[10, 13)", "[20, 24)"]);

        OffsetRange.AddRange(new OffsetRange(2, 10), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[0, 13)", "[20, 24)"]);

        OffsetRange.AddRange(new OffsetRange(14, 19), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[0, 13)", "[14, 19)", "[20, 24)"]);

        OffsetRange.AddRange(new OffsetRange(10, 22), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[0, 24)"]);

        OffsetRange.AddRange(new OffsetRange(-1, 29), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[-1, 29)"]);

        OffsetRange.AddRange(new OffsetRange(-10, -5), ranges);

        ranges.Select(r => r.ToString()).ShouldBe(["[-10, -5)", "[-1, 29)"]);
    }

    [Fact]
    public void AddRangeAndResize()
    {
        var queue = new RangePriorityQueueImpl();

        queue.AddRange(new OffsetRange(0, 20));
        queue.AddRange(new OffsetRange(100, 120));
        queue.AddRange(new OffsetRange(200, 220));

        // disjoint
        queue.AddRangeAndResize(new OffsetRange(25, 27), 0);

        queue.GetRanges().Select(r => r.ToString()).ShouldBe(["[0, 20)", "[98, 118)", "[198, 218)"]);

        queue.AddRangeAndResize(new OffsetRange(19, 20), 0);

        queue.GetRanges().Select(r => r.ToString()).ShouldBe(["[0, 19)", "[97, 117)", "[197, 217)"]);

        queue.AddRangeAndResize(new OffsetRange(19, 97), 0);

        queue.GetRanges().Select(r => r.ToString()).ShouldBe(["[0, 39)", "[119, 139)"]);

        queue.AddRangeAndResize(new OffsetRange(-1000, 1000), 0);

        queue.GetRanges().ShouldBeEmpty();
    }
}

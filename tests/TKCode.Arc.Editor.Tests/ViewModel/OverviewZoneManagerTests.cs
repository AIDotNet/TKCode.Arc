// Ported from vscode (src/vs/editor/test/common/view/overviewZoneManager.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

/// <summary>Suite 'Editor View - OverviewZoneManager'.</summary>
public sealed class OverviewZoneManagerTests
{
    private const int LineCount = 50;
    private const int LineHeight = 20;

    private static OverviewZoneManager Create(double domHeight, double pixelRatio)
    {
        var manager = new OverviewZoneManager(lineNumber => LineHeight * lineNumber);
        manager.SetDOMWidth(30);
        manager.SetDOMHeight(domHeight);
        manager.SetOuterHeight(LineCount * LineHeight);
        manager.SetLineHeight(LineHeight);
        manager.SetPixelRatio(pixelRatio);

        manager.SetZones(
        [
            new OverviewRulerZone(1, 1, 0, "1"),
            new OverviewRulerZone(10, 10, 0, "2"),
            new OverviewRulerZone(30, 31, 0, "3"),
            new OverviewRulerZone(50, 50, 0, "4"),
        ]);
        return manager;
    }

    [Fact]
    public void Pixel_Ratio_1_Dom_Height_600()
    {
        var manager = Create(600, 1);

        // one line = 12, but cap is at 6
        manager.ResolveColorZones().ShouldBe(
        [
            new ColorZone(12, 24, 1),
            new ColorZone(120, 132, 2), // 120 -> 132
            new ColorZone(360, 384, 3), // 360 -> 372 [360 -> 384]
            new ColorZone(588, 600, 4), // 588 -> 600
        ]);
    }

    [Fact]
    public void Pixel_Ratio_1_Dom_Height_300()
    {
        var manager = Create(300, 1);

        // one line = 6, cap is at 6
        manager.ResolveColorZones().ShouldBe(
        [
            new ColorZone(6, 12, 1),
            new ColorZone(60, 66, 2), // 60 -> 66
            new ColorZone(180, 192, 3), // 180 -> 192
            new ColorZone(294, 300, 4), // 294 -> 300
        ]);
    }

    [Fact]
    public void Pixel_Ratio_2_Dom_Height_300()
    {
        var manager = Create(300, 2);

        // one line = 6, cap is at 12
        manager.ResolveColorZones().ShouldBe(
        [
            new ColorZone(12, 24, 1),
            new ColorZone(120, 132, 2), // 120 -> 132
            new ColorZone(360, 384, 3), // 360 -> 384
            new ColorZone(588, 600, 4), // 588 -> 600
        ]);
    }

    [Fact]
    public void Resolved_Zones_Are_Reused_Until_The_Size_Changes()
    {
        var manager = Create(300, 1);
        var first = manager.ResolveColorZones();
        manager.ResolveColorZones()[0].ShouldBeSameAs(first[0]);
        manager.SetDOMHeight(600).ShouldBeTrue();
        manager.ResolveColorZones()[0].ShouldBe(new ColorZone(12, 24, 1));
        manager.GetId2Color().ShouldBe([null, "1", "2", "3", "4"]);
    }
}

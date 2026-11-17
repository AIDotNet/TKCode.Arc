namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceSeed"/> (mulberry32) and <see cref="ShowpieceFormat"/>.</summary>
public class ShowpieceSeedFormatTests
{
    [Fact]
    public void Mulberry32_Matches_The_JavaScript_Sequence()
    {
        // Reference values from the JavaScript original (Math.imul, >>>) run in Node.
        var one = new ShowpieceSeed(1);
        one.NextDouble().ShouldBe(0.6270739405881613, 1e-15);
        one.NextDouble().ShouldBe(0.002735721180215478, 1e-15);
        one.NextDouble().ShouldBe(0.5274470399599522, 1e-15);
        one.NextDouble().ShouldBe(0.9810509674716741, 1e-15);
        one.NextDouble().ShouldBe(0.9683778982143849, 1e-15);

        var galaxy = new ShowpieceSeed(1729);
        galaxy.NextUInt().ShouldBe(407404394u);
        galaxy.NextUInt().ShouldBe(260702121u);
        galaxy.NextUInt().ShouldBe(2440835833u);

        var zoom = new ShowpieceSeed(20261022);
        zoom.NextUInt().ShouldBe(2401570752u);
        zoom.NextUInt().ShouldBe(939868294u);
        zoom.NextDouble().ShouldBe(0.5584872781764716, 1e-15);
    }

    [Fact]
    public void Derived_Draws_Are_Deterministic_And_In_Range()
    {
        var a = new ShowpieceSeed(1729);
        var b = new ShowpieceSeed(1729);
        for (var i = 0; i < 200; i++)
        {
            a.NextGaussian().ShouldBe(b.NextGaussian());
            var n = a.Next(7);
            n.ShouldBe(b.Next(7));
            n.ShouldBeInRange(0, 6);
            var t = a.NextTriangular(4, 7, 10);
            t.ShouldBe(b.NextTriangular(4, 7, 10));
            t.ShouldBeInRange(4, 10);
            a.NextLogNormal(120, 0.55).ShouldBeGreaterThan(0);
            b.NextLogNormal(120, 0.55);
        }
        // Poisson by inversion: the mean of many draws is close to λ.
        var seed = new ShowpieceSeed(20261022);
        var sum = 0;
        for (var i = 0; i < 2000; i++)
        {
            sum += seed.NextPoisson(62);
        }
        (sum / 2000.0).ShouldBe(62, 1);
        // Shuffle keeps every element.
        Span<int> items = [0, 1, 2, 3, 4, 5, 6, 7];
        new ShowpieceSeed(1).Shuffle(items);
        items.ToArray().OrderBy(x => x).ShouldBe(new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
        // Weighted picks never choose a zero weight.
        var weighted = new ShowpieceSeed(3);
        for (var i = 0; i < 100; i++)
        {
            weighted.PickWeighted([0, 2, 0, 1]).ShouldBeOneOf(1, 3);
        }
    }

    [Fact]
    public void Compact_Currency_Follows_Customer_Galaxy()
    {
        ShowpieceFormat.CompactCurrency(6_050_000).ShouldBe("$6.05M");
        ShowpieceFormat.CompactCurrency(870_000).ShouldBe("$870K");
        ShowpieceFormat.CompactCurrency(2_000_000).ShouldBe("$2.00M");
        ShowpieceFormat.CompactCurrency(3_970_000).ShouldBe("$3.97M");
        ShowpieceFormat.CompactCurrency(12_345).ShouldBe("$12K");
        ShowpieceFormat.CompactCurrency(990).ShouldBe("$990");
        ShowpieceFormat.CompactCurrency(0).ShouldBe("$0");
        // Rounding never shows 1000K or 1000.00M.
        ShowpieceFormat.CompactCurrency(999_600).ShouldBe("$1.00M");
        ShowpieceFormat.CompactCurrency(999.6).ShouldBe("$1K");
        ShowpieceFormat.CompactCurrency(1_200_000_000).ShouldBe("$1.20B");
        ShowpieceFormat.CompactCurrency(-1_200_000).ShouldBe("-$1.20M");
        ShowpieceFormat.CompactCurrency(double.NaN).ShouldBe("—");
        ShowpieceFormat.CompactCurrency(870_000, "€").ShouldBe("€870K");

        var parts = ShowpieceFormat.CompactParts(6_050_000);
        parts.Scaled.ShouldBe(6.05, 1e-12);
        parts.Decimals.ShouldBe(2);
        parts.Unit.ShouldBe("M");
        ShowpieceFormat.CompactParts(870_000).ShouldBe(new ShowpieceCompactParts(870, 0, "K"));
    }

    [Fact]
    public void Grouping_Percent_And_Readout()
    {
        ShowpieceFormat.Grouped(1687).ShouldBe("1,687");
        ShowpieceFormat.Grouped(1687.26, 1).ShouldBe("1,687.3");
        ShowpieceFormat.Currency(2_512_340).ShouldBe("$2,512,340");
        ShowpieceFormat.Percent(0.69).ShouldBe("69%");
        ShowpieceFormat.Percent(0.123).ShouldBe("12%");
        ShowpieceFormat.Percent(0.086, 1, signed: true).ShouldBe("+8.6%");
        ShowpieceFormat.Percent(-0.032, 1, signed: true).ShouldBe("-3.2%");
        ShowpieceFormat.Percent(0.0001, 1, signed: true).ShouldBe("0.0%");
        // Zoom readout: one decimal under 10, whole numbers from 10.
        ShowpieceFormat.ZoomReadout(1).ShouldBe("1.0×");
        ShowpieceFormat.ZoomReadout(2.4).ShouldBe("2.4×");
        ShowpieceFormat.ZoomReadout(9.94).ShouldBe("9.9×");
        ShowpieceFormat.ZoomReadout(10).ShouldBe("10×");
        ShowpieceFormat.ZoomReadout(53.04).ShouldBe("53×");
    }
}

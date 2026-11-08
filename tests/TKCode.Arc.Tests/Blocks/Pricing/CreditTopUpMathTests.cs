using System.Globalization;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// CreditTopUp arithmetic against the reference observations (notes §2b, Arc's pricing $0.01 / +10% from 5,000 / +15%
/// from 20,000) and against our sample pricing ($0.008 / +10% from 6,000 / +15% from 24,000).
/// </summary>
public class CreditTopUpMathTests
{
    private static readonly CreditTopUpPricing ArcLike = new()
    {
        UnitPrice = 0.01m,
        Currency = "USD",
        BonusTiers = [new CreditTopUpBonusTier(5_000, 10), new CreditTopUpBonusTier(20_000, 15)]
    };

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    [Theory]
    [InlineData(5_000, 500, 5_500, 50.00)]
    [InlineData(5_500, 550, 6_050, 55.00)]
    [InlineData(20_500, 3_075, 23_575, 205.00)]
    [InlineData(30_000, 4_500, 34_500, 300.00)]
    [InlineData(500, 0, 500, 5.00)]
    [InlineData(4_500, 0, 4_500, 45.00)]
    [InlineData(19_500, 1_950, 21_450, 195.00)]
    [InlineData(21_000, 3_150, 24_150, 210.00)]
    [InlineData(27_000, 4_050, 31_050, 270.00)]
    public void Quote_Matches_The_Reference(long credits, long bonus, long total, double price)
    {
        var quote = CreditTopUp.Quote(credits, ArcLike);
        quote.Bonus.ShouldBe(bonus);
        quote.Total.ShouldBe(total);
        quote.Price.ShouldBe((decimal)price);
        quote.Currency.ShouldBe("USD");
    }

    [Fact]
    public void Sample_Pricing_Prices_The_Sample_Packs()
    {
        var pricing = CreditTopUpPricing.Default;
        CreditTopUp.Quote(1_500, pricing).ShouldBe(new CreditTopUpQuote(1_500, 0, 1_500, 12m, "USD"));
        CreditTopUp.Quote(6_000, pricing).ShouldBe(new CreditTopUpQuote(6_000, 600, 6_600, 48m, "USD"));
        CreditTopUp.Quote(24_000, pricing).ShouldBe(new CreditTopUpQuote(24_000, 3_600, 27_600, 192m, "USD"));
        CreditTopUpMath.RatePerUnit(pricing).ShouldBe(8m);
        CreditTopUpMath.FirstBonusAt(pricing.BonusTiers).ShouldBe(6_000);
    }

    [Fact]
    public void Bonus_Is_The_Highest_Tier_At_Or_Below()
    {
        CreditTopUpMath.BonusPercent(19_500, ArcLike.BonusTiers).ShouldBe(10);
        CreditTopUpMath.BonusPercent(20_000, ArcLike.BonusTiers).ShouldBe(15);
        CreditTopUpMath.BonusPercent(4_999, ArcLike.BonusTiers).ShouldBe(0);
        CreditTopUpMath.BonusPercent(1_000, null).ShouldBe(0);
    }

    [Fact]
    public void Rate_Per_Thousand_Does_Not_Follow_The_Bonus()
    {
        CreditTopUpMath.RatePerUnit(ArcLike).ShouldBe(10m);
        CreditTopUpMoney.Rate(10m, "USD", EnUs).ShouldBe("$10");
    }

    [Fact]
    public void Average_Uses_Full_Days_Only_And_Days_Left_Rounds_Down()
    {
        var average = CreditTopUpMath.AverageDaily(CreditTopUpSamples.Usage())!.Value;
        average.ShouldBe(5_726 / 13.0, 1e-9);
        CreditTopUpMath.DaysLeft(1_260, average).ShouldBe(2);

        // The reference: 13 full days summing to 5,036 (387.38 a day).
        var arc = 5_036 / 13.0;
        CreditTopUpMath.DaysLeft(840, arc).ShouldBe(2);
        CreditTopUpMath.DaysLeft(1_840, arc).ShouldBe(4);
        CreditTopUpMath.DaysLeft(23_840, arc).ShouldBe(61);
        CreditTopUpMath.DaysLeft(10_890, arc).ShouldBe(28);
        CreditTopUpMath.DaysLeft(6_340, arc).ShouldBe(16);
        CreditTopUpMath.DaysLeft(4_840, arc).ShouldBe(12);

        CreditTopUpMath.AverageDaily([new CreditTopUpUsageDay(new DateOnly(2026, 1, 1), 300)]).ShouldBeNull();
        CreditTopUpMath.DaysLeft(1_000, null).ShouldBeNull();
    }

    [Fact]
    public void Health_Is_Balance_Below_Threshold()
    {
        CreditTopUpMath.Health(840, 1_000).ShouldBe(CreditTopUpHealth.RunningLow);
        CreditTopUpMath.Health(840, 500).ShouldBe(CreditTopUpHealth.Healthy);
        CreditTopUpMath.Health(1_840, 1_000).ShouldBe(CreditTopUpHealth.Healthy);
        CreditTopUpMath.Health(1_000, 1_000).ShouldBe(CreditTopUpHealth.Healthy);
    }

    [Fact]
    public void Bar_Height_Is_Value_Over_Max_Times_Plot()
    {
        CreditTopUpMath.BarHeight(356, 489, 44).ShouldBe(32.03, 0.01);
        CreditTopUpMath.BarHeight(214, 489, 44).ShouldBe(19.25, 0.01);
        CreditTopUpMath.BarHeight(318, 489, 44).ShouldBe(28.61, 0.01);
        CreditTopUpMath.BarHeight(489, 489, 44).ShouldBe(44);
        CreditTopUpMath.BarHeight(489, 489, 32).ShouldBe(32);
        CreditTopUpMath.BarHeight(10, 0, 44).ShouldBe(0);
    }

    [Fact]
    public void Slider_Steps()
    {
        var range = CreditTopUpRange.Default;
        CreditTopUpMath.PageStep(range).ShouldBe(3_000);
        CreditTopUpMath.Snap(6_250, range).ShouldBe(6_500);
        CreditTopUpMath.Snap(6_240, range).ShouldBe(6_000);
        CreditTopUpMath.Snap(100, range).ShouldBe(500);
        CreditTopUpMath.Snap(40_000, range).ShouldBe(30_000);
        CreditTopUpMath.Wrap(2, 1, 3).ShouldBe(0);
        CreditTopUpMath.Wrap(0, -1, 3).ShouldBe(2);
    }

    [Fact]
    public void Odometer_Laps_Every_Place_Below_The_Highest_Change()
    {
        // 840 → 1,840: no shared place changes, a new leading place appears: every shared place laps.
        CreditTopUpMath.PlanTurns(CreditTopUpMath.Digits(840), CreditTopUpMath.Digits(1_840), true, true)
            .ShouldBe([10, 10, 10]);
        // 4,840 → 10,890: thousands 4 → 0 takes the short count (+6); hundreds +10, tens 4 → 9 +15, ones +10.
        CreditTopUpMath.PlanTurns(CreditTopUpMath.Digits(4_840), CreditTopUpMath.Digits(10_890), true, true)
            .ShouldBe([10, 15, 10, 6]);
        // 840 → 23,840.
        CreditTopUpMath.PlanTurns(CreditTopUpMath.Digits(840), CreditTopUpMath.Digits(23_840), true, true)
            .ShouldBe([10, 10, 10]);
        // Without laps (checkout figures): 5,500 → 6,050 rolls 6 → 0 (+5), 0 → 5 (+5), 5 → 6 (+1).
        CreditTopUpMath.PlanTurns(CreditTopUpMath.Digits(5_500), CreditTopUpMath.Digits(6_050), true, false)
            .ShouldBe([0, 5, 5, 1]);
        // Shrinking turns back, no laps: 6,050 → 5,500.
        CreditTopUpMath.PlanTurns(CreditTopUpMath.Digits(6_050), CreditTopUpMath.Digits(5_500), false, true)
            .ShouldBe([0, -5, -5, -1]);
    }

    [Fact]
    public void Odometer_Layout_Keys_Places()
    {
        var layout = CreditTopUpOdometer.Layout(1_840, 0, EnUs);
        layout.Select(c => c.Key).ShouldBe(["i3", "g3", "i2", "i1", "i0"]);
        layout[1].Text.ShouldBe(",");
        var price = CreditTopUpOdometer.Layout(4_800, 2, DeDe);
        price.Select(c => c.Key).ShouldBe(["i1", "i0", "d", "f0", "f1"]);
        price[2].Text.ShouldBe(",");
    }

    /// <summary>ICU writes a no-break space where other globalization modes write a space.</summary>
    private static string N(string text) => text.Replace('\u00A0', ' ');

    [Fact]
    public void Money_Follows_The_Culture()
    {
        CreditTopUpMoney.Format(48m, "USD", EnUs).ShouldBe("$48.00");
        CreditTopUpMoney.FormatShort(48m, "USD", EnUs).ShouldBe("$48");
        CreditTopUpMoney.FormatShort(12.5m, "USD", EnUs).ShouldBe("$12.50");
        N(CreditTopUpMoney.Format(48m, "EUR", DeDe)).ShouldBe("48,00 €");
        N(CreditTopUpMoney.Rate(8m, "EUR", DeDe)).ShouldBe("8 €");
        CreditTopUpMoney.Format(1200m, "JPY", EnUs).ShouldBe("¥1,200");
        CreditTopUpMoney.ShapeOf("USD", EnUs).ShouldBe(new CreditTopUpMoney.Shape("$", string.Empty, 2));
        var euro = CreditTopUpMoney.ShapeOf("EUR", DeDe);
        (euro.Prefix, N(euro.Suffix), euro.Decimals).ShouldBe((string.Empty, " €", 2));
        CreditTopUpMoney.Count(6_600, DeDe).ShouldBe("6.600");
    }
}

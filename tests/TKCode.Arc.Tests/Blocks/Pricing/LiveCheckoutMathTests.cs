using System.Globalization;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// LiveCheckout's pure logic against the observation record (checkout-live notes 2b.2 and 4.4) and the invented sample
/// (notes section 11): seat prices, lines, discount, tax, totals, tax names, field rules, card shaping and seat parsing.
/// </summary>
public class LiveCheckoutMathTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(12, 9.60)]
    [InlineData(24, 19.20)]
    [InlineData(42, 33.60)]
    [InlineData(28, 22.40)]
    public void YearlySeatPriceTakesTheDiscountOffAndRoundsToCents(decimal monthly, decimal expected)
    {
        Assert.Equal(expected, LiveCheckoutMath.SeatPrice(monthly, LiveCheckoutBilling.Yearly, 0.2m));
        Assert.Equal(monthly, LiveCheckoutMath.SeatPrice(monthly, LiveCheckoutBilling.Monthly, 0.2m));
    }

    [Fact]
    public void PlanAndAddonRowsFollowTheObservedFormulas()
    {
        Assert.Equal(1843.20m, LiveCheckoutMath.PlanAmount(24, 8, LiveCheckoutBilling.Yearly, 0.2m));
        Assert.Equal(2073.60m, LiveCheckoutMath.PlanAmount(24, 9, LiveCheckoutBilling.Yearly, 0.2m));
        Assert.Equal(96m, LiveCheckoutMath.PlanAmount(12, 8, LiveCheckoutBilling.Monthly, 0.2m));
        Assert.Equal(470.40m, LiveCheckoutMath.AddonAmount(49, LiveCheckoutBilling.Yearly, 0.2m));
        Assert.Equal(49m, LiveCheckoutMath.AddonAmount(49, LiveCheckoutBilling.Monthly, 0.2m));
        Assert.Equal(99m, LiveCheckoutMath.AddonAmount(99, LiveCheckoutBilling.Monthly, 0.2m));
    }

    [Theory]
    [InlineData(0, 0.19, 0, 439.58, 2753.18)]
    [InlineData(20, 0.19, 462.72, 351.67, 2202.55)]
    [InlineData(10, 0.19, 231.36, 395.63, 2477.87)]
    [InlineData(0, 0, 0, 0, 2313.60)]
    [InlineData(0, 0.20, 0, 462.72, 2776.32)]
    [InlineData(0, 0.081, 0, 187.40, 2501.00)]
    [InlineData(0, 0.13, 0, 300.77, 2614.37)]
    [InlineData(0, 0.10, 0, 231.36, 2544.96)]
    public void TotalsMatchTheObservedAmounts(decimal percent, decimal rate, decimal discount, decimal tax, decimal total)
    {
        var totals = LiveCheckoutMath.Totals([1843.20m, 470.40m], percent, rate);
        Assert.Equal(2313.60m, totals.Subtotal);
        Assert.Equal(discount, totals.Discount);
        Assert.Equal(tax, totals.Tax);
        Assert.Equal(total, totals.Total);
    }

    [Fact]
    public void MonthlyStarterTotalsMatch()
    {
        var totals = LiveCheckoutMath.Totals([96m, 49m], 0, 0.19m);
        Assert.Equal(145m, totals.Subtotal);
        Assert.Equal(27.55m, totals.Tax);
        Assert.Equal(172.55m, totals.Total);
    }

    [Fact]
    public void TheSampleDefaultsComeToTheNotesTotal()
    {
        var labels = new LiveCheckoutLabels();
        var plans = LiveCheckoutSamples.Plans();
        var lines = LiveCheckoutMath.Lines(plans[1], 8, LiveCheckoutBilling.Yearly, 0.2m, LiveCheckoutSamples.Addons(),
            ["care"], labels, "USD", En);
        Assert.Equal(2, lines.Count);
        Assert.Equal(2150.40m, lines[0].Amount);
        Assert.Equal("Crew plan", lines[0].Title);
        Assert.Equal("8 seats × $22.40 × 12 months", lines[0].Detail);
        Assert.Equal(432m, lines[1].Amount);
        Assert.Equal("Billed yearly", lines[1].Detail);
        var totals = LiveCheckoutMath.Totals(lines.Select(l => l.Amount), 0, 0.21m);
        Assert.Equal(2582.40m, totals.Subtotal);
        Assert.Equal(542.30m, totals.Tax);
        Assert.Equal(3124.70m, totals.Total);
    }

    [Fact]
    public void AddonRowsFollowListOrderAndOneSeatIsSingular()
    {
        var labels = new LiveCheckoutLabels();
        var lines = LiveCheckoutMath.Lines(LiveCheckoutSamples.Plans()[1], 1, LiveCheckoutBilling.Monthly, 0.2m,
            LiveCheckoutSamples.Addons(), ["archive", "care"], labels, "USD", En);
        Assert.Equal(["crew", "care", "archive"], lines.Select(l => l.Id));
        Assert.Equal("1 seat × $28.00 × 1 month", lines[0].Detail);
        Assert.Equal("Billed monthly", lines[1].Detail);
    }

    [Fact]
    public void TaxNamesAndPercentsDropTrailingZeros()
    {
        var labels = new LiveCheckoutLabels();
        Assert.Equal("19%", LiveCheckoutMath.Percent(0.19m, En));
        Assert.Equal("8.1%", LiveCheckoutMath.Percent(0.081m, En));
        var countries = LiveCheckoutSamples.Countries();
        Assert.Equal("VAT 21%", LiveCheckoutMath.TaxName(countries[0], labels, En));
        Assert.Equal("Tax", LiveCheckoutMath.TaxName(countries[1], labels, En));
        Assert.Equal("GST 15%", LiveCheckoutMath.TaxName(countries[5], labels, En));
        Assert.Equal("−20%", LiveCheckoutMath.DiscountBadge(0.2m, En));
        Assert.Null(LiveCheckoutMath.DiscountBadge(0, En));
    }

    [Fact]
    public void BillingFieldRules()
    {
        Assert.Equal(LiveCheckoutFieldError.EmailMissing, LiveCheckoutValidation.Email(""));
        Assert.Equal(LiveCheckoutFieldError.EmailInvalid, LiveCheckoutValidation.Email("a@b"));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Email("a@b.co"));
        Assert.Equal(LiveCheckoutFieldError.NameMissing, LiveCheckoutValidation.Name("A"));
        Assert.Equal(LiveCheckoutFieldError.NameMissing, LiveCheckoutValidation.Name(" b "));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Name("Al"));
        Assert.Equal(LiveCheckoutFieldError.AddressMissing, LiveCheckoutValidation.Address("ab"));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Address("abc"));
        Assert.Equal(LiveCheckoutFieldError.CityMissing, LiveCheckoutValidation.City(""));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.City("X"));
    }

    [Fact]
    public void PostalCodesFollowTheCountry()
    {
        var c = LiveCheckoutSamples.Countries().ToDictionary(x => x.Code);
        Assert.Equal(LiveCheckoutFieldError.PostalMissing, LiveCheckoutValidation.Postal("", c["US"]));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Postal("94107-1234", c["US"]));
        Assert.Equal(LiveCheckoutFieldError.PostalMismatch, LiveCheckoutValidation.Postal("9410", c["US"]));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Postal("1016 HP", c["NL"]));
        Assert.Equal(LiveCheckoutFieldError.PostalMismatch, LiveCheckoutValidation.Postal("1016", c["NL"]));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Postal("k1a 0b1", c["CA"]));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Postal("100-0001", c["JP"]));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Postal("6011", c["NZ"]));
    }

    [Fact]
    public void CardNetworksGroupingAndChecksum()
    {
        Assert.Equal(LiveCheckoutCardBrand.Novacard, LiveCheckoutValidation.Brand("4"));
        Assert.Equal(LiveCheckoutCardBrand.Meridian, LiveCheckoutValidation.Brand("5"));
        Assert.Equal(LiveCheckoutCardBrand.Meridian, LiveCheckoutValidation.Brand("2221 00"));
        Assert.Equal(LiveCheckoutCardBrand.None, LiveCheckoutValidation.Brand("2721"));
        Assert.Equal(LiveCheckoutCardBrand.Atlas, LiveCheckoutValidation.Brand("37"));
        Assert.Equal(LiveCheckoutCardBrand.None, LiveCheckoutValidation.Brand("6011"));
        Assert.Equal("4111 1111 1111 1111", LiveCheckoutValidation.FormatCard("41111111111111119999"));
        Assert.Equal("3782 822463 10005", LiveCheckoutValidation.FormatCard("378282246310005"));
        Assert.Equal("4111 11", LiveCheckoutValidation.FormatCard("4111a11"));
        Assert.Equal(LiveCheckoutFieldError.CardMissing, LiveCheckoutValidation.Card(""));
        Assert.Equal(LiveCheckoutFieldError.CardShort, LiveCheckoutValidation.Card("4111 1111"));
        Assert.Equal(LiveCheckoutFieldError.CardInvalid, LiveCheckoutValidation.Card("4111 1111 1111 1112"));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Card("4111 1111 1111 1111"));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Card(LiveCheckoutSamples.DeclinedCardNumber));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Card("3782 822463 10005"));
    }

    [Fact]
    public void ExpiryIsShapedAndChecked()
    {
        Assert.Equal("02/", LiveCheckoutValidation.FormatExpiry("2", ""));
        Assert.Equal("1", LiveCheckoutValidation.FormatExpiry("1", ""));
        Assert.Equal("12/", LiveCheckoutValidation.FormatExpiry("12", "1"));
        Assert.Equal("12", LiveCheckoutValidation.FormatExpiry("12", "12/"));
        Assert.Equal("12/30", LiveCheckoutValidation.FormatExpiry("12305", "12/30"));
        var today = new DateTime(2026, 11, 8);
        Assert.Equal(LiveCheckoutFieldError.ExpiryMissing, LiveCheckoutValidation.Expiry("", today));
        Assert.Equal(LiveCheckoutFieldError.ExpiryIncomplete, LiveCheckoutValidation.Expiry("12/3", today));
        Assert.Equal(LiveCheckoutFieldError.ExpiryMonth, LiveCheckoutValidation.Expiry("13/30", today));
        Assert.Equal(LiveCheckoutFieldError.ExpiryPast, LiveCheckoutValidation.Expiry("10/26", today));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Expiry("11/26", today));
    }

    [Fact]
    public void SecurityCodeLengthFollowsTheNetwork()
    {
        Assert.Equal("123", LiveCheckoutValidation.FormatCvc("12345", LiveCheckoutCardBrand.Novacard));
        Assert.Equal("1234", LiveCheckoutValidation.FormatCvc("12345", LiveCheckoutCardBrand.Atlas));
        Assert.Equal(LiveCheckoutFieldError.CvcThree, LiveCheckoutValidation.Cvc("12", LiveCheckoutCardBrand.None));
        Assert.Equal(LiveCheckoutFieldError.CvcFour, LiveCheckoutValidation.Cvc("123", LiveCheckoutCardBrand.Atlas));
        Assert.Equal(LiveCheckoutFieldError.None, LiveCheckoutValidation.Cvc("737", LiveCheckoutCardBrand.Novacard));
    }

    [Fact]
    public void SeatsAreClampedAsTyped()
    {
        Assert.Equal(1, LiveCheckoutValidation.ParseSeats("", 1, 500));
        Assert.Equal(500, LiveCheckoutValidation.ParseSeats("999", 1, 500));
        Assert.Equal(12, LiveCheckoutValidation.ParseSeats("12a", 1, 500));
        Assert.Equal(10, LiveCheckoutValidation.ParseSeats("10", 1, 500));
        Assert.Equal(1, LiveCheckoutValidation.ParseSeats("0", 1, 500));
    }

    [Fact]
    public async Task SampleBackendAcceptsTwoCouponsAndDeclinesOneCard()
    {
        var check = LiveCheckoutSamples.CreateCouponValidator((_, _) => Task.CompletedTask);
        var ok = await check("spring15", CancellationToken.None);
        Assert.True(ok.IsValid);
        Assert.Equal("SPRING15", ok.Code);
        Assert.Equal(15m, ok.Percent);
        Assert.False((await check("HELLO", CancellationToken.None)).IsValid);

        var waits = new List<TimeSpan>();
        var pay = LiveCheckoutSamples.CreatePaymentSimulator((t, _) => { waits.Add(t); return Task.CompletedTask; }, () => 42);
        var receipt = await pay(new LiveCheckoutOrder { CardNumber = "4111111111111111" }, CancellationToken.None);
        Assert.Equal("LW-000042", receipt!.OrderNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pay(new LiveCheckoutOrder { CardNumber = LiveCheckoutValidation.Digits(LiveCheckoutSamples.DeclinedCardNumber) }, CancellationToken.None));
        await pay(new LiveCheckoutOrder { PaymentMethod = LiveCheckoutPaymentMethod.Wallet, CardNumber = "4000000000009995" }, CancellationToken.None);
        Assert.Equal([LiveCheckoutSamples.CardDelay, LiveCheckoutSamples.CardDelay, LiveCheckoutSamples.WalletDelay], waits);
    }
}

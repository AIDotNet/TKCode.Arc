using System.Globalization;
using System.Text.Json;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class WebhooksMathTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void Rate_Rounds_Down_To_A_Tenth_Of_A_Percent_Like_Arc()
    {
        // 294 of 297 delivered is 98.98…% and Arc shows 98.9%; each retry that goes through adds an event and removes a failure.
        WebhookMath.Rate(297, 3, 7, 3).ShouldBe(0.989);
        WebhookMath.Rate(298, 2, 8, 2).ShouldBe(0.993);
        WebhookMath.Rate(299, 1, 9, 1).ShouldBe(0.996);
        WebhookMath.Rate(300, 0, 10, 0).ShouldBe(1.0);
        WebhookMath.FormatRate(0.989, En).ShouldBe("98.9%");
        WebhookMath.FormatRate(1.0, En).ShouldBe("100.0%");
        WebhookMath.FormatRate(null, En).ShouldBe("–");
    }

    [Fact]
    public void Rate_Falls_Back_To_Deliveries_And_Is_Null_Without_Traffic()
    {
        WebhookMath.Rate(0, 0, 4, 1).ShouldBe(0.75);
        WebhookMath.Rate(0, 0, 0, 0).ShouldBeNull();
        WebhookMath.Rate(2, 5, 0, 0).ShouldBe(0.0);
    }

    [Fact]
    public void Durations_Times_And_Codes_Format_Like_The_List()
    {
        WebhookMath.FormatDuration(TimeSpan.FromMilliseconds(2318), En).ShouldBe("2,318 ms");
        WebhookMath.FormatDuration(TimeSpan.FromMilliseconds(91), En).ShouldBe("91 ms");
        WebhookMath.FormatDuration(TimeSpan.FromSeconds(30), En).ShouldBe("30.0 s");
        WebhookMath.FormatTime(new DateTimeOffset(2026, 10, 26, 15, 42, 10, TimeSpan.Zero)).ShouldBe("15:42:10");
        WebhookMath.CodeText(0).ShouldBe("—");
        WebhookMath.StatusTextFor(504).ShouldBe("Gateway Timeout");
        WebhookMath.IsFailureCode(0).ShouldBeTrue();
        WebhookMath.IsFailureCode(399).ShouldBeFalse();
        WebhookMath.IsFailureCode(400).ShouldBeTrue();
    }

    [Fact]
    public void Url_Check_Requires_Public_Https()
    {
        WebhookMath.ValidateUrl("  ", "required", "invalid").ShouldBe("required");
        WebhookMath.ValidateUrl("http://relay.tallowfield.dev/hooks", "required", "invalid").ShouldBe("invalid");
        WebhookMath.ValidateUrl("relay.tallowfield.dev/hooks", "required", "invalid").ShouldBe("invalid");
        WebhookMath.ValidateUrl("https://localhost:8443/hooks", "required", "invalid").ShouldBe("invalid");
        WebhookMath.ValidateUrl("https://api.localhost/hooks", "required", "invalid").ShouldBe("invalid");
        WebhookMath.ValidateUrl("https://relay.tallowfield.dev/hooks", "required", "invalid").ShouldBeNull();
        // Arc accepts IP addresses and test domains.
        WebhookMath.ValidateUrl("https://10.0.4.7/in", "required", "invalid").ShouldBeNull();
        WebhookMath.ValidateUrl("https://shop.test/in", "required", "invalid").ShouldBeNull();
    }

    [Fact]
    public void Urls_Split_Into_Host_And_Rest_Without_The_Scheme()
    {
        WebhookMath.SplitUrl("https://relay.tallowfield.dev/hooks/corvid/v2").ShouldBe(("relay.tallowfield.dev", "/hooks/corvid/v2"));
        WebhookMath.SplitUrl("https://pager.tallowfield.dev:8443").ShouldBe(("pager.tallowfield.dev:8443", ""));
        WebhookMath.HostOf("desk.tallowfield.dev/in?x=1").ShouldBe("desk.tallowfield.dev");
        new WebhookEndpoint { Url = "https://stock.tallowfield.dev/in" }.DisplayName.ShouldBe("stock.tallowfield.dev");
    }

    [Fact]
    public void Chips_Show_Three_And_The_Rest_As_A_Count()
    {
        var (shown, more) = WebhookMath.Chips(["a", "b", "c", "d", "e", "f"], 3);
        shown.ShouldBe(["a", "b", "c"]);
        more.ShouldBe(3);
        WebhookMath.Chips(["a", "b"], 3).More.ShouldBe(0);
    }

    [Fact]
    public void Json_Is_Indented_Into_Colored_Lines()
    {
        var lines = WebhookJson.Format("{\"id\":\"evt_1\",\"n\":42,\"ok\":true,\"none\":null,\"list\":[1,{}],\"empty\":[],\"data\":{\"a\":\"x\"}}")!;
        lines.ShouldNotBeNull();
        var text = WebhookJson.ToText(lines);
        text.ShouldBe(string.Join("\n",
            "{",
            "  \"id\": \"evt_1\",",
            "  \"n\": 42,",
            "  \"ok\": true,",
            "  \"none\": null,",
            "  \"list\": [",
            "    1,",
            "    {}",
            "  ],",
            "  \"empty\": [],",
            "  \"data\": {",
            "    \"a\": \"x\"",
            "  }",
            "}"));
        lines[1].ShouldContain(s => s.Kind == WebhookJsonKind.Key && s.Text == "\"id\"");
        lines[1].ShouldContain(s => s.Kind == WebhookJsonKind.String && s.Text == "\"evt_1\"");
        lines[2].ShouldContain(s => s.Kind == WebhookJsonKind.Number && s.Text == "42");
        lines[3].ShouldContain(s => s.Kind == WebhookJsonKind.Literal && s.Text == "true");
        // The indented text is still the same JSON.
        JsonDocument.Parse(text).RootElement.GetProperty("data").GetProperty("a").GetString().ShouldBe("x");
    }

    [Fact]
    public void Json_Keeps_Escapes_And_Rejects_What_Is_Not_Json()
    {
        var lines = WebhookJson.Format("{\"q\":\"say \\\"hi\\\"\"}")!;
        WebhookJson.ToText(lines).ShouldContain("\"say \\\"hi\\\"\"");
        WebhookJson.Format("gateway gave up after 30 s").ShouldBeNull();
        WebhookJson.Format("{\"open\":").ShouldBeNull();
        WebhookJson.Format(null).ShouldBeNull();
    }

    [Fact]
    public void Default_Delivery_Is_The_First_Open_Failure()
    {
        var ok = new WebhookDelivery { StatusCode = 200 };
        var resolved = new WebhookDelivery { StatusCode = 500, IsResolved = true };
        var failing = new WebhookDelivery { StatusCode = 503 };
        WebhookMath.DefaultDelivery([ok, resolved, failing]).ShouldBeSameAs(failing);
        WebhookMath.DefaultDelivery([ok, resolved]).ShouldBeSameAs(ok);
        WebhookMath.DefaultDelivery([]).ShouldBeNull();
    }

    [Fact]
    public void Buckets_Are_Found_By_Time()
    {
        var start = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
        var day = WebhookMath.EmptyDay(start.AddHours(23).AddMinutes(40)).ToList();
        day.Count.ShouldBe(48);
        day[^1].Start.ShouldBe(start.AddHours(23).AddMinutes(30));
        WebhookMath.BucketIndexFor(day, start.AddHours(23).AddMinutes(45)).ShouldBe(47);
        WebhookMath.BucketIndexFor(day, start.AddDays(2)).ShouldBe(47);
        WebhookMath.BucketIndexFor(day, start.AddDays(-2)).ShouldBe(0);
        WebhookMath.BucketIndexFor(day, day[10].Start.AddMinutes(1)).ShouldBe(10);
        WebhookMath.BucketIndexFor([], start).ShouldBe(-1);
    }

    [Fact]
    public void Endpoint_Stats_Follow_Deliveries_And_The_Chart()
    {
        var endpoint = new WebhookEndpoint { Url = "https://relay.tallowfield.dev/hooks" };
        endpoint.Health.ShouldBe(WebhookHealth.NoTraffic);
        endpoint.DeliveredRate.ShouldBeNull();
        var failing = new WebhookDelivery { StatusCode = 500 };
        endpoint.Deliveries.Add(new WebhookDelivery { StatusCode = 200 });
        endpoint.Deliveries.Add(failing);
        endpoint.FailingCount.ShouldBe(1);
        endpoint.Health.ShouldBe(WebhookHealth.Failing);
        endpoint.DeliveredRate.ShouldBe(0.5);
        endpoint.Volume.Add(new WebhookVolumeBucket { Total = 297, Failed = 3 });
        endpoint.DeliveredRate.ShouldBe(0.989);
        endpoint.Volume[0].Failed = 0;
        endpoint.DeliveredRate.ShouldBe(1.0);
        failing.IsResolved = true;
        endpoint.FailingCount.ShouldBe(0);
        endpoint.Health.ShouldBe(WebhookHealth.Healthy);
        // A later attempt that failed takes over: the original no longer counts.
        var other = new WebhookDelivery { StatusCode = 502 };
        endpoint.Deliveries.Add(other);
        endpoint.FailingCount.ShouldBe(1);
        other.RetriedBy = new WebhookDelivery { StatusCode = 0 };
        endpoint.FailingCount.ShouldBe(0);
    }

    [Fact]
    public void Labels_Use_Singular_Forms()
    {
        var labels = new WebhooksLabels();
        labels.EndpointsWord(1).ShouldBe("endpoint");
        labels.EndpointsWord(3).ShouldBe("endpoints");
        labels.ChartEvents(1).ShouldBe("1 event");
        labels.EventsSelected(1).ShouldBe("1 event selected");
        labels.EventsSelected(2).ShouldBe("2 events selected");
    }
}

using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class WebhookTesterTests
{
    private static WebhookTester Create(Action<WebhookTester>? setup = null)
    {
        var tester = new WebhookTester { AutoPlay = false, SimulatedLatency = TimeSpan.Zero };
        setup?.Invoke(tester);
        return tester;
    }

    private static Window Show(Control control, double width = 900)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 1400;
        Pump(window);
        return window;
    }

    private static void Pump(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    // ---- Pure logic ----

    [Fact]
    public void Signature_Is_Hmac_Sha256_Of_Timestamp_Dot_Body()
    {
        var expected = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("topsecret"), Encoding.UTF8.GetBytes("1793111528.{\"a\":1}"))).ToLowerInvariant();
        var signature = WebhookTesterSigner.Sign("topsecret", 1793111528, "{\"a\":1}");
        signature.ShouldBe(expected);
        signature.Length.ShouldBe(64);
        WebhookTesterSigner.HeaderValue(1793111528, signature).ShouldBe($"t=1793111528,v1={expected}");
        WebhookTesterSigner.Sign("other", 1793111528, "{\"a\":1}").ShouldNotBe(signature);
        WebhookTesterSigner.Sign("topsecret", 1793111529, "{\"a\":1}").ShouldNotBe(signature);
    }

    [Fact]
    public void Json_Check_Reports_Validity_Bytes_And_The_Failing_Line()
    {
        var ok = WebhookTesterJson.Check("{\"a\": \"é\"}");
        ok.IsValid.ShouldBeTrue();
        ok.Bytes.ShouldBe(11); // é is two bytes
        WebhookTesterJson.Check("  ").IsEmpty.ShouldBeTrue();
        var bad = WebhookTesterJson.Check("{\n  \"a\": 1,\n  \"b\": \n}");
        bad.IsValid.ShouldBeFalse();
        bad.IsEmpty.ShouldBeFalse();
        bad.Line.ShouldBeGreaterThan(1);
        bad.Message.ShouldNotBeNullOrWhiteSpace();
        bad.Message!.ShouldNotContain("LineNumber");
    }

    [Fact]
    public void Json_Format_Pretty_Prints_Valid_Json_And_Keeps_The_Rest()
    {
        WebhookTesterJson.Format("{\"a\":[1,2],\"b\":null}").ShouldBe("{\n  \"a\": [\n    1,\n    2\n  ],\n  \"b\": null\n}");
        WebhookTesterJson.Format("not json").ShouldBe("not json");
        WebhookTesterJson.Format("").ShouldBe("");
    }

    [Fact]
    public void Enter_Keeps_The_Indentation_And_Indents_After_An_Opening_Bracket()
    {
        WebhookTesterJson.NewLine("{\n  \"a\": 1", 10).ShouldBe("\n  ");
        WebhookTesterJson.NewLine("{", 1).ShouldBe("\n  ");
        WebhookTesterJson.NewLine("{\n  \"a\": [", 11).ShouldBe("\n    ");
        WebhookTesterJson.NewLine("", 0).ShouldBe("\n");
    }

    [Fact]
    public void Tokenizer_Covers_The_Whole_Text_And_Tells_Keys_From_Strings()
    {
        const string text = "{\n  \"id\": \"evt_1\", \"n\": -12.5e3, \"ok\": true, \"x\": null\n}";
        var runs = WebhookTesterJson.Tokenize(text);
        var rebuilt = string.Concat(runs.Select(r => text.Substring(r.Start, r.Length)));
        rebuilt.ShouldBe(text);
        string Of(WebhookTesterSyntax kind) => string.Join("|", runs.Where(r => r.Kind == kind).Select(r => text.Substring(r.Start, r.Length)));
        Of(WebhookTesterSyntax.Key).ShouldBe("\"id\"|\"n\"|\"ok\"|\"x\"");
        Of(WebhookTesterSyntax.String).ShouldBe("\"evt_1\"");
        Of(WebhookTesterSyntax.Number).ShouldBe("-12.5e3");
        Of(WebhookTesterSyntax.Literal).ShouldBe("true|null");
    }

    [Theory]
    [InlineData("https://api.acme.dev/hook", true)]
    [InlineData("http://localhost:3000/hook", true)]
    [InlineData("http://127.0.0.1/hook", true)]
    [InlineData("http://api.acme.dev/hook", false)]
    [InlineData("ftp://api.acme.dev", false)]
    [InlineData("api.acme.dev/hook", false)]
    [InlineData("", false)]
    public void Endpoint_Must_Be_Https_Except_Localhost(string url, bool allowed) =>
        WebhookTesterJson.IsAllowedEndpoint(url).ShouldBe(allowed);

    // ---- The block ----

    [AvaloniaFact]
    public void First_Look_Offers_The_First_Event_With_Its_Payload()
    {
        var tester = Create();
        var window = Show(tester);
        tester.SelectedEvent!.Type.ShouldBe("order.paid");
        tester.EventSelect!.SelectedItem.ShouldBeSameAs(tester.SelectedEvent);
        tester.Editor!.Text!.ShouldContain("\"type\": \"order.paid\"");
        tester.ValidityText!.Text.ShouldStartWith("Valid JSON · ");
        tester.Editor.LineCount.ShouldBeGreaterThan(5);
        tester.SecretText!.Text.ShouldBe("sigsec_" + new string('•', 16));
        tester.Tabs.Count.ShouldBe(3);
        tester.Phase.ShouldBe(WebhookTestPhase.Idle);
        tester.StepLabel(2)!.Text.ShouldBe("Response");
        tester.ResponsePanel!.Text.ShouldBe("Send a test event to see what your endpoint answers.");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Secret_Stays_Out_Of_The_Tree_Until_It_Is_Revealed()
    {
        var tester = Create();
        var window = Show(tester);
        Avalonia.Automation.AutomationProperties.GetName(tester.SecretText!).ShouldBe("Signing secret, hidden");
        tester.RevealToggle!.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Pump(window);
        tester.RevealToggle.IsChecked.ShouldBe(true);
        tester.SecretText!.Text.ShouldBe(tester.Secret);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Simulated_Send_Signs_Selects_And_Announces()
    {
        var tester = Create();
        var window = Show(tester);
        var delivery = await tester.SendAsync();
        Pump(window);
        delivery.ShouldNotBeNull();
        tester.Deliveries[0].ShouldBeSameAs(delivery);
        tester.SelectedDelivery.ShouldBeSameAs(delivery);
        tester.Phase.ShouldBe(WebhookTestPhase.Succeeded);
        delivery.Event.ShouldBe("order.paid");
        tester.StepLabel(2)!.Text.ShouldBe("200 OK");
        tester.StepDetail(2)!.Text.ShouldBe($"{delivery.Response.DurationMs} ms");
        tester.ResponsePanel!.Text.ShouldBe("{\n  \"received\": true\n}");
        tester.Announcer!.History.Last().ShouldContain("Simulated, nothing was sent.");
        tester.Announcer.History.ShouldContain(h => h.StartsWith("Sending order.paid."));
        tester.DeliveryRows!.Children.Count.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_Request_Handed_To_Your_Server_Is_Signed_With_The_Secret()
    {
        WebhookTestRequest? seen = null;
        var tester = Create(t =>
        {
            t.Secret = "whk_test_secret";
            t.SignatureHeader = "webhook-signature";
            t.SendAction = (request, _) =>
            {
                seen = request;
                return Task.FromResult(new WebhookTestResponse { Status = 200, DurationMs = 42, Body = "ok" });
            };
        });
        var window = Show(tester);
        await tester.SendAsync();
        seen.ShouldNotBeNull();
        seen.Url.ShouldBe(tester.Endpoint);
        seen.Event.ShouldBe("order.paid");
        seen.Body.ShouldBe(tester.Editor!.Text);
        seen.SignatureHeader.ShouldBe("webhook-signature");
        seen.Signature.ShouldBe(WebhookTesterSigner.HeaderValue(seen.Timestamp, WebhookTesterSigner.Sign("whk_test_secret", seen.Timestamp, seen.Body)));
        seen.Headers.ShouldContain(h => h.Key == "webhook-signature" && h.Value == seen.Signature);
        tester.Announcer!.History.Last().ShouldNotContain("Simulated");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Failed_Delivery_Ends_The_Timeline_In_Red_And_Offers_A_Retry()
    {
        var tester = Create(t => t.SendAction = (_, _) => Task.FromResult(new WebhookTestResponse { Status = 500, DurationMs = 310, Body = "boom" }));
        var window = Show(tester);
        var failed = await tester.SendAsync();
        Pump(window);
        failed!.IsFailed.ShouldBeTrue();
        tester.Phase.ShouldBe(WebhookTestPhase.Failed);
        tester.StepLabel(2)!.Text.ShouldBe("500 Internal Server Error");
        tester.StepCircle(2)!.Tone.ShouldBe(WebhookTesterTone.StepFailed);
        tester.RetryButtonOf(failed).ShouldNotBeNull();
        tester.Announcer!.History.Last().ShouldBe("order.paid failed: 500 Internal Server Error.");

        // Retry goes through RetryAction with the failed delivery, under a fresh signature.
        WebhookTestDelivery? retried = null;
        tester.RetryAction = (delivery, _) =>
        {
            retried = delivery;
            return Task.FromResult(new WebhookTestResponse { Status = 200, DurationMs = 55 });
        };
        var again = await tester.RetryAsync(failed);
        Pump(window);
        retried.ShouldBeSameAs(failed);
        again!.IsFailed.ShouldBeFalse();
        again.Request.Body.ShouldBe(failed.Request.Body);
        tester.Deliveries.Count.ShouldBe(2);
        tester.Deliveries[0].ShouldBeSameAs(again);
        tester.Phase.ShouldBe(WebhookTestPhase.Succeeded);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Thrown_Or_Missing_Answer_Is_A_Failed_Delivery()
    {
        var tester = Create(t => t.SendAction = (_, _) => throw new InvalidOperationException("connection refused"));
        var window = Show(tester);
        var thrown = await tester.SendAsync();
        thrown!.Response.Status.ShouldBe(0);
        thrown.Response.Error.ShouldBe("connection refused");
        tester.StepLabel(2)!.Text.ShouldBe("No response");
        tester.ResponsePanel!.Text.ShouldBe("connection refused");

        tester.SendAction = (_, _) => Task.FromResult(new WebhookTestResponse { Status = 0, TimedOut = true, DurationMs = 10000 });
        var timedOut = await tester.SendAsync();
        tester.StepLabel(2)!.Text.ShouldBe("Timed out");
        timedOut!.IsFailed.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Payload_That_Does_Not_Parse_Is_Not_Sent()
    {
        var called = false;
        var tester = Create(t => t.SendAction = (_, _) =>
        {
            called = true;
            return Task.FromResult(new WebhookTestResponse { Status = 200 });
        });
        var window = Show(tester);
        tester.Editor!.Text = "{\n  \"a\": \n}";
        Pump(window);
        tester.ValidityText!.Text.ShouldStartWith("Invalid JSON on line ");
        tester.FormatButton!.IsEnabled.ShouldBeFalse();
        Avalonia.Automation.AutomationProperties.GetHelpText(tester.Editor).ShouldStartWith("Invalid JSON");
        (await tester.SendAsync()).ShouldBeNull();
        called.ShouldBeFalse();
        tester.Announcer!.History.Last().ShouldBe("The payload is not valid JSON.");

        tester.Editor.Text = "";
        Pump(window);
        tester.ValidityText.Text.ShouldBe("Payload is empty");
        window.Close();
    }

    [AvaloniaFact]
    public async Task An_Endpoint_That_Is_Not_Https_Is_Refused_Except_On_Localhost()
    {
        var tester = Create();
        var window = Show(tester);
        tester.Endpoint = "http://api.acme.dev/hook";
        (await tester.SendAsync()).ShouldBeNull();
        tester.EndpointInput!.ErrorMessage.ShouldBe("Use an https URL (http is allowed for localhost).");
        tester.Endpoint = "http://localhost:4000/hook";
        Pump(window);
        tester.EndpointInput.ErrorMessage.ShouldBeNull();
        (await tester.SendAsync()).ShouldNotBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Format_Pretty_Prints_The_Payload()
    {
        var tester = Create();
        var window = Show(tester);
        tester.Editor!.Text = "{\"a\":1}";
        Pump(window);
        tester.FormatButton!.IsEnabled.ShouldBeTrue();
        tester.FormatButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump(window);
        tester.Editor.Text.ShouldBe("{\n  \"a\": 1\n}");
        tester.Payload.ShouldBe("{\n  \"a\": 1\n}");
        window.Close();
    }

    [AvaloniaFact]
    public void Choosing_An_Event_Loads_Its_Payload()
    {
        var tester = Create(t => t.Events.AddRange(WebhookTesterDemoData.RelayEvents()));
        var window = Show(tester);
        tester.SelectedEvent!.Type.ShouldBe("message.delivered");
        tester.SelectedEvent = tester.Events[2];
        Pump(window);
        tester.Editor!.Text!.ShouldContain("\"domain\": \"mail.northwind.dev\"");
        tester.EventSelect!.SelectedItem.ShouldBeSameAs(tester.Events[2]);
        Avalonia.Automation.AutomationProperties.GetName(tester.Editor).ShouldBe("Payload for domain.verified");
        window.Close();
    }

    [AvaloniaFact]
    public void Control_Enter_Sends_From_The_Editor()
    {
        var tester = Create();
        var window = Show(tester);
        tester.Editor!.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        TestHelpers.PumpUntil(() => tester.Deliveries.Count == 1).ShouldBeTrue();
        // The payload was not touched by the shortcut (no stray new line).
        tester.Editor.Text!.ShouldNotEndWith("\n\n");
        window.Close();
    }

    [AvaloniaFact]
    public void Response_Tabs_Rove_With_The_Arrow_Keys_And_Show_Headers_And_The_Request()
    {
        var tester = Create();
        var window = Show(tester);
        var delivery = WebhookTesterDemoData.Deliveries().First();
        tester.Deliveries.Add(delivery);
        tester.SelectedDelivery = delivery;
        Pump(window);
        tester.Tabs[0].IsChecked.ShouldBe(true);
        tester.Tabs[1].IsTabStop.ShouldBeFalse();
        tester.ResponsePanel!.Text.ShouldBe("{\n  \"received\": true\n}");

        tester.Tabs[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump(window);
        tester.Tabs[1].IsChecked.ShouldBe(true);
        tester.Tabs[0].IsChecked.ShouldBe(false);
        tester.ResponsePanel.Text!.ShouldContain("content-type: application/json");

        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Pump(window);
        tester.Tabs[2].IsChecked.ShouldBe(true);
        tester.ResponsePanel.Text!.ShouldStartWith("POST https://api.northwind.dev/webhooks/billing");
        tester.ResponsePanel.Text!.ShouldContain("x-webhook-signature: t=");

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump(window);
        tester.Tabs[0].IsChecked.ShouldBe(true);
        window.Close();
    }

    [AvaloniaFact]
    public void Picking_A_Delivery_Shows_Its_Response_And_The_List_Counts_Failures()
    {
        var tester = Create(t => t.Deliveries.AddRange(WebhookTesterDemoData.Deliveries()));
        var window = Show(tester);
        tester.SelectedDelivery.ShouldBeSameAs(tester.Deliveries[0]); // the first one is selected
        tester.DeliveryRows!.Children.Count.ShouldBe(5);
        var failed = tester.Deliveries[1];
        failed.IsFailed.ShouldBeTrue();
        tester.RetryButtonOf(failed).ShouldNotBeNull();
        tester.RetryButtonOf(tester.Deliveries[0]).ShouldBeNull();

        tester.SelectedDelivery = failed;
        Pump(window);
        tester.Phase.ShouldBe(WebhookTestPhase.Failed);
        tester.StepLabel(2)!.Text.ShouldBe("500 Internal Server Error");
        tester.ResponsePanel!.Text!.ShouldContain("upstream unavailable");

        var timeout = tester.Deliveries[3];
        tester.SelectedDelivery = timeout;
        Pump(window);
        tester.StepLabel(2)!.Text.ShouldBe("Timed out");
        window.Close();
    }

    [AvaloniaFact]
    public void Request_And_Response_Sit_Side_By_Side_From_680_Wide()
    {
        var tester = Create();
        var window = Show(tester, width: 800);
        tester.IsSplit.ShouldBeTrue();
        window.Width = 500;
        Pump(window);
        tester.IsSplit.ShouldBeFalse();
        window.Width = 679;
        Pump(window);
        tester.IsSplit.ShouldBeFalse();
        window.Width = 700;
        Pump(window);
        tester.IsSplit.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void The_Source_Names_The_Subtitle()
    {
        var tester = Create(t => t.Source = new WebhookTesterSource { Name = "Relay Mail" });
        var window = Show(tester);
        tester.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Signed test events from Relay Mail");
        window.Close();
    }
}

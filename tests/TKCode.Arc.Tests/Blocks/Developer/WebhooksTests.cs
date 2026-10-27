using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Developer;

public class WebhooksTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 50, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static Webhooks Create(Action<Webhooks>? setup = null, bool withData = true)
    {
        var hooks = new Webhooks
        {
            Description = WebhooksDemoData.Description,
            CurrentUser = WebhooksDemoData.CurrentUser,
            DelayAsync = static (_, token) => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask,
            Time = new FixedTime(Now),
        };
        hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        if (withData)
        {
            hooks.Endpoints.AddRange(WebhooksDemoData.Endpoints(Now));
        }
        setup?.Invoke(hooks);
        return hooks;
    }

    private static Window Show(Control control, double width = 1200, double height = 1400, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
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

    private static void Step(ManualFrameClock clock, Window window, double milliseconds)
    {
        var frames = (int)Math.Ceiling(milliseconds / (1000.0 / 60.0));
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60.0));
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Visual? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Visual;

    private static void Click(Window window, Visual target)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = target.TranslatePoint(new Point(Math.Min(20, target.Bounds.Width / 2), target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump(window);
    }

    private static WebhookEndpoint Shipping(Webhooks hooks) => hooks.Endpoints[0];

    // ---- First look ----

    [AvaloniaFact]
    public void Shows_The_First_Endpoint_And_Its_First_Failing_Delivery()
    {
        var hooks = Create();
        var window = Show(hooks);
        var shipping = Shipping(hooks);
        hooks.SelectedEndpoint.ShouldBeSameAs(shipping);
        hooks.SelectedDelivery.ShouldBeSameAs(shipping.Deliveries[1]);
        hooks.SelectedDelivery!.StatusCode.ShouldBe(500);
        hooks.CountText.Text.ShouldBe("3");
        hooks.Note.Content.ShouldBe("1 failing");
        hooks.EndpointList.LiveItems.Count.ShouldBe(3);
        hooks.Split!.Inspector.Delivery.ShouldBeSameAs(shipping.Deliveries[1]);
        hooks.Split.List.LiveItems.Count.ShouldBe(7);
        hooks.Detail.Strip.BarCount.ShouldBe(48);
        var row = hooks.RowFor(shipping)!;
        row.Rate.Text.ShouldBe(WebhookMath.FormatRate(shipping.DeliveredRate));
        row.HealthBadge.Content.ShouldBe("3 failing");
        row.Chips.Children.Count.ShouldBe(4); // three events and "+1"
        hooks.RowFor(hooks.Endpoints[1])!.HealthBadge.Content.ShouldBe("Healthy");
        // The selected plate sits on the first row, inset 8 / 2.
        hooks.EndpointHighlight.CurrentRect.X.ShouldBe(8);
        hooks.EndpointHighlight.CurrentOpacity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Rows_And_Buttons_Have_Accessible_Names()
    {
        var hooks = Create();
        var window = Show(hooks);
        var row = hooks.RowFor(Shipping(hooks))!;
        Avalonia.Automation.AutomationProperties.GetName(row.Button).ShouldStartWith("Shipping sync, relay.tallowfield.dev.");
        var failing = hooks.Split!.RowFor(Shipping(hooks).Deliveries[1])!;
        Avalonia.Automation.AutomationProperties.GetName(failing.Button).ShouldStartWith("500 Internal Server Error, shipment.delayed");
        Avalonia.Automation.AutomationProperties.GetName(failing.RetryButton).ShouldStartWith("Retry shipment.delayed sent at ");
        Avalonia.Automation.AutomationProperties.GetName(hooks.Detail.Strip)!.ShouldContain("still failing");
        window.Close();
    }

    // ---- Selection and keyboard ----

    [AvaloniaFact]
    public void Clicking_An_Endpoint_Selects_It_And_Swaps_The_Lower_Half()
    {
        var hooks = Create();
        var window = Show(hooks);
        var changes = new List<WebhookEndpointSelectionChangedEventArgs>();
        hooks.EndpointSelectionChanged += (_, e) => changes.Add(e);
        var desk = hooks.Endpoints[1];
        Click(window, hooks.RowFor(desk)!.Button);
        hooks.SelectedEndpoint.ShouldBeSameAs(desk);
        changes.Single().ChangeSource.ShouldBe(WebhookChangeSource.Pointer);
        changes.Single().OldEndpoint.ShouldBeSameAs(Shipping(hooks));
        hooks.Split!.Endpoint.ShouldBeSameAs(desk);
        hooks.SelectedDelivery.ShouldBeSameAs(desk.Deliveries[0]);
        hooks.RowFor(desk)!.IsSelected.ShouldBeTrue();
        hooks.RowFor(Shipping(hooks))!.IsSelected.ShouldBeFalse();
        var deskRow = hooks.RowFor(desk)!;
        hooks.EndpointHighlight.CurrentRect.Y.ShouldBe(deskRow.Bounds.Y + 2, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Focus_And_Selection_Through_Endpoints_Without_Wrapping()
    {
        var hooks = Create();
        var window = Show(hooks);
        var rows = hooks.Endpoints.Select(e => hooks.RowFor(e)!).ToList();
        rows[0].Button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedEndpoint.ShouldBeSameAs(hooks.Endpoints[1]);
        Focused(window).ShouldBeSameAs(rows[1].Button);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedEndpoint.ShouldBeSameAs(hooks.Endpoints[2]);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedEndpoint.ShouldBeSameAs(hooks.Endpoints[2]);
        Focused(window).ShouldBeSameAs(rows[2].Button);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedEndpoint.ShouldBeSameAs(hooks.Endpoints[0]);
        Focused(window).ShouldBeSameAs(rows[0].Button);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Through_Deliveries_And_Skip_Retry_Buttons()
    {
        var hooks = Create();
        var window = Show(hooks);
        var deliveries = Shipping(hooks).Deliveries;
        var split = hooks.Split!;
        split.RowFor(deliveries[1])!.Button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedDelivery.ShouldBeSameAs(deliveries[2]);
        Focused(window).ShouldBeSameAs(split.RowFor(deliveries[2])!.Button);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedDelivery.ShouldBeSameAs(deliveries[0]);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Pump(window);
        hooks.SelectedDelivery.ShouldBeSameAs(deliveries[^1]);
        window.Close();
    }

    [AvaloniaFact]
    public void Changing_The_Endpoint_With_Focus_In_The_Deliveries_Keeps_The_Focus_In_The_New_List()
    {
        var hooks = Create();
        var window = Show(hooks);
        hooks.Split!.RowFor(Shipping(hooks).Deliveries[0])!.Button.Focus(NavigationMethod.Tab);
        hooks.SelectedEndpoint = hooks.Endpoints[1];
        Pump(window);
        var focused = Focused(window);
        focused.ShouldNotBeNull();
        focused.ShouldBeSameAs(hooks.Split!.RowFor(hooks.Endpoints[1].Deliveries[0])!.Button);
        window.Close();
    }

    // ---- Retry ----

    [AvaloniaFact]
    public void A_Retry_That_Goes_Through_Resolves_The_Delivery_And_Updates_Everything()
    {
        var hooks = Create();
        var window = Show(hooks);
        var shipping = Shipping(hooks);
        var original = shipping.Deliveries[1];
        var oldRate = shipping.DeliveredRate!.Value;
        var (total, failed) = WebhookMath.Sum(shipping.Volume);
        WebhookRetryCompletedEventArgs? completed = null;
        hooks.RetryCompleted += (_, e) => completed = e;
        var row = hooks.Split!.RowFor(original)!;
        row.RetryButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);

        original.IsResolved.ShouldBeTrue();
        shipping.Deliveries.Count.ShouldBe(8);
        var result = shipping.Deliveries[0];
        result.Attempt.ShouldBe(2);
        result.StatusCode.ShouldBe(200);
        original.RetriedBy.ShouldBeSameAs(result);
        shipping.FailingCount.ShouldBe(2);
        WebhookMath.Sum(shipping.Volume).ShouldBe((total + 1, failed - 1));
        shipping.DeliveredRate!.Value.ShouldBeGreaterThan(oldRate);
        completed.ShouldNotBeNull();
        completed!.Original.ShouldBeSameAs(original);
        completed.OldRate.ShouldBe(oldRate);
        hooks.RowFor(shipping)!.HealthBadge.Content.ShouldBe("2 failing");
        // The selection follows the new attempt, and the focus left the Retry button for the new row.
        hooks.SelectedDelivery.ShouldBeSameAs(result);
        Focused(window).ShouldBeSameAs(hooks.Split.RowFor(result)!.Button);
        hooks.Split.RowFor(result)!.IsTwoLine.ShouldBeFalse();
        row.Action.Content.ShouldNotBeSameAs(row.RetryButton);
        hooks.Announcer!.Announcement!.ShouldContain("went through on try 2");
        window.Close();
    }

    [AvaloniaFact]
    public void Retrying_Every_Failure_Makes_The_Endpoint_Healthy()
    {
        var hooks = Create();
        var window = Show(hooks);
        var shipping = Shipping(hooks);
        foreach (var failure in shipping.Deliveries.Where(d => d.IsOpenFailure).ToList())
        {
            hooks.RetryAsync(failure).IsCompleted.ShouldBeTrue();
        }
        Pump(window);
        shipping.FailingCount.ShouldBe(0);
        shipping.Health.ShouldBe(WebhookHealth.Healthy);
        shipping.DeliveredRate.ShouldBe(1.0);
        hooks.Note.Content.ShouldBe("all healthy");
        hooks.RowFor(shipping)!.HealthBadge.Content.ShouldBe("Healthy");
        hooks.Detail.ChartTitleText.ShouldContain("no failures");
        window.Close();
    }

    [AvaloniaFact]
    public void A_Retry_That_Fails_Adds_A_Failed_Attempt_With_Its_Own_Retry()
    {
        var hooks = Create(h => h.RetryAction = (_, _) => Task.FromException<WebhookDelivery>(new TimeoutException("No answer in 30 s.")));
        var window = Show(hooks);
        var shipping = Shipping(hooks);
        var original = shipping.Deliveries[1];
        WebhookActionFailedEventArgs? failedArgs = null;
        hooks.ActionFailed += (_, e) => failedArgs = e;
        var row = hooks.Split!.RowFor(original)!;
        row.RetryButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);

        var attempt = shipping.Deliveries[0];
        attempt.StatusCode.ShouldBe(0);
        attempt.Attempt.ShouldBe(2);
        attempt.ResponseBody.ShouldBe("No answer in 30 s.");
        original.IsResolved.ShouldBeFalse();
        original.RetriedBy.ShouldBeSameAs(attempt);
        shipping.FailingCount.ShouldBe(3);
        failedArgs!.Action.ShouldBe(WebhookActionKind.Retry);
        failedArgs.Exception.ShouldBeOfType<TimeoutException>();
        var newRow = hooks.Split.RowFor(attempt)!;
        newRow.Action.Content.ShouldBeSameAs(newRow.RetryButton);
        Focused(window).ShouldBeSameAs(newRow.RetryButton);
        hooks.Announcer!.Announcement!.ShouldContain("failed again on try 2");
        // The failed attempt can be retried in turn.
        hooks.RetryAction = null;
        hooks.RetryAsync(attempt);
        Pump(window);
        attempt.IsResolved.ShouldBeTrue();
        shipping.FailingCount.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Retry_Shows_A_Busy_Button_While_It_Waits()
    {
        var gate = new TaskCompletionSource<WebhookDelivery>();
        var hooks = Create(h => h.RetryAction = (_, _) => gate.Task);
        var window = Show(hooks);
        var original = Shipping(hooks).Deliveries[1];
        var task = hooks.RetryAsync(original);
        Pump(window);
        hooks.IsRetrying(original).ShouldBeTrue();
        var row = hooks.Split!.RowFor(original)!;
        Avalonia.Automation.AutomationProperties.GetName(row.RetryButton).ShouldBe("Resending shipment.delayed");
        gate.SetResult(new WebhookDelivery { EventType = original.EventType, StatusCode = 200, SentAt = Now });
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();
        original.IsResolved.ShouldBeTrue();
        window.Close();
    }

    // ---- Add endpoint ----

    [AvaloniaFact]
    public void The_Form_Checks_The_Url_And_The_Events_And_Focuses_The_First_Error()
    {
        var hooks = Create();
        var window = Show(hooks);
        hooks.OpenAddPanel();
        Pump(window);
        hooks.AddPanelState.ShouldBe(WebhookAddState.Editing);
        var form = hooks.Form;
        form.EventBoxes.Count.ShouldBe(6);
        form.EventBoxes[0].IsChecked.ShouldBe(true);
        form.CountText.Text.ShouldBe("1 event selected");
        form.EventBoxes[0].IsChecked = false;
        form.CountText.Text.ShouldBe("0 events selected");
        _ = hooks.SubmitFromFormAsync();
        Pump(window);
        form.UrlInput.ErrorMessage.ShouldBe("Add the URL to send events to");
        form.EventsError.IsVisible.ShouldBeTrue();
        Focused(window).ShouldNotBeNull();
        form.UrlInput.IsKeyboardFocusWithin.ShouldBeTrue();
        form.UrlInput.Text = "http://stock.tallowfield.dev/in";
        form.UrlInput.ErrorMessage.ShouldBeNull();
        _ = hooks.SubmitFromFormAsync();
        Pump(window);
        form.UrlInput.ErrorMessage.ShouldBe("Use an https:// address on a public host");
        hooks.EffectiveEndpoints.Count.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Adding_An_Endpoint_Shows_Its_Secret_Once_And_Selects_It()
    {
        var hooks = Create();
        var window = Show(hooks);
        WebhookEndpoint? created = null;
        WebhookAddCloseReason? closed = null;
        hooks.EndpointCreated += (_, e) => created = e.Endpoint;
        hooks.AddPanelClosed += (_, e) => closed = e.Reason;
        hooks.OpenAddPanel();
        Pump(window);
        hooks.Form.UrlInput.Text = "https://stock.tallowfield.dev/in";
        hooks.Form.DescriptionInput.Text = "Inventory mirror";
        hooks.Form.EventBoxes[2].IsChecked = true;
        hooks.Form.SubmitButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);

        created.ShouldNotBeNull();
        created!.Name.ShouldBe("Inventory mirror");
        created.Events.ShouldBe(["shipment.created", "shipment.delivered"]);
        created.AddedBy.ShouldBe(WebhooksDemoData.CurrentUser);
        created.Volume.Count.ShouldBe(48);
        created.SecretHint!.ShouldStartWith("sig_••••");
        hooks.Endpoints[0].ShouldBeSameAs(created);
        hooks.CountText.Text.ShouldBe("4");
        hooks.AddPanelState.ShouldBe(WebhookAddState.Revealing);
        hooks.Reveal.Secret.HasSecret.ShouldBeTrue();
        hooks.AddPanel!.Content.ShouldBeSameAs(hooks.Reveal);
        hooks.SelectedEndpoint.ShouldBeSameAs(created);
        hooks.SelectedDelivery.ShouldBeNull();
        hooks.Split!.EmptyState.IsPresent.ShouldBeTrue();
        hooks.RowFor(created)!.Rate.Text.ShouldBe("–");
        hooks.RowFor(created)!.HealthBadge.Content.ShouldBe("No traffic");
        hooks.EndpointList.StateOf(created)!.Value.Flash.ShouldBeGreaterThan(0);

        hooks.Reveal.Done.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        closed.ShouldBe(WebhookAddCloseReason.Done);
        hooks.IsAddPanelOpen.ShouldBeFalse();
        hooks.AddPanelState.ShouldBe(WebhookAddState.Closed);
        hooks.Reveal.Secret.HasSecret.ShouldBeFalse();
        Focused(window).ShouldBeSameAs(hooks.AddPanel.Toggle);
        // The next open starts from a fresh form.
        hooks.AddPanel.Content.ShouldBeSameAs(hooks.Form);
        hooks.Form.UrlInput.Text.ShouldBeNullOrEmpty();
        window.Close();
    }

    [AvaloniaFact]
    public void A_Failed_Create_Stays_In_The_Form_With_An_Error()
    {
        var hooks = Create(h => h.CreateEndpointAction = (_, _) =>
            Task.FromException<WebhookEndpointCreated>(new InvalidOperationException("The URL answered 404.")));
        var window = Show(hooks);
        WebhookActionFailedEventArgs? failed = null;
        hooks.ActionFailed += (_, e) => failed = e;
        hooks.OpenAddPanel();
        Pump(window);
        hooks.Form.UrlInput.Text = "https://stock.tallowfield.dev/in";
        hooks.Form.SubmitButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        hooks.AddPanelState.ShouldBe(WebhookAddState.Editing);
        hooks.Form.CreateError.IsVisible.ShouldBeTrue();
        hooks.Form.CreateError.Text!.ShouldContain("The URL answered 404.");
        hooks.Form.IsBusy.ShouldBeFalse();
        failed!.Action.ShouldBe(WebhookActionKind.CreateEndpoint);
        hooks.EffectiveEndpoints.Count.ShouldBe(3);
        Focused(window).ShouldBeSameAs(hooks.Form.SubmitButton);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_While_Saving_Cancels_The_Request_And_Keeps_The_Form()
    {
        CancellationToken seen = default;
        var hooks = Create(h => h.CreateEndpointAction = async (_, token) =>
        {
            seen = token;
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        });
        var window = Show(hooks);
        hooks.OpenAddPanel();
        Pump(window);
        hooks.Form.UrlInput.Text = "https://stock.tallowfield.dev/in";
        hooks.Form.SubmitButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        hooks.AddPanelState.ShouldBe(WebhookAddState.Submitting);
        hooks.Form.IsBusy.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);
        seen.IsCancellationRequested.ShouldBeTrue();
        hooks.IsAddPanelOpen.ShouldBeTrue();
        hooks.AddPanelState.ShouldBe(WebhookAddState.Editing);
        hooks.Form.IsBusy.ShouldBeFalse();
        // A second Escape closes the panel as usual.
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);
        hooks.IsAddPanelOpen.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void New_Endpoints_Go_Into_A_Bound_Source()
    {
        var source = new ObservableCollection<WebhookEndpoint>(WebhooksDemoData.Endpoints(Now));
        var hooks = Create(h => h.EndpointsSource = source, withData: false);
        var window = Show(hooks);
        hooks.EffectiveEndpoints.Count.ShouldBe(3);
        hooks.OpenAddPanel();
        Pump(window);
        hooks.Form.UrlInput.Text = "https://stock.tallowfield.dev/in";
        _ = hooks.SubmitFromFormAsync();
        Pump(window);
        source.Count.ShouldBe(4);
        source[0].DisplayName.ShouldBe("stock.tallowfield.dev");
        hooks.CountText.Text.ShouldBe("4");
        window.Close();
    }

    // ---- Test event ----

    [AvaloniaFact]
    public void A_Test_Event_Fills_An_Empty_Endpoint()
    {
        var hooks = Create(withData: false);
        var fresh = WebhooksDemoData.NewEndpoint(Now);
        hooks.Endpoints.Add(fresh);
        var window = Show(hooks);
        var split = hooks.Split!;
        split.EmptyState.IsPresent.ShouldBeTrue();
        split.Inspector.Delivery.ShouldBeNull();
        hooks.RowFor(fresh)!.HealthBadge.Content.ShouldBe("No traffic");
        hooks.Detail.ChartTitleText.ShouldBe("No traffic yet");
        split.SendTestButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(window);
        fresh.Deliveries.Count.ShouldBe(1);
        var test = fresh.Deliveries[0];
        test.IsTest.ShouldBeTrue();
        test.EventType.ShouldBe("shipment.created");
        hooks.SelectedDelivery.ShouldBeSameAs(test);
        fresh.DeliveredRate.ShouldBe(1.0);
        fresh.Health.ShouldBe(WebhookHealth.Healthy);
        fresh.Volume[^1].Total.ShouldBe(1);
        split.EmptyState.IsPresent.ShouldBeFalse();
        hooks.RowFor(fresh)!.Rate.Text.ShouldBe("100.0%");
        Focused(window).ShouldBeSameAs(split.RowFor(test)!.Button);
        split.Inspector.PayloadView!.Text.ShouldContain("\"live\": false");
        window.Close();
    }

    [AvaloniaFact]
    public void A_Failed_Test_Event_Shows_A_Failed_Delivery()
    {
        var hooks = Create(h => h.SendTestAction = (_, _) => Task.FromException<WebhookDelivery>(new HttpRequestException("Connection refused.")), withData: false);
        var fresh = WebhooksDemoData.NewEndpoint(Now);
        hooks.Endpoints.Add(fresh);
        var window = Show(hooks);
        WebhookActionFailedEventArgs? failed = null;
        hooks.ActionFailed += (_, e) => failed = e;
        hooks.SendTestAsync(fresh);
        Pump(window);
        var test = fresh.Deliveries.Single();
        test.IsFailure.ShouldBeTrue();
        test.IsTest.ShouldBeTrue();
        fresh.Health.ShouldBe(WebhookHealth.Failing);
        failed!.Action.ShouldBe(WebhookActionKind.SendTest);
        hooks.Announcer!.Announcement!.ShouldContain("Connection refused.");
        window.Close();
    }

    // ---- Live deliveries ----

    [AvaloniaFact]
    public void Appended_Deliveries_Grow_In_At_The_Top_And_Count_In_The_Chart()
    {
        var hooks = Create();
        var window = Show(hooks);
        var desk = hooks.Endpoints[1];
        hooks.SelectedEndpoint = desk;
        Pump(window);
        var lastTotal = desk.Volume[^1].Total;
        var delivery = WebhooksDemoData.LiveDelivery(Now, 3, "ticket.opened");
        hooks.AppendDelivery(desk, delivery);
        Pump(window);
        desk.Deliveries[0].ShouldBeSameAs(delivery);
        desk.Volume[^1].Total.ShouldBe(lastTotal + 1);
        desk.Volume[^1].Failed.ShouldBe(1);
        desk.Health.ShouldBe(WebhookHealth.Failing);
        hooks.Split!.RowFor(delivery).ShouldNotBeNull();
        window.Close();
    }

    // ---- Inspector ----

    [AvaloniaFact]
    public void The_Tab_Stays_When_The_Delivery_Changes_And_Copy_Follows_It()
    {
        var hooks = Create();
        var window = Show(hooks);
        var inspector = hooks.Split!.Inspector;
        inspector.Tabs.SelectedIndex.ShouldBe(0);
        inspector.CopyText.ShouldStartWith("{\n  \"id\": ");
        inspector.Tabs.SelectedIndex = 1;
        Pump(window);
        hooks.InspectorTab.ShouldBe(WebhookInspectorTab.Headers);
        inspector.CopyButton.Value!.ShouldContain("Content-Type: application/json; charset=utf-8");
        hooks.SelectedDelivery = Shipping(hooks).Deliveries[0];
        Pump(window);
        inspector.Tabs.SelectedIndex.ShouldBe(1);
        inspector.CopyText.ShouldContain("Corvid-Event: shipment.created");
        hooks.SelectedEndpoint = hooks.Endpoints[1];
        Pump(window);
        hooks.Split!.Inspector.Tabs.SelectedIndex.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Payload_Is_Indented_With_Line_Numbers()
    {
        var hooks = Create();
        var window = Show(hooks);
        var payload = hooks.Split!.Inspector.PayloadView!;
        payload.LineCount.ShouldBeGreaterThan(15);
        payload.Text.ShouldContain("    \"object\": {");
        window.Close();
    }

    [AvaloniaFact]
    public void The_Response_Swap_Never_Pushes_The_Tabs_Down()
    {
        // Arc stacks the old and new response body while they swap, pushing the tabs ≈22px down for 160ms. Ours overlap.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var hooks = Create();
        var window = Show(hooks, level: ArcMotionLevel.Full);
        Step(clock, window, 600);
        var inspector = hooks.Split!.Inspector;
        var deliveries = Shipping(hooks).Deliveries;
        hooks.SelectedDelivery = deliveries[2]; // one-line body, like the 500 before it
        Step(clock, window, 17);
        var during = inspector.Tabs.Bounds.Y;
        Step(clock, window, 80);
        inspector.Tabs.Bounds.Y.ShouldBe(during, 0.5);
        Step(clock, window, 1000);
        inspector.Tabs.Bounds.Y.ShouldBe(during, 0.5);
        window.Close();
    }

    // ---- Chart ----

    [AvaloniaFact]
    public void Bars_Scale_To_The_Busiest_Slice_And_Hovering_Reads_One_Out()
    {
        var hooks = Create();
        var window = Show(hooks);
        var strip = hooks.Detail.Strip;
        var volume = Shipping(hooks).Volume;
        var max = volume.Max(b => b.Total);
        var busiest = volume.IndexOf(volume.First(b => b.Total == max));
        strip.BarHeight(busiest).ShouldBe(56, 0.01);
        var failedIndex = volume.IndexOf(volume.First(b => b.Failed > 0));
        strip.CapHeight(failedIndex).ShouldBeGreaterThan(0);
        strip.Hover(failedIndex);
        Pump(window);
        var bucket = volume[failedIndex];
        hooks.Detail.ChartTitleText.ShouldStartWith($"{WebhookMath.FormatClock(bucket.Start)} to {WebhookMath.FormatClock(bucket.End)}");
        hooks.Detail.ChartTitleText.ShouldContain("1 failed");
        strip.BarOpacity(failedIndex).ShouldBe(1, 0.001);
        strip.BarOpacity(busiest == failedIndex ? 0 : busiest).ShouldBe(0.45, 0.001);
        strip.Hover(-1);
        Pump(window);
        hooks.Detail.ChartTitleText.ShouldContain("3 failed");
        strip.BarOpacity(busiest).ShouldBe(1, 0.001);
        window.Close();
    }

    [AvaloniaFact]
    public void Bars_Spring_With_A_Stagger_When_The_Endpoint_Changes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var hooks = Create();
        var window = Show(hooks, level: ArcMotionLevel.Full);
        Step(clock, window, 600);
        var strip = hooks.Detail.Strip;
        var before = Enumerable.Range(0, 48).Select(strip.BarHeight).ToArray();
        hooks.SelectedEndpoint = hooks.Endpoints[1];
        Step(clock, window, 100);
        strip.IsMoving.ShouldBeTrue();
        // Bar 47 starts ≈200ms after bar 0 (4.2ms per bar): at 100ms the first bars have gone further than the last.
        double Progress(int i) => Math.Abs(strip.TargetHeight(i) - before[i]) < 1
            ? double.NaN
            : (strip.BarHeight(i) - before[i]) / (strip.TargetHeight(i) - before[i]);
        var early = Enumerable.Range(0, 8).Select(Progress).Where(p => !double.IsNaN(p)).Average();
        var late = Enumerable.Range(40, 8).Select(Progress).Where(p => !double.IsNaN(p)).Average();
        early.ShouldBeGreaterThan(late);
        Step(clock, window, 1500);
        strip.IsMoving.ShouldBeFalse();
        strip.BarHeight(47).ShouldBe(strip.TargetHeight(47), 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Rate_Swaps_Only_The_Changed_Slots()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var text = new WebhookSlotText { FontSize = 14, Text = "98.9%" };
        var window = Show(text, 400, 200, ArcMotionLevel.Full);
        Step(clock, window, 100);
        text.Text = "99.3%";
        Step(clock, window, 16);
        text.IsMoving.ShouldBeTrue();
        Step(clock, window, 800);
        text.IsMoving.ShouldBeFalse();
        text.Text = "100.0%";
        text.DesiredSize.Width.ShouldBeGreaterThan(0);
        window.Close();
    }

    // ---- Layout ----

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        var hooks = Create();
        var host = new Border { Child = hooks, Width = 1200 };
        var window = Show(host, 1300);
        hooks.IsStacked.ShouldBeFalse();
        hooks.IsNarrow.ShouldBeFalse();
        hooks.RowFor(Shipping(hooks))!.Chips.IsEffectivelyVisible.ShouldBeTrue();
        hooks.Gutter.ShouldBe(20);

        host.Width = 860;
        Pump(window);
        hooks.IsStacked.ShouldBeTrue();
        hooks.IsNarrow.ShouldBeFalse();
        hooks.Split!.Inspector.Bounds.Y.ShouldBeGreaterThan(0);

        host.Width = 700;
        Pump(window);
        hooks.IsNarrow.ShouldBeTrue();
        TopLevel.GetTopLevel(hooks.RowFor(Shipping(hooks))!.Chips).ShouldBeNull();

        host.Width = 420;
        Pump(window);
        hooks.Gutter.ShouldBe(16);
        hooks.Note.GetVisualParent()!.IsVisible.ShouldBeFalse();
        hooks.Split!.RowFor(Shipping(hooks).Deliveries[0])!.IsTwoLine.ShouldBeTrue();
        hooks.OpenAddPanel();
        Pump(window);
        hooks.Form.IsSingleColumn.ShouldBeTrue();
        hooks.Form.EventColumns.ShouldBe(1);

        host.Width = 780;
        Pump(window);
        hooks.Form.EventColumns.ShouldBe(2);
        host.Width = 1200;
        Pump(window);
        hooks.Form.EventColumns.ShouldBe(3);
        hooks.Split!.RowFor(Shipping(hooks).Deliveries[0])!.IsTwoLine.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Custom_Labels_Are_Used()
    {
        var hooks = Create(h => h.Labels = new WebhooksLabels
        {
            EndpointsWord = n => n == 1 ? "Ziel" : "Ziele",
            HealthyBadge = "Gesund",
            AddEndpoint = "Ziel hinzufügen",
        });
        var window = Show(hooks);
        hooks.RowFor(hooks.Endpoints[1])!.HealthBadge.Content.ShouldBe("Gesund");
        hooks.AddPanel!.Title.ShouldBe("Ziel hinzufügen");
        window.Close();
    }
}

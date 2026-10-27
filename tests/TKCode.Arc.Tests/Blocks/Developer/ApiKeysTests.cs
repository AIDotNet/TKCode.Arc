using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// <see cref="ApiKeys"/> on invented samples (five keys, four scopes): the list and its texts, the create panel (reset,
/// validation, generate success, failure and retry, cancel), the one-time reveal, the accordion, revoke (success, failure,
/// cancel, focus afterwards), the empty state, breakpoints, an observed source, names for screen readers and motion.
/// </summary>
public class ApiKeysTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static ApiKeyItem Key(string key, string name, ApiKeyEnvironment env, string lastFour, DateTimeOffset? used,
        string? creator, long requests, params string[] scopes)
    {
        var item = new ApiKeyItem
        {
            Key = key,
            Name = name,
            Environment = env,
            Prefix = env == ApiKeyEnvironment.Live ? "rk_live_" : "rk_test_",
            LastFour = lastFour,
            LastUsedAt = used,
            CreatedAt = Now.AddDays(-60),
            CreatedBy = creator,
            RequestsLast30Days = requests,
        };
        item.Scopes.AddRange(scopes);
        return item;
    }

    private static ApiKeyItem[] Samples() =>
    [
        Key("checkout", "Checkout service", ApiKeyEnvironment.Live, "7c3e", Now.AddMinutes(-4), "Lena Ortiz", 942317,
            "orders:write", "accounts:read", "refunds:write"),
        Key("payout", "Payout runner", ApiKeyEnvironment.Live, "a19b", Now.AddHours(-2), "Kenji Mori", 58204,
            "refunds:write", "exports:read"),
        Key("sandbox", "Sandbox", ApiKeyEnvironment.Test, "4d0f", Now.AddDays(-1), "Amara Nwosu", 3611,
            "orders:write", "accounts:read"),
        Key("ledger", "Ledger sync", ApiKeyEnvironment.Live, "e852", Now.AddDays(-37), "Pavel Novak", 0, "exports:read"),
        Key("qa", "QA harness", ApiKeyEnvironment.Test, "0b6d", null, "Hana Kim", 0,
            "orders:write", "accounts:read", "refunds:write", "exports:read"),
    ];

    private static ApiKeyScope[] Scopes() =>
    [
        new() { Name = "orders:write", Description = "Create, update and cancel orders" },
        new() { Name = "accounts:read", Description = "Read account profiles and contacts", IsDefault = true },
        new() { Name = "refunds:write", Description = "Issue and reverse refunds" },
        new() { Name = "exports:read", Description = "Download ledger and settlement exports" },
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1000, Action<ApiKeys>? configure = null,
            bool samples = true)
        {
            Block = new ApiKeys
            {
                Description = "Keys let your backend call the Northwind Relay API.",
                ReferenceTime = Now,
                RevokeExitDelay = TimeSpan.Zero,
            };
            if (samples)
            {
                Block.Keys.AddRange(Samples());
            }
            Block.Scopes.AddRange(Scopes());
            configure?.Invoke(Block);
            Block.KeyCreated += (_, e) => Created.Add(e);
            Block.KeyCreateFailed += (_, e) => CreateFailures.Add(e);
            Block.KeyRevoked += (_, e) => Revoked.Add(e);
            Block.KeyRevokeFailed += (_, e) => RevokeFailures.Add(e);
            Block.PanelStateChanged += (_, e) => States.Add(e);
            Block.ExpandedKeyChanged += (_, e) => Expansions.Add(e);
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1200;
            Window.Height = 1400;
            Pump();
        }

        public ApiKeys Block { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<ApiKeyCreatedEventArgs> Created { get; } = new();

        public List<ApiKeyCreateFailedEventArgs> CreateFailures { get; } = new();

        public List<ApiKeyRevokedEventArgs> Revoked { get; } = new();

        public List<ApiKeyRevokeFailedEventArgs> RevokeFailures { get; } = new();

        public List<ApiKeysPanelStateChangedEventArgs> States { get; } = new();

        public List<ApiKeyExpandedEventArgs> Expansions { get; } = new();

        public DeveloperMorphPanel Create => Block.CreatePanel!;

        public ApiKeyForm Form => Block.Form;

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public ApiKeyRow Row(string key) => Block.RowFor(Block.EffectiveKeys.First(k => k.Key == key))!;

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public void Click(Avalonia.Controls.Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }

        public void OpenForm()
        {
            Click(Create.Toggle!);
            Block.PanelState.ShouldBe(ApiKeysPanelState.Form);
        }

        public void FillForm(string name)
        {
            Form.NameInput!.Text = name;
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    private static ApiKeyCreated Created(ApiKeyRequest request, string secret)
    {
        var item = new ApiKeyItem
        {
            Key = "new",
            Name = request.Name,
            Environment = request.Environment,
            Prefix = "rk_test_",
            LastFour = secret[^4..],
            CreatedAt = Now,
            CreatedBy = "Lena Ortiz",
        };
        item.Scopes.AddRange(request.Scopes);
        return new ApiKeyCreated(item, secret);
    }

    // ---- List ----

    [AvaloniaFact]
    public void Lists_The_Keys_In_Order_With_Count_Badges_Masks_And_Dates()
    {
        using var rig = new Rig();
        rig.Block.ActiveCount.ShouldBe(5);
        rig.Block.EffectiveKeys.Select(k => k.Key).ShouldBe(["checkout", "payout", "sandbox", "ledger", "qa"]);
        rig.Block.CountSwap!.Content.ShouldBe("5");
        TestHelpers.Descendant<TextBlock>(rig.Block, ApiKeys.CountWordsPart).Text.ShouldBe("active keys");
        AutomationProperties.GetName(TestHelpers.Descendant<StackPanel>(rig.Block, ApiKeys.CountGroupPart)).ShouldBe("5 active keys");

        var checkout = rig.Row("checkout");
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.MaskPart).Text.ShouldBe("rk_live_••••7c3e");
        TestHelpers.Descendant<Badge>(checkout, ApiKeyRow.BadgePart).Tone.ShouldBe(BadgeTone.Success);
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.UsedPart).Text.ShouldBe("Used 4 min ago");
        TestHelpers.Descendant<WrapPanel>(checkout, ApiKeyRow.ScopesPart).Children.Count.ShouldBe(3);
        TestHelpers.Descendant<Badge>(rig.Row("sandbox"), ApiKeyRow.BadgePart).Tone.ShouldBe(BadgeTone.Neutral);
        TestHelpers.Descendant<TextBlock>(rig.Row("sandbox"), ApiKeyRow.UsedPart).Text.ShouldBe("Used yesterday");

        // Idle for 37 days: the warning icon, and its details say so.
        var ledger = rig.Row("ledger");
        ledger.Classes.Contains(":stale").ShouldBeTrue();
        TestHelpers.Descendant<Control>(ledger, ApiKeyRow.StaleIconPart).IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(ledger, ApiKeyRow.NoteTextPart).Text.ShouldBe("Unused for 37 days. Revoke it if nothing relies on it.");
        TestHelpers.Descendant<Control>(checkout, ApiKeyRow.StaleIconPart).IsVisible.ShouldBeFalse();

        var qa = rig.Row("qa");
        TestHelpers.Descendant<TextBlock>(qa, ApiKeyRow.UsedPart).Text.ShouldBe("Never used");
        TestHelpers.Descendant<TextBlock>(qa, ApiKeyRow.LastRequestPart).Text.ShouldBe("No requests yet");

        // The first row has no line above it; the others do.
        checkout.Classes.Contains(":first").ShouldBeTrue();
        rig.Row("payout").Classes.Contains(":first").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void One_Key_Reads_Active_Key_In_The_Singular()
    {
        using var rig = new Rig(samples: false, configure: b => b.Keys.Add(Samples()[0]));
        TestHelpers.Descendant<TextBlock>(rig.Block, ApiKeys.CountWordsPart).Text.ShouldBe("active key");
        rig.Block.ActiveCount.ShouldBe(1);
    }

    [Fact]
    public void Default_Ago_Covers_Minutes_Hours_Yesterday_And_Days()
    {
        ApiKeyLabels.DefaultAgo(TimeSpan.FromSeconds(20)).ShouldBe("just now");
        ApiKeyLabels.DefaultAgo(TimeSpan.FromMinutes(4)).ShouldBe("4 min ago");
        ApiKeyLabels.DefaultAgo(TimeSpan.FromHours(1.5)).ShouldBe("1 hour ago");
        ApiKeyLabels.DefaultAgo(TimeSpan.FromHours(5)).ShouldBe("5 hours ago");
        ApiKeyLabels.DefaultAgo(TimeSpan.FromHours(30)).ShouldBe("yesterday");
        ApiKeyLabels.DefaultAgo(TimeSpan.FromDays(37)).ShouldBe("37 days ago");
        new ApiKeyItem { Prefix = "rk_test_", LastFour = "0b6d" }.MaskedText.ShouldBe("rk_test_••••0b6d");
    }

    // ---- Create panel ----

    [AvaloniaFact]
    public void Opening_Resets_The_Form_And_Focuses_The_Name()
    {
        using var rig = new Rig(configure: b => b.DefaultEnvironment = ApiKeyEnvironment.Test);
        rig.OpenForm();
        rig.Block.IsCreateOpen.ShouldBeTrue();
        rig.Focused.ShouldBe(rig.Form.NameInput);
        rig.Form.ScopeBoxes.Select(b => b.IsChecked == true).ShouldBe([false, true, false, false]);
        rig.Form.Environment.ShouldBe(ApiKeyEnvironment.Test);
        AutomationProperties.GetName(rig.Create.Toggle!).ShouldBe("Close");

        // Change everything, cancel, open again: a fresh form.
        rig.FillForm("Draft");
        rig.Form.EnvironmentSwitch!.SelectedIndex = 1;
        rig.Form.ScopeBoxes[0].IsChecked = true;
        rig.Click(rig.Form.CancelButton!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);
        rig.States.Last().CloseReason.ShouldBe(ApiKeysCloseReason.Cancel);
        rig.Focused.ShouldBe(rig.Create.Toggle);
        rig.OpenForm();
        rig.Form.NameText.ShouldBe(string.Empty);
        rig.Form.Environment.ShouldBe(ApiKeyEnvironment.Test);
        rig.Form.ScopeBoxes.Select(b => b.IsChecked == true).ShouldBe([false, true, false, false]);
    }

    [AvaloniaFact]
    public void An_Empty_Name_Then_No_Scope_Block_The_Submit_With_Errors()
    {
        var calls = 0;
        using var rig = new Rig(configure: b => b.GenerateAction = (r, _) => { calls++; return Task.FromResult(Created(r, "rk_test_" + new string('x', 36) + "9f0a")); });
        rig.OpenForm();
        rig.FillForm("   ");
        rig.Click(rig.Form.GenerateButton!);
        rig.Form.HasNameError.ShouldBeTrue();
        rig.Focused.ShouldBe(rig.Form.NameInput);
        calls.ShouldBe(0);

        // Typing clears the error; then no scope at all.
        rig.FillForm("Order worker");
        rig.Form.HasNameError.ShouldBeFalse();
        foreach (var box in rig.Form.ScopeBoxes)
        {
            box.IsChecked = false;
        }
        rig.Click(rig.Form.GenerateButton!);
        rig.Form.HasScopeError.ShouldBeTrue();
        rig.Focused.ShouldBe(rig.Form.ScopeBoxes[0]);
        calls.ShouldBe(0);
        rig.Form.ScopeBoxes[2].IsChecked = true;
        rig.Pump();
        rig.Form.HasScopeError.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Generating_Lists_The_Key_On_Top_Shows_The_Secret_Once_And_Announces_It()
    {
        ApiKeyRequest? seen = null;
        const string secret = "rk_" + "live_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789wQ7h";
        using var rig = new Rig(configure: b => b.GenerateAction = (r, _) => { seen = r; return Task.FromResult(Created(r, secret)); });
        rig.OpenForm();
        rig.FillForm("  Order worker  ");
        rig.Form.EnvironmentSwitch!.SelectedIndex = 1;
        rig.Form.ScopeBoxes[0].IsChecked = true;
        rig.Pump();
        rig.Form.NameInput!.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();

        seen.ShouldNotBeNull();
        seen.Name.ShouldBe("Order worker");
        seen.Environment.ShouldBe(ApiKeyEnvironment.Live);
        seen.Scopes.ShouldBe(["orders:write", "accounts:read"]);
        seen.Attempt.ShouldBe(1);

        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Reveal);
        rig.Create.Title.ShouldBe("Key created");
        rig.Create.Content.ShouldBe(rig.Block.Reveal);
        var item = rig.Block.EffectiveKeys[0];
        item.Name.ShouldBe("Order worker");
        item.IsFresh.ShouldBeTrue();
        rig.Block.Keys[0].ShouldBe(item);
        rig.Block.ActiveCount.ShouldBe(6);
        rig.Block.CountSwap!.Content.ShouldBe("6");
        rig.Block.List!.StateOf(item)!.Value.Flash.ShouldBeGreaterThan(0);
        rig.Block.Reveal.SecretField!.HasSecret.ShouldBeTrue();
        rig.Created.Single().Item.ShouldBe(item);
        rig.Block.Announcer!.Announcement.ShouldBe("Order worker created. Copy the key before closing.");
        // The focus left with the form and landed on the first stop of the reveal (Copy).
        rig.Block.Reveal.IsKeyboardFocusWithin.ShouldBeTrue();

        rig.Click(rig.Block.Reveal.DoneButton!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);
        rig.States.Last().CloseReason.ShouldBe(ApiKeysCloseReason.Done);
        item.IsFresh.ShouldBeFalse();
        rig.Block.Reveal.SecretField!.HasSecret.ShouldBeFalse();
        rig.Create.Title.ShouldBe("Create key");
        rig.Focused.ShouldBe(rig.Create.Toggle);
        // Once hidden, the panel holds the form again for the next open.
        rig.Create.Content.ShouldBe(rig.Form);
    }

    [AvaloniaFact]
    public void A_Failed_Generate_Keeps_The_Form_With_The_Error_And_A_Retry_Succeeds()
    {
        var attempts = new List<int>();
        using var rig = new Rig(configure: b => b.GenerateAction = (r, _) =>
        {
            attempts.Add(r.Attempt);
            return r.Attempt == 1
                ? Task.FromException<ApiKeyCreated>(new InvalidOperationException("The key service is busy. Try again."))
                : Task.FromResult(Created(r, "rk_test_" + new string('k', 36) + "a1b2"));
        });
        rig.OpenForm();
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);

        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Form);
        rig.Block.GenerateError.ShouldBe("The key service is busy. Try again.");
        rig.Form.GenerateError.ShouldBe("The key service is busy. Try again.");
        rig.Form.NameText.ShouldBe("Order worker");
        rig.CreateFailures.Single().Request.Attempt.ShouldBe(1);
        rig.Block.ActiveCount.ShouldBe(5);
        rig.Block.Announcer!.Announcement.ShouldBe("The key service is busy. Try again.");
        rig.Form.GenerateButton!.IsLoading.ShouldBeFalse();

        rig.Click(rig.Form.GenerateButton!);
        attempts.ShouldBe([1, 2]);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Reveal);
        rig.Block.GenerateError.ShouldBeNull();
        rig.Block.ActiveCount.ShouldBe(6);
    }

    [AvaloniaFact]
    public void Escape_While_Generating_Cancels_The_Request_And_Stays_In_The_Form()
    {
        CancellationToken token = default;
        using var rig = new Rig(configure: b => b.GenerateAction = async (r, t) =>
        {
            token = t;
            await Task.Delay(Timeout.Infinite, t);
            return Created(r, "never");
        });
        rig.OpenForm();
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Generating);
        rig.Form.GenerateButton!.IsLoading.ShouldBeTrue();
        rig.Form.NameInput!.IsReadOnly.ShouldBeTrue();
        rig.Form.CancelButton!.IsEnabled.ShouldBeFalse();

        rig.Form.GenerateButton.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        TestHelpers.PumpUntil(() => rig.Block.PanelState == ApiKeysPanelState.Form).ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeTrue();
        rig.Create.IsOpen.ShouldBeTrue();
        rig.Block.GenerateError.ShouldBeNull();
        rig.Form.NameInput.IsReadOnly.ShouldBeFalse();
        rig.Block.ActiveCount.ShouldBe(5);

        // A second Escape closes the panel as usual.
        rig.Form.NameInput.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        rig.Pump();
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);
        rig.States.Last().CloseReason.ShouldBe(ApiKeysCloseReason.Escape);
    }

    [AvaloniaFact]
    public void Without_A_Generate_Action_A_Server_Is_Simulated()
    {
        using var rig = new Rig(configure: b => { b.CurrentUserName = "Lena Ortiz"; b.SimulatedPrefixFormat = "rk_{0}_"; });
        rig.OpenForm();
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Generating);
        TestHelpers.PumpUntil(() => rig.Block.PanelState == ApiKeysPanelState.Reveal).ShouldBeTrue();
        var item = rig.Block.EffectiveKeys[0];
        item.Prefix.ShouldBe("rk_test_");
        item.LastFour.Length.ShouldBe(4);
        item.CreatedBy.ShouldBe("Lena Ortiz");
        item.CreatedAt.ShouldBe(Now);
        item.Scopes.ShouldBe(["accounts:read"]);
    }

    [AvaloniaFact]
    public void Closing_With_The_Toggle_During_A_Reveal_Clears_The_Secret()
    {
        using var rig = new Rig(configure: b => b.GenerateAction = (r, _) => Task.FromResult(Created(r, "rk_test_" + new string('s', 36) + "c0de")));
        rig.OpenForm();
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);
        rig.Click(rig.Create.Toggle!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);
        rig.States.Last().CloseReason.ShouldBe(ApiKeysCloseReason.Toggle);
        rig.Block.Reveal.SecretField!.HasSecret.ShouldBeFalse();
        rig.Block.EffectiveKeys[0].IsFresh.ShouldBeFalse();
        rig.Block.IsCreateOpen.ShouldBeFalse();

        // The two-way property drives it as well.
        rig.Block.IsCreateOpen = true;
        rig.Pump();
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Form);
        rig.Block.IsCreateOpen = false;
        rig.Pump();
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);
        rig.States.Last().CloseReason.ShouldBe(ApiKeysCloseReason.Programmatic);
    }

    [AvaloniaFact]
    public void The_Closed_Pill_Opens_From_Its_Text_As_Well()
    {
        using var rig = new Rig();
        var shell = rig.Create.Shell!;
        // A press on the title half of the pill (Arc reacts only on the round toggle).
        var point = shell.TranslatePoint(new Point(shell.Bounds.Width - 20, shell.Bounds.Height / 2), rig.Window)!.Value;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        rig.Window.MouseDown(point, MouseButton.Left);
        rig.Window.MouseUp(point, MouseButton.Left);
        rig.Pump();
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Form);
    }

    // ---- Accordion ----

    [AvaloniaFact]
    public void Rows_Open_One_At_A_Time()
    {
        using var rig = new Rig();
        var checkout = rig.Row("checkout");
        checkout.IsDetailShown.ShouldBeFalse();
        checkout.Trigger!.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Block.ExpandedKey!.Key.ShouldBe("checkout");
        checkout.IsExpanded.ShouldBeTrue();
        checkout.IsDetailShown.ShouldBeTrue();
        checkout.Classes.Contains(":expanded").ShouldBeTrue();
        rig.Block.Announcer!.Announcement.ShouldBe("Checkout service: details shown");
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.RequestsPart).Text.ShouldBe(DeveloperText.Thousands(942317));
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.CreatedByPart).Text.ShouldBe("Lena Ortiz");

        rig.Click(rig.Row("payout").Trigger!);
        rig.Block.ExpandedKey!.Key.ShouldBe("payout");
        checkout.IsExpanded.ShouldBeFalse();
        checkout.IsDetailShown.ShouldBeFalse();
        rig.Expansions.Last().OldItem!.Key.ShouldBe("checkout");

        rig.Click(rig.Row("payout").Trigger!);
        rig.Block.ExpandedKey.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Trigger_Is_An_Expand_Collapse_Button_With_An_Inner_Focus_Ring()
    {
        using var rig = new Rig();
        var trigger = rig.Row("checkout").Trigger!;
        var peer = ControlAutomationPeer.CreatePeerForElement(trigger);
        var provider = peer.GetProvider<IExpandCollapseProvider>()!;
        provider.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        provider.Expand();
        rig.Pump();
        rig.Block.ExpandedKey!.Key.ShouldBe("checkout");
        provider.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        AutomationProperties.GetName(trigger)!.ShouldContain("Key ending in 7c3e");

        // The ring sits inside the trigger: a clipping row cannot cut it off.
        var ring = TestHelpers.Descendant<Border>(trigger, "PART_FocusRing");
        ring.Margin.Left.ShouldBeGreaterThanOrEqualTo(0);
        trigger.Focus(NavigationMethod.Tab);
        rig.Pump();
        ring.IsVisible.ShouldBeTrue();
        ring.Bounds.Width.ShouldBeLessThan(trigger.Bounds.Width);
    }

    // ---- Revoke ----

    [AvaloniaFact]
    public void A_Confirmed_Revoke_Strikes_The_Row_Removes_It_And_Moves_The_Focus_To_The_Next_Row()
    {
        // The server answers only when the test says so: the struck-through state can be checked without a race.
        var server = new TaskCompletionSource();
        using var rig = new Rig(configure: b => b.RevokeAction = (_, _) => server.Task);
        rig.Block.Expand(rig.Block.EffectiveKeys[1]);
        rig.Pump();
        var payout = rig.Row("payout");
        var hold = payout.RevokeButton!;
        hold.Focus(NavigationMethod.Tab);
        hold.IsConfirmed = true;
        hold.RaiseEvent(new RoutedEventArgs(HoldToConfirm.ConfirmedEvent));
        rig.Pump();

        var item = payout.Item;
        item.IsRevoked.ShouldBeTrue();
        payout.Classes.Contains(":revoked").ShouldBeTrue();
        payout.NameStrike!.Progress.ShouldBe(1);
        rig.Block.ActiveCount.ShouldBe(5);

        server.SetResult();
        TestHelpers.PumpUntil(() => rig.Revoked.Count == 1).ShouldBeTrue();
        rig.Pump();
        rig.Revoked[0].Index.ShouldBe(1);
        rig.Block.Keys.ShouldNotContain(item);
        rig.Block.ActiveCount.ShouldBe(4);
        rig.Block.CountSwap!.Content.ShouldBe("4");
        rig.Block.ExpandedKey.ShouldBeNull();
        rig.Block.Announcer!.Announcement.ShouldBe("Payout runner has been revoked.");
        rig.Focused.ShouldBe(rig.Row("sandbox").Trigger);
    }

    [AvaloniaFact]
    public async Task Revoking_The_Last_Row_Focuses_The_Previous_One_And_The_Only_Row_Focuses_Create_Key()
    {
        using var rig = new Rig();
        var qa = rig.Row("qa");
        rig.Block.Expand(qa.Item);
        rig.Pump();
        qa.RevokeButton!.Focus(NavigationMethod.Tab);
        (await rig.Block.RevokeAsync(qa.Item)).ShouldBeTrue();
        rig.Pump();
        rig.Focused.ShouldBe(rig.Row("ledger").Trigger);

        foreach (var key in new[] { "checkout", "payout", "sandbox" })
        {
            (await rig.Block.RevokeAsync(rig.Block.EffectiveKeys.First(k => k.Key == key))).ShouldBeTrue();
        }
        rig.Pump();
        var ledger = rig.Row("ledger");
        ledger.Trigger!.Focus(NavigationMethod.Tab);
        (await rig.Block.RevokeAsync(ledger.Item)).ShouldBeTrue();
        rig.Pump();
        rig.Focused.ShouldBe(rig.Create.Toggle);
        rig.Block.ActiveCount.ShouldBe(0);
        rig.Block.EmptyState!.IsVisible.ShouldBeTrue();
        rig.Block.Classes.Contains(":empty").ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task A_Failed_Revoke_Restores_The_Row_And_Announces_It()
    {
        using var rig = new Rig(configure: b => b.RevokeAction = (_, _) => Task.FromException(new InvalidOperationException("The key service did not answer.")));
        var ledger = rig.Row("ledger");
        (await rig.Block.RevokeAsync(ledger.Item)).ShouldBeFalse();
        rig.Pump();
        ledger.Item.IsRevoked.ShouldBeFalse();
        ledger.Classes.Contains(":revoked").ShouldBeFalse();
        ledger.NameStrike!.Progress.ShouldBe(0);
        ledger.RevokeButton!.IsConfirmed.ShouldBeFalse();
        rig.Block.ActiveCount.ShouldBe(5);
        rig.RevokeFailures.Single().Item.ShouldBe(ledger.Item);
        rig.Block.Announcer!.Announcement.ShouldBe("Ledger sync is still active. The key service did not answer.");
    }

    [AvaloniaFact]
    public async Task A_Cancelled_Revoking_Event_Keeps_The_Key()
    {
        using var rig = new Rig();
        rig.Block.KeyRevoking += (_, e) => e.Cancel = e.Item.Key == "checkout";
        var checkout = rig.Row("checkout");
        (await rig.Block.RevokeAsync(checkout.Item)).ShouldBeFalse();
        checkout.Item.IsRevoked.ShouldBeFalse();
        rig.Block.ActiveCount.ShouldBe(5);
    }

    // ---- Empty state ----

    [AvaloniaFact]
    public void The_Empty_State_Shows_Its_Button_And_Opens_The_Form_Unless_Handled()
    {
        using var rig = new Rig(samples: false, configure: b => b.EmptyActionText = "Bring back sample keys");
        rig.Block.EmptyState!.IsVisible.ShouldBeTrue();
        rig.Block.EmptyActionButton!.IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(rig.Block, ApiKeys.EmptyTitlePart).Text.ShouldBe("No active keys");
        rig.Block.CountSwap!.Content.ShouldBe("0");

        var invoked = 0;
        void Restore(object? sender, RoutedEventArgs e)
        {
            invoked++;
            e.Handled = true;
            rig.Block.Keys.AddRange(Samples());
        }
        rig.Block.EmptyActionInvoked += Restore;
        rig.Click(rig.Block.EmptyActionButton);
        invoked.ShouldBe(1);
        rig.Block.ActiveCount.ShouldBe(5);
        rig.Block.EmptyState.IsVisible.ShouldBeFalse();
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Closed);

        rig.Block.EmptyActionInvoked -= Restore;
        rig.Block.Keys.Clear();
        rig.Pump();
        rig.Block.EmptyState.IsVisible.ShouldBeTrue();
        rig.Click(rig.Block.EmptyActionButton);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Form);
    }

    // ---- Sources and layout ----

    [AvaloniaFact]
    public void An_Observed_Source_Receives_New_Keys_And_Loses_Revoked_Ones()
    {
        var source = new ObservableCollection<ApiKeyItem>(Samples());
        using var rig = new Rig(samples: false, configure: b =>
        {
            b.KeysSource = source;
            b.GenerateAction = (r, _) => Task.FromResult(Created(r, "rk_test_" + new string('o', 36) + "beef"));
        });
        rig.Block.ActiveCount.ShouldBe(5);
        source.Add(Key("extra", "Batch importer", ApiKeyEnvironment.Test, "77aa", null, null, 0, "orders:write"));
        rig.Pump();
        rig.Block.ActiveCount.ShouldBe(6);
        TestHelpers.Descendant<TextBlock>(rig.Row("extra"), ApiKeyRow.CreatedByPart).Text.ShouldBe("Unknown");

        rig.OpenForm();
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);
        source[0].Name.ShouldBe("Order worker");
        rig.Block.ActiveCount.ShouldBe(7);
    }

    [AvaloniaFact]
    public async Task A_Revoked_Key_In_A_Read_Only_Source_Stays_Hidden()
    {
        var source = Samples();
        using var rig = new Rig(samples: false, configure: b => b.KeysSource = source);
        (await rig.Block.RevokeAsync(source[0])).ShouldBeTrue();
        rig.Pump();
        rig.Block.EffectiveKeys.Count.ShouldBe(4);
        rig.Block.EffectiveKeys.ShouldNotContain(source[0]);
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        using var rig = new Rig(width: 1000);
        var checkout = rig.Row("checkout");
        rig.Block.Classes.Contains(":narrow").ShouldBeFalse();
        TestHelpers.Descendant<WrapPanel>(checkout, ApiKeyRow.ScopesPart).IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.CreatedPart).IsVisible.ShouldBeTrue();

        rig.Host.Width = 640;
        rig.Pump();
        rig.Block.Classes.Contains(":narrow").ShouldBeTrue();
        rig.Block.Classes.Contains(":compact").ShouldBeFalse();
        TestHelpers.Descendant<WrapPanel>(checkout, ApiKeyRow.ScopesPart).IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<TextBlock>(checkout, ApiKeyRow.CreatedPart).IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Control>(checkout, ApiKeyRow.ScopesFactPart).IsVisible.ShouldBeTrue();

        rig.Host.Width = 390;
        rig.Pump();
        rig.Block.Classes.Contains(":compact").ShouldBeTrue();
        checkout.Classes.Contains(":compact").ShouldBeTrue();
        rig.OpenForm();
        rig.Pump();
        rig.Form.IsStacked.ShouldBeTrue();
        rig.Form.Classes.Contains(":stacked").ShouldBeTrue();

        rig.Host.Width = 1000;
        rig.Pump();
        rig.Form.IsStacked.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Labels_Reword_Every_Text()
    {
        using var rig = new Rig(configure: b => b.Labels = new ApiKeyLabels
        {
            CreateKey = "New key",
            CountWords = n => n == 1 ? "key" : "keys",
            NeverUsed = "Unused",
        });
        rig.Create.Title.ShouldBe("New key");
        TestHelpers.Descendant<TextBlock>(rig.Block, ApiKeys.CountWordsPart).Text.ShouldBe("keys");
        TestHelpers.Descendant<TextBlock>(rig.Row("qa"), ApiKeyRow.UsedPart).Text.ShouldBe("Unused");
    }

    // ---- Motion ----

    [AvaloniaFact]
    public void Full_Motion_Creates_Opens_And_Revokes_On_A_Frame_Clock()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, configure: b => b.GenerateAction = (r, _) => Task.FromResult(Created(r, "rk_test_" + new string('m', 36) + "f00d")));
        void Step(double ms)
        {
            for (var i = 0; i < (int)Math.Ceiling(ms / (1000.0 / 60.0)); i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60.0));
                rig.Window.UpdateLayout();
            }
            Dispatcher.UIThread.RunJobs();
        }

        rig.OpenForm();
        Step(1200);
        rig.FillForm("Order worker");
        rig.Click(rig.Form.GenerateButton!);
        rig.Block.PanelState.ShouldBe(ApiKeysPanelState.Reveal);
        var item = rig.Block.EffectiveKeys[0];
        // The new row grows from nothing on Smooth.
        Step(50);
        rig.Block.List!.StateOf(item)!.Value.HeightFactor.ShouldBeLessThan(1);
        Step(1200);
        rig.Block.List.StateOf(item)!.Value.HeightFactor.ShouldBe(1, 0.01);
        rig.Block.List.StateOf(item)!.Value.Flash.ShouldBe(1, 0.01);

        rig.Click(rig.Block.Reveal.DoneButton!);
        Step(2000);
        rig.Block.List.StateOf(item)!.Value.Flash.ShouldBe(0, 0.01);

        var row = rig.Row("checkout");
        rig.Block.Expand(row.Item);
        rig.Pump();
        Step(900);
        row.IsDetailShown.ShouldBeTrue();
        _ = rig.Block.RevokeAsync(row.Item);
        rig.Pump();
        Step(200);
        row.NameStrike!.Progress.ShouldBeInRange(0.01, 0.99);
        TestHelpers.PumpUntil(() => rig.Revoked.Count == 1, 5000).ShouldBeTrue();
        Step(1500);
        rig.Block.List.ExitingCount.ShouldBe(0);
        rig.Block.ActiveCount.ShouldBe(5);
    }
}

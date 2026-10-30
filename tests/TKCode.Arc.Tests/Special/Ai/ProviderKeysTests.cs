using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// Provider keys: the sample workspace and the empty state, masking, the format check, reveal with its countdown, copy
/// names, retest, add and rotate through the inline form (format errors, Testing, Connected, rejection, cancel), remove
/// with undo, events and the keys written back, the narrow layout, loading rows and the live region.
/// </summary>
public class ProviderKeysTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The test's own invented workspace: made-up providers and placeholder keys.</summary>
    private static class Sample
    {
        public static IReadOnlyList<ProviderKeyProvider> Providers { get; } =
        [
            new ProviderKeyProvider { Id = "northwind", Name = "Northwind AI", KeyPrefix = "nw-", MinimumLength = 24, Mark = ArcIcons.Sparkles },
            new ProviderKeyProvider { Id = "contoso", Name = "Contoso Labs", KeyPrefix = "csk-", MinimumLength = 24, Mark = ArcIcons.Brain },
            new ProviderKeyProvider { Id = "fabrikam", Name = "Fabrikam", KeyPrefix = "fab_", MinimumLength = 24, Mark = ArcIcons.Cpu },
        ];

        public static List<ProviderKey> Keys(DateTimeOffset now) =>
        [
            new ProviderKey
            {
                Provider = "northwind",
                Status = ProviderKeyStatus.Connected,
                Value = "nw-test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a",
                LastUsedAt = now - TimeSpan.FromMinutes(2),
                CheckedAt = now - TimeSpan.FromHours(1),
            },
            new ProviderKey
            {
                Provider = "contoso",
                Status = ProviderKeyStatus.Invalid,
                Value = "csk-test-0000000000000000000000009c1d",
                LastUsedAt = now - TimeSpan.FromDays(3),
                CheckedAt = now - TimeSpan.FromMinutes(20),
                Error = "401 Unauthorized",
            },
        ];
    }

    /// <summary>Opens a card on <see cref="Sample"/> with instant simulated tests, unless <paramref name="empty"/>.</summary>
    private static (ProviderKeys Card, Window Window) Open(Action<ProviderKeys>? setup = null, double width = 720, bool empty = false)
    {
        var card = new ProviderKeys
        {
            Now = () => s_now,
            DelayAsync = static (_, _) => Task.CompletedTask,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        if (!empty)
        {
            card.Providers = Sample.Providers;
            card.Keys = Sample.Keys(s_now);
            card.Simulation = new ProviderKeysSimulation { Delay = TimeSpan.Zero };
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Wait(Task task) => TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();

    private static ProviderKeysRow Row(ProviderKeys card, string id) => card.RowOf(id)!;

    private static T Part<T>(ProviderKeysRow row, string name) where T : Control => TestHelpers.Descendant<T>(row, name);

    [Theory]
    [InlineData("", ProviderKeyFormat.Empty)]
    [InlineData("n", ProviderKeyFormat.TooShort)]
    [InlineData("nw-", ProviderKeyFormat.TooShort)]
    [InlineData("sk-test", ProviderKeyFormat.WrongPrefix)]
    [InlineData("nw-test key with spaces xx", ProviderKeyFormat.BadCharacters)]
    [InlineData("nw-short", ProviderKeyFormat.TooShort)]
    [InlineData("nw-test-xxxxxxxxxxxxxxxxxxxx", ProviderKeyFormat.Valid)]
    public void CheckFormat_Follows_Prefix_Characters_And_Length(string key, ProviderKeyFormat expected)
    {
        var provider = new ProviderKeyProvider { Id = "northwind", Name = "Northwind AI", KeyPrefix = "nw-", MinimumLength = 24 };
        provider.CheckFormat(key).ShouldBe(expected);
    }

    [Theory]
    [InlineData(20, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(3 * 3600, "3 h ago")]
    [InlineData(30 * 3600, "yesterday")]
    [InlineData(3 * 86400, "3 days ago")]
    public void Ago_Reads_Relative_Times(int seconds, string expected) =>
        ProviderKeysLabels.Ago(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);

    [Fact]
    public void Mask_Shows_The_Prefix_Dots_And_The_Last_Four()
    {
        var provider = Sample.Providers[0];
        ProviderKeysRow.Mask(new ProviderKey { Value = "nw-test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a" }, provider).ShouldBe("nw-••••4f2a");
        ProviderKeysRow.Mask(new ProviderKey { Last4 = "9z9z" }, provider).ShouldBe("nw-••••9z9z");
    }

    [AvaloniaFact]
    public void Without_Data_The_Card_Has_No_Rows_And_No_Test()
    {
        var (card, window) = Open(empty: true);
        try
        {
            card.Rows.Count.ShouldBe(0);
            card.EffectiveKeys.ShouldBeEmpty();
            AutomationProperties.GetName(card).ShouldBe("Provider keys");
            // Providers without keys read Not set.
            card.Providers = Sample.Providers;
            Pump();
            card.Rows.Count.ShouldBe(3);
            Row(card, "northwind").ShownStatus.ShouldBe("unset:Not set");
            // Without TestKey or a simulation, a test fails and says why instead of passing.
            var row = Row(card, "northwind");
            row.OpenForm(ProviderKeySaveMode.Add);
            row.FormInput!.Text = "nw-test-xxxxxxxxxxxxxxxxxxxxxxxx0a1b";
            Wait(row.SubmitAsync());
            Pump();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Editing);
            row.FormError.ShouldBe("Rejected: No key test is set up");
            row.Key.ShouldBeNull();
            card.Announcer!.Announcement.ShouldBe("Northwind AI key rejected: No key test is set up");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Tests_Keys_And_The_Apps_TestKey_Wins_Over_It()
    {
        var calls = 0;
        var (card, window) = Open();
        try
        {
            // The simulation connects a new key and says nothing was sent.
            var row = Row(card, "fabrikam");
            row.OpenForm(ProviderKeySaveMode.Add);
            row.FormInput!.Text = "fab_test_xxxxxxxxxxxxxxxxxxxx0a1b";
            Wait(row.SubmitAsync());
            Pump();
            row.Key!.Status.ShouldBe(ProviderKeyStatus.Connected);
            card.Announcer!.Announcement.ShouldBe("Fabrikam key added and connected. Simulated, nothing was sent.");
            // The app's test is used instead once it is set.
            card.TestKey = _ =>
            {
                calls++;
                return Task.FromResult(ProviderKeyTestResult.Fail("403 Forbidden"));
            };
            var northwind = Row(card, "northwind");
            Wait(northwind.RetestAsync());
            calls.ShouldBe(1);
            northwind.Key!.Status.ShouldBe(ProviderKeyStatus.Invalid);
            card.Announcer.Announcement.ShouldBe("Northwind AI key rejected: 403 Forbidden");
            // Clearing both stops simulated tests.
            card.TestKey = null;
            card.Simulation = null;
            Wait(Row(card, "contoso").RetestAsync());
            card.Announcer.Announcement.ShouldBe("Contoso Labs key rejected: No key test is set up");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Fails_A_Stored_Invalid_Key_With_Its_Fallback_Error()
    {
        var simulation = new ProviderKeysSimulation { Delay = TimeSpan.Zero, RejectedError = "Revoked" };
        var invalid = new ProviderKey { Provider = "contoso", Status = ProviderKeyStatus.Invalid, Last4 = "9c1d" };
        var failed = simulation.TestAsync(invalid, null, CancellationToken.None).GetAwaiter().GetResult();
        failed.IsOk.ShouldBeFalse();
        failed.Error.ShouldBe("Revoked");
        simulation.TestAsync(invalid, "csk-test-new-key-xxxxxxxxxxxxxxxx", CancellationToken.None).GetAwaiter().GetResult().IsOk.ShouldBeTrue();
        simulation.TestAsync(null, "csk-test-new-key-xxxxxxxxxxxxxxxx", CancellationToken.None).GetAwaiter().GetResult().IsOk.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void With_Its_Data_It_Shows_The_Workspace()
    {
        var (card, window) = Open();
        try
        {
            card.Rows.Count.ShouldBe(3);
            var northwind = Row(card, "northwind");
            northwind.ShownKey.ShouldBe("nw-••••4f2a");
            northwind.ShownStatus.ShouldBe("connected:Connected");
            northwind.ShownMeta.ShouldBe("Used 2 min ago");
            var contoso = Row(card, "contoso");
            contoso.ShownStatus.ShouldBe("invalid:Invalid");
            contoso.ShownMeta.ShouldBe("401 Unauthorized");
            AutomationProperties.GetName(Part<ProviderKeysStatusButton>(contoso, ProviderKeysRow.StatusPart))
                .ShouldBe("Contoso Labs key: Invalid, 401 Unauthorized. Test again");
            var fabrikam = Row(card, "fabrikam");
            fabrikam.ShownStatus.ShouldBe("unset:Not set");
            fabrikam.ShownKey.ShouldBe("No key added");
            Part<ProviderKeysDisclosureButton>(fabrikam, ProviderKeysRow.AddPart).IsVisible.ShouldBeTrue();
            Part<Panel>(fabrikam, ProviderKeysRow.KeyActionsPart).IsVisible.ShouldBeFalse();
            Part<ProviderKeysStatusButton>(fabrikam, ProviderKeysRow.StatusPart).Focusable.ShouldBeFalse();
            AutomationProperties.GetName(card).ShouldBe("Provider keys");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Icon_Button_Has_A_Provider_Specific_Name()
    {
        var (card, window) = Open();
        try
        {
            var row = Row(card, "northwind");
            AutomationProperties.GetName(Part<ProviderKeysRevealButton>(row, ProviderKeysRow.RevealPart)).ShouldBe("Reveal the Northwind AI key");
            AutomationProperties.GetName(Part<CopyButton>(row, ProviderKeysRow.CopyPart)).ShouldBe("Copy the Northwind AI key");
            AutomationProperties.GetName(Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.RotatePart)).ShouldBe("Rotate the Northwind AI key");
            AutomationProperties.GetName(Part<ProviderKeysIconButton>(row, ProviderKeysRow.RemovePart)).ShouldBe("Remove the Northwind AI key");
            AutomationProperties.GetName(Part<ProviderKeysDisclosureButton>(Row(card, "fabrikam"), ProviderKeysRow.AddPart)).ShouldBe("Add a Fabrikam key");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_List_Is_The_Empty_State()
    {
        var (card, window) = Open(c => c.Keys = []);
        try
        {
            foreach (var row in card.Rows)
            {
                row.Key.ShouldBeNull();
                row.ShownStatus.ShouldBe("unset:Not set");
                Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.AddPart).IsVisible.ShouldBeTrue();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Last_Four_Only_Disables_Reveal_And_Copy()
    {
        var (card, window) = Open(c => c.Keys = [new ProviderKey { Provider = "northwind", Last4 = "7d3e" }]);
        try
        {
            var row = Row(card, "northwind");
            row.ShownKey.ShouldBe("nw-••••7d3e");
            Part<ProviderKeysRevealButton>(row, ProviderKeysRow.RevealPart).IsEnabled.ShouldBeFalse();
            Part<CopyButton>(row, ProviderKeysRow.CopyPart).IsEnabled.ShouldBeFalse();
            row.Reveal();
            row.IsRevealed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reveal_Shows_The_Key_Until_The_Countdown_Ends()
    {
        var (card, window) = Open();
        try
        {
            var row = Row(card, "northwind");
            var toggle = Part<ProviderKeysRevealButton>(row, ProviderKeysRow.RevealPart);
            toggle.IsChecked = true;
            Pump();
            row.IsRevealed.ShouldBeTrue();
            row.ShownKey.ShouldBe("nw-test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a");
            card.Announcer!.Announcement.ShouldBe("Northwind AI key shown for 5 seconds");
            // Motion is off in tests: the reveal and its timer stay, the ring does not show.
            row.RevealRing!.IsVisible.ShouldBeFalse();

            row.ExpireReveal();
            Pump();
            row.IsRevealed.ShouldBeFalse();
            toggle.IsChecked.ShouldBe(false);
            row.ShownKey.ShouldBe("nw-••••4f2a");
            card.Announcer.Announcement.ShouldBe("Northwind AI key hidden");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reveal_Shows_The_Countdown_Ring_With_Full_Motion()
    {
        var card = new ProviderKeys { Now = () => s_now, Providers = Sample.Providers, Keys = Sample.Keys(s_now) };
        var window = TestHelpers.Show(card, Motion.ArcMotionLevel.Full);
        try
        {
            Pump();
            var row = Row(card, "northwind");
            row.Reveal();
            row.RevealRing!.IsVisible.ShouldBeTrue();
            row.RevealRing.Value.ShouldBeGreaterThan(0.9);
            row.Hide(announce: false);
            row.RevealRing.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retest_Of_A_Rejected_Key_Fails_Again_When_Simulated()
    {
        var changes = new List<ProviderKeysChange>();
        var (card, window) = Open(c =>
        {
            c.Simulation!.Delay = TimeSpan.FromMilliseconds(40);
            c.KeysChanged += (_, e) => changes.Add(e.Change);
        });
        try
        {
            var row = Row(card, "contoso");
            var task = row.RetestAsync();
            row.IsRetesting.ShouldBeTrue();
            row.ShownStatus.ShouldBe("testing:Testing");
            Wait(task);
            row.IsRetesting.ShouldBeFalse();
            row.Key!.Status.ShouldBe(ProviderKeyStatus.Invalid);
            changes.ShouldBe([ProviderKeysChange.Retested]);
            card.Announcer!.Announcement.ShouldBe("Contoso Labs key rejected: 401 Unauthorized. Simulated, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retest_Asks_The_App_Without_A_Key_And_Takes_The_Result()
    {
        ProviderKeyTestRequest? seen = null;
        var (card, window) = Open(c => c.TestKey = request =>
        {
            seen = request;
            return Task.FromResult(ProviderKeyTestResult.Ok());
        });
        try
        {
            var row = Row(card, "contoso");
            Wait(row.RetestAsync());
            seen!.Provider.ShouldBe("contoso");
            seen.Key.ShouldBeNull();
            row.Key!.Status.ShouldBe(ProviderKeyStatus.Connected);
            row.Key.Error.ShouldBeNull();
            card.Keys!.Single(k => k.Provider == "contoso").Status.ShouldBe(ProviderKeyStatus.Connected);
            card.Announcer!.Announcement.ShouldBe("Contoso Labs key connected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Form_Checks_The_Format_As_You_Type()
    {
        var (card, window) = Open();
        try
        {
            var row = Row(card, "fabrikam");
            row.OpenForm(ProviderKeySaveMode.Add);
            Pump();
            row.FormBox!.IsExpanded.ShouldBeTrue();
            Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.AddPart).IsExpanded.ShouldBeTrue();
            var input = row.FormInput!;
            input.IsFocused.ShouldBeTrue();
            input.Label.ShouldBe("Fabrikam API key");
            row.ShownHelper.ShouldBe("hint:Paste a Fabrikam key. It starts with fab_");

            input.Text = "sk-wrong";
            row.ShownHelper.ShouldBe("invalid:Fabrikam keys start with fab_");
            input.IsInvalid.ShouldBeTrue();
            AutomationProperties.GetHelpText(input).ShouldBe("Fabrikam keys start with fab_");

            input.Text = "fab_test";
            input.IsInvalid.ShouldBeFalse();
            row.ShownHelper.ShouldBe("hint:Paste a Fabrikam key. It starts with fab_");

            input.Text = "fab_test_xxxxxxxxxxxxxxxxxxxx";
            row.ShownHelper.ShouldBe("valid:Looks right. Save tests the connection.");

            input.Text = "fab_test";
            Wait(row.SubmitAsync());
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Editing);
            row.ShownHelper.ShouldBe("invalid:This key looks too short.");
            input.IsInvalid.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_A_Key_Tests_It_Then_Saves_It()
    {
        var saved = new List<ProviderKeySavedEventArgs>();
        var changes = new List<ProviderKeysChange>();
        var (card, window) = Open(c =>
        {
            c.KeySaved += (_, e) => saved.Add(e);
            c.KeysChanged += (_, e) => changes.Add(e.Change);
        });
        try
        {
            var row = Row(card, "fabrikam");
            row.OpenForm(ProviderKeySaveMode.Add);
            row.FormInput!.Text = "fab_test_xxxxxxxxxxxxxxxxxxxx0a1b";
            Wait(row.SubmitAsync());
            Pump();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Closed);
            row.FormBox!.IsExpanded.ShouldBeFalse();
            row.Key!.Status.ShouldBe(ProviderKeyStatus.Connected);
            row.ShownKey.ShouldBe("fab_••••0a1b");
            row.ShownMeta.ShouldBe("Never used");
            saved.Count.ShouldBe(1);
            saved[0].Provider.ShouldBe("fabrikam");
            saved[0].Key.ShouldBe("fab_test_xxxxxxxxxxxxxxxxxxxx0a1b");
            saved[0].Mode.ShouldBe(ProviderKeySaveMode.Add);
            changes.ShouldBe([ProviderKeysChange.Added]);
            card.Keys!.Count.ShouldBe(3);
            card.Announcer!.Announcement.ShouldBe("Fabrikam key added and connected. Simulated, nothing was sent.");
            Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.RotatePart).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rotation_Keeps_The_Old_Key_Until_The_New_One_Passes()
    {
        var pending = new TaskCompletionSource<ProviderKeyTestResult>();
        ProviderKeyTestRequest? seen = null;
        var (card, window) = Open(c => c.TestKey = request =>
        {
            seen = request;
            return pending.Task;
        });
        try
        {
            var row = Row(card, "northwind");
            row.OpenForm(ProviderKeySaveMode.Rotate);
            Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.RotatePart).IsExpanded.ShouldBeTrue();
            row.FormInput!.Text = "nw-live-yyyyyyyyyyyyyyyyyyyy9b8c";
            var submit = row.SubmitAsync();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Testing);
            row.ShownHelper.ShouldBe("testing:Testing connection…");
            Part<Button>(row, ProviderKeysRow.SavePart).IsLoading.ShouldBeTrue();
            seen!.Key.ShouldBe("nw-live-yyyyyyyyyyyyyyyyyyyy9b8c");
            row.ShownKey.ShouldBe("nw-••••4f2a");

            pending.SetResult(ProviderKeyTestResult.Ok("12 models"));
            Wait(submit);
            row.ShownKey.ShouldBe("nw-••••9b8c");
            row.Key!.Value.ShouldBe("nw-live-yyyyyyyyyyyyyyyyyyyy9b8c");
            card.Announcer!.Announcement.ShouldBe("Northwind AI key rotated and connected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rejected_Key_Keeps_The_Form_Open_With_The_Reason()
    {
        var (card, window) = Open(c => c.TestKey = _ => Task.FromResult(ProviderKeyTestResult.Fail("401 Unauthorized")));
        try
        {
            var row = Row(card, "northwind");
            row.OpenForm(ProviderKeySaveMode.Rotate);
            row.FormInput!.Text = "nw-live-yyyyyyyyyyyyyyyyyyyy9b8c";
            Wait(row.SubmitAsync());
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Editing);
            row.FormError.ShouldBe("Rejected: 401 Unauthorized");
            row.FormErrorIsRejection.ShouldBeTrue();
            row.FormInput.IsInvalid.ShouldBeTrue();
            row.Key!.Value.ShouldBe("nw-test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a");
            card.Announcer!.Announcement.ShouldBe("Northwind AI key rejected: 401 Unauthorized");

            // Typing again clears the rejection.
            row.FormInput.Text = "nw-live-yyyyyyyyyyyyyyyyyyyy9b8d";
            row.FormError.ShouldBeNull();
            row.FormInput.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_The_Form_Aborts_The_Test_In_Flight()
    {
        CancellationToken token = default;
        var pending = new TaskCompletionSource<ProviderKeyTestResult>();
        var (card, window) = Open(c => c.TestKey = request =>
        {
            token = request.CancellationToken;
            return pending.Task;
        });
        try
        {
            var row = Row(card, "fabrikam");
            row.OpenForm(ProviderKeySaveMode.Add);
            row.FormInput!.Text = "fab_test_xxxxxxxxxxxxxxxxxxxx0a1b";
            var submit = row.SubmitAsync();
            row.CloseForm(returnFocus: true);
            token.IsCancellationRequested.ShouldBeTrue();
            pending.SetResult(ProviderKeyTestResult.Ok());
            Wait(submit);
            row.Key.ShouldBeNull();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Saves_And_Escape_Cancels_Returning_Focus()
    {
        var (card, window) = Open();
        try
        {
            var row = Row(card, "fabrikam");
            var add = Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.AddPart);
            add.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Editing);
            row.FormInput!.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            row.FormPhase.ShouldBe(ProviderKeysFormPhase.Closed);
            add.IsFocused.ShouldBeTrue();

            add.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            row.FormInput.Text = "fab_test_xxxxxxxxxxxxxxxxxxxx0a1b";
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => row.FormPhase == ProviderKeysFormPhase.Closed).ShouldBeTrue();
            row.Key!.Status.ShouldBe(ProviderKeyStatus.Connected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Remove_Offers_Undo_And_Reports_Only_When_It_Sticks()
    {
        var removed = new List<string>();
        var changes = new List<ProviderKeysChange>();
        var (card, window) = Open(c =>
        {
            c.KeyRemoved += (_, e) => removed.Add(e.Provider);
            c.KeysChanged += (_, e) => changes.Add(e.Change);
        });
        try
        {
            var row = Row(card, "northwind");
            row.Remove();
            Pump();
            row.Key.ShouldBeNull();
            row.PendingRemoval.ShouldNotBeNull();
            row.ShownKey.ShouldBe("Key removed");
            row.ShownStatus.ShouldBe("unset:Not set");
            card.Keys!.Any(k => k.Provider == "northwind").ShouldBeFalse();
            var undo = Part<Avalonia.Controls.Button>(row, ProviderKeysRow.UndoPart);
            undo.IsVisible.ShouldBeTrue();
            undo.IsFocused.ShouldBeTrue();
            Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.AddPart).IsVisible.ShouldBeFalse();
            removed.ShouldBeEmpty();
            card.Announcer!.Announcement.ShouldBe("Northwind AI key removed. Undo is available for 5 seconds");

            row.Undo();
            Pump();
            row.Key!.Value.ShouldBe("nw-test-xxxxxxxxxxxxxxxxxxxxxxxx4f2a");
            Part<ProviderKeysIconButton>(row, ProviderKeysRow.RemovePart).IsFocused.ShouldBeTrue();
            card.Announcer.Announcement.ShouldBe("Northwind AI key restored");

            row.Remove();
            row.CommitPendingRemoval();
            Pump();
            removed.ShouldBe(["northwind"]);
            undo.IsVisible.ShouldBeFalse();
            Part<ProviderKeysDisclosureButton>(row, ProviderKeysRow.AddPart).IsVisible.ShouldBeTrue();
            changes.ShouldBe([ProviderKeysChange.Removed, ProviderKeysChange.Restored, ProviderKeysChange.Removed]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Raises_KeyCopied_And_Announces()
    {
        var copied = new List<string>();
        var (card, window) = Open(c => c.KeyCopied += (_, e) => copied.Add(e.Provider));
        try
        {
            card.OnCopied(Row(card, "northwind"));
            copied.ShouldBe(["northwind"]);
            card.Announcer!.Announcement.ShouldBe("Northwind AI key copied");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Stacks_Under_560px()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.IsNarrow.ShouldBeTrue();
            card.Rows[0].Classes.Contains(ProviderKeysRow.NarrowPseudoClass).ShouldBeTrue();
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(400);
            foreach (var row in card.Rows)
            {
                row.Bounds.Width.ShouldBeLessThanOrEqualTo(card.Bounds.Width + 0.5);
            }
            window.Width = 720;
            Pump();
            card.IsNarrow.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Placeholder_Rows()
    {
        var (card, window) = Open(c => c.IsLoading = true);
        try
        {
            card.Rows.Count.ShouldBe(3);
            card.Rows.ShouldAllBe(r => r.IsPlaceholder);
            Part<Panel>(card.Rows[0], "PART_Actions").IsVisible.ShouldBeFalse();
            card.IsLoading = false;
            Pump();
            card.Rows.ShouldAllBe(r => !r.IsPlaceholder);
            Row(card, "northwind").ShownKey.ShouldBe("nw-••••4f2a");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (card, window) = Open(c => c.Labels = new ProviderKeysLabels { Connected = "Verbunden", NotSet = "Nicht gesetzt" });
        try
        {
            Row(card, "northwind").ShownStatus.ShouldBe("connected:Verbunden");
            Row(card, "fabrikam").ShownStatus.ShouldBe("unset:Nicht gesetzt");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Title_Description_And_Footer_Show_Or_Hide()
    {
        var (card, window) = Open(c => c.Description = "Bring your own key.");
        try
        {
            TestHelpers.Descendant<TextBlock>(card, ProviderKeys.DescriptionPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Border>(card, ProviderKeys.FooterPart).IsVisible.ShouldBeTrue();
            card.Footer = null;
            card.Description = null;
            TestHelpers.Descendant<TextBlock>(card, ProviderKeys.DescriptionPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(card, ProviderKeys.FooterPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

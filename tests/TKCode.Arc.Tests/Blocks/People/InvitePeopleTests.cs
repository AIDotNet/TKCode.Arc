using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// <see cref="InvitePeople"/> on the invented Lumen Labs data: suggestions and the combobox keyboard, chips from picks,
/// typing and pastes, invalid chips and their fixes, Backspace marking, the role menu (keyboard and clamping), seats and
/// the upgrade, Send held back, sending with deliveries, failure, joining, resend, revoke, reset, breakpoints, announcements
/// and the motion levels (flights, shakes, layout springs).
/// </summary>
public class InvitePeopleTests
{
    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 640, Action<InvitePeople>? configure = null)
        {
            Block = new InvitePeople { IsResetVisible = true };
            People = InvitePeopleDemoData.Fill(Block);
            foreach (var person in Block.Directory)
            {
                person.Avatar = null;
            }
            configure?.Invoke(Block);
            Block.EntriesChanged += (_, e) => EntryEvents.Add(e);
            Block.RoleChanged += (_, e) => RoleEvents.Add(e);
            Block.InvitesSent += (_, e) => SentEvents.Add(e);
            Block.SendFailed += (_, e) => FailedEvents.Add(e);
            Block.InviteJoined += (_, e) => JoinedEvents.Add(e);
            Block.InviteResent += (_, e) => ResentEvents.Add(e);
            Block.InviteRevoked += (_, e) => RevokedEvents.Add(e);
            Block.PlanUpgraded += (_, e) => UpgradedEvents.Add(e);
            Block.ResetPerformed += (_, _) => Resets++;
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 900;
            Window.Height = 1100;
            Pump();
        }

        public InvitePeople Block { get; }

        public InvitePeopleDemoData.People People { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<InvitePeopleEntriesChangedEventArgs> EntryEvents { get; } = new();

        public List<InvitePeopleRoleChangedEventArgs> RoleEvents { get; } = new();

        public List<InvitePeopleInvitesSentEventArgs> SentEvents { get; } = new();

        public List<InvitePeopleSendFailedEventArgs> FailedEvents { get; } = new();

        public List<InvitePeopleInviteEventArgs> JoinedEvents { get; } = new();

        public List<InvitePeopleInviteEventArgs> ResentEvents { get; } = new();

        public List<InvitePeopleInviteEventArgs> RevokedEvents { get; } = new();

        public List<InvitePeoplePlanUpgradedEventArgs> UpgradedEvents { get; } = new();

        public int Resets { get; private set; }

        public InvitePeopleInput Input => Block.Input!;

        public string? LastAnnouncement => Block.Announcer!.History.LastOrDefault();

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Type(string text)
        {
            if (!Input.IsFocused)
            {
                Input.Focus(NavigationMethod.Pointer);
                Pump();
            }
            Input.Text = text;
            Input.CaretIndex = text.Length;
            Pump();
        }

        public void Click(Avalonia.Controls.Button button)
        {
            button.Focus(NavigationMethod.Tab);
            Pump();
            Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    #region Rest and seats

    [AvaloniaFact]
    public void At_Rest_It_Shows_The_Team_The_Seats_And_The_Pending_Invite()
    {
        using var rig = new Rig();
        var block = rig.Block;
        block.SeatsUsed.ShouldBe(6);
        block.IsOverLimit.ShouldBeFalse();
        block.Team!.AvatarOf(rig.People.Iris).ShouldNotBeNull();
        AutomationProperties.GetName(block.Team).ShouldBe("Team, 5 people");
        block.RowOf(block.PendingInvites[0]).ShouldNotBeNull();
        block.SendButton!.IsEnabled.ShouldBeFalse();
        rig.Input.PlaceholderText.ShouldBe(block.Labels.Placeholder);
        block.HintText.ShouldBe(block.Labels.Hint);
        AutomationProperties.GetHelpText(block.Meter!).ShouldBe("6 of 10 seats");
        block.Meter!.Target.Members.ShouldBe(0.5, 1e-9);
        block.Meter.Target.Pending.ShouldBe(0.6, 1e-9);
    }

    #endregion

    #region Suggestions and chips

    [AvaloniaFact]
    public void Clicking_The_Empty_Field_Suggests_Four_People()
    {
        using var rig = new Rig();
        rig.Input.Focus(NavigationMethod.Pointer);
        rig.Pump();
        rig.Block.IsSuggestionsOpen.ShouldBeTrue();
        rig.Block.Suggestions.IsSuggested.ShouldBeTrue();
        rig.Block.Suggestions.Rows.Count.ShouldBe(4);
        rig.Block.HighlightIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Arrows_Skip_Unavailable_People_And_Wrap()
    {
        using var rig = new Rig();
        rig.Type("is");
        var rows = rig.Block.Suggestions.Rows;
        rows.Count.ShouldBe(4);
        rows[2].IsBlocked.ShouldBeTrue();
        rig.Block.HighlightIndex.ShouldBe(0);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Block.HighlightIndex.ShouldBe(1);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Block.HighlightIndex.ShouldBe(0);
        rig.Key(PhysicalKey.ArrowUp);
        rig.Block.HighlightIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Enter_Adds_The_Highlighted_Person_And_Keeps_The_List_Open()
    {
        using var rig = new Rig();
        rig.Type("cla");
        rig.Key(PhysicalKey.Enter);
        var entry = rig.Block.Entries.ShouldHaveSingleItem();
        entry.Person.ShouldBe(rig.People.Clara);
        entry.Role!.Name.ShouldBe("Member");
        rig.Input.Text.ShouldBeNullOrEmpty();
        rig.Input.PlaceholderText.ShouldBe(rig.Block.Labels.MorePlaceholder);
        rig.Block.IsSuggestionsOpen.ShouldBeTrue();
        rig.Block.Suggestions.IsSuggested.ShouldBeTrue();
        rig.Block.Suggestions.Rows.ShouldNotContain(r => r.Person == rig.People.Clara);
        rig.LastAnnouncement.ShouldBe("Added Clara Weiss");
        rig.Block.SeatsUsed.ShouldBe(7);
        rig.Block.SendButton!.IsEnabled.ShouldBeTrue();
        rig.EntryEvents.Last().ValidCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Pointer_Press_On_A_Suggestion_Adds_It_But_Not_An_Unavailable_One()
    {
        using var rig = new Rig();
        rig.Type("is");
        var blocked = rig.Block.SuggestionRows[2];
        blocked.RaiseEvent(new PointerPressedEventArgs(blocked, new Pointer(1, PointerType.Mouse, true), rig.Window, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        rig.Block.Entries.ShouldBeEmpty();
        rig.Block.CommitSuggestion(rig.Block.Suggestions.Rows[1]);
        rig.Block.Entries.Single().Person.ShouldBe(rig.People.Sara);
    }

    [AvaloniaFact]
    public void A_New_Address_Becomes_An_Email_Chip()
    {
        using var rig = new Rig();
        rig.Type("sam@rivers.co");
        rig.Block.Suggestions.Rows.Single().Kind.ShouldBe(InvitePeopleSuggestionKind.Email);
        rig.Key(PhysicalKey.Enter);
        var entry = rig.Block.Entries.Single();
        entry.IsValid.ShouldBeTrue();
        entry.Email.ShouldBe("sam@rivers.co");
        entry.Text.ShouldBe("sam@rivers.co");
    }

    [AvaloniaFact]
    public void Enter_Without_A_Match_And_A_Comma_Turn_Text_Into_Chips()
    {
        using var rig = new Rig();
        rig.Type("nobody");
        rig.Block.Suggestions.Rows.ShouldBeEmpty();
        rig.Key(PhysicalKey.Enter);
        var entry = rig.Block.Entries.Single();
        entry.Error.ShouldBe(InvitePeopleEntryError.NotEmail);
        rig.Block.AreErrorsShown.ShouldBeTrue();
        rig.Type("jonas berg");
        rig.Window.KeyTextInput(",");
        rig.Pump();
        rig.Block.Entries.Count.ShouldBe(2);
        rig.Block.Entries[1].Person.ShouldBe(rig.People.Jonas);
        rig.Input.Text.ShouldBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Escape_Closes_The_List_Then_Clears_The_Text()
    {
        using var rig = new Rig();
        rig.Type("cla");
        rig.Key(PhysicalKey.Escape);
        rig.Block.IsSuggestionsOpen.ShouldBeFalse();
        rig.Input.Text.ShouldBe("cla");
        rig.Key(PhysicalKey.Escape);
        rig.Input.Text.ShouldBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void Backspace_Marks_The_Last_Chip_Then_Removes_It()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss, Mateus Rocha");
        rig.Input.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.Backspace);
        rig.Block.ArmedChip!.Entry.Person.ShouldBe(rig.People.Mateus);
        rig.LastAnnouncement.ShouldBe("Press Backspace again to remove Mateus Rocha");
        rig.Key(PhysicalKey.Backspace);
        rig.Block.Entries.Single().Person.ShouldBe(rig.People.Clara);
        rig.LastAnnouncement.ShouldBe("Removed Mateus Rocha");
        // Typing clears the mark.
        rig.Key(PhysicalKey.Backspace);
        rig.Block.ArmedChip.ShouldNotBeNull();
        rig.Window.KeyTextInput("j");
        rig.Pump();
        rig.Block.ArmedChip.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Sample_Paste_Adds_Chips_Errors_A_Summary_And_Runs_Over()
    {
        using var rig = new Rig();
        var block = rig.Block;
        var summary = block.AddText(InvitePeopleDemoData.SampleList);
        rig.Pump();
        summary.ShouldBe("Added 5 people, 4 entries to fix, skipped 2 duplicates");
        block.HintText.ShouldBe(summary);
        rig.LastAnnouncement.ShouldBe(summary);
        block.Entries.Count.ShouldBe(9);
        block.AreErrorsShown.ShouldBeTrue();
        block.SeatsUsed.ShouldBe(11);
        block.IsOverLimit.ShouldBeTrue();
        block.IsOverLineShown.ShouldBeTrue();
        block.UpgradeButton!.IsVisible.ShouldBeTrue();
        block.Classes.ShouldNotBeNull();
        AutomationProperties.GetHelpText(block.Meter!).ShouldBe("11 of 10 seats, 1 over the limit");
        block.FlushTimers();
        block.HintText.ShouldBe(block.Labels.Hint);
    }

    [AvaloniaFact]
    public void Plain_Text_Pastes_Into_The_Text_Box_And_Lists_Become_Chips()
    {
        using var rig = new Rig();
        rig.Type("cl");
        rig.Block.PasteText("ara");
        rig.Input.Text.ShouldBe("clara");
        rig.Block.Entries.ShouldBeEmpty();
        rig.Block.PasteText("Jonas Berg\nMina Cho");
        rig.Block.Entries.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Selecting_An_Invalid_Chip_Puts_Its_Text_Back_For_Editing()
    {
        using var rig = new Rig();
        rig.Block.AddText("nadia@lumenlabs, Jonas Berg");
        rig.Type("something");
        var invalid = rig.Block.Entries[0];
        var chip = rig.Block.ChipOf(invalid)!;
        AutomationProperties.GetName(chip.EditButton!).ShouldBe(
            "Edit nadia@lumenlabs. The domain of “nadia@lumenlabs” is incomplete, for example lumen.example");
        rig.Click(chip.EditButton!);
        rig.Block.Entries.Single().Person.ShouldBe(rig.People.Jonas);
        rig.Input.Text.ShouldBe("nadia@lumenlabs");
        rig.Input.IsFocused.ShouldBeTrue();
        rig.LastAnnouncement.ShouldBe("Editing nadia@lumenlabs");
    }

    [AvaloniaFact]
    public void Remove_Invalid_Removes_Every_Red_Chip()
    {
        using var rig = new Rig();
        rig.Block.AddText(InvitePeopleDemoData.SampleList);
        rig.Block.RemoveInvalidEntries();
        rig.Pump();
        rig.Block.Entries.Count.ShouldBe(5);
        rig.Block.AreErrorsShown.ShouldBeFalse();
        rig.LastAnnouncement.ShouldBe("Removed 4 entries");
    }

    #endregion

    #region Role menu

    [AvaloniaFact]
    public void Arrow_Down_On_A_Role_Button_Opens_The_Menu_On_The_Current_Role()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss");
        var chip = rig.Block.ChipOf(rig.Block.Entries[0])!;
        chip.RoleButton!.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Block.IsRoleMenuOpen.ShouldBeTrue();
        var items = rig.Block.RoleMenuItems.ToList();
        items.Count.ShouldBe(3);
        items[1].IsFocused.ShouldBeTrue();
        items[1].IsChecked.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowUp);
        items[0].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowUp);
        items[2].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Home);
        items[0].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Block.IsRoleMenuOpen.ShouldBeFalse();
        rig.Block.Entries[0].Role!.Name.ShouldBe("Admin");
        chip.RoleText!.Content.ShouldBe("Admin");
        chip.RoleButton.IsFocused.ShouldBeTrue();
        rig.RoleEvents.Single().OldRole!.Name.ShouldBe("Member");
        rig.LastAnnouncement.ShouldBe("Clara Weiss will join as Admin");
        AutomationProperties.GetName(chip.RoleButton).ShouldBe("Role for Clara Weiss: Admin");
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Role_Menu_And_Returns_The_Focus()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss");
        var chip = rig.Block.ChipOf(rig.Block.Entries[0])!;
        rig.Block.OpenRoleMenu(chip, keyboard: true);
        rig.Pump();
        rig.Key(PhysicalKey.Escape);
        rig.Block.IsRoleMenuOpen.ShouldBeFalse();
        chip.RoleButton!.IsFocused.ShouldBeTrue();
        rig.Block.Entries[0].Role!.Name.ShouldBe("Member");
    }

    [AvaloniaFact]
    public void The_Role_Menu_Stays_Eight_Inside_The_Card()
    {
        using var rig = new Rig(width: 600);
        rig.Block.AddText(string.Join(",", Enumerable.Range(0, 8).Select(i => $"user{i}@x.io")));
        rig.Pump();
        foreach (var entry in rig.Block.Entries)
        {
            var button = rig.Block.ChipOf(entry)!.RoleButton!;
            var x = button.TranslatePoint(default, rig.Block)!.Value.X;
            var left = x + rig.Block.RoleMenuOffset(button);
            left.ShouldBeGreaterThanOrEqualTo(8 - 0.01);
            (left + 232).ShouldBeLessThanOrEqualTo(rig.Block.Bounds.Width - 8 + 0.01);
            if (x - 8 >= 8 && x - 8 + 232 <= rig.Block.Bounds.Width - 8)
            {
                rig.Block.RoleMenuOffset(button).ShouldBe(-8, 0.01);
            }
        }
    }

    #endregion

    #region Send

    [AvaloniaFact]
    public void Send_With_Invalid_Chips_Announces_Instead_Of_Sending()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss, nobody");
        rig.Block.SendButton!.IsEnabled.ShouldBeTrue();
        rig.Block.SendButton.Classes.Contains(":ready").ShouldBeFalse();
        rig.Click(rig.Block.SendButton);
        rig.Block.Entries.Count.ShouldBe(2);
        rig.Block.PendingInvites.Count.ShouldBe(1);
        rig.LastAnnouncement.ShouldBe("Fix or remove 1 entry before sending");
        rig.Block.SendButton.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Send_Over_The_Limit_Announces_And_The_Upgrade_Lifts_It()
    {
        using var rig = new Rig();
        rig.Block.AddText(InvitePeopleDemoData.SampleList);
        rig.Block.RemoveInvalidEntries();
        rig.Pump();
        rig.Block.IsOverLimit.ShouldBeTrue();
        rig.Click(rig.Block.SendButton!);
        rig.Block.PendingInvites.Count.ShouldBe(1);
        rig.LastAnnouncement.ShouldBe("This is 1 seat more than the Basic plan has. Upgrade or remove 1 person.");
        rig.Click(rig.Block.UpgradeButton!);
        rig.Block.CurrentPlan!.Name.ShouldBe("Growth");
        rig.Block.IsOverLimit.ShouldBeFalse();
        rig.Block.IsOverLineShown.ShouldBeFalse();
        rig.UpgradedEvents.Single().NewPlan.SeatLimit.ShouldBe(25);
        rig.LastAnnouncement.ShouldBe("Now on the Growth plan with 25 seats");
        rig.Block.SendButton!.Classes.Contains(":ready").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Largest_Plan_Asks_To_Remove_People()
    {
        using var rig = new Rig(configure: b => b.CurrentPlan = b.Plans[1]);
        rig.Block.AddText(string.Join(",", Enumerable.Range(0, 20).Select(i => $"user{i}@x.io")));
        rig.Pump();
        rig.Block.IsOverLimit.ShouldBeTrue();
        rig.Block.UpgradeButton!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Sending_Moves_The_Chips_Into_The_Pending_List()
    {
        var gate = new TaskCompletionSource();
        InvitePeopleSendRequest? request = null;
        using var rig = new Rig(configure: b => b.SendAction = (r, _) =>
        {
            request = r;
            return gate.Task;
        });
        rig.Block.AddText("Clara Weiss, Mateus Rocha");
        rig.Block.ChipOf(rig.Block.Entries[1])!.Entry.Role = rig.Block.Roles[2];
        rig.Click(rig.Block.SendButton!);
        rig.Block.Entries.ShouldBeEmpty();
        rig.Block.PendingInvites.Count.ShouldBe(3);
        rig.Block.PendingInvites[0].Person.ShouldBe(rig.People.Clara);
        rig.Block.PendingInvites[1].Role!.Name.ShouldBe("Guest");
        rig.Block.PendingInvites[0].Status.ShouldBe(InvitePeopleInviteStatus.Sending);
        rig.LastAnnouncement.ShouldBe("Sending 2 invites");
        request!.Attempt.ShouldBe(1);
        rig.Block.SeatsUsed.ShouldBe(8);
        rig.Input.IsFocused.ShouldBeTrue();

        rig.Block.MarkDelivered(rig.Block.PendingInvites[2]);
        rig.Block.PendingInvites[2].Status.ShouldBe(InvitePeopleInviteStatus.Sent);
        gate.SetResult();
        rig.Pump();
        // The rows still sending turn sent 160 ms apart: the first at once, the next on a timer.
        rig.Block.PendingInvites[0].Status.ShouldBe(InvitePeopleInviteStatus.Sent);
        rig.Block.PendingInvites[1].Status.ShouldBe(InvitePeopleInviteStatus.Sending);
        rig.Block.FlushTimers();
        rig.Block.PendingInvites[1].Status.ShouldBe(InvitePeopleInviteStatus.Sent);
        rig.SentEvents.Single().Request.Invites.Count.ShouldBe(2);
        rig.LastAnnouncement.ShouldBe("2 invites sent");
    }

    [AvaloniaFact]
    public void A_Failed_Send_Takes_The_Rows_Back_And_Returns_The_Chips()
    {
        using var rig = new Rig(configure: b => b.SendAction = async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("busy");
        });
        rig.Block.AddText("Clara Weiss, Mateus Rocha");
        rig.Block.Entries[1].Role = rig.Block.Roles[0];
        rig.Click(rig.Block.SendButton!);
        TestHelpers.PumpUntil(() => rig.FailedEvents.Count > 0);
        rig.Pump();
        rig.Block.PendingInvites.Count.ShouldBe(1);
        rig.Block.Entries.Select(e => e.Person).ShouldBe([rig.People.Clara, rig.People.Mateus]);
        rig.Block.Entries[1].Role!.Name.ShouldBe("Admin");
        rig.FailedEvents.Single().Exception.Message.ShouldBe("busy");
        rig.LastAnnouncement.ShouldBe(rig.Block.Labels.SendFailed);
    }

    #endregion

    #region Pending rows

    [AvaloniaFact]
    public void An_Accepted_Invite_Leaves_And_Joins_The_Team()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss");
        rig.Click(rig.Block.SendButton!);
        rig.Block.FlushTimers();
        var invite = rig.Block.PendingInvites[0];
        rig.Block.MarkJoined(invite);
        rig.Pump();
        invite.Status.ShouldBe(InvitePeopleInviteStatus.Joined);
        rig.LastAnnouncement.ShouldBe("Clara Weiss accepted and joined Lumen Labs");
        rig.Block.RowOf(invite)!.Resend.IsEnabled.ShouldBeFalse();
        rig.Block.FlushTimers();
        rig.Pump();
        rig.Block.PendingInvites.ShouldNotContain(invite);
        rig.Block.Members[0].ShouldBe(rig.People.Clara);
        rig.Block.Team!.AvatarOf(rig.People.Clara).ShouldNotBeNull();
        rig.Block.Team.AvatarOf(rig.People.Priya).ShouldBeNull();
        AutomationProperties.GetName(rig.Block.Team).ShouldBe("Team, 6 people");
        rig.JoinedEvents.Single().Invite.ShouldBe(invite);
        rig.Block.SeatsUsed.ShouldBe(7);
    }

    [AvaloniaFact]
    public void Resend_Shows_Sent_Again_For_A_Moment()
    {
        using var rig = new Rig();
        var invite = rig.Block.PendingInvites[0];
        var row = rig.Block.RowOf(invite)!;
        rig.Click(row.Resend);
        row.IsResent.ShouldBeTrue();
        AutomationProperties.GetName(row.Resend).ShouldBe("Invite sent to Theo Grant again");
        rig.ResentEvents.Single().Invite.ShouldBe(invite);
        rig.Click(row.Resend);
        rig.ResentEvents.Count.ShouldBe(1);
        rig.Block.FlushTimers();
        row.IsResent.ShouldBeFalse();
        row.ResendText.Content.ShouldBe("Resend");
    }

    [AvaloniaFact]
    public void Revoke_Frees_The_Seat_And_Focuses_The_Heading()
    {
        using var rig = new Rig();
        var invite = rig.Block.PendingInvites[0];
        rig.Click(rig.Block.RowOf(invite)!.Revoke);
        rig.Block.PendingInvites.ShouldBeEmpty();
        rig.Block.SeatsUsed.ShouldBe(5);
        rig.Block.PendingHeading!.IsFocused.ShouldBeTrue();
        rig.LastAnnouncement.ShouldBe("Revoked the invite to Theo Grant. 1 seat is free again.");
        rig.RevokedEvents.Single().Invite.ShouldBe(invite);
    }

    [AvaloniaFact]
    public void A_Failed_Revoke_Puts_The_Row_Back()
    {
        using var rig = new Rig(configure: b => b.RevokeAction = async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("nope");
        });
        var invite = rig.Block.PendingInvites[0];
        _ = rig.Block.RevokeAsync(invite);
        rig.Block.PendingInvites.ShouldBeEmpty();
        TestHelpers.PumpUntil(() => rig.Block.PendingInvites.Count == 1);
        rig.Block.PendingInvites[0].ShouldBe(invite);
        rig.LastAnnouncement.ShouldBe("The invite to Theo Grant could not be revoked");
    }

    [AvaloniaFact]
    public void Reset_Puts_Everything_Back()
    {
        using var rig = new Rig();
        rig.Block.AddText("Clara Weiss");
        rig.Click(rig.Block.SendButton!);
        rig.Block.FlushTimers();
        rig.Block.MarkJoined(rig.Block.PendingInvites[0]);
        rig.Block.FlushTimers();
        rig.Block.AddText(InvitePeopleDemoData.SampleList);
        _ = rig.Block.UpgradeAsync();
        rig.Pump();
        rig.Block.Reset();
        rig.Pump();
        rig.Block.Entries.ShouldBeEmpty();
        rig.Block.Members.ShouldBe(rig.People.Team);
        rig.Block.PendingInvites.Single().Person.ShouldBe(rig.People.Theo);
        rig.Block.PendingInvites[0].SentText.ShouldBe("3 days ago");
        rig.Block.CurrentPlan!.Name.ShouldBe("Basic");
        rig.Block.SeatsUsed.ShouldBe(6);
        rig.Resets.ShouldBe(1);
        rig.LastAnnouncement.ShouldBe("Back to the start");
    }

    [AvaloniaFact]
    public void Reset_Cancels_A_Running_Send()
    {
        CancellationToken seen = default;
        using var rig = new Rig(configure: b => b.SendAction = (_, token) =>
        {
            seen = token;
            return Task.Delay(-1, token);
        });
        rig.Block.AddText("Clara Weiss");
        rig.Click(rig.Block.SendButton!);
        rig.Block.Reset();
        rig.Pump();
        seen.IsCancellationRequested.ShouldBeTrue();
        rig.FailedEvents.ShouldBeEmpty();
        rig.Block.PendingInvites.Count.ShouldBe(1);
    }

    #endregion

    #region Layout

    [AvaloniaFact]
    public void A_Narrow_Block_Stacks_The_Status_And_Wraps_The_Header()
    {
        using var rig = new Rig(width: 390);
        rig.Block.Classes.Contains(":compact").ShouldBeTrue();
        rig.Block.IsNarrow.ShouldBeTrue();
        rig.Block.Header!.IsWrapped.ShouldBeTrue();
        var row = rig.Block.RowOf(rig.Block.PendingInvites[0])!;
        row.IsNarrow.ShouldBeTrue();
        row.Copy.Children.ShouldContain(row.Status);
        rig.Host.Width = 880;
        rig.Pump();
        rig.Block.IsNarrow.ShouldBeFalse();
        rig.Block.Classes.Contains(":compact").ShouldBeFalse();
        row.IsNarrow.ShouldBeFalse();
        // Whether the header wraps at 600 depends on the font (the headless stub font is wide); it is checked in snapshots.
    }

    [AvaloniaFact]
    public void Chips_Wrap_And_The_Text_Box_Keeps_Its_Minimum_Width()
    {
        using var rig = new Rig();
        rig.Block.AddText(string.Join(",", Enumerable.Range(0, 9).Select(i => $"user{i}@x.io")));
        rig.Pump();
        var panel = rig.Block.ChipPanel!;
        panel.Bounds.Height.ShouldBeGreaterThan(InvitePeopleChipPanel.ChipHeight * 2);
        rig.Input.Bounds.Width.ShouldBeGreaterThanOrEqualTo(InvitePeopleChipPanel.InputMinWidth);
        foreach (var child in panel.Children)
        {
            child.Bounds.Right.ShouldBeLessThanOrEqualTo(panel.Bounds.Width + 0.5);
            child.Bounds.Height.ShouldBe(InvitePeopleChipPanel.ChipHeight);
        }
    }

    [AvaloniaFact]
    public void Flat_Drops_The_Card()
    {
        using var rig = new Rig(configure: b => b.IsFramed = false);
        rig.Block.Classes.Contains(":framed").ShouldBeFalse();
        rig.Block.BorderThickness.ShouldBe(default);
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void A_Picked_Avatar_Flies_Into_Its_Chip_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Type("cla");
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        var avatar = rig.Block.ChipOf(rig.Block.Entries[0])!.Avatar!;
        rig.Block.Flights!.Count.ShouldBe(1);
        avatar.Opacity.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(1200));
        rig.Pump();
        rig.Block.Flights.Count.ShouldBe(0);
        avatar.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Sent_Avatars_Fly_Into_Their_Rows_And_Rows_Open_On_A_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Block.AddText("Clara Weiss, Mateus Rocha");
        clock.Advance(TimeSpan.FromMilliseconds(400));
        rig.Pump();
        _ = rig.Block.SendAsync();
        rig.Pump();
        rig.Block.Flights!.Count.ShouldBe(2);
        var row = rig.Block.RowOf(rig.Block.PendingInvites[0])!;
        clock.Advance(TimeSpan.FromMilliseconds(80));
        rig.Pump();
        row.Slot.Bounds.Height.ShouldBeLessThan(45);
        clock.Advance(TimeSpan.FromMilliseconds(1200));
        rig.Pump();
        row.Slot.Bounds.Height.ShouldBe(52, 1);
        rig.Block.Flights.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Refused_Chips_Shake_Under_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Block.AddText("nobody");
        clock.Advance(TimeSpan.FromMilliseconds(600));
        rig.Pump();
        var face = rig.Block.ChipOf(rig.Block.Entries[0])!.Face;
        _ = rig.Block.SendAsync();
        clock.Advance(TimeSpan.FromMilliseconds(70));
        rig.Pump();
        ((TranslateTransform)face.RenderTransform!).X.ShouldBe(6, 1.5);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        rig.Pump();
        ((TranslateTransform)face.RenderTransform!).X.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Places_Everything_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Reduced);
        rig.Type("cla");
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        rig.Block.Flights!.Count.ShouldBe(0);
        rig.Block.AddText("nobody");
        rig.Pump();
        rig.Block.ChipOf(rig.Block.Entries[1])!.Face.RenderTransform.ShouldBeNull();
        rig.Block.Meter!.Drawn.New.ShouldBe(rig.Block.Meter.Target.New);
    }

    #endregion

    #region Gallery

    [AvaloniaFact]
    public void Every_Demo_Builds_And_Shows()
    {
        Control[] demos =
        [
            new InvitePeoplePreviewDemo(), new InvitePeopleFailureDemo(), new InvitePeopleEventLogDemo(),
            new InvitePeopleFlatDemo(), new InvitePeopleNarrowDemo()
        ];
        foreach (var demo in demos)
        {
            var window = TestHelpers.Show(demo);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var block = TestHelpers.Descendant<InvitePeople>(demo);
            block.SeatsUsed.ShouldBe(6, demo.GetType().Name);
            block.Input.ShouldNotBeNull();
            window.Close();
        }
    }

    #endregion
}

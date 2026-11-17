using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// <see cref="TeamMembers"/> on invented samples (Northwind Lab: five members with the owner first, two pending invites, ten
/// seats): counts and seats, the invite field (separators, errors, duplicates, seat warnings, Backspace, Enter to send), the
/// async actions with their success and failure paths, the inline removal confirmation and its focus rules, role changes,
/// search, breakpoints, read-only, screen reader names and the motion levels.
/// </summary>
public class TeamMembersTests
{
    private sealed class Rig
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 900, Action<TeamMembers>? configure = null, int extraInvites = 0)
        {
            Block = new TeamMembers
            {
                Description = "Choose who can work in the Northwind Lab workspace.",
                WorkspaceName = "Northwind Lab",
            };
            Block.Members.Add(new TeamMembersPerson { Name = "Lena Hart", Email = "lena@northwind.example", IsOnline = true, LastActive = "Active now", IsOwner = true, IsCurrentUser = true });
            Block.Members.Add(new TeamMembersPerson { Name = "Theo Brandt", Email = "theo@northwind.example", IsOnline = true, LastActive = "Active now", Role = "admin" });
            Block.Members.Add(new TeamMembersPerson { Name = "Ines Kowal", Email = "ines@northwind.example", LastActive = "Active 3 hours ago", Role = "member" });
            Block.Members.Add(new TeamMembersPerson { Name = "Ravi Malhotra", Email = "ravi@northwind.example", LastActive = "Active yesterday", Role = "member" });
            Block.Members.Add(new TeamMembersPerson { Name = "Mei Tanaka", Email = "mei@northwind.example", LastActive = "Active Oct 2", Role = "viewer" });
            Block.Invites.Add(new TeamMembersInvite { Email = "owen.park@northwind.example", Role = "member", Detail = "Invited Oct 7" });
            Block.Invites.Add(new TeamMembersInvite { Email = "sara.lind@coastal.studio", Role = "viewer", Detail = "Invited Oct 8" });
            for (var i = 0; i < extraInvites; i++)
            {
                Block.Invites.Add(new TeamMembersInvite { Email = $"guest{i}@coastal.studio", Role = "viewer" });
            }
            configure?.Invoke(Block);
            Block.InvitesSent += (_, e) => Sent.Add(e);
            Block.InviteResent += (_, e) => Resent.Add(e.Invite);
            Block.InviteRevoked += (_, e) => Revoked.Add(e.Invite);
            Block.MemberRemoved += (_, e) => Removed.Add(e.Member);
            Block.MemberRoleChanged += (_, e) => RoleChanges.Add(e);
            Block.ActionFailed += (_, e) => Failures.Add(e);
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1000;
            Window.Height = 1400;
            Pump();
        }

        public TeamMembers Block { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<TeamMembersInvitesSentEventArgs> Sent { get; } = new();

        public List<TeamMembersInvite> Resent { get; } = new();

        public List<TeamMembersInvite> Revoked { get; } = new();

        public List<TeamMembersPerson> Removed { get; } = new();

        public List<TeamMembersRoleChangedEventArgs> RoleChanges { get; } = new();

        public List<TeamMembersActionFailedEventArgs> Failures { get; } = new();

        public TeamMembersEmailField Field => Block.Field!;

        public Button Send => Block.SendButton!;

        public List<TeamMembersMemberRow> MemberRows => Block.MemberRows.ToList();

        public List<TeamMembersInviteRow> InviteRows => Block.InviteRows.ToList();

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Type(string text)
        {
            Field.FocusInput();
            foreach (var c in text)
            {
                Window.KeyTextInput(c.ToString());
            }
            Pump();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Pump();
        }

        public void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }

        public void WaitFor(Func<bool> condition) => TestHelpers.PumpUntil(condition, 5000).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ShowsTheSeatsAndBothLists()
    {
        var rig = new Rig();
        rig.Block.SeatsUsed.ShouldBe(7);
        rig.Block.SeatsRemaining.ShouldBe(3);
        rig.Block.SeatsUsedDigits!.Value.ShouldBe(7);
        rig.Block.SeatBar!.Value.ShouldBe(7);
        rig.Block.SeatBar.Maximum.ShouldBe(10);
        rig.MemberRows.Count.ShouldBe(5);
        rig.InviteRows.Count.ShouldBe(2);
        rig.Block.MembersCountDigits!.Value.ShouldBe(5);
        rig.Block.PendingCountDigits!.Value.ShouldBe(2);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusIdle);
        rig.Send.Content.ShouldBe("Send invite");
        rig.Send.IsEnabled.ShouldBeFalse();
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Hint);
        rig.Block.Classes.Contains(TeamMembers.FullPseudoClass).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void TheOwnerRowHasNoMenuAndNoRemoveButton()
    {
        var rig = new Rig();
        var owner = rig.MemberRows[0];
        owner.RolePicker!.IsVisible.ShouldBeFalse();
        owner.RemoveButton!.IsVisible.ShouldBeFalse();
        owner.Classes.Contains(TeamMembersMemberRow.OwnerPseudoClass).ShouldBeTrue();
        var theo = rig.MemberRows[1];
        theo.RolePicker!.IsVisible.ShouldBeTrue();
        theo.RolePicker.DisplayText.ShouldBe("Admin");
        theo.RemoveButton!.IsVisible.ShouldBeTrue();
        rig.InviteRows[0].AvatarControl!.Initials.ShouldBe("OP");
        rig.InviteRows[0].DetailText.ShouldBe("Member · Invited Oct 7");
    }

    [AvaloniaFact]
    public void SeparatorsAndEnterCommitChips()
    {
        var rig = new Rig();
        rig.Type("zed@acme.io");
        rig.Block.SendCount.ShouldBe(1);
        rig.Send.IsEnabled.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Block.PendingEmails.ShouldBe(["zed@acme.io"]);
        rig.Field.Draft.ShouldBeEmpty();
        rig.Type("a1@x.io,b2@x.io;c3@x.io ");
        rig.Block.PendingEmails.ShouldBe(["zed@acme.io", "a1@x.io", "b2@x.io", "c3@x.io"]);
        rig.Send.Content.ShouldBe("Send 4 invites");
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Warning);
        rig.Send.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void AValidDraftCountsInTheSendLabel()
    {
        var rig = new Rig();
        rig.Block.AddEmails("a1@x.io");
        rig.Type("b2@x.io");
        rig.Send.Content.ShouldBe("Send 2 invites");
        rig.Block.SendCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public void InvalidTextStaysWithAnErrorThatTypingClears()
    {
        var rig = new Rig();
        rig.Type("bad");
        rig.Key(PhysicalKey.Enter);
        rig.Field.Draft.ShouldBe("bad");
        rig.Field.IsInvalid.ShouldBeTrue();
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Error);
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().Invalid("bad"));
        rig.Type("x");
        rig.Field.IsInvalid.ShouldBeFalse();
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Hint);
    }

    [AvaloniaFact]
    public void DuplicatesDropSilentlyAndMembersOrInvitesRaiseErrors()
    {
        var rig = new Rig();
        rig.Block.AddEmails("zed@acme.io");
        rig.Block.AddEmails("ZED@acme.io").ShouldBeEmpty();
        rig.Block.PendingEmails.Count.ShouldBe(1);
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Hint);

        rig.Block.AddEmails("theo@northwind.example");
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().AlreadyMember("Theo Brandt"));
        rig.Block.AddEmails("owen.park@northwind.example");
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().AlreadyInvited("owen.park@northwind.example"));
        rig.Block.PendingEmails.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void PastedListsAreSplit()
    {
        var rig = new Rig();
        rig.Field.HandlePaste("a1@x.io, b2@x.io\nbad").ShouldBeTrue();
        rig.Pump();
        rig.Block.PendingEmails.ShouldBe(["a1@x.io", "b2@x.io"]);
        rig.Field.Draft.ShouldBe("bad");
        rig.Field.HandlePaste("single@x.io").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void BackspaceOnAnEmptyFieldRemovesTheLastChip()
    {
        var rig = new Rig();
        rig.Block.AddEmails("a1@x.io b2@x.io");
        rig.Field.FocusInput();
        rig.Key(PhysicalKey.Backspace);
        rig.Block.PendingEmails.ShouldBe(["a1@x.io"]);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusChipRemoved("b2@x.io"));
        rig.Block.Announcer!.History.ShouldContain(rig.Block.StatusText);
    }

    [AvaloniaFact]
    public void AChipsRemoveButtonRemovesItAndReturnsFocusToTheInput()
    {
        var rig = new Rig();
        rig.Block.AddEmails("a1@x.io b2@x.io");
        rig.Pump();
        var chip = rig.Field.ChipOf("a1@x.io")!;
        AutomationProperties.GetName(chip.RemoveButton!).ShouldBe(new TeamMembersLabels().RemoveChipName("a1@x.io"));
        chip.RemoveButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        rig.Pump();
        rig.Block.PendingEmails.ShouldBe(["b2@x.io"]);
        rig.Field.Input!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void OverTheFreeSeatsSendIsDisabledWithAWarning()
    {
        var rig = new Rig();
        rig.Block.AddEmails("a@x.io b@x.io c@x.io d@x.io e@x.io");
        rig.Send.Content.ShouldBe("Send 5 invites");
        rig.Send.IsEnabled.ShouldBeFalse();
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().OverLimit(3, 2));
        rig.Block.SendInvitesAsync().Result.ShouldBeFalse();
        rig.Block.RemovePendingEmail("e@x.io");
        rig.Block.RemovePendingEmail("d@x.io");
        rig.Send.IsEnabled.ShouldBeTrue();
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Hint);
    }

    [AvaloniaFact]
    public void FullSeatsTintTheBarAndWarn()
    {
        var rig = new Rig(extraInvites: 3);
        rig.Block.SeatsRemaining.ShouldBe(0);
        rig.Block.Classes.Contains(TeamMembers.FullPseudoClass).ShouldBeTrue();
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().Full(10));
        rig.Block.AddEmails("a@x.io");
        rig.Block.MessageText.ShouldBe(new TeamMembersLabels().FullWithChips(10));
        rig.Send.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task EnterOnAnEmptyFieldSendsAndTheInvitesJoinTheList()
    {
        var rig = new Rig();
        rig.Block.AddEmails("lena.v@corp.io omar@corp.io");
        rig.Field.FocusInput();
        rig.Key(PhysicalKey.Enter);
        rig.WaitFor(() => rig.Sent.Count == 1);
        rig.Sent[0].Invites.Select(i => i.Email).ShouldBe(["lena.v@corp.io", "omar@corp.io"]);
        rig.Sent[0].Role.ShouldBe("member");
        rig.Block.EffectiveInvites.Count.ShouldBe(4);
        rig.Block.Invites[2].Detail.ShouldBe(new TeamMembersLabels().InvitedJustNow);
        rig.Block.PendingEmails.ShouldBeEmpty();
        rig.Block.SeatsUsed.ShouldBe(9);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusInvitedMany(2, "Member"));
        rig.InviteRows.Count.ShouldBe(4);
        rig.InviteRows[3].AvatarControl!.Initials.ShouldBe("O");
        (await rig.Block.SendInvitesAsync()).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task SendingSpinsAndLocksTheFieldUntilTheHandlerFinishes()
    {
        var gate = new TaskCompletionSource();
        TeamMembersSendRequest? request = null;
        var rig = new Rig(configure: b => b.SendInvitesAction = async (r, _) =>
        {
            request = r;
            await gate.Task;
        });
        rig.Block.InviteRole = "viewer";
        rig.Block.AddEmails("zed@acme.io");
        var send = rig.Block.SendInvitesAsync();
        rig.Pump();
        rig.Block.IsSending.ShouldBeTrue();
        rig.Send.IsLoading.ShouldBeTrue();
        rig.Field.IsBusy.ShouldBeTrue();
        rig.Block.InviteRolePicker!.IsEnabled.ShouldBeFalse();
        request!.Emails.ShouldBe(["zed@acme.io"]);
        request.Role.ShouldBe("viewer");
        gate.SetResult();
        (await send).ShouldBeTrue();
        rig.Pump();
        rig.Block.IsSending.ShouldBeFalse();
        rig.Send.IsLoading.ShouldBeFalse();
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusInvitedOne("zed@acme.io", "Viewer"));
    }

    [AvaloniaFact]
    public async Task AFailedSendKeepsTheChipsAndShowsTheError()
    {
        var rig = new Rig(configure: b => b.SendInvitesAction = (_, _) => throw new InvalidOperationException("Mail server is down"));
        rig.Block.AddEmails("zed@acme.io");
        (await rig.Block.SendInvitesAsync()).ShouldBeFalse();
        rig.Pump();
        rig.Block.PendingEmails.ShouldBe(["zed@acme.io"]);
        rig.Block.EffectiveInvites.Count.ShouldBe(2);
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Error);
        rig.Block.MessageText.ShouldBe("Mail server is down");
        rig.Block.StatusText.ShouldBe("Mail server is down");
        rig.Block.Classes.Contains(TeamMembers.StatusErrorPseudoClass).ShouldBeTrue();
        rig.Block.Alert!.History.ShouldContain("Mail server is down");
        rig.Failures.Single().Action.ShouldBe(TeamMembersAction.Send);
        rig.Field.IsBusy.ShouldBeFalse();
        rig.Send.IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ResendReadsSentForAMomentAndRewritesTheDetail()
    {
        var previous = TeamMembersInviteRow.SentDuration;
        TeamMembersInviteRow.SentDuration = TimeSpan.FromMilliseconds(150);
        try
        {
            var gate = new TaskCompletionSource();
            var rig = new Rig(configure: b => b.ResendInviteAction = async (_, _) => await gate.Task);
            var row = rig.InviteRows[0];
            var resend = rig.Block.ResendAsync(row.Invite);
            rig.Pump();
            row.ResendState.ShouldBe(TeamMembersResendState.Sending);
            row.ResendButton!.IsLoading.ShouldBeTrue();
            gate.SetResult();
            (await resend).ShouldBeTrue();
            rig.Pump();
            row.ResendState.ShouldBe(TeamMembersResendState.Sent);
            AutomationProperties.GetName(row.ResendButton).ShouldBe(new TeamMembersLabels().SentName("owen.park@northwind.example"));
            row.DetailText.ShouldBe("Member · Resent just now");
            rig.Resent.Single().ShouldBe(row.Invite);
            rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusResent("owen.park@northwind.example"));
            rig.WaitFor(() => row.ResendState == TeamMembersResendState.Idle);
            row.ResendButton.Content.ShouldBe("Resend");
        }
        finally
        {
            TeamMembersInviteRow.SentDuration = previous;
        }
    }

    [AvaloniaFact]
    public async Task AFailedResendGoesBackAndReportsInTheFooter()
    {
        var rig = new Rig(configure: b => b.ResendInviteAction = (_, _) => Task.FromException(new Exception("nope")));
        var row = rig.InviteRows[1];
        (await rig.Block.ResendAsync(row.Invite)).ShouldBeFalse();
        row.ResendState.ShouldBe(TeamMembersResendState.Idle);
        row.DetailText.ShouldBe("Viewer · Invited Oct 8");
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().ResendFailed("sara.lind@coastal.studio"));
        rig.Failures.Single().Action.ShouldBe(TeamMembersAction.Resend);
    }

    [AvaloniaFact]
    public void RevokeRemovesTheInviteAtOnceAndFreesTheSeat()
    {
        var rig = new Rig(extraInvites: 3);
        rig.Block.Classes.Contains(TeamMembers.FullPseudoClass).ShouldBeTrue();
        var row = rig.InviteRows[0];
        rig.Click(row.RevokeButton!);
        rig.Block.EffectiveInvites.Count.ShouldBe(4);
        rig.Revoked.Single().Email.ShouldBe("owen.park@northwind.example");
        rig.Block.SeatsUsed.ShouldBe(9);
        rig.Block.Classes.Contains(TeamMembers.FullPseudoClass).ShouldBeFalse();
        rig.Block.MessageKind.ShouldBe(TeamMembersMessageKind.Hint);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusRevoked("owen.park@northwind.example"));
    }

    [AvaloniaFact]
    public async Task AFailedRevokeKeepsTheRow()
    {
        var rig = new Rig(configure: b => b.RevokeInviteAction = (_, _) => Task.FromException(new Exception("no")));
        (await rig.Block.RevokeAsync(rig.Block.Invites[0])).ShouldBeFalse();
        rig.Block.EffectiveInvites.Count.ShouldBe(2);
        rig.InviteRows[0].IsRevoking.ShouldBeFalse();
        rig.Failures.Single().Action.ShouldBe(TeamMembersAction.Revoke);
    }

    [AvaloniaFact]
    public void RemoveAsksInTheRowAndEscapeCancels()
    {
        var rig = new Rig();
        var row = rig.MemberRows[2];
        rig.Block.RequestRemove(row.Person).ShouldBeTrue();
        rig.Pump();
        row.IsConfirming.ShouldBeTrue();
        row.Classes.Contains(TeamMembersMemberRow.ConfirmingPseudoClass).ShouldBeTrue();
        row.CancelButton!.IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        row.IsConfirming.ShouldBeFalse();
        row.RemoveButton!.IsFocused.ShouldBeTrue();
        rig.Block.RequestRemove(rig.MemberRows[0].Person).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void OnlyOneRowConfirmsAtATime()
    {
        var rig = new Rig();
        rig.Click(rig.MemberRows[2].RemoveButton!);
        rig.Click(rig.MemberRows[3].RemoveButton!);
        rig.MemberRows[2].IsConfirming.ShouldBeFalse();
        rig.MemberRows[3].IsConfirming.ShouldBeTrue();
        rig.Click(rig.MemberRows[3].CancelButton!);
        rig.MemberRows[3].IsConfirming.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ConfirmingRemovesTheMemberAndMovesFocusToTheNextRemoveButton()
    {
        var rig = new Rig();
        var row = rig.MemberRows[2];
        var next = rig.MemberRows[3];
        rig.Block.RequestRemove(row.Person);
        rig.Pump();
        rig.Click(row.ConfirmButton!);
        rig.Removed.Single().Name.ShouldBe("Ines Kowal");
        rig.Block.EffectiveMembers.Count.ShouldBe(4);
        rig.Block.SeatsUsed.ShouldBe(6);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusMemberRemoved("Ines Kowal", "Northwind Lab"));
        next.RemoveButton!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task AFailedRemovalKeepsTheConfirmationOpen()
    {
        var gate = new TaskCompletionSource();
        var rig = new Rig(configure: b => b.RemoveMemberAction = async (_, _) =>
        {
            await gate.Task;
            throw new Exception("denied");
        });
        var row = rig.MemberRows[1];
        rig.Block.RequestRemove(row.Person);
        var remove = rig.Block.RemoveMemberAsync(row.Person);
        rig.Pump();
        row.ConfirmButton!.IsLoading.ShouldBeTrue();
        gate.SetResult();
        (await remove).ShouldBeFalse();
        row.IsConfirming.ShouldBeTrue();
        row.ConfirmButton.IsLoading.ShouldBeFalse();
        rig.Block.EffectiveMembers.Count.ShouldBe(5);
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().RemoveFailed("Theo Brandt"));
    }

    [AvaloniaFact]
    public void TheRoleMenuListsTheRolesAndChoosingChangesTheRole()
    {
        var rig = new Rig();
        var picker = rig.MemberRows[2].RolePicker!;
        picker.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        picker.IsMenuOpen.ShouldBeTrue();
        picker.Classes.Contains(TeamMembersRolePicker.OpenPseudoClass).ShouldBeTrue();
        picker.MenuItems.Select(i => i.Label).ShouldBe(["Admin", "Member", "Viewer"]);
        picker.Highlighted!.Option.Key.ShouldBe("member");
        picker.MenuItems[1].IsChecked.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowDown);
        picker.Highlighted!.Option.Key.ShouldBe("viewer");
        rig.Key(PhysicalKey.Enter);
        picker.IsMenuOpen.ShouldBeFalse();
        picker.IsFocused.ShouldBeTrue();
        rig.Block.Members[2].Role.ShouldBe("viewer");
        picker.DisplayText.ShouldBe("Viewer");
        rig.RoleChanges.Single().OldRole.ShouldBe("member");
        rig.RoleChanges[0].NewRole.ShouldBe("viewer");
        rig.Block.StatusText.ShouldBe(new TeamMembersLabels().StatusRoleChanged("Ines Kowal", "Viewer"));
    }

    [AvaloniaFact]
    public void EscapeClosesTheRoleMenuWithoutAChange()
    {
        var rig = new Rig();
        var picker = rig.Block.InviteRolePicker!;
        picker.OpenMenu(keyboard: true);
        rig.Pump();
        rig.Key(PhysicalKey.Escape);
        picker.IsMenuOpen.ShouldBeFalse();
        rig.Block.InviteRole.ShouldBe("member");
        picker.OpenMenu(keyboard: true);
        rig.Pump();
        rig.Key(PhysicalKey.ArrowUp);
        rig.Key(PhysicalKey.Enter);
        rig.Block.InviteRole.ShouldBe("admin");
    }

    [AvaloniaFact]
    public async Task AFailedRoleChangePutsTheOldRoleBack()
    {
        var rig = new Rig(configure: b => b.ChangeRoleAction = (_, _) => Task.FromException(new Exception("no")));
        (await rig.Block.ChangeRoleAsync(rig.Block.Members[1], "viewer")).ShouldBeFalse();
        rig.Block.Members[1].Role.ShouldBe("admin");
        rig.MemberRows[1].RolePicker!.DisplayText.ShouldBe("Admin");
        rig.Failures.Single().Action.ShouldBe(TeamMembersAction.ChangeRole);
        (await rig.Block.ChangeRoleAsync(rig.Block.Members[0], "viewer")).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void SearchFiltersMembersAndInvitesTogether()
    {
        var rig = new Rig();
        rig.Block.SearchText = "ravi";
        rig.Pump();
        rig.Block.MembersCountDigits!.Value.ShouldBe(1);
        rig.Block.SlotOf(rig.Block.Members[3])!.IsCollapsed.ShouldBeFalse();
        rig.Block.SlotOf(rig.Block.Members[0])!.IsCollapsed.ShouldBeTrue();
        rig.Block.PendingCountDigits!.Value.ShouldBe(0);
        rig.Block.InvitesEmptySlot!.IsCollapsed.ShouldBeFalse();
        rig.Block.InvitesEmptyRow!.Text.ShouldBe(new TeamMembersLabels().NoInvitesMatch);
        rig.Block.MembersEmptySlot!.IsCollapsed.ShouldBeTrue();
        rig.Block.SeatsUsed.ShouldBe(7);

        rig.Block.SearchText = "zzq";
        rig.Pump();
        rig.Block.MembersEmptySlot.IsCollapsed.ShouldBeFalse();
        rig.Block.MembersEmptyRow!.Text.ShouldBe(new TeamMembersLabels().NoMembersMatch("zzq"));
        rig.Click(rig.Block.MembersEmptyRow.ClearButton!);
        rig.Block.SearchText.ShouldBeEmpty();
        rig.Block.Search!.Text.ShouldBeEmpty();
        rig.Block.MembersEmptySlot.IsCollapsed.ShouldBeTrue();
        rig.Block.Members.All(m => !rig.Block.SlotOf(m)!.IsCollapsed).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void TypingInTheSearchFieldUpdatesSearchText()
    {
        var rig = new Rig();
        rig.Block.Search!.Text = "coastal";
        rig.Pump();
        rig.Block.SearchText.ShouldBe("coastal");
        rig.Block.PendingCountDigits!.Value.ShouldBe(1);
        rig.Block.MembersCountDigits!.Value.ShouldBe(0);
    }

    [AvaloniaFact]
    public void TheFirstVisibleRowHasNoDivider()
    {
        var rig = new Rig();
        rig.MemberRows[0].Classes.Contains(TeamMembersRowBase.FirstPseudoClass).ShouldBeTrue();
        rig.MemberRows[1].Classes.Contains(TeamMembersRowBase.FirstPseudoClass).ShouldBeFalse();
        rig.InviteRows[0].Classes.Contains(TeamMembersRowBase.FirstPseudoClass).ShouldBeTrue();
        rig.Block.SearchText = "ines";
        rig.Pump();
        rig.MemberRows[2].Classes.Contains(TeamMembersRowBase.FirstPseudoClass).ShouldBeTrue();
    }

    [AvaloniaTheory]
    [InlineData(900, "Wide", false, false)]
    [InlineData(620, "Narrow", false, false)]
    [InlineData(560, "Narrow", true, false)]
    [InlineData(460, "Narrow", true, true)]
    [InlineData(400, "TwoTier", true, true)]
    public void BreakpointsFollowTheBlocksWidth(double width, string modeName, bool stacked, bool actionsBelow)
    {
        var mode = Enum.Parse<TeamMembersRowMode>(modeName);
        var rig = new Rig(width: width);
        rig.Block.RowMode.ShouldBe(mode);
        rig.Block.Classes.Contains(TeamMembers.StackedPseudoClass).ShouldBe(stacked);
        rig.Block.Classes.Contains(TeamMembers.ActionsBelowPseudoClass).ShouldBe(actionsBelow);
        rig.MemberRows[1].Mode.ShouldBe(mode);
        rig.InviteRows[0].ActionsBelow.ShouldBe(actionsBelow);
        rig.Block.HeaderPanel!.IsStacked.ShouldBe(stacked);
        rig.Block.InviteFormPanel!.IsStacked.ShouldBe(stacked);
        rig.Block.MembersHeadPanel!.IsStacked.ShouldBe(stacked);
        var row = rig.MemberRows[1];
        var expected = mode switch
        {
            TeamMembersRowMode.Wide => 52.0,
            TeamMembersRowMode.Narrow => 69.0,
            _ => 84.0
        };
        row.Bounds.Height.ShouldBe(expected, 2.0);
    }

    [AvaloniaFact]
    public void WideRowsKeepTheirColumns()
    {
        var rig = new Rig(width: 900);
        var row = rig.MemberRows[1];
        var panel = row.LayoutPanel!;
        // 900 − 2 × 1 card border − 2 × 20 padding = 858; the view ends with 128 + 112 + 32 and three 12px gaps.
        row.ActiveText!.Bounds.Width.ShouldBeLessThanOrEqualTo(128);
        row.ActiveText.TranslatePoint(default, panel)!.Value.X.ShouldBe(858 - 32 - 12 - 112 - 12 - 128, 0.5);
        row.RemoveButton!.Bounds.Width.ShouldBe(32);
    }

    [AvaloniaFact]
    public void ReadOnlyHidesTheInviteFormAndTheActions()
    {
        var rig = new Rig(configure: b => b.IsReadOnly = true);
        TestHelpers.Descendant<Border>(rig.Block, TeamMembers.FormPart).IsVisible.ShouldBeFalse();
        rig.MemberRows[1].RemoveButton!.IsVisible.ShouldBeFalse();
        rig.Block.RequestRemove(rig.Block.Members[1]).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void FramedIsTheDefaultAndFlatDropsTheCard()
    {
        var rig = new Rig();
        rig.Block.Classes.Contains(TeamMembers.FramedPseudoClass).ShouldBeTrue();
        rig.Block.IsFramed = false;
        rig.Pump();
        rig.Block.Classes.Contains(TeamMembers.FramedPseudoClass).ShouldBeFalse();
        rig.Block.BorderThickness.ShouldBe(default);
    }

    [AvaloniaFact]
    public void ScreenReaderNamesComeFromTheLabels()
    {
        var labels = new TeamMembersLabels { RoleFor = n => $"Rolle von {n}", RemoveName = n => $"{n} entfernen", SendOne = "Einladen" };
        var rig = new Rig(configure: b => b.Labels = labels);
        AutomationProperties.GetName(rig.MemberRows[1].RolePicker!).ShouldBe("Rolle von Theo Brandt");
        AutomationProperties.GetName(rig.MemberRows[1].RemoveButton!).ShouldBe("Theo Brandt entfernen");
        AutomationProperties.GetName(rig.MemberRows[0].AvatarControl!).ShouldBe("Lena Hart, online");
        AutomationProperties.GetName(rig.InviteRows[0].RevokeButton!).ShouldBe(labels.RevokeName("owen.park@northwind.example"));
        AutomationProperties.GetHelpText(rig.Block.SeatBar!).ShouldBe("7 of 10 seats used");
        rig.Send.Content.ShouldBe("Einladen");
    }

    [AvaloniaFact]
    public void AReadOnlySourceIsFollowedAndTheHostAddsTheInvites()
    {
        var invites = new System.Collections.ObjectModel.ObservableCollection<TeamMembersInvite>
        {
            new() { Email = "owen.park@northwind.example", Role = "member" }
        };
        var view = new System.Collections.ObjectModel.ReadOnlyObservableCollection<TeamMembersInvite>(invites);
        var rig = new Rig(configure: b =>
        {
            b.Invites.Clear();
            b.InvitesSource = view;
            b.InvitesSent += (_, e) =>
            {
                foreach (var invite in e.Invites)
                {
                    invites.Add(invite);
                }
            };
        });
        rig.InviteRows.Count.ShouldBe(1);
        rig.Block.AddEmails("zed@acme.io");
        rig.Block.SendInvitesAsync().Result.ShouldBeTrue();
        rig.Pump();
        rig.InviteRows.Count.ShouldBe(2);
        rig.Block.SeatsUsed.ShouldBe(7);
    }

    [AvaloniaFact]
    public void FullMotionGrowsNewRowsAndFliesTheAddressesIn()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Full);
        rig.Block.AddEmails("lena.v@corp.io omar@corp.io");
        rig.Pump();
        rig.Block.SendInvitesAsync().Result.ShouldBeTrue();
        rig.Pump();
        var slot = rig.Block.SlotOf(rig.Block.Invites[3])!;
        slot.Reveal.ShouldBeLessThan(0.2);
        rig.Block.Flights!.Ghosts.Count().ShouldBe(2);
        rig.InviteRows[3].EmailBadge!.Opacity.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        rig.Pump();
        rig.InviteRows[2].AvatarControl!.PopProgress.ShouldBe(0);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        rig.Pump();
        slot.Reveal.ShouldBeGreaterThan(0.5);
        rig.InviteRows[2].AvatarControl!.PopProgress.ShouldBeGreaterThan(0);
        rig.Block.Flights.Ghosts.Count().ShouldBe(2);
        clock.Advance(TimeSpan.FromSeconds(2));
        rig.Pump();
        rig.WaitFor(() => !rig.Block.Flights.Ghosts.Any());
        slot.Reveal.ShouldBe(1);
        rig.InviteRows[3].EmailBadge!.Opacity.ShouldBe(1);
        rig.InviteRows[3].AvatarControl!.PopProgress.ShouldBe(1, 0.01);
    }

    [AvaloniaFact]
    public void FullMotionPopsChipsIn()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Full);
        rig.Block.AddEmails("zed@acme.io");
        rig.Pump();
        var chip = rig.Field.ChipOf("zed@acme.io")!;
        chip.Opacity.ShouldBeLessThan(0.5);
        clock.Advance(TimeSpan.FromMilliseconds(400));
        rig.Pump();
        chip.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void ReducedMotionSkipsTheFlight()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Reduced);
        rig.Block.AddEmails("zed@acme.io");
        rig.Block.SendInvitesAsync().Result.ShouldBeTrue();
        rig.Pump();
        rig.Block.Flights!.Ghosts.ShouldBeEmpty();
        rig.Block.SlotOf(rig.Block.Invites[2])!.Reveal.ShouldBe(1);
        rig.InviteRows[2].EmailBadge!.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void FullMotionRemovesRowsAfterTheirExit()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Full);
        var slot = rig.Block.SlotOf(rig.Block.Invites[0])!;
        rig.Block.RevokeAsync(rig.Block.Invites[0]).Result.ShouldBeTrue();
        rig.Pump();
        slot.IsLeaving.ShouldBeTrue();
        rig.Block.SeatsUsed.ShouldBe(6);
        clock.Advance(TimeSpan.FromSeconds(1));
        rig.Pump();
        rig.WaitFor(() => slot.Parent is null);
        rig.InviteRows.Count.ShouldBe(1);
    }
}

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Publishing;

/// <summary>
/// The share dialog: address rules, the rows (owner, names, pending, you), refusing addresses, inviting (with and without
/// a handler, and its failure), role changes, removal and focus, link access and its pickers, labels and the narrow layout.
/// </summary>
public class ShareAccessTests
{
    private static (ShareAccess Share, Window Window) Open(double width = 500, ArcMotionLevel level = ArcMotionLevel.Off,
                                                          Action<ShareAccess>? setup = null)
    {
        var share = new ShareAccess
        {
            Width = width,
            DocumentName = "Trail plan",
            OrganizationName = "Fernhollow",
            Link = "https://notes.example/d/1",
            CurrentUserEmail = "mira@fern.example"
        };
        share.People.Add(new ShareAccessPerson { Name = "Mira Holt", Email = "mira@fern.example", Role = ShareAccessRole.Owner });
        share.People.Add(new ShareAccessPerson { Name = "Tobias Brandt", Email = "tobias@fern.example", Role = ShareAccessRole.Editor });
        share.People.Add(new ShareAccessPerson { Email = "guest@trail.example", Role = ShareAccessRole.Viewer, IsPending = true });
        setup?.Invoke(share);
        var window = TestHelpers.Show(share, level);
        window.Width = 700;
        window.Height = 900;
        Pump();
        return (share, window);
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    // ---- Addresses ----

    [Theory]
    [InlineData("ana@fern.example", "ana@fern.example")]
    [InlineData("  Ana@Fern.Example ", "ana@fern.example")]
    [InlineData("Ana Ruiz <ana@fern.example>", "ana@fern.example")]
    [InlineData("ana@fern", null)]
    [InlineData("ana fern.example", null)]
    [InlineData("@fern.example", null)]
    [InlineData("ana@@fern.example", null)]
    [InlineData("", null)]
    public void Addresses_are_trimmed_lower_cased_and_checked(string text, string? expected)
    {
        ShareAccessEmail.Normalize(text).ShouldBe(expected);
    }

    // ---- Rows ----

    [AvaloniaFact]
    public void Rows_show_names_addresses_pending_invites_and_you()
    {
        var (share, window) = Open();
        var owner = share.RowFor(share.People[0])!;
        owner.RoleButton.ShouldBeNull();
        AutomationProperties.GetName(owner).ShouldBe("Mira Holt");
        owner.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Mira Holt (you)");
        owner.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Owner");

        var editor = share.RowFor(share.People[1])!;
        AutomationProperties.GetName(editor.RoleButton!).ShouldBe("Role of Tobias Brandt: Editor");
        editor.Menu!.Items.OfType<MenuEntry>().Count(e => e.IsChecked).ShouldBe(1);

        var guest = share.RowFor(share.People[2])!;
        guest.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Invite sent");
        window.Close();
    }

    [AvaloniaFact]
    public void Rows_follow_changes_to_a_person()
    {
        var (share, window) = Open();
        share.People[2].Name = "Guest Ranger";
        share.People[2].IsPending = false;
        Pump();
        var row = share.RowFor(share.People[2])!;
        AutomationProperties.GetName(row).ShouldBe("Guest Ranger");
        row.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("guest@trail.example");
        window.Close();
    }

    // ---- Inviting ----

    [AvaloniaFact]
    public void Text_that_is_not_an_address_or_someone_with_access_is_refused()
    {
        var (share, window) = Open();
        var field = share.InviteField!;
        field.AddTag("not an address").ShouldBeTrue();
        Pump();
        field.Value.ShouldBeEmpty();
        field.ErrorMessage.ShouldBe("“not an address” is not an email address");
        field.IsInvalid.ShouldBeTrue();
        share.LastAnnouncement.ShouldBe(field.ErrorMessage);

        field.AddTag("TOBIAS@fern.example");
        Pump();
        field.Value.ShouldBeEmpty();
        field.ErrorMessage.ShouldBe("tobias@fern.example already has access");

        field.AddTag("Ana Ruiz <Ana@Fern.example>");
        Pump();
        field.Value.ShouldBe(["ana@fern.example"]);
        field.IsInvalid.ShouldBeFalse();
        share.InviteButton!.IsEnabled.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Invite_adds_pending_people_with_the_chosen_role()
    {
        var (share, window) = Open();
        ShareAccessInvitedEventArgs? args = null;
        share.PeopleInvited += (_, e) => args = e;
        share.InviteRolePicker!.SelectedIndex = (int)ShareAccessRole.Commenter;
        share.InviteRole.ShouldBe(ShareAccessRole.Commenter);
        share.InviteField!.AddTag("ana@fern.example");
        share.InviteField.AddTag("lee@fern.example");
        Pump();

        var added = await share.InviteAsync();
        Pump();
        added.Count.ShouldBe(2);
        share.People.Count.ShouldBe(5);
        share.People[3].Email.ShouldBe("ana@fern.example");
        share.People[3].Role.ShouldBe(ShareAccessRole.Commenter);
        share.People[3].IsPending.ShouldBeTrue();
        share.RowFor(share.People[4]).ShouldNotBeNull();
        share.InviteField.Value.ShouldBeEmpty();
        share.InviteButton!.IsEnabled.ShouldBeFalse();
        args!.People.Count.ShouldBe(2);
        args.Role.ShouldBe(ShareAccessRole.Commenter);
        share.LastAnnouncement.ShouldBe("Invited 2 people as Commenter");
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_on_an_empty_draft_sends_the_invites()
    {
        var (share, window) = Open();
        share.InviteField!.AddTag("ana@fern.example");
        Pump();
        share.FocusInvite().ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        share.People.Select(p => p.Email).ShouldContain("ana@fern.example");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_failing_handler_keeps_the_chips_and_shows_its_message()
    {
        var (share, window) = Open(setup: s => s.InviteHandler = (_, _, _) => throw new InvalidOperationException("Server says no."));
        share.InviteField!.AddTag("ana@fern.example");
        Pump();
        var added = await share.InviteAsync();
        added.ShouldBeEmpty();
        share.People.Count.ShouldBe(3);
        share.InviteField.Value.ShouldBe(["ana@fern.example"]);
        share.InviteField.ErrorMessage.ShouldBe("Could not send the invite. Server says no.");
        share.IsInviting.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_button_spins_while_the_handler_runs()
    {
        var gate = new TaskCompletionSource();
        IReadOnlyList<string>? sent = null;
        var (share, window) = Open(setup: s => s.InviteHandler = async (emails, _, _) =>
        {
            sent = emails;
            await gate.Task;
        });
        share.InviteField!.AddTag("ana@fern.example");
        Pump();
        var invite = share.InviteAsync();
        Pump();
        share.IsInviting.ShouldBeTrue();
        share.InviteButton!.IsLoading.ShouldBeTrue();
        share.Classes.Contains(":inviting").ShouldBeTrue();
        gate.SetResult();
        (await invite).Count.ShouldBe(1);
        sent.ShouldBe(["ana@fern.example"]);
        share.InviteButton.IsLoading.ShouldBeFalse();
        window.Close();
    }

    // ---- Roles and removal ----

    [AvaloniaFact]
    public void Choosing_a_role_in_the_menu_changes_it()
    {
        var (share, window) = Open();
        ShareAccessRoleChangedEventArgs? args = null;
        share.RoleChanged += (_, e) => args = e;
        var row = share.RowFor(share.People[1])!;
        var viewer = row.Menu!.Items.OfType<MenuEntry>().First(e => e.Tag is ShareAccessRole.Viewer);
        row.Menu.RaiseEvent(new MenuEntryClickedEventArgs(MenuSurface.ItemClickedEvent, viewer, null));
        Pump();
        share.People[1].Role.ShouldBe(ShareAccessRole.Viewer);
        args!.OldRole.ShouldBe(ShareAccessRole.Editor);
        args.NewRole.ShouldBe(ShareAccessRole.Viewer);
        AutomationProperties.GetName(row.RoleButton!).ShouldBe("Role of Tobias Brandt: Viewer");
        share.LastAnnouncement.ShouldBe("Tobias Brandt is now Viewer");
        window.Close();
    }

    [AvaloniaFact]
    public void The_owner_keeps_its_role_and_its_place()
    {
        var (share, window) = Open();
        share.ChangeRole(share.People[0], ShareAccessRole.Viewer);
        share.RemovePerson(share.People[0]);
        share.People[0].Role.ShouldBe(ShareAccessRole.Owner);
        share.People.Count.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Remove_access_takes_the_person_out_and_raises_the_event()
    {
        var (share, window) = Open();
        ShareAccessRemovedEventArgs? args = null;
        share.AccessRemoved += (_, e) => args = e;
        var person = share.People[1];
        var row = share.RowFor(person)!;
        var remove = row.Menu!.Items.OfType<MenuEntry>().Last();
        remove.IsDestructive.ShouldBeTrue();
        remove.Header.ShouldBe("Remove access");
        row.Menu.RaiseEvent(new MenuEntryClickedEventArgs(MenuSurface.ItemClickedEvent, remove, null));
        Pump();
        share.People.ShouldNotContain(person);
        share.RowFor(person).ShouldBeNull();
        args!.Person.ShouldBe(person);
        share.LastAnnouncement.ShouldBe("Removed access for Tobias Brandt");
        window.Close();
    }

    [AvaloniaFact]
    public void Removal_by_keyboard_moves_focus_to_the_next_role_button()
    {
        var (share, window) = Open();
        var next = share.RowFor(share.People[2])!.RoleButton!;
        share.RowFor(share.People[1])!.RoleButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        share.RemovePerson(share.People[1]);
        Pump();
        next.IsFocused.ShouldBeTrue();
        window.Close();
    }

    // ---- Link access ----

    [AvaloniaFact]
    public void Link_scope_shows_its_description_and_the_link_role_picker()
    {
        var (share, window) = Open();
        var changes = new List<(ShareAccessLinkScope, ShareAccessRole)>();
        share.LinkAccessChanged += (_, e) => changes.Add((e.Scope, e.Role));
        share.LinkRoleHost!.IsPresent.ShouldBeFalse();

        share.ScopePicker!.SelectedIndex = 1;
        Pump();
        share.LinkScope.ShouldBe(ShareAccessLinkScope.Organization);
        share.LinkRoleHost.IsPresent.ShouldBeTrue();
        share.GetVisualDescendants().OfType<ArcContentSwap>().Select(s => s.Content as string)
            .ShouldContain("Anyone signed in at Fernhollow can open it with the link");

        share.LinkRolePicker!.SelectedIndex = (int)ShareAccessRole.Editor;
        Pump();
        share.LinkRole.ShouldBe(ShareAccessRole.Editor);
        changes.ShouldBe([(ShareAccessLinkScope.Organization, ShareAccessRole.Viewer), (ShareAccessLinkScope.Organization, ShareAccessRole.Editor)]);

        // Set from code: the pickers follow.
        share.LinkScope = ShareAccessLinkScope.Anyone;
        Pump();
        share.ScopePicker.SelectedIndex.ShouldBe(2);
        share.LinkRole = ShareAccessRole.Owner;
        share.LinkRole.ShouldBe(ShareAccessRole.Viewer);
        window.Close();
    }

    [AvaloniaFact]
    public void Copy_and_done_buttons_use_the_link_and_raise_done()
    {
        var (share, window) = Open();
        share.CopyLinkButton!.Value.ShouldBe("https://notes.example/d/1");
        share.CopyLinkButton.Label.ShouldBe("Copy link");
        var done = 0;
        share.DoneRequested += (_, _) => done++;
        share.DoneButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        done.ShouldBe(1);
        share.Link = null;
        share.CopyLinkButton.IsEnabled.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Labels_translate_every_word()
    {
        var (share, window) = Open(setup: s => s.Labels = new ShareAccessLabels
        {
            Title = name => $"Partager « {name} »",
            Invite = "Inviter",
            Role = role => role.ToString().ToUpperInvariant()
        });
        share.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Partager « Trail plan »");
        share.InviteButton!.Content.ShouldBe("Inviter");
        ((SelectItem)share.InviteRolePicker!.Items[2]!).Content.ShouldBe("EDITOR");
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_dialogs_stack_the_invite_row()
    {
        var (share, window) = Open(width: 360);
        share.Classes.Contains(":narrow").ShouldBeTrue();
        Grid.GetRow(share.InviteButton!).ShouldBe(1);
        share.Width = 500;
        Pump();
        share.Classes.Contains(":narrow").ShouldBeFalse();
        Grid.GetRow(share.InviteButton!).ShouldBe(0);
        Grid.GetColumn(share.InviteButton!).ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void New_rows_grow_in_at_the_full_motion_level()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (share, window) = Open(level: ArcMotionLevel.Full);
        share.People.Add(new ShareAccessPerson { Email = "new@fern.example" });
        Pump();
        var host = share.RowFor(share.People[3])!.Parent as PresenceHost;
        host.ShouldNotBeNull();
        host!.IsPresent.ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1));
        Pump();
        host.Progress.ShouldBe(1, 0.01);
        window.Close();
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// <see cref="RolesPermissions"/> on an invented workspace (five roles with a locked founder, four groups of 4/3/3/3
/// permissions): the change model, cells and group checkboxes, the ripple, duplicate / remove / revert / discard, the save
/// states (success, failure, blocked edits), the grid keyboard, folding, the width tiers, the role menu and screen
/// reader names.
/// </summary>
public class RolesPermissionsTests
{
    private static RolesPermissionsRole Founder() => new("owner", "Founder", 1, "Lena Hart", isLocked: true);

    private static HashSet<RolesPermissionsGrant> SavedSample()
    {
        var g = new HashSet<RolesPermissionsGrant>();
        void Add(string role, params string[] keys)
        {
            foreach (var k in keys)
            {
                g.Add(new RolesPermissionsGrant(role, k));
            }
        }
        Add("lead", "doc.read", "doc.create", "doc.share", "doc.archive", "team.invite", "team.roles", "team.remove", "pay.receipts",
            "acc.history", "acc.tokens");
        Add("member", "doc.read", "doc.create", "doc.share", "team.invite");
        Add("guest", "doc.read");
        Add("finance", "pay.receipts", "pay.card", "pay.plan");
        return g;
    }

    private sealed class Rig
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1100, Action<RolesPermissions>? configure = null)
        {
            Block = new RolesPermissions { WorkspaceName = "Northwind Studio", MemberCount = 31, SavedHoldDuration = TimeSpan.FromMilliseconds(30) };
            Block.Roles.Add(Founder());
            Block.Roles.Add(new RolesPermissionsRole("lead", "Lead", 4, "Omar Diaz and 3 others"));
            Block.Roles.Add(new RolesPermissionsRole("member", "Member", 9));
            Block.Roles.Add(new RolesPermissionsRole("guest", "Guest", 15));
            Block.Roles.Add(new RolesPermissionsRole("finance", "Finance", 2));
            Block.Groups.Add(new RolesPermissionsGroup("documents", "Documents",
                new("doc.read", "Read documents"), new("doc.create", "Create documents"),
                new("doc.share", "Change document sharing"), new("doc.archive", "Archive documents")));
            Block.Groups.Add(new RolesPermissionsGroup("team", "Team",
                new("team.invite", "Send invitations"), new("team.roles", "Assign roles"), new("team.remove", "Remove people")));
            Block.Groups.Add(new RolesPermissionsGroup("payments", "Payments",
                new("pay.receipts", "See receipts"), new("pay.card", "Update card on file"), new("pay.plan", "Switch subscription")));
            Block.Groups.Add(new RolesPermissionsGroup("access", "Access",
                new("acc.saml", "Set up SAML login"), new("acc.history", "Read activity history"), new("acc.tokens", "Rotate access tokens")));
            Block.SavedGrants = SavedSample();
            configure?.Invoke(Block);
            Block.GrantChanged += (_, e) => GrantEvents.Add(e);
            Block.GroupToggled += (_, e) => GroupEvents.Add(e);
            Block.RoleDuplicated += (_, e) => DuplicateEvents.Add(e);
            Block.RoleDeleted += (_, e) => DeleteEvents.Add(e);
            Block.ChangesReverted += (_, e) => RevertEvents.Add(e);
            Block.Saved += (_, e) => SavedEvents.Add(e);
            Block.SaveFailed += (_, e) => FailedEvents.Add(e);
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = Math.Max(width + 40, 600);
            Window.Height = 1100;
            Pump();
        }

        public RolesPermissions Block { get; }

        public Border Host { get; }

        public Window Window { get; }

        public RolesPermissionsGrid Grid => Block.Grid!;

        public List<RolesPermissionsGrantChangedEventArgs> GrantEvents { get; } = new();

        public List<RolesPermissionsGroupToggledEventArgs> GroupEvents { get; } = new();

        public List<RolesPermissionsRoleDuplicatedEventArgs> DuplicateEvents { get; } = new();

        public List<RolesPermissionsRoleDeletedEventArgs> DeleteEvents { get; } = new();

        public List<RolesPermissionsRevertedEventArgs> RevertEvents { get; } = new();

        public List<RolesPermissionsSavedEventArgs> SavedEvents { get; } = new();

        public List<RolesPermissionsSaveFailedEventArgs> FailedEvents { get; } = new();

        public RolesPermissionsCell Cell(string role, string permission)
            => Grid.AllCells().First(c => c.Role.Key == role && c.Permission?.Key == permission && c.IsHitTestVisible);

        public RolesPermissionsCell GroupCell(string role, string group)
            => Grid.AllCells().First(c => c.Role.Key == role && c.IsGroupCell && c.Group.Key == group && c.IsHitTestVisible);

        public RolesPermissionsRoleHeader Header(string role) => Grid.Columns.First(c => c.Role.Key == role && !c.Removing).Header;

        public RolesPermissionsGroupButton Button(string group) => Grid.Rows.First(r => r.Button?.Group.Key == group).Button!;

        public RolesPermissionsGroup Group(string key) => Block.Groups.First(g => g.Key == key);

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Pump();
        }

        public void Focus(Control part)
        {
            part.Focus(NavigationMethod.Tab);
            Pump();
        }

        public Control? Focused => Window.FocusManager?.GetFocusedElement() as Control;

        public void ClickSave()
        {
            Block.SaveButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }
    }

    #region Model

    [AvaloniaFact]
    public void Group_rule_turns_mixed_or_off_on_and_on_off_changing_only_cells_that_differ()
    {
        var model = new RolesPermissionsModel();
        var role = new RolesPermissionsRole("r", "R");
        model.Reset([new("r", "a")], null);
        var keys = new[] { "a", "b", "c" };
        Assert.Equal(RolesPermissionsCheck.Mixed, model.GroupState(role, keys));
        var (changed, value) = model.ToggleGroup(role, keys);
        Assert.True(value);
        Assert.Equal(["b", "c"], changed);
        Assert.Equal(RolesPermissionsCheck.On, model.GroupState(role, keys));
        (changed, value) = model.ToggleGroup(role, keys);
        Assert.False(value);
        Assert.Equal(3, changed.Count);
        Assert.Equal(RolesPermissionsCheck.Off, model.GroupState(role, keys));
    }

    [AvaloniaFact]
    public void Pending_count_is_one_per_changed_cell_plus_one_per_new_role_including_its_own_edits()
    {
        var model = new RolesPermissionsModel();
        var locked = new RolesPermissionsRole("o", "O", isLocked: true);
        var role = new RolesPermissionsRole("r", "R");
        var keys = new[] { "a", "b" };
        model.Reset([new("r", "a")], null);
        Assert.False(model.Set(locked, "a", false));
        Assert.True(model.IsGranted(locked, "b"));
        model.Set(role, "b", true);
        Assert.Equal(1, model.PendingCount([locked, role], keys));
        model.Set(role, "b", false);
        Assert.Equal(0, model.PendingCount([locked, role], keys));

        var copy = new RolesPermissionsRole("r-copy", "R copy");
        model.AddNewRole(role, copy, keys);
        Assert.True(model.IsGranted(copy, "a"));
        Assert.False(model.IsUnsaved(copy, "a"));
        Assert.Equal(1, model.PendingCount([role, copy], keys));
        model.Set(copy, "b", true);
        Assert.True(model.IsUnsaved(copy, "b"));
        Assert.Equal(2, model.PendingCount([role, copy], keys));
        model.Commit();
        Assert.Equal(0, model.PendingCount([role, copy], keys));
        Assert.False(model.IsNew(copy));
    }

    [AvaloniaFact]
    public void Copy_names_and_keys_count_up_while_taken()
    {
        var taken = new List<string> { "Guest" };
        var first = RolesPermissionsModel.CopyName("Guest", "{0} (copy)", "{0} (copy {1})", taken);
        Assert.Equal("Guest (copy)", first);
        taken.Add(first);
        Assert.Equal("Guest (copy 2)", RolesPermissionsModel.CopyName("Guest", "{0} (copy)", "{0} (copy {1})", taken));
        Assert.Equal("Guest (copy) (copy)", RolesPermissionsModel.CopyName(first, "{0} (copy)", "{0} (copy {1})", taken));
        Assert.Equal("guest-copy-2", RolesPermissionsModel.CopyKey("guest", new List<string> { "guest", "guest-copy" }));
    }

    [AvaloniaFact]
    public void Summary_lists_roles_in_column_order_and_sums_their_members()
    {
        var labels = new RolesPermissionsLabels();
        RolesPermissionsRole R(string name, int members) => new(name.ToLowerInvariant(), name, members);
        Assert.Equal("Lead · reaches 4 people", RolesPermissionsModel.Summary([R("Lead", 4)], labels));
        Assert.Equal("Lead and Member · reaches 13 people", RolesPermissionsModel.Summary([R("Lead", 4), R("Member", 9)], labels));
        Assert.Equal("A, B and C · reaches 1 person", RolesPermissionsModel.Summary([R("A", 1), R("B", 0), R("C", 0)], labels));
        Assert.Equal("A, B and 2 more · reaches 10 people",
            RolesPermissionsModel.Summary([R("A", 1), R("B", 2), R("C", 3), R("D", 4)], labels));
        Assert.Equal("Guest (copy) · nobody assigned yet", RolesPermissionsModel.Summary([R("Guest (copy)", 0)], labels));
    }

    #endregion

    #region Cells

    [AvaloniaFact]
    public void Shows_the_saved_grants_with_mixed_groups_and_a_locked_column_and_no_bar()
    {
        var rig = new Rig();
        Assert.Equal(5, rig.Grid.Columns.Count);
        Assert.Equal(RolesPermissionsCheck.On, rig.Cell("lead", "doc.read").State);
        Assert.Equal(RolesPermissionsCheck.Off, rig.Cell("member", "doc.archive").State);
        Assert.Equal(RolesPermissionsCheck.Mixed, rig.GroupCell("member", "documents").State);
        Assert.Equal(RolesPermissionsCheck.On, rig.GroupCell("lead", "documents").State);
        Assert.True(rig.Cell("owner", "acc.saml").IsLocked);
        Assert.False(rig.Block.HasUnsavedChanges);
        Assert.False(rig.Block.IsBarOpen);
        Assert.False(rig.Block.Bar!.IsVisible);
        Assert.Null(rig.Block.Grants);
    }

    [AvaloniaFact]
    public void Switching_a_cell_marks_it_and_its_role_and_opens_the_bar_and_switching_back_clears_it()
    {
        var rig = new Rig();
        var cell = rig.Cell("member", "doc.archive");
        cell.Activate();
        rig.Pump();
        Assert.Equal(RolesPermissionsCheck.On, cell.State);
        Assert.True(cell.IsUnsaved);
        Assert.True(rig.Header("member").HasChanges);
        Assert.Equal(RolesPermissionsCheck.On, rig.GroupCell("member", "documents").State);
        Assert.Equal(1, rig.Block.PendingChangeCount);
        Assert.True(rig.Block.IsBarOpen);
        Assert.Equal("Member · reaches 9 people", rig.Block.BarText);
        Assert.Contains(new RolesPermissionsGrant("member", "doc.archive"), rig.Block.Grants!);
        var e = Assert.Single(rig.GrantEvents);
        Assert.True(e.IsGranted);
        Assert.True(e.IsUnsaved);
        Assert.Equal(RolesPermissionsChangeSource.Cell, e.ChangeSource);
        Assert.Equal("Archive documents turned on for Member", rig.Block.Announcer!.Announcement);
        Assert.EndsWith(", unsaved", AutomationProperties.GetName(cell));

        cell.Activate();
        rig.Pump();
        Assert.False(cell.IsUnsaved);
        Assert.False(rig.Header("member").HasChanges);
        Assert.Equal(0, rig.Block.PendingChangeCount);
        Assert.False(rig.Block.IsBarOpen);
    }

    [AvaloniaFact]
    public void A_group_checkbox_switches_the_group_and_ripples_the_changed_cells_60ms_apart()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Full);
        var group = rig.GroupCell("lead", "payments");
        Assert.Equal(RolesPermissionsCheck.Mixed, group.State);
        group.Activate();
        rig.Pump();
        var toggled = Assert.Single(rig.GroupEvents);
        Assert.True(toggled.IsGranted);
        Assert.Equal(2, toggled.ChangedCount);
        Assert.Equal(2, rig.GrantEvents.Count);
        // See receipts was already on: Update card (2nd) waits 120ms, Switch subscription (3rd) 180ms.
        clock.Advance(TimeSpan.FromMilliseconds(150));
        rig.Pump();
        Assert.True(rig.Cell("lead", "pay.card").PaintProgress > 0.05);
        Assert.True(rig.Cell("lead", "pay.plan").PaintProgress < 0.001);
        clock.Advance(TimeSpan.FromMilliseconds(800));
        rig.Pump();
        Assert.True(rig.Cell("lead", "pay.plan").PaintProgress > 0.999);
        Assert.True(rig.Cell("lead", "pay.plan").DotProgress > 0.99);
        Assert.Equal(2, rig.Block.PendingChangeCount);

        group.Activate();
        rig.Pump();
        Assert.Equal(RolesPermissionsCheck.Off, group.State);
        Assert.Equal(1, rig.Block.PendingChangeCount);
    }

    [AvaloniaFact]
    public void Reduced_motion_applies_a_group_change_at_once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(ArcMotionLevel.Reduced);
        rig.GroupCell("guest", "documents").Activate();
        rig.Pump();
        var last = rig.Cell("guest", "doc.archive");
        Assert.Equal(RolesPermissionsCheck.On, last.State);
        Assert.Equal(1, last.PaintProgress, 3);
        Assert.Equal(1, last.CheckProgress, 3);
        Assert.Equal(1, last.DotProgress, 3);
        Assert.True(rig.Block.Bar!.IsVisible);
        Assert.Equal(1, rig.Block.Bar.Opacity, 3);
    }

    [AvaloniaFact]
    public void A_locked_cell_does_not_change_and_says_why()
    {
        var rig = new Rig();
        var cell = rig.Cell("owner", "doc.read");
        cell.Activate();
        rig.Pump();
        Assert.Equal(RolesPermissionsCheck.On, cell.State);
        Assert.Equal(0, rig.Block.PendingChangeCount);
        Assert.Equal("Founder always has full access", rig.Block.Announcer!.Announcement);
        Assert.EndsWith(", always on", AutomationProperties.GetName(cell));
        Assert.Contains(", permissions are fixed", AutomationProperties.GetName(rig.Header("owner")));
        Assert.False(rig.Block.SetGrant("owner", "doc.read", false));
    }

    [AvaloniaFact]
    public void Writing_Grants_from_the_app_updates_the_cells()
    {
        var rig = new Rig();
        var grants = SavedSample();
        grants.Add(new RolesPermissionsGrant("guest", "acc.saml"));
        rig.Block.Grants = grants;
        rig.Pump();
        Assert.Equal(RolesPermissionsCheck.On, rig.Cell("guest", "acc.saml").State);
        Assert.True(rig.Cell("guest", "acc.saml").IsUnsaved);
        Assert.Equal(1, rig.Block.PendingChangeCount);
    }

    #endregion

    #region Roles

    [AvaloniaFact]
    public void Duplicating_inserts_a_new_role_right_of_the_source_that_counts_once_plus_its_own_edits()
    {
        var rig = new Rig();
        var copy = rig.Block.DuplicateRole("guest")!;
        rig.Pump();
        Assert.Equal("Guest (copy)", copy.Name);
        Assert.True(copy.IsNew);
        Assert.Equal(["owner", "lead", "member", "guest", "guest-copy", "finance"], rig.Block.Roles.Select(r => r.Key));
        Assert.Equal(6, rig.Grid.Columns.Count);
        Assert.Equal(1, rig.Block.PendingChangeCount);
        Assert.Equal(RolesPermissionsCheck.On, rig.Cell("guest-copy", "doc.read").State);
        Assert.False(rig.Cell("guest-copy", "doc.read").IsUnsaved);
        Assert.True(rig.Header("guest-copy").HasChanges);
        Assert.Equal("New role", rig.Header("guest-copy").SecondLine);
        Assert.Equal("Guest (copy) · nobody assigned yet", rig.Block.BarText);
        Assert.Equal("Guest copied as Guest (copy), not saved yet", rig.Block.Announcer!.Announcement);
        Assert.Single(rig.DuplicateEvents);

        Assert.Equal("Guest (copy 2)", rig.Block.DuplicateRole("guest")!.Name);
        rig.Pump();
        Assert.Equal(["guest", "guest-copy-2", "guest-copy"], rig.Block.Roles.Skip(3).Take(3).Select(r => r.Key));

        rig.Cell("guest-copy", "acc.saml").Activate();
        rig.Pump();
        Assert.True(rig.Cell("guest-copy", "acc.saml").IsUnsaved);
        Assert.Equal(3, rig.Block.PendingChangeCount);

        Assert.True(rig.Block.DeleteRole("guest-copy"));
        rig.Pump();
        Assert.DoesNotContain(rig.Block.Roles, r => r.Key == "guest-copy");
        Assert.Equal(6, rig.Grid.Columns.Count);
        Assert.Equal(1, rig.Block.PendingChangeCount);
        Assert.False(rig.Block.DeleteRole("guest"));
        Assert.False(Assert.Single(rig.DeleteEvents).IsDiscard);
    }

    [AvaloniaFact]
    public void Reverting_a_role_puts_back_only_its_cells()
    {
        var rig = new Rig();
        rig.Block.SetGrant("member", "doc.archive", true);
        rig.Block.SetGrant("member", "team.roles", true);
        rig.Block.SetGrant("lead", "acc.saml", true);
        rig.Pump();
        Assert.Equal(3, rig.Block.PendingChangeCount);
        Assert.Equal(2, rig.Block.RevertRole("member"));
        rig.Pump();
        Assert.False(rig.Block.IsGranted("member", "doc.archive"));
        Assert.True(rig.Block.IsGranted("lead", "acc.saml"));
        Assert.Equal(1, rig.Block.PendingChangeCount);
        var e = Assert.Single(rig.RevertEvents);
        Assert.Equal("member", e.Role!.Key);
        Assert.Equal(2, e.Count);
    }

    [AvaloniaFact]
    public void Discard_throws_away_every_change_and_every_new_role_and_closes_the_bar()
    {
        var rig = new Rig();
        rig.Block.SetGrant("member", "doc.archive", true);
        rig.Block.DuplicateRole("lead");
        rig.Pump();
        Assert.Equal(2, rig.Block.PendingChangeCount);
        Assert.Equal(2, rig.Block.Discard());
        rig.Pump();
        Assert.Equal(0, rig.Block.PendingChangeCount);
        Assert.False(rig.Block.IsGranted("member", "doc.archive"));
        Assert.Equal(5, rig.Block.Roles.Count);
        Assert.False(rig.Block.IsBarOpen);
        Assert.True(Assert.Single(rig.DeleteEvents).IsDiscard);
        Assert.Null(Assert.Single(rig.RevertEvents).Role);
        Assert.Equal("2 changes undone", rig.Block.Announcer!.Announcement);
    }

    [AvaloniaFact]
    public void Role_menus_offer_copy_undo_and_remove_by_role_kind()
    {
        var rig = new Rig();
        rig.Block.ToggleRoleMenu(rig.Header("owner"), MenuOpenFocus.First);
        rig.Pump();
        var menu = rig.Block.Menu!;
        Assert.True(menu.IsOpen);
        Assert.True(rig.Header("owner").IsOpen);
        Assert.Equal(["Copy role"], menu.Items.OfType<MenuEntry>().Select(e => (string)e.Header!));
        menu.Close();
        rig.Pump();
        Assert.False(rig.Header("owner").IsOpen);

        rig.Block.ToggleRoleMenu(rig.Header("guest"), MenuOpenFocus.First);
        rig.Pump();
        var entries = menu.Items.OfType<MenuEntry>().ToList();
        Assert.Equal(["Copy role", "Undo edits"], entries.Select(e => (string)e.Header!));
        Assert.False(entries[1].IsEnabled);
        entries[0].PerformClick();
        rig.Pump();
        Assert.Equal("guest-copy", rig.Block.Roles[4].Key);
        TestHelpers.PumpUntil(() => ReferenceEquals(rig.Focused, rig.Header("guest-copy")), 2000);
        Assert.Same(rig.Header("guest-copy"), rig.Focused);

        rig.Block.ToggleRoleMenu(rig.Header("guest-copy"), MenuOpenFocus.First);
        rig.Pump();
        entries = menu.Items.OfType<MenuEntry>().ToList();
        Assert.Equal(["Copy role", "Undo edits", "Remove role"], entries.Select(e => (string)e.Header!));
        Assert.True(entries[2].IsDestructive);
        entries[2].PerformClick();
        rig.Pump();
        Assert.DoesNotContain(rig.Block.Roles, r => r.Key == "guest-copy");
    }

    #endregion

    #region Save

    [AvaloniaFact]
    public void Saving_blocks_edits_then_commits_clears_the_dots_reads_saved_and_closes()
    {
        var gate = new TaskCompletionSource();
        RolesPermissionsSaveRequest? seen = null;
        var rig = new Rig(configure: b => b.SaveHandler = async (request, _) =>
        {
            seen = request;
            await gate.Task;
        });
        rig.Block.SetGrant("member", "doc.archive", true);
        rig.Block.DuplicateRole("guest");
        rig.Pump();
        rig.ClickSave();
        Assert.Equal(RolesPermissionsSaveState.Saving, rig.Block.SaveState);
        Assert.True(rig.Block.SaveButton!.IsLoading);
        Assert.False(rig.Block.DiscardButton!.IsEnabled);
        Assert.Equal("Saving…", rig.Block.BarText);
        Assert.NotNull(seen);
        Assert.Equal(2, seen!.ChangeCount);
        Assert.Contains(new RolesPermissionsChange("member", "doc.archive", true), seen.Changes);
        Assert.Equal("guest-copy", Assert.Single(seen.AddedRoles).Key);

        rig.Cell("lead", "acc.saml").Activate();
        rig.Pump();
        Assert.False(rig.Block.IsGranted("lead", "acc.saml"));
        Assert.Equal("Saving, one moment", rig.Block.Announcer!.Announcement);
        Assert.Null(rig.Block.DuplicateRole("lead"));

        var states = new List<RolesPermissionsSaveState>();
        rig.Block.PropertyChanged += (_, e) =>
        {
            if (e.Property == RolesPermissions.SaveStateProperty)
            {
                states.Add(rig.Block.SaveState);
            }
        };
        gate.SetResult();
        TestHelpers.PumpUntil(() => states.Contains(RolesPermissionsSaveState.Idle), 3000);
        Assert.Equal([RolesPermissionsSaveState.Saved, RolesPermissionsSaveState.Idle], states);
        Assert.Equal(0, rig.Block.PendingChangeCount);
        Assert.False(rig.Cell("member", "doc.archive").IsUnsaved);
        Assert.False(rig.Block.Roles.First(r => r.Key == "guest-copy").IsNew);
        Assert.Contains(new RolesPermissionsGrant("member", "doc.archive"), rig.Block.SavedGrants!);
        Assert.Single(rig.SavedEvents);
        rig.Pump();
        Assert.False(rig.Block.IsBarOpen);
    }

    [AvaloniaFact]
    public void Saved_holds_its_text_and_button_then_further_edits_reopen_the_review()
    {
        var rig = new Rig(configure: b => b.SavedHoldDuration = TimeSpan.FromSeconds(30));
        rig.Block.SetGrant("member", "doc.archive", true);
        rig.Pump();
        rig.ClickSave();
        Assert.True(TestHelpers.PumpUntil(() => rig.Block.SaveState == RolesPermissionsSaveState.Saved, 3000));
        rig.Pump();
        Assert.True(rig.Block.IsBarOpen);
        Assert.Equal("All changes are live", rig.Block.BarText);
        Assert.False(rig.Block.SaveButton!.IsLoading);
        Assert.Equal("Saved", AutomationProperties.GetName(rig.Block.SaveButton));
        Assert.Equal("1 change saved", rig.Block.Announcer!.Announcement);

        rig.Block.SetGrant("member", "doc.archive", false);
        rig.Pump();
        Assert.Equal(RolesPermissionsSaveState.Idle, rig.Block.SaveState);
        Assert.Equal(1, rig.Block.PendingChangeCount);
        Assert.True(rig.Block.IsBarOpen);
        Assert.Equal("Apply changes", rig.Block.SaveButton.Content);
    }

    [AvaloniaFact]
    public void A_failed_save_keeps_the_changes_and_shows_the_error_until_the_next_edit()
    {
        var rig = new Rig(configure: b => b.SaveHandler = (_, _) => Task.FromException(new InvalidOperationException("Server unreachable")));
        rig.Block.SetGrant("member", "doc.archive", true);
        rig.Pump();
        rig.ClickSave();
        Assert.True(TestHelpers.PumpUntil(() => rig.Block.SaveState == RolesPermissionsSaveState.Failed, 3000));
        Assert.Equal("Server unreachable", rig.Block.ErrorText);
        Assert.Equal("Server unreachable", rig.Block.BarText);
        Assert.Equal(1, rig.Block.PendingChangeCount);
        Assert.True(rig.Cell("member", "doc.archive").IsUnsaved);
        Assert.Equal("Try again", rig.Block.SaveButton!.Content);
        Assert.Single(rig.FailedEvents);
        Assert.Equal("Couldn't save. Server unreachable", rig.Block.Alert!.Announcement);

        rig.Block.SetGrant("member", "team.roles", true);
        rig.Pump();
        Assert.Equal(RolesPermissionsSaveState.Idle, rig.Block.SaveState);
        Assert.Null(rig.Block.ErrorText);
        Assert.Equal(2, rig.Block.PendingChangeCount);
    }

    #endregion

    #region Keyboard and folding

    [AvaloniaFact]
    public void The_grid_has_one_tab_stop_and_arrows_keep_the_column_snap_and_skip_folded_rows()
    {
        var rig = new Rig();
        var stops = rig.Grid.GetVisualChildren().OfType<RolesPermissionsPart>().Count(p => p.IsTabStop);
        Assert.Equal(1, stops);
        Assert.True(rig.Header("owner").IsTabStop);

        rig.Focus(rig.Header("lead"));
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.GroupCell("lead", "documents"), rig.Focused);
        rig.Key(PhysicalKey.Home);
        Assert.Same(rig.Button("documents"), rig.Focused);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.Cell("owner", "doc.read"), rig.Focused);
        rig.Key(PhysicalKey.ArrowUp);
        Assert.Same(rig.GroupCell("owner", "documents"), rig.Focused);
        rig.Key(PhysicalKey.ArrowLeft);
        Assert.Same(rig.Button("documents"), rig.Focused);
        rig.Key(PhysicalKey.ArrowLeft);
        Assert.Same(rig.Button("documents"), rig.Focused);
        rig.Key(PhysicalKey.End);
        Assert.Same(rig.GroupCell("finance", "documents"), rig.Focused);
        Assert.True(rig.GroupCell("finance", "documents").IsTabStop);
        Assert.False(rig.Header("owner").IsTabStop);

        rig.Key(PhysicalKey.End, RawInputModifiers.Control);
        Assert.Same(rig.Cell("finance", "acc.tokens"), rig.Focused);
        rig.Key(PhysicalKey.Home, RawInputModifiers.Control);
        Assert.Same(rig.Header("owner"), rig.Focused);

        rig.Block.SetGroupExpanded(rig.Group("team"), false);
        rig.Pump();
        rig.Focus(rig.Cell("member", "doc.archive"));
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.GroupCell("member", "team"), rig.Focused);
        rig.Key(PhysicalKey.ArrowDown);
        Assert.Same(rig.GroupCell("member", "payments"), rig.Focused);

        rig.Key(PhysicalKey.Space);
        Assert.Equal(RolesPermissionsCheck.On, rig.GroupCell("member", "payments").State);
    }

    [AvaloniaFact]
    public void Folding_a_group_hides_its_rows_writes_IsExpanded_back_and_moves_focus_to_its_button()
    {
        var rig = new Rig();
        var height = rig.Grid.Bounds.Height;
        rig.Focus(rig.Cell("lead", "team.roles"));
        rig.Block.ToggleGroupExpanded(rig.Group("team"));
        rig.Pump();
        Assert.False(rig.Group("team").IsExpanded);
        Assert.False(rig.Button("team").IsExpanded);
        Assert.False(rig.Cell("lead", "team.roles").IsVisible);
        Assert.Same(rig.Button("team"), rig.Focused);
        Assert.Equal(height - 3 * 40, rig.Grid.Bounds.Height, 1);
        Assert.Equal(-90, rig.Button("team").ChevronAngle, 1);

        rig.Group("team").IsExpanded = true;
        rig.Pump();
        Assert.True(rig.Cell("lead", "team.roles").IsVisible);
        Assert.Equal(height, rig.Grid.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void The_band_follows_the_focused_column()
    {
        var rig = new Rig();
        Assert.Null(rig.Grid.BandColumn);
        rig.Focus(rig.Cell("guest", "doc.read"));
        Assert.Equal("guest", rig.Grid.BandColumn!.Role.Key);
        Assert.Equal(rig.Grid.Columns[3].X, rig.Grid.BandX, 1);
        rig.Focus(rig.Button("team"));
        Assert.Null(rig.Grid.BandColumn);
    }

    #endregion

    #region Layout

    [AvaloniaFact]
    public void Width_tiers_follow_the_content_width()
    {
        var wide = new Rig(width: 1100);
        Assert.Equal(100, wide.Grid.RoleWidth);
        Assert.Equal(1100 - 2 - 500, wide.Grid.LabelWidth, 1);
        Assert.True(wide.Grid.IsCountVisible);

        var narrow = new Rig(width: 640);
        Assert.True(narrow.Block.Classes.Contains(":narrow"));
        Assert.Equal(92, narrow.Grid.RoleWidth);
        Assert.Equal(200, narrow.Grid.LabelWidth, 1);

        var compact = new Rig(width: 420);
        Assert.True(compact.Block.Classes.Contains(":compact"));
        Assert.Equal(80, compact.Grid.RoleWidth);
        Assert.Equal(148, compact.Grid.LabelWidth, 1);
        Assert.False(compact.Grid.IsCountVisible);
        Assert.False(compact.Grid.IsChevronVisible);
        Assert.Equal(16, compact.Grid.LabelInset);
    }

    [AvaloniaFact]
    public void Flat_form_drops_the_frame_and_pads_the_page()
    {
        var rig = new Rig(configure: b => b.IsFramed = false);
        Assert.False(rig.Block.Classes.Contains(":framed"));
        Assert.Equal(new Thickness(32), rig.Block.Padding);
        Assert.Equal(new Thickness(0), rig.Block.BorderThickness);
    }

    [AvaloniaFact]
    public void Subtitle_counts_members_and_roles()
    {
        var rig = new Rig();
        var digits = rig.Block.GetVisualDescendants().OfType<Primitives.RollingDigits>().ToList();
        Assert.Contains(digits, d => d.Value == 31);
        Assert.Contains(digits, d => d.Value == 5);
        rig.Block.DuplicateRole("lead");
        rig.Pump();
        Assert.Contains(rig.Block.GetVisualDescendants().OfType<Primitives.RollingDigits>(), d => d.Value == 6);
    }

    #endregion
}

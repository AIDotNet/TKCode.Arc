namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="RolesPermissions"/> shows or announces, in English by default. Replace the instance (an object
/// initializer that sets only what changes) to translate or reword. Formats use <c>{0}</c>, <c>{1}</c> placeholders; a
/// malformed format shows as written instead of throwing.
/// </summary>
public sealed class RolesPermissionsLabels
{
    /// <summary>The caption over the permission names in the header row.</summary>
    public string PermissionHeader { get; init; } = "Permission";

    /// <summary>The header subtitle: {0} the workspace, {1} the member count, {2} the role count (both roll when they change).</summary>
    public string SubtitleFormat { get; init; } = "{0} · {1} people in {2} roles";

    /// <summary>The subtitle when no workspace name is set: {0} the member count, {1} the role count.</summary>
    public string SubtitleNoWorkspaceFormat { get; init; } = "{0} people in {1} roles";

    /// <summary>The screen reader name of the header's avatars.</summary>
    public string MembersName { get; init; } = "Workspace members";

    /// <summary>A role header's second line for one member.</summary>
    public string PersonFormat { get; init; } = "{0} person";

    /// <summary>A role header's second line for any other count.</summary>
    public string PeopleFormat { get; init; } = "{0} people";

    /// <summary>A duplicated role's second line until it is saved.</summary>
    public string NewRole { get; init; } = "New role";

    /// <summary>The menu header's second line for a duplicated role.</summary>
    public string NotSavedYet { get; init; } = "Not saved yet";

    /// <summary>The menu row that duplicates the role.</summary>
    public string CopyRole { get; init; } = "Copy role";

    /// <summary>The menu row that reverts the role's unsaved changes.</summary>
    public string UndoEdits { get; init; } = "Undo edits";

    /// <summary>The menu row that removes a duplicated role (only new roles have it).</summary>
    public string RemoveRole { get; init; } = "Remove role";

    /// <summary>A copy's name: {0} the source's name.</summary>
    public string CopyNameFormat { get; init; } = "{0} (copy)";

    /// <summary>A copy's name when <see cref="CopyNameFormat"/> is taken: {0} the source's name, {1} 2, 3, ...</summary>
    public string CopyNameNumberedFormat { get; init; } = "{0} (copy {1})";

    /// <summary>The footer's legend next to the lock.</summary>
    public string LockedLegend { get; init; } = "Locked roles always have full access";

    /// <summary>The footer's legend next to the dot.</summary>
    public string UnsavedLegend { get; init; } = "Not saved yet";

    /// <summary>The word between the count and "change(s)" in the review bar; hidden below 540px.</summary>
    public string PendingWord { get; init; } = "pending";

    /// <summary>The review bar's noun after a count of one.</summary>
    public string ChangeOne { get; init; } = "change";

    /// <summary>The review bar's noun after any other count.</summary>
    public string ChangeMany { get; init; } = "changes";

    /// <summary>Two changed roles: {0} and {1} their names.</summary>
    public string TwoRolesFormat { get; init; } = "{0} and {1}";

    /// <summary>Three changed roles.</summary>
    public string ThreeRolesFormat { get; init; } = "{0}, {1} and {2}";

    /// <summary>Four or more changed roles: {0} and {1} the first two names, {2} how many more.</summary>
    public string ManyRolesFormat { get; init; } = "{0}, {1} and {2} more";

    /// <summary>The review bar's second line: {0} the roles, {1} how many people hold them.</summary>
    public string ReachFormat { get; init; } = "{0} · reaches {1} people";

    /// <summary>The second line when the roles reach one person.</summary>
    public string ReachOneFormat { get; init; } = "{0} · reaches 1 person";

    /// <summary>The second line when only roles without members changed.</summary>
    public string NoMembersFormat { get; init; } = "{0} · nobody assigned yet";

    /// <summary>The second line while saving.</summary>
    public string Saving { get; init; } = "Saving…";

    /// <summary>The first line after a save.</summary>
    public string SavedTitle { get; init; } = "Access updated";

    /// <summary>The second line after a save.</summary>
    public string SavedText { get; init; } = "All changes are live";

    /// <summary>The save button's text after a save.</summary>
    public string SavedButton { get; init; } = "Saved";

    /// <summary>The first line after a failed save.</summary>
    public string FailedTitle { get; init; } = "Couldn't save";

    /// <summary>The second line after a failed save when the exception has no message.</summary>
    public string FailedText { get; init; } = "Something went wrong. Your changes are still here.";

    /// <summary>The save button's text after a failed save.</summary>
    public string RetryButton { get; init; } = "Try again";

    /// <summary>The button that throws every unsaved change away.</summary>
    public string Discard { get; init; } = "Undo all";

    /// <summary>The button that saves.</summary>
    public string Save { get; init; } = "Apply changes";

    /// <summary>The screen reader name of the review bar.</summary>
    public string ReviewBarName { get; init; } = "Unsaved changes";

    /// <summary>A group checkbox's name: {0} the role, {1} the group.</summary>
    public string GroupCellFormat { get; init; } = "{0}: all {1} permissions";

    /// <summary>A permission switch's name: {0} the role, {1} the permission.</summary>
    public string CellFormat { get; init; } = "{0}: {1}";

    /// <summary>Added to a locked cell's name.</summary>
    public string AlwaysOnSuffix { get; init; } = ", always on";

    /// <summary>Added to an unsaved cell's name.</summary>
    public string UnsavedSuffix { get; init; } = ", unsaved";

    /// <summary>Added to a locked role header's name.</summary>
    public string FixedSuffix { get; init; } = ", permissions are fixed";

    /// <summary>Added to the header of a role with unsaved changes.</summary>
    public string ChangedSuffix { get; init; } = ", unsaved changes";

    /// <summary>A group button's name: {0} the group, {1} how many permissions it has.</summary>
    public string GroupButtonFormat { get; init; } = "{0}, {1} permissions";

    /// <summary>Announced when a cell turns on: {0} the role, {1} the permission.</summary>
    public string GrantedFormat { get; init; } = "{1} turned on for {0}";

    /// <summary>Announced when a cell turns off.</summary>
    public string RevokedFormat { get; init; } = "{1} turned off for {0}";

    /// <summary>Announced when a group checkbox turns a group on: {0} the role, {1} the group.</summary>
    public string GroupGrantedFormat { get; init; } = "Every {1} permission turned on for {0}";

    /// <summary>Announced when a group checkbox turns a group off.</summary>
    public string GroupRevokedFormat { get; init; } = "Every {1} permission turned off for {0}";

    /// <summary>Announced when a locked cell is pressed: {0} the role.</summary>
    public string LockedFormat { get; init; } = "{0} always has full access";

    /// <summary>Announced after a duplicate: {0} the source, {1} the copy.</summary>
    public string DuplicatedFormat { get; init; } = "{0} copied as {1}, not saved yet";

    /// <summary>Announced after a new role is removed: {0} its name.</summary>
    public string RemovedFormat { get; init; } = "{0} removed";

    /// <summary>Announced after a role's edits are undone: {0} its name.</summary>
    public string RevertedFormat { get; init; } = "Edits to {0} undone";

    /// <summary>Announced after Discard: {0} the count with its noun ("3 changes").</summary>
    public string DiscardedFormat { get; init; } = "{0} undone";

    /// <summary>Announced when a save starts.</summary>
    public string SavingFormat { get; init; } = "Saving {0}";

    /// <summary>Announced when a save succeeds.</summary>
    public string SavedFormat { get; init; } = "{0} saved";

    /// <summary>Announced when a save fails: {0} the error line.</summary>
    public string FailedFormat { get; init; } = "Couldn't save. {0}";

    /// <summary>Announced when a change is attempted while a save runs.</summary>
    public string BusyMessage { get; init; } = "Saving, one moment";

    /// <summary>"3 changes" / "1 change" for the announcements.</summary>
    internal string CountText(int count) => count + " " + (count == 1 ? ChangeOne : ChangeMany);

    /// <summary>"1 person" / "4 people".</summary>
    internal string People(int count) => ProjectsText.Format(count == 1 ? PersonFormat : PeopleFormat, count);
}

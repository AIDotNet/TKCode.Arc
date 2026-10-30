using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TeamSwitcher"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; texts with values in them are format functions.
/// </summary>
public sealed class TeamSwitcherLabels
{
    /// <summary>The English defaults.</summary>
    public static TeamSwitcherLabels Default { get; } = new();

    /// <summary>The trigger's name, from the workspace and its plan: "Acme, Enterprise plan".</summary>
    public Func<string, string?, string> Trigger { get; init; } =
        (name, plan) => string.IsNullOrEmpty(plan) ? name : name + ", " + plan + " plan";

    /// <summary>The line under a name: the plan and the member count, "Enterprise · 214 members".</summary>
    public Func<string?, int?, string> PlanLine { get; init; } = (plan, members) =>
    {
        var count = members switch
        {
            null => null,
            1 => "1 member",
            { } n => n.ToString("N0", CultureInfo.InvariantCulture) + " members"
        };
        return (plan, count) switch
        {
            ({ Length: > 0 }, { }) => plan + " · " + count,
            ({ Length: > 0 }, null) => plan!,
            (_, { }) => count,
            _ => string.Empty
        };
    };

    /// <summary>The trigger's name line, and the menu's list, when there is no workspace (no workspaces and no simulation).</summary>
    public string NoWorkspace { get; init; } = "No workspace";

    /// <summary>The shimmering line on the trigger while a workspace loads.</summary>
    public string Switching { get; init; } = "Switching…";

    /// <summary>The search field's placeholder and name.</summary>
    public string Search { get; init; } = "Find a workspace…";

    /// <summary>The heading over the list.</summary>
    public string Workspaces { get; init; } = "Workspaces";

    /// <summary>The list's name for screen readers.</summary>
    public string ListName { get; init; } = "Your workspaces";

    /// <summary>What the list says when nothing matches the search.</summary>
    public Func<string, string> NoMatches { get; init; } = query => "No workspace matches “" + query + "”";

    /// <summary>The hint under <see cref="NoMatches"/>: Enter opens Create workspace with the name filled in.</summary>
    public string NoMatchesHint { get; init; } = "Press Enter to create it";

    /// <summary>The keyboard shortcut of the nth workspace (1 to 9). The flag is true on macOS (Command), false elsewhere (Ctrl).</summary>
    public Func<int, bool, string> Shortcut { get; init; } = (n, mac) => (mac ? "⌘" : "Ctrl ") + n.ToString(CultureInfo.InvariantCulture);

    /// <summary>The shortcut as screen readers read it: "Command 1", "Control 1".</summary>
    public Func<int, bool, string> ShortcutName { get; init; } =
        (n, mac) => (mac ? "Command " : "Control ") + n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Added to the name of the active workspace's row.</summary>
    public string Current { get; init; } = "current";

    /// <summary>The action row and the title of the create view.</summary>
    public string CreateWorkspace { get; init; } = "Create workspace";

    /// <summary>The action row and the title of the join view.</summary>
    public string JoinWorkspace { get; init; } = "Join with invite";

    /// <summary>The back button of the create and join views.</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The create view's field label.</summary>
    public string NameLabel { get; init; } = "Workspace name";

    /// <summary>The create view's field placeholder.</summary>
    public string NamePlaceholder { get; init; } = "Acme Inc.";

    /// <summary>The create view's note under the field.</summary>
    public string NameDescription { get; init; } = "You will be its owner. You can invite people next.";

    /// <summary>The create view's submit button.</summary>
    public string Create { get; init; } = "Create";

    /// <summary>The join view's field label.</summary>
    public string InviteLabel { get; init; } = "Invite link or code";

    /// <summary>The join view's field placeholder.</summary>
    public string InvitePlaceholder { get; init; } = "Paste a link or a code";

    /// <summary>The join view's note under the field (a simulation shows its invite codes here instead).</summary>
    public string InviteDescription { get; init; } = "Ask a workspace admin for an invite.";

    /// <summary>The join view's note while a simulation with invites runs: the codes it accepts.</summary>
    public Func<string, string> SampleInvites { get; init; } = codes => "Sample invites: " + codes;

    /// <summary>The join view's submit button.</summary>
    public string Join { get; init; } = "Join";

    /// <summary>The cancel button of the create and join views.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The error when the name is empty.</summary>
    public string NameRequired { get; init; } = "Enter a name for the workspace.";

    /// <summary>The error when the invite is empty.</summary>
    public string InviteRequired { get; init; } = "Paste an invite link or code.";

    /// <summary>The simulated create's error for a name already in the list.</summary>
    public Func<string, string> NameTaken { get; init; } = name => "You already have a workspace called “" + name + "”.";

    /// <summary>The simulated join's error for an invite it does not know.</summary>
    public string InviteInvalid { get; init; } = "This invite is invalid or has expired.";

    /// <summary>The simulated join's error for an invite to a workspace you are already in.</summary>
    public Func<string, string> AlreadyMember { get; init; } = name => "You are already a member of " + name + ".";

    /// <summary>The fallback error when a delegate throws an exception without a message.</summary>
    public string GenericError { get; init; } = "Something went wrong. Try again.";

    /// <summary>Announced when a switch starts.</summary>
    public Func<string, string> SwitchingTo { get; init; } = name => "Switching to " + name;

    /// <summary>Announced when a switch has finished.</summary>
    public Func<string, string> SwitchedTo { get; init; } = name => "Switched to " + name;

    /// <summary>Announced when a switch failed (with the error's message).</summary>
    public Func<string, string, string> SwitchFailed { get; init; } = (name, error) => "Could not switch to " + name + ". " + error;

    /// <summary>Announced when a workspace was created.</summary>
    public Func<string, string> Created { get; init; } = name => "Created " + name;

    /// <summary>Announced when a workspace was joined.</summary>
    public Func<string, string> Joined { get; init; } = name => "Joined " + name;

    /// <summary>Added to an announcement when a simulated switch, create or join did the work.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent";

    /// <summary>Formats an announcement with the simulated note.</summary>
    internal string WithSimulated(string text, bool simulated) => simulated ? text + ". " + Simulated + "." : text;
}

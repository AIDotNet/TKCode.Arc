using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What <see cref="InvitePeople.SendAction"/> receives: the invites that were just added to the pending list.</summary>
public sealed class InvitePeopleSendRequest
{
    internal InvitePeopleSendRequest(IReadOnlyList<InvitePeoplePendingInvite> invites, int attempt)
    {
        Invites = invites;
        Attempt = attempt;
    }

    /// <summary>
    /// The new rows, in the order of the chips, each with its person and role and <see cref="InvitePeopleInviteStatus.Sending"/>.
    /// Call <see cref="InvitePeople.MarkDelivered"/> for each as it is delivered; the ones still sending when the task
    /// completes are marked sent then.
    /// </summary>
    public IReadOnlyList<InvitePeoplePendingInvite> Invites { get; }

    /// <summary>1 for the first send of the block, 2 for the next one, and so on (a reset starts again at 1).</summary>
    public int Attempt { get; }
}

/// <summary>The chips in the field changed (added, removed, role changed).</summary>
public sealed class InvitePeopleEntriesChangedEventArgs : RoutedEventArgs
{
    internal InvitePeopleEntriesChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<InvitePeopleEntry> entries)
        : base(routedEvent)
    {
        Entries = entries;
    }

    /// <summary>Every chip now in the field, in order.</summary>
    public IReadOnlyList<InvitePeopleEntry> Entries { get; }

    /// <summary>The chips that can be invited.</summary>
    public int ValidCount => Entries.Count(e => e.IsValid);
}

/// <summary>Invites were sent (<see cref="InvitePeople.InvitesSent"/>).</summary>
public sealed class InvitePeopleInvitesSentEventArgs : RoutedEventArgs
{
    internal InvitePeopleInvitesSentEventArgs(RoutedEvent routedEvent, InvitePeopleSendRequest request) : base(routedEvent)
    {
        Request = request;
    }

    /// <summary>The request that went through.</summary>
    public InvitePeopleSendRequest Request { get; }
}

/// <summary>Sending failed (<see cref="InvitePeople.SendFailed"/>): the rows were taken back and the chips are in the field again.</summary>
public sealed class InvitePeopleSendFailedEventArgs : RoutedEventArgs
{
    internal InvitePeopleSendFailedEventArgs(RoutedEvent routedEvent, InvitePeopleSendRequest request, Exception exception)
        : base(routedEvent)
    {
        Request = request;
        Exception = exception;
    }

    /// <summary>The request that failed.</summary>
    public InvitePeopleSendRequest Request { get; }

    /// <summary>What <see cref="InvitePeople.SendAction"/> threw.</summary>
    public Exception Exception { get; }
}

/// <summary>Something happened to one pending invite (joined, resent, revoked).</summary>
public sealed class InvitePeopleInviteEventArgs : RoutedEventArgs
{
    internal InvitePeopleInviteEventArgs(RoutedEvent routedEvent, InvitePeoplePendingInvite invite) : base(routedEvent)
    {
        Invite = invite;
    }

    /// <summary>The invite.</summary>
    public InvitePeoplePendingInvite Invite { get; }
}

/// <summary>A chip's role changed (<see cref="InvitePeople.RoleChanged"/>).</summary>
public sealed class InvitePeopleRoleChangedEventArgs : RoutedEventArgs
{
    internal InvitePeopleRoleChangedEventArgs(RoutedEvent routedEvent, InvitePeopleEntry entry, InvitePeopleRole? oldRole,
                                              InvitePeopleRole? newRole)
        : base(routedEvent)
    {
        Entry = entry;
        OldRole = oldRole;
        NewRole = newRole;
    }

    /// <summary>The chip.</summary>
    public InvitePeopleEntry Entry { get; }

    /// <summary>The role before.</summary>
    public InvitePeopleRole? OldRole { get; }

    /// <summary>The role now.</summary>
    public InvitePeopleRole? NewRole { get; }
}

/// <summary>The plan changed after an upgrade (<see cref="InvitePeople.PlanUpgraded"/>).</summary>
public sealed class InvitePeoplePlanUpgradedEventArgs : RoutedEventArgs
{
    internal InvitePeoplePlanUpgradedEventArgs(RoutedEvent routedEvent, InvitePeoplePlan? oldPlan, InvitePeoplePlan newPlan)
        : base(routedEvent)
    {
        OldPlan = oldPlan;
        NewPlan = newPlan;
    }

    /// <summary>The plan before.</summary>
    public InvitePeoplePlan? OldPlan { get; }

    /// <summary>The plan now.</summary>
    public InvitePeoplePlan NewPlan { get; }
}

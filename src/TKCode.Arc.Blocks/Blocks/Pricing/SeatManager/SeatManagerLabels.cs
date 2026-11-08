using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="SeatManager"/>. Replace the whole object to localize the block. Entries whose text
/// holds a rolling number are templates: <c>{0}</c> (and <c>{1}</c>) mark where the number rolls, and a function picks the
/// singular or plural template from the count. Sentences without rolling numbers are functions that receive the counts
/// and the already formatted amounts and dates. Amounts and dates use <see cref="Culture"/>.
/// </summary>
public sealed class SeatManagerLabels
{
    private static string Seats(int n) => n == 1 ? "1 seat" : $"{n} seats";

    /// <summary>Culture for amounts, dates and counts (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The card's title: "Seats".</summary>
    public string Title { get; init; } = "Seats";

    /// <summary>The line under the title: (workspace, price per seat, interval, renewal date) ⇒ "Tidewater · $18 a seat/mo · Renews Nov 18".</summary>
    public Func<string, string, SeatManagerInterval, string, string> Subtitle { get; init; } =
        (workspace, price, interval, date) =>
        {
            var per = interval == SeatManagerInterval.Year ? "yr" : "mo";
            var head = string.IsNullOrWhiteSpace(workspace) ? string.Empty : $"{workspace} · ";
            return $"{head}{price} a seat/{per} · Renews {date}";
        };

    /// <summary>Template of the usage line, with the seats in use ({0}) and paid for ({1}) rolling: "{0} of {1} seats in use".</summary>
    public string UsedTemplate { get; init; } = "{0} of {1} seats in use";

    /// <summary>Accessible name of the avatar row: count ⇒ "10 members".</summary>
    public Func<int, string> MembersGroup { get; init; } = n => n == 1 ? "1 member" : $"{n} members";

    /// <summary>Legend template for members: "{0} members".</summary>
    public Func<int, string> LegendMembers { get; init; } = n => n == 1 ? "{0} member" : "{0} members";

    /// <summary>Legend template for invited seats: "{0} invited".</summary>
    public Func<int, string> LegendInvited { get; init; } = _ => "{0} invited";

    /// <summary>Legend template for open seats: "{0} open".</summary>
    public Func<int, string> LegendOpen { get; init; } = _ => "{0} open";

    /// <summary>Legend template for seats being added: "{0} new".</summary>
    public Func<int, string> LegendNew { get; init; } = _ => "{0} new";

    /// <summary>Legend template for seats being removed: "{0} removed".</summary>
    public Func<int, string> LegendRemoved { get; init; } = _ => "{0} removed";

    /// <summary>
    /// The seat meter's accessible name: (used, purchased, members, invited, open, adding, removing) ⇒ "12 of 15 seats in
    /// use: 10 members, 2 invited, 3 open".
    /// </summary>
    public Func<int, int, int, int, int, int, int, string> MeterName { get; init; } =
        (used, purchased, members, invited, open, adding, removing) =>
        {
            var text = $"{used} of {purchased} seats in use: {members} members, {invited} invited, {open} open";
            if (adding > 0)
            {
                text += $", {adding} being added";
            }
            else if (removing > 0)
            {
                text += $", {removing} being removed";
            }
            return text;
        };

    /// <summary>Notice while seats are added: count ⇒ "3 new seats open once you update."</summary>
    public Func<int, string> NoticeAdding { get; init; } = n => n == 1
        ? "1 new seat opens once you update."
        : $"{n} new seats open once you update.";

    /// <summary>Notice while open seats are removed: (count, date) ⇒ "3 open seats leave the plan on Nov 18."</summary>
    public Func<int, string, string> NoticeRemoving { get; init; } = (n, date) => n == 1
        ? $"1 open seat leaves the plan on {date}."
        : $"{n} open seats leave the plan on {date}.";

    /// <summary>Notice while some seats are free: count ⇒ "3 seats free for new people."</summary>
    public Func<int, string> NoticeOpen { get; init; } = n => n == 1
        ? "1 seat free for someone new."
        : $"{n} seats free for new people.";

    /// <summary>Notice when every seat is taken: "Every seat is taken. Invites need a free seat."</summary>
    public string NoticeFull { get; init; } = "Every seat is taken. Invites need a free seat.";

    /// <summary>The action beside the full-team notice: "Add a seat".</summary>
    public string AddSeat { get; init; } = "Add a seat";

    /// <summary>Notice after pressing past the plan's cap: (plan name, cap) ⇒ "Team stops at 25 seats. Talk to sales for more."</summary>
    public Func<string, int, string> NoticeAtMax { get; init; } = (plan, max) =>
        $"{(string.IsNullOrWhiteSpace(plan) ? "This plan" : plan)} stops at {max} seats. Talk to sales for more.";

    /// <summary>Notice after pressing below the seats in use: count ⇒ "12 seats are in use. Revoke an invite to go lower."</summary>
    public Func<int, string> NoticeAtMin { get; init; } = n => $"{Seats(n)} {(n == 1 ? "is" : "are")} in use. Revoke an invite to go lower.";

    /// <summary>Label of the stepper row: "Seats".</summary>
    public string StepperTitle { get; init; } = "Seats";

    /// <summary>Template under the stepper label, the seats paid for rolling: "{0} paid for".</summary>
    public string PurchasedTemplate { get; init; } = "{0} paid for";

    /// <summary>Accessible name of the minus button: "Remove a seat".</summary>
    public string RemoveSeat { get; init; } = "Remove a seat";

    /// <summary>Accessible name of the plus button: "Add a seat".</summary>
    public string AddSeatButton { get; init; } = "Add a seat";

    /// <summary>Accessible name of the seat count: "Seats".</summary>
    public string SpinName { get; init; } = "Seats";

    /// <summary>The seat count as read aloud: count ⇒ "15 seats".</summary>
    public Func<int, string> SpinValueText { get; init; } = Seats;

    /// <summary>Label of the first quote row: "Due today".</summary>
    public string DueToday { get; init; } = "Due today";

    /// <summary>Note beside "Due today" when nothing changes: "No change".</summary>
    public string NoteNoChange { get; init; } = "No change";

    /// <summary>Note beside "Due today" when seats are added: days left ⇒ "Prorated, 22 days left".</summary>
    public Func<int, string> NoteProrated { get; init; } = days => days == 1 ? "Prorated, 1 day left" : $"Prorated, {days} days left";

    /// <summary>Note beside "Due today" when seats are removed: date ⇒ "Takes effect Nov 18".</summary>
    public Func<string, string> NoteRemovals { get; init; } = date => $"Takes effect {date}";

    /// <summary>Label of the second quote row: date ⇒ "From Nov 18".</summary>
    public Func<string, string> From { get; init; } = date => $"From {date}";

    /// <summary>Note beside "From …" when nothing changes: "Current total".</summary>
    public string NoteCurrent { get; init; } = "Current total";

    /// <summary>Note beside "From …" when the total changes: old total ⇒ "Was $216.00".</summary>
    public Func<string, string> NoteWas { get; init; } = total => $"Was {total}";

    /// <summary>Suffix of the recurring total: interval ⇒ "/mo" or "/yr".</summary>
    public Func<SeatManagerInterval, string> PerInterval { get; init; } = i => i == SeatManagerInterval.Year ? "/yr" : "/mo";

    /// <summary>Title of the invites section: "Pending invites".</summary>
    public string InvitesTitle { get; init; } = "Pending invites";

    /// <summary>The invites section's empty state: "No pending invites".</summary>
    public string InvitesEmpty { get; init; } = "No pending invites";

    /// <summary>An invite's second line: (invited by or null, note or null) ⇒ "Invited by Mira Okafor, 3 days ago".</summary>
    public Func<string?, string?, string?> InviteNote { get; init; } = (by, note) =>
    {
        if (string.IsNullOrWhiteSpace(by))
        {
            return string.IsNullOrWhiteSpace(note) ? null : note;
        }
        return string.IsNullOrWhiteSpace(note) ? $"Invited by {by}" : $"Invited by {by}, {note}";
    };

    /// <summary>The resend button: "Resend".</summary>
    public string Resend { get; init; } = "Resend";

    /// <summary>The resend button once the invite went out again: "Sent".</summary>
    public string Sent { get; init; } = "Sent";

    /// <summary>The revoke button: "Revoke".</summary>
    public string Revoke { get; init; } = "Revoke";

    /// <summary>The undo button of a revoked invite: "Undo".</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>A revoked invite's second line: "Revoked, a seat is free".</summary>
    public string Revoked { get; init; } = "Revoked, a seat is free";

    /// <summary>Accessible name of the resend button: email ⇒ "Resend invite to theo@…".</summary>
    public Func<string, string> ResendName { get; init; } = email => $"Resend invite to {email}";

    /// <summary>Accessible name of the resend button once sent: email ⇒ "Invite sent to theo@…".</summary>
    public Func<string, string> SentName { get; init; } = email => $"Invite sent to {email}";

    /// <summary>Accessible name of the revoke button: email ⇒ "Revoke invite to theo@…".</summary>
    public Func<string, string> RevokeName { get; init; } = email => $"Revoke invite to {email}";

    /// <summary>Accessible name of the undo button: email ⇒ "Undo revoke for theo@…".</summary>
    public Func<string, string> UndoName { get; init; } = email => $"Undo revoke for {email}";

    /// <summary>The card line in the footer: (brand, last four) ⇒ "Nova ending 1881".</summary>
    public Func<string, string, string> Card { get; init; } = (brand, last4) => $"{brand} ending {last4}";

    /// <summary>What screen readers hear before the card line: processor ⇒ "Billed via Ledgerline,".</summary>
    public Func<string, string> BilledVia { get; init; } = processor => $"Billed via {processor},";

    /// <summary>The bold part of the footer while seats are added or removed: delta ⇒ "+3 seats", "−3 seats".</summary>
    public Func<int, string> DeltaSeats { get; init; } = delta =>
        (delta > 0 ? "+" : "−") + Seats(Math.Abs(delta));

    /// <summary>The rest of the footer while seats are added: amount due ⇒ " · $38.32 due now".</summary>
    public Func<string, string> DeltaAddTail { get; init; } = amount => $" · {amount} due now";

    /// <summary>The rest of the footer while seats are removed: date ⇒ " from Nov 18 · nothing due now".</summary>
    public Func<string, string> DeltaRemoveTail { get; init; } = date => $" from {date} · nothing due now";

    /// <summary>The primary button at rest: "Update seats".</summary>
    public string UpdateSeats { get; init; } = "Update seats";

    /// <summary>The button that leaves the confirm step: "Back".</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The pay button of the confirm step: amount ⇒ "Pay $38.32".</summary>
    public Func<string, string> Pay { get; init; } = amount => $"Pay {amount}";

    /// <summary>The confirm button of the confirm step when seats are only removed: "Confirm".</summary>
    public string Confirm { get; init; } = "Confirm";

    /// <summary>The retry button after a failed update: "Try again".</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The confirm question when seats are added: (amount, card or null) ⇒ "Pay $38.32 now with Nova ending 1881?"</summary>
    public Func<string, string?, string> ConfirmAdd { get; init; } = (amount, card) =>
        card is null ? $"Pay {amount} now?" : $"Pay {amount} now with {card}?";

    /// <summary>The confirm question when seats are removed: (count, date) ⇒ "Drop 3 seats on Nov 18?"</summary>
    public Func<int, string, string> ConfirmRemove { get; init; } = (n, date) => $"Drop {Seats(n)} on {date}?";

    /// <summary>The shimmering footer while paying: card or null ⇒ "Paying with Nova ending 1881".</summary>
    public Func<string?, string> Charging { get; init; } = card => card is null ? "Paying" : $"Paying with {card}";

    /// <summary>The shimmering footer while seats are only removed: "Saving seat changes".</summary>
    public string Updating { get; init; } = "Saving seat changes";

    /// <summary>The footer after a change: (seats, receipt email or null) ⇒ "You're on 15 seats. Receipt sent to accounts@…".</summary>
    public Func<int, string?, string> Done { get; init; } = (n, email) =>
        string.IsNullOrWhiteSpace(email) ? $"You're on {Seats(n)}." : $"You're on {Seats(n)}. Receipt sent to {email}.";

    /// <summary>The footer when the update failed without a message: "The seats were not updated.".</summary>
    public string ErrorFallback { get; init; } = "The seats were not updated.";

    /// <summary>Announced when the confirm step opens for added seats: (count, amount) ⇒ "Review: 3 seats added, $38.32 due now."</summary>
    public Func<int, string, string> AnnounceReviewAdd { get; init; } = (n, amount) => $"Review: {Seats(n)} added, {amount} due now.";

    /// <summary>Announced when the confirm step opens for removed seats: (count, date) ⇒ "Review: 3 seats removed on Nov 18."</summary>
    public Func<int, string, string> AnnounceReviewRemove { get; init; } = (n, date) => $"Review: {Seats(n)} removed on {date}.";

    /// <summary>Announced after an update: (seats, amount charged or null) ⇒ "Now on 15 seats. $38.32 charged."</summary>
    public Func<int, string?, string> AnnounceUpdated { get; init; } = (n, amount) =>
        amount is null ? $"Now on {Seats(n)}." : $"Now on {Seats(n)}. {amount} charged.";

    /// <summary>Announced when an invite went out again: email ⇒ "Invite sent again to theo@…."</summary>
    public Func<string, string> AnnounceResent { get; init; } = email => $"Invite sent again to {email}.";

    /// <summary>Announced when resending failed: email ⇒ "Could not resend the invite to theo@…."</summary>
    public Func<string, string> AnnounceResendFailed { get; init; } = email => $"Could not resend the invite to {email}.";

    /// <summary>Announced on revoke: (email, undo seconds) ⇒ "Invite to theo@… revoked, a seat is free. Undo for 3 seconds."</summary>
    public Func<string, int, string> AnnounceRevoked { get; init; } = (email, seconds) =>
        $"Invite to {email} revoked, a seat is free. Undo for {seconds} seconds.";

    /// <summary>Announced when the undo window closed: email ⇒ "Invite to theo@… removed."</summary>
    public Func<string, string> AnnounceRemoved { get; init; } = email => $"Invite to {email} removed.";

    /// <summary>Announced on undo, or when revoking failed and the invite came back: email ⇒ "Invite to theo@… restored."</summary>
    public Func<string, string> AnnounceRestored { get; init; } = email => $"Invite to {email} restored.";

    /// <summary>Added to announcements when no handler charged anything: " Simulated, nothing was charged."</summary>
    public string SimulatedCharge { get; init; } = " Simulated, nothing was charged.";

    /// <summary>Added to announcements when no handler sent anything: " Simulated, nothing was sent."</summary>
    public string SimulatedSend { get; init; } = " Simulated, nothing was sent.";
}

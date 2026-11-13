using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="WalletCard"/>. Replace the whole object to localize the block; entries with numbers are
/// functions that receive already formatted values.
/// </summary>
public sealed class WalletCardLabels
{
    /// <summary>Culture for amounts and dates (en-US by default, so every machine renders the same text).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Over the balance: "Available balance".</summary>
    public string Balance { get; init; } = "Available balance";

    /// <summary>The limit line: (spent, limit) ⇒ "$1,240.18 of $3,000.00 monthly limit".</summary>
    public Func<string, string, string> LimitLine { get; init; } = (spent, limit) => $"{spent} of {limit} monthly limit";

    /// <summary>Freeze button: "Freeze".</summary>
    public string Freeze { get; init; } = "Freeze";

    /// <summary>Freeze button while the card is frozen: "Unfreeze".</summary>
    public string Unfreeze { get; init; } = "Unfreeze";

    /// <summary>Details button: "Show details".</summary>
    public string ShowDetails { get; init; } = "Show details";

    /// <summary>Details button while the details are shown: "Hide details".</summary>
    public string HideDetails { get; init; } = "Hide details";

    /// <summary>Copy button under the face while the details are shown: "Copy number".</summary>
    public string CopyNumber { get; init; } = "Copy number";

    /// <summary>Label of the expiry date on the face: "Valid thru".</summary>
    public string ValidThru { get; init; } = "Valid thru";

    /// <summary>Label of the security code on the face: "CVC".</summary>
    public string SecurityCode { get; init; } = "CVC";

    /// <summary>The pill on a frozen face: "Frozen".</summary>
    public string Frozen { get; init; } = "Frozen";

    /// <summary>The note under the balance while the card is frozen.</summary>
    public string FrozenNote { get; init; } = "New purchases are declined until you unfreeze the card. Refunds still arrive.";

    /// <summary>Heading of the activity list: "Recent activity".</summary>
    public string RecentActivity { get; init; } = "Recent activity";

    /// <summary>Shown when there are no transactions: "No transactions yet".</summary>
    public string NoActivity { get; init; } = "No transactions yet";

    /// <summary>Tag of a pending transaction: "Pending".</summary>
    public string Pending { get; init; } = "Pending";

    /// <summary>Tag of a declined transaction: "Declined".</summary>
    public string Declined { get; init; } = "Declined";

    /// <summary>A transaction's date: (date, today) ⇒ "Today", "Yesterday" or "Nov 6".</summary>
    public Func<DateTimeOffset, DateTime, string> Date { get; init; } = (at, today) =>
    {
        var day = at.LocalDateTime.Date;
        if (day == today)
        {
            return "Today";
        }
        return day == today.AddDays(-1) ? "Yesterday" : at.LocalDateTime.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"));
    };

    /// <summary>Screen reader name of the face: (network, holder, last four, frozen) ⇒ "Orbit card of Ines Moreau ending in 4821, frozen".</summary>
    public Func<string, string, string, bool, string> FaceName { get; init; } = (network, holder, last4, frozen)
        => $"{network} card of {holder} ending in {last4}{(frozen ? ", frozen" : string.Empty)}";

    /// <summary>Screen reader name of a row: (merchant, amount, date, status) ⇒ "Juniper Grocers, $42.10, Today, Pending".</summary>
    public Func<string, string, string, string?, string> RowName { get; init; } = (merchant, amount, date, status)
        => status is null ? $"{merchant}, {amount}, {date}" : $"{merchant}, {amount}, {date}, {status}";

    /// <summary>Announced when the card is frozen: "Card frozen".</summary>
    public string AnnounceFrozen { get; init; } = "Card frozen";

    /// <summary>Announced when the card is unfrozen: "Card unfrozen".</summary>
    public string AnnounceUnfrozen { get; init; } = "Card unfrozen";

    /// <summary>Announced when freezing or unfreezing fails: "The card could not be changed. Try again.".</summary>
    public string AnnounceFreezeFailed { get; init; } = "The card could not be changed. Try again.";

    /// <summary>Announced when the details are shown: "Card details shown".</summary>
    public string AnnounceDetailsShown { get; init; } = "Card details shown";

    /// <summary>Announced when the details are hidden: "Card details hidden".</summary>
    public string AnnounceDetailsHidden { get; init; } = "Card details hidden";
}

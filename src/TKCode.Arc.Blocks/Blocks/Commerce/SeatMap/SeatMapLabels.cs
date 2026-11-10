using System.Globalization;

namespace TKCode.Arc;

/// <summary>The built-in text of <see cref="SeatMap"/>. Replace the whole object to localize the block.</summary>
public sealed class SeatMapLabels
{
    /// <summary>Culture for prices (en-US by default).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The legend entry of taken seats: "Taken".</summary>
    public string Taken { get; init; } = "Taken";

    /// <summary>The legend entry of chosen seats: "Your seats".</summary>
    public string Chosen { get; init; } = "Your seats";

    /// <summary>A tier in the legend: (name, price) ⇒ "Premium · $120".</summary>
    public Func<string, string, string> Tier { get; init; } = (name, price) => $"{name} · {price}";

    /// <summary>Heading of the summary: "Your seats".</summary>
    public string SummaryHeading { get; init; } = "Your seats";

    /// <summary>Shown before any seat is chosen: "Pick seats on the map.".</summary>
    public string NothingChosen { get; init; } = "Pick seats on the map.";

    /// <summary>A chosen seat: (section, row, number) ⇒ "Orchestra · Row C · Seat 12".</summary>
    public Func<string, char, int, string> Seat { get; init; } = (section, row, number) => $"{section} · Row {row} · Seat {number}";

    /// <summary>The limit note: max ⇒ "You can choose up to 6 seats.".</summary>
    public Func<int, string> Limit { get; init; } = max => $"You can choose up to {max} seats.";

    /// <summary>Totals row: count ⇒ "3 seats".</summary>
    public Func<int, string> Subtotal { get; init; } = count => count == 1 ? "1 seat" : $"{count} seats";

    /// <summary>Totals row: "Fees".</summary>
    public string Fees { get; init; } = "Fees";

    /// <summary>Totals row: "Total".</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The continue button: "Continue".</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>Screen reader name of a remove button: seat ⇒ "Remove Orchestra · Row C · Seat 12".</summary>
    public Func<string, string> Remove { get; init; } = seat => $"Remove {seat}";

    /// <summary>Screen reader name of the map: "Seat map".</summary>
    public string MapName { get; init; } = "Seat map";

    /// <summary>The help text of the map: "Arrow keys move between seats, Enter or Space chooses one.".</summary>
    public string MapHelp { get; init; } = "Arrow keys move between seats, Enter or Space chooses one.";

    /// <summary>A seat read aloud: (seat, price, state) ⇒ "Orchestra · Row C · Seat 12, $95, available".</summary>
    public Func<string, string, string, string> SeatName { get; init; } = (seat, price, state) => $"{seat}, {price}, {state}";

    /// <summary>Seat states read aloud.</summary>
    public string Available { get; init; } = "available";

    /// <summary>A taken seat read aloud: "taken".</summary>
    public string TakenState { get; init; } = "taken";

    /// <summary>A chosen seat read aloud: "chosen".</summary>
    public string ChosenState { get; init; } = "chosen";

    /// <summary>Announced when a seat is chosen or released: (seat, chosen, count) ⇒ "Row C Seat 12 chosen. 3 seats.".</summary>
    public Func<string, bool, int, string> AnnounceToggle { get; init; } = (seat, chosen, count)
        => $"{seat} {(chosen ? "chosen" : "released")}. {(count == 1 ? "1 seat" : $"{count} seats")}.";

    /// <summary>The stage or front label: "Stage".</summary>
    public string Stage { get; init; } = "Stage";
}

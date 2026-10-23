using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="KanbanBoard"/> shows or announces, in English by default. Replace the instance (or create one
/// with <c>with</c>-style initializers) to translate or reword; texts with values are functions so word order can change.
/// </summary>
public sealed class KanbanBoardLabels
{
    /// <summary>The Status option of the view switch.</summary>
    public string StatusView { get; init; } = "Status";

    /// <summary>The swimlanes option of the view switch.</summary>
    public string LanesView { get; init; } = "By person";

    /// <summary>The accessible name of the view switch.</summary>
    public string ViewSwitchName { get; init; } = "Board view";

    /// <summary>The word after the total card count (the number rolls in front of it).</summary>
    public Func<int, string> CardsWord { get; init; } = count => count == 1 ? "card" : "cards";

    /// <summary>The accessible text of a count of cards (the board total, a column without a limit, a lane).</summary>
    public Func<int, string> CardCount { get; init; } = count => count == 1 ? "1 card" : $"{count} cards";

    /// <summary>The accessible text of a column count with a limit: count, then limit.</summary>
    public Func<int, int, string> CardCountOfLimit { get; init; } = (count, limit) => $"{count} of {limit} allowed";

    /// <summary>The chip shown on a column over its limit.</summary>
    public string OverLimit { get; init; } = "Above limit";

    /// <summary>The text in the slot while a drop would put its column over the limit.</summary>
    public Func<int, string> SlotOverLimit { get; init; } = limit => $"Limit of {limit} exceeded";

    /// <summary>The accessible name of a column's "+" button; the argument is the column name.</summary>
    public Func<string, string> AddCardTo { get; init; } = column => $"New card in {column}";

    /// <summary>The Add card row under a column, and the composer's submit button.</summary>
    public string AddCard { get; init; } = "Add card";

    /// <summary>The composer's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The accessible name of the composer's text box; the argument is the column name.</summary>
    public Func<string, string> ComposerName { get; init; } = column => $"Title of the new {column} card";

    /// <summary>The composer's placeholder.</summary>
    public string ComposerPlaceholder { get; init; } = "What needs doing?";

    /// <summary>A card's short estimate ("5 pt").</summary>
    public Func<int, string> PointsShort { get; init; } = points => $"{points} pt";

    /// <summary>A card's estimate in full ("1 point", "5 points").</summary>
    public Func<int, string> Points { get; init; } = points => points == 1 ? "1 point" : $"{points} points";

    /// <summary>The short date on cards and in the drawer.</summary>
    public Func<DateOnly, string> Date { get; init; } = date => date.ToString("MMM d", CultureInfo.CurrentCulture);

    /// <summary>The accessible text of a card's checklist progress: done, total.</summary>
    public Func<int, int, string> ChecklistProgress { get; init; } = (done, total) => $"{done} of {total} steps done";

    /// <summary>The accessible role description of a card.</summary>
    public string CardRole { get; init; } = "Movable card";

    /// <summary>The accessible name of a card: title, key, owner name (null when nobody owns it).</summary>
    public Func<string, string, string?, string> CardName { get; init; } =
        (title, key, owner) => owner is null ? $"{title}, {key}" : $"{title}, {key}, {owner}";

    /// <summary>The title shown for a card whose title is empty (the data keeps the empty text).</summary>
    public string Untitled { get; init; } = "Untitled";

    /// <summary>The accessible name of a swimlane; the argument is the person's name.</summary>
    public Func<string, string> LaneName { get; init; } = name => $"Cards for {name}";

    /// <summary>The lane of cards nobody owns, and the owner menu's entry for nobody.</summary>
    public string Unassigned { get; init; } = "Unassigned";

    /// <summary>The drawer's close button.</summary>
    public string CloseDetails { get; init; } = "Close card";

    /// <summary>The drawer's title field.</summary>
    public string TitleField { get; init; } = "Title";

    /// <summary>The drawer's status options.</summary>
    public string StatusField { get; init; } = "Status";

    /// <summary>The drawer's owner fact label.</summary>
    public string OwnerField { get; init; } = "Owner";

    /// <summary>The drawer's label fact label.</summary>
    public string LabelField { get; init; } = "Label";

    /// <summary>The drawer's due date fact label.</summary>
    public string DueField { get; init; } = "Due";

    /// <summary>The drawer's estimate fact label.</summary>
    public string EstimateField { get; init; } = "Estimate";

    /// <summary>The drawer's notes heading (and the notes box's accessible name).</summary>
    public string NotesField { get; init; } = "Notes";

    /// <summary>The notes box's placeholder.</summary>
    public string NotesPlaceholder { get; init; } = "Notes for whoever picks this up";

    /// <summary>The drawer's checklist heading.</summary>
    public string ChecklistField { get; init; } = "Steps";

    /// <summary>The add-step field's accessible name and placeholder.</summary>
    public string NewStep { get; init; } = "New step";

    /// <summary>The add-step button.</summary>
    public string AddStep { get; init; } = "Add";

    /// <summary>Shown in the drawer for a card without a due date.</summary>
    public string NoDate { get; init; } = "No due date";

    /// <summary>Shown in the drawer for a card without a label.</summary>
    public string NoLabel { get; init; } = "None";

    /// <summary>Shown in the drawer for a card without an estimate.</summary>
    public string NoEstimate { get; init; } = "Not estimated";

    /// <summary>The accessible name of the owner trigger: the owner's name.</summary>
    public Func<string, string> OwnerTriggerName { get; init; } = owner => $"Owner: {owner}";

    // Announcements (assertive while dragging, polite otherwise). Arguments: card title, column, person, position (1-based).

    /// <summary>A pointer or touch drag lifted a card.</summary>
    public Func<string, string> PickedUp { get; init; } = title => $"{title} picked up.";

    /// <summary>A keyboard drag lifted a card: title, then where it is (<see cref="Position"/>).</summary>
    public Func<string, string, string> Lifted { get; init; } =
        (title, where) => $"{title} lifted. {where}. Arrow keys move it, Space places it, Escape puts it back.";

    /// <summary>Where a card is during a keyboard drag in Status view: column, position.</summary>
    public Func<string, int, string> Position { get; init; } = (column, position) => $"{column}, place {position}";

    /// <summary>Where a card is during a keyboard drag in swimlanes: column, person, position.</summary>
    public Func<string, string, int, string> LanePosition { get; init; } =
        (column, person, position) => $"{column}, {person}, place {position}";

    /// <summary>A card was placed: title, column.</summary>
    public Func<string, string, string> Dropped { get; init; } = (title, column) => $"{title} placed in {column}.";

    /// <summary>A card was placed in another person's lane: title, column, person.</summary>
    public Func<string, string, string, string> DroppedForPerson { get; init; } =
        (title, column, person) => $"{title} placed in {column}, now with {person}.";

    /// <summary>A drag was cancelled and the card went back.</summary>
    public Func<string, string> Returned { get; init; } = title => $"{title} is back where it was.";

    /// <summary>Quick add created a card: title, column.</summary>
    public Func<string, string, string> Added { get; init; } = (title, column) => $"{title} added to {column}.";

    /// <summary>The drawer moved a card to another column: title, column.</summary>
    public Func<string, string, string> MovedTo { get; init; } = (title, column) => $"{title} moved to {column}.";

    /// <summary>A column went over its limit: column, count, limit.</summary>
    public Func<string, int, int, string> ColumnOverLimit { get; init; } =
        (column, count, limit) => $"{column} is above its limit: {count} of {limit}.";
}

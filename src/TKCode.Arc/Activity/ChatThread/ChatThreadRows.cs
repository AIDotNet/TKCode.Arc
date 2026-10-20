namespace TKCode.Arc;

/// <summary>One row of the thread's virtualized list: a day separator, a message, or the typing indicator.</summary>
internal abstract class ChatThreadRow
{
    /// <summary>Stable key (row models are kept across rebuilds so containers are not recreated).</summary>
    public abstract string Key { get; }

    /// <summary>Set for rows added after the thread was shown: their first realization plays the arrival.</summary>
    public bool Fresh { get; set; }
}

/// <summary>A day separator ("Today", "Yesterday", "Monday", "Mon, Oct 12").</summary>
internal sealed class ChatThreadDayRow(DateOnly day) : ChatThreadRow
{
    public DateOnly Day { get; } = day;

    /// <summary>A moment of that day (for formatting the label).</summary>
    public DateTimeOffset Sample { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>The first row of the list (4px on top instead of 20px).</summary>
    public bool IsFirst { get; set; }

    public override string Key { get; } = "day-" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A message with its place in its group.</summary>
internal sealed class ChatThreadMessageRow(ChatMessage message) : ChatThreadRow
{
    public ChatMessage Message { get; } = message;

    public ChatParticipant Author { get; set; } = new();

    /// <summary>Written by the current user (right side, accent bubble).</summary>
    public bool Mine { get; set; }

    /// <summary>Opens a group (meta line, 14px above).</summary>
    public bool First { get; set; }

    /// <summary>Closes a group (portrait in the gutter).</summary>
    public bool Last { get; set; }

    /// <summary>Directly under a day separator (10px above).</summary>
    public bool AfterDay { get; set; }

    /// <summary>Index of the message in the thread.</summary>
    public int Index { get; set; }

    /// <summary>Participants whose last read message this is (their receipts sit under it).</summary>
    public IReadOnlyList<ChatParticipant> Readers { get; set; } = Array.Empty<ChatParticipant>();

    /// <summary>The current user's last message: shows its delivery status.</summary>
    public bool ShowsStatus { get; set; }

    /// <summary>The status shown (failed and sending win; a receipt on or after it reads as read).</summary>
    public ChatMessageStatus Status { get; set; }

    public override string Key => Message.Id;
}

/// <summary>The typing indicator row (the first typer's portrait and breathing dots).</summary>
internal sealed class ChatThreadTypingRow : ChatThreadRow
{
    public const string RowKey = "\u0001typing";

    public ChatParticipant Typer { get; set; } = new();

    /// <summary>True while it plays its exit before it is removed.</summary>
    public bool Leaving { get; set; }

    public override string Key => RowKey;
}

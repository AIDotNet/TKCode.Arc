using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a person did with an item in <see cref="InboxTriage"/>.</summary>
public enum InboxTriageAction
{
    /// <summary>Done with it: it leaves the inbox.</summary>
    Archive,

    /// <summary>Not now: it leaves the inbox until <see cref="InboxTriageItem.SnoozedUntil"/>.</summary>
    Snooze,

    /// <summary>Handed to a teammate: it leaves the inbox with <see cref="InboxTriageItem.Assignee"/> set.</summary>
    Assign,

    /// <summary>A label added or removed; the item stays.</summary>
    Label
}

/// <summary>
/// One message, ticket or request waiting in an <see cref="InboxTriage"/>. Its fields can change in place (it raises
/// <see cref="PropertyChanged"/>); the block sets <see cref="Labels"/>, <see cref="Assignee"/>,
/// <see cref="SnoozedUntil"/> and <see cref="IsUnread"/> as people triage.
/// </summary>
public sealed class InboxTriageItem : INotifyPropertyChanged
{
    private string _sender = string.Empty;
    private string _subject = string.Empty;
    private string? _preview;
    private DateTimeOffset _receivedAt;
    private IReadOnlyList<string> _labels = Array.Empty<string>();
    private string? _assignee;
    private DateTimeOffset? _snoozedUntil;
    private bool _isUnread = true;

    /// <summary>Creates an empty item (XAML).</summary>
    public InboxTriageItem()
    {
    }

    /// <summary>Creates an item.</summary>
    public InboxTriageItem(string id, string sender, string subject, string? preview, DateTimeOffset receivedAt)
    {
        Id = id;
        _sender = sender;
        _subject = subject;
        _preview = preview;
        _receivedAt = receivedAt;
    }

    /// <summary>Raised when a property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable identity.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Who it is from; also the avatar's initials.</summary>
    public string Sender { get => _sender; set => Set(ref _sender, value); }

    /// <summary>The subject line; it also names the row for screen readers.</summary>
    public string Subject { get => _subject; set => Set(ref _subject, value); }

    /// <summary>The first line of the body; null shows none.</summary>
    public string? Preview { get => _preview; set => Set(ref _preview, value); }

    /// <summary>When it arrived.</summary>
    public DateTimeOffset ReceivedAt { get => _receivedAt; set => Set(ref _receivedAt, value); }

    /// <summary>Labels on the item, in the order they show.</summary>
    public IReadOnlyList<string> Labels { get => _labels; set => Set(ref _labels, value ?? Array.Empty<string>()); }

    /// <summary>The teammate it was handed to; null while unassigned.</summary>
    public string? Assignee { get => _assignee; set => Set(ref _assignee, value); }

    /// <summary>When a snoozed item comes back; null while not snoozed.</summary>
    public DateTimeOffset? SnoozedUntil { get => _snoozedUntil; set => Set(ref _snoozedUntil, value); }

    /// <summary>Whether it is unread (bold sender and a dot).</summary>
    public bool IsUnread { get => _isUnread; set => Set(ref _isUnread, value); }

    /// <summary>The subject (the swipe list names rows with it).</summary>
    public override string ToString() => Subject;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Arguments of <see cref="InboxTriage.Triaged"/> and <see cref="InboxTriage.Undone"/>.</summary>
public sealed class InboxTriageEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InboxTriageEventArgs(RoutedEvent routedEvent, InboxTriageItem item, InboxTriageAction action, string? value)
        : base(routedEvent)
    {
        Item = item;
        Action = action;
        Value = value;
    }

    /// <summary>The item.</summary>
    public InboxTriageItem Item { get; }

    /// <summary>What was done (or undone).</summary>
    public InboxTriageAction Action { get; }

    /// <summary>The teammate for <see cref="InboxTriageAction.Assign"/>, the label for <see cref="InboxTriageAction.Label"/>; null otherwise.</summary>
    public string? Value { get; }
}

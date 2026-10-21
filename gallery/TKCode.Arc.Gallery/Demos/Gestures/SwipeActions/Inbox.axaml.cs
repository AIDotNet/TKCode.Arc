using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SwipeActionsInboxDemo : UserControl
{
    private readonly ObservableCollection<Message> _messages = [];
    private Message? _lastRemoved;
    private int _lastIndex;

    public SwipeActionsInboxDemo()
    {
        InitializeComponent();
        Inbox.ItemsSource = _messages;
        Fill();
    }

    private void Fill()
    {
        _messages.Clear();
        _messages.Add(new Message("Maya Chen", "Design review moved to Thursday", "The room is booked from 2pm. Bring the latest flows.", "9:41"));
        _messages.Add(new Message("Linear", "3 issues assigned to you", "ARC-212 Swipe rows stutter on resize, and two more.", "8:15"));
        _messages.Add(new Message("Jonas Weber", "Invoice #2041", "Attached is the invoice for October. Thanks again!", "Yesterday"));
        _messages.Add(new Message("Calendar", "Standup in 10 minutes", "Daily standup · Room 4B · 10:00–10:15", "Yesterday"));
    }

    private void OnActionInvoked(object? sender, SwipeActionInvokedEventArgs e)
    {
        if (e.Item is not Message message)
        {
            return;
        }

        if (e.Action.KeepsRow)
        {
            message.IsUnread = !message.IsUnread;
            Status.Text = $"Marked “{message.Subject}” as {(message.IsUnread ? "unread" : "read")}.";
            return;
        }

        _lastIndex = _messages.IndexOf(message);
        _lastRemoved = message;
        _messages.Remove(message);
        Undo.IsVisible = true;
        Status.Text = $"{(e.Action.Tone == SwipeActionTone.Danger ? "Deleted" : "Archived")} “{message.Subject}”.";
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (_lastRemoved is null)
        {
            return;
        }

        _messages.Insert(Math.Clamp(_lastIndex, 0, _messages.Count), _lastRemoved);
        _lastRemoved = null;
        Undo.IsVisible = false;
        Status.Text = "Restored.";
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Fill();
        Undo.IsVisible = false;
        Status.Text = "Inbox reset.";
    }

    public sealed class Message(string sender, string subject, string preview, string time) : INotifyPropertyChanged
    {
        private bool _isUnread = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Sender { get; } = sender;

        public string Subject { get; } = subject;

        public string Preview { get; } = preview;

        public string Time { get; } = time;

        public bool IsUnread
        {
            get => _isUnread;
            set
            {
                if (_isUnread != value)
                {
                    _isUnread = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnread)));
                }
            }
        }

        public override string ToString() => Subject;
    }
}

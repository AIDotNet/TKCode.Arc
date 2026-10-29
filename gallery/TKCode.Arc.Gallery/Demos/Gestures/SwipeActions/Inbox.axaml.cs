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
        _messages.CollectionChanged += (_, _) => UpdateRemaining();
        Fill();
    }

    private void UpdateRemaining()
    {
        Remaining.Text = _messages.Count == 0 ? "All caught up" : $"{_messages.Count} left to triage";
    }

    private void Fill()
    {
        _messages.Clear();
        _messages.Add(new Message("Maya Chen", "Design review moved to Thursday", "The room is booked from 2pm. Bring the latest flows.", "9:41", 47));
        _messages.Add(new Message("Daniel Ortiz", "Contract draft for Fernhill", "Two clauses changed since Monday. Legal wants a reply today.", "8:15", 12));
        _messages.Add(new Message("Priya Nair", "Survey results are in", "Response rate was 38%. The summary deck is in the shared folder.", "Yesterday", 45) { IsUnread = false });
        _messages.Add(new Message("Jonas Weber", "Invoice #2041", "Attached is the invoice for October. Thanks again!", "Mon", 59) { IsUnread = false });
        UpdateRemaining();
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
        Recovery.IsVisible = true;
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
        Recovery.IsVisible = false;
        Status.Text = "Swipe a message, or use its menu.";
    }

    public sealed class Message(string sender, string subject, string preview, string time, int portrait) : INotifyPropertyChanged
    {
        private bool _isUnread = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Sender { get; } = sender;

        public string Subject { get; } = subject;

        public string Preview { get; } = preview;

        public string Time { get; } = time;

        public ImageSource? Photo { get; } = ImageSource.TryParse($"https://i.pravatar.cc/160?img={portrait}", out var source) ? source : null;

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

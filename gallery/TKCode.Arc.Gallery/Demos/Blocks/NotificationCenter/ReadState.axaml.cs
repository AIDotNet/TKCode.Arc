using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class NotificationCenterReadStateDemo : UserControl
{
    private readonly ObservableCollection<NotificationItem> _store = new();
    private readonly ObservableCollection<string> _log = new();
    private int _next;

    public NotificationCenterReadStateDemo()
    {
        InitializeComponent();
        Center.ItemsSource = _store;
        Log.ItemsSource = _log;
        Reset();
    }

    private void Reset()
    {
        _store.Clear();
        _store.Add(new NotificationItem { Id = "invoice", Title = "Invoice #1042 was paid", Description = "Acme Corp paid $4,800.", Time = "5m", Tone = NotificationTone.Success });
        _store.Add(new NotificationItem { Id = "comment", Title = "Leo Martin replied to your comment", Time = "26m", ActorName = "Leo Martin", ActorSource = ImageSourceOf("https://i.pravatar.cc/160?img=15") });
        _store.Add(new NotificationItem { Id = "seats", Title = "Two seats left on your plan", Description = "Add seats before inviting more people.", Time = "2h", Tone = NotificationTone.Warning, IsRead = true });
        _log.Clear();
        _next = 0;
        UpdateSummary();
    }

    private static ImageSource? ImageSourceOf(string url) =>
        ImageSource.TryParse(url, out var source) ? source : null;

    // Your data store learns about every change from the events; the items' IsRead is already updated.
    private void OnReadChanged(object? sender, NotificationReadChangedEventArgs e)
    {
        _log.Insert(0, $"Marked “{e.Item.Title}” {(e.IsRead ? "read" : "unread")}");
        UpdateSummary();
    }

    private void OnDismissed(object? sender, NotificationDismissedEventArgs e)
    {
        _log.Insert(0, $"Dismissed “{e.Item.Title}”");
        UpdateSummary();
    }

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        _next++;
        _store.Insert(0, new NotificationItem
        {
            Id = $"build-{_next}",
            Title = $"Build #{2810 + _next} passed",
            Description = "All 214 checks passed on main.",
            Time = "now",
            Tone = NotificationTone.Success
        });
        _log.Insert(0, $"Received “Build #{2810 + _next} passed”");
        UpdateSummary();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => Reset();

    private void UpdateSummary() =>
        Summary.Text = $"{_store.Count} stored · {_store.Count(i => !i.IsRead)} unread";
}

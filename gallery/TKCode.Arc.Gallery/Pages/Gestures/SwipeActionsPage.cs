using System.Collections.ObjectModel;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class SwipeActionsPage
{
    private sealed record InboxMessage(string Sender, string Subject, string Preview, string Time)
    {
        public bool IsUnread { get; set; } = true;

        public override string ToString() => Subject;
    }

    private static readonly InboxMessage[] Seed =
    [
        new("Maya Chen", "Design review moved to Thursday", "The room is booked from 2pm. Bring the latest flows.", "9:41"),
        new("Linear", "3 issues assigned to you", "ARC-212 Swipe rows stutter on resize, and two more.", "8:15"),
        new("Jonas Weber", "Invoice #2041", "Attached is the invoice for October. Thanks again!", "Yesterday"),
        new("Calendar", "Standup in 10 minutes", "Daily standup · Room 4B · 10:00–10:15", "Yesterday")
    ];

    public static Control Create()
    {
        var status = GalleryUi.Text("Swipe a row left or right, or open its More actions menu.", ArcDesignTokenKind.TextSecondary, 13);
        var messages = new ObservableCollection<InboxMessage>(Seed.Select(m => m with { }));
        var inbox = new SwipeActions
        {
            Label = "Inbox",
            Width = 440,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = messages,
            ItemTemplate = new FuncDataTemplate<InboxMessage>((message, _) => MessageView(message), supportsRecycling: false)
        };
        inbox.LeadingActions.Add(new SwipeAction { Label = "Unread", Icon = ArcIcons.Mail, Tone = SwipeActionTone.Accent, KeepsRow = true });
        inbox.TrailingActions.Add(new SwipeAction { Label = "Archive", Icon = ArcIcons.Archive });
        inbox.TrailingActions.Add(new SwipeAction { Label = "Delete", Icon = ArcIcons.Trash2, Tone = SwipeActionTone.Danger });

        InboxMessage? lastRemoved = null;
        var lastIndex = -1;
        var undo = new Button { Content = "Undo", Variant = ButtonVariant.Ghost, Size = ControlSize.Small, IsVisible = false };
        var reset = new Button { Content = "Reset inbox", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        inbox.ActionInvoked += (_, e) =>
        {
            if (e.Item is not InboxMessage message)
            {
                return;
            }
            if (e.Action.KeepsRow)
            {
                message.IsUnread = !message.IsUnread;
                if (e.Row.Content is Control view)
                {
                    UpdateUnread(view, message);
                }
                status.Text = $"Marked “{message.Subject}” as {(message.IsUnread ? "unread" : "read")}.";
                return;
            }
            lastIndex = messages.IndexOf(message);
            lastRemoved = message;
            messages.Remove(message);
            undo.IsVisible = true;
            status.Text = $"{(e.Action.Tone == SwipeActionTone.Danger ? "Deleted" : "Archived")} “{message.Subject}”.";
        };
        undo.Click += (_, _) =>
        {
            if (lastRemoved is null)
            {
                return;
            }
            messages.Insert(Math.Clamp(lastIndex, 0, messages.Count), lastRemoved);
            lastRemoved = null;
            undo.IsVisible = false;
            status.Text = "Restored.";
        };
        reset.Click += (_, _) =>
        {
            messages.Clear();
            foreach (var message in Seed)
            {
                messages.Add(message with { });
            }
            undo.IsVisible = false;
            status.Text = "Inbox reset.";
        };

        // Tasks declared inline, each row with its own actions; the second row has no full swipe.
        var tasks = new SwipeActions { Label = "Tasks", Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (title, fullSwipe) in new[] { ("Write the release notes", true), ("Review pull request #418", false), ("Book flights for the offsite", true) })
        {
            var row = new SwipeActionsRow
            {
                Label = title,
                IsFullSwipeEnabled = fullSwipe,
                Content = TaskView(title, fullSwipe ? null : "Full swipe off: tap the revealed action")
            };
            row.LeadingActions.Add(new SwipeAction { Label = "Done", Icon = ArcIcons.Check, Tone = SwipeActionTone.Accent });
            row.TrailingActions.Add(new SwipeAction { Label = "Flag", Icon = ArcIcons.Flag, KeepsRow = true });
            row.TrailingActions.Add(new SwipeAction { Label = "Delete", Icon = ArcIcons.Trash2, Tone = SwipeActionTone.Danger });
            tasks.Items.Add(row);
        }
        tasks.ActionInvoked += (_, e) =>
        {
            if (!e.Action.KeepsRow)
            {
                tasks.Items.Remove(e.Row);
            }
        };

        // A row resting open, so the revealed actions can be seen without a gesture.
        var revealed = new SwipeActions { Label = "Notifications", Width = 440, HorizontalAlignment = HorizontalAlignment.Left };
        var open = new SwipeActionsRow { Label = "Your export is ready", Content = TaskView("Your export is ready", "orders-2024.csv · 2.4 MB") };
        open.TrailingActions.Add(new SwipeAction { Label = "Mute", Icon = ArcIcons.BellOff, KeepsRow = true });
        open.TrailingActions.Add(new SwipeAction { Label = "Dismiss", Icon = ArcIcons.X, Tone = SwipeActionTone.Danger });
        var closed = new SwipeActionsRow { Label = "New sign-in from Safari", Content = TaskView("New sign-in from Safari", "Berlin, Germany · just now") };
        closed.TrailingActions.Add(new SwipeAction { Label = "Dismiss", Icon = ArcIcons.X, Tone = SwipeActionTone.Danger });
        revealed.Items.Add(open);
        revealed.Items.Add(closed);
        revealed.ActionInvoked += (_, e) =>
        {
            if (!e.Action.KeepsRow)
            {
                revealed.Items.Remove(e.Row);
            }
        };
        open.AttachedToVisualTree += (_, _) => open.Open(SwipeActionSide.Trailing);

        return GalleryUi.Column(20,
            GalleryUi.Card("Inbox",
                "Swipe right for Unread, left for Archive and Delete. Past the last stop the row follows with resistance; a long swipe commits the outermost action, which stretches across the row as the row slides away and the list closes the gap. Unread keeps the row.",
                GalleryUi.Column(12, inbox, GalleryUi.Row(undo, reset), status)),
            GalleryUi.Card("Rows with their own actions",
                "Inline rows declare their actions. The second row has no full swipe: rest it open and tap the action.",
                tasks),
            GalleryUi.Card("Resting open",
                "Actions share the revealed width; only one row of a list is open at a time, and pressing elsewhere or Escape puts it away.",
                revealed),
            GalleryUi.Card("Keyboard and menu",
                "Every row has a More actions button with the same actions, so the gesture is never required: Tab to it, press Enter or Space to open the menu on the first row, arrows to move, Escape to close the menu or an open row. A row removed from its menu hands focus to its neighbour.",
                GalleryUi.Text("Try it on the lists above.", ArcDesignTokenKind.TextSecondary, 13)));
    }

    private static Control MessageView(InboxMessage message)
    {
        var sender = GalleryUi.Text(message.Sender, ArcDesignTokenKind.Foreground, 14);
        sender.Name = "Sender";
        var time = GalleryUi.Text(message.Time, ArcDesignTokenKind.TextMuted, 12);
        time.HorizontalAlignment = HorizontalAlignment.Right;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(sender);
        Grid.SetColumn(time, 1);
        header.Children.Add(time);
        var subject = GalleryUi.Text(message.Subject, ArcDesignTokenKind.Foreground, 13);
        subject.TextTrimming = TextTrimming.CharacterEllipsis;
        var preview = GalleryUi.Text(message.Preview, ArcDesignTokenKind.TextSecondary, 13);
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        var view = new StackPanel { Spacing = 2, Children = { header, subject, preview } };
        UpdateUnread(view, message);
        return view;
    }

    private static void UpdateUnread(Control view, InboxMessage message)
    {
        if (view is StackPanel { Children: [Grid header, ..] } && header.Children[0] is TextBlock sender)
        {
            sender.FontWeight = message.IsUnread ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }

    private static Control TaskView(string title, string? detail)
    {
        var panel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(GalleryUi.Text(title, ArcDesignTokenKind.Foreground, 14));
        if (detail is not null)
        {
            panel.Children.Add(GalleryUi.Text(detail, ArcDesignTokenKind.TextSecondary, 13));
        }
        return panel;
    }
}

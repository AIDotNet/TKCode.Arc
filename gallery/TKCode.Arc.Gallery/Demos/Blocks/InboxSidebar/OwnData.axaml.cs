using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class InboxSidebarOwnDataDemo : UserControl
{
    private readonly InboxSidebarFolder _open = new("open", "Open", ArcIcons.Inbox, 7);
    private readonly DispatcherTimer _timer;

    public InboxSidebarOwnDataDemo()
    {
        InitializeComponent();
        Mail.Folders.Add(_open);
        Mail.Folders.Add(new InboxSidebarFolder("waiting", "Waiting on customer", ArcIcons.Clock, 3, isCountUnread: false));
        Mail.Folders.Add(new InboxSidebarFolder("solved", "Solved", ArcIcons.Check));
        Mail.MailLabels.Add(new InboxSidebarLabel("billing", "Billing", InboxSidebarLabelTone.Warning, 2));
        Mail.MailLabels.Add(new InboxSidebarLabel("bugs", "Bugs", InboxSidebarLabelTone.Danger, 4));
        Mail.MailLabels.Add(new InboxSidebarLabel("ideas", "Ideas", InboxSidebarLabelTone.Series2));
        // Counts are plain properties: change them and the row follows.
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.Background, (_, _) => _open.Count++);
        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void OnMarkRead(object? sender, RoutedEventArgs e) => _open.Count = 0;
}

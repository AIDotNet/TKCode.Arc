using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LiquidTabBarBadgesDemo : UserControl
{
    public LiquidTabBarBadgesDemo()
    {
        InitializeComponent();
        Receive.Click += (_, _) => SetMail(Mail.Badge + 3);
        Read.Click += (_, _) => SetMail(0);
        Assign.Click += (_, _) =>
        {
            Tasks.Badge++;
            Tasks.BadgeLabel = Tasks.Badge == 1 ? "1 due today" : $"{Tasks.Badge} due today";
        };
    }

    private void SetMail(int count)
    {
        Mail.Badge = count;
        Mail.BadgeLabel = count == 1 ? "1 unread" : $"{count} unread";
    }
}

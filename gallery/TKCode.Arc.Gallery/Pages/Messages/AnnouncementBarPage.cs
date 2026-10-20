using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class AnnouncementBarPage
{
    private static readonly IMessageStorage s_storage = new InMemoryMessageStorage();

    public static Control Create()
    {
        var rotating = new AnnouncementBar { Storage = s_storage };
        rotating.Messages.Add(new Announcement { Id = "launch", Message = "Arc 2.0 is here, with 40 new components.", Action = new AnnouncementAction { Label = "See what's new" } });
        rotating.Messages.Add(new Announcement { Id = "webinar", Message = "Live walkthrough on Thursday at 17:00 UTC.", Action = new AnnouncementAction { Label = "Save a seat" } });
        rotating.Messages.Add(new Announcement { Id = "status", Message = "All systems operational." });

        var controls = new AnnouncementBar { Storage = s_storage, IsNavigationVisible = true };
        controls.Messages.Add(new Announcement
        {
            Id = "sale",
            Message = "Spring sale: 30% off annual plans.",
            Countdown = new AnnouncementCountdown { To = DateTimeOffset.Now.AddDays(1).AddHours(3), Label = "Ends in" },
            Action = new AnnouncementAction { Label = "Upgrade" }
        });
        controls.Messages.Add(new Announcement { Id = "docs", Message = "The new docs have a search that understands questions." });
        controls.Messages.Add(new Announcement { Id = "mobile", Message = "Arc for iOS is in public beta.", Action = new AnnouncementAction { Label = "Join the beta" } });

        var inverted = new AnnouncementBar { Storage = s_storage, Tone = AnnouncementBarTone.Inverted };
        inverted.Messages.Add(new Announcement
        {
            Id = "maintenance",
            Message = "Scheduled maintenance tonight.",
            Countdown = new AnnouncementCountdown { To = DateTimeOffset.Now.AddHours(5).AddMinutes(12), Label = "Starts in" }
        });

        var remembered = new AnnouncementBar { Storage = s_storage, DismissalKey = "demo-campaign" };
        remembered.Messages.Add(new Announcement { Id = "campaign", Message = "Dismiss me: the choice is remembered under a key." });
        var log = GalleryUi.Text("Events appear here.", ArcDesignTokenKind.TextMuted, 12);
        rotating.ActionInvoked += (_, e) => log.Text = $"Action: {e.Announcement.Id}";
        controls.ActionInvoked += (_, e) => log.Text = $"Action: {e.Announcement.Id}";
        remembered.Closed += (_, _) => log.Text = "Dismissed and remembered.";
        var reset = new Button { Content = "Forget dismissal", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        reset.Click += (_, _) =>
        {
            AnnouncementBar.ClearDismissal("demo-campaign", s_storage);
            remembered.DismissalKey = null;
            remembered.DismissalKey = "demo-campaign";
            remembered.IsOpen = true;
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Rotating messages", "Every 6s the next message rises from below while the bar springs to its height; hover or focus pauses it.",
                Frame(rotating)),
            GalleryUi.Card("Controls and countdown", "Previous, next and a pause ring that fills over the interval. Countdown digits drop in on the beat.",
                Frame(controls)),
            GalleryUi.Card("Inverted", "Foreground fill with background text for high-priority notices.", Frame(inverted)),
            GalleryUi.Card("Remembered dismissal", "With a DismissalKey the dismissal is stored; the bar collapses so the page below eases up.",
                GalleryUi.Column(12, Frame(remembered), GalleryUi.Row(reset), log)));
    }

    private static Border Frame(Control bar)
    {
        var frame = new Border
        {
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new StackPanel
            {
                Children =
                {
                    bar,
                    new TextBlock { Text = "Page content", Margin = new Thickness(20, 24), Opacity = 0.6 }
                }
            }
        };
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return frame;
    }
}

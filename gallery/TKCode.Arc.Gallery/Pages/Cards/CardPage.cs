using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class CardPage
{
    public static Control Create()
    {
        var grid = new UniformGrid { Columns = 3, ColumnSpacing = 16, RowSpacing = 16 };
        grid.Children.Add(Project("Harbour redesign", "New booking flow and room pages.", "Maya Chen", "Updated 2 hours ago",
            Gradient("#5B8DEF", "#9CC2FF"), ArcIcons.House,
            "Scope: a calmer booking flow with fewer steps, room pages rebuilt around photos, and an availability calendar. Open questions: deposits, and how far ahead people can book."));
        grid.Children.Add(Project("Atlas", "A field guide to every API the platform exposes.", "Jonas Berg", "Updated yesterday",
            Gradient("#2FA88B", "#8EE0C6"), ArcIcons.Globe,
            "Milestones: endpoint inventory (done), examples for every resource (in review), versioned changelog (next). Owners rotate weekly."));
        grid.Children.Add(Project("Night market", "Pop-up stalls, lights and a map for the autumn fair.", "Ada Okafor", "Updated 3 days ago",
            Gradient("#E0814A", "#F7C59F"), ArcIcons.Sparkles,
            "Forty stalls across two streets. Lights go up Friday morning; the printed map ships to stall holders a week before."));

        var status = new Card
        {
            Title = "Weekly digest",
            Description = "Highlights from every team, every Friday.",
            Avatar = new Avatar { DisplayName = "Lin Park", Size = AvatarSize.Small },
            Meta = "Lin Park",
            Status = "Updated 2 hours ago",
            Width = 300
        };
        var steps = new[] { "Updated just now", "Edited 1 minute ago", "Published to 4 teams", "Updated 2 hours ago" };
        var step = 0;
        var bump = new Button { Content = "Change status", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        bump.Click += (_, _) => status.Status = steps[step++ % steps.Length];

        var plain = new Card { Title = "Plain card", Description = "A title and a line of copy, nothing else.", Width = 240, VerticalAlignment = VerticalAlignment.Top };
        var withAction = new Card
        {
            Title = "Design review",
            Description = "Thursday, 14:00 · Room 3",
            Meta = "Six people",
            Action = new Button { Content = "Join", Variant = ButtonVariant.Secondary, Size = ControlSize.Small },
            Width = 260,
            VerticalAlignment = VerticalAlignment.Top
        };
        var media = new Card
        {
            Title = "Spring palette",
            Media = Gradient("#B07CE8", "#F3B6D8"),
            Content = GalleryUi.Text("Twelve swatches tuned for print and screen.", ArcDesignTokenKind.TextSecondary, 14),
            Width = 240,
            VerticalAlignment = VerticalAlignment.Top
        };

        var controlled = new Card
        {
            Title = "Quarterly plan",
            Description = "Goals, owners and dates for the next three months.",
            Media = Gradient("#4A5568", "#A0AEC0"),
            Details = GalleryUi.Text("Opened from code: IsDetailsOpen is two-way, so a binding or a button can open and close the quick look.",
                ArcDesignTokenKind.TextSecondary, 14),
            Width = 280
        };
        var open = new Button { Content = "Open quick look", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        open.Click += (_, _) => controlled.IsDetailsOpen = true;
        var log = GalleryUi.Text("Closed", ArcDesignTokenKind.TextMuted, 13);
        controlled.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "Open" : $"Closed ({e.Reason})";

        return GalleryUi.Column(20,
            GalleryUi.Card("Quick look", "Click a card (or Tab to its title and press Enter): its surface, photo, title and byline grow into a larger view on one spring while the page dims. Escape, the scrim or the close button send it back.", grid),
            GalleryUi.Card("Status updates", "A new status lifts the old line away while its words rise in one after another; it is announced politely.",
                GalleryUi.Column(12, GalleryUi.Row(status), GalleryUi.Row(bump))),
            GalleryUi.Card("Without a quick look", "Without details the title is a plain heading and the card only lifts on hover; footer actions stay separately clickable.",
                GalleryUi.Row(plain, withAction, media)),
            GalleryUi.Card("Controlled", "IsDetailsOpen opens and closes the quick look; DetailsOpenChanged reports why it closed.",
                GalleryUi.Column(12, GalleryUi.Row(controlled), GalleryUi.Row(open, log))));
    }

    private static Card Project(string title, string description, string owner, string status, IBrush media, Geometry icon, string details)
    {
        var art = new Border { Height = 132, Background = media };
        var glyph = new ArcGlyph { Data = icon, Size = 36, StrokeThickness = 1.5, Foreground = Brushes.White, Opacity = 0.9 };
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        glyph.VerticalAlignment = VerticalAlignment.Center;
        art.Child = glyph;
        return new Card
        {
            Title = title,
            Description = description,
            Media = art,
            Avatar = new Avatar { DisplayName = owner, Size = AvatarSize.Small },
            Meta = owner,
            Status = status,
            Action = new Button { Content = "Share", Variant = ButtonVariant.Ghost, Size = ControlSize.Small },
            Details = GalleryUi.Text(details, ArcDesignTokenKind.TextSecondary, 14),
            VerticalAlignment = VerticalAlignment.Top
        };
    }

    private static IBrush Gradient(string from, string to) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(from), 0), new GradientStop(Color.Parse(to), 1) }
    };
}

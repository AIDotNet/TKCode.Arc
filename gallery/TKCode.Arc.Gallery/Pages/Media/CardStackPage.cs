using AtomUI.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class CardStackPage
{
    private sealed record Destination(string Name, string Country, string Note, int Scene, string? Photo = null);

    private static readonly Destination[] s_destinations =
    [
        new("Lisbon", "Portugal", "Tiled streets, trams and late dinners.", 2, "https://picsum.photos/seed/arc-lisbon/640/400"),
        new("Kyoto", "Japan", "Temples, gardens and quiet mornings.", 5, "https://picsum.photos/seed/arc-kyoto/640/400"),
        new("Oaxaca", "Mexico", "Markets, mezcal and mountain light.", 0, "https://picsum.photos/seed/arc-oaxaca/640/400"),
        new("Tbilisi", "Georgia", "Sulphur baths and balconies.", 4, "https://picsum.photos/seed/arc-tbilisi/640/400"),
        new("Hobart", "Australia", "Harbour walks and wild coast.", 3, "https://picsum.photos/seed/arc-hobart/640/400"),
        new("Bergen", "Norway", "Fjords on the doorstep.", 1, "https://picsum.photos/seed/arc-bergen/640/400")
    ];

    public static Control Create()
    {
        var log = GalleryUi.Mono("Drag a card, or focus the stack and press ← / →.");
        var shortlist = new CardStack
        {
            Label = "Destinations",
            ItemsSource = s_destinations,
            LabelSelector = item => ((Destination)item!).Name,
            LeftOutcome = "skipped",
            RightOutcome = "shortlisted",
            ItemTemplate = new FuncDataTemplate<Destination>((d, _) => d is null ? null : Card(d, photos: false))
        };
        shortlist.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: {(e.Decision == CardDecision.Right ? "shortlisted" : "skipped")}";
        shortlist.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}: restored";
        shortlist.StackReset += (_, _) => log.Text = "All cards are back in the stack.";

        var candidates = new CardStack
        {
            Label = "Candidates",
            LeftLabel = "Not now",
            RightLabel = "Interview",
            ItemsSource = new[] { "Ada Lovelace", "Grace Hopper", "Alan Turing" },
            ItemTemplate = new FuncDataTemplate<string>((name, _) => name is null ? null : Candidate(name)),
            EmptyContent = EmptyState()
        };

        var photos = new CardStack
        {
            Label = "Photographed destinations",
            ItemsSource = s_destinations,
            LabelSelector = item => ((Destination)item!).Name,
            ItemTemplate = new FuncDataTemplate<Destination>((d, _) => d is null ? null : Card(d, photos: true))
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Shortlist",
                "Drag or flick the top card left or right; the cards behind rise as it leaves. A short drag springs back. Undo flies the last card back from where it left; Backspace and Ctrl / Cmd + Z undo from the keyboard.",
                GalleryUi.Column(16, shortlist, Centered(log))),
            GalleryUi.Card("Custom actions and empty state",
                "Action names label the buttons and the stamps that appear while dragging. Once every card is decided, custom content replaces the default empty state and can bind to ResetCommand.",
                candidates),
            GalleryUi.Card("Photos",
                "Card faces with AsyncImage photos over a drawn scene, so they still look complete offline.",
                photos));
    }

    private static Control Centered(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Center;
        return control;
    }

    private static Control Card(Destination destination, bool photos)
    {
        var art = new Panel();
        art.Children.Add(new MediaArt { Scene = destination.Scene });
        if (photos && destination.Photo is { } url)
        {
            art.Children.Add(new AsyncImage { Source = ImageSource.Parse(url), Stretch = Stretch.UniformToFill });
        }
        var text = GalleryUi.Column(4,
            GalleryUi.Text(destination.Name, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium),
            GalleryUi.Text($"{destination.Country} · {destination.Note}", ArcDesignTokenKind.TextSecondary, 14));
        text.Margin = new Thickness(20, 16, 20, 20);
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        grid.Children.Add(art);
        Grid.SetRow(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static Control Candidate(string name)
    {
        var avatar = new Avatar { DisplayName = name, Size = AvatarSize.XLarge, HorizontalAlignment = HorizontalAlignment.Center };
        var column = GalleryUi.Column(14,
            avatar,
            GalleryUi.Text(name, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium),
            GalleryUi.Text("Senior engineer · 9 years · Remote", ArcDesignTokenKind.TextSecondary, 14));
        foreach (var child in column.Children)
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        column.VerticalAlignment = VerticalAlignment.Center;
        return column;
    }

    private static Control EmptyState()
    {
        var again = new Button { Content = "Review again", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        again.Click += (_, _) =>
        {
            if (again.FindAncestorOfType<CardStack>() is { } stack)
            {
                stack.ResetCommand.Execute(null);
            }
        };
        var column = GalleryUi.Column(12,
            GalleryUi.Glyph(ArcIcons.Check, 28, ArcDesignTokenKind.Success),
            GalleryUi.Text("Every candidate has a decision", ArcDesignTokenKind.Foreground, 16, FontWeight.Medium),
            again);
        foreach (var child in column.Children)
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        return column;
    }
}

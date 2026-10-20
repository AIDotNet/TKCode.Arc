using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ReactionsPage
{
    /// <summary>Lucide party-popper (not in the Core glyph subset).</summary>
    private static readonly Geometry PartyPopper = ArcIcons.Parse(
        "M5.8 11.3 2 22l10.7-3.79 M4 3h.01 M22 8h.01 M15 2h.01 M22 20h.01 " +
        "M22 2l-2.24.75a2.9 2.9 0 0 0-1.96 3.12c.1.86-.57 1.63-1.45 1.63h-.38c-.86 0-1.6.6-1.76 1.44L14 10 " +
        "M22 13l-.82-.33c-.86-.34-1.82.2-1.98 1.11c-.11.7-.72 1.22-1.43 1.22H17 " +
        "M11 2l.33.82c.34.86-.2 1.82-1.11 1.98C9.52 4.9 9 5.52 9 6.23V7 " +
        "M11 13c1.93 1.93 2.83 4.17 2 5-.83.83-3.07-.07-5-2-1.93-1.93-2.83-4.17-2-5 .83-.83 3.07.07 5 2Z");

    private static ReactionOption[] DemoOptions() =>
    [
        new("like", "Like", ArcIcons.ThumbsUp),
        new("love", "Love", ArcIcons.Heart),
        new("celebrate", "Celebrate", PartyPopper)
    ];

    private static ReactionOption[] AllOptions() =>
    [
        new("like", "Like", ArcIcons.ThumbsUp),
        new("love", "Love", ArcIcons.Heart),
        new("celebrate", "Celebrate", PartyPopper),
        new("smile", "Smile", ArcIcons.Smile),
        new("star", "Star", ArcIcons.Star),
        new("ship", "Ship it", ArcIcons.Zap)
    ];

    public static Control Create()
    {
        // Arc's demo: three options, a Like count of 3 the viewer has not joined yet.
        var demo = new Reactions { Value = [new Reaction("like", 3)] };
        foreach (var option in DemoOptions())
        {
            demo.Options.Add(option);
        }
        var announcement = GalleryUi.Text("Tap a pill or add a reaction.", ArcDesignTokenKind.TextMuted, 13);
        demo.ValueChanged += (_, _) => announcement.Text = demo.LastAnnouncement;

        var people = new Reactions
        {
            Value =
            [
                new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
                new Reaction("celebrate", 2) { ReactedBy = ["Maya Chen", "Sam Ortiz"] },
                new Reaction("ship", 12) { ReactedBy = ["Priya Nair", "Tom Hale", "Eli Moss"] }
            ]
        };
        foreach (var option in AllOptions())
        {
            people.Options.Add(option);
        }

        var narrow = new Reactions
        {
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Value =
            [
                new Reaction("like", 8), new Reaction("love", 3, true), new Reaction("celebrate", 1)
            ]
        };
        foreach (var option in AllOptions())
        {
            narrow.Options.Add(option);
        }

        var live = new Reactions { Value = [new Reaction("like", 98), new Reaction("love", 9, true)] };
        foreach (var option in DemoOptions())
        {
            live.Options.Add(option);
        }
        var random = new Random(7);
        var someone = new Button { Content = "Someone reacts", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        someone.Click += (_, _) =>
        {
            var current = live.Value ?? [];
            var id = random.Next(3) switch { 0 => "like", 1 => "love", _ => "celebrate" };
            var existing = current.FirstOrDefault(r => r.Id == id);
            live.Value = existing is null
                ? [.. current, new Reaction(id, 1)]
                : current.Select(r => r.Id == id ? r with { Count = r.Count + 1 } : r).ToArray();
        };
        var someoneLeaves = new Button { Content = "Someone leaves", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        someoneLeaves.Click += (_, _) =>
        {
            var current = live.Value ?? [];
            var target = current.Where(r => r.Count > (r.Reacted ? 1 : 0)).OrderBy(_ => random.Next()).FirstOrDefault();
            if (target is not null)
            {
                live.Value = current.Select(r => r == target ? r with { Count = r.Count - 1 } : r).Where(r => r.Count > 0).ToArray();
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Reactions", "One tap adds or removes your reaction: the count rolls and the icon kicks (tap quickly and the kicks stack). The add button opens a picker that grows from it; arrows move, Enter picks, Escape closes.",
                GalleryUi.Column(12, Left(demo), announcement)),
            GalleryUi.Card("Who reacted", "Hover or focus a pill to see its label and who reacted. Your own reactions take the accent and a filled icon.",
                Left(people)),
            GalleryUi.Card("Wrapping", "The bar wraps as pills are added; a new row grows the bar on a spring and the pills glide into place.",
                Left(narrow)),
            GalleryUi.Card("Live counts", "Counts that change elsewhere roll only the digits that changed, up when they grow and down when they shrink.",
                GalleryUi.Column(12, Left(live), GalleryUi.Row(someone, someoneLeaves))));
    }

    private static Control Left(Control content)
    {
        content.HorizontalAlignment = HorizontalAlignment.Left;
        return content;
    }
}

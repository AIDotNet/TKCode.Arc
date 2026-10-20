using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ExpandableTextPage
{
    private const string Review =
        "We moved our whole design team onto the new workspace in a single afternoon. The import kept every comment and version, " +
        "and the shared libraries meant nobody had to rebuild components from scratch. Two weeks in, review cycles are noticeably " +
        "shorter: feedback lands on the exact frame, threads resolve themselves when the change ships, and the weekly digest " +
        "finally replaced our status meeting. The only thing we miss is offline editing on long flights, which the team says is " +
        "coming next quarter.";

    public static Control Create()
    {
        var review = new ExpandableText { Text = Review, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };

        var comment = new ExpandableText
        {
            Lines = 2,
            Text = "Love the new keyboard shortcuts. Cmd+K to jump anywhere is the feature I did not know I needed, and the command bar remembers what I used last, which saves a surprising amount of time every day.",
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var shortText = new ExpandableText { Text = "Short notes fit on their lines, so no toggle appears.", MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };

        var state = GalleryUi.Text("Collapsed", ArcDesignTokenKind.TextMuted, 12);
        var bio = new ExpandableText
        {
            Lines = 4,
            Text = "Maya leads platform design at Acme, where she shaped the component library used by forty product teams. " +
                   "Before Acme she built accessibility tooling at a public broadcaster and taught interaction design at the city college. " +
                   "She writes about motion that explains rather than decorates, and she still sketches every interface on paper first. " +
                   "Outside work she restores old film cameras and runs a small darkroom for the neighborhood.",
            MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        bio.ExpandedChanged += (_, e) => state.Text = e.IsExpanded ? "Expanded" : "Collapsed";
        var external = new Button { Content = "Toggle from outside", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        external.Click += (_, _) => bio.IsExpanded = !bio.IsExpanded;

        return GalleryUi.Column(20,
            GalleryUi.Card("Expandable text", "Three lines with a soft fade over the last one. Read more glides open with a calm ease out; Show less closes it. A click mid-flight reverses from where it is.", review),
            GalleryUi.Card("Two lines", "Dense lists such as comments use two lines.", comment),
            GalleryUi.Card("Fits", "The toggle appears only when the text really overflows, and follows width and font changes.", shortText),
            GalleryUi.Card("Controlled", "IsExpanded is two-way; ExpandedChanged reports the new state.", GalleryUi.Column(12, bio, GalleryUi.Row(external, state))));
    }
}

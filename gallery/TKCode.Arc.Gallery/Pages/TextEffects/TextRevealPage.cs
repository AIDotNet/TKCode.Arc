using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class TextRevealPage
{
    public static Control Create()
    {
        var hero = new TextReveal { Text = "Interfaces that feel considered from the first frame", Level = TextLevel.H1 };
        var replay = new Button { Content = "Replay", Variant = ButtonVariant.Secondary };
        replay.Click += (_, _) => hero.Replay();

        var lines = new TextReveal { Text = "Ship the quiet details.\nKeep the motion honest.", Level = TextLevel.H2, Delay = TimeSpan.FromSeconds(0.2) };
        var replayLines = new Button { Content = "Replay", Variant = ButtonVariant.Secondary };
        replayLines.Click += (_, _) => lines.Replay();

        var paragraph = new TextReveal
        {
            Text = "A paragraph reveals with a softer blur, word after word, and the whole stagger stays under half a second.",
            Level = TextLevel.Paragraph
        };
        var replayParagraph = new Button { Content = "Replay", Variant = ButtonVariant.Secondary };
        replayParagraph.Click += (_, _) => paragraph.Replay();

        return GalleryUi.Column(20,
            GalleryUi.Card("Hero", "Each word rises out of its own feathered clip while it sharpens from a blur; lines are balanced and at most 24 characters wide.",
                GalleryUi.Column(16, hero, GalleryUi.Row(replay))),
            GalleryUi.Card("Forced lines and delay", "\\n forces a line break; Delay waits before the first word.", GalleryUi.Column(16, lines, GalleryUi.Row(replayLines))),
            GalleryUi.Card("Paragraph", "Paragraphs use body type and the soft blur.", GalleryUi.Column(16, paragraph, GalleryUi.Row(replayParagraph))));
    }
}

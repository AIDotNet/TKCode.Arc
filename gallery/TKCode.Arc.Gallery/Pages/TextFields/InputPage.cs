using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class InputPage
{
    public static Control Create()
    {
        var email = new Input { Label = "Email", Placeholder = "you@example.com", Width = 360, HorizontalAlignment = HorizontalAlignment.Left };

        // Helper copy is derived from state: changing the string rewrites only the words that changed, and counts roll.
        var handle = new Input
        {
            Label = "Username",
            Placeholder = "ada",
            Description = "3 to 20 characters. 20 left.",
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        handle.TextChanged += (_, _) =>
        {
            var text = handle.Text ?? string.Empty;
            var left = Math.Max(0, 20 - text.Length);
            handle.Description = $"3 to 20 characters. {left} left.";
            handle.ErrorMessage = text.Length is > 0 and < 3
                ? "Use at least 3 characters."
                : text.Contains(' ') ? "Spaces are not allowed in a username." : null;
        };

        var sizes = GalleryUi.Column(16,
            new Input { Label = "Small", Size = ControlSize.Small, Placeholder = "36px tall", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
            new Input { Label = "Medium", Placeholder = "44px tall (default)", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
            new Input { Label = "Large", Size = ControlSize.Large, Placeholder = "50px tall", Width = 360, HorizontalAlignment = HorizontalAlignment.Left });

        var states = GalleryUi.Column(16,
            new Input { Label = "Disabled", Text = "Not editable right now", IsEnabled = false, Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
            new Input { Label = "Read-only", Text = "ws_7f3a9c", IsReadOnly = true, Description = "Copy it, but it cannot change.", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
            new Input { Label = "Website", Text = "example", ErrorMessage = "Enter a full address, like https://example.com.", Width = 360, HorizontalAlignment = HorizontalAlignment.Left });

        var affixes = GalleryUi.Column(16,
            new Input { Label = "Site", Prefix = Muted("https://"), Placeholder = "example.com", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
            new Input { Label = "Price", Prefix = Muted("$"), Suffix = Muted("USD"), Text = "49", Width = 360, HorizontalAlignment = HorizontalAlignment.Left });

        // The shared foundation: any editor or trigger gets the same label, frame and message rows.
        var trigger = new Field
        {
            Label = "Due date",
            Description = "Pickers and special inputs reuse Field and FieldFrame.",
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new FieldFrame
            {
                Prefix = new ArcGlyph { Data = ArcIcons.Calendar, Size = 18 },
                Content = GalleryUi.Text("Pick a date", ArcDesignTokenKind.TextMuted)
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Basic", "A labeled single-line field. Hover and focus darken the border; nothing changes size.", email),
            GalleryUi.Card("Helper and live error", "Type a space or fewer than three characters: the error row opens on a spring and only changed words move.", handle),
            GalleryUi.Card("Sizes", "Small, Medium and Large map to ControlSizeSM / MD / LG.", sizes),
            GalleryUi.Card("States", "Disabled, read-only and invalid.", states),
            GalleryUi.Card("Prefix and suffix", "Slots inside the frame for units, currencies and actions.", affixes),
            GalleryUi.Card("Field foundation", "Field (label and messages) around a FieldFrame trigger.", trigger));
    }

    private static TextBlock Muted(string text) => GalleryUi.Text(text, ArcDesignTokenKind.TextMuted);
}

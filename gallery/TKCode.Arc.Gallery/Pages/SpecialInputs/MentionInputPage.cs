using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class MentionInputPage
{
    private static readonly MentionPerson[] s_people =
    [
        new("u_maya", "Maya Chen") { Role = "Product designer" },
        new("u_jordan", "Jordan Lee") { Role = "Engineering manager" },
        new("u_priya", "Priya Natarajan") { Role = "Frontend engineer" },
        new("u_sam", "Sam Okafor") { Role = "Data scientist" },
        new("u_alex", "Alex Rivera") { Role = "Support lead" },
        new("u_emma", "Emma Schultz") { Role = "Marketing" },
        new("u_noah", "Noah Kim") { Role = "Design systems" }
    ];

    private static readonly MentionChannel[] s_channels =
    [
        new("c_design", "design") { Description = "Critiques, reviews and handoff", Members = 48 },
        new("c_engineering", "engineering") { Description = "Builds, deploys and code review", Members = 132 },
        new("c_launch", "launch-q3") { Description = "Q3 launch coordination", Members = 19 },
        new("c_support", "support") { Description = "Customer escalations", Members = 41 },
        new("c_random", "random") { Description = "Everything else", Members = 256 }
    ];

    public static Control Create()
    {
        return GalleryUi.Column(20,
            GalleryUi.Card("Comment box",
                "Type @ for people or # for channels. Arrow keys move, Enter or Tab picks, Escape closes this list only. " +
                "Mentions are single tokens: arrows hop over them and one Backspace removes the whole token.",
                CommentBox()),
            GalleryUi.Card("Submit on Enter",
                "With IsSubmitOnEnter, Enter sends and Shift+Enter adds a line. Picking a suggestion with Enter never sends.",
                SubmitOnEnter()),
            GalleryUi.Card("Prefilled value",
                "A value carries plain text plus mentions with offsets; editing around a token moves it, editing inside drops it.",
                Prefilled()),
            GalleryUi.Card("Growth and scrolling",
                "The field springs between MinLines and MaxLines (here 2 and 4 lines of 24px), then scrolls with a hidden scrollbar.",
                Growth()),
            GalleryUi.Card("Placement",
                "SuggestionPlacement Top always opens above the line; Auto flips above when there is less than 300px below.",
                PlacementTop()),
            GalleryUi.Card("Disabled", "The whole field fades to 55% and takes no input.", Disabled()),
            GalleryUi.Card("Serialized output",
                "MentionSerializer.Serialize turns the value into storage text: <@id> for people and <#id> for channels.",
                Serialized()));
    }

    private static MentionInput Field(string label, string? placeholder = null) => new()
    {
        Label = label,
        Placeholder = placeholder,
        People = s_people,
        Channels = s_channels,
        Width = 440,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private static Control CommentBox()
    {
        var input = Field("Comment", "Leave a comment. Use @ to mention, # for a channel");
        var added = GalleryUi.Text("Mentions appear here as you add them.", ArcDesignTokenKind.TextMuted, 13);
        added.HorizontalAlignment = HorizontalAlignment.Center;
        input.MentionAdded += (_, e) => added.Text = $"Added {MentionSerializer.MentionText(e.Mention.Kind, e.Mention.Label)} ({e.Mention.Id})";
        var mention = new Button { Content = "Mention someone", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        mention.Click += (_, _) => input.OpenSuggestions(MentionKind.Person);
        var channel = new Button { Content = "Link a channel", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        channel.Click += (_, _) => input.OpenSuggestions(MentionKind.Channel);
        var clear = new Button { Content = "Clear", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        clear.Click += (_, _) => input.Clear();
        return GalleryUi.Column(14, input, GalleryUi.Row(mention, channel, clear), added);
    }

    private static Control SubmitOnEnter()
    {
        var input = Field("Message", "Message #design");
        input.IsSubmitOnEnter = true;
        var log = new StackPanel { Spacing = 6, Width = 440, HorizontalAlignment = HorizontalAlignment.Center };
        var empty = GalleryUi.Text("Sent messages show up here.", ArcDesignTokenKind.TextMuted, 13);
        log.Children.Add(empty);
        input.Submitted += (_, e) =>
        {
            log.Children.Remove(empty);
            var mentions = e.Value.Mentions.Count == 1 ? "1 mention" : $"{e.Value.Mentions.Count} mentions";
            log.Children.Add(GalleryUi.Text($"Sent: {e.Value.Text.Trim()}  ·  {mentions}", ArcDesignTokenKind.TextSecondary, 13));
            input.Clear();
        };
        return GalleryUi.Column(14, input, log);
    }

    private static Control Prefilled()
    {
        const string text = "Thanks @Maya Chen, the new flow reads well. Can @Jordan Lee review it in #design before Friday?";
        var maya = text.IndexOf("@Maya Chen", StringComparison.Ordinal);
        var jordan = text.IndexOf("@Jordan Lee", StringComparison.Ordinal);
        var design = text.IndexOf("#design", StringComparison.Ordinal);
        var input = Field("Review note");
        input.Value = new MentionValue(text,
        [
            new Mention(MentionKind.Person, "u_maya", "Maya Chen", maya, maya + "@Maya Chen".Length),
            new Mention(MentionKind.Person, "u_jordan", "Jordan Lee", jordan, jordan + "@Jordan Lee".Length),
            new Mention(MentionKind.Channel, "c_design", "design", design, design + "#design".Length)
        ]);
        var count = GalleryUi.Text(Summary(input.Value), ArcDesignTokenKind.TextMuted, 13);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        input.ValueChanged += (_, e) => count.Text = Summary(e.NewValue);
        return GalleryUi.Column(14, input, count);
    }

    private static string Summary(MentionValue value) => value.Mentions.Count switch
    {
        0 => "No mentions",
        1 => "1 mention: " + value.Mentions[0].Text,
        _ => $"{value.Mentions.Count} mentions: " + string.Join(", ", value.Mentions.Select(static m => m.Text))
    };

    private static Control Growth()
    {
        var input = Field("Notes", "Write a few lines. Shift+Enter is not needed here: Enter adds a line.");
        input.MinLines = 2;
        input.MaxLines = 4;
        return input;
    }

    private static Control PlacementTop()
    {
        var input = Field("Reply", "Suggestions open above. Try @ or #");
        input.SuggestionPlacement = MentionSuggestionPlacement.Top;
        return input;
    }

    private static Control Disabled()
    {
        var input = Field("Locked comment");
        input.IsEnabled = false;
        const string text = "Waiting on @Alex Rivera before this thread reopens.";
        var alex = text.IndexOf("@Alex Rivera", StringComparison.Ordinal);
        input.Value = new MentionValue(text, [new Mention(MentionKind.Person, "u_alex", "Alex Rivera", alex, alex + "@Alex Rivera".Length)]);
        return input;
    }

    private static Control Serialized()
    {
        var input = Field("Announcement", "Type a message with a few mentions");
        const string text = "Ship notes are up, @Priya Natarajan. Posting in #engineering next.";
        var priya = text.IndexOf("@Priya Natarajan", StringComparison.Ordinal);
        var engineering = text.IndexOf("#engineering", StringComparison.Ordinal);
        input.Value = new MentionValue(text,
        [
            new Mention(MentionKind.Person, "u_priya", "Priya Natarajan", priya, priya + "@Priya Natarajan".Length),
            new Mention(MentionKind.Channel, "c_engineering", "engineering", engineering, engineering + "#engineering".Length)
        ]);
        var output = GalleryUi.Mono(MentionSerializer.Serialize(input.Value));
        output.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        output.Width = 440;
        output.HorizontalAlignment = HorizontalAlignment.Center;
        input.ValueChanged += (_, e) => output.Text = MentionSerializer.Serialize(e.NewValue);
        return GalleryUi.Column(14, input, output);
    }
}

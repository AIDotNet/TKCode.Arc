using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ExpandingButtonGroupPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("Point at an action, or Tab in and use the arrow keys.", ArcDesignTokenKind.TextSecondary, 13);
        var message = new ExpandingButtonGroup
        {
            Label = "Message actions",
            Items =
            {
                new ExpandingButtonGroupItem { Label = "Reply", Icon = ArcIcons.Reply },
                new ExpandingButtonGroupItem { Label = "Send", Icon = ArcIcons.Send, DoneLabel = "Sent" },
                new ExpandingButtonGroupItem
                {
                    Label = "Archive",
                    Icon = ArcIcons.Archive,
                    DoneLabel = "Archived",
                    AsyncAction = async () =>
                    {
                        await Task.Delay(600);
                        return true;
                    }
                },
                new ExpandingButtonGroupItem { Label = "Save", Icon = ArcIcons.Bookmark, DoneLabel = "Saved" },
                new ExpandingButtonGroupItem { Label = "Delete", Icon = ArcIcons.Trash2, DoneLabel = "Deleted", Tone = ExpandingButtonGroupTone.Danger }
            }
        };
        message.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";

        var files = new ExpandingButtonGroup
        {
            Label = "File actions",
            Size = ControlSize.Small,
            Items =
            {
                new ExpandingButtonGroupItem { Label = "Download", Icon = ArcIcons.Download, DoneLabel = "Downloaded" },
                new ExpandingButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2, DoneLabel = "Link copied" },
                new ExpandingButtonGroupItem { Label = "Lock", Icon = ArcIcons.Lock, IsDisabled = true },
                new ExpandingButtonGroupItem { Label = "Delete", Icon = ArcIcons.Trash2, Tone = ExpandingButtonGroupTone.Danger, DoneLabel = "Deleted" }
            }
        };

        var declined = new ExpandingButtonGroupItem
        {
            Label = "Publish",
            Icon = ArcIcons.Upload,
            DoneLabel = "Published",
            AsyncAction = async () =>
            {
                await Task.Delay(500);
                return false;
            }
        };
        var resting = new ExpandingButtonGroupItem { Label = "Comment", Icon = ArcIcons.MessageCircle };
        var card = new ExpandingButtonGroup
        {
            Label = "Card actions",
            Items = { new ExpandingButtonGroupItem { Label = "Like", Icon = ArcIcons.Heart, DoneLabel = "Liked" }, resting, declined },
            DefaultExpandedItem = resting
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Message actions", "The action you point at or focus grows to show its label while its neighbours slide aside; actions with a done word confirm in place for 1.4s.", GalleryUi.Column(12, GalleryUi.Row(message), log)),
            GalleryUi.Card("Small", "28px actions with 14px icons. Lock is disabled: it still reveals its label but does nothing.", GalleryUi.Row(files)),
            GalleryUi.Card("Resting action and declined result", "DefaultExpandedItem picks the label shown at rest. Publish returns false, so it skips the confirmation.", GalleryUi.Row(card)));
    }
}

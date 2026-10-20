using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ConfirmMorphPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("State: Idle", ArcDesignTokenKind.TextSecondary, 13);
        var press = new ConfirmMorph
        {
            Label = "Delete project",
            Icon = ArcIcons.Trash2,
            ConfirmAction = () => Task.Delay(900),
            UndoAction = () => Task.Delay(600)
        };
        press.StateChanged += (_, e) => log.Text = $"State: {e.NewState}";

        var hold = new ConfirmMorph
        {
            Label = "Remove member",
            ConfirmLabel = "Remove",
            PendingLabel = "Removing",
            DoneLabel = "Removed",
            ConfirmMode = ConfirmMorphMode.Hold,
            ConfirmAction = () => Task.Delay(700)
        };

        var countdown = new ConfirmMorph
        {
            Label = "Discard draft",
            ConfirmLabel = "Discard",
            PendingLabel = "Discarding",
            DoneLabel = "Discarded",
            ConfirmMode = ConfirmMorphMode.Countdown,
            UndoAction = () => Task.Delay(500)
        };

        var neutral = new ConfirmMorph
        {
            Label = "Revoke access",
            Prompt = "Revoke 3 keys?",
            ConfirmLabel = "Revoke",
            PendingLabel = "Revoking",
            DoneLabel = "Revoked",
            Tone = ConfirmMorphTone.Neutral,
            ConfirmAction = () => Task.Delay(800)
        };

        var attempts = 0;
        var error = new ConfirmMorph
        {
            Label = "Delete 3 files",
            Icon = ArcIcons.Trash2,
            Prompt = "Delete 3 files?",
            ConfirmAction = async () =>
            {
                await Task.Delay(800);
                if (++attempts % 2 == 1)
                {
                    throw new InvalidOperationException("Offline");
                }
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Press", "The pill morphs into the question, then a spinner, then the result with Undo. Escape, an outside press or 6s without an answer return it to rest; the result leaves after 5s (resting the pointer holds both).", GalleryUi.Column(12, GalleryUi.Row(press), log)),
            GalleryUi.Card("Hold", "Hold Confirm (pointer, Space or Enter) for 1s; letting go early rewinds the fill.", GalleryUi.Row(hold)),
            GalleryUi.Card("Countdown", "Confirm starts a 3 second countdown that Cancel or Escape can still stop; Enter runs it at once.", GalleryUi.Row(countdown)),
            GalleryUi.Card("Neutral tone", "For important actions that can be reversed, the answer takes the accent.", GalleryUi.Row(neutral)),
            GalleryUi.Card("Error and retry", "Every other attempt fails here: the error face offers Retry.", GalleryUi.Row(error)),
            GalleryUi.Card("Disabled", "The resting pill drops its shadow.", GalleryUi.Row(new ConfirmMorph { Label = "Delete project", Icon = ArcIcons.Trash2, IsEnabled = false })));
    }
}

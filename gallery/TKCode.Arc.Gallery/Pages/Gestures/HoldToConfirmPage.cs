using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class HoldToConfirmPage
{
    public static Control Create()
    {
        var hint = GalleryUi.Text("Press and hold, or focus it and hold Space or Enter.", ArcDesignTokenKind.TextSecondary, 13);
        var project = new HoldToConfirm { Label = "Hold to delete project", ConfirmedLabel = "Deleted" };
        project.HoldingChanged += (_, e) => hint.Text = e.IsHolding ? "Keep holding…" : project.IsConfirmed ? "Deleted." : "Let go early and the fill rewinds.";
        var reset = new Button { Content = "Reset", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        reset.Click += (_, _) => project.IsConfirmed = false;

        var tones = GalleryUi.Row(
            new HoldToConfirm { Label = "Hold to delete", Tone = HoldToConfirmTone.Danger, ConfirmedLabel = "Deleted" },
            new HoldToConfirm { Label = "Hold to archive", Tone = HoldToConfirmTone.Neutral, Icon = ArcIcons.Archive, ConfirmedLabel = "Archived" },
            new HoldToConfirm { Label = "Hold to revoke", HoldDuration = TimeSpan.FromSeconds(2), Icon = ArcIcons.Key, ConfirmedLabel = "Revoked" });

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "The fill follows the hold on a linear timeline; the label inverts exactly at its edge. Finishing morphs the label and draws a check.", GalleryUi.Column(12, GalleryUi.Row(project, reset), hint)),
            GalleryUi.Card("Tones and durations", "Danger for irreversible actions, neutral fills with the foreground; the last one needs a 2 second hold.", tones),
            GalleryUi.Card("Disabled", "A disabled button cannot be held.", GalleryUi.Row(new HoldToConfirm { Label = "Hold to delete", IsEnabled = false })));
    }
}

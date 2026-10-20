using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class MultiRegionFailoverPage
{
    public static Control Create()
    {
        var automatic = new MultiRegionFailover();
        var manual = new MultiRegionFailover { IsAutomatic = false };
        var primary = GalleryUi.Text("Primary: us-east-1", ArcDesignTokenKind.TextMuted, 13);
        manual.PrimaryRegionChanged += (_, index) => primary.Text = $"Primary: {MultiRegionFailover.DefaultRegions[index]}";

        Button Action(string text, Action act, ButtonVariant variant = ButtonVariant.Secondary)
        {
            var button = new Button { Content = text, Variant = variant, Size = ControlSize.Small };
            button.Click += (_, _) => act();
            return button;
        }

        var controls = GalleryUi.Row(
            Action("Fail us-east-1", () => manual.FailRegion(0), ButtonVariant.Danger),
            Action("Recover us-east-1", () => manual.RecoverRegion(0)),
            Action("Fail eu-west-1", () => manual.FailRegion(1), ButtonVariant.Danger),
            Action("Recover eu-west-1", () => manual.RecoverRegion(1)),
            primary);

        return GalleryUi.Column(20,
            GalleryUi.Card("Automatic failover",
                "us-east-1 fails its health checks, the load balancer shifts traffic to eu-west-1 and promotes its database, us-east-1 recovers as a replica, then a planned failback.",
                GalleryUi.Column(12, automatic, IllustrationSamples.SpeedRow(automatic))),
            GalleryUi.Card("Drive it yourself",
                "IsAutomatic = false: FailRegion and RecoverRegion run the same state machine; PrimaryRegionChanged reports promotions.",
                GalleryUi.Column(12, manual, controls)),
            GalleryUi.Card("Two regions", "Regions takes two or three names.",
                new MultiRegionFailover { Regions = ["primary-fra", "standby-ams"] }),
            GalleryUi.Card("Reduced motion", "The healthy deployment as a still frame.",
                IllustrationSamples.Reduced(new MultiRegionFailover())));
    }
}

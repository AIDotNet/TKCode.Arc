using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ExpandableCardPage
{
    public static Control Create()
    {
        var plan = new ExpandableCard
        {
            Header = "Pro plan",
            Description = "$24 per month · billed yearly",
            CollapsedWidth = 420,
            Content = GalleryUi.Column(8,
                GalleryUi.Text("Unlimited projects and collaborators.", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Version history for a full year.", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Priority support with a four-hour response time.", ArcDesignTokenKind.TextSecondary))
        };

        var order = new ExpandableCard
        {
            Header = "Order #4821",
            Description = "Shipped · arrives Thursday",
            CollapsedWidth = 360,
            ExpandedWidth = 520,
            Content = GalleryUi.Column(8,
                GalleryUi.Text("2 × Linen shirt, 1 × Canvas tote", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Carrier: Northwind Express · tracking NW-22-9183", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Delivery window 9:00–13:00", ArcDesignTokenKind.TextSecondary))
        };
        var statuses = new[] { "Shipped · arrives Thursday", "Out for delivery · arrives today", "Delivered · left at the front door" };
        var step = 0;
        var advance = new Button { Content = "Advance status", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        advance.Click += (_, _) =>
        {
            step = (step + 1) % statuses.Length;
            order.Description = statuses[step];
        };

        var log = new ExpandableCard
        {
            Header = "Deploy failed",
            Description = "api-gateway · 2 minutes ago",
            IsExpanded = true,
            Content = GalleryUi.Text("Health check on /ready timed out after 30s. The previous release is still serving traffic; no action is needed until the next deploy.", ArcDesignTokenKind.TextSecondary)
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Expandable card", "The header opens the details: width and height grow on one spring and the chevron turns with it; the details fade in once the box has made room. Escape closes it from inside.", plan),
            GalleryUi.Card("Growing sideways", "ExpandedWidth lets the card grow wider while it opens. Changed words in the summary roll in; unchanged words hold still.",
                GalleryUi.Column(12, order, GalleryUi.Row(advance))),
            GalleryUi.Card("Open by default", "IsExpanded is two-way; the card fills the container when no width is given.", log));
    }
}

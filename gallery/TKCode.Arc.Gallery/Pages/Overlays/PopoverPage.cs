using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using AtomTextBox = AtomUI.Desktop.Controls.TextBox;

namespace TKCode.Arc.Gallery.Pages;

public static class PopoverPage
{
    public static Control Create()
    {
        var share = new Popover
        {
            Trigger = new Button { Content = "Share", Variant = ButtonVariant.Secondary },
            Content = GalleryUi.Column(12,
                GalleryUi.Text("Share link", ArcDesignTokenKind.Foreground, 14, Avalonia.Media.FontWeight.Medium),
                GalleryUi.Text("Anyone with the link can view.", ArcDesignTokenKind.TextSecondary),
                new CopyButton { Value = "https://uiarc.dev/p/atlas" })
        };
        Avalonia.Automation.AutomationProperties.SetName(share, "Share link");

        Button Placed(string label, PopoverPlacement placement, PopoverAlignment alignment = PopoverAlignment.Start)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary };
            Popover.SetAttach(button, new Popover { Placement = placement, Alignment = alignment, Content = $"Opens on the {label.ToLowerInvariant()} side." });
            return button;
        }

        var filter = new Popover
        {
            Trigger = new Button { Content = "Filter", Variant = ButtonVariant.Ghost },
            Content = GalleryUi.Column(12, GalleryUi.Text("Status", ArcDesignTokenKind.TextSecondary), new AtomTextBox { PlaceholderText = "Any" },
                new Button { Content = "Apply" })
        };

        var anchor = new Border { Width = 120, Height = 36, CornerRadius = new Avalonia.CornerRadius(18), BorderThickness = new Avalonia.Thickness(1) };
        ArcTokens.Bind(anchor, Border.BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        var codePopover = new Popover { Content = "Opened from code with ShowAt(anchor).", Alignment = PopoverAlignment.Center };
        var show = new Button { Content = "Show at the box", Variant = ButtonVariant.Secondary };
        show.Click += (_, _) => codePopover.ShowAt(anchor);

        return GalleryUi.Column(20,
            GalleryUi.Card("Share", "A click opens it, focus moves in, Escape or a press outside closes it and returns focus. The trigger answers presses with color only.", GalleryUi.Row(share)),
            GalleryUi.Card("Sides", "Attached to any control with arc:Popover.Attach. The panel starts 5px toward its trigger and settles on a spring from the side it lands on.",
                GalleryUi.Row(Placed("Top", PopoverPlacement.Top), Placed("Bottom", PopoverPlacement.Bottom), Placed("Left", PopoverPlacement.Left),
                    Placed("Right", PopoverPlacement.Right), Placed("Center", PopoverPlacement.Bottom, PopoverAlignment.Center))),
            GalleryUi.Card("Small form", null, GalleryUi.Row(filter)),
            GalleryUi.Card("From code", "Other controls reuse the same surface anchored to anything.", GalleryUi.Row(show, anchor)));
    }
}

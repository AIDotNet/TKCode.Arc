using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class FloatingButtonGroupPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("Point at the tray, or Tab into it and use the arrow keys.", ArcDesignTokenKind.TextSecondary, 13);
        var share = new FloatingButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2, ReserveLabels = ["Copied"] };
        share.Click += async (_, _) =>
        {
            share.Label = "Copied";
            await Task.Delay(1600);
            share.Label = "Share";
        };
        var present = new FloatingButtonGroupItem { Label = "Present", Icon = ArcIcons.Monitor, IsChecked = false };
        present.Click += (_, _) => present.IsChecked = present.IsChecked != true;
        var board = new FloatingButtonGroup
        {
            Label = "Board actions",
            Items =
            {
                new FloatingButtonGroupItem { Label = "Undo", Icon = ArcIcons.Undo2, IsIconOnly = true, Shortcut = "⌘Z" },
                new FloatingButtonGroupItem { Label = "Redo", Icon = ArcIcons.Redo2, IsIconOnly = true, Shortcut = "⇧⌘Z" },
                new FloatingButtonGroupSeparator(),
                share,
                present
            }
        };
        board.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";

        var selection = new FloatingButtonGroup
        {
            Label = "Text formatting",
            Variant = FloatingButtonGroupVariant.Floating,
            IsIconOnly = true,
            Items =
            {
                new FloatingButtonGroupItem { Label = "Bold", Icon = ArcIcons.Bold, Shortcut = "⌘B" },
                new FloatingButtonGroupItem { Label = "Italic", Icon = ArcIcons.Italic, Shortcut = "⌘I" },
                new FloatingButtonGroupItem { Label = "Underline", Icon = ArcIcons.Underline, Shortcut = "⌘U" },
                new FloatingButtonGroupItem { Label = "Strikethrough", Icon = ArcIcons.Strikethrough },
                new FloatingButtonGroupSeparator(),
                new FloatingButtonGroupItem { Label = "Link", Icon = ArcIcons.Link, Shortcut = "⌘K" },
                new FloatingButtonGroupItem { Label = "Quote", Icon = ArcIcons.Quote, IsEnabled = false }
            }
        };

        var small = new FloatingButtonGroup
        {
            Label = "Zoom",
            Size = ControlSize.Small,
            ItemsSource = new[] { "Fit", "50%", "100%", "200%" }
        };

        var rail = new FloatingButtonGroup
        {
            Label = "Tools",
            Orientation = Orientation.Vertical,
            IsIconOnly = true,
            Items =
            {
                new FloatingButtonGroupItem { Label = "Select", Icon = ArcIcons.Move, Shortcut = "V" },
                new FloatingButtonGroupItem { Label = "Pen", Icon = ArcIcons.Pencil, Shortcut = "P" },
                new FloatingButtonGroupItem { Label = "Highlighter", Icon = ArcIcons.Highlighter },
                new FloatingButtonGroupSeparator(),
                new FloatingButtonGroupItem { Label = "Eraser", Icon = ArcIcons.Eraser, Shortcut = "E" }
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Muted tray", "One shared highlight morphs between buttons and holds still across gaps and separators. Share reserves the width of Copied; Present stays tinted while on.", GalleryUi.Column(12, GalleryUi.Row(board), log)),
            GalleryUi.Card("Floating, icon only", "For overlays such as a selection toolbar: labels and shortcuts move into tooltips. Quote is disabled and skipped by the arrow keys.", GalleryUi.Row(selection)),
            GalleryUi.Card("Small, from ItemsSource", "30px buttons; plain data items become buttons.", GalleryUi.Row(small)),
            GalleryUi.Card("Vertical", "A column uses Up and Down, and its tooltips open to the right.", GalleryUi.Row(rail)));
    }
}

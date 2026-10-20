using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ButtonGroupPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("Point at the group, or Tab into it and use the arrow keys.", ArcDesignTokenKind.TextSecondary, 13);
        var share = new ButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2, ReserveLabels = ["Copied"] };
        var copies = 0;
        share.Click += async (_, _) =>
        {
            var current = ++copies;
            share.Label = "Copied";
            share.Icon = ArcIcons.Check;
            await Task.Delay(1600);
            if (current == copies)
            {
                share.Label = "Share";
                share.Icon = ArcIcons.Share2;
            }
        };
        var document = new ButtonGroup
        {
            Label = "Document actions",
            Items =
            {
                share,
                new ButtonGroupItem { Label = "Duplicate", Icon = ArcIcons.Copy },
                new ButtonGroupItem { Label = "Archive", Icon = ArcIcons.Archive }
            },
            Menu = new MenuSurface
            {
                Items =
                {
                    new MenuEntry { Header = "Pin", Icon = ArcIcons.Bookmark },
                    new MenuEntry { Header = "Move to…", Icon = ArcIcons.Folder },
                    new MenuEntry { Header = "Delete", Icon = ArcIcons.Trash2, IsDestructive = true, HasSeparatorBefore = true }
                }
            }
        };
        document.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
        document.MenuItemClicked += (_, e) => log.Text = $"Menu: {e.Header}";

        var reply = new ButtonGroup
        {
            Label = "Message actions",
            Variant = ButtonGroupVariant.Solid,
            Items =
            {
                new ButtonGroupItem { Label = "Reply", Icon = ArcIcons.Reply },
                new ButtonGroupItem { Label = "Reply all", Icon = ReplyAll },
                new ButtonGroupItem { Label = "Forward", Icon = Forward }
            }
        };

        var zoomValue = new ButtonGroupItem { Label = "Reset zoom", Value = "100%" };
        var zoom = 100;
        void SetZoom(int value)
        {
            zoom = Math.Clamp(value, 25, 400);
            zoomValue.Value = $"{zoom}%";
        }
        var zoomOut = new ButtonGroupItem { Label = "Zoom out", Icon = ArcIcons.ZoomOut, IsIconOnly = true };
        var zoomIn = new ButtonGroupItem { Label = "Zoom in", Icon = ArcIcons.ZoomIn, IsIconOnly = true };
        zoomOut.Click += (_, _) => SetZoom(zoom - 25);
        zoomIn.Click += (_, _) => SetZoom(zoom + 25);
        zoomValue.Click += (_, _) => SetZoom(100);
        var zoomGroup = new ButtonGroup
        {
            Label = "Zoom",
            Size = ControlSize.Small,
            Items = { zoomOut, zoomValue, zoomIn }
        };

        var vertical = new ButtonGroup
        {
            Label = "Insert",
            Orientation = Orientation.Vertical,
            Items =
            {
                new ButtonGroupItem { Label = "Image", Icon = ArcIcons.Image },
                new ButtonGroupItem { Label = "Table", Icon = ArcIcons.Table },
                new ButtonGroupItem { Label = "Code block", Icon = ArcIcons.Code }
            },
            Menu = new MenuSurface
            {
                Items =
                {
                    new MenuEntry { Header = "Divider" },
                    new MenuEntry { Header = "Callout" }
                }
            },
            MenuLabel = "More blocks"
        };

        var collapsing = new ButtonGroup
        {
            Label = "Editing",
            Items =
            {
                new ButtonGroupItem { Label = "Undo", Icon = ArcIcons.Undo2 },
                new ButtonGroupItem { Label = "Redo", Icon = ArcIcons.Redo2 },
                new ButtonGroupItem { Label = "Download", Icon = ArcIcons.Download },
                new ButtonGroupItem { Label = "Settings", Icon = ArcIcons.Settings }
            }
        };
        var frame = new Border { Width = 240, Child = collapsing, HorizontalAlignment = HorizontalAlignment.Left };
        var narrow = new Button { Content = "Narrow", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        var wide = new Button { Content = "Wide", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        narrow.Click += (_, _) => frame.Width = 240;
        wide.Click += (_, _) => frame.Width = 520;

        var disabled = new ButtonGroup
        {
            Label = "Publishing",
            Items =
            {
                new ButtonGroupItem { Label = "Preview", Icon = ArcIcons.Eye },
                new ButtonGroupItem { Label = "Publish", Icon = ArcIcons.Upload, IsDisabled = true },
                new ButtonGroupItem { Label = "Schedule", Icon = ArcIcons.Clock }
            }
        };
        var allDisabled = new ButtonGroup
        {
            Label = "Formatting",
            IsEnabled = false,
            Items =
            {
                new ButtonGroupItem { Label = "Bold", Icon = ArcIcons.Bold, IsIconOnly = true },
                new ButtonGroupItem { Label = "Italic", Icon = ArcIcons.Italic, IsIconOnly = true },
                new ButtonGroupItem { Label = "Underline", Icon = ArcIcons.Underline, IsIconOnly = true }
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Document actions",
                "Related actions on one surface with hairline dividers. One highlight glides between segments and the dividers beside it step aside; the pressed segment dips in place. Share reserves the width of Copied, and the chevron opens rarer and destructive actions aligned to the group's end.",
                GalleryUi.Column(12, GalleryUi.Row(document), log)),
            GalleryUi.Card("Solid", "The primary fill for a screen's main action row. Use it at most once per screen.", GalleryUi.Row(reply)),
            GalleryUi.Card("Small, with a live value",
                "Icon-only segments carry their label as the tooltip and name; the middle segment shows the zoom level and resets it.",
                GalleryUi.Row(zoomGroup)),
            GalleryUi.Card("Vertical", "A column uses Up and Down; its menu aligns to the start.", GalleryUi.Row(vertical)),
            GalleryUi.Card("Collapsing labels",
                "When the row does not fit, segments drop their labels and keep the icon; they come back once there is room for the full row.",
                GalleryUi.Column(12, GalleryUi.Row(narrow, wide), frame)),
            GalleryUi.Card("Disabled",
                "A disabled segment stays focusable and reports unavailable; a disabled group dims as a whole.",
                GalleryUi.Row(disabled, allDisabled)));
    }

    /// <summary>Lucide "reply-all".</summary>
    private static readonly Avalonia.Media.Geometry ReplyAll = ArcIcons.Parse("M7 17l-5-5 5-5 M12 17l-5-5 5-5 M22 18v-2a4 4 0 0 0-4-4H7");

    /// <summary>Lucide "forward".</summary>
    private static readonly Avalonia.Media.Geometry Forward = ArcIcons.Parse("M15 17l5-5-5-5 M4 18v-2a4 4 0 0 1 4-4h12");
}

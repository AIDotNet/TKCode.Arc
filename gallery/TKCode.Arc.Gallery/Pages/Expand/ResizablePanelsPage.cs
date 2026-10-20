using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ResizablePanelsPage
{
    private static Control Pane(string title, string body, ArcDesignTokenKind background)
    {
        var border = new Border { Padding = new Thickness(16) };
        ArcTokens.Bind(border, Border.BackgroundProperty, background);
        border.Child = GalleryUi.Column(6, GalleryUi.Text(title, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium), GalleryUi.Text(body, ArcDesignTokenKind.TextSecondary, 13));
        return border;
    }

    private static Border Frame(Control content)
    {
        var frame = new Border { Height = 320, BorderThickness = new Thickness(1), Padding = new Thickness(1), CornerRadius = new CornerRadius(26), ClipToBounds = true, Child = content };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return frame;
    }

    public static Control Create()
    {
        var status = GalleryUi.Text("Drag a divider", ArcDesignTokenKind.TextMuted, 12);
        var mail = new ResizablePanels { Label = "Mail layout" };
        mail.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 22, MinSize = 140, IsCollapsible = true, Content = Pane("Folders", "Inbox, Drafts, Sent, Archive", ArcDesignTokenKind.SurfaceMuted) });
        mail.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 33, MinSize = 200, Content = Pane("Messages", "42 conversations", ArcDesignTokenKind.Surface) });
        mail.Panels.Add(new ResizablePanel { Id = "reading", Label = "Reading pane", DefaultSize = 45, MinSize = 220, IsCollapsible = true, Content = Pane("Quarterly review", "Hi team, the numbers are in and they look good. Let's walk through them on Thursday.", ArcDesignTokenKind.Surface) });
        mail.LayoutChanged += (_, e) => status.Text = "Layout: " + string.Join(" / ", e.Sizes.Select(s => $"{s:0}%"));

        var editor = new ResizablePanels { Label = "Editor layout" };
        editor.Panels.Add(new ResizablePanel { Id = "files", Label = "Files", DefaultSize = 25, MinSize = 160, IsCollapsible = true, IsCollapsed = true, Content = Pane("Files", "src / components / panels.tsx", ArcDesignTokenKind.SurfaceMuted) });
        editor.Panels.Add(new ResizablePanel { Id = "code", Label = "Code", DefaultSize = 50, MinSize = 240, Content = Pane("panels.tsx", "export function ResizablePanels() { … }", ArcDesignTokenKind.Surface) });
        editor.Panels.Add(new ResizablePanel { Id = "preview", Label = "Preview", DefaultSize = 25, MinSize = 160, IsCollapsible = true, Content = Pane("Preview", "Live preview of the component", ArcDesignTokenKind.SurfaceMuted) });

        IReadOnlyList<double>? saved = null;
        var save = new Button { Content = "Save layout", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        var restore = new Button { Content = "Restore saved", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        save.Click += (_, _) => saved = editor.Sizes;
        restore.Click += (_, _) =>
        {
            if (saved is not null)
            {
                editor.Sizes = saved;
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Resizable panels", "Dividers follow the pointer 1:1, resist past each pane's minimum and snap collapsible panes closed when dragged well past it or flicked toward the edge; release settles on a spring that keeps the pointer's speed. Focus a divider: arrows resize (Shift for big steps), Home/End jump to the limits, Enter hides or restores a pane, Escape cancels a drag, double-click resets.",
                GalleryUi.Column(12, Frame(mail), status)),
            GalleryUi.Card("Collapsed by default and persisted", "Files starts hidden; press its restore tab to bring it back. Sizes is two-way, so a layout can be saved and restored.",
                GalleryUi.Column(12, Frame(editor), GalleryUi.Row(save, restore))));
    }
}

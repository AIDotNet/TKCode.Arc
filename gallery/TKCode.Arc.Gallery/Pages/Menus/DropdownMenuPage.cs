using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc.Gallery.Pages;

public static class DropdownMenuPage
{
    public static Control Create()
    {
        // Arc's usage example: Rename, Duplicate, and Delete last behind a separator.
        var status = GalleryUi.Text("Pick an action.", ArcDesignTokenKind.TextSecondary);
        var actions = new DropdownMenu { Label = "Actions" };
        actions.Items.Add(new MenuEntry { Header = "Rename", Icon = ArcIcons.Pencil });
        actions.Items.Add(new MenuEntry { Header = "Duplicate", Icon = ArcIcons.Copy });
        actions.Items.Add(new MenuEntry { Header = "Delete", Icon = ArcIcons.Trash2, IsDestructive = true, HasSeparatorBefore = true });
        actions.ItemClicked += (_, e) => status.Text = $"Selected: {e.Header}";

        var withIcon = new DropdownMenu { Label = "Share", Icon = ArcIcons.Share2 };
        withIcon.Items.Add(new MenuEntry { Header = "Copy link", Icon = ArcIcons.Link });
        withIcon.Items.Add(new MenuEntry { Header = "Email", Icon = ArcIcons.Mail });
        withIcon.Items.Add(new MenuEntry { Header = "Export PDF", Icon = ArcIcons.Download, IsEnabled = false });
        withIcon.Items.Add(new MenuEntry { Header = "Embed", Icon = ArcIcons.Code });

        // A new label rises in while the trigger width springs.
        var sort = new DropdownMenu { Label = "Sort: Newest", Icon = ArcIcons.ArrowUpDown, Alignment = MenuAlignment.Start };
        sort.Items.Add(new MenuEntryLabel { Text = "Sort by" });
        foreach (var (name, check) in new[] { ("Newest", true), ("Oldest", false), ("Name", false), ("Size", false) })
        {
            var row = new MenuEntry { Header = name, ToggleType = MenuEntryToggleType.Radio, GroupName = "sort", IsChecked = check };
            row.Click += (_, _) => sort.Label = $"Sort: {name}";
            sort.Items.Add(row);
        }

        var view = new DropdownMenu { Label = "View", Icon = ArcIcons.LayoutGrid };
        view.Items.Add(new MenuEntry { Header = "Show sidebar", ToggleType = MenuEntryToggleType.CheckBox, IsChecked = true, StaysOpenOnClick = true, InputGesture = KeyGesture.Parse("Ctrl+B") });
        view.Items.Add(new MenuEntry { Header = "Show minimap", ToggleType = MenuEntryToggleType.CheckBox, StaysOpenOnClick = true, InputGesture = KeyGesture.Parse("Ctrl+M") });
        view.Items.Add(new MenuEntry { Header = "Wrap lines", ToggleType = MenuEntryToggleType.CheckBox, IsChecked = true, StaysOpenOnClick = true });
        view.Items.Add(new MenuEntrySeparator());
        view.Items.Add(new MenuEntryLabel { Text = "Density" });
        view.Items.Add(new MenuEntry { Header = "Comfortable", ToggleType = MenuEntryToggleType.Radio, GroupName = "density", IsChecked = true });
        view.Items.Add(new MenuEntry { Header = "Compact", ToggleType = MenuEntryToggleType.Radio, GroupName = "density" });

        // Any trigger: an icon button opens a menu from data.
        var more = new DropdownMenu
        {
            Trigger = new Button { Content = new ArcGlyph { Data = ArcIcons.Ellipsis }, Variant = ButtonVariant.Ghost, Size = ControlSize.Small },
            ItemsSource = new[] { "Move to folder", "Make a copy", "Version history" }
        };
        more.ItemClicked += (_, e) => status.Text = $"Selected: {e.Item}";

        // The shared surface on its own: anchor it to any control (split buttons, button groups, swipe rows).
        var anchor = new Button { Content = "Open a MenuSurface", Variant = ButtonVariant.Secondary, IsPressScaleEnabled = false };
        var surface = new MenuSurface { Alignment = MenuAlignment.Start, AnchorGap = 4 };
        surface.Items.Add(new MenuEntry { Header = "Save as draft", Icon = ArcIcons.FileText, InputGestureText = "⌘S" });
        surface.Items.Add(new MenuEntry { Header = "Schedule", Icon = ArcIcons.Clock });
        surface.Items.Add(new MenuEntry { Header = "Discard", Icon = ArcIcons.Trash2, IsDestructive = true, HasSeparatorBefore = true });
        surface.ItemClicked += (_, e) => status.Text = $"Surface: {e.Header}";
        anchor.Click += (_, _) =>
        {
            if (surface.IsOpen)
            {
                surface.Close(returnFocus: false);
            }
            else
            {
                surface.Open(anchor);
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Actions", "A labelled trigger; the menu grows from its edge and one highlight glides between rows. Enter, Space or ArrowDown open it on the first row; Escape returns focus.",
                GalleryUi.Column(12, GalleryUi.Row(actions, withIcon), status)),
            GalleryUi.Card("Live label, groups and toggles", "Choosing a sort morphs the trigger label. Check rows stay open; radio rows close. Typeahead jumps to rows by their first letters.",
                GalleryUi.Row(sort, view)),
            GalleryUi.Card("Custom trigger and data", "Any control can be the trigger (buttons never press-scale while they anchor a menu); rows can come from ItemsSource.",
                GalleryUi.Row(more)),
            GalleryUi.Card("MenuSurface", "The reusable surface behind every Arc menu, opened from code against any anchor.",
                GalleryUi.Row(anchor)));
    }
}

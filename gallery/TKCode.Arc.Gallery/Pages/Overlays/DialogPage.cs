using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using AtomTextBox = AtomUI.Desktop.Controls.TextBox;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class DialogPage
{
    public static Control Create()
    {
        return GalleryUi.Column(20, Rename(), Delete(), Steps(), Sizes());
    }

    private static Control Rename()
    {
        var name = new AtomTextBox { Text = "Atlas redesign" };
        var status = GalleryUi.Text("Project: Atlas redesign", ArcDesignTokenKind.TextSecondary);
        var save = new Button { Content = "Save" };
        var cancel = new Button { Content = "Cancel", Variant = ButtonVariant.Secondary };
        Dialog.SetResult(save, true);
        Dialog.SetResult(cancel, false);
        var dialog = new Dialog
        {
            Title = "Rename project",
            Description = "Choose a short name your team will recognize.",
            Content = name,
            Actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, save } }
        };
        dialog.Closed += (_, e) =>
        {
            if (e.Result is true)
            {
                status.Text = $"Project: {name.Text}";
            }
        };
        var open = new Button { Content = "Rename project", Variant = ButtonVariant.Secondary, Command = dialog.OpenCommand };
        return GalleryUi.Card("Short form",
            "Declared next to its trigger; it renders nothing in place. Escape or a press on the blurred scrim closes it, Tab stays inside, focus returns to the button.",
            GalleryUi.Column(12, GalleryUi.Row(open, dialog), status));
    }

    private static Control Delete()
    {
        var status = GalleryUi.Text("Nothing deleted", ArcDesignTokenKind.TextSecondary);
        var delete = new Button { Content = "Delete project", Variant = ButtonVariant.Danger };
        delete.Click += async (_, _) =>
        {
            var confirmed = await Dialog.ConfirmAsync(delete, "Delete project?",
                "This permanently deletes Atlas redesign and its 14 files. This cannot be undone.", "Delete", isDanger: true);
            status.Text = confirmed ? "Deleted" : "Kept";
        };
        return GalleryUi.Card("Confirmation from code", "Dialog.ConfirmAsync builds the dialog and returns true only for the destructive action.",
            GalleryUi.Column(12, GalleryUi.Row(delete), status));
    }

    private static Control Steps()
    {
        string[] titles = ["Invite your team", "Choose a role", "Send invites"];
        string[] descriptions = ["Add people by email.", "Editors can change content; viewers only read.", "We will email everyone a link."];
        var step = 0;
        var next = new Button { Content = "Next" };
        var dialog = new Dialog
        {
            Title = titles[0],
            Description = descriptions[0],
            Content = GalleryUi.Text("Step 1 of 3", ArcDesignTokenKind.TextMuted),
            Actions = next
        };
        next.Click += (_, _) =>
        {
            step++;
            if (step >= titles.Length)
            {
                dialog.Close(true);
                return;
            }
            dialog.Title = titles[step];
            dialog.Description = descriptions[step];
            dialog.Content = GalleryUi.Text($"Step {step + 1} of 3", ArcDesignTokenKind.TextMuted);
            next.Content = step == titles.Length - 1 ? "Send" : "Next";
        };
        dialog.Opened += (_, _) =>
        {
            step = 0;
            dialog.Title = titles[0];
            dialog.Description = descriptions[0];
            dialog.Content = GalleryUi.Text("Step 1 of 3", ArcDesignTokenKind.TextMuted);
            next.Content = "Next";
        };
        var open = new Button { Content = "Invite people", Variant = ButtonVariant.Secondary, Command = dialog.OpenCommand };
        return GalleryUi.Card("Steps", "The title and description cross-fade in place: the new copy rises in with a soft blur.", GalleryUi.Row(open, dialog));
    }

    private static Control Sizes()
    {
        var dialog = new Dialog
        {
            Title = "Workspace limits",
            Description = "Panel widths: Small 360, Medium 440 (Arc), Large 560.",
            Content = GalleryUi.Text("Every size stays within the window minus a 16px gutter and scrolls when taller than the window.")
        };
        Button Open(string label, ControlSize size)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary };
            button.Click += (_, _) =>
            {
                dialog.Size = size;
                dialog.IsOpen = true;
            };
            return button;
        }

        return GalleryUi.Card("Sizes", null, GalleryUi.Row(Open("Small", ControlSize.Small), Open("Medium", ControlSize.Medium), Open("Large", ControlSize.Large), dialog));
    }
}

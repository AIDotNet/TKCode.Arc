using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class SplitButtonPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("Run the main action, or open the chevron for the alternatives.", ArcDesignTokenKind.TextSecondary, 13);
        var merge = new SplitButton
        {
            Label = "Merge pull request",
            Icon = GitMerge,
            Items =
            {
                new MenuEntry { Header = "Squash and merge" },
                new MenuEntry { Header = "Rebase and merge" },
                new MenuEntry { Header = "Close pull request", Icon = ArcIcons.CircleX, IsDestructive = true, HasSeparatorBefore = true }
            }
        };
        merge.Click += (_, _) => log.Text = "Merged with a merge commit.";
        merge.ItemClicked += (_, e) => log.Text = $"Menu: {e.Header}";

        // Copy page answers Copied in place: the shared "Cop" stays put while the rest of the label morphs.
        var copy = new SplitButton
        {
            Label = "Copy page",
            Icon = ArcIcons.Copy,
            Items =
            {
                new MenuEntry { Header = "Copy as Markdown", Icon = ArcIcons.FileText },
                new MenuEntry { Header = "Copy link", Icon = ArcIcons.Link },
                new MenuEntry { Header = "View as plain text", Icon = ArcIcons.Eye }
            }
        };
        var copies = 0;
        copy.Click += async (_, _) =>
        {
            var current = ++copies;
            copy.Label = "Copied";
            copy.Icon = ArcIcons.Check;
            await Task.Delay(1600);
            if (current == copies)
            {
                copy.Label = "Copy page";
                copy.Icon = ArcIcons.Copy;
            }
        };

        var export = new SplitButton
        {
            Variant = SplitButtonVariant.Secondary,
            Label = "Export as CSV",
            Icon = ArcIcons.Download,
            Items =
            {
                new MenuEntry { Header = "Export as JSON" },
                new MenuEntry { Header = "Export as PDF" },
                new MenuEntry { Header = "Schedule export…", Icon = ArcIcons.Clock, HasSeparatorBefore = true }
            }
        };
        export.ItemClicked += (_, e) => export.Label = e.Header?.ToString() ?? export.Label;

        var disabled = new SplitButton
        {
            Label = "Deploy",
            IsEnabled = false,
            Items = { new MenuEntry { Header = "Deploy to staging" } }
        };
        var disabledSecondary = new SplitButton
        {
            Label = "Publish",
            Variant = SplitButtonVariant.Secondary,
            IsEnabled = false,
            Items = { new MenuEntry { Header = "Publish later" } }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Merge strategies",
                "One default action and its close variants. The whole pill presses in for the main action; the chevron half answers in color only, turns while open, and its menu opens 4px below, aligned to the end.",
                GalleryUi.Column(12, GalleryUi.Row(merge), log)),
            GalleryUi.Card("Result in place",
                "Copy page answers Copied: shared letters glide, changed ones rise out of a soft blur, the icon swaps and the main half springs to the new width. The change is announced politely.",
                GalleryUi.Row(copy)),
            GalleryUi.Card("Secondary",
                "A compact outlined pill with 12px text and a 142px main half. Picking a format makes it the default here.",
                GalleryUi.Row(export)),
            GalleryUi.Card("Disabled", "Disables both halves.", GalleryUi.Row(disabled, disabledSecondary)),
            GalleryUi.Card("Keyboard",
                "Tab to the main half and press Enter or Space to run it. On the chevron, Enter, Space or ArrowDown opens the menu on the first row; arrows loop, Escape closes and returns focus to the chevron.",
                GalleryUi.Text("Try it on the examples above.", ArcDesignTokenKind.TextSecondary, 13)));
    }

    /// <summary>Lucide "git-merge".</summary>
    private static readonly Avalonia.Media.Geometry GitMerge = ArcIcons.Parse("M18 15a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M6 3a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M6 21V9a9 9 0 0 0 9 9");
}

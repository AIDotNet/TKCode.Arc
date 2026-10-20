using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class RadioGroupPage
{
    public static Control Create()
    {
        var chosen = GalleryUi.Text("Selected: pro", ArcDesignTokenKind.TextMuted, 12);
        var plans = new RadioGroup
        {
            Header = "Plan",
            Width = 420,
            ItemsSource = new[]
            {
                new RadioOption("hobby", "Hobby", "For side projects and experiments."),
                new RadioOption("pro", "Pro", "For professionals who ship every week."),
                new RadioOption("team", "Team", "Shared workspaces, roles and audit logs for growing teams.")
            },
            SelectedValue = "pro"
        };
        plans.SelectionChanged += (_, _) => chosen.Text = $"Selected: {plans.SelectedValue}";

        var density = new RadioGroup { Header = "Density", Width = 420 };
        density.Items.Add(new RadioOption("comfortable", "Comfortable"));
        density.Items.Add(new RadioOption("compact", "Compact"));
        density.Items.Add(new RadioOption("spacious", "Spacious") { IsDisabled = true });
        density.SelectedValue = "comfortable";

        return GalleryUi.Column(20,
            GalleryUi.Card("Plans", "One highlight glides to the chosen row on the morph spring while the new dot springs in. Arrow keys take the same path; Tab enters and leaves at the selected row.",
                GalleryUi.Row(GalleryUi.Column(12, plans, chosen))),
            GalleryUi.Card("Inline options", "Options can be written inline; disabled options are skipped by the arrow keys.",
                GalleryUi.Row(density)));
    }
}

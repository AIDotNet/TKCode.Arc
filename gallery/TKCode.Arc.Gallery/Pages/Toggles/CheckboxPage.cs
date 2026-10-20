using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class CheckboxPage
{
    public static Control Create()
    {
        var terms = new Checkbox
        {
            Content = "Accept terms and conditions",
            Description = "You agree to our Terms of Service and Privacy Policy."
        };

        // A parent row that reflects its children: checked, unchecked or indeterminate (the check morphs into a dash).
        var children = new[]
        {
            new Checkbox { Content = "Email", IsChecked = true },
            new Checkbox { Content = "Push notifications" },
            new Checkbox { Content = "SMS" }
        };
        var all = new Checkbox { Content = "All channels" };
        var syncing = false;
        void FromChildren()
        {
            if (syncing)
            {
                return;
            }
            syncing = true;
            var on = children.Count(c => c.IsChecked == true);
            all.CheckedState = on == 0 ? CheckboxState.Unchecked : on == children.Length ? CheckboxState.Checked : CheckboxState.Indeterminate;
            syncing = false;
        }
        all.CheckedStateChanged += (_, e) =>
        {
            if (syncing || e.NewState == CheckboxState.Indeterminate)
            {
                return;
            }
            syncing = true;
            foreach (var child in children)
            {
                child.IsChecked = e.NewState == CheckboxState.Checked;
            }
            syncing = false;
        };
        foreach (var child in children)
        {
            child.CheckedStateChanged += (_, _) => FromChildren();
            child.Margin = new Thickness(28, 0, 0, 0);
        }
        FromChildren();
        var tree = GalleryUi.Column(0, all, children[0], children[1], children[2]);

        var state = GalleryUi.Text("State: Unchecked", ArcDesignTokenKind.TextMuted, 12);
        var cycle = new Checkbox { Content = "Three-state cycle", IsThreeState = true };
        cycle.CheckedStateChanged += (_, e) => state.Text = $"State: {e.NewState}";

        return GalleryUi.Column(20,
            GalleryUi.Card("Label and description", "The label's first line centers on the 18px square; the whole row is clickable. Space toggles.",
                GalleryUi.Row(terms)),
            GalleryUi.Card("Indeterminate parent", "The parent shows a dash while some children are checked; clicking it checks every child.",
                GalleryUi.Row(tree)),
            GalleryUi.Card("Tri-state API", "IsChecked is bool? (null is indeterminate) and CheckedState is the same value as an enum. IsThreeState opts into Avalonia's click cycle.",
                GalleryUi.Row(GalleryUi.Column(8, cycle, state))),
            GalleryUi.Card("States", "Disabled, invalid, and a box without a label (its accessible name falls back to \"Checkbox\").",
                GalleryUi.Row(
                    new Checkbox { Content = "Disabled", IsEnabled = false },
                    new Checkbox { Content = "Disabled checked", IsChecked = true, IsEnabled = false },
                    new Checkbox { Content = "Required", IsInvalid = true, IsRequired = true, Description = "Check this box to continue." },
                    new Checkbox())));
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CustomerGalaxyFocusedClusterDemo : UserControl
{
    public CustomerGalaxyFocusedClusterDemo()
    {
        InitializeComponent();
        foreach (var grouping in CustomerGalaxyDemoData.Groupings())
        {
            Galaxy.Groupings.Add(grouping);
        }
        var accounts = CustomerGalaxyDemoData.Accounts;
        Galaxy.ItemsSource = accounts;
        // The largest Corporate account (the accounts are sorted by MRR).
        Galaxy.SelectedAccount = accounts.First(a => a.Groups["plan"] == "corporate");
        Galaxy.GroupFocused += (_, e) => Show(e.Group is { } group ? $"Focused: {group.Label}" : "Showing every cluster");
        Galaxy.AccountSelected += (_, e) => Show(e.Account is { } account ? $"Selected: {account.Name}" : "No account selected");
        Show($"Focused: Corporate · Selected: {Galaxy.SelectedAccount?.Name}");
    }

    private void Show(string text) => Status.Text = text;
}

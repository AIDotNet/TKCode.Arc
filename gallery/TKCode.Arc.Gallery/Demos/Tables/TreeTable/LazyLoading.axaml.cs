using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeTableLazyLoadingDemo : UserControl
{
    private sealed record Account(string Id, string Name, int Seats, bool HasChildren = false);

    private int _salesAttempts;

    public TreeTableLazyLoadingDemo()
    {
        InitializeComponent();
        Table.KeySelector = item => ((Account)item).Id;
        // No ChildrenSelector: every child is loaded on demand.
        Table.HasChildrenSelector = item => ((Account)item).HasChildren;
        Table.ChildrenLoader = LoadAsync;
        Table.Columns.Add(TreeTableColumn.Create<Account>("Account", a => a.Name));
        Table.Columns.Add(new TreeTableColumn { Header = "Seats", ValueSelector = a => ((Account)a).Seats, Width = new GridLength(96), IsEndAligned = true });
        Table.ItemsSource = new[]
        {
            new Account("marketing", "Marketing", 42, HasChildren: true),
            new Account("sales", "Sales", 65, HasChildren: true),
            new Account("legal", "Legal", 8)
        };
    }

    private async Task<IEnumerable?> LoadAsync(object item)
    {
        var account = (Account)item;
        await Task.Delay(900);
        if (account.Id == "sales" && _salesAttempts++ == 0)
        {
            throw new InvalidOperationException("The sales service is not responding.");
        }
        return account.Id switch
        {
            "marketing" => new[] { new Account("brand", "Brand", 14), new Account("growth", "Growth", 19), new Account("events", "Events", 9) },
            _ => new[] { new Account("emea", "EMEA", 28), new Account("amer", "Americas", 37) }
        };
    }

    private void OnLoadFailed(object? sender, TreeTableRowEventArgs e) => Status.Text = $"Couldn't load {((Account)e.Item).Name}: {e.Error?.Message}";
}

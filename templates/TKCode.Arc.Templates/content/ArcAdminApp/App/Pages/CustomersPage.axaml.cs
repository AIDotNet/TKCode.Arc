using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using ArcStarterApp.Models;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Pages;

public partial class CustomersPage : UserControl
{
    public CustomersPage()
    {
        InitializeComponent();
        // Typed delegates read the cells: no reflection, so the app can be trimmed. Sorting sorts the rows on screen
        // (one page); sort on the server, or in the view model, to sort every customer.
        Table.Columns.Add(DataTableColumn.Create<Customer>("Name", c => c.Name));
        Table.Columns.Add(DataTableColumn.Create<Customer>("Company", c => c.Company));
        Table.Columns.Add(DataTableColumn.Create<Customer>("Plan", c => c.Plan));
        Table.Columns.Add(new DataTableColumn("Status", c => ((Customer)c).Status)
        {
            CellTemplate = new FuncDataTemplate<Customer>((customer, _) => customer is null ? null : new Badge
            {
                Content = StatusText(customer.Status),
                Tone = customer.Status switch
                {
                    CustomerStatus.Active => BadgeTone.Success,
                    CustomerStatus.Trial => BadgeTone.Info,
                    CustomerStatus.PastDue => BadgeTone.Warning,
                    _ => BadgeTone.Neutral,
                },
            }),
        });
        Table.Columns.Add(new DataTableColumn("MRR", c => ((Customer)c).MonthlyRevenue)
        {
            IsNumeric = true,
            Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : string.Empty,
        });
        Table.Columns.Add(DataTableColumn.Create<Customer>("Joined", c => c.Joined.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)));
        Table.Columns.Add(new DataTableColumn("", c => null)
        {
            IsSortable = false,
            CellTemplate = new FuncDataTemplate<Customer>((customer, _) => customer is null ? null : new TKCode.Arc.Button
            {
                Content = "View",
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Command = (DataContext as CustomersPageViewModel)?.OpenCommand,
                CommandParameter = customer,
            }),
        });
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is CustomersPageViewModel { IsLoading: true } vm)
        {
            await vm.LoadAsync();
        }
    }

    private static string StatusText(CustomerStatus status) => status switch
    {
        CustomerStatus.PastDue => "Past due",
        _ => status.ToString(),
    };
}

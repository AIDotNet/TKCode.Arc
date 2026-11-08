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
        Table.Columns.Add(Sized(DataTableColumn.Create<Customer>("Name", c => c.Name), 2));
        Table.Columns.Add(Sized(DataTableColumn.Create<Customer>("Company", c => c.Company), 2));
        Table.Columns.Add(Sized(DataTableColumn.Create<Customer>("Plan", c => c.Plan), 1.2));
        Table.Columns.Add(new DataTableColumn("Status", c => ((Customer)c).Status)
        {
            Width = new GridLength(1.2, GridUnitType.Star),
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
            Width = new GridLength(1, GridUnitType.Star),
            IsNumeric = true,
            Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : string.Empty,
        });
        Table.Columns.Add(Sized(DataTableColumn.Create<Customer>("Joined", c => c.Joined.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)), 1.4));
        Table.Columns.Add(new DataTableColumn("", c => null)
        {
            Width = new GridLength(80),
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

    // The columns share the table's width by weight, so the header and the rows line up on any window size.
    private static DataTableColumn Sized(DataTableColumn column, double weight)
    {
        column.Width = new GridLength(weight, GridUnitType.Star);
        return column;
    }

    private static string StatusText(CustomerStatus status) => status switch
    {
        CustomerStatus.PastDue => "Past due",
        _ => status.ToString(),
    };
}

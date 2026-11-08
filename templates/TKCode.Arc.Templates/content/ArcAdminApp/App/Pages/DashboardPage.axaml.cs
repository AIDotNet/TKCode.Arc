using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is not DashboardPageViewModel vm)
        {
            return;
        }
        await vm.LoadAsync();
        if (vm.Summary is { } summary)
        {
            // The chart's x values are the days; each series supplies one value per day.
            Revenue.ItemsSource = summary.Days.Select(day => day.ToString("MMM d", CultureInfo.InvariantCulture)).ToArray();
            Revenue.Series[0].Values = summary.Revenue;
            Revenue.Series[1].Values = summary.PreviousRevenue;
        }
    }
}

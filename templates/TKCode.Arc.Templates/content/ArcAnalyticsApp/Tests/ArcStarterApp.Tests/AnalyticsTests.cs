using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.Navigation;
using ArcStarterApp.Services;
using ArcStarterApp.ViewModels;
using ArcStarterApp.Views;

namespace ArcStarterApp.Tests;

public class AnalyticsTests
{
    [AvaloniaFact]
    public void Every_page_opens_and_shows_its_block()
    {
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        var host = window.GetVisualDescendants().OfType<PageHost>().Single();

        foreach (var page in AppPages.All)
        {
            viewModel.SelectedKey = page.Key;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(page.Key, host.Current?.Key);
            Assert.NotEmpty(((Control)host.Child!).GetVisualDescendants());
        }
    }

    [AvaloniaFact]
    public void The_sample_service_has_every_series()
    {
        var analytics = App.Services.GetRequiredService<IAnalyticsService>();

        Assert.NotEmpty(analytics.GetKpis());
        Assert.NotEmpty(analytics.GetRevenue());
        Assert.NotEmpty(analytics.GetRetention());
        Assert.NotEmpty(analytics.GetUsage());
        Assert.NotEmpty(analytics.GetServices());
    }
}

using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ArcStarterApp.Models;
using ArcStarterApp.Navigation;
using ArcStarterApp.Services;
using ArcStarterApp.ViewModels;
using ArcStarterApp.Views;

namespace ArcStarterApp.Tests;

public class AdminTests
{
    [AvaloniaFact]
    public void Every_page_opens_after_sign_in()
    {
        var viewModel = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        viewModel.SignIn(new Account("Test Person", "test@example.com", "Owner"));
        Dispatcher.UIThread.RunJobs();
        var host = window.GetVisualDescendants().OfType<PageHost>().Single();

        foreach (var page in AppPages.All)
        {
            viewModel.SelectedKey = page.Key;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(page.Key, host.Current?.Key);
            Assert.NotNull(host.Child);
        }
    }

    [AvaloniaFact]
    public async Task Sample_accounts_need_the_sample_password()
    {
        var auth = App.Services.GetRequiredService<IAuthService>();

        var account = await auth.FindAsync("ada@northwind.example", CancellationToken.None);

        Assert.NotNull(account);
        Assert.False(await auth.VerifyPasswordAsync(account, "wrong", CancellationToken.None));
        Assert.True(await auth.VerifyPasswordAsync(account, "demo", CancellationToken.None));
    }

    [AvaloniaFact]
    public async Task Search_and_status_narrow_the_customers()
    {
        var viewModel = App.Services.GetRequiredService<CustomersPageViewModel>();
        await viewModel.LoadAsync();
        var all = viewModel.Summary;

        viewModel.Status = nameof(CustomerStatus.Trial);

        Assert.NotEqual(all, viewModel.Summary);
        Assert.All(viewModel.Rows, c => Assert.Equal(CustomerStatus.Trial, c.Status));

        viewModel.ClearFiltersCommand.Execute(null);
        Assert.Equal(all, viewModel.Summary);
        Assert.Equal(CustomersPageViewModel.PageSize, viewModel.Rows.Count);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.ViewModels;

/// <summary>The dashboard: the headline figures and the revenue chart, loaded when the page is first shown.</summary>
public sealed partial class DashboardPageViewModel : ObservableObject
{
    private readonly ICustomerService _customers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(Kpis))]
    private DashboardSummary? _summary;

    public DashboardPageViewModel(ICustomerService customers)
    {
        _customers = customers;
    }

    public bool IsLoading => Summary is null;

    /// <summary>The headline figures (none while loading).</summary>
    public IReadOnlyList<Kpi> Kpis => Summary?.Kpis ?? [];

    public async Task LoadAsync()
    {
        Summary ??= await _customers.GetDashboardAsync(CancellationToken.None);
    }
}

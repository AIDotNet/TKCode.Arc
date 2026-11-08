using ArcStarterApp.Models;

namespace ArcStarterApp.Services;

/// <summary>The customers and the dashboard's figures. Replace <c>SampleData/SampleCustomerService.cs</c> with your API.</summary>
public interface ICustomerService
{
    /// <summary>Every customer. The Customers page searches, filters and pages them itself; a large data set would pass the query to the server instead.</summary>
    Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellation);

    /// <summary>The figures and the revenue chart of the dashboard.</summary>
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellation);
}

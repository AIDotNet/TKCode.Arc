using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.SampleData;

/// <summary>Invented customers and figures, made up the same way on every run.</summary>
public sealed class SampleCustomerService : ICustomerService
{
    private static readonly string[] s_first = ["Mara", "Jonas", "Priya", "Leo", "Sofia", "Kenji", "Amara", "Felix", "Nora", "Omar", "Ines", "Theo", "Yara", "Lukas", "Mei", "Rafael"];
    private static readonly string[] s_last = ["Lind", "Okafor", "Shah", "Brandt", "Rossi", "Tanaka", "Diallo", "Weber", "Haas", "Haddad", "Kowal", "Park", "Costa", "Novak", "Chen", "Silva"];
    private static readonly string[] s_companies = ["Fieldwork", "Brightline", "Copperleaf", "Harbor & Co", "Lumen Labs", "Northstar", "Pinecrest", "Quartz", "Riverbend", "Solstice", "Tidewater", "Vantage"];
    private static readonly string[] s_plans = ["Starter", "Team", "Business", "Enterprise"];
    private static readonly decimal[] s_prices = [29m, 99m, 299m, 1200m];
    private static readonly string[] s_countries = ["US", "DE", "GB", "JP", "BR", "IN", "FR", "CA"];

    private readonly IReadOnlyList<Customer> _customers = Create();

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellation)
    {
        await Task.Delay(250, cancellation);
        return _customers;
    }

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellation)
    {
        await Task.Delay(250, cancellation);
        var random = new Random(7);
        var today = new DateOnly(2026, 11, 8);
        var days = new List<DateOnly>();
        var revenue = new List<double>();
        var previous = new List<double>();
        for (var i = 29; i >= 0; i--)
        {
            days.Add(today.AddDays(-i));
            double trend = 4200 + (29 - i) * 38;
            revenue.Add(Math.Round(trend + random.Next(-420, 520) + (i % 7 is 0 or 6 ? -650 : 0)));
            previous.Add(Math.Round(trend * 0.86 + random.Next(-380, 380) + (i % 7 is 0 or 6 ? -600 : 0)));
        }
        var active = _customers.Count(c => c.Status is CustomerStatus.Active or CustomerStatus.PastDue);
        var mrr = (double)_customers.Where(c => c.Status != CustomerStatus.Churned).Sum(c => c.MonthlyRevenue);
        Kpi[] kpis =
        [
            new("Monthly revenue", mrr, "$", null, 0, "+8.2%"),
            new("Active customers", active, null, null, 0, "+12"),
            new("Trials", _customers.Count(c => c.Status == CustomerStatus.Trial), null, null, 0, "+3"),
            new("Net retention", 104.2, null, "%", 1, "+1.2%"),
        ];
        return new DashboardSummary(kpis, days, revenue, previous);
    }

    private static IReadOnlyList<Customer> Create()
    {
        var random = new Random(42);
        var list = new List<Customer>();
        for (var i = 0; i < 64; i++)
        {
            var first = s_first[i % s_first.Length];
            var last = s_last[(i * 7 + 3) % s_last.Length];
            var company = s_companies[(i * 5 + 1) % s_companies.Length];
            var plan = random.Next(s_plans.Length);
            var roll = random.Next(100);
            var status = roll < 68 ? CustomerStatus.Active : roll < 82 ? CustomerStatus.Trial : roll < 91 ? CustomerStatus.PastDue : CustomerStatus.Churned;
            var domain = company.ToLowerInvariant().Replace(" & ", "").Replace(" ", "");
            list.Add(new Customer(
                $"cus_{1000 + i}",
                $"{first} {last}",
                $"{first.ToLowerInvariant()}@{domain}.example",
                company,
                s_plans[plan],
                status,
                status == CustomerStatus.Trial ? 0m : s_prices[plan],
                new DateOnly(2026, 11, 8).AddDays(-random.Next(5, 900)),
                s_countries[random.Next(s_countries.Length)]));
        }
        return list;
    }
}

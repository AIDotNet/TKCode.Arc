namespace ArcStarterApp.Models;

/// <summary>Where a customer's subscription stands.</summary>
public enum CustomerStatus
{
    Active,
    Trial,
    PastDue,
    Churned,
}

/// <summary>A customer account: one row of the Customers page.</summary>
public sealed record Customer(
    string Id,
    string Name,
    string Email,
    string Company,
    string Plan,
    CustomerStatus Status,
    decimal MonthlyRevenue,
    DateOnly Joined,
    string Country);

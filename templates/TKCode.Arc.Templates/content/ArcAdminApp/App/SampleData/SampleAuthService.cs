using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.SampleData;

/// <summary>
/// A sign-in without a server: three invented accounts, all with the password <see cref="Password"/>. Any other email
/// signs in as a new member. Delete this file (and the rest of SampleData/) once your own services are registered.
/// </summary>
public sealed class SampleAuthService : IAuthService
{
    /// <summary>The password of every sample account (the sign-in screen shows it).</summary>
    public const string Password = "demo";

    /// <summary>The email the sign-in screen starts with.</summary>
    public const string DefaultEmail = "ada@northwind.example";

    private static readonly Account[] s_accounts =
    [
        new("Ada Lovelace", DefaultEmail, "Owner"),
        new("Grace Hopper", "grace@northwind.example", "Admin"),
        new("Alan Kay", "alan@northwind.example", "Member"),
    ];

    public async Task<Account?> FindAsync(string email, CancellationToken cancellation)
    {
        await Task.Delay(300, cancellation); // a server's round trip
        foreach (var account in s_accounts)
        {
            if (string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return account;
            }
        }
        return null;
    }

    public async Task<bool> VerifyPasswordAsync(Account account, string password, CancellationToken cancellation)
    {
        await Task.Delay(400, cancellation);
        return password == Password;
    }
}

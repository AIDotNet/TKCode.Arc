using ArcStarterApp.Models;

namespace ArcStarterApp.Services;

/// <summary>
/// Signs people in. The sign-in screen asks for an email first (<see cref="FindAsync"/>), then the password
/// (<see cref="VerifyPasswordAsync"/>). Replace <c>SampleData/SampleAuthService.cs</c> with a call to your identity
/// provider and register it in <c>Services/AppServices.cs</c>.
/// </summary>
public interface IAuthService
{
    /// <summary>The account with this email, or null when there is none (the screen then offers to continue anyway).</summary>
    Task<Account?> FindAsync(string email, CancellationToken cancellation);

    /// <summary>Whether <paramref name="password"/> is the password of <paramref name="account"/>.</summary>
    Task<bool> VerifyPasswordAsync(Account account, string password, CancellationToken cancellation);
}

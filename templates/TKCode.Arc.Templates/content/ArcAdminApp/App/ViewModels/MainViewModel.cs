using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArcStarterApp.Models;
using ArcStarterApp.Navigation;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The main view: who is signed in and which page is shown. The sign-in screen shows while <see cref="Account"/> is
/// null; the sidebar's selected key and the page host are bound to <see cref="SelectedKey"/> and <see cref="CurrentPage"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private Account? _found;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(AccountName), nameof(AccountRole))]
    private Account? _account;

    [ObservableProperty]
    private string? _selectedKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private AppPage? _currentPage;

    public MainViewModel(IAuthService auth)
    {
        _auth = auth;
        SelectedKey = AppPages.Dashboard;
    }

    public bool IsSignedIn => Account is not null;

    /// <summary>The signed-in person's name and role, for the sidebar's footer (empty while signed out).</summary>
    public string AccountName => Account?.Name ?? string.Empty;

    public string AccountRole => Account?.Role ?? string.Empty;

    /// <summary>The shown page's title, for the top bar.</summary>
    public string? Title => CurrentPage?.Title;

    /// <summary>The sign-in screen's email step.</summary>
    public async Task<Account?> FindAccountAsync(string email, CancellationToken cancellation)
    {
        _found = await _auth.FindAsync(email, cancellation);
        return _found;
    }

    /// <summary>The sign-in screen's password step. An email no account has signs in as a new member.</summary>
    public Task<bool> VerifyPasswordAsync(string name, string email, string password, CancellationToken cancellation)
    {
        _found ??= new Account(name, email, "Member");
        return _auth.VerifyPasswordAsync(_found, password, cancellation);
    }

    /// <summary>Opens the app for <paramref name="account"/> (after the sign-in screen, or from tests).</summary>
    public void SignIn(Account account)
    {
        SelectedKey = AppPages.Dashboard;
        Account = account;
    }

    /// <summary>Opens the app for the account the sign-in screen found.</summary>
    public void CompleteSignIn(string name, string email) => SignIn(_found ?? new Account(name, email, "Member"));

    [RelayCommand]
    private void SignOut()
    {
        _found = null;
        Account = null;
    }

    /// <summary>Runs a command palette entry: a page key, or one of the actions in Views/MainView.axaml.</summary>
    public void RunCommand(string? id)
    {
        switch (id)
        {
            case "theme-light":
                ArcTheme.Apply(t => t.Appearance(ArcAppearance.Light));
                break;
            case "theme-dark":
                ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark));
                break;
            case "sign-out":
                SignOut();
                break;
            default:
                SelectedKey = id;
                break;
        }
    }

    partial void OnSelectedKeyChanged(string? value)
    {
        // A key that names no page (a row without one) keeps the current page.
        if (AppPages.Find(value) is { } page)
        {
            CurrentPage = page;
        }
    }
}

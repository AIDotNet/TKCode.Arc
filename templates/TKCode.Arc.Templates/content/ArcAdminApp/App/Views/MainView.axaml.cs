using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ArcStarterApp.SampleData;
using ArcStarterApp.Settings;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Views;

/// <summary>
/// The app's content: the sign-in screen until someone signs in, then the sidebar and the pages. The main window shows
/// it on the desktop.
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // The sidebar opens as it was left: folded to the icon rail or not.
        Nav.IsCollapsed = AppSettings.Current.IsSidebarCollapsed;
        Nav.PropertyChanged += OnNavPropertyChanged;

        // The sample accounts' email and password, so the screen can be tried at once. Remove these lines (and the
        // EmailSubtitle) with SampleData/.
        Login.Email = SampleAuthService.DefaultEmail;
        Login.Labels = new LoginSplitLabels
        {
            EmailSubtitle = $"Sample accounts use the password \"{SampleAuthService.Password}\".",
            SigningDescription = "Loading your workspace.",
        };
        // No single sign-on in the sample: add providers (LoginSplitProvider.Google() ...) and SignInWithProviderAsync.
        Login.Providers.Clear();
        Login.SignedIn += (_, e) => ViewModel?.CompleteSignIn(e.Account.Name, e.Account.Email);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel is not { } vm)
        {
            return;
        }
        // The screen's steps ask the view model, which asks IAuthService (Services/).
        Login.LookupAccountAsync = async (email, cancellation) =>
            await vm.FindAccountAsync(email, cancellation) is { } account ? new LoginSplitAccount(account.Name, account.Email) : null;
        Login.VerifyPasswordAsync = (account, password, cancellation) =>
            vm.VerifyPasswordAsync(account.Name, account.Email, password, cancellation);
        vm.PropertyChanged += (_, args) =>
        {
            // Signed out: the sign-in screen starts again at the email step.
            if (args.PropertyName == nameof(MainViewModel.IsSignedIn) && !vm.IsSignedIn)
            {
                Login.Reset();
            }
        };
    }

    private void OnNavPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Sidebar.IsCollapsedProperty && AppSettings.Current.IsSidebarCollapsed != Nav.IsCollapsed)
        {
            AppSettings.Current.IsSidebarCollapsed = Nav.IsCollapsed;
            AppSettings.Current.Save();
        }
    }

    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();

    private void OnCommandSelected(object? sender, CommandPaletteItemSelectedEventArgs e) => ViewModel?.RunCommand(e.Item.Id);
}

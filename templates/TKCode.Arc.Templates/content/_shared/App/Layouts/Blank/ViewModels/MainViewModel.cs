using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
#if (UsesI18n)
using ArcStarterApp.Localization;
#endif

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The main view's data and actions. [ObservableProperty] generates the bindable property (Name from _name, with
/// change notification) and [RelayCommand] the command (GetStartedCommand from GetStarted), at build time.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetStartedCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _sendUpdates = true;

    [ObservableProperty]
    private string _greeting = @@cs:GreetingPrompt@@;

    [RelayCommand(CanExecute = nameof(CanGetStarted))]
    private void GetStarted()
    {
        var format = SendUpdates ? @@cs:GreetingWelcomeUpdates@@ : @@cs:GreetingWelcome@@;
        Greeting = string.Format(CultureInfo.CurrentCulture, format, Name.Trim());
    }

    private bool CanGetStarted() => !string.IsNullOrWhiteSpace(Name);
}

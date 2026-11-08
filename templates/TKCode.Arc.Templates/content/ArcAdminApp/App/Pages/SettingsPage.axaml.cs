using Avalonia.Controls;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        Page.SaveRequested += (_, e) => Run(e, vm => vm.SaveGeneral());
        Page.DiscardRequested += (_, e) => Run(e, vm => vm.DiscardGeneral());
    }

    private void Run(SettingsSectionEventArgs e, Action<SettingsPageViewModel> action)
    {
        if (e.Section == General && DataContext is SettingsPageViewModel vm)
        {
            action(vm);
        }
    }
}

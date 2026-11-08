using Avalonia.Controls;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Pages;

public partial class TeamPage : UserControl
{
    public TeamPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TeamPageViewModel vm)
        {
            // The blocks await these and show their own progress, success and errors.
            Members.SendInvitesAction = vm.SendInvitesAsync;
            Roles.SaveHandler = vm.SaveGrantsAsync;
        }
    }
}

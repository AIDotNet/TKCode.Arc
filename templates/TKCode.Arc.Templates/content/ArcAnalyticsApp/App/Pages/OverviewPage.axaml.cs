using Avalonia.Controls;
using ArcStarterApp.ViewModels;

namespace ArcStarterApp.Pages;

public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // The dimensions have no bindable source: add them once.
        if (DataContext is OverviewPageViewModel vm && Kpis.Dimensions.Count == 0)
        {
            Kpis.Dimensions.AddRange(vm.Dimensions);
        }
    }
}

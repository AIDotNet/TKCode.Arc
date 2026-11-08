using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcItemNamespace.ViewModels;

/// <summary>The ArcItemTitle page's data and actions.</summary>
public sealed partial class ArcItemPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _heading = "ArcItemTitle";

    [ObservableProperty]
    private DateTimeOffset? _refreshedAt;

    [RelayCommand]
    private void Refresh()
    {
        RefreshedAt = DateTimeOffset.Now;
        Heading = $"Refreshed at {RefreshedAt:t}";
    }
}

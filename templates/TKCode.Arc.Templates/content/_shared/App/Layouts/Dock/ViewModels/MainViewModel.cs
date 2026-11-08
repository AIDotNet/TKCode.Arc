using CommunityToolkit.Mvvm.ComponentModel;

namespace ArcStarterApp.ViewModels;

/// <summary>The main view's data: here the text of the Notes document. Add what your panes show.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _notes;
}

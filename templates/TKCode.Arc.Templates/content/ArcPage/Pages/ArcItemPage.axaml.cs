using Avalonia.Controls;

namespace ArcItemNamespace.Pages;

// To show this page in an app made with dotnet new tkcode-arc (navigation layouts):
//   Navigation/AppPages.cs, in All:
#if (viewModel)
//       new("arc-item-key", "ArcItemTitle", () => new ArcItemPage { DataContext = new ViewModels.ArcItemPageViewModel() }),
#else
//       new("arc-item-key", "ArcItemTitle", () => new ArcItemPage()),
#endif
//   Views/MainView.axaml, in the sidebar:
//       <arc:SidebarItem Key="arc-item-key" Label="ArcItemTitle" Icon="{lucide:LucideGeometry File}" />
public partial class ArcItemPage : UserControl
{
    public ArcItemPage()
    {
        InitializeComponent();
    }
}

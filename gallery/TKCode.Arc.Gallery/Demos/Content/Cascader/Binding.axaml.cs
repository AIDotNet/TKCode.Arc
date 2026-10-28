using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderBindingDemo : UserControl
{
    public CascaderBindingDemo()
    {
        InitializeComponent();
        CascaderDemoData.Configure(Region);
        // The view model has written its value before there is any data: the field keeps it and
        // finds the path when the data shows up.
        Region.SelectedValue = "330106";
        Region.SelectionChanged += (_, _) => ShowPath();
        ShowPath();
        Load();
        Reload.Click += (_, _) =>
        {
            Region.ItemsSource = null;
            Load();
        };
    }

    private void ShowPath() =>
        Path.Text = $"SelectedValue: {Region.SelectedValue ?? "none"}\n"
                    + $"SelectedPath: {(Region.SelectedPath.Count == 0 ? "(empty)" : string.Join(" > ", Region.SelectedPath.Select(n => ((CascaderRegion)n).Name)))}";

    private async void Load()
    {
        await Task.Delay(1000);
        Region.ItemsSource = CascaderDemoData.Regions();
    }
}

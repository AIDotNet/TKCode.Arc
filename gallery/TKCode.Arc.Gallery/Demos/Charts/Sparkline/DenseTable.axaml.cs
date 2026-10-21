using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class SparklineDenseTableDemo : UserControl
{
    public SparklineDenseTableDemo()
    {
        InitializeComponent();
        Gateway.ItemsSource = ChartsADemoData.Daily(20, 50, 30, 11);
        Billing.ItemsSource = ChartsADemoData.Daily(20, 50, 30, 12);
        Search.ItemsSource = ChartsADemoData.Daily(20, 50, 30, 13);
    }
}

using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteHeaderPreviewDemo : UserControl
{
    private readonly SiteHeader _header = new() { Current = "product" };
    private readonly TextBlock _title;
    private readonly TextBlock _status = new() { Opacity = 0, Height = 0 };

    public SiteHeaderPreviewDemo()
    {
        InitializeComponent();
        // Arc's example items, with a drawn picture on the Product card.
        _header.ItemsSource = SiteHeaderDemoData.ExampleItems();
        _header.Navigate += (_, e) => _status.Text = $"Opened {e.Label}";
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _header.PropertyChanged += (_, e) =>
        {
            if (e.Property == SiteHeader.CurrentProperty)
            {
                OnCurrentChanged(_header.Current);
            }
        };
        var frame = SiteHeaderDemoData.CreateFrame(_header, 520, out _title);
        Host.Children.Add(frame);
        Host.Children.Add(_status);
    }

    private void OnCurrentChanged(string? current)
    {
        _title.Text = current switch
        {
            "product" => "Product",
            "resources" => "Resources",
            "pricing" => "Pricing",
            "customers" => "Customers",
            _ => "Arc"
        };
    }

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is SegmentedControl { SelectedValue: string value })
        {
            _header.Variant = value switch
            {
                "simple" => SiteHeaderVariant.Simple,
                "centered" => SiteHeaderVariant.Centered,
                _ => SiteHeaderVariant.Mega
            };
        }
    }
}

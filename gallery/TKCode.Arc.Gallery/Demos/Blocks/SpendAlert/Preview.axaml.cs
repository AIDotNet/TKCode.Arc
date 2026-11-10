using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SpendAlertPreviewDemo : UserControl
{
    public SpendAlertPreviewDemo()
    {
        InitializeComponent();
        SpendAlertSamples.Apply(Alert);
        Purchase.Click += (_, _) =>
        {
            var (place, amount) = SpendAlertSamples.Purchases[_next++ % SpendAlertSamples.Purchases.Count];
            Purchase.Content = $"Add a purchase (last: {place})";
            Alert.AddSpending(amount);
        };
    }

    private int _next;
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PaymentFlowCardPaymentDemo : UserControl
{
    private int _approved;
    private int _total;

    public PaymentFlowCardPaymentDemo()
    {
        InitializeComponent();
    }

    private void OnPaymentCompleted(object? sender, PaymentCompletedEventArgs e)
    {
        _total++;
        _approved += e.Approved ? 1 : 0;
        Log.Text = $"{(e.Approved ? "Approved" : "Declined")} · {_approved} of {_total} approved";
    }

    private void OnHalfSpeedClick(object? sender, RoutedEventArgs e)
    {
        Payment.Speed = 0.5;
    }

    private void OnNormalSpeedClick(object? sender, RoutedEventArgs e)
    {
        Payment.Speed = 1;
    }

    private void OnDoubleSpeedClick(object? sender, RoutedEventArgs e)
    {
        Payment.Speed = 2;
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Payment.Restart();
    }
}

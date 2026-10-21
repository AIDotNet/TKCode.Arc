using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class StepperVerticalWithErrorDemo : UserControl
{
    public StepperVerticalWithErrorDemo()
    {
        InitializeComponent();
    }

    private void OnToggleErrorClick(object? sender, RoutedEventArgs e)
    {
        var payment = Setup.Steps[2];
        payment.Error = payment.Error is null ? "Card declined. Try another card." : null;
    }
}

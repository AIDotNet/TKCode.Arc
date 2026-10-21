using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class StepperCheckoutDemo : UserControl
{
    public StepperCheckoutDemo()
    {
        InitializeComponent();
    }

    private void OnStepSelected(object? sender, StepperStepSelectedEventArgs e)
    {
        Selected.Text = $"Returned to step {e.Index + 1}: {e.Step.Label}";
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        Checkout.CurrentIndex = Math.Max(0, Checkout.CurrentIndex - 1);
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Checkout.CurrentIndex = Math.Min(Checkout.Steps.Count, Checkout.CurrentIndex + 1);
    }

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        Checkout.CurrentIndex = Checkout.Steps.Count;
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingIllustrationControlledDemo : UserControl
{
    private bool _syncing;

    public OnboardingIllustrationControlledDemo()
    {
        InitializeComponent();
        Step.SelectedValue = "0";
        // The card writes CurrentIndex and IsCompleted back (Next, Back, the dots, the arrows, Get started).
        Card.PropertyChanged += (_, e) =>
        {
            if (e.Property == OnboardingIllustration.CurrentIndexProperty || e.Property == OnboardingIllustration.IsCompletedProperty)
            {
                Sync();
            }
        };
    }

    private void Sync()
    {
        _syncing = true;
        Step.SelectedValue = Card.CurrentIndex.ToString();
        Done.IsChecked = Card.IsCompleted;
        _syncing = false;
    }

    private void OnStepSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Card is not null && Step.SelectedValue is string value && int.TryParse(value, out var index))
        {
            Card.CurrentIndex = index;
        }
    }

    private void OnDoneChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && Card is not null)
        {
            Card.IsCompleted = Done.IsChecked == true;
        }
    }
}

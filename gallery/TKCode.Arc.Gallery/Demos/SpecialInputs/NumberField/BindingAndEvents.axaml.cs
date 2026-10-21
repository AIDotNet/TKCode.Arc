using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NumberFieldBindingAndEventsDemo : UserControl
{
    public NumberFieldBindingAndEventsDemo()
    {
        InitializeComponent();
    }

    private void OnValueChanged(object? sender, NumberFieldValueChangedEventArgs e)
        => Readout.Text = $"Value {e.NewValue} (was {e.OldValue}, {e.ChangeSource})";
}

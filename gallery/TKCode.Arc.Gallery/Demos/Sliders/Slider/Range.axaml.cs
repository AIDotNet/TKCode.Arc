using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SliderRangeDemo : UserControl
{
    public SliderRangeDemo()
    {
        InitializeComponent();
        Price.ValueFormatter = v => $"${v:N0}";
    }

    // Raised once per release or key press, not on every step of the drag.
    private void OnValueCommitted(object? sender, SliderValueCommittedEventArgs e)
        => Committed.Text = $"Committed: ${e.LowerValue:N0} – ${e.UpperValue:N0}";
}

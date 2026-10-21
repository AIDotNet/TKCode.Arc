using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ScrubInputStatesDemo : UserControl
{
    public ScrubInputStatesDemo()
    {
        InitializeComponent();
        var tryHint = Hint.Text;
        // An entry that cannot be read sets IsInvalid for a moment and keeps the old value.
        SizeInput.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrubInput.IsInvalidProperty)
            {
                Hint.Text = SizeInput.IsInvalid ? "Rejected: the value stays at " + SizeInput.FormattedValue : tryHint;
            }
        };
    }
}

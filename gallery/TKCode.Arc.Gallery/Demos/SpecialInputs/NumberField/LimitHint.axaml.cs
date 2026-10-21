using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NumberFieldLimitHintDemo : UserControl
{
    public NumberFieldLimitHintDemo()
    {
        InitializeComponent();
        Guests.LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum
            ? $"Room holds {limit}"
            : $"At least {limit}";
    }
}

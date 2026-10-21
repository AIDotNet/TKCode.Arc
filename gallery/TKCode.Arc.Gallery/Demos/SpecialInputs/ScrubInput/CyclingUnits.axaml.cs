using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ScrubInputCyclingUnitsDemo : UserControl
{
    public ScrubInputCyclingUnitsDemo()
    {
        InitializeComponent();
        // The unit button sets Unit; show which unit is in effect.
        WidthInput.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrubInput.UnitProperty)
            {
                UnitLog.Text = "Unit: " + WidthInput.EffectiveUnit;
            }
        };
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiStepFormControlledDemo : UserControl
{
    public MultiStepFormControlledDemo()
    {
        InitializeComponent();
        Form.StepChanged += (_, e) =>
            Log.Text = $"CurrentIndex = {e.NewIndex} ({(e.Direction > 0 ? "forward" : "back")} from {e.OldIndex})";
        Form.Completed += (_, _) => Log.Text = "Completed";
    }
}

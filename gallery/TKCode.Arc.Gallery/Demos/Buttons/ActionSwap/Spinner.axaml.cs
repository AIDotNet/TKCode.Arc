using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionSwapSpinnerDemo : UserControl
{
    public ActionSwapSpinnerDemo()
    {
        InitializeComponent();
        // "Added" has no PendingLabel, so the pill folds into a round spinner while the task runs.
        Save.AsyncAction = async (next, _) =>
        {
            if (next.Id == "added")
            {
                await Task.Delay(1100);
            }
        };
    }
}

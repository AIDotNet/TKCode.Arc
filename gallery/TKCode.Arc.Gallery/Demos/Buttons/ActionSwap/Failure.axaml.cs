using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionSwapFailureDemo : UserControl
{
    private int _attempts;

    public ActionSwapFailureDemo()
    {
        InitializeComponent();
        // The first attempt fails: the pill shows "Try again" for 1.8 seconds, then returns to "Publish".
        Publish.AsyncAction = async (next, _) =>
        {
            await Task.Delay(800);
            if (_attempts++ % 2 == 0)
            {
                Message.Text = "The first attempt fails, the second succeeds.";
                throw new InvalidOperationException("The server did not answer.");
            }
            Message.Text = string.Empty;
        };
    }
}

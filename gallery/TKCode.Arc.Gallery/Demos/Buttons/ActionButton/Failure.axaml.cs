using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionButtonFailureDemo : UserControl
{
    public ActionButtonFailureDemo()
    {
        InitializeComponent();
        Sync.AsyncAction = async () =>
        {
            await Task.Delay(900);
            throw new InvalidOperationException("The server did not answer.");
        };
    }

    private void OnActionFailed(object? sender, ActionButtonFailedEventArgs e)
    {
        Message.Text = $"Back to idle: {e.Exception.Message}";
    }
}

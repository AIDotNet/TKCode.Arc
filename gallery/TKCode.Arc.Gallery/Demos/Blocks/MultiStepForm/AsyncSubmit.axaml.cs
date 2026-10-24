using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiStepFormAsyncSubmitDemo : UserControl
{
    public MultiStepFormAsyncSubmitDemo()
    {
        InitializeComponent();
        // The final submission: the button spins and Back is disabled while it runs; a throw keeps the form on the step.
        Form.CompleteAsync = async cancellation =>
        {
            Log.Text = "Sending…";
            await Task.Delay(1200, cancellation);
            if (FailNext.IsChecked == true)
            {
                FailNext.IsChecked = false;
                throw new InvalidOperationException("The server did not answer. Try again.");
            }
        };
        Form.CompleteFailed += (_, e) => Log.Text = e.Exception.Message;
        Form.Completed += (_, _) =>
        {
            Log.Text = "Sent.";
            StartOver.IsVisible = true;
        };
        StartOver.Click += (_, _) =>
        {
            StartOver.IsVisible = false;
            Log.Text = string.Empty;
            Form.Restart();
        };
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesFailingRedeployDemo : UserControl
{
    private int _attempts;

    public EnvVariablesFailingRedeployDemo()
    {
        InitializeComponent();
        EnvVariablesDemoData.Fill(Block);

        // Odd attempts fail after a second; the retry goes through. The changes stay pending until one succeeds.
        Block.RedeployAction = async (request, cancellation) =>
        {
            await Task.Delay(1000, cancellation);
            _attempts++;
            if (_attempts % 2 == 1)
            {
                throw new InvalidOperationException("The build queue is full. Your changes are kept; try again.");
            }
        };
        Block.RedeployFailed += (_, e) => Log.Text = $"Attempt {_attempts} failed: {e.Exception?.Message}";
        Block.Redeployed += (_, e) =>
            Log.Text = $"Attempt {_attempts} deployed {e.Request.ChangeCount} change(s) to {string.Join(", ", e.Request.Environments)}.";
    }
}

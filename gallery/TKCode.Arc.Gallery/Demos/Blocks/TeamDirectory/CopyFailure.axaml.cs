using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamDirectoryCopyFailureDemo : UserControl
{
    private int _attempts;

    public TeamDirectoryCopyFailureDemo()
    {
        InitializeComponent();
        Directory.People.AddRange(TeamDirectoryDemoData.People());

        // Stand-in for a remote clipboard or an audit call: it takes half a second and refuses every second request.
        // The button stays as it is while the call runs, then shows Copied or Retry for 1.8 seconds.
        Directory.CopyAsync = async (email, cancellation) =>
        {
            await Task.Delay(500, cancellation);
            _attempts++;
            if (_attempts % 2 == 0)
            {
                throw new InvalidOperationException("The clipboard service is busy.");
            }
        };
        Directory.EmailCopied += (_, e) => Log.Text = e.Succeeded
            ? $"Attempt {_attempts}: copied {e.Email}."
            : $"Attempt {_attempts}: {e.Error?.Message ?? "failed"} The button offers a retry.";
    }
}

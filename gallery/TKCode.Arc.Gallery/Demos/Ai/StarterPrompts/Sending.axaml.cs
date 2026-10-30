using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app-driven send with failure, retry and stop.</summary>
public partial class StarterPromptsSendingDemo : UserControl
{
    public StarterPromptsSendingDemo()
    {
        InitializeComponent();
        StarterPromptsDemoData.AddStarters(Prompts);
        Prompts.SubmitRequested += (_, e) =>
        {
            var from = e.Prompt is { } prompt ? $"from “{prompt.Title}”" : "written by hand";
            Log.Text = $"Sending {e.Message.Length} characters, {from}.";
            e.Sending = SendAsync(FailNext.IsChecked == true, e.CancellationToken);
            FailNext.IsChecked = false;
        };
    }

    /// <summary>Stands in for a chat endpoint: two seconds, then success or an error the footer shows.</summary>
    private async Task SendAsync(bool fail, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }
        catch (OperationCanceledException)
        {
            Log.Text = "Stopped. The text stays in the composer.";
            throw;
        }
        if (fail)
        {
            Log.Text = "The send failed.";
            throw new InvalidOperationException("Couldn't send. Try again.");
        }
        Log.Text = "Sent.";
    }
}

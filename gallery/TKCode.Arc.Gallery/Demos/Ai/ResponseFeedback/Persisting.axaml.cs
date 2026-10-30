using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Persists feedback through a pretend server call that can be made to fail, and logs every submission.</summary>
public partial class ResponseFeedbackPersistingDemo : UserControl
{
    public ResponseFeedbackPersistingDemo()
    {
        InitializeComponent();
        Card.Reply = ResponseFeedbackDemoData.Reply();
        Card.FeedbackHandler = SaveAsync;
        Card.FeedbackSubmitted += (_, e) => Log.Text = Describe(e.Submission);
    }

    private async Task SaveAsync(FeedbackSubmission feedback, CancellationToken cancellation)
    {
        // A real app would POST the submission here and pass the token on, so a closed panel aborts the request.
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellation);
        if (FailSwitch.IsChecked == true)
        {
            FailSwitch.IsChecked = false;
            throw new InvalidOperationException("The feedback service is not reachable.");
        }
    }

    private static string Describe(FeedbackSubmission feedback)
    {
        var text = $"{feedback.Stage} · {feedback.Rating} · message {feedback.MessageId}";
        if (feedback.Reasons.Count > 0)
        {
            text += " · " + string.Join(", ", feedback.Reasons);
        }
        if (feedback.Comment is { } comment)
        {
            text += $" · \"{comment}\"";
        }
        return text;
    }
}

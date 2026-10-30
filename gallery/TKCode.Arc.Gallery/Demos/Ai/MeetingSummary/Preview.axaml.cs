using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulation live, a failed pass and a call with no transcript.</summary>
public partial class MeetingSummaryPreviewDemo : UserControl
{
    private readonly bool _ready;

    public MeetingSummaryPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "live");
        }
    }

    private void Show(string scenario)
    {
        switch (scenario)
        {
            case "error":
                Card.Meeting = null;
                Card.ErrorText = "The recording ended before the transcript was ready. Try again in a minute.";
                Card.Status = MeetingSummaryStatus.Error;
                break;
            case "empty":
                Card.Meeting = new MeetingSummaryData
                {
                    Title = "Quick check-in",
                    StartsAt = new DateTime(2026, 10, 29, 9, 0, 0),
                    DurationMinutes = 1,
                    Attendees = [new("sam", "Sam Porter"), new("noor", "Noor Haddad")],
                };
                Card.ErrorText = null;
                Card.Status = MeetingSummaryStatus.Ready;
                break;
            default:
                // Back to the card's own pass on the simulation.
                Card.Meeting = null;
                Card.ErrorText = null;
                Card.Status = null;
                Card.Regenerate();
                break;
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A meeting of your own, with the status, the sends and the checks handled by the app.</summary>
public partial class MeetingSummaryYourMeetingDemo : UserControl
{
    private readonly DispatcherTimer _request = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private int _issue = 30;

    public MeetingSummaryYourMeetingDemo()
    {
        InitializeComponent();
        Card.Meeting = Notes();
        Card.Model = MeetingSummaryDemoData.Model;
        Card.Status = MeetingSummaryStatus.Summarizing;
        _request.Tick += (_, _) =>
        {
            // Your request resolved: the notes stream in.
            _request.Stop();
            Card.Status = MeetingSummaryStatus.Ready;
        };
        AttachedToVisualTree += (_, _) => _request.Start();
        DetachedFromVisualTree += (_, _) => _request.Stop();
    }

    private void OnSendRequested(object? sender, MeetingSummarySendEventArgs e) =>
        e.Completion = CreateAsync(e.Item, e.Target, e.CancellationToken);

    /// <summary>A stand-in for your tracker's API: pass the token on to your HTTP call.</summary>
    private async Task<MeetingSummarySendResult?> CreateAsync(MeetingActionItem item, MeetingSummarySendTarget target, CancellationToken token)
    {
        await Task.Delay(700, token);
        var reference = target == MeetingSummarySendTarget.Docs ? "Onboarding notes" : "ENG-" + _issue++;
        return new MeetingSummarySendResult(reference);
    }

    private void OnSent(object? sender, MeetingSummarySentEventArgs e) =>
        Log.Text = $"Sent \"{e.Item.Text}\" to {e.Target.Name} as {e.Result?.Reference}.";

    private void OnChanged(object? sender, MeetingActionItemChangedEventArgs e) =>
        Log.Text = $"\"{e.Item.Text}\" is {(e.IsDone ? "done" : "open again")}.";

    private void OnRegenerate(object? sender, RoutedEventArgs e)
    {
        Log.Text = "Regenerating…";
        Card.Status = MeetingSummaryStatus.Summarizing;
        _request.Start();
    }

    private static TimeSpan At(int minutes, int seconds) => new(0, minutes, seconds);

    private static MeetingSummaryData Notes() => new()
    {
        Title = "Onboarding flow review",
        Platform = "Video call",
        StartsAt = new DateTime(2026, 10, 27, 10, 30, 0),
        DurationMinutes = 48,
        TranscriptWords = 6120,
        Attendees =
        [
            new("ines", "Ines Duarte", "Design"),
            new("kofi", "Kofi Mensah", "Engineering"),
            new("ruth", "Ruth Adler", "Research"),
        ],
        Summary =
        [
            new("s1", "Research found that most new teams stall on the invite step, before they create a first project.",
                new("ruth", At(4, 12), "Seven of the ten teams we watched got stuck on invites. They didn't know who to add yet.")),
            new("s2", "The checklist on the home page helps, but people miss it below the fold on laptops.",
                new("ines", At(15, 40), "On a 13 inch screen the checklist starts right at the bottom edge. Nobody scrolls to it.")),
        ],
        Decisions =
        [
            new("d1", "Make inviting teammates optional and move it after the first project.",
                new("kofi", At(27, 5), "Let's just let them skip invites. They can add people once there's something to share.")),
        ],
        ActionItems =
        [
            new("a1", "Move the invite step after project creation", "kofi", new DateOnly(2026, 11, 3),
                new("kofi", At(29, 18), "I can move the step this sprint, it's mostly routing.")),
            new("a2", "Pin the checklist above the fold on small screens", "ines", new DateOnly(2026, 11, 5),
                new("ines", At(33, 51), "I'll try a pinned version of the checklist for small screens.")),
            new("a3", "Share the session recordings with the growth team", "ruth", new DateOnly(2026, 10, 30),
                new("ruth", At(44, 2), "I'll send the recordings to growth after this call.")),
        ],
    };
}

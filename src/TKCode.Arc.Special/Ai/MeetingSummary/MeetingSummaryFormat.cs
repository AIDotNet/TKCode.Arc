using System.Globalization;
using System.Text;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Pure text helpers of <see cref="MeetingSummary"/>.</summary>
internal static class MeetingSummaryFormat
{
    /// <summary>A transcript timestamp: "2:14", "17:05", from an hour on "1:02:07".</summary>
    public static string Timestamp(TimeSpan at)
    {
        var total = Math.Max(0, (long)Math.Floor(at.TotalSeconds));
        var h = total / 3600;
        var m = total / 60 % 60;
        var s = total % 60;
        return h > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{h}:{m:00}:{s:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{total / 60}:{s:00}");
    }

    /// <summary>The first name: everything before the first space.</summary>
    public static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed[..space];
    }

    /// <summary>The notes as plain text for the clipboard: title, then each section as a list.</summary>
    public static string PlainText(MeetingSummaryData data, MeetingSummaryLabels labels, Func<MeetingActionItem, bool> isDone)
    {
        var text = new StringBuilder();
        text.Append(data.Title);
        void Section(string heading, IEnumerable<string> lines)
        {
            var first = true;
            foreach (var line in lines)
            {
                if (first)
                {
                    text.Append("\n\n").Append(heading);
                    first = false;
                }
                text.Append('\n').Append(line);
            }
        }
        Section(labels.Summary, data.Summary.Select(p => "- " + p.Text));
        Section(labels.Decisions, data.Decisions.Select(p => "- " + p.Text));
        Section(labels.ActionItems, data.ActionItems.Select(item =>
        {
            var line = (isDone(item) ? "- [x] " : "- [ ] ") + item.Text;
            var owner = data.Attendee(item.OwnerId);
            if (owner is not null)
            {
                line += " (" + owner.Name + ")";
            }
            if (item.Due is { } due)
            {
                line += ", " + labels.Due(due);
            }
            return line;
        }));
        return text.ToString();
    }
}

/// <summary>The one-shot timers of a <see cref="MeetingSummary"/> (the summarizing pass, the stream ticks, the hover intent, the sample's moves, simulated sends).</summary>
internal interface IMeetingSummaryTimers
{
    /// <summary>Runs <paramref name="callback"/> once after <paramref name="delay"/>; dispose to cancel.</summary>
    IDisposable Start(TimeSpan delay, Action callback);
}

/// <summary>Dispatcher timers on the UI thread.</summary>
internal sealed class MeetingSummaryDispatcherTimers : IMeetingSummaryTimers
{
    public static readonly MeetingSummaryDispatcherTimers Instance = new();

    public IDisposable Start(TimeSpan delay, Action callback)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : delay };
        var shot = new Shot(timer);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!shot.Cancelled)
            {
                callback();
            }
        };
        timer.Start();
        return shot;
    }

    private sealed class Shot(DispatcherTimer timer) : IDisposable
    {
        public bool Cancelled { get; private set; }

        public void Dispose()
        {
            Cancelled = true;
            timer.Stop();
        }
    }
}

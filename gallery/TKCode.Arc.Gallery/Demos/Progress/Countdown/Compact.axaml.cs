using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CountdownCompactDemo : UserControl
{
    public CountdownCompactDemo()
    {
        InitializeComponent();
        Deadline.Target = DateTimeOffset.UtcNow + new TimeSpan(0, 5, 12, 40);
        Hours.Target = DateTimeOffset.UtcNow + new TimeSpan(1, 2, 30, 0);
        // A wall-clock time in a named zone resolves to one fixed moment.
        Keynote.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", TimeZoneInfo.Utc);
    }
}

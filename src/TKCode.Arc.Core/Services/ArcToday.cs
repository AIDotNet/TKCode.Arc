using Avalonia.Threading;

namespace TKCode.Arc.Primitives;

/// <summary>Today's date with a notification at local midnight (Arc's use-today).</summary>
public static class ArcToday
{
    private static DispatcherTimer? s_timer;
    private static EventHandler? s_changed;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static event EventHandler? Changed
    {
        add
        {
            s_changed += value;
            EnsureTimer();
        }
        remove => s_changed -= value;
    }

    private static void EnsureTimer()
    {
        if (s_timer is not null)
        {
            return;
        }
        s_timer = new DispatcherTimer();
        s_timer.Tick += (_, _) =>
        {
            Schedule();
            s_changed?.Invoke(null, EventArgs.Empty);
        };
        Schedule();
    }

    private static void Schedule()
    {
        var now = DateTime.Now;
        var next = now.Date.AddDays(1).AddSeconds(1);
        s_timer!.Interval = next - now;
        s_timer.Start();
    }
}

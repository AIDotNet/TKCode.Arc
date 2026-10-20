using Avalonia.Threading;

namespace TKCode.Arc.Tests.Media;

/// <summary>Dispatcher helpers for the Media tests (timers only fire inside a running frame).</summary>
internal static class MediaTestSupport
{
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }
            RunFrame(10);
        }
        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    public static void Pump(int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            RunFrame(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void RunFrame(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }
}

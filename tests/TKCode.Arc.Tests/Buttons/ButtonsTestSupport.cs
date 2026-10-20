using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Buttons;

/// <summary>Helpers for the Buttons and Gestures tests (timers, template parts, peers).</summary>
internal static class ButtonsTestSupport
{
    /// <summary>
    /// Runs the dispatcher loop (jobs and timers) until <paramref name="condition"/> holds. Dispatcher timers only fire
    /// inside a running frame, so this pushes short frames instead of calling RunJobs.
    /// </summary>
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
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

    /// <summary>Runs the dispatcher loop for <paramref name="milliseconds"/>.</summary>
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

    public static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    public static AutomationPeer Peer(Control control) => ControlAutomationPeer.CreatePeerForElement(control);
}

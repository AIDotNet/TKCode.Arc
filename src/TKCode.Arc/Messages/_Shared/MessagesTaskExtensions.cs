using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Runs a continuation of a motion task on the UI thread. Motion tasks complete with
/// <c>RunContinuationsAsynchronously</c>; posting explicitly keeps continuations on the UI thread no matter which
/// synchronization context (if any) was current when the animation started.
/// </summary>
internal static class MessagesTaskExtensions
{
    public static void ThenOnUiThread(this Task task, Action continuation)
    {
        if (task.IsCompleted && Dispatcher.UIThread.CheckAccess())
        {
            continuation();
            return;
        }
        task.ContinueWith(static (_, state) => Dispatcher.UIThread.Post((Action)state!), continuation,
                          CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}

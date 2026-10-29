using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Runs a continuation of a motion task on its owner's dispatcher. Motion tasks complete with
/// <c>RunContinuationsAsynchronously</c>; posting explicitly keeps continuations on the UI thread no matter which
/// synchronization context (if any) was current when the animation started. The owner's dispatcher, not
/// <see cref="Dispatcher.UIThread"/>: the continuation runs on a pool thread, where <c>UIThread</c> makes a dispatcher
/// for that thread when none is current.
/// </summary>
internal static class MessagesTaskExtensions
{
    public static void ThenOnUiThread(this Task task, Dispatcher dispatcher, Action continuation)
    {
        if (task.IsCompleted && dispatcher.CheckAccess())
        {
            continuation();
            return;
        }
        task.ContinueWith(_ => dispatcher.Post(continuation),
                          CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}

using System.Globalization;
using AtomUI;
using AtomUI.Localization;
using Avalonia;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The workflow controls' built-in text in the current AtomUI language (<c>Localization/*.xlf</c>).</summary>
internal static class WorkflowText
{
    public static ILanguageManager? Languages => Application.Current?.GetLanguageManager();

    /// <summary>The AtomUI formatting culture, or the current culture before the application starts.</summary>
    public static CultureInfo FormattingCulture => Languages?.Current.FormattingCulture ?? CultureInfo.CurrentCulture;

    public static string Get(WorkflowLangResourceKind kind) =>
        Application.Current?.GetLocalizer()?.Get(kind) ?? Fallback(kind);

    public static string Format(WorkflowLangResourceKind kind, params object?[] arguments)
    {
        if (Application.Current?.GetLocalizer() is { } localizer)
        {
            return localizer.Format(kind, arguments);
        }
        return string.Format(CultureInfo.InvariantCulture, Fallback(kind), arguments);
    }

    /// <summary>The word for a run state (null for <see cref="WorkflowNodeState.Idle"/>).</summary>
    public static string? State(WorkflowNodeState state) => state switch
    {
        WorkflowNodeState.Pending => Get(WorkflowLangResourceKind.StatePending),
        WorkflowNodeState.Running => Get(WorkflowLangResourceKind.StateRunning),
        WorkflowNodeState.Done => Get(WorkflowLangResourceKind.StateDone),
        WorkflowNodeState.Failed => Get(WorkflowLangResourceKind.StateFailed),
        WorkflowNodeState.Skipped => Get(WorkflowLangResourceKind.StateSkipped),
        _ => null
    };

    /// <summary>A step's time: "118 ms" under a second, "1.2 s" under a minute, then "1 min 05 s".</summary>
    public static string Duration(TimeSpan duration)
    {
        var culture = FormattingCulture;
        if (duration < TimeSpan.FromSeconds(1))
        {
            var ms = Math.Max(0, (int)Math.Round(duration.TotalMilliseconds));
            return Format(WorkflowLangResourceKind.DurationMilliseconds, ms.ToString("N0", culture));
        }
        if (duration < TimeSpan.FromMinutes(1))
        {
            return Format(WorkflowLangResourceKind.DurationSeconds, duration.TotalSeconds.ToString("0.0", culture));
        }
        var minutes = (int)duration.TotalMinutes;
        return Format(WorkflowLangResourceKind.DurationMinutes, minutes.ToString("N0", culture),
            duration.Seconds.ToString("00", culture));
    }

    /// <summary>"Workflow, 6 steps" for a canvas without its own name.</summary>
    public static string CanvasName(int steps) => steps == 1
        ? Get(WorkflowLangResourceKind.CanvasNameOne)
        : Format(WorkflowLangResourceKind.CanvasNameOther, steps.ToString("N0", FormattingCulture));

    /// <summary>English text for code that runs before the application starts (design time, plain unit tests).</summary>
    private static string Fallback(WorkflowLangResourceKind kind) => kind switch
    {
        WorkflowLangResourceKind.CanvasNameOne => "Workflow, 1 step",
        WorkflowLangResourceKind.CanvasNameOther => "Workflow, {0} steps",
        WorkflowLangResourceKind.DurationMilliseconds => "{0} ms",
        WorkflowLangResourceKind.DurationMinutes => "{0} min {1} s",
        WorkflowLangResourceKind.DurationSeconds => "{0} s",
        WorkflowLangResourceKind.StateDone => "Done",
        WorkflowLangResourceKind.StateFailed => "Failed",
        WorkflowLangResourceKind.StatePending => "Waiting",
        WorkflowLangResourceKind.StateRunning => "Running",
        WorkflowLangResourceKind.StateSkipped => "Skipped",
        WorkflowLangResourceKind.AnnounceFailed => "{0} failed",
        WorkflowLangResourceKind.AnnounceFailedReason => "{0} failed: {1}",
        WorkflowLangResourceKind.AnnounceFinished => "Run finished: {0} done, {1} failed, {2} skipped",
        WorkflowLangResourceKind.CanvasHelp => "Arrow keys move between steps, Alt with an arrow follows the connections, Enter opens a step, plus and minus zoom, Shift+1 fits the workflow.",
        WorkflowLangResourceKind.ZoomFit => "Fit to view",
        WorkflowLangResourceKind.ZoomIn => "Zoom in",
        WorkflowLangResourceKind.ZoomOut => "Zoom out",
        WorkflowLangResourceKind.ZoomPercent => "{0}%",
        WorkflowLangResourceKind.ZoomReset => "Reset to 100%",
        WorkflowLangResourceKind.ConnectSelf => "Can't connect a step to itself",
        WorkflowLangResourceKind.ConnectDirection => "Connect an output to an input",
        WorkflowLangResourceKind.ConnectDuplicate => "Already connected",
        WorkflowLangResourceKind.ConnectFull => "This port takes no more connections",
        WorkflowLangResourceKind.ConnectType => "Port types don't match",
        WorkflowLangResourceKind.ConnectCycle => "This would create a loop",
        WorkflowLangResourceKind.AnnounceConnected => "Connected {0} to {1}",
        WorkflowLangResourceKind.AnnounceDisconnected => "Disconnected {0} from {1}",
        WorkflowLangResourceKind.AnnounceRefused => "Can't connect: {0}",
        WorkflowLangResourceKind.AnnounceConnectMode => "Connecting from {0} to {1}: arrow keys choose a step, Enter connects, Escape cancels",
        WorkflowLangResourceKind.AnnounceConnectPort => "Output {0}: left and right choose an output, Enter continues",
        WorkflowLangResourceKind.AnnounceConnectNoTarget => "No step can take this connection",
        WorkflowLangResourceKind.AnnounceConnectCancelled => "Connection cancelled",
        WorkflowLangResourceKind.AnnounceUndo => "Undone",
        WorkflowLangResourceKind.AnnounceRedo => "Redone",
        WorkflowLangResourceKind.AnnounceDeletedEdgesOne => "Deleted 1 connection",
        WorkflowLangResourceKind.AnnounceDeletedEdgesOther => "Deleted {0} connections",
        WorkflowLangResourceKind.CanvasHelpEditable => "Arrow keys move between steps, Alt with an arrow follows the connections, Enter opens a step, Space selects, M moves the selected steps, C connects the focused step, Delete removes the selection, Ctrl+C and Ctrl+V copy and paste, Ctrl+Z undoes, plus and minus zoom, Shift+1 fits the workflow.",
        WorkflowLangResourceKind.AnnounceDeletedOne => "Deleted 1 step",
        WorkflowLangResourceKind.AnnounceDeletedOther => "Deleted {0} steps",
        WorkflowLangResourceKind.AnnounceMovedOne => "Moved 1 step",
        WorkflowLangResourceKind.AnnounceMovedOther => "Moved {0} steps",
        WorkflowLangResourceKind.AnnounceMoveMode => "Moving: arrow keys move, Enter places, Escape cancels",
        WorkflowLangResourceKind.AnnounceMoveCancelled => "Move cancelled",
        WorkflowLangResourceKind.CardSelected => "selected",
        WorkflowLangResourceKind.AnnounceCopiedOne => "Copied 1 step",
        WorkflowLangResourceKind.AnnounceCopiedOther => "Copied {0} steps",
        WorkflowLangResourceKind.AnnounceCutOne => "Cut 1 step",
        WorkflowLangResourceKind.AnnounceCutOther => "Cut {0} steps",
        WorkflowLangResourceKind.AnnounceNothingToPaste => "Nothing to paste",
        WorkflowLangResourceKind.AnnouncePastedOne => "Pasted 1 step",
        WorkflowLangResourceKind.AnnouncePastedOther => "Pasted {0} steps",
        _ => kind.ToString()
    };
}

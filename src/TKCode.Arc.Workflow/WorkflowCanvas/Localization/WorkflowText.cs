using System.Globalization;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>The workflow controls' built-in text in the current Arc language (<c>Localization/*.xlf</c>).</summary>
internal static class WorkflowText
{
    public static ArcLanguageManager? Languages => ArcLanguageManager.Current;

    /// <summary>The culture of the current Arc language (the current culture before the application starts).</summary>
    public static CultureInfo FormattingCulture => ArcLanguage.Culture;

    public static string Get(WorkflowLangResourceKind kind) => WorkflowLangResources.Get(kind);

    public static string Format(WorkflowLangResourceKind kind, params object?[] arguments) =>
        WorkflowLangResources.Format(kind, arguments);

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
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentRoutinesEventsDemo : UserControl
{
    public AgentRoutinesEventsDemo()
    {
        InitializeComponent();
        var count = 0;
        List.RunHandler = async (routine, cancellation) =>
        {
            var started = DateTimeOffset.Now;
            await Task.Delay(1200, cancellation);
            return ++count % 2 == 0
                ? new AgentRoutineRun(started, AgentRoutineOutcome.Failed, DateTimeOffset.Now - started, "Stopped: the inbox could not be reached")
                : new AgentRoutineRun(started, AgentRoutineOutcome.Succeeded, DateTimeOffset.Now - started, "Posted a digest of 9 messages");
        };
        Log.Text = "Flip a switch or press Run now.";
        List.EnabledChanged += (_, e) => Log.Text = $"EnabledChanged: {e.Routine.Name} is {(e.Routine.IsEnabled ? "on" : "paused")}";
        List.RunCompleted += (_, e) => Log.Text = $"RunCompleted: {e.Routine.Name} {e.Run?.Outcome}";
    }
}

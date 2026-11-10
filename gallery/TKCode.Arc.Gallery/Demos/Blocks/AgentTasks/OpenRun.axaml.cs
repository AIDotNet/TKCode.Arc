using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentTasksOpenRunDemo : UserControl
{
    public AgentTasksOpenRunDemo()
    {
        InitializeComponent();
        var tasks = new ObservableCollection<AgentTaskItem>(AgentTasksSamples.Tasks(DateTimeOffset.Now));
        Board.Tasks = tasks;
        Log.Text = "Press a card to open its run.";
        Board.TaskOpened += (_, e) => Log.Text = $"TaskOpened: {e.Task.Id} \"{e.Task.Title}\" ({e.Task.Agent}, {e.Task.Status})";
    }
}

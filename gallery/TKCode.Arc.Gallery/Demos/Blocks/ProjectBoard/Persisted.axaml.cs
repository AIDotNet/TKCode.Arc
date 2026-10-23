using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProjectBoardPersistedDemo : UserControl
{
    private readonly List<string> _lines = new();

    public ProjectBoardPersistedDemo()
    {
        InitializeComponent();
        ProjectBoardDemoData.Fill(Board);
        Board.TaskMoving += (_, e) =>
        {
            // A rule from the app: a task goes to review only once someone has commented on it.
            if (e.ToStage.Key == "review" && e.Task.CommentCount == 0)
            {
                e.Cancel = true;
                Write($"Kept “{e.Task.Title}” in {e.FromStage.Header}: review needs a comment first");
            }
        };
        Board.TaskMoved += (_, e) =>
            Write($"PATCH /tasks/{Short(e.Task.Key)}  stage: {e.FromStage.Key} → {e.ToStage.Key} (position {e.ToIndex + 1})");
        Board.TaskAdded += (_, e) =>
            Write($"POST /tasks  “{e.Task.Title}” in {e.Stage.Key}");
    }

    private static string Short(string key) => key.Length > 6 ? key[..6] : key;

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 5)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}

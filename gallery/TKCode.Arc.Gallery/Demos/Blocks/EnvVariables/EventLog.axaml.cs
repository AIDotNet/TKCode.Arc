using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EnvVariablesEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public EnvVariablesEventLogDemo()
    {
        InitializeComponent();
        EnvVariablesDemoData.Fill(Block);
        Block.RedeployAction = (_, cancellation) => Task.Delay(800, cancellation);

        Block.VariablesChanged += (_, e) => Write($"VariablesChanged {e.Reason}: {string.Join(", ", e.Affected.Select(v => v.Key))} ({e.Snapshot.Count} rows)");
        Block.ScopeToggled += (_, e) => Write($"ScopeToggled {e.Variable.Key} {e.Environment} → {(e.IsOn ? "on" : "off")}");
        Block.ScopeConflict += (_, e) => Write($"ScopeConflict {e.Variable.Key} {e.Environment}");
        Block.VariableSaved += (_, e) => Write($"VariableSaved {e.Variable.Key} changed={e.ValueChanged} sensitive={e.BecameSensitive}");
        Block.VariableAdded += (_, e) => Write($"VariableAdded {e.Variable.Key}");
        Block.VariableRemoved += (_, e) => Write($"VariableRemoved {e.Variable.Key} at {e.Index}");
        Block.VariablesImported += (_, e) => Write($"VariablesImported {e.Added.Count} added, lines skipped: {string.Join(", ", e.SkippedLines)}");
        Block.ValueCopied += (_, e) => Write($"ValueCopied {e.Variable.Key} {e.From} → {e.To}");
        Block.ValueRevealed += (_, e) => Write($"ValueRevealed {e.Variable.Key} {(e.IsRevealed ? "shown" : "hidden")}");
        Block.RedeployRequested += (_, e) => Write($"RedeployRequested {e.Request.ChangeCount} change(s)");
        Block.Redeployed += (_, e) => Write($"Redeployed {string.Join(", ", e.Request.Environments)}");
        Block.PropertyChanged += (_, e) =>
        {
            if (e.Property == EnvVariables.ViewModeProperty || e.Property == EnvVariables.RedeployStateProperty)
            {
                View.Text = $"View: {Block.ViewMode} · Banner: {Block.RedeployState}";
            }
        };
        View.Text = $"View: {Block.ViewMode} · Banner: {Block.RedeployState}";
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 8)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}

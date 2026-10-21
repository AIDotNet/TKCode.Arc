using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InlineEditRenameDemo : UserControl
{
    public InlineEditRenameDemo()
    {
        InitializeComponent();
        ProjectName.Validator = value => value.Length == 0 ? "A project needs a name." : null;
        ProjectName.SaveHandler = SaveAsync;
    }

    // Stands in for a request; turn on the switch to see a failed save roll back.
    private async Task SaveAsync(string next)
    {
        await Task.Delay(700);
        if (FailNext.IsChecked == true)
        {
            FailNext.IsChecked = false;
            throw new InvalidOperationException("Network unavailable");
        }
    }

    private void OnTextCommitted(object? sender, InlineEditCommittedEventArgs e)
        => Log.Text = $"Committed “{e.NewValue}” (was “{e.OldValue}”)";

    private void OnSaved(object? sender, InlineEditCommittedEventArgs e)
        => Log.Text = $"Saved “{e.NewValue}”";

    private void OnSaveFailed(object? sender, InlineEditSaveFailedEventArgs e)
        => Log.Text = $"Save failed: {e.Error.Message}";
}

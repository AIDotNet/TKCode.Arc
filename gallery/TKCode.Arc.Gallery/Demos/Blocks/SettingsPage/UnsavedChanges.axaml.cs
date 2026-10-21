using Avalonia.Controls;
using TKCode.Arc;

namespace TKCode.Arc.Gallery.Demos;

public partial class SettingsPageUnsavedChangesDemo : UserControl
{
    private string _savedName;

    public SettingsPageUnsavedChangesDemo()
    {
        InitializeComponent();
        _savedName = WorkspaceName.Text ?? string.Empty;
        WorkspaceName.TextChanged += (_, _) => General.IsDirty = WorkspaceName.Text != _savedName;
        Page.SaveRequested += (_, e) =>
        {
            _savedName = WorkspaceName.Text ?? string.Empty;
            e.Section.IsDirty = false;
            Log.Text = $"Saved \"{_savedName}\" (Save button or Ctrl/⌘+S).";
        };
        Page.DiscardRequested += (_, _) =>
        {
            WorkspaceName.Text = _savedName;
            Log.Text = "Discarded the change.";
        };
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Saves the layout as JSON into the text box and loads whatever the box holds back into the layout.</summary>
public partial class DockLayoutSaveAndRestoreDemo : UserControl
{
    public DockLayoutSaveAndRestoreDemo()
    {
        InitializeComponent();
        Json.Text = Layout.SaveLayout();
        SaveButton.Click += OnSave;
        RestoreButton.Click += OnRestore;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Json.Text = Layout.SaveLayout();
        Status.Text = "Layout saved.";
    }

    private void OnRestore(object? sender, RoutedEventArgs e)
    {
        Status.Text = Layout.LoadLayout(Json.Text ?? string.Empty)
            ? "Layout restored."
            : "That text is not a saved layout.";
    }
}

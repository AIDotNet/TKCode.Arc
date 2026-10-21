using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConfirmMorphPressDemo : UserControl
{
    public ConfirmMorphPressDemo()
    {
        InitializeComponent();
        Delete.ConfirmAction = () => Task.Delay(900);
        Delete.UndoAction = () => Task.Delay(600);
    }

    private void OnStateChanged(object? sender, ConfirmMorphStateChangedEventArgs e)
    {
        Log.Text = $"State: {e.NewState}";
    }
}

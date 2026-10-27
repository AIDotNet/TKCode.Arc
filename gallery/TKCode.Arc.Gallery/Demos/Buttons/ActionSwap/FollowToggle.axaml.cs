using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionSwapFollowToggleDemo : UserControl
{
    public ActionSwapFollowToggleDemo()
    {
        InitializeComponent();
        Follow.AsyncAction = async (_, _) => await Task.Delay(700);
    }

    private void OnValueChanged(object? sender, ActionSwapValueChangedEventArgs e)
    {
        Message.Text = e.Value == "following" ? "Following Maya Chen" : "Not following";
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmptyStatesCustomItemsDemo : UserControl
{
    public EmptyStatesCustomItemsDemo()
    {
        InitializeComponent();
    }

    private async void OnActionInvoked(object? sender, EmptyStatesActionEventArgs e)
    {
        if (e.Item.Scene == EmptyStatesScene.NoResults)
        {
            // Showing or hiding paid invoices is instant: toggle between the two phases.
            States.Phase = e.Phase == EmptyStatesPhase.Idle ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
            return;
        }
        States.Phase = EmptyStatesPhase.Loading;
        await Task.Delay(900);
        if (States.SelectedItem == e.Item && States.Phase == EmptyStatesPhase.Loading)
        {
            States.Phase = EmptyStatesPhase.Done;
        }
    }
}

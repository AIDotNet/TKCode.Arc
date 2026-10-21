using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EmptyStatesPreviewDemo : UserControl
{
    private int _run;

    public EmptyStatesPreviewDemo()
    {
        InitializeComponent();
    }

    // Arc's preview simulation: filters clear and come back at once; a retry, a mail check and a page search load for a
    // moment and then finish. A finished retry or search goes back to the empty state on the next press; the inbox
    // always checks again.
    private async void OnActionInvoked(object? sender, EmptyStatesActionEventArgs e)
    {
        var scene = e.Item.Scene;
        if (scene == EmptyStatesScene.NoResults || (e.Phase == EmptyStatesPhase.Done && scene != EmptyStatesScene.CaughtUp))
        {
            States.Phase = e.Phase == EmptyStatesPhase.Idle ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
            return;
        }
        var run = ++_run;
        States.Phase = EmptyStatesPhase.Loading;
        await Task.Delay(scene switch
        {
            EmptyStatesScene.Offline => 1400,
            EmptyStatesScene.CaughtUp => 1100,
            _ => 1200
        });
        // Switching tabs meanwhile puts the block back to Idle; only finish the run that is still loading.
        if (run == _run && States.SelectedItem == e.Item && States.Phase == EmptyStatesPhase.Loading)
        {
            States.Phase = EmptyStatesPhase.Done;
        }
    }
}

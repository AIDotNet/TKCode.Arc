using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Selectable toasts with a session limit, logging Selected, Dismissed and Shown.</summary>
public partial class SocialProofToastSelectableDemo : UserControl
{
    private int _shown;

    public SocialProofToastSelectableDemo()
    {
        InitializeComponent();
        Stack.Selected += (_, e) => Log.Text = "Selected: open the story for " + e.Event.Name;
        Stack.Dismissed += (_, e) => Log.Text = "Dismissed: " + e.Event.Name;
        Stack.Shown += (_, e) =>
        {
            _shown++;
            Log.Text = $"Shown {_shown} of 5: {e.Event.Name}";
        };
    }
}

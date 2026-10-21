using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DialogStepsDemo : UserControl
{
    private static readonly string[] Titles = ["Invite your team", "Choose a role", "Send invites"];

    private static readonly string[] Descriptions =
        ["Add people by email.", "Editors can change content; viewers only read.", "We will email everyone a link."];

    private int _step;

    public DialogStepsDemo()
    {
        InitializeComponent();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _step = 0;
        ShowStep();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        _step++;
        if (_step >= Titles.Length)
        {
            InviteDialog.Close(true);
            return;
        }
        ShowStep();
    }

    // The new title and description crossfade in place while the dialog stays open.
    private void ShowStep()
    {
        InviteDialog.Title = Titles[_step];
        InviteDialog.Description = Descriptions[_step];
        Step.Text = $"Step {_step + 1} of {Titles.Length}";
        Next.Content = _step == Titles.Length - 1 ? "Send" : "Next";
    }
}

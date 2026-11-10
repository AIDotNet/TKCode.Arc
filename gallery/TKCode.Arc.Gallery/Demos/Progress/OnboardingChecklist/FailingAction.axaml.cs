using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OnboardingChecklistFailingActionDemo : UserControl
{
    private int _attempts;

    public OnboardingChecklistFailingActionDemo()
    {
        InitializeComponent();
        Checklist.Steps[0].Action = ConnectAsync;
    }

    private async Task ConnectAsync()
    {
        await Task.Delay(700);
        if (++_attempts == 1)
        {
            throw new InvalidOperationException("The payment provider did not answer. Try again.");
        }
    }
}

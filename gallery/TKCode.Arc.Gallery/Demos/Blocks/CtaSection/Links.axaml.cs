using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CtaSectionLinksDemo : UserControl
{
    public CtaSectionLinksDemo()
    {
        InitializeComponent();
    }

    // The mail link is handled here, so the block does not open it; the web link is not, so the launcher opens it.
    private void OnActionInvoked(object? sender, CtaActionEventArgs e)
    {
        var kind = e.IsPrimary ? "Primary" : "Secondary";
        var handled = false;
        if (!e.IsPrimary)
        {
            e.Handled = true;
            handled = true;
        }
        Log.Text = $"{kind} “{e.Action.Label}” pressed" + (handled ? " (handled in code, nothing opened)." : " (the launcher opens the page).");
    }
}

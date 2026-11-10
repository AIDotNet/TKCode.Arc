using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentGalleryInstallDemo : UserControl
{
    public AgentGalleryInstallDemo()
    {
        InitializeComponent();
        Gallery.InstallAsync = AgentGallerySamples.FailingInstall(TimeSpan.FromMilliseconds(900));
        Gallery.AgentInstalled += (_, e) => Log.Text = $"Installed {e.Agent.Name}";
        Gallery.AgentUninstalled += (_, e) => Log.Text = $"Removed {e.Agent.Name}";
        Gallery.InstallFailed += (_, e) => Log.Text = $"{e.Agent.Name} failed: {e.Message}";
        Gallery.PromptChosen += (_, e) => Log.Text = $"Start a chat with {e.Agent.Name}: “{e.Prompt}”";
    }
}

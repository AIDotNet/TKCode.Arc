using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProfilePageControlledTabDemo : UserControl
{
    public ProfilePageControlledTabDemo()
    {
        InitializeComponent();
        ProfilePageDemoData.Fill(Page);
        ShowActivity.Click += (_, _) => Page.Tab = ProfilePageTab.Activity;
        ShowProjects.Click += (_, _) => Page.Tab = ProfilePageTab.Projects;

        Page.TabChanged += (_, e) => Log.Text = $"TabChanged: {e.OldTab} → {e.NewTab}";
        Page.StarChanged += (_, e) =>
            Log.Text = $"StarChanged: {e.Project.Title} {(e.IsStarred ? "starred" : "unstarred")} · {e.StarCount:N0} stars";
        Page.Shared += (_, e) => Log.Text = e.Succeeded ? $"Shared: copied {e.Url}" : "Shared: the clipboard refused the link";
        Page.Saved += (_, e) => Log.Text = $"Saved: {(e.ChangedFields == ProfileFields.None ? "nothing changed" : e.ChangedFields.ToString())}";
    }
}

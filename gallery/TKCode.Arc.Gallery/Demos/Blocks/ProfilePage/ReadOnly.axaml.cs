using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProfilePageReadOnlyDemo : UserControl
{
    public ProfilePageReadOnlyDemo()
    {
        InitializeComponent();
        Page.Profile = ProfilePageDemoData.Colleague();
        Page.ProfileUrl = "https://fieldnote.app/@imani";
        Page.Stats.AddRange([new ProfileStat("Followers", 1284), new ProfileStat("Following", 96), new ProfileStat("Studies", 27)]);
        Page.Activity.AddRange(ProfilePageDemoData.Activity().Take(3));
        Page.Projects.AddRange(ProfilePageDemoData.Projects().Skip(1).Take(2));
    }
}

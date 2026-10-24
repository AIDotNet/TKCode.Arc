using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginSplitCustomDemo : UserControl
{
    public LoginLoginSplitCustomDemo()
    {
        InitializeComponent();
        Split.Labels = LoginSplitDemoData.NorthdeskLabels();
        Split.Photo = LoginSplitDemoData.CoastPhoto();
        Split.Providers.Clear();
        Split.Providers.Add(LoginSplitProvider.Google());
        Split.Providers.Add(new LoginSplitProvider("Work SSO") { Icon = LoginSplitDemoData.WorkMark });
    }
}

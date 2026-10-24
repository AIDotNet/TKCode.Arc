using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginSignInPreviewDemo : UserControl
{
    public LoginSignInPreviewDemo()
    {
        InitializeComponent();
        // Arc's teammates keep their photo when their address is typed (emma@…, marcus@…); these portraits are drawn.
        foreach (var teammate in SignInDemoData.Teammates())
        {
            Card.Accounts.Add(teammate);
        }
    }
}

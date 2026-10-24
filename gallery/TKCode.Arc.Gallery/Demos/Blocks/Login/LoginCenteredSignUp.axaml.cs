using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginCenteredSignUpDemo : UserControl
{
    public LoginLoginCenteredSignUpDemo()
    {
        InitializeComponent();
    }

    // New accounts go to onboarding, returning people to the app (Arc's "Route sign ups to onboarding" example).
    private void OnSignedIn(object? sender, LoginCenteredSignedInEventArgs e)
    {
        var route = e.Mode == LoginCenteredMode.SignUp ? "/welcome" : "/app";
        Route.Text = $"{e.Email} ({e.Method}) would open {route}";
    }

    private void OnSignedOut(object? sender, RoutedEventArgs e) => Route.Text = "Signed out.";
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginCenteredConnectedDemo : UserControl
{
    private readonly List<string> _log = new();

    public LoginLoginCenteredConnectedDemo()
    {
        InitializeComponent();
        // Ask the platform for a passkey (WebAuthn); here a fake device answers after a second.
        Login.PasskeyAsync = async token =>
        {
            Write("passkey requested");
            await Task.Delay(1000, token);
            return "robin@park.dev";
        };
        // Send the code with your mailer; the switch makes the next send fail.
        Login.SendCodeAsync = async (email, mode, token) =>
        {
            Write($"send code to {email} ({mode})");
            await Task.Delay(600, token);
            if (Fail.IsChecked == true)
            {
                Fail.IsChecked = false;
                throw new InvalidOperationException("Mailer unavailable");
            }
        };
        // Check the code on your server.
        Login.VerifyCodeAsync = async (email, code, token) =>
        {
            Write($"check {code} for {email}");
            await Task.Delay(500, token);
            return code == "424242";
        };
        // Start the provider's OAuth flow and return the account it resolves to.
        Login.SignInWithProviderAsync = async (provider, token) =>
        {
            Write($"OAuth with {provider}");
            await Task.Delay(800, token);
            return "robin@park.dev";
        };
        Login.SignedIn += (_, e) => Write($"signed in: {e.Email}, {e.Method}, {e.Mode}");
    }

    // A real app opens its own terms page; the demo only logs the request.
    private void OnNavigate(object? sender, LoginCenteredNavigateEventArgs e)
    {
        e.Handled = true;
        Write($"open {e.Href}");
    }

    private void Write(string line)
    {
        _log.Add(line);
        if (_log.Count > 4)
        {
            _log.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _log);
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginImmersiveEventsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public LoginLoginImmersiveEventsDemo()
    {
        InitializeComponent();
        Login.Photo = LoginImmersiveDemoData.Harbor();
        Login.PhotoAlignment = new Avalonia.Point(0.5, 0.7);
        // Send the link with your mailer; throwing shows an error under the field.
        Login.SendLinkAsync = async (email, mode, token) =>
        {
            Add($"send link to {email} ({mode})");
            await Task.Delay(700, token);
            if (Fail.IsChecked == true)
            {
                Fail.IsChecked = false;
                throw new InvalidOperationException("Mailer unavailable");
            }
        };
        // Open the person's mail app; true once your server saw the link followed (here: after a moment).
        Login.OpenMailAppAsync = async (email, token) =>
        {
            Add($"open mail app for {email}");
            await Task.Delay(900, token);
            return true;
        };
        // Run the provider's OAuth flow and return the account it resolves to (null: the person cancelled).
        Login.SignInWithProviderAsync = async (provider, email, token) =>
        {
            Add($"OAuth with {provider.Name}");
            await Task.Delay(800, token);
            return email ?? "mara@harbordesk.app";
        };
        Login.LinkSent += (_, e) => Add($"LinkSent    {e.Email}{(e.IsResend ? " (again)" : "")}");
        Login.SignedIn += (_, e) => Add($"SignedIn    {e.Email}, {(e.Provider?.Name ?? "magic link")}, {e.Mode}");
        Login.SignedOut += (_, _) => Add("SignedOut");
        // A real app opens its terms page; the demo only logs the request.
        Login.Navigate += (_, e) =>
        {
            e.Handled = true;
            Add($"Navigate    {e.Link} {e.Href}");
        };
    }

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}

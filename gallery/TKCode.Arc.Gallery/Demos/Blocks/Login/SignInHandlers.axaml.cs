using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginSignInHandlersDemo : UserControl
{
    private readonly List<string> _log = new();

    public LoginSignInHandlersDemo()
    {
        InitializeComponent();
        // Send and check codes through your auth provider; here a fake service answers after a short wait.
        Card.SendCodeAsync = async (email, token) =>
        {
            Write($"send code to {email}");
            await Task.Delay(600, token);
            return null; // derive the name from the address
        };
        Card.VerifyCodeAsync = async (account, code, token) =>
        {
            Write($"check {code} for {account.Email}");
            await Task.Delay(500, token);
            return code == "424242";
        };
        Card.SignInWithAsync = async (method, token) =>
        {
            Write($"sign in with {method}");
            await Task.Delay(700, token);
            return new SignInAccount("Robin Park", "robin@park.dev");
        };
    }

    private void OnSignedIn(object? sender, SignInSignedInEventArgs e) => Write($"SignedIn: {e.Account.Name} with {e.Method}");

    private void OnSignedOut(object? sender, SignInSignedOutEventArgs e) => Write($"SignedOut: {e.Account.Email}");

    // Handled: the app opens its sign up flow, so the card does not say "Sign up opens in your app".
    private void OnSignUpRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Write("SignUpRequested: open the sign up page");
    }

    private void Write(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 4)
        {
            _log.RemoveAt(_log.Count - 1);
        }
        Log.Text = string.Join("\n", _log);
    }
}

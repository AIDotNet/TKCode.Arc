using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginSignupFormHandlersDemo : UserControl
{
    public LoginSignupFormHandlersDemo()
    {
        InitializeComponent();
        Form.SubmitAsync = CreateAccountAsync;
    }

    // Stands in for a call to your sign-up endpoint: a thrown message shows above Create account.
    private static async Task CreateAccountAsync(SignupFormDetails details, CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
        if (string.Equals(details.Email, "taken@northwind.studio", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That email is already registered.");
        }
    }

    private void OnAccountCreated(object? sender, SignupFormCompletedEventArgs e)
        => Write($"Account created for {e.Email} with {e.Method}; product updates {(e.ProductUpdates ? "on" : "off")}.");

    // Start your OAuth redirect here; without ProviderAsync the card simulates the provider.
    private void OnProviderSelected(object? sender, SignupFormProviderEventArgs e) => Write($"Provider picked: {e.Provider}.");

    private void OnNavigate(object? sender, SignupFormNavigateEventArgs e)
    {
        Write($"Link pressed: {e.Link}.");
        e.Handled = true;
    }

    private void Write(string line) => Log.Text = line;
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NewsletterSignupPreviewDemo : UserControl
{
    public NewsletterSignupPreviewDemo()
    {
        InitializeComponent();
        Rebuild();
    }

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e) => Rebuild();

    // Arc keys the block by its variant, so a switch starts a fresh signup with the other layout.
    private void Rebuild()
    {
        if (Host is null)
        {
            return;
        }
        var card = Layout.SelectedValue as string == "card";
        var signup = new NewsletterSignup
        {
            Variant = card ? NewsletterVariant.Card : NewsletterVariant.Inline,
            Publication = NewsletterSignupDemoData.CreatePublication(),
            SubscribeAsync = SimulateAsync
        };
        signup.ReaderFaces.AddRange(NewsletterSignupDemoData.CreateFaces());
        Host.Content = signup;
    }

    // Fails once after 1100 ms while the switch is on, otherwise succeeds after the same time.
    private async Task SimulateAsync(string email, CancellationToken token)
    {
        await Task.Delay(1100, token);
        if (Fail.IsChecked == true)
        {
            throw new InvalidOperationException("Simulated failure");
        }
    }
}

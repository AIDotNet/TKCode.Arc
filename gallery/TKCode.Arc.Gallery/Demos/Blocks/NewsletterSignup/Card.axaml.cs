using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NewsletterSignupCardDemo : UserControl
{
    public NewsletterSignupCardDemo()
    {
        InitializeComponent();
        Signup.Publication = NewsletterSignupDemoData.CreatePublication();
        Signup.ReaderFaces.AddRange(NewsletterSignupDemoData.CreateFaces());
    }

    private void OnSubscribed(object? sender, NewsletterSubscribedEventArgs e)
    {
        // A real app would store the address here; the demo only simulates the send.
    }
}

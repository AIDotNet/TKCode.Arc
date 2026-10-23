using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NewsletterSignupNarrowDemo : UserControl
{
    public NewsletterSignupNarrowDemo()
    {
        InitializeComponent();
        Signup.ReaderFaces.AddRange(NewsletterSignupDemoData.CreateFaces());
    }
}

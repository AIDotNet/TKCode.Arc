using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: a reply with simulated feedback.</summary>
public partial class ResponseFeedbackPreviewDemo : UserControl
{
    public ResponseFeedbackPreviewDemo()
    {
        InitializeComponent();
        // A paragraph, a list and inline code: a reply of your own can be any control.
        Card.Reply = ResponseFeedbackDemoData.Reply();
    }
}

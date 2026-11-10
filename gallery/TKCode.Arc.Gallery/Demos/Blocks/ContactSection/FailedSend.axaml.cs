using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ContactSectionFailedSendDemo : UserControl
{
    public ContactSectionFailedSendDemo()
    {
        InitializeComponent();
        Section.SubmitHandler = async _ =>
        {
            await Task.Delay(800);
            throw new InvalidOperationException("The server is down.");
        };
    }
}

using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PasswordFieldSignInDemo : UserControl
{
    public PasswordFieldSignInDemo()
    {
        InitializeComponent();
        Password.PropertyChanged += (_, e) =>
        {
            if (e.Property == PasswordField.IsRevealedProperty)
            {
                State.Text = Password.IsRevealed ? "Shown" : "Hidden";
            }
        };
    }
}

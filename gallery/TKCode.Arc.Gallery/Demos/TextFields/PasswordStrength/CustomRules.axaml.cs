using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PasswordStrengthCustomRulesDemo : UserControl
{
    public PasswordStrengthCustomRulesDemo()
    {
        InitializeComponent();
        Passphrase.Rules = PasswordRules.Create(minLength: 16);
    }
}

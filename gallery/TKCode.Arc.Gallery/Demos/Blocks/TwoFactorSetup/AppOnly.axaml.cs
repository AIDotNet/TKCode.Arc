using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TwoFactorSetupAppOnlyDemo : UserControl
{
    public TwoFactorSetupAppOnlyDemo()
    {
        InitializeComponent();
        // A fresh random secret, as a server would create per person; scan it with any authenticator app.
        Setup.Secret = TwoFactorSetup.GenerateSecret();
        foreach (var code in TwoFactorSetup.GenerateBackupCodes())
        {
            Setup.BackupCodes.Add(code);
        }
        Hint.Text = "This card made a new secret just now, so the QR code is different on every visit.";
    }
}

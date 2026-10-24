using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LoginLoginSplitHandlersDemo : UserControl
{
    private static readonly LoginSplitAccount[] s_team = LoginSplitDemoData.Team();

    public LoginLoginSplitHandlersDemo()
    {
        InitializeComponent();
        Split.LookupAccountAsync = async (email, token) =>
        {
            await Task.Delay(400, token);
            if (!email.EndsWith("@fieldnote.app", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("No account for this address.");
            }
            return s_team.FirstOrDefault(a => a.Email == email);
        };
        Split.VerifyPasswordAsync = async (_, password, token) =>
        {
            await Task.Delay(600, token);
            return password == "correct horse";
        };
        Split.SignInWithProviderAsync = async (_, email, token) =>
        {
            await Task.Delay(700, token);
            return email is null ? s_team[0] : s_team.FirstOrDefault(a => a.Email == email) ?? throw new InvalidOperationException("Not on the team.");
        };
        Split.SendResetLinkAsync = (_, token) => Task.Delay(500, token);
    }
}

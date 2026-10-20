using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class PasswordStrengthPage
{
    public static Control Create()
    {
        var create = new PasswordStrength { Label = "New password", Width = 400, HorizontalAlignment = HorizontalAlignment.Left };
        var result = GalleryUi.Text("Strength: none yet", ArcDesignTokenKind.TextSecondary);
        create.StrengthChanged += (_, e) => result.Text = e.Level == 0 ? "Strength: none yet" : $"Strength: {e.Label} (level {e.Level}, {e.Score.MetRuleIds.Count} rules met)";

        var custom = new PasswordStrength
        {
            Label = "Team passphrase",
            Rules = PasswordRules.Create(minLength: 16),
            ShortLength = 10,
            Width = 400,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var withError = new PasswordStrength { Label = "Password", Text = "password", Width = 400, HorizontalAlignment = HorizontalAlignment.Left };
        var submit = new Button { Content = "Submit", Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Left };
        var attempts = 0;
        submit.Click += (_, _) =>
        {
            attempts++;
            // Each new error shakes the field once.
            withError.ErrorMessage = withError.Level < 3
                ? (attempts % 2 == 1 ? "Choose a stronger password." : "That password is too easy to guess.")
                : null;
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Create a password", "Segments fill in a wave and change tone, the word rises or drops with strength, rules tick off as you type. Scoring runs on the device.",
                GalleryUi.Column(12, create, result)),
            GalleryUi.Card("Custom rules", "PasswordRules.Create(minLength: 16) with a 10-character short length.", custom),
            GalleryUi.Card("Errors shake", "Submit a weak password: every new error shakes the field once (full motion only).", GalleryUi.Column(16, withError, submit)));
    }
}

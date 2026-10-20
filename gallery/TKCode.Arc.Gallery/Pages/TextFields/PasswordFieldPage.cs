using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class PasswordFieldPage
{
    public static Control Create()
    {
        var signIn = new PasswordField
        {
            Label = "Password",
            Placeholder = "Your password",
            Description = "Use the reveal toggle to check what you typed.",
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var state = GalleryUi.Text("Hidden", ArcDesignTokenKind.TextSecondary);
        signIn.PropertyChanged += (_, e) =>
        {
            if (e.Property == PasswordField.IsRevealedProperty)
            {
                state.Text = signIn.IsRevealed ? "Shown" : "Hidden";
            }
        };

        var confirm = new PasswordField { Label = "Confirm password", Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var first = new PasswordField { Label = "New password", Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        void Check() => confirm.ErrorMessage = !string.IsNullOrEmpty(confirm.Text) && confirm.Text != first.Text
            ? "The passwords do not match yet."
            : null;
        first.TextChanged += (_, _) => Check();
        confirm.TextChanged += (_, _) => Check();

        return GalleryUi.Column(20,
            GalleryUi.Card("Sign in", "A slash draws across the eye instead of swapping icons; after the first toggle the text resolves from a light blur. Enter or Space on the toggle.",
                GalleryUi.Column(12, signIn, state)),
            GalleryUi.Card("Confirm", "A shared error row under the second field.", GalleryUi.Column(16, first, confirm)),
            GalleryUi.Card("Sizes and states", null, GalleryUi.Column(16,
                new PasswordField { Label = "Small", Size = ControlSize.Small, Text = "secret", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
                new PasswordField { Label = "Disabled", Text = "secret", IsEnabled = false, Width = 360, HorizontalAlignment = HorizontalAlignment.Left })));
    }
}

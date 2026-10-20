using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class CookieConsentPage
{
    public static Control Create()
    {
        var consent = new CookieConsent
        {
            Storage = new InMemoryMessageStorage(),
            StorageKey = "demo-consent",
            Policy = new CookieConsentPolicy { Label = "Cookie policy" }
        };
        var page = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 8,
            Children =
            {
                GalleryUi.Text("Acme Store", ArcDesignTokenKind.Foreground, 22, Avalonia.Media.FontWeight.Medium),
                GalleryUi.Text("The layer lets clicks through everywhere except on the consent surface.", ArcDesignTokenKind.TextSecondary)
            }
        };
        var stage = new Border
        {
            Height = 520,
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new Grid { Children = { page, consent } }
        };
        ArcTokens.Bind(stage, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(stage, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var state = GalleryUi.Text("Not decided yet.", ArcDesignTokenKind.TextMuted, 12);
        consent.ConsentChanged += (_, e) => state.Text = e.Consent is null
            ? "Reset: asking again."
            : $"{e.Action}: " + string.Join(", ", e.Consent.Select(p => $"{p.Key} {(p.Value ? "on" : "off")}"));

        Button Make(string label, Action run)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
            button.Click += (_, _) => run();
            return button;
        }

        var actions = GalleryUi.Row(
            Make("Cookie settings", consent.OpenPreferences),
            Make("Reset", consent.Reset),
            Make("Bottom", () => consent.Position = CookieConsentPosition.Bottom),
            Make("Bottom left", () => consent.Position = CookieConsentPosition.BottomLeft),
            Make("Bottom right", () => consent.Position = CookieConsentPosition.BottomRight));

        return GalleryUi.Column(20,
            GalleryUi.Card("Consent bar", "Customize morphs the bar into the preferences panel; a choice folds it into a 44px reopen button. Reject all is as easy as Accept all.",
                GalleryUi.Column(12, stage, actions, state)));
    }
}

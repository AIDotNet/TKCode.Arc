using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class StepperPage
{
    private static StepperStep[] Checkout() =>
    [
        new("Account", "Create your login") { Id = "account" },
        new("Shipping", "Where should we send it") { Id = "shipping" },
        new("Payment", "Card or invoice") { Id = "payment" },
        new("Review", "Check and confirm") { Id = "review" }
    ];

    public static Control Create()
    {
        var checkout = new Stepper { ItemsSource = Checkout(), CurrentIndex = 1, IsNavigable = true };
        var selected = GalleryUi.Text("Choose a completed step to go back.", ArcDesignTokenKind.TextSecondary);
        checkout.StepSelected += (_, e) => selected.Text = $"Returned to step {e.Index + 1}: {e.Step.Label}";
        var back = new Button { Content = "Back", Variant = ButtonVariant.Ghost };
        back.Click += (_, _) => checkout.CurrentIndex = Math.Max(0, checkout.CurrentIndex - 1);
        var next = new Button { Content = "Continue", Variant = ButtonVariant.Secondary };
        next.Click += (_, _) => checkout.CurrentIndex = Math.Min(4, checkout.CurrentIndex + 1);
        var jump = new Button { Content = "Finish all", Variant = ButtonVariant.Secondary };
        jump.Click += (_, _) => checkout.CurrentIndex = 4;

        var setupSteps = Checkout();
        var setup = new Stepper
        {
            ItemsSource = setupSteps,
            CurrentIndex = 2,
            Orientation = Orientation.Vertical,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var fail = new Button { Content = "Toggle error", Variant = ButtonVariant.Danger };
        fail.Click += (_, _) => setupSteps[2].Error = setupSteps[2].Error is null ? "Card declined. Try another card." : null;

        var compact = new Stepper { ItemsSource = Checkout(), CurrentIndex = 2, IsCompact = true };
        var currentOnly = new Stepper { ItemsSource = Checkout(), CurrentIndex = 1, Details = StepperDetails.Current };

        return GalleryUi.Column(20,
            GalleryUi.Card("Checkout", "Connectors fill one after another when progress jumps; numbers turn into drawn checks. Completed steps can be chosen by click or with the arrow keys and Enter.",
                GalleryUi.Column(16, checkout, GalleryUi.Row(back, next, jump), selected)),
            GalleryUi.Card("Vertical with error", "A failed step turns its marker into an alert and its message replaces the description.",
                GalleryUi.Column(16, setup, GalleryUi.Row(fail))),
            GalleryUi.Card("Compact", "Markers only, with the current step named underneath (also automatic below 30rem).", compact),
            GalleryUi.Card("Current details only", "Only the step in progress shows its description.", currentOnly));
    }
}

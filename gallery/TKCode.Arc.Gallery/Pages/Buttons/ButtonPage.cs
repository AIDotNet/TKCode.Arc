using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ButtonPage
{
    public static Control Create()
    {
        var variants = GalleryUi.Row(
            new Button { Content = "Save changes", Variant = ButtonVariant.Primary },
            new Button { Content = "Cancel", Variant = ButtonVariant.Secondary },
            new Button { Content = "Learn more", Variant = ButtonVariant.Ghost },
            new Button { Content = "Delete project", Variant = ButtonVariant.Danger });

        var sizes = GalleryUi.Row(
            new Button { Content = "Small", Size = ControlSize.Small, Variant = ButtonVariant.Secondary },
            new Button { Content = "Medium", Size = ControlSize.Medium, Variant = ButtonVariant.Secondary },
            new Button { Content = "Large", Size = ControlSize.Large, Variant = ButtonVariant.Secondary });

        var morph = new Button { Content = "Save", Variant = ButtonVariant.Primary };
        morph.Click += async (_, _) =>
        {
            morph.IsLoading = true;
            await Task.Delay(900);
            morph.IsLoading = false;
            morph.Content = "Saved";
            await Task.Delay(1600);
            morph.Content = "Save";
        };

        var disabled = GalleryUi.Row(
            new Button { Content = "Primary", IsEnabled = false },
            new Button { Content = "Secondary", Variant = ButtonVariant.Secondary, IsEnabled = false },
            new Button { Content = "Ghost", Variant = ButtonVariant.Ghost, IsEnabled = false },
            new Button { Content = "Danger", Variant = ButtonVariant.Danger, IsEnabled = false });

        return GalleryUi.Column(20,
            GalleryUi.Card("Variants", "Use one primary action per surface.", variants),
            GalleryUi.Card("Sizes", "Heights 36, 44 and 50. Pick large for primary touch targets.", sizes),
            GalleryUi.Card("Loading and label morph", "Click to see the spinner, then the label morph from Save to Saved while the width springs.", GalleryUi.Row(morph)),
            GalleryUi.Card("Disabled", "Outlined variants keep their shape so an unavailable action still reads as a button.", disabled));
    }
}

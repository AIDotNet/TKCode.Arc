using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ToastPage
{
    public static Control Create()
    {
        var toast = new Toast { Title = "Changes saved", Description = "Synced to all your devices.", VerticalAlignment = VerticalAlignment.Bottom };
        var stage = Stage(toast);
        var show = new Button { Content = "Save changes", Variant = ButtonVariant.Primary };
        show.Click += (_, _) =>
        {
            toast.Title = "Changes saved";
            toast.Description = "Synced to all your devices.";
            toast.IsOpen = true;
        };
        var morph = new Button { Content = "Change text", Variant = ButtonVariant.Secondary };
        morph.Click += (_, _) =>
        {
            toast.IsOpen = true;
            toast.Title = toast.Title == "Changes saved" ? "Draft published" : "Changes saved";
            toast.Description = toast.Description is null ? "Readers will see it in a few seconds, and subscribers get an email." : null;
        };
        var log = GalleryUi.Text("Swipe the toast left or right, or wait 4.5s.", ArcDesignTokenKind.TextMuted, 12);
        toast.Closed += (_, e) => log.Text = $"Closed: {e.Reason}";

        var app = GalleryUi.Row(
            AppButton("Success", ToastKind.Success, "Invite sent", "Maya will get an email shortly."),
            AppButton("Info", ToastKind.Info, "New comment", null),
            AppButton("Error", ToastKind.Error, "Upload failed", "The file is larger than 25 MB."));

        return GalleryUi.Column(20,
            GalleryUi.Card("Local toast", "One success confirmation controlled by IsOpen: it rises on the morph spring while its check mark draws in.",
                GalleryUi.Column(12, stage, GalleryUi.Row(show, morph), log)),
            GalleryUi.Card("From anywhere", "Toast.Show(owner, title, kind) sends app-wide toasts to the window's ToastStack (created on its overlay layer when there is none).", app));
    }

    private static Button AppButton(string label, ToastKind kind, string title, string? description)
    {
        var button = new Button { Content = label, Variant = ButtonVariant.Secondary };
        button.Click += (_, _) => Toast.Show(button, title, kind, description);
        return button;
    }

    internal static Border Stage(Control content)
    {
        var stage = new Border
        {
            Height = 200,
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = content
        };
        ArcTokens.Bind(stage, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(stage, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return stage;
    }
}

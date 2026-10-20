using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ToastStackPage
{
    public static Control Create()
    {
        var stack = new ToastStack { IsContained = true };
        var stage = new Border
        {
            Height = 380,
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new Grid
            {
                Children =
                {
                    new TextBlock { Text = "Hover or focus the stack to fan it out. Alt+T jumps into it; Escape closes the focused toast.", Margin = new Thickness(20), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    stack
                }
            }
        };
        ArcTokens.Bind(stage, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(stage, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var count = 0;
        Button Make(string label, Action run)
        {
            var button = new Button { Content = label, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
            button.Click += (_, _) => run();
            return button;
        }

        var kinds = GalleryUi.Row(
            Make("Success", () => stack.Show(new ToastOptions { Kind = ToastKind.Success, Title = "Changes saved", Description = $"Version {++count} is live." })),
            Make("Info", () => stack.Show(new ToastOptions { Kind = ToastKind.Info, Title = "Maya joined the workspace" })),
            Make("Warning", () => stack.Show(new ToastOptions { Kind = ToastKind.Warning, Title = "Storage almost full", Description = "92% of 10 GB used." })),
            Make("Error", () => stack.Show(new ToastOptions { Kind = ToastKind.Error, Title = "Couldn't send invite", Description = "Check the address and try again." })));

        var flows = GalleryUi.Row(
            Make("Loading → success", async () =>
            {
                var id = stack.Show(new ToastOptions { Kind = ToastKind.Loading, Title = "Uploading report.pdf" });
                await Task.Delay(1800);
                stack.Update(id, new ToastUpdate { Kind = ToastKind.Success, Title = "Report uploaded", Description = "Shared with 4 people." });
            }),
            Make("Undo", () =>
            {
                stack.Show(new ToastOptions
                {
                    Title = "Project archived",
                    Action = new ToastAction("Undo", id => stack.Update(id, new ToastUpdate { Kind = ToastKind.Success, Title = "Project restored", RemoveAction = true }))
                });
            }),
            Make("Dismiss all", () => stack.Dismiss()));

        var positions = GalleryUi.Row(
            Make("Bottom left", () => stack.Position = ToastStackPosition.BottomLeft),
            Make("Bottom center", () => stack.Position = ToastStackPosition.BottomCenter),
            Make("Bottom right", () => stack.Position = ToastStackPosition.BottomRight));

        return GalleryUi.Column(20,
            GalleryUi.Card("Stack", "New toasts rise from the edge, older ones tuck 14px behind and shrink 5% per step. Swipe right to dismiss.",
                GalleryUi.Column(12, stage, kinds, flows)),
            GalleryUi.Card("Position", "Bottom right by default; windows narrower than 480px always center the stack.", positions));
    }
}

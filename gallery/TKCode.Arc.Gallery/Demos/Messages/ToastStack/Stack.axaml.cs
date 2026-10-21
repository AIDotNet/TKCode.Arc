using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToastStackStackDemo : UserControl
{
    private int _version;

    public ToastStackStackDemo()
    {
        InitializeComponent();
    }

    private void OnSuccessClick(object? sender, RoutedEventArgs e)
        => Stack.Show(new ToastOptions { Kind = ToastKind.Success, Title = "Changes saved", Description = $"Version {++_version} is live." });

    private void OnInfoClick(object? sender, RoutedEventArgs e)
        => Stack.Show(new ToastOptions { Kind = ToastKind.Info, Title = "Maya joined the workspace" });

    private void OnWarningClick(object? sender, RoutedEventArgs e)
        => Stack.Show(new ToastOptions { Kind = ToastKind.Warning, Title = "Storage almost full", Description = "92% of 10 GB used." });

    private void OnErrorClick(object? sender, RoutedEventArgs e)
        => Stack.Show(new ToastOptions { Kind = ToastKind.Error, Title = "Couldn't send invite", Description = "Check the address and try again." });

    // A loading toast waits for an update, which morphs it into its result.
    private async void OnLoadingClick(object? sender, RoutedEventArgs e)
    {
        var id = Stack.Show(new ToastOptions { Kind = ToastKind.Loading, Title = "Uploading report.pdf" });
        await Task.Delay(1800);
        Stack.Update(id, new ToastUpdate { Kind = ToastKind.Success, Title = "Report uploaded", Description = "Shared with 4 people." });
    }

    // Undo updates the toast into its result instead of closing it.
    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        Stack.Show(new ToastOptions
        {
            Title = "Project archived",
            Action = new ToastAction("Undo", id => Stack.Update(id, new ToastUpdate
            {
                Kind = ToastKind.Success,
                Title = "Project restored",
                RemoveAction = true
            }))
        });
    }

    private void OnDismissAllClick(object? sender, RoutedEventArgs e) => Stack.Dismiss();
}

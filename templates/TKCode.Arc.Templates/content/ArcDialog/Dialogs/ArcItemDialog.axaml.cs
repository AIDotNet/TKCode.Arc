using Avalonia;
using Avalonia.Controls;
using TKCode.Arc;

namespace ArcItemNamespace.Dialogs;

/// <summary>What <see cref="ArcItemDialog.ShowAsync"/> returns when the dialog is confirmed.</summary>
/// <param name="Name">The name entered.</param>
/// <param name="Notify">Whether "Let the team know" was on.</param>
public sealed record ArcItemDialogResult(string Name, bool Notify);

/// <summary>
/// A form shown in an Arc <see cref="Dialog"/> over the window: the scrim blurs the page, Escape or a press on the scrim
/// cancels, and focus returns to where it was. From any control in the window:
/// <code>
/// if (await ArcItemDialog.ShowAsync(this, "My item") is { } result) { /* use result.Name, result.Notify */ }
/// </code>
/// </summary>
public partial class ArcItemDialog : UserControl
{
    public ArcItemDialog()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog over the window of <paramref name="owner"/>; returns null when it is cancelled.</summary>
    /// <param name="owner">Any visual in the window the dialog belongs to.</param>
    /// <param name="name">The name the form starts with.</param>
    public static async Task<ArcItemDialogResult?> ShowAsync(Visual owner, string? name = null)
    {
        var form = new ArcItemDialog();
        form.NameInput.Text = name;
        var dialog = new Dialog
        {
            Title = "ArcItemTitle",
            Description = "Change the details and save.",
            Content = form,
            // A button with a Dialog.Result closes the dialog with that result (BuildActions sets it).
            Actions = Dialog.BuildActions([
                new DialogAction("Cancel", false),
                new DialogAction("Save", true, ButtonVariant.Primary)
            ])
        };
        var result = await dialog.ShowAsync(owner);
        if (result is not true)
        {
            return null;
        }
        return new ArcItemDialogResult(form.NameInput.Text?.Trim() ?? string.Empty, form.NotifyChoice.IsChecked == true);
    }
}

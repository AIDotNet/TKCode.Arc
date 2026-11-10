using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareAccessPreviewDemo : UserControl
{
    public ShareAccessPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnInvited(object? sender, ShareAccessInvitedEventArgs e) =>
        Status.Text = $"Invited {string.Join(", ", e.People.Select(p => p.Email))} as {e.Role}";

    private void OnRoleChanged(object? sender, ShareAccessRoleChangedEventArgs e) =>
        Status.Text = $"{e.Person.Name ?? e.Person.Email}: {e.OldRole} → {e.NewRole}";

    private void OnRemoved(object? sender, ShareAccessRemovedEventArgs e) =>
        Status.Text = $"Removed {e.Person.Name ?? e.Person.Email}";

    private void OnLinkChanged(object? sender, ShareAccessLinkChangedEventArgs e) =>
        Status.Text = e.Scope == ShareAccessLinkScope.Restricted ? "Link: restricted" : $"Link: {e.Scope}, {e.Role}";

    private void OnDone(object? sender, RoutedEventArgs e) => Status.Text = "Done pressed: close the dialog here.";
}

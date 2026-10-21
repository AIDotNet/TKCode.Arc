using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommandPalettePreviewDemo : UserControl
{
    public CommandPalettePreviewDemo()
    {
        InitializeComponent();
        Palette.ItemsSource = Commands();
    }

    private void OnItemSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
        => LastCommand.Text = $"Last command: {e.Item.Label}";

    private static List<CommandPaletteItem> Commands() =>
    [
        new() { Id = "dashboard", Label = "Go to dashboard", Description = "Overview of your workspace", Group = "Navigation", Icon = ArcIcons.House, Shortcut = "G D" },
        new() { Id = "projects", Label = "Open projects", Description = "All active projects", Group = "Navigation", Icon = ArcIcons.Folder, Shortcut = "G P" },
        new() { Id = "inbox", Label = "View inbox", Description = "Mentions and assigned tasks", Group = "Navigation", Icon = ArcIcons.Inbox, Shortcut = "G I" },
        new() { Id = "calendar", Label = "Open calendar", Group = "Navigation", Icon = ArcIcons.Calendar },
        new() { Id = "new-project", Label = "Create project", Description = "Start from a blank board", Group = "Actions", Icon = ArcIcons.Plus, Shortcut = "C" },
        new() { Id = "invite", Label = "Invite teammate", Description = "Send an email invite", Group = "Actions", Icon = ArcIcons.Users },
        new() { Id = "upload", Label = "Upload file", Group = "Actions", Icon = ArcIcons.Upload, Shortcut = "U" },
        new() { Id = "copy-link", Label = "Copy page link", Group = "Actions", Icon = ArcIcons.Link, Keywords = ["share", "url"] },
        new() { Id = "theme", Label = "Switch theme", Description = "Light, dark or system", Group = "Settings", Icon = ArcIcons.SunMoon, Keywords = ["appearance", "dark mode"], Shortcut = "T" },
        new() { Id = "notifications", Label = "Notification settings", Group = "Settings", Icon = ArcIcons.Bell },
        new() { Id = "billing", Label = "Billing and plans", Group = "Settings", Icon = ArcIcons.CreditCard, Keywords = ["invoice", "subscription"] },
        new() { Id = "sign-out", Label = "Sign out", Group = "Settings", Icon = ArcIcons.LogOut, Keywords = ["log out"] }
    ];
}

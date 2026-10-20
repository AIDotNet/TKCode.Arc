using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class UserMenuPage
{
    private static UserMenu Create(string name, string email, string? plan)
    {
        var menu = new UserMenu { DisplayName = name, Email = email, Plan = plan };
        menu.Items.Add(new MenuEntry { Header = "Profile", Icon = ArcIcons.User });
        menu.Items.Add(new MenuEntry { Header = "Settings", Icon = ArcIcons.Settings, InputGestureText = "⌘ ," });
        menu.Items.Add(new MenuEntry { Header = "Billing", Icon = ArcIcons.CreditCard });
        return menu;
    }

    private static Border TopBar(string title, Control account)
    {
        var bar = new Border
        {
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Child = new DockPanel
            {
                Children =
                {
                    account,
                    GalleryUi.Text(title, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium)
                }
            }
        };
        DockPanel.SetDock(account, Dock.Right);
        ((TextBlock)((DockPanel)bar.Child).Children[1]).VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(bar, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(bar, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return bar;
    }

    public static Control Create()
    {
        var status = GalleryUi.Text("Theme: System", ArcDesignTokenKind.TextSecondary);

        // Arc's usage example: name shown, Pro plan, three destinations, async sign out.
        var account = Create("Ada Lovelace", "ada@analytical.dev", "Pro");
        account.IsNameVisible = true;
        account.SignOutGestureText = "⇧⌘Q";
        account.SignOutAction = () => Task.Delay(1200);
        account.ThemePreferenceChanged += (_, e) => status.Text = $"Theme: {e.ThemePreference}";
        account.ItemClicked += (_, e) => status.Text = $"Opened {e.Header}";
        account.SignOutRequested += (_, _) => status.Text = "Signing out…";

        // Presence: the dot changes shape as well as color (solid, bar, ring).
        var presence = Create("Grace Hopper", "grace@compiler.dev", "Admin");
        presence.IsStatusVisible = true;
        presence.Status = UserMenuStatus.Busy;
        presence.IsThemeSwitchVisible = false;
        presence.PanelAlignment = MenuAlignment.Start;
        presence.StatusChanged += (_, e) => status.Text = $"Status: {e.Status}";

        return GalleryUi.Column(20,
            GalleryUi.Card("Top bar", "The panel scales out of the avatar; theme choices keep it open; sign out shows progress until it settles. Below 640px it opens as a bottom sheet you can drag down.",
                GalleryUi.Column(12, TopBar("Northwind", account), status)),
            GalleryUi.Card("Presence", "Status adds the dot and an inline switch: ArrowLeft and ArrowRight choose within it.",
                GalleryUi.Row(presence)));
    }
}

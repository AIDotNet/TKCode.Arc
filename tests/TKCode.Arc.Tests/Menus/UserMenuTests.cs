using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Menus;

public class UserMenuTests
{
    private static UserMenu CreateMenu()
    {
        var menu = new UserMenu { DisplayName = "Ada Lovelace", Email = "ada@analytical.dev", Plan = "Pro", IsNameVisible = true };
        menu.Items.Add(new MenuEntry { Header = "Profile", Icon = ArcIcons.User });
        menu.Items.Add(new MenuEntry { Header = "Settings", Icon = ArcIcons.Settings, InputGestureText = "⌘," });
        menu.Items.Add(new MenuEntry { Header = "Billing", Icon = ArcIcons.CreditCard });
        return menu;
    }

    private static Window Show(Control control, double width = 900)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(40), Children = { control } });
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static AvaloniaButton Trigger(UserMenu menu) =>
        menu.GetVisualDescendants().OfType<AvaloniaButton>().First(b => b.Name == UserMenu.TriggerPart);

    [AvaloniaFact]
    public void Trigger_Is_A_40px_Pill_That_Never_Scales()
    {
        var menu = CreateMenu();
        var window = Show(menu);
        try
        {
            var trigger = Trigger(menu);
            ((Button)trigger).IsPressScaleEnabled.ShouldBeFalse();
            trigger.Bounds.Height.ShouldBe(40);
            menu.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Name").IsVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(trigger).ShouldBe("Account menu, Ada Lovelace");
            menu.IsStatusVisible = true;
            menu.Status = UserMenuStatus.Busy;
            Avalonia.Automation.AutomationProperties.GetName(trigger).ShouldBe("Account menu, Ada Lovelace, Busy");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Are_Composed_With_Theme_Switch_And_Sign_Out()
    {
        var menu = CreateMenu();
        var window = Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Menu.Variant.ShouldBe(MenuSurfaceVariant.Account);
            menu.Menu.Bounds.Width.ShouldBe(248);
            menu.Menu.Header.ShouldNotBeNull();
            var rows = menu.Menu.Items.OfType<MenuEntry>().ToList();
            rows.Count.ShouldBe(5);
            TextSearchText(rows[3]).ShouldBe("Theme");
            TextSearchText(rows[4]).ShouldBe("Sign out");
            menu.Menu.Items.OfType<MenuEntrySeparator>().Count().ShouldBe(3);
            rows[0].Bounds.Height.ShouldBe(32);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Change_The_Theme_Without_Closing()
    {
        var menu = CreateMenu();
        UserMenuThemePreference? reported = null;
        menu.ThemePreferenceChanged += (_, e) => reported = e.ThemePreference;
        var window = Show(menu);
        try
        {
            Trigger(menu).Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            TextSearchText(menu.Menu.HighlightedEntry!).ShouldBe("Sign out");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            TextSearchText(menu.Menu.HighlightedEntry!).ShouldBe("Theme");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            menu.ThemePreference.ShouldBe(UserMenuThemePreference.Dark);
            reported.ShouldBe(UserMenuThemePreference.Dark);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            menu.ThemePreference.ShouldBe(UserMenuThemePreference.Light);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            menu.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.None);
            TextSearchText(menu.Menu.HighlightedEntry!).ShouldBe("Sign out");
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            Trigger(menu).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Async_Sign_Out_Keeps_The_Menu_Open_Until_It_Settles()
    {
        var menu = CreateMenu();
        var gate = new TaskCompletionSource();
        var requested = 0;
        menu.SignOutRequested += (_, _) => requested++;
        menu.SignOutAction = () => gate.Task;
        var window = Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            var signOut = menu.Menu.Items.OfType<MenuEntry>().Last();
            signOut.PerformClick();
            Dispatcher.UIThread.RunJobs();
            requested.ShouldBe(1);
            menu.IsSigningOut.ShouldBeTrue();
            menu.IsOpen.ShouldBeTrue();
            signOut.Icon.ShouldBeOfType<Primitives.ArcSpinner>();
            gate.SetResult();
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            menu.IsSigningOut.ShouldBeFalse();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Window_Opens_A_Bottom_Sheet()
    {
        var menu = CreateMenu();
        var window = Show(menu, 400);
        try
        {
            menu.Classes.Contains(UserMenu.CompactPseudoClass).ShouldBeTrue();
            menu.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Name").IsVisible.ShouldBeFalse();
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.Menu.Variant.ShouldBe(MenuSurfaceVariant.Sheet);
            menu.Menu.Width.ShouldBe(400);
            var rows = menu.Menu.Items.OfType<MenuEntry>().ToList();
            rows[0].Bounds.Height.ShouldBe(46);
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Built_In_Text_Follows_The_Language()
    {
        var menu = CreateMenu();
        menu.IsStatusVisible = true;
        menu.Status = UserMenuStatus.Busy;
        var window = Show(menu);
        var languages = ArcLanguageManager.Current!;
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            var rows = menu.Menu.Items.OfType<MenuEntry>().ToList();
            var (status, theme, signOut) = (rows[3], rows[4], rows[5]);
            TextSearchText(status).ShouldBe("Status");
            AutomationProperties.GetHelpText(status).ShouldBe("Busy");

            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            TextSearchText(status).ShouldBe("状态");
            AutomationProperties.GetHelpText(status).ShouldBe("忙碌");
            TextSearchText(theme).ShouldBe("主题");
            AutomationProperties.GetHelpText(theme).ShouldBe("跟随系统");
            TextSearchText(signOut).ShouldBe("退出登录");
            ((ArcContentSwap)signOut.Header!).Content.ShouldBe("退出登录");
            AutomationProperties.GetName(Trigger(menu)).ShouldBe("账户菜单, Ada Lovelace, 忙碌");
            AutomationProperties.GetName(menu.Menu).ShouldBe("账户菜单");

            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            TextSearchText(status).ShouldBe("狀態");
            ((ArcContentSwap)signOut.Header!).Content.ShouldBe("登出");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_The_Localized_Text()
    {
        var menu = CreateMenu();
        menu.IsStatusVisible = true;
        menu.StatusLabel = "Presence";
        var window = Show(menu);
        var languages = ArcLanguageManager.Current!;
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            var rows = menu.Menu.Items.OfType<MenuEntry>().ToList();
            var (status, theme, signOut) = (rows[3], rows[4], rows[5]);
            TextSearchText(status).ShouldBe("Presence");
            AutomationProperties.GetName(status).ShouldBe("Presence");

            menu.ThemeLabel = "Appearance";
            menu.SignOutLabel = "Log out";
            TextSearchText(theme).ShouldBe("Appearance");
            TextSearchText(signOut).ShouldBe("Log out");
            AutomationProperties.GetName(signOut).ShouldBe("Log out");
            ((ArcContentSwap)signOut.Header!).Content.ShouldBe("Log out");

            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            TextSearchText(status).ShouldBe("Presence");
            TextSearchText(signOut).ShouldBe("Log out");

            menu.SignOutLabel = null;
            menu.StatusLabel = null;
            TextSearchText(signOut).ShouldBe("退出登录");
            TextSearchText(status).ShouldBe("状态");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Signing_Out_Shows_The_Signing_Out_Label()
    {
        var menu = CreateMenu();
        var gate = new TaskCompletionSource();
        menu.SignOutAction = () => gate.Task;
        menu.SignOutLabel = "Log out";
        menu.SigningOutLabel = "Logging out…";
        var window = Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            var signOut = menu.Menu.Items.OfType<MenuEntry>().Last();
            var label = (ArcContentSwap)signOut.Header!;
            signOut.PerformClick();
            Dispatcher.UIThread.RunJobs();
            label.Content.ShouldBe("Logging out…");
            AutomationProperties.GetHelpText(signOut).ShouldBe("Logging out…");
            gate.SetResult();
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            label.Content.ShouldBe("Log out");
            AutomationProperties.GetHelpText(signOut).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    private static string? TextSearchText(MenuEntry entry) => Avalonia.Controls.Primitives.TextSearch.GetText(entry) ?? entry.Header as string;
}

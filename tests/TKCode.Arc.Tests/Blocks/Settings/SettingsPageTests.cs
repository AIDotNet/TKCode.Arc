using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// SettingsPage: default selection, keyboard model, save bar and its events, compact layout, navigation names, badge tones
/// and the selected value.
/// </summary>
public class SettingsPageTests
{
    private static SettingsPage Page(out SettingsSection profile, out SettingsSection alerts, out SettingsSection billing)
    {
        profile = new SettingsSection { Header = "Profile", Icon = ArcIcons.User, Content = new TextBlock { Text = "Profile body" } };
        alerts = new SettingsSection { Header = "Notifications", Badge = "2", Content = new TextBlock { Text = "Alerts body" } };
        billing = new SettingsSection { Header = "Billing", Content = new TextBlock { Text = "Billing body" } };
        return new SettingsPage
        {
            Width = 820,
            Height = 480,
            Footer = new TextBlock { Text = "Account" },
            Items = { profile, alerts, billing }
        };
    }

    private static Window Show(SettingsPage page)
    {
        var window = TestHelpers.Show(page);
        window.Width = 900;
        window.Height = 560;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static SettingsPageNavItem Nav(SettingsPage page, int index) => (SettingsPageNavItem)page.ContainerFromIndex(index)!;

    private static object? Shown(SettingsPage page) => TestHelpers.Descendant<SettingsPageContentHost>(page).Current;

    [AvaloniaFact]
    public void The_First_Section_Is_Selected_And_Shown()
    {
        var page = Page(out var profile, out _, out _);
        var window = Show(page);
        try
        {
            page.SelectedIndex.ShouldBe(0);
            page.SelectedSection.ShouldBe(profile);
            Shown(page).ShouldBe(profile);
            Nav(page, 0).IsSelected.ShouldBeTrue();
            Nav(page, 0).Header.ShouldBe("Profile");
            TestHelpers.Descendant<GlidingIndicator>(page, SettingsPage.SelectionPillPart).HasTarget.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Home_And_End_Move_The_Selection_Without_Wrapping()
    {
        var page = Page(out _, out var alerts, out var billing);
        var window = Show(page);
        try
        {
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            page.SelectedSection.ShouldBe(alerts);
            Shown(page).ShouldBe(alerts);

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            page.SelectedSection.ShouldBe(billing);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            page.SelectedSection.ShouldBe(billing);

            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            page.SelectedIndex.ShouldBe(0);
            // One tab stop for the whole navigation: the item with focus.
            Nav(page, 0).IsTabStop.ShouldBeTrue();
            Nav(page, 1).IsTabStop.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Disabled_Section_Is_Skipped()
    {
        var page = Page(out _, out var alerts, out var billing);
        alerts.IsEnabled = false;
        var window = Show(page);
        try
        {
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            page.SelectedSection.ShouldBe(billing);
            page.Select(alerts).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unsaved_Changes_Show_The_Save_Bar_And_Save_And_Discard_Raise_Events()
    {
        var page = Page(out var profile, out _, out _);
        var window = Show(page);
        try
        {
            var bar = TestHelpers.Descendant<PresenceHost>(page, SettingsPage.SaveBarPart);
            bar.IsPresent.ShouldBeFalse();

            profile.IsDirty = true;
            bar.IsPresent.ShouldBeTrue();
            page.Classes.Contains(SettingsPage.DirtyPseudoClass).ShouldBeTrue();
            Nav(page, 0).IsDirty.ShouldBeTrue();

            SettingsSection? saved = null;
            SettingsSection? discarded = null;
            page.SaveRequested += (_, e) => saved = e.Section;
            page.DiscardRequested += (_, e) => discarded = e.Section;
            TestHelpers.Descendant<Avalonia.Controls.Button>(page, SettingsPage.SaveButtonPart)
                       .RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            TestHelpers.Descendant<Avalonia.Controls.Button>(page, SettingsPage.DiscardButtonPart)
                       .RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            saved.ShouldBe(profile);
            discarded.ShouldBe(profile);

            profile.IsDirty = false;
            bar.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_S_Saves_Only_When_The_Section_Has_Changes()
    {
        var page = Page(out var profile, out _, out _);
        var window = Show(page);
        try
        {
            var saves = 0;
            page.SaveRequested += (_, _) => saves++;
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            saves.ShouldBe(0);

            profile.IsDirty = true;
            window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
            saves.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Page_Becomes_One_Column_Without_Its_Footer()
    {
        var page = Page(out _, out _, out _);
        var window = Show(page);
        try
        {
            page.IsCompact.ShouldBeFalse();
            TestHelpers.Descendant<Border>(page, SettingsPage.FooterSlotPart).IsVisible.ShouldBeTrue();

            page.Width = 480;
            Dispatcher.UIThread.RunJobs();
            page.IsCompact.ShouldBeTrue();
            page.Classes.Contains(SettingsPage.CompactPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<Border>(page, SettingsPage.FooterSlotPart).IsVisible.ShouldBeFalse();
            Nav(page, 1).Bounds.X.ShouldBeGreaterThan(Nav(page, 0).Bounds.X);

            // Compact navigation runs left to right.
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            page.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navigation_Names_Read_The_Badge_And_Unsaved_Changes()
    {
        var page = Page(out _, out var alerts, out _);
        var window = Show(page);
        try
        {
            AutomationProperties.GetName(Nav(page, 1)).ShouldBe("Notifications, 2");
            alerts.IsDirty = true;
            AutomationProperties.GetName(Nav(page, 1)).ShouldBe("Notifications, 2, has unsaved changes");
            alerts.Header = "Alerts";
            Nav(page, 1).Header.ShouldBe("Alerts");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Badge_Tone_Reaches_The_Navigation_Badge()
    {
        var page = Page(out _, out var alerts, out _);
        var window = Show(page);
        try
        {
            alerts.BadgeTone.ShouldBe(BadgeTone.Neutral);
            var badge = TestHelpers.Descendant<Badge>(Nav(page, 1), "PART_Badge");
            badge.Tone.ShouldBe(BadgeTone.Neutral);

            alerts.BadgeTone = BadgeTone.Warning;
            Nav(page, 1).BadgeTone.ShouldBe(BadgeTone.Warning);
            badge.Tone.ShouldBe(BadgeTone.Warning);
            badge.Classes.Contains(Badge.WarningPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Is_The_Selected_Sections_Value_And_Selects_It()
    {
        var page = Page(out var profile, out var alerts, out var billing);
        profile.Value = "profile";
        alerts.Value = "alerts";
        billing.Value = "billing";
        var window = Show(page);
        try
        {
            // Selection to value.
            page.SelectedValue.ShouldBe("profile");
            page.SelectedIndex = 2;
            page.SelectedValue.ShouldBe("billing");
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            page.SelectedValue.ShouldBe("profile");

            // Value to selection.
            page.SelectedValue = "alerts";
            page.SelectedSection.ShouldBe(alerts);
            Shown(page).ShouldBe(alerts);
            Nav(page, 1).IsSelected.ShouldBeTrue();

            // A new value on the selected section is reported at once.
            alerts.Value = "notifications";
            page.SelectedValue.ShouldBe("notifications");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Section_Without_A_Value_Reports_Itself()
    {
        var page = Page(out var profile, out _, out var billing);
        var window = Show(page);
        try
        {
            page.SelectedValue.ShouldBe(profile);
            page.SelectedValue = billing;
            page.SelectedSection.ShouldBe(billing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Binds_Two_Way_To_An_Enum()
    {
        var model = new SettingsModel { Tab = SettingsTab.Notifications };
        var page = Page(out var profile, out var alerts, out var billing);
        profile.Value = SettingsTab.Profile;
        alerts.Value = SettingsTab.Notifications;
        billing.Value = SettingsTab.Billing;
        page.DataContext = model;
        page.Bind(SelectingItemsControl.SelectedValueProperty, new Binding(nameof(SettingsModel.Tab)));
        var window = Show(page);
        try
        {
            page.SelectedSection.ShouldBe(alerts);

            // The person picks a section: the view model hears the enum member.
            page.Select(billing).ShouldBeTrue();
            model.Tab.ShouldBe(SettingsTab.Billing);

            // The view model changes the tab: the page shows that section.
            model.Tab = SettingsTab.Profile;
            Dispatcher.UIThread.RunJobs();
            page.SelectedSection.ShouldBe(profile);
            Shown(page).ShouldBe(profile);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bound_Value_In_Xaml_Selects_Its_Section_At_Start()
    {
        var page = AvaloniaRuntimeXamlLoader.Parse<SettingsPage>("""
            <arc:SettingsPage xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                              Width="820" Height="480" SelectedValue="{Binding Tab}">
                <arc:SettingsSection Header="Profile" Value="profile" />
                <arc:SettingsSection Header="Billing" Value="billing" />
            </arc:SettingsPage>
            """);
        var model = new StringModel { Tab = "billing" };
        page.DataContext = model;
        var window = Show(page);
        try
        {
            page.SelectedIndex.ShouldBe(1);
            page.SelectedSection!.Header.ShouldBe("Billing");
            Nav(page, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            model.Tab.ShouldBe("profile");
        }
        finally
        {
            window.Close();
        }
    }

    public enum SettingsTab
    {
        Profile,
        Notifications,
        Billing
    }

    public sealed class SettingsModel : System.ComponentModel.INotifyPropertyChanged
    {
        private SettingsTab _tab;

        public SettingsTab Tab
        {
            get => _tab;
            set
            {
                if (_tab != value)
                {
                    _tab = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Tab)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed class StringModel
    {
        public string? Tab { get; set; }
    }
}

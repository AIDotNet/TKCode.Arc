using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Navigation;

public class TabsTests
{
    private static Tabs Inline(params string[] headers)
    {
        var tabs = new Tabs();
        foreach (var header in headers)
        {
            tabs.Items.Add(new TabItem { Header = header, Content = new TextBlock { Text = header + " panel" } });
        }
        return tabs;
    }

    private static TabsPanelHost Host(Tabs tabs) => TestHelpers.Descendant<TabsPanelHost>(tabs);

    private static string? ShownText(Tabs tabs) => (Host(tabs).CurrentPresenter.Content as TextBlock)?.Text;

    [AvaloniaFact]
    public void First_Tab_Is_Selected_And_Its_Panel_Shown()
    {
        var tabs = Inline("Overview", "Activity", "Settings");
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedIndex.ShouldBe(0);
            ShownText(tabs).ShouldBe("Overview panel");
            var first = (TabItem)tabs.ContainerFromIndex(0)!;
            first.IsSelected.ShouldBeTrue();
            first.Classes.Contains(":selected").ShouldBeTrue();
            first.MinHeight.ShouldBe(36);
            first.MinWidth.ShouldBe(88);
            var pill = TestHelpers.Descendant<GlidingIndicator>(tabs);
            pill.HasTarget.ShouldBeTrue();
            pill.CurrentRect.Width.ShouldBe(first.Bounds.Width - 1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_Moves_Pill_And_Swaps_Panel()
    {
        var tabs = Inline("Overview", "Activity", "Settings");
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            ShownText(tabs).ShouldBe("Settings panel");
            tabs.SelectedContent.ShouldBeOfType<TextBlock>();
            var third = tabs.ContainerFromIndex(2)!;
            var pill = TestHelpers.Descendant<GlidingIndicator>(tabs);
            var origin = third.TranslatePoint(default, pill)!.Value;
            pill.CurrentRect.X.ShouldBe(origin.X + 0.5, 0.01);
            Host(tabs).ExitingPresenter.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Select_Skip_Disabled_And_Loop()
    {
        var tabs = Inline("One", "Two", "Three", "Four");
        ((TabItem)tabs.Items[2]!).IsEnabled = false;
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(0);
            tabs.ContainerFromIndex(0)!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Manual_Activation_Moves_Focus_Then_Enter_Selects()
    {
        var tabs = Inline("Day", "Week", "Month");
        tabs.ActivationMode = TabsActivationMode.Manual;
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(0);
            tabs.ContainerFromIndex(1)!.IsFocused.ShouldBeTrue();
            tabs.ContainerFromIndex(1)!.IsTabStop.ShouldBeTrue();
            tabs.ContainerFromIndex(0)!.IsTabStop.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Selected_Tab_Is_A_Tab_Stop()
    {
        var tabs = Inline("A", "B", "C");
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            tabs.ContainerFromIndex(0)!.IsTabStop.ShouldBeFalse();
            tabs.ContainerFromIndex(1)!.IsTabStop.ShouldBeTrue();
            tabs.ContainerFromIndex(2)!.IsTabStop.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Uses_ContentTemplate_And_TwoWay_SelectedItem()
    {
        var tabs = new Tabs
        {
            ItemsSource = new[] { "Inbox", "Drafts", "Sent" },
            ContentTemplate = new FuncDataTemplate<string>((s, _) => new TextBlock { Text = s + "!" })
        };
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.SelectedItem.ShouldBe("Inbox");
            tabs.SelectedItem = "Sent";
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(2);
            Host(tabs).CurrentPresenter.Content.ShouldBe("Sent");
            var text = Host(tabs).CurrentPresenter.GetVisualDescendants().OfType<TextBlock>().First();
            text.Text.ShouldBe("Sent!");
            var header = tabs.ContainerFromIndex(1)!.GetVisualDescendants().OfType<TextBlock>().First();
            header.Text.ShouldBe("Drafts");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_List_Overflows_And_Shows_Scroll_Button()
    {
        var tabs = Inline("Inbox", "Drafts", "Sent", "Scheduled", "Spam", "Trash", "Archive");
        tabs.Width = 300;
        var window = TestHelpers.Show(tabs);
        try
        {
            tabs.Classes.Contains(":overflow").ShouldBeTrue();
            tabs.Classes.Contains(":scrolled-end").ShouldBeTrue();
            var right = TestHelpers.Descendant<Visual>(tabs).GetVisualDescendants().OfType<Button>().First(b => b.Name == Tabs.ScrollRightPart);
            right.IsVisible.ShouldBeTrue();
            right.IsEnabled.ShouldBeTrue();
            tabs.SelectedIndex = 6;
            Dispatcher.UIThread.RunJobs();
            var viewport = TestHelpers.Descendant<ScrollViewer>(tabs);
            viewport.Offset.X.ShouldBeGreaterThan(0);
            tabs.Classes.Contains(":scrolled-start").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Tab_Selects_It()
    {
        var tabs = Inline("One", "Two");
        var window = TestHelpers.Show(tabs);
        try
        {
            var second = tabs.ContainerFromIndex(1)!;
            var center = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Back_Mid_Flight_Reuses_The_Leaving_Panel()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var tabs = Inline("One", "Two", "Three");
        var window = TestHelpers.Show(tabs, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            var host = Host(tabs);
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(40));
            Dispatcher.UIThread.RunJobs();
            host.ExitingPresenter.ShouldNotBeNull();
            ((TextBlock)host.ExitingPresenter!.Content!).Text.ShouldBe("One panel");
            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            ShownText(tabs).ShouldBe("One panel");
            ((TextBlock)host.ExitingPresenter!.Content!).Text.ShouldBe("Two panel");
            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            ShownText(tabs).ShouldBe("Three panel");
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            host.ExitingPresenter.ShouldBeNull();
            host.CurrentPresenter.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}

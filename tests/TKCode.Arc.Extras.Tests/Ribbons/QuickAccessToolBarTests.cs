using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Ribbons;

/// <summary>The quick access toolbar: own commands, pinned ribbon commands, the context menu and the customize menu.</summary>
public class QuickAccessToolBarTests
{
    private static (RibbonSample Sample, QuickAccessToolBar Bar, Window Window) Show()
    {
        var sample = new RibbonSample();
        var save = new RibbonButton { Label = "Save", Icon = ArcIcons.Pin };
        var bar = new QuickAccessToolBar { Ribbon = sample.Ribbon };
        bar.Items.Add(save);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        root.Children.Add(bar);
        root.Children.Add(sample.Ribbon);
        var window = TestHelpers.Show(root);
        window.Width = 1600;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        return (sample, bar, window);
    }

    private static void Click(Window window, Control target)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Own_Commands_Show_Small()
    {
        var (_, bar, window) = Show();
        try
        {
            var save = (RibbonButton)bar.Items[0];
            save.EffectiveSize.ShouldBe(RibbonItemSize.Small);
            save.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pinned_Command_Shows_A_Copy_That_Runs_It()
    {
        var (sample, bar, window) = Show();
        try
        {
            bar.Keys.Add("cut");
            Dispatcher.UIThread.RunJobs();
            var proxy = bar.Proxies.ShouldHaveSingleItem();
            proxy.Target.ShouldBe(sample.Cut);
            proxy.Icon.ShouldBe(sample.Cut.Icon);
            proxy.EffectiveSize.ShouldBe(RibbonItemSize.Small);

            var runs = 0;
            sample.Cut.Click += (_, _) => runs++;
            RibbonItemInvokedEventArgs? invoked = null;
            sample.Ribbon.ItemInvoked += (_, e) => invoked = e;
            Click(window, proxy);
            runs.ShouldBe(1);
            invoked!.Item.ShouldBe(sample.Cut);

            // The copy follows the command's enabled state.
            sample.Cut.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            proxy.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pinned_Command_On_Another_Tab_Still_Runs()
    {
        var (sample, bar, window) = Show();
        try
        {
            bar.Keys.Add("bold");
            sample.Ribbon.SelectedTab = sample.View;
            Dispatcher.UIThread.RunJobs();
            var proxy = bar.Proxies.ShouldHaveSingleItem();
            Click(window, proxy);
            sample.Bold.IsChecked.ShouldBeTrue();
            proxy.Classes.Contains(RibbonButton.CheckedPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pinned_Drop_Down_Opens_Its_Menu_At_The_Copy()
    {
        var (sample, bar, window) = Show();
        try
        {
            bar.Keys.Add("align");
            Dispatcher.UIThread.RunJobs();
            var proxy = bar.Proxies.ShouldHaveSingleItem();
            proxy.Classes.Contains(RibbonButton.DropDownPseudoClass).ShouldBeTrue();
            Click(window, proxy);
            sample.Align.IsDropDownOpen.ShouldBeTrue();
            sample.Align.Menu.PlacementTarget.ShouldBe(proxy);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unknown_Keys_Are_Kept_And_Show_Nothing()
    {
        var (_, bar, window) = Show();
        try
        {
            bar.Keys.Add("missing");
            Dispatcher.UIThread.RunJobs();
            bar.Proxies.ShouldBeEmpty();
            bar.Keys.ShouldContain("missing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Context_Menu_Of_A_Command_Pins_And_Unpins_It()
    {
        var (sample, bar, window) = Show();
        try
        {
            sample.Copy.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent, Source = sample.Copy });
            Dispatcher.UIThread.RunJobs();
            var menu = sample.Ribbon.CommandContextMenu.ShouldNotBeNull();
            menu.IsOpen.ShouldBeTrue();
            var entry = menu.Items.OfType<MenuEntry>().Single();
            entry.Header.ShouldBe("Add to Quick Access Toolbar");
            Click(window, entry);
            bar.Keys.ShouldBe(["copy"]);
            Dispatcher.UIThread.RunJobs();
            bar.Proxies.ShouldHaveSingleItem().Target.ShouldBe(sample.Copy);

            sample.Copy.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent, Source = sample.Copy });
            Dispatcher.UIThread.RunJobs();
            entry.Header.ShouldBe("Remove from Quick Access Toolbar");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Command_Without_A_Key_Offers_No_Context_Menu()
    {
        var (sample, _, window) = Show();
        try
        {
            var args = new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent, Source = sample.Painter };
            sample.Painter.RaiseEvent(args);
            args.Handled.ShouldBeFalse();
            sample.Ribbon.CommandContextMenu.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Customize_Menu_Lists_The_Ribbon_Commands_And_Toggles_Them()
    {
        var (sample, bar, window) = Show();
        try
        {
            var customize = TestHelpers.Descendant<RibbonButton>(bar, QuickAccessToolBar.CustomizeButtonPart);
            Click(window, customize);
            var menu = bar.CustomizeMenu.ShouldNotBeNull();
            var rows = menu.Items.OfType<MenuEntry>().ToList();
            rows.Select(r => r.Tag).ShouldBe(["paste", "cut", "copy", "bold", "node", "align"]);
            rows.ShouldAllBe(r => !r.IsChecked);
            Click(window, rows[3]);
            bar.Keys.ShouldBe(["bold"]);
            rows[3].IsChecked.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_A_Command_From_The_Ribbon_Removes_Its_Copy()
    {
        var (sample, bar, window) = Show();
        try
        {
            bar.Keys.Add("node");
            Dispatcher.UIThread.RunJobs();
            bar.Proxies.Count.ShouldBe(1);
            sample.Insert.Items.Remove(sample.Node);
            TestHelpers.PumpUntil(() => bar.Proxies.Count == 0).ShouldBeTrue();
            bar.Keys.ShouldContain("node");
        }
        finally
        {
            window.Close();
        }
    }
}

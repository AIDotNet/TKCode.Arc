using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Ribbons;

/// <summary>Selection, keyboard, the command buttons, collapsed groups and the collapsed ribbon.</summary>
public class RibbonInteractionTests
{
    private static Window Show(RibbonSample sample, double width = 1600) => RibbonLayoutTests.Show(sample, width);

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    private static void Click(Window window, Control target, Point? at = null)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var point = target.TranslatePoint(at ?? new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A group of nine medium commands: collapsing it saves room, so it collapses before the others.</summary>
    private static RibbonGroup AddWideGroup(RibbonSample sample, out List<RibbonButton> commands)
    {
        var group = new RibbonGroup { Header = "Wide", ReductionPriority = 10 };
        commands = new List<RibbonButton>();
        for (var i = 0; i < 9; i++)
        {
            var button = new RibbonButton { Label = $"Command number {i}", Icon = ArcIcons.Settings, Size = RibbonItemSize.Medium };
            commands.Add(button);
            group.Items.Add(button);
        }
        sample.Home.Groups.Add(group);
        return group;
    }

    [AvaloniaFact]
    public void Clicking_A_Tab_Selects_It_And_Raises_SelectionChanged()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            RibbonSelectionChangedEventArgs? args = null;
            sample.Ribbon.SelectionChanged += (_, e) => args = e;
            var viewHeader = sample.Ribbon.Headers[1];
            Click(window, viewHeader);
            sample.Ribbon.SelectedTab.ShouldBe(sample.View);
            sample.Ribbon.SelectedIndex.ShouldBe(1);
            args.ShouldNotBeNull();
            args.OldItem.ShouldBe(sample.Home);
            args.NewItem.ShouldBe(sample.View);
            sample.ZoomIn.IsEffectivelyVisible.ShouldBeTrue();
            sample.Cut.ShouldSatisfyAllConditions(c => TopLevel.GetTopLevel(c).ShouldBeNull());
            viewHeader.Classes.Contains(RibbonTabHeader.SelectedPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_Selected_Tab_Hands_The_Selection_To_The_First_Visible_Tab()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            sample.Ribbon.Headers[2].IsVisible.ShouldBeFalse();
            sample.Tools.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.Headers[2].IsVisible.ShouldBeTrue();
            sample.Ribbon.Headers[2].Classes.Contains(RibbonTabHeader.ContextualPseudoClass).ShouldBeTrue();
            sample.Ribbon.SelectedTab = sample.Tools;
            sample.Ribbon.SelectedIndex.ShouldBe(2);
            sample.Tools.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.SelectedTab.ShouldBe(sample.Home);
            sample.Ribbon.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Tabs_And_Down_Enters_The_Groups()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var home = sample.Ribbon.Headers[0];
            home.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.SelectedTab.ShouldBe(sample.View);
            Focused(window).ShouldBe(sample.Ribbon.Headers[1]);
            // The contextual tab is hidden, so Right loops back to Home.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.SelectedTab.ShouldBe(sample.Home);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(sample.Paste);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(sample.Cut);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(sample.Paste);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(sample.Ribbon.Headers[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_The_Selected_Tab_Is_A_Tab_Stop()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            KeyboardNavigation.GetIsTabStop(sample.Ribbon.Headers[0]).ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(sample.Ribbon.Headers[1]).ShouldBeFalse();
            sample.Ribbon.SelectedIndex = 1;
            KeyboardNavigation.GetIsTabStop(sample.Ribbon.Headers[0]).ShouldBeFalse();
            KeyboardNavigation.GetIsTabStop(sample.Ribbon.Headers[1]).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Button_Runs_Its_Command_And_The_Ribbon_Reports_It()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var clicks = 0;
            sample.Cut.Click += (_, _) => clicks++;
            RibbonItemInvokedEventArgs? invoked = null;
            sample.Ribbon.ItemInvoked += (_, e) => invoked = e;
            Click(window, sample.Cut);
            clicks.ShouldBe(1);
            invoked.ShouldNotBeNull();
            invoked.Item.ShouldBe(sample.Cut);
            invoked.Entry.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Toggle_Button_Flips_And_Reports()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var changes = 0;
            sample.Bold.IsCheckedChanged += (_, _) => changes++;
            var invoked = 0;
            sample.Ribbon.ItemInvoked += (_, _) => invoked++;
            Click(window, sample.Bold);
            sample.Bold.IsChecked.ShouldBeTrue();
            sample.Bold.Classes.Contains(RibbonButton.CheckedPseudoClass).ShouldBeTrue();
            changes.ShouldBe(1);
            invoked.ShouldBe(1);
            Click(window, sample.Bold);
            sample.Bold.IsChecked.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drop_Down_Button_Opens_Its_Menu_And_Reports_The_Row()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var commandRuns = 0;
            sample.Align.Click += (_, _) => commandRuns++;
            RibbonItemInvokedEventArgs? invoked = null;
            sample.Ribbon.ItemInvoked += (_, e) => invoked = e;
            Click(window, sample.Align);
            sample.Align.IsDropDownOpen.ShouldBeTrue();
            sample.Align.Classes.Contains(RibbonButton.OpenPseudoClass).ShouldBeTrue();
            commandRuns.ShouldBe(0);
            invoked.ShouldBeNull();
            var entryClicks = 0;
            sample.AlignLeft.Click += (_, _) => entryClicks++;
            Click(window, sample.AlignLeft);
            entryClicks.ShouldBe(1);
            invoked.ShouldNotBeNull();
            invoked.Item.ShouldBe(sample.Align);
            invoked.Entry.ShouldBe(sample.AlignLeft);
            TestHelpers.PumpUntil(() => !sample.Align.IsDropDownOpen).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Split_Button_Runs_Its_Action_From_The_Icon_And_Opens_Its_Menu_From_The_Chevron()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var runs = 0;
            sample.Paste.Click += (_, _) => runs++;
            Click(window, sample.Paste, new Point(sample.Paste.Bounds.Width / 2, 12));
            runs.ShouldBe(1);
            sample.Paste.IsDropDownOpen.ShouldBeFalse();
            var part = sample.Paste.MenuPartControl.ShouldNotBeNull();
            part.IsEffectivelyVisible.ShouldBeTrue();
            part.Bounds.Top.ShouldBeGreaterThan(sample.Paste.Bounds.Height / 3);
            Click(window, part);
            runs.ShouldBe(1);
            sample.Paste.IsDropDownOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Down_Opens_The_Menu_Of_A_Split_Button()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            sample.Paste.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sample.Paste.IsDropDownOpen.ShouldBeTrue();
            sample.Paste.Menu.HighlightedEntry.ShouldBe(sample.PasteSpecial);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collapsed_Group_Shows_Its_Commands_In_A_Drop_Down()
    {
        var sample = new RibbonSample();
        var wide = AddWideGroup(sample, out var commands);
        var window = Show(sample, 1600);
        try
        {
            var full = sample.Home.GroupsPanel!.DesiredSize.Width;
            var saving = wide.GetWidth(0) - wide.GetWidth(RibbonGroup.CollapsedLevel);
            saving.ShouldBeGreaterThan(0);
            RibbonLayoutTests.Resize(window, full - saving + 4);
            wide.IsCollapsed.ShouldBeTrue();
            wide.Classes.Contains(RibbonGroup.CollapsedPseudoClass).ShouldBeTrue();
            var button = wide.CollapsedButton.ShouldNotBeNull();
            button.Bounds.Width.ShouldBeGreaterThan(0);
            button.Label.ShouldBe("Wide");

            Click(window, button);
            wide.IsDropDownOpen.ShouldBeTrue();
            // Commands show at their own size in the drop-down, inside the pop-up.
            commands[0].EffectiveSize.ShouldBe(RibbonItemSize.Medium);
            commands[0].FindAncestorOfType<RibbonGroupDropDown>().ShouldNotBeNull();
            commands[0].Parent.ShouldBe(wide);

            var runs = 0;
            commands[4].Click += (_, _) => runs++;
            RibbonItemInvokedEventArgs? invoked = null;
            sample.Ribbon.ItemInvoked += (_, e) => invoked = e;
            Click(window, commands[4]);
            runs.ShouldBe(1);
            invoked!.Item.ShouldBe(commands[4]);
            wide.IsDropDownOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collapsed_Group_Opens_From_The_Keyboard_And_Escape_Returns()
    {
        var sample = new RibbonSample();
        var wide = AddWideGroup(sample, out var commands);
        var window = Show(sample, 1600);
        try
        {
            var full = sample.Home.GroupsPanel!.DesiredSize.Width;
            RibbonLayoutTests.Resize(window, full - (wide.GetWidth(0) - wide.GetWidth(RibbonGroup.CollapsedLevel)) + 4);
            var button = wide.CollapsedButton.ShouldNotBeNull();
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => Focused(window) == commands[0]).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            wide.IsDropDownOpen.ShouldBeFalse();
            Focused(window).ShouldBe(button);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Expanded_Form_Of_A_Collapsed_Group_Is_Out_Of_The_Tab_Order()
    {
        var sample = new RibbonSample();
        var wide = AddWideGroup(sample, out var commands);
        var window = Show(sample, 1600);
        try
        {
            wide.CollapsedButton!.Focusable.ShouldBeFalse();
            wide.CollapsedButton.IsHitTestVisible.ShouldBeFalse();
            var full = sample.Home.GroupsPanel!.DesiredSize.Width;
            RibbonLayoutTests.Resize(window, full - (wide.GetWidth(0) - wide.GetWidth(RibbonGroup.CollapsedLevel)) + 4);
            wide.CollapsedButton.Focusable.ShouldBeTrue();
            var expanded = commands[0].FindAncestorOfType<DockPanel>().ShouldNotBeNull();
            KeyboardNavigation.GetTabNavigation(expanded).ShouldBe(KeyboardNavigationMode.None);
            expanded.IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Launcher_Raises_LauncherClick_And_Runs_Its_Command()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var clicks = 0;
            sample.Font.LauncherClick += (_, _) => clicks++;
            var launcher = TestHelpers.Descendant<Avalonia.Controls.Button>(sample.Font, RibbonGroup.LauncherPart);
            launcher.IsEffectivelyVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(launcher).ShouldBe("Font options");
            Click(window, launcher);
            clicks.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Minimized_Ribbon_Opens_The_Selected_Tab_In_A_Popup_Until_A_Command_Runs()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            var height = sample.Ribbon.Bounds.Height;
            sample.Ribbon.IsMinimized = true;
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.Bounds.Height.ShouldBeLessThan(height);
            sample.Ribbon.Classes.Contains(Arc.Ribbon.MinimizedPseudoClass).ShouldBeTrue();
            sample.Cut.ShouldSatisfyAllConditions(c => TopLevel.GetTopLevel(c).ShouldBeNull());

            Click(window, sample.Ribbon.Headers[0]);
            sample.Ribbon.IsPopupOpen.ShouldBeTrue();
            sample.Cut.FindAncestorOfType<RibbonPopupSurface>().ShouldNotBeNull();
            Click(window, sample.Cut);
            sample.Ribbon.IsPopupOpen.ShouldBeFalse();

            sample.Ribbon.IsMinimized = false;
            Dispatcher.UIThread.RunJobs();
            sample.Cut.FindAncestorOfType<Arc.Ribbon>().ShouldBe(sample.Ribbon);
            sample.Cut.FindAncestorOfType<RibbonPopupSurface>().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_F1_And_The_End_Button_Collapse_And_Pin_The_Ribbon()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            sample.Ribbon.Headers[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.IsMinimized.ShouldBeTrue();
            var button = TestHelpers.Descendant<RibbonButton>(sample.Ribbon, Arc.Ribbon.MinimizeButtonPart);
            button.Label.ShouldBe(RibbonLangResources.Get(RibbonLangResourceKind.ExpandRibbon));
            var invoked = 0;
            sample.Ribbon.ItemInvoked += (_, _) => invoked++;
            Click(window, button);
            sample.Ribbon.IsMinimized.ShouldBeFalse();
            invoked.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Clicking_A_Tab_Collapses_The_Ribbon()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var header = sample.Ribbon.Headers[0];
            var point = header.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.IsMinimized.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tooltip_Shows_The_Label_Shortcut_And_Description()
    {
        var sample = new RibbonSample();
        sample.Cut.Description = "Cut the selection to the clipboard.";
        var window = Show(sample);
        try
        {
            sample.Cut.Focus(NavigationMethod.Tab);
            var tip = Tooltip.GetTip(sample.Cut).ShouldBeOfType<StackPanel>();
            var texts = tip.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("Cut");
            texts.ShouldContain("Ctrl+X");
            texts.ShouldContain("Cut the selection to the clipboard.");
            // A labelled command without more to say has no tooltip; a small one shows its label.
            sample.Copy.Focus(NavigationMethod.Tab);
            Tooltip.GetTip(sample.Copy).ShouldBeNull();
            sample.Bold.Focus(NavigationMethod.Tab);
            Tooltip.GetTip(sample.Bold).ShouldBe("Bold");
            // An app's own tooltip wins.
            Tooltip.SetTip(sample.Italic, "Mine");
            sample.Italic.Focus(NavigationMethod.Tab);
            Tooltip.GetTip(sample.Italic).ShouldBe("Mine");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Commands_Are_Skipped_By_The_Arrow_Keys()
    {
        var sample = new RibbonSample();
        sample.Cut.IsEnabled = false;
        var window = Show(sample);
        try
        {
            sample.Paste.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(sample.Copy);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ArrowDown_On_A_Collapsed_Ribbon_Opens_The_Tab_And_Focuses_Its_First_Command()
    {
        var sample = new RibbonSample();
        sample.Ribbon.IsMinimized = true;
        var window = Show(sample);
        try
        {
            sample.Ribbon.Headers[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => Focused(window) == sample.Paste).ShouldBeTrue();
            sample.Ribbon.IsPopupOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.IsPopupOpen.ShouldBeFalse();
            Focused(window).ShouldBe(sample.Ribbon.Headers[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_F10_On_A_Command_Offers_The_Quick_Access_Toolbar()
    {
        var sample = new RibbonSample();
        var bar = new QuickAccessToolBar { Ribbon = sample.Ribbon };
        var window = Show(sample);
        try
        {
            sample.Cut.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            sample.Ribbon.CommandContextMenu.ShouldNotBeNull().IsOpen.ShouldBeTrue();
            bar.Keys.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Built_In_Text_Follows_The_Language()
    {
        var sample = new RibbonSample();
        var window = Show(sample);
        var languages = ArcLanguageManager.Current!;
        try
        {
            var button = TestHelpers.Descendant<RibbonButton>(sample.Ribbon, Arc.Ribbon.MinimizeButtonPart);
            var launcher = TestHelpers.Descendant<Avalonia.Controls.Button>(sample.Font, RibbonGroup.LauncherPart);
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            button.Label.ShouldBe("折叠功能区");
            Avalonia.Automation.AutomationProperties.GetName(launcher).ShouldBe("Font选项");
            languages.Set(LanguageTags.ZhTW);
            button.Label.ShouldBe("摺疊功能區");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }
}

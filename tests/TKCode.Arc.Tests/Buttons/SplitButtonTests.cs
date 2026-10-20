using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Buttons;

public class SplitButtonTests
{
    private static SplitButton Create()
    {
        var split = new SplitButton();
        split.Items.Add(new MenuEntry { Header = "Squash and merge" });
        split.Items.Add(new MenuEntry { Header = "Rebase and merge", IsEnabled = false });
        split.Items.Add(new MenuEntry { Header = "Close pull request", IsDestructive = true });
        return split;
    }

    private static Window Show(Control control) =>
        TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { control } });

    private static Button Primary(SplitButton split) => Part<Button>(split, SplitButton.PrimaryButtonPart);

    private static Button Chevron(SplitButton split) => Part<Button>(split, SplitButton.MenuButtonPart);

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var split = Create();
        var window = Show(split);
        try
        {
            split.Label.ShouldBe("Merge pull request");
            split.Variant.ShouldBe(SplitButtonVariant.Primary);
            split.Menu.AnchorGap.ShouldBe(4);
            split.Menu.Alignment.ShouldBe(MenuAlignment.End);
            // 44px halves inside the 1px group border.
            Primary(split).Bounds.Height.ShouldBe(44, 0.5);
            split.Bounds.Height.ShouldBe(46, 0.5);
            Chevron(split).Bounds.Width.ShouldBe(38, 0.5);
            Chevron(split).IsPressScaleEnabled.ShouldBeFalse();
            AutomationProperties.GetName(Primary(split)).ShouldBe("Merge pull request");
            AutomationProperties.GetName(Chevron(split)).ShouldBe("Merge pull request more actions");
            TestHelpers.ColorOf(Primary(split).Background).ShouldBe(TestHelpers.ColorOf(split.BorderBrush));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Secondary_Is_A_Compact_Pill()
    {
        var split = Create();
        split.Variant = SplitButtonVariant.Secondary;
        split.Label = "Export";
        var window = Show(split);
        try
        {
            split.Classes.ShouldContain(SplitButton.SecondaryPseudoClass);
            Primary(split).Bounds.Height.ShouldBe(36, 0.5);
            Primary(split).Bounds.Width.ShouldBeGreaterThanOrEqualTo(142);
            Chevron(split).Bounds.Width.ShouldBe(32, 0.5);
            Primary(split).FontSize.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Main_Half_Raises_Click_And_Runs_The_Command()
    {
        var split = Create();
        var clicks = 0;
        var commands = 0;
        split.Click += (_, _) => clicks++;
        split.Command = new RelayCommand(() => commands++);
        var window = Show(split);
        try
        {
            Primary(split).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe(1);
            commands.ShouldBe(1);
            split.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chevron_Opens_The_Menu_On_The_First_Row_And_Escape_Returns_Focus()
    {
        var split = Create();
        var window = Show(split);
        try
        {
            var chevron = Chevron(split);
            chevron.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            split.IsOpen.ShouldBeTrue();
            split.Classes.ShouldContain(SplitButton.OpenPseudoClass);
            chevron.Classes.ShouldContain(":open");
            split.Menu.HighlightedEntry!.Header.ShouldBe("Squash and merge");
            // Disabled rows are skipped and the arrows loop.
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            split.Menu.HighlightedEntry!.Header.ShouldBe("Close pull request");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            split.Menu.HighlightedEntry!.Header.ShouldBe("Squash and merge");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            split.IsOpen.ShouldBeFalse();
            chevron.IsFocused.ShouldBeTrue();
            chevron.Classes.ShouldNotContain(":open");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_A_Row_Raises_ItemClicked_And_Closes()
    {
        var split = Create();
        MenuEntryClickedEventArgs? clicked = null;
        split.ItemClicked += (_, e) => clicked = e;
        var window = Show(split);
        try
        {
            var chevron = Chevron(split);
            chevron.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            split.IsOpen.ShouldBeTrue();
            ((MenuEntry)split.Items[0]!).PerformClick();
            Dispatcher.UIThread.RunJobs();
            clicked.ShouldNotBeNull();
            clicked!.Header.ShouldBe("Squash and merge");
            split.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsOpen_Is_Two_Way_And_The_Trigger_Reports_ExpandCollapse()
    {
        var split = Create();
        var window = Show(split);
        try
        {
            split.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            split.Menu.IsOpen.ShouldBeTrue();
            var peer = (IExpandCollapseProvider)Peer(Chevron(split)).GetProvider<IExpandCollapseProvider>()!;
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            split.Close();
            Dispatcher.UIThread.RunJobs();
            split.IsOpen.ShouldBeFalse();
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Disables_Both_Halves()
    {
        var split = Create();
        split.IsEnabled = false;
        var window = Show(split);
        try
        {
            Primary(split).IsEffectivelyEnabled.ShouldBeFalse();
            Chevron(split).IsEffectivelyEnabled.ShouldBeFalse();
            Primary(split).Opacity.ShouldBe(0.52, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Label_Change_Morphs_Shared_Runs_And_Is_Announced()
    {
        var split = Create();
        split.Label = "Copy page";
        var window = Show(split);
        try
        {
            var label = TestHelpers.Descendant<SplitButtonLabel>(split);
            var c = label.GlyphIdentity(0);
            var o = label.GlyphIdentity(1);
            split.Label = "Copied";
            Dispatcher.UIThread.RunJobs();
            string.Concat(label.PresentGlyphs).ShouldBe("Copied");
            // "Cop" is shared, so its glyphs keep their identity; motion is off, so nothing lingers.
            label.GlyphIdentity(0).ShouldBeSameAs(c);
            label.GlyphIdentity(1).ShouldBeSameAs(o);
            label.LeavingCount.ShouldBe(0);
            AutomationProperties.GetName(Primary(split)).ShouldBe("Copied");
            AutomationProperties.GetName(Chevron(split)).ShouldBe("Copied more actions");
            Part<TextBlock>(split, SplitButton.StatusPart).Text.ShouldStartWith("Copied");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removed_Letters_Leave_With_Full_Motion()
    {
        var split = Create();
        split.Label = "Copy page";
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { split } }, Motion.ArcMotionLevel.Full);
        try
        {
            var label = TestHelpers.Descendant<SplitButtonLabel>(split);
            split.Label = "Copied";
            // The morph starts synchronously: "y page" leaves while the shared "Cop" stays.
            label.LeavingCount.ShouldBe(6);
            string.Concat(label.PresentGlyphs).ShouldBe("Copied");
            WaitFor(() => label.LeavingCount == 0).ShouldBeTrue();
            string.Concat(label.PresentGlyphs).ShouldBe("Copied");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Primary_Press_Scales_The_Group_But_The_Chevron_Does_Not()
    {
        var split = Create();
        split.Icon = ArcIcons.Copy;
        var window = Show(split);
        try
        {
            split.Classes.ShouldContain(SplitButton.HasIconPseudoClass);
            var primary = Primary(split);
            var center = primary.TranslatePoint(new Point(primary.Bounds.Width / 2, primary.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            split.Classes.ShouldContain(SplitButton.PrimaryPressedPseudoClass);
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            split.Classes.ShouldNotContain(SplitButton.PrimaryPressedPseudoClass);

            var chevron = Chevron(split);
            var chevronCenter = chevron.TranslatePoint(new Point(chevron.Bounds.Width / 2, chevron.Bounds.Height / 2), window)!.Value;
            window.MouseDown(chevronCenter, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            split.Classes.ShouldNotContain(SplitButton.PrimaryPressedPseudoClass);
            window.MouseUp(chevronCenter, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            split.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>The share sheet: initials and tints, where it opens, sharing, actions, the keyboard and automation.</summary>
public class ShareSheetTests
{
    private static (ShareSheet Sheet, Avalonia.Controls.Button Trigger) Sheet(ShareSheetPresentation presentation)
    {
        var trigger = new Avalonia.Controls.Button { Content = "Share" };
        var sheet = new ShareSheet { Content = trigger, Presentation = presentation, Link = "https://example.com/a" };
        sheet.People.Add(new ShareSheetTarget { Name = "Mira Okafor", Detail = "mira@example.com" });
        sheet.People.Add(new ShareSheetTarget { Name = "Tomas Reyes" });
        sheet.People.Add(new ShareSheetTarget { Name = "Ana Lind" });
        sheet.Destinations.Add(new ShareSheetTarget { Name = "Email", Glyph = ArcIcons.Mail });
        sheet.Actions.Add(new ShareSheetAction { Label = "Download", Glyph = ArcIcons.Download });
        return (sheet, trigger);
    }

    private static void Click(Avalonia.Controls.Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Initials_come_from_the_first_and_last_words()
    {
        new ShareSheetTarget { Name = "Mira Okafor" }.EffectiveInitials.ShouldBe("MO");
        new ShareSheetTarget { Name = "ana van der berg" }.EffectiveInitials.ShouldBe("AB");
        new ShareSheetTarget { Name = "kofi" }.EffectiveInitials.ShouldBe("K");
        new ShareSheetTarget().EffectiveInitials.ShouldBe("?");
        new ShareSheetTarget { Name = "Mira Okafor", Initials = "M" }.EffectiveInitials.ShouldBe("M");
    }

    [Fact]
    public void A_name_keeps_its_tint()
    {
        ShareSheetPanel.TintOf("Mira Okafor").ShouldBe(ShareSheetPanel.TintOf("Mira Okafor"));
        foreach (var name in new[] { "A", "Bo", "Cyd", "Dee Dee", "", null })
        {
            ShareSheetPanel.TintOf(name).ShouldBeInRange(0, 3);
        }
    }

    [AvaloniaFact]
    public void The_trigger_opens_a_popover_with_every_target_and_action()
    {
        var (sheet, trigger) = Sheet(ShareSheetPresentation.Popover);
        var window = TestHelpers.Show(sheet);
        try
        {
            Click(trigger);
            sheet.IsOpen.ShouldBeTrue();
            sheet.PopoverHost!.IsOpen.ShouldBeTrue();
            sheet.SheetHost.ShouldBeNull();
            var panel = sheet.Panel!;
            panel.People.Count.ShouldBe(3);
            panel.Destinations.Count.ShouldBe(1);
            panel.Actions.Count.ShouldBe(1);
            panel.Classes.Contains(ShareSheetPanel.SheetPseudoClass).ShouldBeFalse();

            Click(trigger);
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_a_person_raises_shared_and_closes()
    {
        var (sheet, _) = Sheet(ShareSheetPresentation.Popover);
        ShareSheetTargetEventArgs? shared = null;
        sheet.Shared += (_, e) => shared = e;
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            sheet.Panel!.People[1].Invoke();
            Dispatcher.UIThread.RunJobs();
            shared.ShouldNotBeNull();
            shared.Target.Name.ShouldBe("Tomas Reyes");
            shared.IsPerson.ShouldBeTrue();
            sheet.IsOpen.ShouldBeFalse();
            sheet.PopoverHost!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_action_raises_its_event_and_runs_its_command()
    {
        var (sheet, _) = Sheet(ShareSheetPresentation.Popover);
        var ran = 0;
        sheet.Actions[0].Command = new CommandProbe(() => ran++);
        ShareSheetAction? invoked = null;
        sheet.ActionInvoked += (_, e) => invoked = e.Action;
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var peer = ControlAutomationPeer.CreatePeerForElement(sheet.Panel!.Actions[0]);
            peer.GetName().ShouldBe("Download");
            ((IInvokeProvider)peer).Invoke();
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBeSameAs(sheet.Actions[0]);
            ran.ShouldBe(1);
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_sheet_presentation_rises_from_the_bottom_and_closes_with_the_sheet()
    {
        var (sheet, _) = Sheet(ShareSheetPresentation.Sheet);
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            sheet.SheetHost!.IsOpen.ShouldBeTrue();
            sheet.Panel!.Classes.Contains(ShareSheetPanel.SheetPseudoClass).ShouldBeTrue();
            sheet.SheetHost.Close();
            Dispatcher.UIThread.RunJobs();
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_opens_a_sheet_on_a_narrow_viewport()
    {
        var (sheet, _) = Sheet(ShareSheetPresentation.Auto);
        var window = TestHelpers.Show(sheet);
        try
        {
            // The test window is 400 px wide, under the 640 px compact width.
            sheet.UsesSheet.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_arrows_move_along_a_strip_with_one_tab_stop()
    {
        var (sheet, _) = Sheet(ShareSheetPresentation.Popover);
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var people = sheet.Panel!.People;
            people.Count(p => p.IsTabStop).ShouldBe(1);
            people[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(people[1]);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(people[2]);
            people[2].IsTabStop.ShouldBeTrue();
            people[0].IsTabStop.ShouldBeFalse();
            var peer = ControlAutomationPeer.CreatePeerForElement(people[0]);
            peer.GetName().ShouldBe("Mira Okafor");
            peer.GetHelpText().ShouldBe("mira@example.com");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class CommandProbe : System.Windows.Input.ICommand
    {
        private readonly System.Action _run;

        public CommandProbe(System.Action run) => _run = run;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run();
    }
}

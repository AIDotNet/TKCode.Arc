using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class TogglesAutomationTests
{
    [AvaloniaFact]
    public void Groups_Are_Groups_And_Options_Are_Radios()
    {
        var group = new RadioGroup { Header = "Plan", ItemsSource = new[] { new RadioOption("a", "A"), new RadioOption("b", "B") }, SelectedValue = "b" };
        var window = TestHelpers.Show(group);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(group);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Plan");
            var option = ControlAutomationPeer.CreatePeerForElement(group.ContainerFromIndex(0)!);
            option.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
            option.GetName().ShouldBe("A");
            var selection = option.GetProvider<ISelectionItemProvider>()!;
            selection.IsSelected.ShouldBeFalse();
            selection.Select();
            Dispatcher.UIThread.RunJobs();
            group.SelectedValue.ShouldBe("a");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segments_Toggle_And_Checkbox_Is_A_CheckBox()
    {
        var segmented = new SegmentedControl { ItemsSource = new[] { "List", "Board" }, SelectedIndex = 0 };
        var box = new Checkbox { Content = "Terms", IsChecked = null };
        var window = TestHelpers.Show(new StackPanel { Children = { segmented, box } });
        try
        {
            var segment = ControlAutomationPeer.CreatePeerForElement(segmented.ContainerFromIndex(1)!);
            segment.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            var toggle = segment.GetProvider<IToggleProvider>()!;
            toggle.ToggleState.ShouldBe(ToggleState.Off);
            toggle.Toggle();
            Dispatcher.UIThread.RunJobs();
            segmented.SelectedIndex.ShouldBe(1);
            var check = ControlAutomationPeer.CreatePeerForElement(box);
            check.GetAutomationControlType().ShouldBe(AutomationControlType.CheckBox);
            check.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Indeterminate);
        }
        finally
        {
            window.Close();
        }
    }
}

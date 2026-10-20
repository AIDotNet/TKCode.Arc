using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Expand;

public class AccordionTests
{
    private static Accordion Faq(int count = 3)
    {
        var accordion = new Accordion();
        for (var i = 0; i < count; i++)
        {
            accordion.Items.Add(new AccordionItem { Header = $"Question {i}", Content = $"Answer {i} with enough words to wrap a little." });
        }
        return accordion;
    }

    private static AccordionItem Row(Accordion accordion, int index) => (AccordionItem)accordion.ContainerFromIndex(index)!;

    private static Control Panel(AccordionItem row) => row.GetVisualDescendants().OfType<Control>().First(c => c.Name == AccordionItem.PanelPart);

    private static AccordionTrigger Trigger(AccordionItem row) => row.GetVisualDescendants().OfType<AccordionTrigger>().First();

    [AvaloniaFact]
    public void First_Row_Opens_By_Default_And_Others_Are_Collapsed()
    {
        var accordion = Faq();
        var window = TestHelpers.Show(accordion);
        try
        {
            accordion.ExpandedIndex.ShouldBe(0);
            Row(accordion, 0).IsExpanded.ShouldBeTrue();
            Panel(Row(accordion, 0)).Bounds.Height.ShouldBeGreaterThan(0);
            Panel(Row(accordion, 1)).Bounds.Height.ShouldBe(0);
            Trigger(Row(accordion, 0)).Bounds.Height.ShouldBeGreaterThanOrEqualTo(50);
            // A closed answer is out of reach.
            Row(accordion, 1).GetVisualDescendants().OfType<Control>().First(c => c.Name == AccordionItem.AnswerPart).IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Mode_Keeps_One_Row_Open_And_The_Open_Row_Can_Close()
    {
        var accordion = Faq();
        var window = TestHelpers.Show(accordion);
        try
        {
            Trigger(Row(accordion, 2)).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Row(accordion, 2).IsExpanded.ShouldBeTrue();
            Row(accordion, 0).IsExpanded.ShouldBeFalse();
            accordion.ExpandedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Row(accordion, 2).IsExpanded.ShouldBeFalse();
            accordion.ExpandedIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandedIndex_Is_Two_Way()
    {
        var accordion = Faq();
        accordion.ExpandedIndex = -1;
        var window = TestHelpers.Show(accordion);
        try
        {
            Row(accordion, 0).IsExpanded.ShouldBeFalse();
            accordion.ExpandedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Row(accordion, 1).IsExpanded.ShouldBeTrue();
            Panel(Row(accordion, 1)).Bounds.Height.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Multiple_Mode_Opens_Rows_Independently()
    {
        var accordion = Faq();
        accordion.ExpandMode = AccordionExpandMode.Multiple;
        var window = TestHelpers.Show(accordion);
        try
        {
            Row(accordion, 1).IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Row(accordion, 0).IsExpanded.ShouldBeTrue();
            Row(accordion, 1).IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Triggers_And_Loop()
    {
        var accordion = Faq();
        var window = TestHelpers.Show(accordion);
        try
        {
            Trigger(Row(accordion, 0)).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Trigger(Row(accordion, 1)).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Trigger(Row(accordion, 2)).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Trigger(Row(accordion, 0)).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Trigger(Row(accordion, 2)).IsFocused.ShouldBeTrue();
            // Focus only moves; nothing toggles.
            accordion.ExpandedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Large_Size_Uses_Taller_Rows()
    {
        var accordion = Faq();
        accordion.Size = AccordionSize.Large;
        var window = TestHelpers.Show(accordion);
        try
        {
            Trigger(Row(accordion, 1)).Bounds.Height.ShouldBeGreaterThanOrEqualTo(76);
            Row(accordion, 1).Classes.Contains(":large").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Height_Springs_With_Full_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var accordion = Faq();
        var window = TestHelpers.Show(accordion, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            Row(accordion, 1).IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            var opening = Panel(Row(accordion, 1)).Bounds.Height;
            var closing = Panel(Row(accordion, 0)).Bounds.Height;
            opening.ShouldBeGreaterThan(0);
            closing.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            Panel(Row(accordion, 1)).Bounds.Height.ShouldBeGreaterThan(opening);
            Panel(Row(accordion, 0)).Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}

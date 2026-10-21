using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>The shared Settings parts: rows stack when narrow, label clicks toggle, hairlines between rows only.</summary>
public class SettingsGroupTests
{
    private static SettingsGroup Group(out SettingsRow first, out SettingsRow second, out Switch toggle)
    {
        toggle = new Switch();
        first = new SettingsRow { Header = "Mentions", Description = "When someone mentions you.", Content = toggle };
        second = new SettingsRow { Header = "Digest", Content = new Button { Content = "Edit" } };
        return new SettingsGroup { Header = "Notifications", Width = 640, Items = { first, second } };
    }

    [AvaloniaFact]
    public void A_Narrow_Row_Moves_Its_Control_Under_The_Text()
    {
        var group = Group(out var first, out _, out _);
        var window = TestHelpers.Show(group);
        try
        {
            first.IsStacked.ShouldBeFalse();
            first.Classes.Contains(SettingsRow.StackedPseudoClass).ShouldBeFalse();

            group.Width = 360;
            Dispatcher.UIThread.RunJobs();
            first.IsStacked.ShouldBeTrue();
            var presenter = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(first, "PART_ContentPresenter");
            Grid.GetRow(presenter).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Label_Toggles_A_Toggle_Control()
    {
        var group = Group(out var first, out _, out var toggle);
        var window = TestHelpers.Show(group);
        try
        {
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var label = TestHelpers.Descendant<StackPanel>(first, SettingsRow.LabelPart);
            var point = label.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            toggle.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Rows_After_The_First_Have_A_Hairline()
    {
        var group = Group(out var first, out var second, out _);
        var window = TestHelpers.Show(group);
        try
        {
            TestHelpers.Descendant<Border>(first, "PART_Divider").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Border>(second, "PART_Divider").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Control_Takes_The_Header_And_Description_Unless_It_Has_Its_Own()
    {
        var group = Group(out var first, out var second, out var toggle);
        var window = TestHelpers.Show(group);
        try
        {
            AutomationProperties.GetName(toggle).ShouldBe("Mentions");
            AutomationProperties.GetHelpText(toggle).ShouldBe("When someone mentions you.");
            first.Header = "Replies";
            AutomationProperties.GetName(toggle).ShouldBe("Replies");

            var named = new Button { Content = "Edit" };
            AutomationProperties.SetName(named, "Edit digest");
            second.Content = named;
            AutomationProperties.GetName(named).ShouldBe("Edit digest");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Plain_Group_Has_No_Frame()
    {
        var group = Group(out _, out _, out _);
        group.Appearance = SettingsGroupAppearance.Plain;
        var window = TestHelpers.Show(group);
        try
        {
            group.Classes.Contains(SettingsGroup.PlainPseudoClass).ShouldBeTrue();
            group.BorderThickness.ShouldBe(new Thickness(0));
        }
        finally
        {
            window.Close();
        }
    }
}

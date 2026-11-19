using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Ribbons;

/// <summary>How groups lay out their commands and shrink, step by step, when the ribbon is narrow.</summary>
public class RibbonLayoutTests
{
    internal static Window Show(RibbonSample sample, double width)
    {
        var window = TestHelpers.Show(sample.Ribbon);
        window.Width = width;
        window.Height = 400;
        sample.Ribbon.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    internal static void Resize(Window window, double width)
    {
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
    }

    private static double RowWidth(RibbonSample sample) =>
        sample.Home.GroupsPanel.ShouldNotBeNull().DesiredSize.Width;

    [AvaloniaFact]
    public void A_Wide_Ribbon_Shows_Every_Command_At_Its_Own_Size()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            foreach (var group in sample.HomeGroups)
            {
                group.Level.ShouldBe(0);
                group.IsCollapsed.ShouldBeFalse();
            }
            sample.Paste.EffectiveSize.ShouldBe(RibbonItemSize.Large);
            sample.Cut.EffectiveSize.ShouldBe(RibbonItemSize.Medium);
            sample.Bold.EffectiveSize.ShouldBe(RibbonItemSize.Small);
            sample.Paste.Classes.Contains(RibbonButton.LargePseudoClass).ShouldBeTrue();
            sample.Ribbon.SelectedTab.ShouldBe(sample.Home);
            sample.Home.IsSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Large_Commands_Take_The_Full_Height_And_Medium_Ones_Stack_In_A_Column()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var panel = sample.Clipboard.ItemsPanel;
            sample.Paste.Bounds.Height.ShouldBe(panel.ContentHeight);
            // Cut, Copy and Format painter share one column after Paste, top to bottom.
            sample.Cut.Bounds.X.ShouldBe(sample.Copy.Bounds.X);
            sample.Copy.Bounds.X.ShouldBe(sample.Painter.Bounds.X);
            sample.Cut.Bounds.X.ShouldBeGreaterThan(sample.Paste.Bounds.Right);
            sample.Cut.Bounds.Y.ShouldBeLessThan(sample.Copy.Bounds.Y);
            sample.Copy.Bounds.Y.ShouldBeLessThan(sample.Painter.Bounds.Y);
            sample.Painter.Bounds.Bottom.ShouldBeLessThanOrEqualTo(panel.ContentHeight);
            // Each command is arranged at the natural size it reported, so nothing is cut off.
            sample.Cut.Bounds.Width.ShouldBe(sample.Cut.GetNaturalSize(RibbonItemSize.Medium).Width, 0.5);
            sample.Paste.Bounds.Width.ShouldBe(sample.Paste.GetNaturalSize(RibbonItemSize.Large).Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Width_A_Group_Reports_For_A_Step_Is_The_Width_It_Takes()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            for (var step = 0; step <= RibbonGroup.CollapsedLevel; step++)
            {
                foreach (var group in sample.HomeGroups)
                {
                    var reported = group.GetWidth(step);
                    group.SetLevel(step);
                    group.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    group.DesiredSize.Width.ShouldBe(reported, 0.5, $"{group.Header} at step {step}");
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Separator_Starts_A_New_Column()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var separator = sample.Font.Items.OfType<RibbonSeparator>().Single();
            var grow = (RibbonButton)sample.Font.Items[3];
            separator.Bounds.X.ShouldBeGreaterThan(sample.Bold.Bounds.Right);
            grow.Bounds.X.ShouldBeGreaterThan(separator.Bounds.Right);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Groups_Shrink_Step_By_Step_And_Never_Grow_As_The_Ribbon_Narrows()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var full = RowWidth(sample);
            var previous = sample.HomeGroups.Select(g => g.Level).ToArray();
            for (var width = (int)full + 40; width >= 120; width -= 20)
            {
                Resize(window, width);
                var levels = sample.HomeGroups.Select(g => g.Level).ToArray();
                for (var i = 0; i < levels.Length; i++)
                {
                    levels[i].ShouldBeGreaterThanOrEqualTo(previous[i], $"group {i} grew back at width {width}");
                }
                previous = levels;
                var available = sample.Ribbon.Bounds.Width;
                if (sample.HomeGroups.Any(g => g.Level < g.MaxLevel))
                {
                    RowWidth(sample).ShouldBeLessThanOrEqualTo(available, $"row overflows at width {width}");
                }
            }
            // At the narrowest width every group has taken every step that makes it narrower (collapsing is skipped where the
            // group's button would be wider than its small commands).
            foreach (var group in sample.HomeGroups)
            {
                var narrowest = Enumerable.Range(0, group.MaxLevel + 1).Min(group.GetWidth);
                group.GetWidth(group.Level).ShouldBe(narrowest, 0.5);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Widening_Again_Restores_The_Same_Layout()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 700);
        try
        {
            var at700 = sample.HomeGroups.Select(g => g.Level).ToArray();
            Resize(window, 300);
            Resize(window, 1600);
            sample.HomeGroups.ShouldAllBe(g => g.Level == 0);
            Resize(window, 700);
            sample.HomeGroups.Select(g => g.Level).ToArray().ShouldBe(at700);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Equal_Groups_Take_Turns_From_The_Right()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var full = RowWidth(sample);
            // Just a little too narrow: only the rightmost group that can get narrower takes a step.
            Resize(window, full - 4);
            sample.Arrange.Level.ShouldBeGreaterThan(0);
            sample.Clipboard.Level.ShouldBe(0);
            sample.Insert.Level.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Higher_Reduction_Priority_Shrinks_All_The_Way_First()
    {
        var sample = new RibbonSample();
        sample.Clipboard.ReductionPriority = 5;
        var window = Show(sample, 1600);
        try
        {
            var full = RowWidth(sample);
            Resize(window, full - 4);
            sample.Clipboard.Level.ShouldBeGreaterThan(0);
            sample.Arrange.Level.ShouldBe(0);
            // Narrow enough to need all of Clipboard's savings: Clipboard reaches its narrowest form before the others move.
            var levels = Enumerable.Range(0, sample.Clipboard.MaxLevel + 1);
            var saving = sample.Clipboard.GetWidth(0) - levels.Min(sample.Clipboard.GetWidth);
            var chrome = sample.Home.Padding.Left + sample.Home.Padding.Right;
            Resize(window, full + chrome - saving + 2);
            sample.Clipboard.GetWidth(sample.Clipboard.Level).ShouldBe(levels.Min(sample.Clipboard.GetWidth), 0.5);
            sample.Arrange.Level.ShouldBe(0);
            sample.Insert.Level.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CanCollapse_False_Stops_At_Small()
    {
        var sample = new RibbonSample();
        foreach (var group in sample.HomeGroups)
        {
            group.CanCollapse = false;
        }
        var window = Show(sample, 100);
        try
        {
            sample.HomeGroups.ShouldAllBe(g => !g.IsCollapsed);
            sample.Paste.EffectiveSize.ShouldBe(RibbonItemSize.Small);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MinSize_Keeps_A_Command_Large()
    {
        var sample = new RibbonSample();
        sample.Paste.MinSize = RibbonItemSize.Large;
        sample.Clipboard.CanCollapse = false;
        var window = Show(sample, 100);
        try
        {
            sample.Paste.EffectiveSize.ShouldBe(RibbonItemSize.Large);
            sample.Cut.EffectiveSize.ShouldBe(RibbonItemSize.Small);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Label_Change_Lays_The_Group_Out_Again()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var before = sample.Insert.Bounds.Width;
            sample.Node.Label = "A much longer node label";
            Dispatcher.UIThread.RunJobs();
            sample.Insert.Bounds.Width.ShouldBeGreaterThan(before);
            sample.Node.Bounds.Width.ShouldBe(sample.Node.GetNaturalSize(RibbonItemSize.Large).Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Commands_And_Groups_Take_No_Room()
    {
        var sample = new RibbonSample();
        var window = Show(sample, 1600);
        try
        {
            var groupWidth = sample.Clipboard.Bounds.Width;
            sample.Paste.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            sample.Clipboard.Bounds.Width.ShouldBeLessThan(groupWidth);
            var fontX = sample.Font.Bounds.X;
            sample.Clipboard.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            sample.Font.Bounds.X.ShouldBeLessThan(fontX);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Any_Control_Can_Sit_In_A_Group()
    {
        var sample = new RibbonSample();
        var field = new Border { Width = 90, Height = 24 };
        sample.Arrange.Items.Add(field);
        var window = Show(sample, 1600);
        try
        {
            field.Bounds.Width.ShouldBe(90);
            field.GetVisualParent().ShouldBeOfType<RibbonGroupItemsPanel>();
            field.Parent.ShouldBe(sample.Arrange);
        }
        finally
        {
            window.Close();
        }
    }
}

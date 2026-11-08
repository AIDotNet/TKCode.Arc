using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Layout;

/// <summary>WaterfallPanel: shortest-lane placement, lane sizes, item size limits, FillLastItem and ItemsControl use.</summary>
public class WaterfallPanelTests
{
    private static Border Item(double height, double width = double.NaN) => new() { Height = height, Width = width };

    private static WaterfallPanel Panel(double width, double height, params Control[] children)
    {
        var panel = new WaterfallPanel
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.Children.AddRange(children);
        return panel;
    }

    private static Rect Layout(Control panel, int i) => ((Panel)panel).Children[i].Bounds;

    /// <summary>Shows content in a window tall enough that no panel here is cut to the window.</summary>
    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 1000;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Theme_Gives_The_Token_Spacing()
    {
        var panel = new WaterfallPanel();
        var window = Show(panel);
        try
        {
            panel.Spacing.ShouldBe(12);
            panel.MinLaneSize.ShouldBe(200);
            panel.Columns.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Child_Goes_To_The_Shortest_Column_First_Column_On_Ties()
    {
        // Three 100px columns with 10px gaps: (320 - 20) / 3.
        var panel = Panel(320, double.NaN, Item(120), Item(200), Item(150), Item(50), Item(80), Item(10));
        panel.Spacing = 10;
        var window = Show(panel);
        try
        {
            Layout(panel, 0).ShouldBe(new Rect(0, 0, 100, 120));
            Layout(panel, 1).ShouldBe(new Rect(110, 0, 100, 200));
            Layout(panel, 2).ShouldBe(new Rect(220, 0, 100, 150));
            // Column 0 ends at 120: shortest.
            Layout(panel, 3).ShouldBe(new Rect(0, 130, 100, 50));
            // Column 0 ends at 180, column 2 at 150.
            Layout(panel, 4).ShouldBe(new Rect(220, 160, 100, 80));
            // Column 0 (180) is now shortest.
            Layout(panel, 5).ShouldBe(new Rect(0, 190, 100, 10));
            // Height is the longest column: 0..240 in column 2.
            panel.Bounds.Height.ShouldBe(240);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Uses_Rows_And_Keeps_Child_Widths()
    {
        var panel = Panel(double.NaN, 210, Item(double.NaN, 120), Item(double.NaN, 60), Item(double.NaN, 40));
        panel.Orientation = Orientation.Horizontal;
        panel.Rows = 2;
        panel.Spacing = 10;
        var window = Show(panel);
        try
        {
            panel.LaneCount.ShouldBe(2);
            Layout(panel, 0).ShouldBe(new Rect(0, 0, 120, 100));
            Layout(panel, 1).ShouldBe(new Rect(0, 110, 60, 100));
            Layout(panel, 2).ShouldBe(new Rect(70, 110, 40, 100));
            panel.Bounds.Width.ShouldBe(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Min_And_Max_Item_Size_Clamp_The_Flow_Size()
    {
        // Content of a given height in a stretchable border, so the border takes whatever the panel gives it.
        static Border Content(double height) => new() { Child = new Border { Height = height } };
        var panel = Panel(200, double.NaN, new Border(), Content(500), Content(30));
        panel.Columns = 1;
        panel.Spacing = 0;
        panel.MinItemSize = 40;
        panel.MaxItemSize = 300;
        var window = Show(panel);
        try
        {
            Layout(panel, 0).Height.ShouldBe(40);
            Layout(panel, 1).Height.ShouldBe(300);
            Layout(panel, 1).Y.ShouldBe(40);
            Layout(panel, 2).Height.ShouldBe(40);
            panel.Bounds.Height.ShouldBe(380);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Child_Minimum_Wins_Over_MaxItemSize()
    {
        var panel = Panel(200, double.NaN, new Border { MinHeight = 120 });
        panel.Columns = 1;
        panel.MaxItemSize = 80;
        var window = Show(panel);
        try
        {
            Layout(panel, 0).Height.ShouldBe(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FillLastItem_Stretches_The_Last_Child_Of_Each_Column_To_The_Longest()
    {
        var panel = Panel(210, double.NaN, new Border { MinHeight = 100 }, new Border { MinHeight = 40 }, new Border { MinHeight = 30 });
        panel.Columns = 2;
        panel.Spacing = 10;
        panel.FillLastItem = true;
        var window = Show(panel);
        try
        {
            // Column 0: item 0 (0..100). Column 1: item 1 (0..40), item 2 (50..80) stretched to 100.
            Layout(panel, 0).ShouldBe(new Rect(0, 0, 100, 100));
            Layout(panel, 1).ShouldBe(new Rect(110, 0, 100, 40));
            Layout(panel, 2).ShouldBe(new Rect(110, 50, 100, 50));
            panel.Bounds.Height.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FillLastItem_Reaches_The_Panel_End_When_The_Panel_Is_Taller()
    {
        var panel = Panel(210, 300, new Border { MinHeight = 100 }, new Border { MinHeight = 40 });
        panel.Columns = 2;
        panel.Spacing = 10;
        panel.FillLastItem = true;
        var window = Show(panel);
        try
        {
            Layout(panel, 0).Height.ShouldBe(300);
            Layout(panel, 1).Height.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsed_Children_Take_No_Place()
    {
        var hidden = Item(500);
        hidden.IsVisible = false;
        var panel = Panel(210, double.NaN, Item(50), hidden, Item(70));
        panel.Columns = 2;
        panel.Spacing = 10;
        var window = Show(panel);
        try
        {
            Layout(panel, 2).ShouldBe(new Rect(110, 0, 100, 70));
            panel.Bounds.Height.ShouldBe(70);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zero_Columns_Fit_As_Many_As_MinLaneSize_Allows_And_Follow_The_Width()
    {
        var panel = Panel(650, double.NaN, Item(10), Item(10), Item(10), Item(10));
        panel.Columns = 0;
        panel.MinLaneSize = 200;
        panel.Spacing = 10;
        var window = Show(panel);
        try
        {
            // (650 + 10) / 210 = 3.14 -> 3 columns.
            panel.LaneCount.ShouldBe(3);
            panel.Width = 400;
            Dispatcher.UIThread.RunJobs();
            panel.LaneCount.ShouldBe(1);
            Layout(panel, 3).ShouldBe(new Rect(0, 60, 400, 10));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_And_Removing_Children_Relays_Out()
    {
        var panel = Panel(210, double.NaN, Item(50), Item(20));
        panel.Columns = 2;
        panel.Spacing = 10;
        var window = Show(panel);
        try
        {
            panel.Children.Add(Item(30));
            Dispatcher.UIThread.RunJobs();
            Layout(panel, 2).ShouldBe(new Rect(110, 30, 100, 30));
            panel.Children.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            // Now 20 and 30: one per column.
            Layout(panel, 1).ShouldBe(new Rect(110, 0, 100, 30));
            panel.Bounds.Height.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Works_As_An_ItemsPanel()
    {
        var items = new ItemsControl
        {
            Width = 210,
            ItemsSource = new[] { 50.0, 20.0, 30.0 },
            ItemsPanel = new FuncTemplate<Panel?>(() => new WaterfallPanel { Columns = 2, Spacing = 10 }),
            ItemTemplate = new FuncDataTemplate<double>((h, _) => new Border { Height = h }),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = Show(items);
        try
        {
            var panel = items.GetVisualDescendants().OfType<WaterfallPanel>().Single();
            panel.Children.Count.ShouldBe(3);
            panel.Children[2].Bounds.ShouldBe(new Rect(110, 30, 100, 30));
            items.Bounds.Height.ShouldBe(60);
        }
        finally
        {
            window.Close();
        }
    }
}

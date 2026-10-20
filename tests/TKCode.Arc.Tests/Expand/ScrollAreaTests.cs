using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Expand;

public class ScrollAreaTests
{
    private static ScrollArea Tall(double height = 200, int rows = 30)
    {
        var stack = new StackPanel();
        for (var i = 0; i < rows; i++)
        {
            stack.Children.Add(new Border { Height = 40, Child = new TextBlock { Text = $"Row {i}" } });
        }
        return new ScrollArea { Height = height, Width = 300, Content = stack };
    }

    private static Border Presenter(ScrollArea area) =>
        area.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_FadeHostY");

    [AvaloniaFact]
    public void Edges_Fade_Only_Where_Content_Continues()
    {
        var area = Tall();
        var edges = new List<ScrollAreaEdges>();
        area.EdgesChanged += (_, e) => edges.Add(e.Edges);
        var window = TestHelpers.Show(area);
        try
        {
            area.Edges.ShouldBe(new ScrollAreaEdges(false, true, false, false));
            var mask = Presenter(area).OpacityMask.ShouldBeAssignableTo<IGradientBrush>()!;
            mask.GradientStops[1].Offset.ShouldBe(0);
            mask.GradientStops[2].Offset.ShouldBe(1 - 28.0 / 200, 0.001);

            area.Offset = new Vector(0, 10);
            Dispatcher.UIThread.RunJobs();
            // The top fade is exactly as long as the content hidden above (10px), up to 28px.
            mask = Presenter(area).OpacityMask.ShouldBeAssignableTo<IGradientBrush>()!;
            mask.GradientStops[1].Offset.ShouldBe(10.0 / 200, 0.001);
            area.Edges.ShouldBe(new ScrollAreaEdges(true, true, false, false));

            area.Offset = new Vector(0, area.MaxOffset.Y);
            Dispatcher.UIThread.RunJobs();
            area.Edges.ShouldBe(new ScrollAreaEdges(true, false, false, false));
            edges.Last().ShouldBe(new ScrollAreaEdges(true, false, false, false));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_That_Fits_Has_No_Fade_And_No_Track()
    {
        var area = Tall(height: 400, rows: 3);
        var window = TestHelpers.Show(area);
        try
        {
            Presenter(area).OpacityMask.ShouldBeNull();
            area.VerticalTrack!.IsVisible.ShouldBeFalse();
            area.Classes.Contains(":overflow").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumb_Follows_The_Offset_With_A_Minimum_Length()
    {
        var area = Tall(height: 200, rows: 200);
        var window = TestHelpers.Show(area);
        try
        {
            var track = area.VerticalTrack!;
            track.IsVisible.ShouldBeTrue();
            track.ThumbLength.ShouldBe(28);
            track.ThumbOffset.ShouldBe(0);
            area.Offset = new Vector(0, area.MaxOffset.Y);
            Dispatcher.UIThread.RunJobs();
            track.ThumbOffset.ShouldBe(track.Bounds.Height - 28, 0.01);
            track.Bounds.Width.ShouldBe(11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrollbars_Show_While_Scrolling_And_Always_Mode_Keeps_Them()
    {
        var area = Tall();
        var window = TestHelpers.Show(area);
        try
        {
            area.AreScrollbarsShown.ShouldBeFalse();
            area.Offset = new Vector(0, 30);
            Dispatcher.UIThread.RunJobs();
            area.AreScrollbarsShown.ShouldBeTrue();
            area.VerticalTrack!.Shown.ShouldBe(1);
            var always = Tall();
            always.OverlayScrollbarMode = ScrollAreaScrollbarMode.Always;
            window.Close();
            window = TestHelpers.Show(always);
            always.AreScrollbarsShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Scrolls_The_Focused_Area()
    {
        var area = Tall();
        var window = TestHelpers.Show(area);
        try
        {
            area.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            area.Offset.Y.ShouldBe(40);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            area.Offset.Y.ShouldBe(area.MaxOffset.Y);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            area.Offset.Y.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            area.Offset.Y.ShouldBe(180, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Area_Turns_The_Wheel_Sideways_While_It_Can()
    {
        var strip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        for (var i = 0; i < 20; i++)
        {
            strip.Children.Add(new Border { Width = 100, Height = 50 });
        }
        var area = new ScrollArea { Orientation = ScrollAreaOrientation.Horizontal, Width = 300, Content = strip };
        var window = TestHelpers.Show(area);
        try
        {
            var center = area.TranslatePoint(new Point(150, 25), window)!.Value;
            window.MouseWheel(center, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            area.Offset.X.ShouldBe(50);
            area.Offset.Y.ShouldBe(0);
            area.Edges.Left.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

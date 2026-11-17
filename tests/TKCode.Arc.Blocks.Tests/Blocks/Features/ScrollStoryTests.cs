using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Features;

public class ScrollStoryTests
{
    private static (Window Window, ScrollViewer Viewer) Show(ScrollStory story, double width = 1100)
    {
        var viewer = new ScrollViewer { Content = story };
        var window = TestHelpers.Show(viewer);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return (window, viewer);
    }

    [AvaloniaFact]
    public void Defaults_Tell_The_Sample_Story_With_The_First_Step_Active()
    {
        var story = new ScrollStory();
        var (window, _) = Show(story);
        try
        {
            story.Views.Count.ShouldBe(4);
            story.ActiveIndex.ShouldBe(0);
            story.Views[0].IsActive.ShouldBeTrue();
            story.Views[1].IsActive.ShouldBeFalse();
            story.Stage!.Scene.ShouldBe(ScrollStoryScene.Capture);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Reading_Line_Picks_The_Step_And_The_Stage_Stays_In_View()
    {
        var story = new ScrollStory();
        var changes = new List<int>();
        story.ActiveStepChanged += (_, e) => changes.Add(e.Index);
        var (window, _) = Show(story);
        try
        {
            var layout = story.Layout!;
            var third = story.Views[2].Bounds;
            // A viewport whose reading line (45% down) crosses the third step.
            var viewport = new Rect(0, third.Center.Y - 700 * 0.45, layout.Bounds.Width, 700);
            layout.UpdateViewport(viewport);
            story.ActiveIndex.ShouldBe(2);
            story.Stage!.Scene.ShouldBe(ScrollStoryScene.Review);
            changes.ShouldContain(2);
            layout.PinOffset.ShouldBeGreaterThan(0);
            layout.PinOffset.ShouldBeLessThanOrEqualTo(layout.Bounds.Height);

            layout.UpdateViewport(new Rect(0, -400, layout.Bounds.Width, 700));
            layout.PinOffset.ShouldBe(0);
            story.ActiveIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Move_Between_Steps_And_Make_Them_Active()
    {
        var story = new ScrollStory();
        var (window, _) = Show(story);
        try
        {
            story.Views[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            story.Views[1].IsFocused.ShouldBeTrue();
            story.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            story.ActiveIndex.ShouldBe(3);
            story.Stage!.Scene.ShouldBe(ScrollStoryScene.Launch);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Widths_Stack_The_Steps_With_Their_Own_Pictures()
    {
        var story = new ScrollStory();
        var (window, _) = Show(story, 600);
        try
        {
            story.Classes.Contains(":compact").ShouldBeTrue();
            story.Layout!.IsStacked.ShouldBeTrue();
            story.Views.ShouldAllBe(v => v.IsStacked);
            TestHelpers.Descendant<ScrollStorySceneView>(story.Views[1], "PART_Inline").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Steps_Replace_The_Samples()
    {
        var story = new ScrollStory();
        story.Steps.Add(new ScrollStoryStep { Title = "First", Scene = ScrollStoryScene.Arrange });
        story.Steps.Add(new ScrollStoryStep { Title = "Second", Kicker = "Two" });
        var (window, _) = Show(story);
        try
        {
            story.Views.Select(v => v.Kicker).ShouldBe(["01", "Two"]);
            story.Stage!.Scene.ShouldBe(ScrollStoryScene.Arrange);
        }
        finally
        {
            window.Close();
        }
    }
}

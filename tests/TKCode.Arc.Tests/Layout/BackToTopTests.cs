using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Tests.Layout;

/// <summary>BackToTop: threshold, scrollability, scrolling back (and being interrupted), targets, the attached button, focus and naming.</summary>
public class BackToTopTests
{
    private static StackPanel Rows(int count, double width = double.NaN)
    {
        var panel = new StackPanel { Width = width };
        for (var i = 0; i < count; i++)
        {
            panel.Children.Add(new Border { Height = 40 });
        }
        return panel;
    }

    private static (Grid Root, ScrollViewer Viewer, BackToTop Button) Overlay(int rows = 100, double contentWidth = double.NaN)
    {
        var viewer = new ScrollViewer
        {
            Content = Rows(rows, contentWidth),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var button = new BackToTop { Target = viewer };
        var root = new Grid { Children = { viewer, button } };
        return (root, viewer, button);
    }

    private static void ScrollTo(ScrollViewer viewer, double y)
    {
        viewer.Offset = new Vector(viewer.Offset.X, y);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A ListBox with a template like Fluent's (a ScrollViewer around the items): Arc ships no ListBox theme, so without one
    /// it would have no ScrollViewer to find.
    /// </summary>
    private static ListBox List() => new()
    {
        ItemsSource = Enumerable.Range(1, 200).Select(i => $"Item {i}").ToList(),
        Template = new FuncControlTemplate<ListBox>((_, scope) => new ScrollViewer
        {
            Name = "PART_ScrollViewer",
            Content = new ItemsPresenter { Name = "PART_ItemsPresenter" }.RegisterInNameScope(scope)
        }.RegisterInNameScope(scope))
    };

    private static BackToTopButton Part(BackToTop button) => TestHelpers.Descendant<BackToTopButton>(button);

    [AvaloniaFact]
    public void Shows_Only_Past_The_Threshold()
    {
        var (root, viewer, button) = Overlay();
        TestHelpers.Show(root);
        button.IsShown.ShouldBeFalse();
        button.Classes.Contains(":shown").ShouldBeFalse();

        ScrollTo(viewer, 150);
        button.IsShown.ShouldBeFalse();

        ScrollTo(viewer, 250);
        button.IsShown.ShouldBeTrue();
        button.Classes.Contains(":shown").ShouldBeTrue();

        button.Threshold = 400;
        button.IsShown.ShouldBeFalse();
        button.Threshold = -50;
        button.IsShown.ShouldBeTrue();

        ScrollTo(viewer, 0);
        button.IsShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hides_When_The_Content_Cannot_Scroll()
    {
        var (root, viewer, button) = Overlay(rows: 3);
        button.Threshold = -1;
        TestHelpers.Show(root);
        button.IsShown.ShouldBeFalse();

        var (longRoot, longViewer, longButton) = Overlay();
        TestHelpers.Show(longRoot);
        ScrollTo(longViewer, 1000);
        longButton.IsShown.ShouldBeTrue();
        // The content gets short: the ScrollViewer clamps the offset and nothing is left to scroll.
        ((StackPanel)longViewer.Content!).Children.RemoveRange(3, 97);
        Dispatcher.UIThread.RunJobs();
        longButton.IsShown.ShouldBeFalse();
        viewer.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Click_Scrolls_Back_To_The_Top_And_Keeps_X()
    {
        var (root, viewer, button) = Overlay(contentWidth: 900);
        TestHelpers.Show(root);
        ScrollTo(viewer, 1200);
        viewer.Offset = new Vector(60, 1200);
        Dispatcher.UIThread.RunJobs();
        var reached = 0;
        button.ScrolledToTop += (_, e) =>
        {
            e.Source.ShouldBeSameAs(button);
            reached++;
        };

        Part(button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        viewer.Offset.Y.ShouldBe(0);
        viewer.Offset.X.ShouldBe(60);
        reached.ShouldBe(1);
        button.IsShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ScrollToTop_Raises_ScrolledToTop_That_Bubbles()
    {
        var (root, viewer, button) = Overlay();
        TestHelpers.Show(root);
        ScrollTo(viewer, 900);
        var bubbled = 0;
        root.AddHandler(BackToTop.ScrolledToTopEvent, (_, _) => bubbled++);

        button.ScrollToTop();
        Dispatcher.UIThread.RunJobs();

        viewer.Offset.Y.ShouldBe(0);
        bubbled.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Smooth_Scroll_Stops_When_The_User_Scrolls()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var (root, viewer, button) = Overlay();
        var window = TestHelpers.Show(root, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            ScrollTo(viewer, 2000);
            var reached = 0;
            button.ScrolledToTop += (_, _) => reached++;

            button.ScrollToTop();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Dispatcher.UIThread.RunJobs();
            viewer.Offset.Y.ShouldBeLessThan(2000);
            viewer.Offset.Y.ShouldBeGreaterThan(0);

            // The user takes over: the animation stops and does not pull the offset back.
            ScrollTo(viewer, 1500);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            Dispatcher.UIThread.RunJobs();
            viewer.Offset.Y.ShouldBe(1500);
            reached.ShouldBe(0);

            // Uninterrupted, it arrives and reports it.
            button.ScrollToTop();
            clock.Advance(TimeSpan.FromMilliseconds(700));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => reached == 1, 2000).ShouldBeTrue();
            viewer.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finds_The_ScrollViewer_Inside_A_ListBox()
    {
        var list = List();
        var button = new BackToTop { Target = list };
        var root = new Grid { Children = { list, button } };
        TestHelpers.Show(root);

        var viewer = button.Viewer.ShouldNotBeNull();
        viewer.GetVisualAncestors().ShouldContain(list);

        ScrollTo(viewer, 600);
        button.IsShown.ShouldBeTrue();
        button.ScrollToTop();
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Finds_A_ScrollViewer_Whose_Template_Arrives_Later()
    {
        var list = List();
        var button = new BackToTop();
        var root = new Grid { Children = { button } };
        TestHelpers.Show(root);
        button.Target = list;
        button.Viewer.ShouldBeNull();

        root.Children.Insert(0, list);
        Dispatcher.UIThread.RunJobs();
        button.Viewer.ShouldNotBeNull().GetVisualAncestors().ShouldContain(list);
    }

    [AvaloniaFact]
    public void Attached_Button_Is_Added_To_And_Removed_From_The_Adorner_Layer()
    {
        var list = List();
        list.Height = 300;
        var root = new Panel { Children = { list } };
        TestHelpers.Show(root);
        var layer = AdornerLayer.GetAdornerLayer(list).ShouldNotBeNull();

        BackToTop.SetThreshold(list, 100);
        BackToTop.SetIsAttached(list, true);
        Dispatcher.UIThread.RunJobs();
        var button = BackToTop.AttachedButtonOf(list).ShouldNotBeNull();
        button.GetVisualAncestors().ShouldContain(layer);
        button.Threshold.ShouldBe(100);
        button.Target.ShouldBeSameAs(list);

        ScrollTo(button.Viewer.ShouldNotBeNull(), 400);
        button.IsShown.ShouldBeTrue();
        // Bottom right of the viewport, 16 px in.
        var corner = button.TranslatePoint(new Point(button.Bounds.Width, button.Bounds.Height), list).ShouldNotBeNull();
        corner.X.ShouldBe(list.Bounds.Width - 16, 0.5);
        corner.Y.ShouldBe(list.Bounds.Height - 16, 0.5);

        BackToTop.SetPlacement(list, BackToTopPlacement.TopLeft);
        BackToTop.SetInset(list, new Thickness(8));
        Dispatcher.UIThread.RunJobs();
        var origin = button.TranslatePoint(default, list).ShouldNotBeNull();
        origin.X.ShouldBe(8, 0.5);
        origin.Y.ShouldBe(8, 0.5);

        BackToTop.SetIsAttached(list, false);
        Dispatcher.UIThread.RunJobs();
        BackToTop.AttachedButtonOf(list).ShouldBeNull();
        TopLevel.GetTopLevel(button).ShouldBeNull();
        layer.GetVisualDescendants().OfType<BackToTop>().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Attached_Button_Leaves_With_Its_Host()
    {
        var list = List();
        BackToTop.SetIsAttached(list, true);
        var root = new Panel { Children = { list } };
        TestHelpers.Show(root);
        var layer = AdornerLayer.GetAdornerLayer(list).ShouldNotBeNull();
        var button = BackToTop.AttachedButtonOf(list).ShouldNotBeNull();
        button.GetVisualAncestors().ShouldContain(layer);
        button.Viewer.ShouldNotBeNull();

        root.Children.Remove(list);
        Dispatcher.UIThread.RunJobs();
        layer.GetVisualDescendants().OfType<BackToTop>().ShouldBeEmpty();
        button.Viewer.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Hidden_Button_Takes_No_Input_And_No_Focus()
    {
        var (root, viewer, button) = Overlay();
        TestHelpers.Show(root);
        var part = Part(button);
        part.Focusable.ShouldBeFalse();
        part.IsTabStop.ShouldBeFalse();
        part.IsHitTestVisible.ShouldBeFalse();
        part.Focus().ShouldBeFalse();

        ScrollTo(viewer, 500);
        part.Focusable.ShouldBeTrue();
        part.IsTabStop.ShouldBeTrue();
        part.IsHitTestVisible.ShouldBeTrue();
        part.Focus().ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Size_Sets_The_Circle()
    {
        var (root, _, button) = Overlay();
        TestHelpers.Show(root);
        Part(button).Bounds.Width.ShouldBe(44);
        Part(button).Bounds.Height.ShouldBe(44);
        button.Size = ControlSize.Small;
        Dispatcher.UIThread.RunJobs();
        button.Classes.Contains(":small").ShouldBeTrue();
        Part(button).Bounds.Width.ShouldBe(36);
        button.Size = ControlSize.Large;
        Dispatcher.UIThread.RunJobs();
        Part(button).Bounds.Height.ShouldBe(50);
        TestHelpers.Descendant<ArcGlyph>(button).Size.ShouldBe(20);
    }

    [AvaloniaFact]
    public void Name_Is_Localized_Unless_A_Label_Is_Set()
    {
        var (root, _, button) = Overlay();
        TestHelpers.Show(root);
        var part = Part(button);
        AutomationProperties.GetName(part).ShouldBe("Back to top");
        Tooltip.GetTip(part).ShouldBe("Back to top");

        var languages = ArcLanguageManager.Current!;
        try
        {
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(part).ShouldBe("回到顶部");
            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetTip(part).ShouldBe("回到頂部");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
        }

        button.Label = "Top of the log";
        AutomationProperties.GetName(part).ShouldBe("Top of the log");
        Tooltip.GetTip(part).ShouldBe("Top of the log");
        button.Label = null;
        Dispatcher.UIThread.RunJobs();
        AutomationProperties.GetName(part).ShouldBe("Back to top");
    }
}

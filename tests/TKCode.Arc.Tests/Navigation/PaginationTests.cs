using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Navigation;

public class PaginationTests
{
    private static List<PaginationButton> Pages(Pagination pagination) =>
        pagination.GetVisualDescendants().OfType<PaginationButton>()
                  .Where(b => b.Name!.StartsWith("PART_Page") && b.IsVisible).ToList();

    private static PaginationButton Part(Pagination pagination, string name) =>
        pagination.GetVisualDescendants().OfType<PaginationButton>().First(b => b.Name == name);

    [AvaloniaFact]
    public void Shows_A_Five_Page_Window_Around_The_Current_Page()
    {
        var pagination = new Pagination { PageCount = 12, CurrentPage = 7 };
        var window = TestHelpers.Show(pagination);
        try
        {
            pagination.WindowStart.ShouldBe(5);
            Pages(pagination).Select(b => b.Page).ShouldBe([5, 6, 7, 8, 9]);
            var current = Pages(pagination).Single(b => b.IsCurrent);
            current.Page.ShouldBe(7);
            current.Classes.Contains(":current").ShouldBeTrue();
            current.Bounds.Width.ShouldBe(36);
            current.Bounds.Height.ShouldBe(36);
            var mark = TestHelpers.Descendant<GlidingIndicator>(pagination);
            mark.CurrentRect.X.ShouldBe(current.Bounds.X + 0.5, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Window_Clamps_At_The_Ends_And_Arrows_Disable()
    {
        var pagination = new Pagination { PageCount = 12, CurrentPage = 1 };
        var window = TestHelpers.Show(pagination);
        try
        {
            Pages(pagination).Select(b => b.Page).ShouldBe([1, 2, 3, 4, 5]);
            Part(pagination, Pagination.PreviousPart).IsEnabled.ShouldBeFalse();
            pagination.CurrentPage = 12;
            Dispatcher.UIThread.RunJobs();
            Pages(pagination).Select(b => b.Page).ShouldBe([8, 9, 10, 11, 12]);
            Part(pagination, Pagination.NextPart).IsEnabled.ShouldBeFalse();
            pagination.CurrentPage = 99;
            Dispatcher.UIThread.RunJobs();
            pagination.EffectivePage.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Page_Buttons_Change_Page_And_Raise_Event()
    {
        var pagination = new Pagination { PageCount = 12, CurrentPage = 1 };
        var changes = new List<(int, int)>();
        pagination.PageChanged += (_, e) => changes.Add((e.OldPage, e.NewPage));
        var window = TestHelpers.Show(pagination);
        try
        {
            var next = Part(pagination, Pagination.NextPart);
            next.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            pagination.CurrentPage.ShouldBe(2);
            var four = Pages(pagination).Single(b => b.Page == 4);
            four.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            pagination.CurrentPage.ShouldBe(4);
            changes.ShouldBe([(1, 2), (2, 4)]);
            // Focus follows the number to its new slot.
            Pages(pagination).Single(b => b.Page == 4).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Few_Pages_Show_All_And_Zero_Shows_Nothing()
    {
        var pagination = new Pagination { PageCount = 3, CurrentPage = 2 };
        var window = TestHelpers.Show(pagination);
        try
        {
            Pages(pagination).Select(b => b.Page).ShouldBe([1, 2, 3]);
            pagination.PageCount = 0;
            Dispatcher.UIThread.RunJobs();
            Pages(pagination).ShouldBeEmpty();
            Part(pagination, Pagination.PreviousPart).IsVisible.ShouldBeFalse();
            pagination.EffectivePage.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Boundary_Pages_Show_First_And_Last_Behind_Ellipses()
    {
        var pagination = new Pagination { PageCount = 48, CurrentPage = 20, ShowBoundaryPages = true };
        var window = TestHelpers.Show(pagination);
        try
        {
            var first = Part(pagination, Pagination.FirstPart);
            var last = Part(pagination, Pagination.LastPart);
            first.IsVisible.ShouldBeTrue();
            first.Page.ShouldBe(1);
            last.IsVisible.ShouldBeTrue();
            last.Page.ShouldBe(48);
            pagination.GetVisualDescendants().OfType<Control>().First(c => c.Name == Pagination.EllipsisStartPart).IsVisible.ShouldBeTrue();
            pagination.CurrentPage = 2;
            Dispatcher.UIThread.RunJobs();
            first.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Lazy_Focus_Ring_Keeps_The_Template_Geometry()
    {
        var pagination = new Pagination { PageCount = 12, CurrentPage = 1 };
        var window = TestHelpers.Show(pagination);
        try
        {
            var button = Part(pagination, "PART_Page1");
            button.Focus(Avalonia.Input.NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(button).ShouldBeTrue();
            var ring = TestHelpers.Descendant<Border>(button, Primitives.LazyFocusRing.PartName);
            ring.Margin.ShouldBe(new Thickness(-4));
            ring.CornerRadius.ShouldBe(new CornerRadius(22));
            ring.BorderThickness.Left.ShouldBeGreaterThan(0);
            ring.Bounds.Width.ShouldBe(button.Bounds.Width + 8);
            // The last child of the template's panel: it draws over the frame.
            ((Panel)ring.GetVisualParent()!).Children[^1].ShouldBe(ring);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Boundary_Parts_Are_Built_On_Demand_In_Row_Order()
    {
        var pagination = new Pagination { PageCount = 48, CurrentPage = 20 };
        var window = TestHelpers.Show(pagination);
        try
        {
            var panel = TestHelpers.Descendant<PaginationPanel>(pagination);
            panel.Children.ShouldNotContain(c => c.Name == Pagination.FirstPart);
            TestHelpers.IsFocusRingVisible(pagination).ShouldBeFalse();
            pagination.GetVisualDescendants().OfType<Border>().ShouldNotContain(b => b.Name == Primitives.LazyFocusRing.PartName);
            pagination.ShowBoundaryPages = true;
            Dispatcher.UIThread.RunJobs();
            panel.Children.Select(c => c.Name).Where(n => n is not null).ToArray().ShouldBe(
            [
                Pagination.MarkPart, Pagination.PreviousPart, Pagination.FirstPart, Pagination.EllipsisStartPart,
                "PART_Page0", "PART_Page1", "PART_Page2", "PART_Page3", "PART_Page4",
                Pagination.EllipsisEndPart, Pagination.LastPart, Pagination.NextPart
            ]);
            Part(pagination, Pagination.FirstPart).Bounds.Width.ShouldBeGreaterThan(0);
            var ellipsis = pagination.GetVisualDescendants().OfType<Control>().First(c => c.Name == Pagination.EllipsisStartPart);
            ellipsis.Bounds.Width.ShouldBe(24);
            ellipsis.Bounds.Height.ShouldBe(Part(pagination, Pagination.FirstPart).Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Belt_Runs_Only_With_Full_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var pagination = new Pagination { PageCount = 12, CurrentPage = 1 };
        var window = TestHelpers.Show(pagination, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            pagination.CurrentPage = 5;
            Dispatcher.UIThread.RunJobs();
            var panel = TestHelpers.Descendant<PaginationPanel>(pagination);
            panel.GhostCount.ShouldBe(2);
            Pages(pagination)[0].BeltOffset.ShouldBe(80, 0.5);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            panel.GhostCount.ShouldBe(0);
            Pages(pagination)[0].BeltOffset.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}

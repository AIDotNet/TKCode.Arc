using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Navigation;

public class BreadcrumbTests
{
    private sealed class CountingCommand : ICommand
    {
        public int Count { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Count++;
    }

    private static BreadcrumbItem Crumb(Breadcrumb breadcrumb, int index) => (BreadcrumbItem)breadcrumb.ContainerFromIndex(index)!;

    [AvaloniaFact]
    public void Last_Crumb_Is_The_Current_Page_And_First_Has_No_Separator()
    {
        var breadcrumb = new Breadcrumb { ItemsSource = new[] { "Workspace", "Settings", "Billing" } };
        var window = TestHelpers.Show(breadcrumb);
        try
        {
            var first = Crumb(breadcrumb, 0);
            var last = Crumb(breadcrumb, 2);
            last.IsCurrent.ShouldBeTrue();
            last.Classes.Contains(":current").ShouldBeTrue();
            last.Focusable.ShouldBeFalse();
            first.IsCurrent.ShouldBeFalse();
            first.Focusable.ShouldBeTrue();
            first.Classes.Contains(":first").ShouldBeTrue();
            first.GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Separator").IsVisible.ShouldBeFalse();
            last.GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Separator").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Crumb_Raises_ItemClick_And_Runs_Its_Command()
    {
        var command = new CountingCommand();
        var breadcrumb = new Breadcrumb();
        breadcrumb.Items.Add(new BreadcrumbItem { Content = "Home", Command = command });
        breadcrumb.Items.Add(new BreadcrumbItem { Content = "Docs" });
        var clicks = new List<int>();
        breadcrumb.ItemClick += (_, e) => clicks.Add(e.Index);
        var window = TestHelpers.Show(breadcrumb);
        try
        {
            Crumb(breadcrumb, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe([0]);
            command.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_A_Crumb_Moves_The_Current_Page_Without_Shifting_The_Path()
    {
        var path = new ObservableCollection<string> { "Home", "Projects" };
        var breadcrumb = new Breadcrumb { ItemsSource = path };
        var window = TestHelpers.Show(breadcrumb);
        try
        {
            var projects = Crumb(breadcrumb, 1);
            var width = projects.Bounds.Width;
            path.Add("Arc");
            Dispatcher.UIThread.RunJobs();
            projects.IsCurrent.ShouldBeFalse();
            Crumb(breadcrumb, 2).IsCurrent.ShouldBeTrue();
            projects.Bounds.Width.ShouldBe(width, 0.01);
            path.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            projects.IsCurrent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MaxVisibleItems_Collapses_The_Middle_Into_An_Overflow_Menu()
    {
        var breadcrumb = new Breadcrumb
        {
            MaxVisibleItems = 4,
            ItemsSource = new[] { "Acme", "Engineering", "Platform", "Services", "Billing", "Invoices", "2026" }
        };
        var clicks = new List<(int, object?)>();
        breadcrumb.ItemClick += (_, e) => clicks.Add((e.Index, e.Item));
        var window = TestHelpers.Show(breadcrumb);
        try
        {
            Crumb(breadcrumb, 1).Classes.Contains(":overflow").ShouldBeTrue();
            Crumb(breadcrumb, 2).IsVisible.ShouldBeFalse();
            Crumb(breadcrumb, 3).IsVisible.ShouldBeFalse();
            Crumb(breadcrumb, 4).IsVisible.ShouldBeTrue();
            breadcrumb.OpenOverflow();
            Dispatcher.UIThread.RunJobs();
            breadcrumb.IsOverflowOpen.ShouldBeTrue();
            var entries = window.GetVisualDescendants().OfType<BreadcrumbMenuItem>().ToList();
            entries.Select(e => e.Content).ShouldBe(["Engineering", "Platform", "Services"]);
            entries[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe([(2, (object?)"Platform")]);
            breadcrumb.IsOverflowOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

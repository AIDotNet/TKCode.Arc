using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Selects;

/// <summary>#212 / #214: the menus of Select, Combobox, MultiSelect and TreeSelect grow past a narrow field.</summary>
public class SelectMenuWidthTests
{
    private const string Short = "三个字";
    private const string Long = "JetBrainsMono Nerd Font Mono and a very long tail";
    private static readonly string[] s_items = [Short, Long];

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Width = 900;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border Surface(Window window) =>
        TestHelpers.Descendant<SelectOptionView>(window).GetVisualAncestors().OfType<Border>().First(b => b.Name == "PART_Surface");

    private static SelectOptionView Options(Window window) => TestHelpers.Descendant<SelectOptionView>(window);

    // Each control behind the same four operations.
    public static TheoryData<string> Kinds() => new() { "Select", "Combobox", "MultiSelect" };

    private static (Control Control, Action<bool> SetOpen, Func<double> MenuMin, Action<double> SetMin, Action<double> SetMax,
        Action<bool> SetTip) Create(string kind)
    {
        switch (kind)
        {
            case "Select":
            {
                var c = new Select { Width = 92, ItemsSource = s_items };
                return (c, o => c.IsOpen = o, () => c.MenuMinWidth, v => c.MenuMinWidth = v, v => c.MenuMaxWidth = v, v => c.ShowTrimmedOptionToolTip = v);
            }
            case "Combobox":
            {
                var c = new Combobox { Width = 92, ItemsSource = s_items };
                return (c, o => c.IsOpen = o, () => c.MenuMinWidth, v => c.MenuMinWidth = v, v => c.MenuMaxWidth = v, v => c.ShowTrimmedOptionToolTip = v);
            }
            default:
            {
                var c = new MultiSelect { Width = 92, ItemsSource = s_items };
                return (c, o => c.IsOpen = o, () => c.MenuMinWidth, v => c.MenuMinWidth = v, v => c.MenuMaxWidth = v, v => c.ShowTrimmedOptionToolTip = v);
            }
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void Menu_Grows_Past_A_Narrow_Field_And_Keeps_Options_Untrimmed(string kind)
    {
        var (control, setOpen, _, _, _, _) = Create(kind);
        var window = Show(control);
        try
        {
            setOpen(true);
            Dispatcher.UIThread.RunJobs();
            var surface = Surface(window);
            var options = Options(window);
            // The field keeps its own size (its theme minimum, not the menu's).
            surface.Width.ShouldBeGreaterThan(control.Bounds.Width);
            control.Bounds.Width.ShouldBeLessThan(260);
            options.TrimmedTextAt(0).ShouldBeNull();
            options.TrimmedTextAt(1).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void MenuMaxWidth_Caps_The_Menu_And_Trimmed_Rows_Get_A_ToolTip(string kind)
    {
        var (control, setOpen, _, _, setMax, _) = Create(kind);
        setMax(150);
        var window = Show(control);
        try
        {
            setOpen(true);
            Dispatcher.UIThread.RunJobs();
            Surface(window).Width.ShouldBe(150);
            var options = Options(window);
            options.TrimmedTextAt(0).ShouldBeNull();
            options.TrimmedTextAt(1).ShouldBe(Long);
            options.UpdateToolTip(1);
            ToolTip.GetTip(options).ShouldBe(Long);
            options.UpdateToolTip(0);
            ToolTip.GetTip(options).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void ShowTrimmedOptionToolTip_False_Disables_The_ToolTip(string kind)
    {
        var (control, setOpen, _, _, setMax, setTip) = Create(kind);
        setMax(150);
        setTip(false);
        var window = Show(control);
        try
        {
            setOpen(true);
            Dispatcher.UIThread.RunJobs();
            var options = Options(window);
            options.TrimmedTextAt(1).ShouldBe(Long);
            options.UpdateToolTip(1);
            ToolTip.GetTip(options).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Kinds))]
    public void MenuMinWidth_Is_Public_And_Widens_The_Menu(string kind)
    {
        var (_, _, getMin, _, _, _) = Create(kind);
        getMin().ShouldBe(0);
        var again = Create(kind);
        again.SetMin(820);
        var window = Show(again.Control);
        try
        {
            again.SetOpen(true);
            Dispatcher.UIThread.RunJobs();
            Surface(window).Width.ShouldBe(820);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Menu_Never_Exceeds_The_Window()
    {
        var select = new Select { Width = 92, ItemsSource = new[] { new string('W', 400) } };
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Surface(window).Width.ShouldBeLessThanOrEqualTo(window.ClientSize.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Menu_Follows_Item_Changes_While_Open()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<string> { Short };
        var select = new Select { Width = 92, ItemsSource = items };
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var before = Surface(window).Width;
            items.Add(Long);
            Dispatcher.UIThread.RunJobs();
            Surface(window).Width.ShouldBeGreaterThan(before);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed record Node(string Name, IReadOnlyList<Node>? Children = null);

    private static TreeSelect Tree() => new()
    {
        Width = 120,
        ItemsSource = new[] { new Node(Long + Long, [new Node("child")]) },
        ChildrenSelector = n => ((Node)n).Children,
        TextSelector = n => ((Node)n).Name,
        ValueSelector = n => ((Node)n).Name,
    };

    [AvaloniaFact]
    public void TreeSelect_Menu_Grows_And_MenuMaxWidth_Caps_It()
    {
        var tree = Tree();
        var window = Show(tree);
        try
        {
            tree.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = TestHelpers.Descendant<TreeSelectRowsPanel>(window).GetVisualAncestors().OfType<Border>().First(b => b.Name == "PART_Surface");
            surface.Width.ShouldBeGreaterThan(300);
            tree.MenuMaxWidth = 260;
            Dispatcher.UIThread.RunJobs();
            surface.Width.ShouldBe(260);
            tree.MenuMinWidth.ShouldBe(0);
            tree.ShowTrimmedOptionToolTip.ShouldBeTrue();
            var row = TestHelpers.Descendant<TreeSelectRow>(window);
            row.TrimmedText.ShouldBe(Long + Long);
        }
        finally
        {
            window.Close();
        }
    }
}

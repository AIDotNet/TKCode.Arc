using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileNavigationStackTabsWithStacksDemo : UserControl
{
    private static readonly string[] s_titles = ["Home", "Search", "Profile"];

    // One stack per tab: switching tabs swaps the stack, so each tab keeps the pages it has pushed.
    private readonly Mobile.NavigationStack[] _stacks;

    public MobileNavigationStackTabsWithStacksDemo()
    {
        InitializeComponent();
        _stacks = s_titles.Select(CreateStack).ToArray();
        Shell.Content = _stacks[0];
    }

    private static Mobile.NavigationStack CreateStack(string title)
    {
        var stack = new Mobile.NavigationStack();
        var open = new Mobile.Button { Content = "Open a page", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        open.Click += (_, _) => stack.Push(CreateDetail(stack, title, stack.Depth));
        stack.Root = new Mobile.Page
        {
            Header = new Mobile.Navbar { Title = title },
            Content = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 12, Children = { open } },
        };
        return stack;
    }

    private static Mobile.Page CreateDetail(Mobile.NavigationStack stack, string tab, int depth)
    {
        var next = new Mobile.Button { Content = "Go deeper", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        next.Click += (_, _) => stack.Push(CreateDetail(stack, tab, stack.Depth));
        return new Mobile.Page
        {
            Header = new Mobile.Navbar { Title = $"{tab} {depth}", ShowBack = true, BackText = depth == 1 ? tab : "Back", BackCommand = stack.BackCommand },
            Content = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Spacing = 12, Children = { next } },
        };
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The first selection fires while the XAML loads, before the stacks exist.
        if (sender is Mobile.Tabbar { SelectedIndex: >= 0 and var index } && _stacks is not null && index < _stacks.Length)
        {
            Shell.Content = _stacks[index];
        }
    }
}

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.AppShell;

/// <summary>Docs sidebar: the pages map onto the sidebar, the current page opens its section and parent, search, versions and adjacent pages.</summary>
public class DocsSidebarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Builds_The_Sample_Site_And_Marks_The_Current_Page()
    {
        var docs = new DocsSidebar { Height = 600 };
        var window = TestHelpers.Show(docs);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(docs, DocsSidebar.SidebarPart);
            sidebar.Sections.Select(s => s.Header).ShouldBe(["Getting started", "Guides", "API reference", "Help"]);
            sidebar.SelectedKey.ShouldBe("guides/caching");
            docs.CurrentPage!.Title.ShouldBe("Caching");
            sidebar.Sections[1].Items.Single(i => i.Key == "guides/caching").Badge.ShouldBe("New");
            sidebar.Sections[1].Items[0].Items.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Current_Key_Opens_Its_Section_And_Parent_And_Follows_Clicks()
    {
        var docs = new DocsSidebar { Height = 400 };
        docs.Sections.Add(new DocsSidebarSection("Basics", new DocsSidebarPage("a", "Alpha"), new DocsSidebarPage("b", "Beta")));
        var parent = new DocsSidebarPage("cli", "Command line");
        parent.Pages.Add(new DocsSidebarPage("cli/build", "build"));
        var closed = new DocsSidebarSection("Reference", parent) { IsExpanded = false };
        docs.Sections.Add(closed);
        var window = TestHelpers.Show(docs);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(docs, DocsSidebar.SidebarPart);
            sidebar.Sections.Count.ShouldBe(2);
            sidebar.Sections[1].IsExpanded.ShouldBeFalse();

            docs.CurrentKey = "cli/build";
            Settle();
            closed.IsExpanded.ShouldBeTrue();
            sidebar.Sections[1].IsExpanded.ShouldBeTrue();
            sidebar.Sections[1].Items[0].IsExpanded.ShouldBeTrue();
            sidebar.SelectedKey.ShouldBe("cli/build");

            DocsSidebarPage? invoked = null;
            docs.PageInvoked += (_, e) => invoked = e.Page;
            sidebar.SelectedKey = "b";
            Settle();
            docs.CurrentKey.ShouldBe("b");

            // Reading order skips folders: Alpha, Beta, build.
            docs.GetAdjacentPage(1)!.Key.ShouldBe("cli/build");
            docs.GetAdjacentPage(-1)!.Key.ShouldBe("a");
            docs.CurrentKey = "a";
            docs.GetAdjacentPage(-1).ShouldBeNull();
            invoked.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Text_Flows_Both_Ways_And_Shortcut_Focuses_The_Box()
    {
        var docs = new DocsSidebar { Height = 600 };
        var window = TestHelpers.Show(docs);
        try
        {
            Settle();
            var sidebar = TestHelpers.Descendant<Sidebar>(docs, DocsSidebar.SidebarPart);
            docs.SearchText = "rout";
            sidebar.FilterText.ShouldBe("rout");
            sidebar.FilterText = "deploy";
            docs.SearchText.ShouldBe("deploy");

            window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
            Settle();
            var focused = TopLevel.GetTopLevel(docs)!.FocusManager!.GetFocusedElement();
            focused.ShouldBeAssignableTo<TextBox>();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Version_Switcher_Lists_The_Versions_And_Reports_A_Change()
    {
        var docs = new DocsSidebar();
        var window = TestHelpers.Show(docs);
        try
        {
            Settle();
            docs.EffectiveVersion!.Name.ShouldBe("v3.2");
            var menu = TestHelpers.Descendant<DropdownMenu>(docs, DocsSidebar.VersionMenuPart);
            menu.Items.OfType<MenuEntry>().Select(e => e.Header).ShouldBe(["v3.2 (latest)", "v3.1", "v2.9 (legacy)"]);
            menu.Items.OfType<MenuEntry>().First().IsChecked.ShouldBeTrue();

            DocsSidebarVersionChangedEventArgs? changed = null;
            docs.VersionChanged += (_, e) => changed = e;
            var entry = menu.Items.OfType<MenuEntry>().Last();
            menu.RaiseEvent(new MenuEntryClickedEventArgs(DropdownMenu.ItemClickedEvent, entry, entry));
            changed!.OldVersion.ShouldBe("v3.2");
            changed.NewVersion.ShouldBe("v2.9");
            docs.SelectedVersion.ShouldBe("v2.9");
            entry.IsChecked.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

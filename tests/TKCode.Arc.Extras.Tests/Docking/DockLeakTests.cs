using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Docking;

/// <summary>
/// Leaks (spec 15): a closed layout, closed panes, removed source items and a replaced workspace must be collectable even
/// when the data (a view-model's collection, the old workspace) lives on.
/// </summary>
public class DockLeakTests
{
    private static void ShouldBeCollected(params WeakReference[] references)
    {
        // The motion scheduler lets go of a stopped driver on its next frame; Collect ticks one each round.
        TestHelpers.Collect(references);
        references.Where(r => r.IsAlive).Select(r => r.Target?.GetType().Name).ShouldBeEmpty();
    }

    private static DockLayout SpecLayout() => DockLayoutViewTests.LoadSpec();

    /// <summary>Shows <paramref name="create"/>'s layout, works it, closes the window; weak references to the layout and its views.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] Over(Func<DockLayout> create, Action<DockLayout, Window> use)
    {
        var layout = create();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        use(layout, window);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var views = layout.Workspace.Groups().Select(g => new WeakReference(g.View)).ToList();
        window.Content = null;
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return [new WeakReference(layout), .. views];
    }

    [AvaloniaFact]
    public void A_Closed_Layout_Is_Collected_After_Auto_Hide_Switcher_Maximize_And_Menus()
    {
        var refs = Over(SpecLayout, (layout, window) =>
        {
            var explorer = layout.Workspace.FindPane("explorer")!;
            layout.AutoHide(explorer);
            layout.Activate(explorer);
            layout.ShowPaneSwitcherCommand.Execute(null);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            var readme = layout.Workspace.FindPane("doc:readme")!;
            layout.ToggleMaximize(readme.Group!);
            layout.ShowPaneMenu(readme, DockLayoutViewTests.TabOf(layout, "doc:readme"), null);
            layout.Menu!.Close();
            // Leave the flyout open while the window closes.
            layout.Activate(explorer);
        });
        ShouldBeCollected(refs);
    }

    [AvaloniaFact]
    public void A_Long_Lived_Source_Does_Not_Keep_A_Closed_Layout()
    {
        var files = new ObservableCollection<string> { "a.md", "b.md" };
        var refs = Over(() => new DockLayout { Root = new DockDocumentArea(), DocumentsSource = files }, (layout, _) =>
        {
            files.Add("c.md");
            layout.Close(layout.Workspace.FindPane("a.md")!);
        });
        ShouldBeCollected(refs);
        files.ShouldBe(["b.md", "c.md"]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] PanesOf(DockLayout layout, params string[] ids)
        => ids.Select(id => new WeakReference(layout.Workspace.FindPane(id)!)).ToArray();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] RemoveAndClose(DockLayout layout, ObservableCollection<string> files)
    {
        var refs = PanesOf(layout, "a.md", "b.md");
        layout.Activate(layout.Workspace.FindPane("a.md")!);
        files.Remove("a.md");
        layout.Close(layout.Workspace.FindPane("b.md")!);
        Dispatcher.UIThread.RunJobs();
        return refs;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] DropSource(DockLayout layout)
    {
        var refs = PanesOf(layout, "c.md");
        layout.DocumentsSource = null;
        Dispatcher.UIThread.RunJobs();
        return refs;
    }

    [AvaloniaFact]
    public void Panes_Of_Removed_Items_And_Of_A_Dropped_Source_Are_Collected()
    {
        var files = new ObservableCollection<string> { "a.md", "b.md", "c.md" };
        var layout = new DockLayout { Root = new DockDocumentArea(), DocumentsSource = files };
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            ShouldBeCollected(RemoveAndClose(layout, files));
            ShouldBeCollected(DropSource(layout));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Replaced_Workspace_Lets_Go_Of_The_Layout()
    {
        var old = new DockSample().Workspace;
        var refs = Over(() => new DockLayout { Workspace = old }, (layout, _) =>
        {
            layout.AutoHide(old.FindPane("explorer")!);
            layout.Workspace = new DockWorkspace
            {
                Root = new DockSplit(Orientation.Horizontal,
                    new DockGroup(DockSample.Tool("tool", "Tool", DockSide.Left)),
                    new DockDocumentArea { Root = new DockGroup(DockSample.Document("doc", "Doc")) })
            };
        });
        // The old workspace (kept by this test) holds nothing of the layout or its views.
        ShouldBeCollected(refs);
        GC.KeepAlive(old);
    }

    [AvaloniaFact]
    public void A_Workspace_Kept_By_The_Host_Does_Not_Keep_A_Closed_Layout()
    {
        var sample = new DockSample();
        var refs = Over(() => new DockLayout { Workspace = sample.Workspace }, (layout, _) =>
        {
            layout.AutoHide(sample.Terminal);
            layout.Activate(sample.Terminal);
            layout.Close(sample.Search);
        });
        // A closed layout can stay listening to a workspace the host keeps; it must not be kept by it.
        ShouldBeCollected(refs);
        GC.KeepAlive(sample);
    }
}

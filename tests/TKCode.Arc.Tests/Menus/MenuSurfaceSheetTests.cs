using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Menus;

/// <summary>The bottom-sheet variant of <see cref="MenuSurface"/>: its scrim and its window subscription end with the sheet.</summary>
public class MenuSurfaceSheetTests
{
    [AvaloniaFact]
    public void A_Sheet_Closed_By_Its_Popup_Takes_Its_Scrim_Away()
    {
        var anchor = new Button { Content = "Account" };
        var surface = new MenuSurface { Variant = MenuSurfaceVariant.Sheet };
        surface.Items.Add(new MenuEntry { Header = "Profile" });
        var window = TestHelpers.Show(new StackPanel { Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Dispatcher.UIThread.RunJobs();
            var scrim = surface.Sheet.ShouldNotBeNull().Scrim.ShouldNotBeNull();
            scrim.GetVisualParent().ShouldNotBeNull();
            // Light dismiss, the window losing focus or the anchor leaving: the popup closes on its own.
            surface.Popup.ShouldNotBeNull().IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeFalse();
            scrim.GetVisualParent().ShouldBeNull("the scrim would keep covering the window and swallow every click");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Closed_Sheet_Does_Not_Keep_Its_Menu_In_The_Window()
    {
        var host = new StackPanel();
        var window = TestHelpers.Show(host);
        try
        {
            var reference = SheetOpenedAndClosed(host);
            // The window stays open: Collect commits a frame each round, which lets its compositor drop what was removed.
            TestHelpers.Collect(reference);
            reference.IsAlive.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference SheetOpenedAndClosed(Panel host)
    {
        var anchor = new Button { Content = "Account" };
        host.Children.Add(anchor);
        var surface = new MenuSurface { Variant = MenuSurfaceVariant.Sheet };
        surface.Items.Add(new MenuEntry { Header = "Profile" });
        surface.Open(anchor);
        Dispatcher.UIThread.RunJobs();
        surface.IsOpen = false;
        Dispatcher.UIThread.RunJobs();
        host.Children.Remove(anchor);
        Dispatcher.UIThread.RunJobs();
        return new WeakReference(surface);
    }
}

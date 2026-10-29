using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Menus;

public class MenuSurfacePointerTests
{
    private static async Task PumpUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Point Center(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [AvaloniaFact]
    public async Task Pointer_Highlights_Rows_Releases_After_Grace_And_Clicks_Activate()
    {
        var anchor = new Button { Content = "Actions" };
        var surface = new MenuSurface();
        var rename = new MenuEntry { Header = "Rename" };
        var duplicate = new MenuEntry { Header = "Duplicate" };
        var archive = new MenuEntry { Header = "Archive", IsEnabled = false };
        surface.Items.Add(rename);
        surface.Items.Add(duplicate);
        surface.Items.Add(archive);
        object? clicked = null;
        surface.ItemClicked += (_, e) => clicked = e.Item;
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { anchor } });
        try
        {
            surface.Open(anchor);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(Center(rename, window));
            Dispatcher.UIThread.RunJobs();
            surface.HighlightedEntry.ShouldBe(rename);
            window.MouseMove(Center(duplicate, window));
            Dispatcher.UIThread.RunJobs();
            surface.HighlightedEntry.ShouldBe(duplicate);
            duplicate.IsFocused.ShouldBeTrue();

            // Disabled rows never take the highlight; it is released after the grace period.
            window.MouseMove(Center(archive, window));
            await PumpUntil(() => surface.HighlightedEntry is null);
            surface.HighlightedEntry.ShouldBeNull();

            window.MouseMove(Center(rename, window));
            window.MouseDown(Center(rename, window), MouseButton.Left);
            window.MouseUp(Center(rename, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            clicked.ShouldBe(rename);
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Surface_Works_As_A_Static_Menu()
    {
        var surface = new MenuSurface();
        surface.Items.Add(new MenuEntry { Header = "Cut" });
        surface.Items.Add(new MenuEntry { Header = "Copy" });
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { surface } });
        try
        {
            surface.IsOpen = true;
            surface.IsOpen.ShouldBeFalse();
            surface.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            (surface.HighlightedEntry?.Header as string).ShouldBe("Cut");
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
            (surface.HighlightedEntry?.Header as string).ShouldBe("Copy");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Sheet_Follows_A_Drag_And_Closes_Past_80px()
    {
        var anchor = new Button { Content = "Account" };
        var surface = new MenuSurface { Variant = MenuSurfaceVariant.Sheet, Header = new TextBlock { Text = "Ada Lovelace", Height = 40 } };
        surface.Items.Add(new MenuEntry { Header = "Profile" });
        // A plain window: its layer manager (Arc's base Window theme) hosts both the scrim (overlay layer) and the sheet
        // (popup overlay layer, above the scrim), as in an app.
        var window = new Window { Content = new StackPanel { Children = { anchor } }, Width = 400, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Full);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            surface.Open(anchor);
            var header = (Control)surface.Header!;
            // Let the sheet finish rising: its position is stable once the spring has settled.
            var last = new Point(-1, -1);
            var stable = 0;
            await PumpUntil(() =>
            {
                var now = TopLevel.GetTopLevel(header) is not null ? Center(header, window) : default;
                stable = now == last ? stable + 1 : 0;
                last = now;
                return stable >= 10;
            }, 4000);
            var start = Center(header, window);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(0, 40));
            Dispatcher.UIThread.RunJobs();
            surface.Sheet!.DragOffset.ShouldBe(36, 0.5);
            window.MouseMove(start + new Point(0, 120));
            window.MouseUp(start + new Point(0, 120), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

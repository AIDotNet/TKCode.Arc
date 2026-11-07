using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// Controls that shape themselves by "the window" treat an <see cref="ArcViewportHost"/> as their window (M4 of
/// design/engineering/gallery-mobile-preview.md): a phone-sized host in a wide window gives the phone's behavior.
/// </summary>
public class ArcViewportControlsTests
{
    private static UserMenu CreateMenu()
    {
        var menu = new UserMenu { DisplayName = "Ada Lovelace", Email = "ada@analytical.dev", Plan = "Pro", IsNameVisible = true };
        menu.Items.Add(new MenuEntry { Header = "Profile", Icon = ArcIcons.User });
        menu.Items.Add(new MenuEntry { Header = "Settings", Icon = ArcIcons.Settings });
        return menu;
    }

    /// <summary>A wide window with a 390 × 700 host on its left (not at the window's origin) and the control inside.</summary>
    private static (Window Window, ArcViewportHost Host) InPhone(Control control, double width = 390)
    {
        var host = new ArcViewportHost
        {
            Width = width,
            Height = 700,
            Margin = new Thickness(200, 60, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { new StackPanel { Margin = new Thickness(16), Children = { control } } }
        };
        var window = TestHelpers.Show(host);
        window.Width = 1200;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (window, host);
    }

    [AvaloniaFact]
    public void A_User_Menu_On_A_Phone_In_A_Wide_Window_Opens_As_A_Sheet_Across_The_Phone()
    {
        var menu = CreateMenu();
        var (window, host) = InPhone(menu);
        try
        {
            menu.Classes.Contains(UserMenu.CompactPseudoClass).ShouldBeTrue();
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.Menu.Variant.ShouldBe(MenuSurfaceVariant.Sheet);
            menu.Menu.Width.ShouldBe(390);

            // The sheet sits along the phone's bottom edge, not the window's, and its scrim covers only the phone.
            var phone = ArcViewport.GetBounds(menu).ShouldNotBeNull();
            var sheet = new Rect(menu.Menu.Bounds.Size).TransformToAABB(menu.Menu.TransformToVisual(window)!.Value);
            sheet.Left.ShouldBe(phone.Left, 1);
            sheet.Bottom.ShouldBe(phone.Bottom, 1);
            var scrim = menu.Menu.Sheet.ShouldNotBeNull().Scrim.ShouldNotBeNull();
            scrim.GetVisualParent().ShouldBeSameAs(host.Layer);
            scrim.Width.ShouldBe(390);

            menu.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Follows_The_Host_When_It_Grows()
    {
        var menu = CreateMenu();
        var (window, host) = InPhone(menu);
        try
        {
            menu.Classes.Contains(UserMenu.CompactPseudoClass).ShouldBeTrue();
            host.Width = 800;
            Dispatcher.UIThread.RunJobs();
            menu.Classes.Contains(UserMenu.CompactPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Metric_Card_On_A_Phone_Is_Narrow_And_In_A_Wide_Window_Is_Not()
    {
        // MetricCard turns narrow at 380 and below (a small phone).
        var phoneCard = new MetricCard { Label = "Revenue", Value = 42 };
        var (window, _) = InPhone(phoneCard, 360);
        try
        {
            phoneCard.Classes.Contains(MetricCard.NarrowPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }

        var wideCard = new MetricCard { Label = "Revenue", Value = 42 };
        var wide = TestHelpers.Show(wideCard);
        try
        {
            wide.Width = 1200;
            Dispatcher.UIThread.RunJobs();
            wideCard.Classes.Contains(MetricCard.NarrowPseudoClass).ShouldBeFalse();
        }
        finally
        {
            wide.Close();
        }
    }

    [AvaloniaFact]
    public void A_Stack_Placed_In_A_Host_Serves_The_Host_Only()
    {
        var owner = new Border();
        var placed = new ToastStack();
        var (window, host) = InPhone(new StackPanel { Children = { owner, placed } });
        try
        {
            ToastHost.GetStack(owner).ShouldBeSameAs(placed);
            ToastHost.Find(window).ShouldBeNull();
            ((Panel)placed.GetVisualParent()!).Children.Remove(placed);
            Dispatcher.UIThread.RunJobs();
            ToastHost.GetStack(owner).ShouldNotBeSameAs(placed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toasts_From_Inside_A_Host_Hang_From_The_Host()
    {
        var owner = new Border();
        var (window, host) = InPhone(owner);
        try
        {
            var stack = ToastHost.GetStack(owner);
            stack.GetVisualParent().ShouldBeSameAs(host.Layer);
            ToastHost.GetStack(owner).ShouldBeSameAs(stack);
            Dispatcher.UIThread.RunJobs();
            stack.Width.ShouldBe(390);
            stack.Height.ShouldBe(700);
            // Outside the host, the window keeps its own stack; the host's stack does not serve the window.
            var windowStack = ToastHost.GetStack(window);
            windowStack.ShouldNotBeSameAs(stack);
            ArcViewport.FindHost(windowStack).ShouldBeNull();
            ToastHost.Find(window).ShouldBeSameAs(windowStack);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Zoomed_Viewport_Gives_A_Picker_The_Same_Room_As_An_Unzoomed_One()
    {
        // Placement compares window distances (scaled by the zoom) with panel sizes (unscaled): both must be in the same
        // units, or a zoomed phone preview trims the panel's shadow too early.
        static (ColorPicker Picker, Control Root) Phone(double zoom)
        {
            var picker = new ColorPicker { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(16, 300, 0, 0) };
            var host = new ArcViewportHost { Width = 390, Height = 844, Children = { picker } };
            var scaled = new LayoutTransformControl
            {
                LayoutTransform = new Avalonia.Media.ScaleTransform(zoom, zoom),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = host
            };
            ArcZoom.SetScale(scaled, zoom);
            return (picker, scaled);
        }

        var rooms = new List<Thickness>();
        foreach (var zoom in new[] { 1.0, 680.0 / 844.0 })
        {
            var (picker, root) = Phone(zoom);
            var window = TestHelpers.Show(root);
            window.Width = 1200;
            window.Height = 1000;
            Dispatcher.UIThread.RunJobs();
            try
            {
                picker.SetCurrentValue(ColorPicker.IsDropDownOpenProperty, true);
                Dispatcher.UIThread.RunJobs();
                rooms.Add(picker.Surface.ShouldNotBeNull().ShadowRoom);
            }
            finally
            {
                window.Close();
            }
        }
        rooms[1].Left.ShouldBe(rooms[0].Left, 1);
        rooms[1].Top.ShouldBe(rooms[0].Top, 1);
        rooms[1].Right.ShouldBe(rooms[0].Right, 1);
        rooms[1].Bottom.ShouldBe(rooms[0].Bottom, 1);
    }

    [AvaloniaFact]
    public void An_Agent_Status_Opened_In_A_Host_Floats_On_The_Host()
    {
        var status = new AgentStatus
        {
            Status = AgentStatusRunState.Working,
            Agent = new AgentModel("Atlas Scout 2"),
            Activity = new AgentStatusActivity("Reading the logs", ArcIcons.Search)
        };
        var (window, host) = InPhone(status);
        try
        {
            status.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            status.IsFloating.ShouldBeTrue();
            // Its viewport's layer, so the open panel stays on the phone's screen.
            status.Surface.GetVisualAncestors().ShouldContain(host.Layer);
            status.IsExpanded = false;
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}

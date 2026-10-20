using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Menus;

public class MenuSurfaceMotionTests
{
    private static async Task PumpUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static MenuSurface CreateSurface(MenuSurfaceVariant variant = MenuSurfaceVariant.Dropdown)
    {
        var surface = new MenuSurface { Variant = variant };
        surface.Items.Add(new MenuEntry { Header = "Rename", Icon = ArcIcons.Pencil });
        surface.Items.Add(new MenuEntry { Header = "Duplicate", Icon = ArcIcons.Copy });
        surface.Items.Add(new MenuEntry { Header = "Delete", Icon = ArcIcons.Trash2, IsDestructive = true, HasSeparatorBefore = true });
        return surface;
    }

    [AvaloniaFact]
    public async Task Dropdown_Grows_From_The_Trigger_Edge_On_The_Css_Spring_And_Rows_Stagger()
    {
        var anchor = new Button { Content = "Actions" };
        var surface = CreateSurface();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { anchor } }, ArcMotionLevel.Full);
        try
        {
            surface.Open(anchor);
            Dispatcher.UIThread.RunJobs();
            var popup = surface.Popup!;
            var enter = popup.OpenMotion.ShouldBeOfType<MenuSurfaceEnterMotion>();
            enter.Scale.ShouldBe(0.97);
            enter.TransformDuration.ShouldBe(TimeSpan.FromMilliseconds(580));
            enter.TransformEasing.ShouldBe(ArcEasings.CssSpring);
            enter.OpacityDuration.ShouldBe(TimeSpan.FromMilliseconds(160));
            popup.LastOrigin.Point.ShouldBe(new Point(1, 0));
            enter.Offset.ShouldBe(new Vector(0, -5));
            var exit = popup.CloseMotion.ShouldBeOfType<MenuSurfaceExitMotion>();
            exit.ExitDuration.ShouldBe(TimeSpan.FromMilliseconds(130));
            exit.Scale.ShouldBe(0.985);
            exit.Offset.ShouldBe(new Vector(0, -2.5));

            // Rows start transparent and fade in by index.
            var rows = surface.GetRealizedContainers().OfType<MenuEntry>().ToList();
            rows[2].Opacity.ShouldBeLessThan(1);
            await PumpUntil(() => rows.All(r => r.Opacity >= 1));
            rows.All(r => r.Opacity >= 1).ShouldBeTrue();

            surface.Close();
            await PumpUntil(() => !popup.IsOpen);
            popup.IsOpen.ShouldBeFalse();
            surface.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Only_Fades()
    {
        var anchor = new Button { Content = "Actions" };
        var surface = CreateSurface(MenuSurfaceVariant.Context);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { anchor } }, ArcMotionLevel.Reduced);
        try
        {
            surface.OpenAt(anchor, new Point(10, 10));
            Dispatcher.UIThread.RunJobs();
            var enter = surface.Popup!.OpenMotion.ShouldBeOfType<MenuSurfaceEnterMotion>();
            enter.FadeOnly.ShouldBeTrue();
            enter.OpacityDuration.ShouldBe(TimeSpan.FromMilliseconds(120));
            surface.GetRealizedContainers().OfType<MenuEntry>().All(r => r.Opacity >= 1).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Context_Menu_Grows_From_The_Pointer_On_A_Snappy_Spring()
    {
        var anchor = new Border { Width = 200, Height = 100, Background = Avalonia.Media.Brushes.Transparent };
        var surface = CreateSurface(MenuSurfaceVariant.Context);
        var window = TestHelpers.Show(new StackPanel { Children = { anchor } }, ArcMotionLevel.Full);
        try
        {
            surface.OpenAt(anchor, new Point(20, 20));
            Dispatcher.UIThread.RunJobs();
            var enter = surface.Popup!.OpenMotion.ShouldBeOfType<MenuSurfaceEnterMotion>();
            enter.Scale.ShouldBe(0.96);
            enter.TransformEasing.ShouldBe(ArcSpringEasing.For(ArcSpring.Snappy));
            enter.Offset.ShouldBe(default);
            surface.Popup!.LastOrigin.Point.ShouldBe(new Point(0, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Highlight_Follows_The_Keyboard_Position()
    {
        var anchor = new Button { Content = "Actions" };
        var surface = CreateSurface();
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { anchor } }, ArcMotionLevel.Full);
        try
        {
            surface.Open(anchor, MenuOpenFocus.First);
            await PumpUntil(() => surface.HighlightedEntry is not null);
            var first = surface.HighlightedEntry!;
            var highlight = surface.Highlight!;
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.Height.ShouldBe(36);
            first.Classes.Contains(MenuEntry.HighlightedPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

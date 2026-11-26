using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="ModalPage"/>: sheet and full-screen presentations, inherited safe area, soft-keyboard
/// avoidance, Escape and Android system back handling.
/// </summary>
public class ModalPageTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Sheet_And_FullScreen_Presentations_Respect_Inherited_Safe_Area_And_Keyboard()
    {
        var source = new KeyboardInsetSource();
        var body = new Border { Name = "ModalBody" };
        var modal = new ModalPage
        {
            Header = new Navbar { Title = "Compose" },
            Content = body,
            Presentation = ModalPagePresentation.Sheet,
        };
        var page = new Page
        {
            UsePlatformSafeArea = false,
            SafeArea = new Thickness(0, 44, 0, 34),
            Content = new Border(),
            Overlay = modal,
        };
        var window = new Window { Content = page, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        Page.SetKeyboardInsetSource(window, source);
        window.Show();
        try
        {
            Settle(window);
            modal.IsHitTestVisible.ShouldBeFalse();

            modal.IsOpen = true;
            Settle(window);
            modal.IsHitTestVisible.ShouldBeTrue();
            modal.SurfaceOffset.ShouldBe(0, 0.5);

            var surface = modal.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Surface");
            var safe = modal.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Safe");
            // Sheet presentation offsets from the top safe area + SheetTopGap (16) and pads bottom with safe area (34).
            surface.Margin.Top.ShouldBe(44 + 16, 0.5);
            safe.Padding.Bottom.ShouldBe(34, 0.5);

            // Soft keyboard lifts the modal bottom inset to the keyboard height.
            source.Set(280);
            Settle(window);
            safe.Padding.Bottom.ShouldBe(280, 0.5);

            source.Set(0);
            Settle(window);
            safe.Padding.Bottom.ShouldBe(34, 0.5);

            // FullScreen presentation covers edge to edge and pads the top safe area inside.
            modal.Presentation = ModalPagePresentation.FullScreen;
            Settle(window);
            surface.Margin.Top.ShouldBe(0, 0.5);
            safe.Padding.Top.ShouldBe(44, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void System_Back_Closes_Dismissible_ModalPage_And_Blocks_When_Non_Dismissible()
    {
        var modal = new ModalPage { Content = new TextBlock { Text = "Form" }, IsDismissible = true };
        var detail = new Page { Content = new TextBlock { Text = "Detail" }, Overlay = modal };
        var root = new Page { Content = new TextBlock { Text = "Root" } };
        var stack = new NavigationStack { Root = root };
        var window = new Window { Content = stack, Width = 390, Height = 844 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        try
        {
            Settle(window);
            stack.Push(detail);
            Settle(window);

            modal.IsOpen = true;
            Settle(window);

            var firstBack = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(firstBack);
            Settle(window);
            firstBack.Handled.ShouldBeTrue();
            modal.IsOpen.ShouldBeFalse();
            stack.Depth.ShouldBe(2);

            // Non-dismissible modal absorbs back without closing itself or popping the underlying stack.
            modal.IsDismissible = false;
            modal.IsOpen = true;
            Settle(window);

            var secondBack = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(secondBack);
            Settle(window);
            secondBack.Handled.ShouldBeTrue();
            modal.IsOpen.ShouldBeTrue();
            stack.Depth.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}

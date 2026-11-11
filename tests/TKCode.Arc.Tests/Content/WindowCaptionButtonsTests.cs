using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Content;

/// <summary>
/// WindowCaptionButtons (issue #107): minimize, maximize / restore and close for a window whose content reaches the top.
/// They drive the window they are in, follow its state and what it allows, carry the system roles (Snap Layouts on
/// Windows) while they drive it, and do nothing as a preview. TitleBar draws its own buttons with the same control.
/// </summary>
public class WindowCaptionButtonsTests
{
    private static Window Open(Control content, Window? window = null)
    {
        window ??= new Window();
        window.Content = content;
        window.Width = 800;
        window.Height = 500;
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static AvaloniaButton Button(Visual root, string part) => Part<AvaloniaButton>(root, part);

    private static void Press(AvaloniaButton button) => button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));

    private static WindowDecorationsElementRole Role(Visual visual) => WindowDecorationProperties.GetElementRole(visual);

    [AvaloniaFact]
    public void The_Buttons_Minimize_Maximize_Restore_And_Close_Their_Window()
    {
        var buttons = new WindowCaptionButtons();
        var window = Open(new Border { Child = buttons });
        var maximize = Button(buttons, WindowCaptionButtons.MaximizeButtonPart);
        var glyph = Part<ArcGlyph>(buttons, WindowCaptionButtons.MaximizeGlyphPart);
        Tooltip.GetTip(maximize).ShouldBe("Maximize");
        glyph.Data.ShouldBeSameAs(ArcIcons.Square);
        glyph.Size.ShouldBe(13);

        Press(maximize);
        window.WindowState.ShouldBe(WindowState.Maximized);
        buttons.Classes.Contains(WindowCaptionButtons.MaximizedPseudoClass).ShouldBeTrue();
        Tooltip.GetTip(maximize).ShouldBe("Restore");
        Avalonia.Automation.AutomationProperties.GetName(maximize).ShouldBe("Restore");
        glyph.Data.ShouldBeSameAs(ArcIcons.Copy);
        glyph.Size.ShouldBe(14);

        Press(maximize);
        window.WindowState.ShouldBe(WindowState.Normal);
        Tooltip.GetTip(maximize).ShouldBe("Maximize");

        // A state change from elsewhere shows on the button too.
        window.WindowState = WindowState.Maximized;
        Tooltip.GetTip(maximize).ShouldBe("Restore");
        window.WindowState = WindowState.Normal;

        Press(Button(buttons, WindowCaptionButtons.MinimizeButtonPart));
        window.WindowState.ShouldBe(WindowState.Minimized);
        window.WindowState = WindowState.Normal;

        var closed = false;
        window.Closed += (_, _) => closed = true;
        Press(Button(buttons, WindowCaptionButtons.CloseButtonPart));
        Dispatcher.UIThread.RunJobs();
        closed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Buttons_Look_Like_The_Title_Bar_Ones()
    {
        var buttons = new WindowCaptionButtons { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Open(new Border { Child = buttons });
        try
        {
            var minimize = Button(buttons, WindowCaptionButtons.MinimizeButtonPart);
            var close = Button(buttons, WindowCaptionButtons.CloseButtonPart);
            minimize.Bounds.Size.ShouldBe(new Size(30, 30));
            minimize.Focusable.ShouldBeFalse();
            Tooltip.GetTip(minimize).ShouldBe("Minimize");
            Tooltip.GetTip(close).ShouldBe("Close");
            // Three 30 px squares, 4 px apart.
            buttons.Bounds.Width.ShouldBe(98);

            var center = close.TranslatePoint(new Point(15, 15), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            close.Background.ShouldBeAssignableTo<ISolidColorBrush>()!.Color
                 .ShouldBe(ArcTokens.GetColor(close, DesignTokens.ArcDesignTokenKind.DangerSubtle));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Buttons_Have_The_System_Roles_While_They_Drive_The_Window()
    {
        var buttons = new WindowCaptionButtons();
        var window = Open(new Border { Child = buttons });
        try
        {
            Role(Button(buttons, WindowCaptionButtons.MinimizeButtonPart)).ShouldBe(WindowDecorationsElementRole.MinimizeButton);
            Role(Button(buttons, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe(WindowDecorationsElementRole.MaximizeButton);
            Role(Button(buttons, WindowCaptionButtons.CloseButtonPart)).ShouldBe(WindowDecorationsElementRole.CloseButton);

            // Taken out of the window, they drive nothing and say nothing.
            ((Border)window.Content!).Child = null;
            Dispatcher.UIThread.RunJobs();
            Role(Button(buttons, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe(WindowDecorationsElementRole.None);
            Press(Button(buttons, WindowCaptionButtons.MaximizeButtonPart));
            window.WindowState.ShouldBe(WindowState.Normal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Preview_Drives_Nothing_And_Has_No_Roles()
    {
        var buttons = new WindowCaptionButtons { IsPreview = true };
        var window = Open(new Border { Child = buttons });
        try
        {
            var maximize = Button(buttons, WindowCaptionButtons.MaximizeButtonPart);
            Role(maximize).ShouldBe(WindowDecorationsElementRole.None);
            Role(Button(buttons, WindowCaptionButtons.CloseButtonPart)).ShouldBe(WindowDecorationsElementRole.None);
            Press(maximize);
            Press(Button(buttons, WindowCaptionButtons.MinimizeButtonPart));
            Press(Button(buttons, WindowCaptionButtons.CloseButtonPart));
            Dispatcher.UIThread.RunJobs();
            window.WindowState.ShouldBe(WindowState.Normal);
            window.IsVisible.ShouldBeTrue();

            // Turning the preview off makes them the window's.
            buttons.IsPreview = false;
            Role(maximize).ShouldBe(WindowDecorationsElementRole.MaximizeButton);
            Press(maximize);
            window.WindowState.ShouldBe(WindowState.Maximized);
            buttons.IsPreview = true;
            Role(maximize).ShouldBe(WindowDecorationsElementRole.None);
            buttons.Classes.Contains(WindowCaptionButtons.MaximizedPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Buttons_Follow_What_The_Window_Allows()
    {
        var buttons = new WindowCaptionButtons();
        var window = Open(new Border { Child = buttons });
        try
        {
            var minimize = Button(buttons, WindowCaptionButtons.MinimizeButtonPart);
            var maximize = Button(buttons, WindowCaptionButtons.MaximizeButtonPart);
            maximize.IsEnabled.ShouldBeTrue();
            window.CanResize = false;
            maximize.IsEnabled.ShouldBeFalse();
            Press(maximize);
            window.WindowState.ShouldBe(WindowState.Normal);
            window.CanResize = true;
            window.CanMaximize = false;
            maximize.IsEnabled.ShouldBeFalse();
            window.CanMaximize = true;
            maximize.IsEnabled.ShouldBeTrue();

            minimize.IsEnabled.ShouldBeTrue();
            window.CanMinimize = false;
            minimize.IsEnabled.ShouldBeFalse();
            Button(buttons, WindowCaptionButtons.CloseButtonPart).IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Button_Can_Be_Hidden_And_Takes_No_Room()
    {
        var buttons = new WindowCaptionButtons
        {
            ShowMinimizeButton = false,
            ShowMaximizeButton = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        var window = Open(new Border { Child = buttons });
        try
        {
            Button(buttons, WindowCaptionButtons.MinimizeButtonPart).IsVisible.ShouldBeFalse();
            Button(buttons, WindowCaptionButtons.MaximizeButtonPart).IsVisible.ShouldBeFalse();
            Button(buttons, WindowCaptionButtons.CloseButtonPart).IsEffectivelyVisible.ShouldBeTrue();
            buttons.Bounds.Width.ShouldBe(30);

            buttons.ShowMaximizeButton = true;
            buttons.ShowCloseButton = false;
            Dispatcher.UIThread.RunJobs();
            Button(buttons, WindowCaptionButtons.MaximizeButtonPart).IsEffectivelyVisible.ShouldBeTrue();
            Button(buttons, WindowCaptionButtons.CloseButtonPart).IsVisible.ShouldBeFalse();
            buttons.Bounds.Width.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tooltips_Follow_The_Language()
    {
        var languages = ArcLanguageManager.Current!;
        var buttons = new WindowCaptionButtons();
        var window = Open(new Border { Child = buttons });
        try
        {
            var close = Button(buttons, WindowCaptionButtons.CloseButtonPart);
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetTip(close).ShouldBe("关闭");
            Tooltip.GetTip(Button(buttons, WindowCaptionButtons.MinimizeButtonPart)).ShouldBe("最小化");
            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetTip(Button(buttons, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe("最大化");
            Avalonia.Automation.AutomationProperties.GetName(close).ShouldBe("關閉");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Title_Bar_Without_Drawn_Buttons_Leaves_The_Roles_To_The_App_Buttons()
    {
        // The content reaches up beside a bar that draws no buttons; the app's own set is the window's only one.
        var mine = new WindowCaptionButtons();
        var window = new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.None } };
        Open(new Border { Child = mine }, window);
        try
        {
            var bar = window.TitleBar!;
            // The bar's own buttons are hidden and drive nothing (hidden, they are not even built yet).
            var barButtons = Part<WindowCaptionButtons>(bar, TitleBar.WindowCaptionButtonsPart);
            barButtons.IsEffectivelyVisible.ShouldBeFalse();
            barButtons.Window.ShouldBeNull();
            mine.Window.ShouldBeSameAs(window);
            Role(Button(mine, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe(WindowDecorationsElementRole.MaximizeButton);

            bar.CaptionButtons = TitleBarCaptionButtons.Drawn;
            Dispatcher.UIThread.RunJobs();
            barButtons.Window.ShouldBeSameAs(window);
            Role(Button(barButtons, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe(WindowDecorationsElementRole.MaximizeButton);
            Press(Button(barButtons, WindowCaptionButtons.MaximizeButtonPart));
            window.WindowState.ShouldBe(WindowState.Maximized);
            // Both sets show the window's state.
            mine.Classes.Contains(WindowCaptionButtons.MaximizedPseudoClass).ShouldBeTrue();
            window.WindowState = WindowState.Normal;

            bar.CaptionButtons = TitleBarCaptionButtons.System;
            barButtons.Window.ShouldBeNull();
            Role(Button(barButtons, WindowCaptionButtons.MaximizeButtonPart)).ShouldBe(WindowDecorationsElementRole.None);
            Role(Button(barButtons, WindowCaptionButtons.CloseButtonPart)).ShouldBe(WindowDecorationsElementRole.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Buttons_Placed_In_A_Title_Bar_Area_Drive_The_Window()
    {
        var mine = new WindowCaptionButtons();
        var window = new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.None, RightContent = mine } };
        Open(new Border(), window);
        try
        {
            Role(Button(mine, WindowCaptionButtons.CloseButtonPart)).ShouldBe(WindowDecorationsElementRole.CloseButton);
            Press(Button(mine, WindowCaptionButtons.MaximizeButtonPart));
            window.WindowState.ShouldBe(WindowState.Maximized);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Modals_In_An_Overlay_Host_Leave_The_Title_Bar_Uncovered()
    {
        // The pattern from the docs: the bar with the caption buttons beside an ArcOverlayHost that holds the app.
        var buttons = new WindowCaptionButtons();
        var bar = new Border { Height = 40, Child = buttons, [DockPanel.DockProperty] = Dock.Top };
        var anchor = new Border();
        var host = new ArcOverlayHost { Children = { anchor } };
        var window = Open(new DockPanel { Children = { bar, host } }, new ArcWindow { IsTitleBarVisible = false });
        try
        {
            var layer = ArcOverlayLayer.Find(anchor).ShouldNotBeNull();
            layer.ShouldBeSameAs(host.Layer);
            layer.TranslatePoint(default, window)!.Value.Y.ShouldBe(40);
            ArcOverlayLayer.Find(buttons).ShouldNotBeSameAs(host.Layer);
        }
        finally
        {
            window.Close();
        }
    }
}

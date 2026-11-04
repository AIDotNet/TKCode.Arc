using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
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
/// ArcWindow extends its client area into the decorations and draws an Arc TitleBar: three areas for the app's controls,
/// caption buttons with the system roles (Snap Layouts on Windows), a press-to-move and double-click-to-maximize surface.
/// The headless platform never really extends the client area, so these tests read the hints and the visual tree.
/// </summary>
public class ArcWindowTests
{
    private static ArcWindow Open(ArcWindow? window = null, double width = 1000, double height = 600)
    {
        window ??= new ArcWindow();
        window.Width = width;
        window.Height = height;
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static AvaloniaButton Button(TitleBar bar, string part) => Part<AvaloniaButton>(bar, part);

    private static void Press(AvaloniaButton button) => button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));

    private static Rect BoundsIn(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    [AvaloniaFact]
    public void Extends_Into_The_Decorations_With_The_Bar_Above_The_Content()
    {
        var content = new Border();
        var window = Open(new ArcWindow { Title = "Cost", Content = content });
        try
        {
            window.ExtendClientAreaToDecorationsHint.ShouldBeTrue();
            window.WindowDecorationsTheme.ShouldNotBeNull().TargetType.ShouldBe(typeof(WindowDrawnDecorations));
            var bar = window.TitleBar.ShouldNotBeNull();
            bar.IsEffectivelyVisible.ShouldBeTrue();
            bar.Bounds.Height.ShouldBe(40);
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(40);
            BoundsIn(bar, window).ShouldBe(new Rect(0, 0, window.Bounds.Width, 40));
            content.TranslatePoint(default, window)!.Value.Y.ShouldBe(40);
            Part<TextBlock>(bar, "PART_Title").Text.ShouldBe("Cost");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bar_And_The_Content_Share_The_Overlay_Layer()
    {
        var content = new Border();
        var window = new ArcWindow { Content = content };
        VisualLayerManager? named = null;
        window.TemplateApplied += (_, e) => named = e.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager");
        Open(window);
        try
        {
            var layers = window.GetVisualDescendants().OfType<VisualLayerManager>().ShouldHaveSingleItem();
            named.ShouldBeSameAs(layers);
            OverlayLayer.GetOverlayLayer(content).ShouldNotBeNull();
            OverlayLayer.GetOverlayLayer(window.TitleBar!).ShouldBeSameAs(OverlayLayer.GetOverlayLayer(content));
            window.TitleBar!.GetVisualAncestors().ShouldContain(layers);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_Bar_Gives_The_Top_To_The_Content()
    {
        var content = new Border();
        var window = Open(new ArcWindow { Content = content, IsTitleBarVisible = false });
        try
        {
            Part<ContentPresenter>(window, ArcWindow.TitleBarPresenterPart).IsVisible.ShouldBeFalse();
            content.TranslatePoint(default, window)!.Value.Y.ShouldBe(0);
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(-1);

            window.IsTitleBarVisible = true;
            Dispatcher.UIThread.RunJobs();
            content.TranslatePoint(default, window)!.Value.Y.ShouldBe(40);
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Replaced_Bar_Sets_The_Height_Hint_And_Its_Own_Title()
    {
        var bar = new TitleBar { Height = 48, Title = "Mine" };
        var window = Open(new ArcWindow { Title = "Window", TitleBar = bar });
        try
        {
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(48);
            Part<TextBlock>(bar, "PART_Title").Text.ShouldBe("Mine");

            window.TitleBar = null;
            Dispatcher.UIThread.RunJobs();
            window.ExtendClientAreaTitleBarHeightHint.ShouldBe(-1);
            TopLevel.GetTopLevel(bar).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Title_Follows_The_Window_Until_Set()
    {
        var window = Open(new ArcWindow { Title = "First" });
        try
        {
            var bar = window.TitleBar!;
            var title = Part<TextBlock>(bar, "PART_Title");
            title.Text.ShouldBe("First");
            title.IsHitTestVisible.ShouldBeFalse();
            window.Title = "Second";
            title.Text.ShouldBe("Second");
            bar.Title = "Own";
            title.Text.ShouldBe("Own");
            window.Title = "Third";
            title.Text.ShouldBe("Own");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Areas_Take_Input_And_The_Caption_Buttons_Have_The_System_Roles()
    {
        var bar = new TitleBar
        {
            CaptionButtons = TitleBarCaptionButtons.Drawn,
            LeftContent = new TextBlock { Text = "Menu" },
            CenterContent = new Border { Width = 200, Height = 28 },
            RightContent = new AvaloniaButton { Content = "Tool" }
        };
        var window = Open(new ArcWindow { TitleBar = bar });
        try
        {
            foreach (var name in new[] { "PART_LeftPresenter", "PART_CenterPresenter", "PART_RightPresenter" })
            {
                var presenter = Part<ContentPresenter>(bar, name);
                presenter.IsEffectivelyVisible.ShouldBeTrue();
                WindowDecorationProperties.GetElementRole(presenter).ShouldBe(WindowDecorationsElementRole.User);
            }
            WindowDecorationProperties.GetElementRole(Button(bar, TitleBar.MinimizeButtonPart))
                .ShouldBe(WindowDecorationsElementRole.MinimizeButton);
            WindowDecorationProperties.GetElementRole(Button(bar, TitleBar.MaximizeButtonPart))
                .ShouldBe(WindowDecorationsElementRole.MaximizeButton);
            WindowDecorationProperties.GetElementRole(Button(bar, TitleBar.CloseButtonPart))
                .ShouldBe(WindowDecorationsElementRole.CloseButton);
            // On Windows the bar is the system caption; elsewhere it moves the window itself.
            WindowDecorationProperties.GetElementRole(bar).ShouldBe(OperatingSystem.IsWindows()
                ? WindowDecorationsElementRole.TitleBar
                : WindowDecorationsElementRole.None);
            Part<Border>(bar, "PART_CaptionDivider").IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Areas_Take_No_Room()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        try
        {
            var bar = window.TitleBar!;
            foreach (var name in new[] { "PART_IconPresenter", "PART_LeftPresenter", "PART_CenterPresenter", "PART_RightPresenter" })
            {
                Part<ContentPresenter>(bar, name).IsVisible.ShouldBeFalse();
            }
            Part<Border>(bar, "PART_CaptionDivider").IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Center_Is_Centered_On_The_Bar_While_It_Fits()
    {
        var center = new Border { Width = 200, Height = 28 };
        var left = new Border { Width = 120, Height = 20 };
        var bar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn, Title = "", LeftContent = left, CenterContent = center };
        var window = Open(new ArcWindow { TitleBar = bar }, width: 1000);
        try
        {
            var panel = Part<Panel>(bar, "PART_Panel");
            var middle = BoundsIn(center, panel).Center.X;
            middle.ShouldBe(panel.Bounds.Width / 2, 0.5);

            // A wide left side pushes the center right, never under the left side.
            left.Width = 600;
            Dispatcher.UIThread.RunJobs();
            var leading = BoundsIn(Part<StackPanel>(bar, "PART_Leading"), panel);
            var placed = BoundsIn(center, panel);
            placed.X.ShouldBeGreaterThanOrEqualTo(leading.Right + 12 - 0.5);
            placed.Width.ShouldBe(200);
            placed.Right.ShouldBeLessThanOrEqualTo(BoundsIn(Part<StackPanel>(bar, "PART_Trailing"), panel).X + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Bar_Keeps_The_Caption_Buttons_Whole_And_Nothing_Overlaps()
    {
        var bar = new TitleBar
        {
            CaptionButtons = TitleBarCaptionButtons.Drawn,
            Title = "A very long window title that cannot fit in a narrow window",
            LeftContent = new Border { Width = 200, Height = 20 },
            CenterContent = new Border { Width = 300, Height = 28 }
        };
        var window = Open(new ArcWindow { TitleBar = bar, MinWidth = 0 }, width: 380);
        try
        {
            var close = Button(bar, TitleBar.CloseButtonPart);
            var closeBounds = BoundsIn(close, bar);
            closeBounds.Right.ShouldBeLessThanOrEqualTo(bar.Bounds.Width + 0.5);
            closeBounds.Width.ShouldBe(30);
            var minimize = BoundsIn(Button(bar, TitleBar.MinimizeButtonPart), bar);
            var leading = BoundsIn(Part<StackPanel>(bar, "PART_Leading"), bar);
            var centered = BoundsIn(Part<ContentPresenter>(bar, "PART_CenterPresenter"), bar);
            leading.Right.ShouldBeLessThanOrEqualTo(minimize.X + 0.5);
            centered.Right.ShouldBeLessThanOrEqualTo(minimize.X + 0.5);
            centered.X.ShouldBeGreaterThanOrEqualTo(leading.Right - 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Place_Centers_Then_Clamps_To_The_Gap()
    {
        TitleBarPanel.Place(1000, 100, 900, 200).ShouldBe(400);
        TitleBarPanel.Place(1000, 500, 900, 200).ShouldBe(500);
        TitleBarPanel.Place(1000, 100, 520, 200).ShouldBe(320);
        TitleBarPanel.Place(1000, 300, 400, 200).ShouldBe(300);
    }

    [AvaloniaFact]
    public void Caption_Buttons_Minimize_Maximize_Restore_And_Close()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        var bar = window.TitleBar!;
        var maximize = Button(bar, TitleBar.MaximizeButtonPart);
        var glyph = Part<ArcGlyph>(bar, "PART_MaximizeGlyph");
        Part<StackPanel>(bar, "PART_CaptionButtons").IsEffectivelyVisible.ShouldBeTrue();
        Tooltip.GetTip(maximize).ShouldBe("Maximize");
        glyph.Data.ShouldBeSameAs(ArcIcons.Square);

        Press(maximize);
        window.WindowState.ShouldBe(WindowState.Maximized);
        bar.Classes.Contains(TitleBar.MaximizedPseudoClass).ShouldBeTrue();
        Tooltip.GetTip(maximize).ShouldBe("Restore");
        Avalonia.Automation.AutomationProperties.GetName(maximize).ShouldBe("Restore");
        glyph.Data.ShouldBeSameAs(ArcIcons.Copy);

        Press(maximize);
        window.WindowState.ShouldBe(WindowState.Normal);
        Tooltip.GetTip(maximize).ShouldBe("Maximize");

        bar.ToggleMaximize();
        window.WindowState.ShouldBe(WindowState.Maximized);
        bar.ToggleMaximize();
        window.WindowState.ShouldBe(WindowState.Normal);

        Press(Button(bar, TitleBar.MinimizeButtonPart));
        window.WindowState.ShouldBe(WindowState.Minimized);
        window.WindowState = WindowState.Normal;

        var closed = false;
        window.Closed += (_, _) => closed = true;
        Press(Button(bar, TitleBar.CloseButtonPart));
        Dispatcher.UIThread.RunJobs();
        closed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Maximize_Is_Disabled_When_The_Window_Cannot_Grow()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        try
        {
            var bar = window.TitleBar!;
            var maximize = Button(bar, TitleBar.MaximizeButtonPart);
            maximize.IsEnabled.ShouldBeTrue();
            window.CanResize = false;
            maximize.IsEnabled.ShouldBeFalse();
            bar.ToggleMaximize();
            window.WindowState.ShouldBe(WindowState.Normal);
            window.CanResize = true;
            window.CanMaximize = false;
            maximize.IsEnabled.ShouldBeFalse();
            window.CanMaximize = true;
            maximize.IsEnabled.ShouldBeTrue();

            window.CanMinimize = false;
            Button(bar, TitleBar.MinimizeButtonPart).IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void System_Buttons_Leave_Room_At_The_Left_Except_In_Full_Screen()
    {
        var window = Open(new ArcWindow { Title = "Mac", TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.System } });
        try
        {
            var bar = window.TitleBar!;
            bar.EffectiveCaptionButtons.ShouldBe(TitleBarCaptionButtons.System);
            Part<StackPanel>(bar, "PART_CaptionButtons").IsEffectivelyVisible.ShouldBeFalse();
            var inset = Part<Border>(bar, "PART_SystemInset");
            inset.IsEffectivelyVisible.ShouldBeTrue();
            inset.Bounds.Width.ShouldBe(60);
            Part<TextBlock>(bar, "PART_Title").TranslatePoint(default, bar)!.Value.X.ShouldBe(82);

            window.WindowState = WindowState.FullScreen;
            Dispatcher.UIThread.RunJobs();
            inset.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void None_Draws_No_Buttons_And_Leaves_No_Room()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.None } });
        try
        {
            var bar = window.TitleBar!;
            Part<StackPanel>(bar, "PART_CaptionButtons").IsEffectivelyVisible.ShouldBeFalse();
            Part<Border>(bar, "PART_SystemInset").IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_Follows_The_System()
    {
        new TitleBar().EffectiveCaptionButtons.ShouldBe(OperatingSystem.IsMacOS()
            ? TitleBarCaptionButtons.System
            : TitleBarCaptionButtons.Drawn);
    }

    [AvaloniaFact]
    public void Close_Turns_Danger_Under_The_Pointer_And_Back()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        try
        {
            var close = Button(window.TitleBar!, TitleBar.CloseButtonPart);
            var before = close.Background;
            var center = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            close.IsPointerOver.ShouldBeTrue();
            close.Background.ShouldBeAssignableTo<ISolidColorBrush>()!.Color
                 .ShouldBe(ArcTokens.GetColor(close, DesignTokens.ArcDesignTokenKind.DangerSubtle));
            close.Foreground.ShouldBeAssignableTo<ISolidColorBrush>()!.Color
                 .ShouldBe(ArcTokens.GetColor(close, DesignTokens.ArcDesignTokenKind.Danger));

            window.MouseMove(new Point(500, 400));
            Dispatcher.UIThread.RunJobs();
            close.IsPointerOver.ShouldBeFalse();
            close.Background.ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Double_Click_On_The_Bar_Maximizes_And_Restores()
    {
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        try
        {
            // A third click right after a double click counts as a triple click, so the second pair lands elsewhere.
            void DoubleClick(Point point)
            {
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
            }

            DoubleClick(new Point(500, 20));
            window.WindowState.ShouldBe(WindowState.Maximized);
            DoubleClick(new Point(300, 20));
            window.WindowState.ShouldBe(WindowState.Normal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bar_In_The_Content_Is_A_Preview_That_Drives_Nothing()
    {
        var preview = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn, Title = "Preview", Height = 40 };
        var window = Open(new ArcWindow
        {
            Title = "Host",
            Content = new StackPanel { Margin = new Thickness(0, 100, 0, 0), Children = { preview } }
        });
        try
        {
            preview.Title.ShouldBe("Preview");
            WindowDecorationProperties.GetElementRole(preview).ShouldBe(WindowDecorationsElementRole.None);
            WindowDecorationProperties.GetElementRole(Button(preview, TitleBar.MaximizeButtonPart))
                .ShouldBe(WindowDecorationsElementRole.None);

            Press(Button(preview, TitleBar.MaximizeButtonPart));
            var point = preview.TranslatePoint(new Point(500, 20), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Press(Button(preview, TitleBar.CloseButtonPart));
            Dispatcher.UIThread.RunJobs();

            window.WindowState.ShouldBe(WindowState.Normal);
            window.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Control_In_An_Area_Keeps_Its_Clicks()
    {
        var clicks = 0;
        var tool = new AvaloniaButton { Content = "Tool", Width = 60, Height = 28, Background = Brushes.Gray };
        tool.Click += (_, _) => clicks++;
        var window = Open(new ArcWindow { TitleBar = new TitleBar { RightContent = tool } });
        try
        {
            var point = tool.TranslatePoint(new Point(30, 14), window)!.Value;
            for (var i = 0; i < 2; i++)
            {
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
            }
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe(2);
            window.WindowState.ShouldBe(WindowState.Normal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Caption_Tooltips_Follow_The_Language()
    {
        var languages = ArcLanguageManager.Current!;
        var window = Open(new ArcWindow { TitleBar = new TitleBar { CaptionButtons = TitleBarCaptionButtons.Drawn } });
        try
        {
            var close = Button(window.TitleBar!, TitleBar.CloseButtonPart);
            Tooltip.GetTip(close).ShouldBe("Close");
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetTip(close).ShouldBe("关闭");
            Avalonia.Automation.AutomationProperties.GetName(close).ShouldBe("关闭");
            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetTip(Button(window.TitleBar!, TitleBar.MaximizeButtonPart)).ShouldBe("最大化");
            Tooltip.GetTip(close).ShouldBe("關閉");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Window_Theme_Gives_Arc_Text_And_Background()
    {
        var window = Open();
        try
        {
            window.Background.ShouldBeAssignableTo<ISolidColorBrush>()!.Color
                  .ShouldBe(ArcTokens.GetColor(window, DesignTokens.ArcDesignTokenKind.Background));
            window.MinWidth.ShouldBe(360);
            window.MinHeight.ShouldBe(240);
            window.TitleBar!.Background.ShouldBeAssignableTo<ISolidColorBrush>()!.Color
                  .ShouldBe(ArcTokens.GetColor(window, DesignTokens.ArcDesignTokenKind.Background));
        }
        finally
        {
            window.Close();
        }
    }
}

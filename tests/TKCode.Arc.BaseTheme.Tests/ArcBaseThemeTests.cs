using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Path = Avalonia.Controls.Shapes.Path;

namespace TKCode.Arc.BaseTheme.Tests;

/// <summary>
/// ArcBaseTheme in an application that has no other theme: every covered built-in type gets its template from it, the
/// template parts Avalonia requires are there, scrolling works, token changes (a light/dark publish) reach the controls,
/// and the theme yields to FluentTheme and SimpleTheme.
/// </summary>
public class ArcBaseThemeTests
{
    private static readonly Color LightForeground = Color.Parse("#0A0A0A");
    private static readonly Color DarkForeground = Color.Parse("#FAFAFA");

    /// <summary>Registers the base theme and a few ArcDesign tokens, as the Foundation publishes them (boxed kind keys).</summary>
    private static Application Setup(bool dark = false)
    {
        var app = Application.Current!;
        Publish(app, dark);
        ArcBaseTheme.TryRegister(app).ShouldBeTrue();
        return app;
    }

    private static void Publish(Application app, bool dark)
    {
        app.Resources[ArcDesignTokenKind.Foreground] = new SolidColorBrush(dark ? DarkForeground : LightForeground);
        app.Resources[ArcDesignTokenKind.Background] = new SolidColorBrush(dark ? Colors.Black : Colors.White);
        app.Resources[ArcDesignTokenKind.Accent] = new SolidColorBrush(dark ? Colors.CornflowerBlue : Colors.RoyalBlue);
        app.Resources[ArcDesignTokenKind.TypeSM] = 14.0;
        app.Resources[ArcDesignTokenKind.GlyphSizeSM] = 16.0;
        app.Resources[ArcDesignTokenKind.RadiusPill] = new CornerRadius(999);
    }

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Part<T>(Visual root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static ScrollViewer LongList(out StackPanel list)
    {
        list = new StackPanel();
        for (var i = 0; i < 50; i++)
        {
            list.Children.Add(new TextBlock { Text = "Row " + i, Height = 20 });
        }
        return new ScrollViewer { Content = list, Width = 200, Height = 150 };
    }

    [AvaloniaFact]
    public void Registers_A_Default_Theme_For_Every_Covered_Type()
    {
        var app = Application.Current!;
        ArcBaseTheme.ShouldRegister(app).ShouldBeTrue();
        ArcBaseTheme.TryRegister(app).ShouldBeTrue();

        foreach (var type in ArcBaseTheme.StyledTypes)
        {
            app.TryFindResource(type, out var value).ShouldBeTrue(type.Name);
            var theme = value.ShouldBeOfType<ControlTheme>();
            theme.TargetType.ShouldBe(type);
        }
        // A second call finds its own theme and does nothing.
        ArcBaseTheme.TryRegister(app).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ScrollViewer_Has_Its_Parts_And_Scrolls()
    {
        Setup();
        var viewer = LongList(out _);
        var window = Show(viewer);

        Part<ScrollContentPresenter>(viewer, "PART_ContentPresenter").Bounds.Size.ShouldBe(new Size(200, 150));
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        bar.IsVisible.ShouldBeTrue();
        bar.Bounds.Width.ShouldBe(11);
        bar.Bounds.Right.ShouldBe(200);
        Part<ScrollBar>(viewer, "PART_HorizontalScrollBar").IsVisible.ShouldBeFalse();
        var track = Part<Track>(bar, "PART_Track");
        track.IsDirectionReversed.ShouldBeTrue();
        Part<RepeatButton>(bar, "PART_PageUpButton");
        Part<RepeatButton>(bar, "PART_PageDownButton");
        var thumb = Part<Thumb>(bar, "thumb");
        var pill = Part<Border>(thumb, "PART_Pill");
        pill.Bounds.Width.ShouldBe(5);
        thumb.Bounds.Height.ShouldBeGreaterThanOrEqualTo(28);

        // Dragging the thumb scrolls.
        var grab = thumb.TranslatePoint(new Point(5, 10), window)!.Value;
        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(grab + new Point(0, 40));
        window.MouseUp(grab + new Point(0, 40), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBeGreaterThan(200);

        // The bar drives the viewer and the viewer drives the bar.
        bar.SetCurrentValue(RangeBase.ValueProperty, 300.0);
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBe(300);
        viewer.Offset = new Vector(0, 850);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        bar.Value.ShouldBe(850);
        thumb.Bounds.Bottom.ShouldBe(track.Bounds.Height, 0.5);

        // The mouse wheel scrolls.
        viewer.Offset = new Vector(0, 0);
        window.UpdateLayout();
        window.MouseWheel(viewer.TranslatePoint(new Point(50, 50), window)!.Value, new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBeGreaterThan(0);

        // Pressing the track below the thumb pages down.
        var before = viewer.Offset.Y;
        var below = bar.TranslatePoint(new Point(5, 140), window)!.Value;
        window.MouseDown(below, MouseButton.Left);
        window.MouseUp(below, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBeGreaterThan(before);
    }

    [AvaloniaFact]
    public void Thumb_Thickens_Under_The_Pointer()
    {
        Setup();
        var viewer = LongList(out _);
        var window = Show(viewer);
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        var thumb = Part<Thumb>(bar, "thumb");
        var pill = Part<Border>(thumb, "PART_Pill");
        thumb.Opacity.ShouldBe(0.30, 0.001);

        window.MouseMove(bar.TranslatePoint(new Point(5, 10), window)!.Value);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        thumb.Opacity.ShouldBe(0.46, 0.001);
        pill.Bounds.Width.ShouldBe(7);
        pill.Bounds.Right.ShouldBe(9);
    }

    [AvaloniaFact]
    public void Bars_Are_Hidden_At_Rest_And_Show_While_A_Mouse_Is_Over()
    {
        Setup();
        using var timers = new ManualHideTimers();
        var viewer = LongList(out _);
        var window = Show(new Border { Padding = new Thickness(50), Child = viewer });
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        bar.IsVisible.ShouldBeTrue();
        bar.Opacity.ShouldBe(0);
        bar.IsHitTestVisible.ShouldBeFalse();

        window.MouseMove(viewer.TranslatePoint(new Point(50, 50), window)!.Value);
        Dispatcher.UIThread.RunJobs();
        ScrollViewerAutoHide.GetIsShown(viewer).ShouldBeTrue();
        bar.Opacity.ShouldBe(1);
        bar.IsHitTestVisible.ShouldBeTrue();

        // Hovering is not a timer: the bars stay however long the mouse rests there.
        timers.Advance(TimeSpan.FromSeconds(5));
        bar.Opacity.ShouldBe(1);

        window.MouseMove(new Point(10, 10));
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(0);
        bar.IsHitTestVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Bars_Show_While_Scrolling_And_Hide_After_The_Delay()
    {
        Setup();
        using var timers = new ManualHideTimers();
        var viewer = LongList(out _);
        var window = Show(viewer);
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        bar.Opacity.ShouldBe(0);

        viewer.Offset = new Vector(0, 100);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(1);

        // Every offset change starts the delay over.
        timers.Advance(TimeSpan.FromMilliseconds(600));
        viewer.Offset = new Vector(0, 200);
        timers.Advance(TimeSpan.FromMilliseconds(600));
        bar.Opacity.ShouldBe(1);
        timers.Advance(ScrollViewerAutoHide.HideDelay - TimeSpan.FromMilliseconds(600));
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(0);
        bar.IsHitTestVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Bars_Stay_While_The_Thumb_Is_Dragged()
    {
        Setup();
        using var timers = new ManualHideTimers();
        var viewer = LongList(out _);
        var window = Show(new Border { Padding = new Thickness(0, 0, 100, 0), Child = viewer });
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        var thumb = Part<Thumb>(bar, "thumb");
        var grab = thumb.TranslatePoint(new Point(5, 10), window)!.Value;
        window.MouseMove(grab);
        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(grab + new Point(0, 20));
        Dispatcher.UIThread.RunJobs();

        // Out of the viewer, far past the delay, still dragging.
        window.MouseMove(grab + new Point(150, 30));
        timers.Advance(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.Y.ShouldBeGreaterThan(0);
        bar.Opacity.ShouldBe(1);

        window.MouseUp(grab + new Point(150, 30), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(1);
        timers.Advance(ScrollViewerAutoHide.HideDelay);
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void AllowAutoHide_False_Keeps_The_Bars()
    {
        Setup();
        using var timers = new ManualHideTimers();
        var viewer = LongList(out _);
        viewer.AllowAutoHide = false;
        Show(viewer);
        var bar = Part<ScrollBar>(viewer, "PART_VerticalScrollBar");
        bar.Opacity.ShouldBe(1);
        bar.IsHitTestVisible.ShouldBeTrue();

        viewer.Offset = new Vector(0, 100);
        timers.Advance(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(1);

        viewer.AllowAutoHide = true;
        Dispatcher.UIThread.RunJobs();
        bar.Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_ScrollBar_On_Its_Own_Is_Always_Shown()
    {
        Setup();
        var bar = new ScrollBar { Orientation = Avalonia.Layout.Orientation.Vertical, Maximum = 100, ViewportSize = 10, Height = 100 };
        Show(new StackPanel { Children = { bar } });
        bar.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Bars_Fade_By_Motion_Level_And_Switch_At_Once_With_Motion_Off()
    {
        Setup();
        using var timers = new ManualHideTimers();
        var off = LongList(out _);
        var full = LongList(out _);
        var reduced = LongList(out _);
        var window = Show(new StackPanel { Children = { off, full, reduced } });
        ArcMotion.SetLevel(full, ArcMotionLevel.Full);
        ArcMotion.SetLevel(reduced, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        var offBar = Part<ScrollBar>(off, "PART_VerticalScrollBar");
        var fullBar = Part<ScrollBar>(full, "PART_VerticalScrollBar");
        fullBar.Classes.Contains(":motion-full").ShouldBeTrue();
        offBar.Classes.Contains(":motion-full").ShouldBeFalse();
        offBar.Transitions.ShouldBeNull();

        // Hidden: the fade out is the considered duration; Reduced keeps a short fade.
        Fade(fullBar).Duration.ShouldBe(ArcMotionPresets.Considered);
        Fade(Part<ScrollBar>(reduced, "PART_VerticalScrollBar")).Duration.ShouldBe(ArcMotionPresets.Fast);

        off.Offset = new Vector(0, 100);
        full.Offset = new Vector(0, 100);
        Dispatcher.UIThread.RunJobs();
        offBar.Opacity.ShouldBe(1);
        // Shown: the fade in is the fast duration and has only begun.
        Fade(fullBar).Duration.ShouldBe(ArcMotionPresets.Fast);
        fullBar.Opacity.ShouldBeLessThan(1);

        // Hidden again: the fade out is back to the considered duration (Avalonia's transition clock follows the render
        // timer, which headless cannot advance by a set time, so the fade itself is not stepped here).
        timers.Advance(ScrollViewerAutoHide.HideDelay);
        Dispatcher.UIThread.RunJobs();
        offBar.Opacity.ShouldBe(0);
        Fade(fullBar).Duration.ShouldBe(ArcMotionPresets.Considered);
        window.Close();
    }

    private static DoubleTransition Fade(ScrollBar bar)
        => bar.Transitions.ShouldNotBeNull().OfType<DoubleTransition>().Single(t => t.Property == Visual.OpacityProperty);

    [AvaloniaFact]
    public void Horizontal_Bar_Lies_Along_The_Bottom()
    {
        Setup();
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        for (var i = 0; i < 30; i++)
        {
            row.Children.Add(new Border { Width = 40, Height = 20 });
        }
        var viewer = new ScrollViewer
        {
            Content = row,
            Width = 200,
            Height = 100,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Show(viewer);
        var bar = Part<ScrollBar>(viewer, "PART_HorizontalScrollBar");
        bar.IsVisible.ShouldBeTrue();
        bar.Bounds.Height.ShouldBe(11);
        bar.Bounds.Bottom.ShouldBe(100);
        Part<Track>(bar, "PART_Track").IsDirectionReversed.ShouldBeFalse();
        Part<Border>(Part<Thumb>(bar, "thumb"), "PART_Pill").Bounds.Height.ShouldBe(5);
        bar.SetCurrentValue(RangeBase.ValueProperty, 400.0);
        Dispatcher.UIThread.RunJobs();
        viewer.Offset.X.ShouldBe(400);
    }

    [AvaloniaFact]
    public void Token_Changes_Reach_The_Controls()
    {
        var app = Setup();
        var viewer = LongList(out _);
        var handle = new TextSelectionHandle();
        Show(new StackPanel { Children = { viewer, handle } });
        var pill = Part<Border>(Part<Thumb>(viewer, "thumb"), "PART_Pill");
        var drop = Part<Path>(handle, "PART_Handle");
        ((ISolidColorBrush)pill.Background!).Color.ShouldBe(LightForeground);
        ((ISolidColorBrush)drop.Fill!).Color.ShouldBe(Colors.RoyalBlue);

        Publish(app, dark: true);
        Dispatcher.UIThread.RunJobs();

        ((ISolidColorBrush)pill.Background!).Color.ShouldBe(DarkForeground);
        ((ISolidColorBrush)drop.Fill!).Color.ShouldBe(Colors.CornflowerBlue);
    }

    [AvaloniaFact]
    public void ItemsControl_Presents_Its_Items()
    {
        Setup();
        var items = new ItemsControl { ItemsSource = new[] { "one", "two", "three" } };
        Show(items);
        Part<ItemsPresenter>(items, "PART_ItemsPresenter");
        items.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldBe(["one", "two", "three"]);
    }

    [AvaloniaFact]
    public void PathIcon_Draws_Its_Geometry_At_Glyph_Size()
    {
        Setup();
        var icon = new PathIcon { Data = Geometry.Parse("M0,0 L10,0 L10,10 Z"), Foreground = Brushes.Red };
        Show(new StackPanel { Children = { icon } });
        icon.Bounds.Size.ShouldBe(new Size(16, 16));
        var path = Part<Path>(icon, "PART_Path");
        path.Data.ShouldBeSameAs(icon.Data);
        path.Fill.ShouldBe(Brushes.Red);
    }

    [AvaloniaFact]
    public void TextSelectionHandle_Applies_Its_Template_With_An_Indicator()
    {
        Setup();
        var handle = new TextSelectionHandle();
        Show(new Canvas { Children = { handle } });
        handle.IsTemplateApplied().ShouldBeTrue();
        Part<Border>(handle, "PART_Indicator").Width.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Overlay_Popup_Host_Presents_The_Popup()
    {
        Setup();
        var anchor = new Border { Width = 10, Height = 10 };
        // Headless has no popup windows: popups go to the overlay layer of this layer manager (an internal switch).
        var layers = new VisualLayerManager { Child = anchor };
        typeof(VisualLayerManager)
            .GetProperty("EnablePopupOverlayLayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(layers, true);
        var window = Show(layers);
        var child = new TextBlock { Text = "popup" };
        var popup = new Popup { Child = child, PlacementTarget = anchor, ShouldUseOverlayLayer = true };
        ((ISetLogicalParent)popup).SetParent(anchor);
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        var host = child.GetVisualAncestors().OfType<TemplatedControl>().OfType<OverlayPopupHost>().FirstOrDefault();
        host.ShouldNotBeNull();
        Part<ContentPresenter>(host, "PART_ContentPresenter").Child.ShouldBe(child);
        child.FontSize.ShouldBe(14);
        popup.IsOpen = false;
        window.Close();
    }

    /// <summary>Headless creates no native popup windows (PopupRoot) or embedded roots, so their templates are built directly.</summary>
    [AvaloniaFact]
    public void Host_Templates_Have_The_Parts_Avalonia_Uses()
    {
        var app = Setup();
        foreach (var type in new[] { typeof(PopupRoot), typeof(EmbeddableControlRoot), typeof(OverlayPopupHost) })
        {
            app.TryFindResource(type, out var value).ShouldBeTrue();
            var template = ((ControlTheme)value!).Setters.OfType<Setter>()
                                                 .Single(s => s.Property == TemplatedControl.TemplateProperty).Value
                                                 .ShouldBeAssignableTo<IControlTemplate>()!;
            // Built against a stand-in: the template only needs a templated parent to bind to.
            var built = template.Build(new ContentControl())!;
            built.NameScope.Find<ContentPresenter>("PART_ContentPresenter").ShouldNotBeNull(type.Name);
            built.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager").ShouldNotBeNull(type.Name);
        }
    }

    /// <summary>
    /// The test application's own window theme taken away, as in an app that sets none: a plain Window gets the base
    /// theme.
    /// </summary>
    private static Application SetupWithoutAppWindowTheme(bool dark = false)
    {
        var app = Application.Current!;
        app.Resources.Remove(typeof(Window));
        app.Resources[ArcDesignTokenKind.BodyFontFamily] = new FontFamily("Arc Body Test");
        return Setup(dark);
    }

    private static Window ShowPlain(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Plain_Window_Gets_The_Base_Template_With_Overlay_And_Adorner_Layers()
    {
        SetupWithoutAppWindowTheme();
        var text = new TextBlock { Text = "content" };
        var window = ShowPlain(text);

        var layers = Part<VisualLayerManager>(window, "PART_VisualLayerManager");
        layers.EnableOverlayLayer.ShouldBeTrue();
        Part<ContentPresenter>(window, "PART_ContentPresenter").Child.ShouldBe(text);
        Part<Border>(window, "PART_TransparencyFallback");
        // The layers popups, toasts and the ThemeSwitcher reveal use, sized to the window.
        var overlay = OverlayLayer.GetOverlayLayer(text);
        overlay.ShouldNotBeNull();
        overlay.GetVisualAncestors().ShouldContain(layers);
        // The manager creates the layer when it is first asked for; the next layout sizes it to the window.
        window.UpdateLayout();
        overlay.Bounds.Size.ShouldBe(new Size(400, 300));
        var adorners = AdornerLayer.GetAdornerLayer(text);
        adorners.ShouldNotBeNull();
        adorners.GetVisualAncestors().ShouldContain(layers);
        window.Close();
    }

    [AvaloniaFact]
    public void Plain_Window_Hosts_Overlay_Popups_Without_A_Layer_Manager_Of_Its_Own()
    {
        SetupWithoutAppWindowTheme();
        var anchor = new Border { Width = 10, Height = 10 };
        var window = ShowPlain(anchor);
        var child = new TextBlock { Text = "popup" };
        var popup = new Popup { Child = child, PlacementTarget = anchor, ShouldUseOverlayLayer = true };
        ((ISetLogicalParent)popup).SetParent(anchor);
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        child.GetVisualAncestors().OfType<OverlayPopupHost>().ShouldHaveSingleItem();
        popup.IsOpen = false;
        window.Close();
    }

    [AvaloniaFact]
    public void Plain_Window_Takes_Its_Background_And_Text_Defaults_From_The_Tokens_And_Follows_Them()
    {
        var app = SetupWithoutAppWindowTheme();
        var text = new TextBlock { Text = "content" };
        var window = ShowPlain(text);
        ((ISolidColorBrush)window.Background!).Color.ShouldBe(Colors.White);
        ((ISolidColorBrush)text.Foreground!).Color.ShouldBe(LightForeground);
        text.FontSize.ShouldBe(14);
        text.FontFamily.Name.ShouldBe("Arc Body Test");
        text.FontWeight.ShouldBe(FontWeight.Normal);

        Publish(app, dark: true);
        Dispatcher.UIThread.RunJobs();

        ((ISolidColorBrush)window.Background!).Color.ShouldBe(Colors.Black);
        ((ISolidColorBrush)text.Foreground!).Color.ShouldBe(DarkForeground);
        window.Close();
    }

    [AvaloniaFact]
    public void An_Application_Window_Theme_In_Resources_Wins_And_The_Rest_Is_Registered()
    {
        // The test application defines its own Window theme (AvaloniaTestApp.cs).
        var app = Application.Current!;
        var own = app.Resources[typeof(Window)].ShouldBeOfType<ControlTheme>();
        ArcBaseTheme.TryRegister(app).ShouldBeTrue();
        app.Resources.MergedDictionaries.ShouldHaveSingleItem().TryGetResource(typeof(Window), null, out _).ShouldBeFalse();
        app.TryFindResource(typeof(ScrollViewer), out var viewerTheme).ShouldBeTrue();
        viewerTheme.ShouldBeOfType<ControlTheme>();
        app.TryFindResource(typeof(Window), out var windowTheme).ShouldBeTrue();
        windowTheme.ShouldBeSameAs(own);

        var window = ShowPlain(new Border());
        window.GetVisualDescendants().OfType<VisualLayerManager>().ShouldNotContain(m => m.Name == "PART_VisualLayerManager");
        window.Close();
    }

    [AvaloniaFact]
    public void An_Application_Window_Theme_In_Styles_Wins()
    {
        var app = Application.Current!;
        var own = (ControlTheme)app.Resources[typeof(Window)]!;
        app.Resources.Remove(typeof(Window));
        app.Styles.Add(new Styles { Resources = { [typeof(Window)] = own } });
        ArcBaseTheme.TryRegister(app).ShouldBeTrue();
        app.TryFindResource(typeof(Window), out var windowTheme).ShouldBeTrue();
        windowTheme.ShouldBeSameAs(own);
    }

    [AvaloniaFact]
    public void A_Window_That_Sets_Its_Own_Template_Keeps_It()
    {
        // The gallery's window sets its template and binds its text defaults locally.
        SetupWithoutAppWindowTheme();
        var marker = new Border { Name = "Own" };
        var window = new Window
        {
            Width = 400,
            Height = 300,
            Background = Brushes.Red,
            Template = new FuncControlTemplate<Window>((_, _) => marker)
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.GetVisualChildren().ShouldHaveSingleItem().ShouldBeSameAs(marker);
        window.Background.ShouldBe(Brushes.Red);
        window.Close();
    }

    /// <summary>
    /// The browser's root exists before Arc is set up and never looks its default theme up again; a style hands it the
    /// base theme's (headless cannot create an embedded root, so the style itself is checked).
    /// </summary>
    [AvaloniaFact]
    public void A_Style_Gives_An_Existing_Browser_Root_The_Base_Theme()
    {
        var app = Setup();
        app.TryFindResource(typeof(EmbeddableControlRoot), out var theme).ShouldBeTrue();
        var style = app.Styles.OfType<Style>().ShouldHaveSingleItem();
        style.Selector!.ToString().ShouldBe("EmbeddableControlRoot");
        var setter = style.Setters.OfType<Setter>().ShouldHaveSingleItem();
        setter.Property.ShouldBe(StyledElement.ThemeProperty);
        setter.Value.ShouldBeSameAs(theme);
    }

    [AvaloniaFact]
    public void Window_Template_Has_The_Parts_Avalonia_Uses()
    {
        var resources = ArcBaseTheme.Create();
        resources.TryGetResource(typeof(Window), null, out var value).ShouldBeTrue();
        var theme = value.ShouldBeOfType<ControlTheme>();
        theme.TargetType.ShouldBe(typeof(Window));
        var template = theme.Setters.OfType<Setter>().Single(s => s.Property == TemplatedControl.TemplateProperty).Value
                            .ShouldBeAssignableTo<IControlTemplate>()!;
        var built = template.Build(new ContentControl())!;
        built.NameScope.Find<ContentPresenter>("PART_ContentPresenter").ShouldNotBeNull();
        built.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager").ShouldNotBeNull().EnableOverlayLayer.ShouldBeTrue();
        built.NameScope.Find<Border>("PART_TransparencyFallback").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Yields_To_FluentTheme()
    {
        var app = Application.Current!;
        app.Styles.Add(new FluentTheme());
        ArcBaseTheme.ShouldRegister(app).ShouldBeFalse();
        ArcBaseTheme.TryRegister(app).ShouldBeFalse();

        var viewer = LongList(out _);
        Show(viewer);
        viewer.GetVisualDescendants().OfType<Thumb>().ShouldNotContain(t => t.Name == "thumb"
            && t.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "PART_Pill"));
        app.Resources.MergedDictionaries.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Yields_To_SimpleTheme()
    {
        var app = Application.Current!;
        app.Styles.Add(new SimpleTheme());
        ArcBaseTheme.ShouldRegister(app).ShouldBeFalse();
        ArcBaseTheme.TryRegister(app).ShouldBeFalse();
        app.Resources.MergedDictionaries.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Detects_A_Theme_That_Is_Not_Yet_In_An_Application()
    {
        ArcBaseTheme.DefinesBuiltInThemes(new FluentTheme()).ShouldBeTrue();
        ArcBaseTheme.DefinesBuiltInThemes(new SimpleTheme()).ShouldBeTrue();
        ArcBaseTheme.DefinesBuiltInThemes(new ResourceDictionary()).ShouldBeFalse();
        // An application's window theme alone is not a theme for the built-in controls.
        ArcBaseTheme.DefinesBuiltInThemes(new ResourceDictionary { [typeof(Window)] = new ControlTheme(typeof(Window)) }).ShouldBeFalse();
        ArcBaseTheme.DefinesBuiltInThemes(ArcBaseTheme.Create()).ShouldBeTrue();
    }
}

/// <summary>The base theme's hide delays on a manual clock: <see cref="Advance"/> fires every delay that came due.</summary>
internal sealed class ManualHideTimers : IDisposable
{
    private readonly Func<TimeSpan, Action, ScrollViewerAutoHide.IHideTimer> _previous;
    private readonly List<Timer> _timers = new();
    private TimeSpan _now;

    public ManualHideTimers()
    {
        _previous = ScrollViewerAutoHide.CreateTimer;
        ScrollViewerAutoHide.CreateTimer = (delay, elapsed) =>
        {
            var timer = new Timer(this, delay, elapsed);
            _timers.Add(timer);
            return timer;
        };
    }

    public void Advance(TimeSpan duration)
    {
        _now += duration;
        foreach (var timer in _timers.ToArray())
        {
            if (timer.Due is { } due && due <= _now)
            {
                timer.Due = null;
                timer.Elapsed();
            }
        }
    }

    public void Dispose() => ScrollViewerAutoHide.CreateTimer = _previous;

    private sealed class Timer(ManualHideTimers owner, TimeSpan delay, Action elapsed) : ScrollViewerAutoHide.IHideTimer
    {
        public TimeSpan? Due { get; set; }

        public Action Elapsed { get; } = elapsed;

        public void Restart() => Due = owner._now + delay;

        public void Stop() => Due = null;
    }
}

internal static class TemplateExtensions
{
    public static bool IsTemplateApplied(this TemplatedControl control) => control.GetVisualChildren().Any();
}

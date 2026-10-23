using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.HeadersFooters;

public class SiteHeaderTests
{
    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Continuations of finished motion hop through the thread pool: pump until the condition holds.</summary>
    private static void PumpUntil(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Thread.Sleep(10);
            Pump();
        }
    }

    private static (Window Window, SiteHeader Header, ScrollViewer? Scroll) Show(double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off,
                                                                              SiteHeaderVariant variant = SiteHeaderVariant.Mega, bool scrolling = false)
    {
        var header = new SiteHeader { Variant = variant, Current = "product", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        Control root = header;
        ScrollViewer? scroll = null;
        if (scrolling)
        {
            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(new Border { Height = 2000 });
            scroll = new ScrollViewer { Content = stack, Height = 650 };
            root = scroll;
        }
        var window = TestHelpers.Show(root, level);
        window.Width = width;
        window.Height = 700;
        Pump();
        header.Width = scrolling ? double.NaN : width;
        Pump();
        return (window, header, scroll);
    }

    [AvaloniaFact]
    public void Defaults_Show_Arcs_Example_Items_And_Actions()
    {
        var (_, header, _) = Show();
        header.Variant.ShouldBe(SiteHeaderVariant.Mega);
        header.BrandName.ShouldBe("Arc");
        header.PrimaryActionLabel.ShouldBe("Get Arc");
        header.SecondaryActionLabel.ShouldBe("Sign in");
        header.CollapseWidth.ShouldBe(760);
        header.ScrollThreshold.ShouldBe(8);
        header.IsSticky.ShouldBeTrue();
        header.NavItemsPanel!.Children.Count.ShouldBe(4);
        header.NavButton("product")!.HasChevron.ShouldBeTrue();
        header.NavButton("pricing")!.HasChevron.ShouldBeFalse();
        header.NavButton("product")!.Bounds.Height.ShouldBe(36);
        header.Bounds.Height.ShouldBe(65);
        header.Classes.Contains(":solid").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Items_Source_Wins_Over_Items()
    {
        var (_, header, _) = Show();
        header.Items.Add(new SiteHeaderItem { Value = "a", Label = "A" });
        header.NavItemsPanel!.Children.Count.ShouldBe(1);
        header.ItemsSource = new[] { new SiteHeaderItem { Value = "x", Label = "X" }, new SiteHeaderItem { Value = "y", Label = "Y" } };
        header.NavItemsPanel.Children.Count.ShouldBe(2);
        header.NavButton("x").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Scrolling_Turns_Solid_With_Hysteresis_And_Sticks()
    {
        var (_, header, scroll) = Show(scrolling: true);
        scroll!.Offset = new Vector(0, 8);
        Pump();
        header.IsScrolled.ShouldBeFalse();
        scroll.Offset = new Vector(0, 9);
        Pump();
        header.IsScrolled.ShouldBeTrue();
        header.Classes.Contains(":solid").ShouldBeTrue();
        scroll.Offset = new Vector(0, 5);
        Pump();
        header.IsScrolled.ShouldBeTrue();
        scroll.Offset = new Vector(0, 4);
        Pump();
        header.IsScrolled.ShouldBeFalse();
        scroll.Offset = new Vector(0, 300);
        Pump();
        header.StickTransform.Y.ShouldBe(300);
        header.IsSticky = false;
        header.StickTransform.Y.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Hover_Opens_After_Intent_And_Switches_At_Once()
    {
        var (_, header, _) = Show();
        var product = header.NavButton("product")!;
        var resources = header.NavButton("resources")!;
        header.RaisePointerEnter(product);
        header.HasPendingOpen.ShouldBeTrue();
        header.OpenItemValue.ShouldBeNull();
        header.FlushOpenTimer();
        header.OpenItemValue.ShouldBe("product");
        header.Classes.Contains(":solid").ShouldBeTrue();
        product.IsExpanded.ShouldBeTrue();
        header.RaisePointerEnter(resources);
        header.OpenItemValue.ShouldBe("resources");
        header.HasPendingOpen.ShouldBeFalse();
        header.PanelView!.Faces.Children.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Leaving_The_Region_Closes_After_Grace_And_Returning_Cancels()
    {
        var (_, header, _) = Show();
        header.OpenPanel("product");
        header.RaiseRegionExit();
        header.HasPendingClose.ShouldBeTrue();
        header.RaiseRegionEnter();
        header.HasPendingClose.ShouldBeFalse();
        header.RaiseRegionExit();
        header.FlushCloseTimer();
        header.OpenItemValue.ShouldBeNull();
        Pump();
        header.PanelView.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Plain_Item_Hover_While_Open_Closes_After_Grace()
    {
        var (_, header, _) = Show();
        header.OpenPanel("product");
        header.RaisePointerEnter(header.NavButton("pricing")!);
        header.HasPendingClose.ShouldBeTrue();
        header.FlushCloseTimer();
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Touch_Pointer_Does_Not_Hover_Or_Open()
    {
        var (_, header, _) = Show();
        header.RaisePointerEnter(header.NavButton("product")!, PointerType.Touch);
        header.HasPendingOpen.ShouldBeFalse();
        header.HoveredValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Click_On_A_Trigger_Toggles_The_Panel()
    {
        var (window, header, _) = Show();
        var product = header.NavButton("product")!;
        window.MouseDown(product.TranslatePoint(new Point(20, 10), window)!.Value, Avalonia.Input.MouseButton.Left);
        window.MouseUp(product.TranslatePoint(new Point(20, 10), window)!.Value, Avalonia.Input.MouseButton.Left);
        header.OpenItemValue.ShouldBe("product");
        window.MouseDown(product.TranslatePoint(new Point(20, 10), window)!.Value, Avalonia.Input.MouseButton.Left);
        window.MouseUp(product.TranslatePoint(new Point(20, 10), window)!.Value, Avalonia.Input.MouseButton.Left);
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Direction_Follows_The_Index_Difference()
    {
        var (_, header, _) = Show();
        header.OpenPanel("resources");
        Pump();
        var first = header.PanelView!.CurrentFace!;
        header.OpenPanel("product");
        Pump();
        header.PanelView.CurrentFace!.ItemValue.ShouldBe("product");
        first.IsLeaving.ShouldBeTrue();
        first.IsHitTestVisible.ShouldBeFalse();
        // Off removes the leaving face at once.
        header.PanelView.Faces.Children.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Face_Enters_From_The_Side_Of_Travel_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (_, header, _) = Show(level: ArcMotionLevel.Full);
        header.OpenPanel("product");
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(800));
        header.OpenPanel("resources");
        Pump();
        var next = header.PanelView!.CurrentFace!;
        next.Offset.X.ShouldBe(36, 0.5);
        var old = header.PanelView.Faces.Children.OfType<SiteHeaderFace>().First(f => f.IsLeaving);
        old.IsLeaving.ShouldBeTrue();
        for (var i = 0; i < 4; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Pump();
        }
        PumpUntil(() => header.PanelView.Faces.Children.Count == 1);
        next.Offset.X.ShouldBe(0, 0.5);
        next.Opacity.ShouldBe(1, 0.01);
        header.PanelView.Faces.Children.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Panel_Height_Follows_The_Current_Face()
    {
        var (_, header, _) = Show();
        header.OpenPanel("product");
        Pump();
        var box = header.PanelView!.HeightBox;
        var productHeight = box.CurrentHeight;
        productHeight.ShouldBeGreaterThan(100);
        header.OpenPanel("resources");
        Pump();
        Pump();
        box.CurrentHeight.ShouldBe(box.NaturalHeight, 0.5);
    }

    [AvaloniaFact]
    public void Panel_Hit_Testing_Reaches_Below_The_Header_Bounds()
    {
        var (window, header, _) = Show();
        header.OpenPanel("product");
        Pump();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Pump();
        var link = header.PanelView!.FocusTargets().First();
        var point = link.TranslatePoint(new Point(10, 10), window)!.Value;
        point.Y.ShouldBeGreaterThan(header.Bounds.Height);
        var hit = window.InputHitTest(point) as Visual;
        (hit == link || link.IsVisualAncestorOf(hit!)).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Choosing_A_Panel_Link_Sets_Current_Raises_Navigate_And_Closes()
    {
        var (_, header, _) = Show();
        SiteHeaderNavigateEventArgs? seen = null;
        header.Navigate += (_, e) =>
        {
            seen = e;
            e.Handled = true;
        };
        header.OpenPanel("resources");
        Pump();
        var link = header.PanelView!.FocusTargets()[1];
        ((Avalonia.Controls.Button)link).PerformClickForTest();
        seen.ShouldNotBeNull();
        seen!.Kind.ShouldBe(SiteHeaderNavigateKind.PanelLink);
        seen.Label.ShouldBe("Guides");
        seen.Section.ShouldBe("resources");
        seen.Link.ShouldNotBeNull();
        header.Current.ShouldBe("resources");
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Plain_Item_Brand_And_Actions_Report_Their_Kind()
    {
        var (_, header, _) = Show();
        var kinds = new List<SiteHeaderNavigateKind>();
        var commands = 0;
        header.Navigate += (_, e) => kinds.Add(e.Kind);
        header.NavigateCommand = new RelayCommandForTest(_ => commands++);
        header.NavButton("pricing")!.PerformClickForTest();
        header.BrandButton!.PerformClickForTest();
        header.PrimaryButton!.PerformClickForTest();
        header.SecondaryButton!.PerformClickForTest();
        kinds.ShouldBe([SiteHeaderNavigateKind.Item, SiteHeaderNavigateKind.Brand, SiteHeaderNavigateKind.PrimaryAction, SiteHeaderNavigateKind.SecondaryAction]);
        commands.ShouldBe(4);
        header.Current.ShouldBe("pricing");
    }

    [AvaloniaFact]
    public void Simple_And_Centered_Items_Are_Plain_Links()
    {
        var (_, simple, _) = Show(variant: SiteHeaderVariant.Simple);
        simple.NavButton("product")!.HasChevron.ShouldBeFalse();
        simple.OpenPanel("product");
        simple.OpenItemValue.ShouldBeNull();
        var (_, centered, _) = Show(variant: SiteHeaderVariant.Centered);
        centered.Classes.Contains(":centered").ShouldBeTrue();
        centered.NavButton("product")!.IsCapsule.ShouldBeTrue();
        centered.NavButton("product")!.Bounds.Height.ShouldBe(32);
        centered.CapsuleIndicator!.HasTarget.ShouldBeTrue();
        centered.UnderlineIndicator!.HasTarget.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Current_Underline_Insets_And_Moves_With_Current()
    {
        var (_, header, _) = Show();
        var button = header.NavButton("product")!;
        var underline = header.UnderlineIndicator!;
        underline.HasTarget.ShouldBeTrue();
        underline.CurrentRect.X.ShouldBe(button.Bounds.X + 12, 0.5);
        underline.CurrentRect.Width.ShouldBe(button.Bounds.Width - 12 - 30, 0.5);
        underline.CurrentRect.Height.ShouldBe(2);
        header.Current = "pricing";
        var pricing = header.NavButton("pricing")!;
        underline.CurrentRect.Width.ShouldBe(pricing.Bounds.Width - 24, 0.5);
        header.NavButton("pricing")!.Classes.Contains(":current").ShouldBeTrue();
        header.NavButton("product")!.Classes.Contains(":current").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Hover_Block_Appears_In_Place_And_Vanishes_On_Leave()
    {
        var (_, header, _) = Show();
        var pricing = header.NavButton("pricing")!;
        header.RaisePointerEnter(pricing);
        header.HoveredValue.ShouldBe("pricing");
        header.HoverBlock!.HasTarget.ShouldBeTrue();
        header.HoverBlock.CurrentRect.ShouldBe(pricing.Bounds);
        header.RaiseNavListExit();
        header.HoverBlock.HasTarget.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Keyboard_Arrows_Move_Focus_And_Switch_Open_Panel()
    {
        var (window, header, _) = Show();
        var product = header.NavButton("product")!;
        product.Focus();
        Pump();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        TopLevel.GetTopLevel(header)!.FocusManager!.GetFocusedElement().ShouldBe(header.NavButton("resources"));
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        TopLevel.GetTopLevel(header)!.FocusManager!.GetFocusedElement().ShouldBe(header.NavButton("customers"));
        header.OpenPanel("product");
        header.NavButton("customers")!.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        header.NavButton("product")!.IsFocused.ShouldBeTrue();
        header.OpenItemValue.ShouldBe("product");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        header.OpenItemValue.ShouldBe("resources");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void ArrowDown_Opens_And_Focuses_First_Link_Then_Panel_Keys_Move_And_Escape_Returns()
    {
        var (window, header, _) = Show();
        header.NavButton("product")!.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump();
        header.OpenItemValue.ShouldBe("product");
        var targets = header.PanelView!.FocusTargets();
        targets.Count.ShouldBe(5);
        targets[0].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        targets[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        targets[4].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        targets[4].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        targets[0].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        header.OpenItemValue.ShouldBeNull();
        header.NavButton("product")!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ArrowUp_On_The_First_Link_Closes_And_Returns_Focus()
    {
        var (window, header, _) = Show();
        header.OpenPanel("product");
        Pump();
        header.PanelView!.FocusTargets()[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        header.OpenItemValue.ShouldBeNull();
        header.NavButton("product")!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Pressing_Outside_Closes_The_Panel()
    {
        var (window, header, _) = Show();
        header.OpenPanel("product");
        Pump();
        window.MouseDown(new Point(500, 650), Avalonia.Input.MouseButton.Left);
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Narrow_Width_Collapses_To_The_Menu_Sheet()
    {
        var (_, header, _) = Show(width: 600);
        header.IsCollapsed.ShouldBeTrue();
        header.Classes.Contains(":narrow").ShouldBeTrue();
        header.NavItemsPanel!.IsEffectivelyVisible.ShouldBeFalse();
        header.MenuButton!.IsVisible.ShouldBeTrue();
        header.SecondaryButton!.IsVisible.ShouldBeFalse();
        header.PrimaryButton!.IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(header.MenuButton).ShouldBe("Open menu");
        header.OpenMenu();
        Pump();
        AutomationProperties.GetName(header.MenuButton).ShouldBe("Close menu");
        header.Sheet.ShouldNotBeNull();
        header.Classes.Contains(":solid").ShouldBeTrue();
        header.SheetRows.Count.ShouldBe(4);
        header.SheetGroups.ContainsKey("product").ShouldBeTrue();
        header.SheetGroups.ContainsKey("pricing").ShouldBeFalse();
        header.CloseMenu();
        Pump();
        Pump();
        header.Sheet.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Sheet_Groups_Expand_One_At_A_Time_And_Links_Choose()
    {
        var (_, header, _) = Show(width: 600);
        header.OpenMenu();
        Pump();
        var product = header.SheetRows[0];
        var resources = header.SheetRows[1];
        product.PerformClickForTest();
        header.ExpandedGroup.ShouldBe("product");
        product.IsExpanded.ShouldBeTrue();
        resources.PerformClickForTest();
        header.ExpandedGroup.ShouldBe("resources");
        product.IsExpanded.ShouldBeFalse();
        resources.PerformClickForTest();
        header.ExpandedGroup.ShouldBeNull();
        resources.PerformClickForTest();
        SiteHeaderNavigateEventArgs? seen = null;
        header.Navigate += (_, e) =>
        {
            seen = e;
            e.Handled = true;
        };
        Pump();
        var link = header.SheetGroups["resources"].Box.GetVisualDescendants().OfType<SiteSheetLink>().First();
        link.PerformClickForTest();
        seen!.Label.ShouldBe("Documentation");
        header.IsMenuOpen.ShouldBeFalse();
        header.Current.ShouldBe("resources");
    }

    [AvaloniaFact]
    public void Widening_Closes_The_Sheet_And_Narrowing_Closes_The_Panel()
    {
        var (window, header, _) = Show(width: 600);
        header.OpenMenu();
        Pump();
        header.Width = 1000;
        window.Width = 1100;
        Pump();
        header.IsCollapsed.ShouldBeFalse();
        header.IsMenuOpen.ShouldBeFalse();
        header.OpenPanel("product");
        header.Width = 500;
        Pump();
        header.OpenItemValue.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Sheet_And_Returns_Focus_To_The_Menu_Button()
    {
        var (window, header, _) = Show(width: 600);
        header.OpenMenu();
        Pump();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        header.IsMenuOpen.ShouldBeFalse();
        header.MenuButton!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Scrim_Press_Closes_The_Sheet_And_Wheel_Is_Locked()
    {
        var (window, header, scroll) = Show(width: 600, scrolling: true);
        header.OpenMenu();
        Pump();
        header.Scrim.ShouldNotBeNull();
        var before = scroll!.Offset;
        window.MouseWheel(new Point(300, 620), new Vector(0, -3));
        scroll.Offset.ShouldBe(before);
        window.MouseDown(new Point(300, 620), Avalonia.Input.MouseButton.Left);
        header.IsMenuOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Off_And_Reduced_Show_The_Panel_Without_Travel()
    {
        var (_, off, _) = Show(level: ArcMotionLevel.Off);
        off.OpenPanel("product");
        Pump();
        off.PanelView!.Pose.ShouldBe((1, 0, 1));
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (_, reduced, _) = Show(level: ArcMotionLevel.Reduced);
        reduced.OpenPanel("product");
        Pump();
        reduced.PanelView!.Pose.ShouldBe((1, 0, 1));
        reduced.OpenPanel("resources");
        Pump();
        reduced.PanelView.CurrentFace!.Offset.ShouldBe(default);
    }

    [AvaloniaFact]
    public void Panel_Enters_On_Grow_And_Leaves_Faster_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (_, header, _) = Show(level: ArcMotionLevel.Full);
        header.OpenPanel("product");
        Pump();
        var view = header.PanelView!;
        view.Pose.Opacity.ShouldBe(0, 0.01);
        view.Pose.Y.ShouldBe(-6, 0.01);
        view.Pose.Scale.ShouldBe(0.985, 0.001);
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        view.Pose.ShouldBe((1, 0, 1));
        header.ClosePanel();
        clock.Advance(TimeSpan.FromMilliseconds(90));
        view.Pose.Opacity.ShouldBeLessThan(1);
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(200));
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Pump();
        PumpUntil(() => header.PanelView is null);
        header.PanelView.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Gallery_Demos_Render()
    {
        foreach (var demo in new Control[]
                 {
                     new TKCode.Arc.Gallery.Demos.SiteHeaderPreviewDemo(), new TKCode.Arc.Gallery.Demos.SiteHeaderSimpleDemo(),
                     new TKCode.Arc.Gallery.Demos.SiteHeaderEventsDemo(), new TKCode.Arc.Gallery.Demos.SiteHeaderNarrowDemo()
                 })
        {
            var window = TestHelpers.Show(demo);
            Pump();
            demo.GetVisualDescendants().OfType<SiteHeader>().Count().ShouldBe(1);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Names_And_Landmarks_Are_Set()
    {
        var (_, header, _) = Show();
        AutomationProperties.GetName(header.NavButton("product")!).ShouldBe("Product");
        AutomationProperties.GetName(header.BrandButton!).ShouldBe("Arc");
        var nav = header.GetVisualDescendants().OfType<Border>().First(b => b.Name == SiteHeader.NavListPart);
        AutomationProperties.GetName(nav).ShouldBe("Main");
        header.OpenPanel("resources");
        AutomationProperties.GetName(header.PanelView!).ShouldBe("Resources");
        header.Label = "Primary";
        AutomationProperties.GetName(nav).ShouldBe("Primary");
    }
}

internal sealed class RelayCommandForTest(Action<object?> run) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run(parameter);
}

internal static class SiteHeaderTestExtensions
{
    public static void PerformClickForTest(this Avalonia.Controls.Button button)
        => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
}

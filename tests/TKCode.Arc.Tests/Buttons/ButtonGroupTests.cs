using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class ButtonGroupTests
{
    private static ButtonGroup Create(MenuSurface? menu = null)
    {
        var group = new ButtonGroup
        {
            Label = "Document actions",
            Items =
            {
                new ButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2, ReserveLabels = ["Copied"] },
                new ButtonGroupItem { Label = "Duplicate", Icon = ArcIcons.Copy },
                new ButtonGroupItem { Label = "Archive", Icon = ArcIcons.Archive, IsDisabled = true }
            },
            Menu = menu
        };
        return group;
    }

    private static MenuSurface CreateMenu() => new()
    {
        Items =
        {
            new MenuEntry { Header = "Pin" },
            new MenuEntry { Header = "Delete", IsDestructive = true }
        }
    };

    private static Window Show(Control control, double width = 520) =>
        TestHelpers.Show(new Border { Width = width, Padding = new Thickness(20), Child = control });

    private static ButtonGroupItem Segment(ButtonGroup group, int index) => (ButtonGroupItem)group.ContainerFromIndex(index)!;

    private static Button Chevron(ButtonGroup group) => Part<Button>(group, ButtonGroup.MenuButtonPart);

    private static Point Center(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var group = Create();
        var window = Show(group);
        try
        {
            group.Variant.ShouldBe(ButtonGroupVariant.Outline);
            group.Size.ShouldBe(ControlSize.Medium);
            group.Bounds.Height.ShouldBe(44, 0.5);
            Segment(group, 0).Bounds.Height.ShouldBe(42, 0.5);
            Segment(group, 0).Classes.ShouldNotContain(ButtonGroupItem.JoinedPseudoClass);
            Segment(group, 1).Classes.ShouldContain(ButtonGroupItem.JoinedPseudoClass);
            Chevron(group).IsVisible.ShouldBeFalse();
            var peer = Peer(group);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Document actions");
            // Share reserves the width of Copied.
            var share = Segment(group, 0);
            var before = share.Bounds.Width;
            share.Label = "Copied";
            Dispatcher.UIThread.RunJobs();
            share.Bounds.Width.ShouldBe(before, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Small_Segments_Are_34px()
    {
        var group = Create();
        group.Size = ControlSize.Small;
        var window = Show(group);
        try
        {
            group.Bounds.Height.ShouldBe(36, 0.5);
            Segment(group, 1).Bounds.Height.ShouldBe(34, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Rove_And_Wrap_Including_Disabled_Segments()
    {
        var group = Create(CreateMenu());
        var window = Show(group);
        try
        {
            Segment(group, 0).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            group.ActiveSegment.ShouldBe(Segment(group, 0));
            var highlight = Part<Primitives.GlidingIndicator>(group, ButtonGroup.HighlightPart);
            highlight.HasTarget.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Segment(group, 1).IsFocused.ShouldBeTrue();
            // The disabled segment stays reachable (aria-disabled) and still shows the keyboard position.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Segment(group, 2).IsFocused.ShouldBeTrue();
            highlight.HasTarget.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Segment(group, 0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Segment(group, 0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
            group.IsOpen().ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Segment_Is_A_Tab_Stop()
    {
        var group = Create(CreateMenu());
        var before = new Avalonia.Controls.Button { Content = "Before" };
        var window = TestHelpers.Show(new StackPanel { Width = 520, Children = { before, group } });
        try
        {
            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Segment(group, 0).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Segment(group, 1).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Segment(group, 2).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_To_Left_Mirrors_The_Arrows()
    {
        var group = Create();
        group.FlowDirection = FlowDirection.RightToLeft;
        var window = Show(group);
        try
        {
            Segment(group, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Segment(group, 1).IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Group_Uses_Up_And_Down_And_The_Chevron_Opens_With_ArrowDown()
    {
        var menu = CreateMenu();
        var group = Create(menu);
        group.Orientation = Orientation.Vertical;
        var window = Show(group);
        try
        {
            group.Classes.ShouldContain(ButtonGroup.VerticalPseudoClass);
            menu.Alignment.ShouldBe(MenuAlignment.Start);
            Segment(group, 0).Bounds.Width.ShouldBe(Segment(group, 1).Bounds.Width, 0.5);
            Segment(group, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Segment(group, 1).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
            // ArrowUp on the chevron moves back; ArrowDown opens the menu on the first row.
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Segment(group, 2).IsFocused.ShouldBeTrue();
            menu.IsOpen.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Chevron(group).IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.HighlightedEntry!.Header.ShouldBe("Pin");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Segment_Is_Focusable_But_Does_Not_Act()
    {
        var group = Create();
        var invoked = new List<string>();
        group.ActionInvoked += (_, e) => invoked.Add(e.Item.Label);
        var window = Show(group);
        try
        {
            var archive = Segment(group, 2);
            archive.Focusable.ShouldBeTrue();
            archive.IsEffectivelyEnabled.ShouldBeTrue();
            archive.Classes.ShouldContain(ButtonGroupItem.InertPseudoClass);
            Peer(archive).IsEnabled().ShouldBeFalse();
            archive.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Segment(group, 1).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBe(["Duplicate"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Highlight_Follows_Hover_Skips_Disabled_And_Quiets_Dividers()
    {
        var group = Create();
        var window = Show(group);
        try
        {
            var highlight = Part<Primitives.GlidingIndicator>(group, ButtonGroup.HighlightPart);
            var duplicate = Segment(group, 1);
            window.MouseMove(Center(duplicate, window));
            Dispatcher.UIThread.RunJobs();
            group.ActiveSegment.ShouldBe(duplicate);
            highlight.HasTarget.ShouldBeTrue();
            var expected = new Rect(duplicate.TranslatePoint(default, highlight)!.Value, duplicate.Bounds.Size).Deflate(3);
            highlight.CurrentRect.ShouldBe(expected);
            // The dividers on both sides of the active segment fade.
            duplicate.Classes.ShouldContain(ButtonGroupItem.QuietPseudoClass);
            Segment(group, 2).Classes.ShouldContain(ButtonGroupItem.QuietPseudoClass);
            Segment(group, 0).Classes.ShouldNotContain(ButtonGroupItem.QuietPseudoClass);

            window.MouseMove(Center(Segment(group, 2), window));
            Dispatcher.UIThread.RunJobs();
            highlight.HasTarget.ShouldBeFalse();

            window.MouseDown(Center(duplicate, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Classes.ShouldContain(ButtonGroup.PressingPseudoClass);
            window.MouseUp(Center(duplicate, window), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Classes.ShouldNotContain(ButtonGroup.PressingPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chevron_Menu_Opens_Raises_MenuItemClicked_And_Never_Scales()
    {
        var menu = CreateMenu();
        var group = Create(menu);
        MenuEntryClickedEventArgs? clicked = null;
        group.MenuItemClicked += (_, e) => clicked = e;
        var window = Show(group);
        try
        {
            var chevron = Chevron(group);
            chevron.IsVisible.ShouldBeTrue();
            chevron.IsPressScaleEnabled.ShouldBeFalse();
            chevron.Bounds.Width.ShouldBe(38, 0.5);
            AutomationProperties.GetName(chevron).ShouldBe("More actions");
            Segment(group, 2).Classes.ShouldContain(ButtonGroupItem.JoinedPseudoClass);
            chevron.Classes.ShouldContain("joined");
            menu.AnchorGap.ShouldBe(6);
            menu.Alignment.ShouldBe(MenuAlignment.End);
            menu.MinWidth.ShouldBe(208);

            chevron.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            group.Classes.ShouldContain(ButtonGroup.MenuOpenPseudoClass);
            group.ActiveSegment.ShouldBe(chevron);
            menu.HighlightedEntry!.Header.ShouldBe("Pin");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clicked!.Header.ShouldBe("Pin");
            menu.IsOpen.ShouldBeFalse();
            chevron.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Label_Change_Right_After_A_Press_Is_Announced()
    {
        var group = Create();
        var window = Show(group);
        try
        {
            var share = Segment(group, 0);
            share.Click += (_, _) => share.Label = "Copied";
            share.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Part<TextBlock>(group, ButtonGroup.StatusPart).Text.ShouldStartWith("Copied");
            AutomationProperties.GetName(share).ShouldBe("Copied");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Collapse_To_Icons_When_The_Row_Does_Not_Fit()
    {
        var group = Create();
        var host = new Border { Width = 200, Child = group };
        var window = TestHelpers.Show(host);
        try
        {
            group.AreLabelsCollapsed.ShouldBeTrue();
            group.Classes.ShouldContain(ButtonGroup.CompactPseudoClass);
            Segment(group, 1).Bounds.Width.ShouldBe(42, 0.5);
            Tooltip.GetTip(Segment(group, 1)).ShouldBe("Duplicate");
            AutomationProperties.GetName(Segment(group, 1)).ShouldBe("Duplicate");
            group.Bounds.Width.ShouldBeLessThanOrEqualTo(200);

            host.Width = 600;
            Dispatcher.UIThread.RunJobs();
            group.AreLabelsCollapsed.ShouldBeFalse();
            Segment(group, 1).Bounds.Width.ShouldBeGreaterThan(42);
            Tooltip.GetTip(Segment(group, 1)).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Generates_Segments_And_Reports_The_Data()
    {
        var group = new ButtonGroup { Label = "Zoom", ItemsSource = new[] { "Fit", "50%", "100%" } };
        object? data = null;
        group.ActionInvoked += (_, e) => data = e.Data;
        var window = Show(group);
        try
        {
            Segment(group, 1).Label.ShouldBe("50%");
            Segment(group, 2).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            data.ShouldBe("100%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Segment_Keeps_The_Label_As_Its_Name()
    {
        var zoom = new ButtonGroupItem { Label = "Reset zoom", Value = "100%" };
        var group = new ButtonGroup
        {
            Items =
            {
                new ButtonGroupItem { Label = "Zoom out", Icon = ArcIcons.ZoomOut, IsIconOnly = true },
                zoom,
                new ButtonGroupItem { Label = "Zoom in", Icon = ArcIcons.ZoomIn, IsIconOnly = true }
            }
        };
        var window = Show(group);
        try
        {
            zoom.Classes.ShouldContain(ButtonGroupItem.ValuePseudoClass);
            AutomationProperties.GetName(zoom).ShouldBe("Reset zoom");
            Segment(group, 0).Bounds.Width.ShouldBe(42, 0.5);
            Tooltip.GetTip(Segment(group, 0)).ShouldBe("Zoom out");
            var phase = zoom.Phase;
            zoom.Value = "110%";
            Dispatcher.UIThread.RunJobs();
            // A live value updates in place: same phase, new text.
            zoom.Phase.ShouldBeSameAs(phase);
            TestHelpers.Descendant<TextBlock>(zoom.Phase!).Text.ShouldBe("110%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Solid_Variant_Uses_The_Primary_Fill()
    {
        var group = Create();
        group.Variant = ButtonGroupVariant.Solid;
        var window = Show(group);
        try
        {
            group.Classes.ShouldContain(ButtonGroup.SolidPseudoClass);
            TestHelpers.ColorOf(group.Background).ShouldBe(TestHelpers.ColorOf(group.BorderBrush));
            TestHelpers.ColorOf(Segment(group, 0).Foreground).ShouldNotBe(TestHelpers.ColorOf(group.Background));
            Segment(group, 0).Classes.ShouldContain(ButtonGroupItem.SolidPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Group_Dims_And_Has_No_Stops()
    {
        var group = Create();
        group.IsEnabled = false;
        var window = Show(group);
        try
        {
            group.Opacity.ShouldBe(0.52, 0.001);
            Segment(group, 0).IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class ButtonGroupTestExtensions
{
    public static bool IsOpen(this ButtonGroup group) => group.Menu?.IsOpen == true;
}

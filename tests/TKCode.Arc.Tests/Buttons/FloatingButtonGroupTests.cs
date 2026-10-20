using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Buttons;

public class FloatingButtonGroupTests
{
    private static FloatingButtonGroup Board(out FloatingButtonGroupItem undo, out FloatingButtonGroupItem redo, out FloatingButtonGroupItem share,
                                             out FloatingButtonGroupItem quote)
    {
        undo = new FloatingButtonGroupItem { Label = "Undo", Icon = ArcIcons.Undo2, IsIconOnly = true, Shortcut = "⌘Z" };
        redo = new FloatingButtonGroupItem { Label = "Redo", Icon = ArcIcons.Redo2, IsIconOnly = true };
        share = new FloatingButtonGroupItem { Label = "Share", Icon = ArcIcons.Share2, ReserveLabels = ["Copied to clipboard"] };
        quote = new FloatingButtonGroupItem { Label = "Quote", IsEnabled = false };
        return new FloatingButtonGroup { Label = "Board actions", Items = { undo, redo, new FloatingButtonGroupSeparator(), share, quote } };
    }

    [AvaloniaFact]
    public void Tray_And_Buttons_Use_Arc_Metrics()
    {
        var group = Board(out var undo, out _, out var share, out _);
        var window = TestHelpers.Show(group);
        try
        {
            group.Padding.ShouldBe(new Thickness(4));
            group.CornerRadius.ShouldBe(new CornerRadius(18));
            undo.Bounds.Width.ShouldBe(36);
            undo.Bounds.Height.ShouldBe(36);
            share.Bounds.Height.ShouldBe(36);
            share.Padding.Left.ShouldBe(10);
            var separator = group.GetRealizedContainers().OfType<FloatingButtonGroupSeparator>().Single();
            separator.Bounds.Width.ShouldBe(1);
            separator.Bounds.Height.ShouldBe(18);
            Peer(group).GetAutomationControlType().ShouldBe(AutomationControlType.ToolBar);
            Peer(separator).GetAutomationControlType().ShouldBe(AutomationControlType.Separator);
            AutomationProperties.GetName(group).ShouldBe("Board actions");

            group.Size = ControlSize.Small;
            Dispatcher.UIThread.RunJobs();
            undo.Bounds.Width.ShouldBe(30);
            group.Padding.ShouldBe(new Thickness(3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Icon_Only_And_Shortcut_Items_Get_Tooltips()
    {
        var group = Board(out var undo, out var redo, out var share, out _);
        var window = TestHelpers.Show(group);
        try
        {
            undo.Classes.ShouldContain(":icon-only");
            Tooltip.GetTip(undo).ShouldBeOfType<StackPanel>();
            Tooltip.GetTip(redo).ShouldBe("Redo");
            Tooltip.GetSide(redo).ShouldBe(TooltipSide.Top);
            Tooltip.GetTip(share).ShouldBeNull();
            AutomationProperties.GetName(undo).ShouldBe("Undo");
            AutomationProperties.GetHelpText(undo).ShouldBe("⌘Z");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reserved_Labels_Keep_The_Width()
    {
        var group = Board(out _, out _, out var share, out _);
        var window = TestHelpers.Show(group);
        try
        {
            var width = share.Bounds.Width;
            share.Label = "Copied to clipboard";
            Dispatcher.UIThread.RunJobs();
            share.Bounds.Width.ShouldBe(width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Rove_Wrap_And_Skip_Disabled_Items_With_The_Highlight()
    {
        var group = Board(out var undo, out var redo, out var share, out var quote);
        var window = TestHelpers.Show(group);
        try
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            undo.IsFocused.ShouldBeTrue();
            group.ActiveItem.ShouldBe(undo);
            var highlight = Part<GlidingIndicator>(group, FloatingButtonGroup.HighlightPart);
            highlight.HasTarget.ShouldBeTrue();
            highlight.CurrentRect.Width.ShouldBe(36, 0.5);

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            redo.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            share.IsFocused.ShouldBeTrue();
            group.ActiveItem.ShouldBe(share);
            Dispatcher.UIThread.RunJobs();
            highlight.CurrentRect.Width.ShouldBe(share.Bounds.Width, 0.5);
            // Quote is disabled: the next stop wraps to Undo.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            undo.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            share.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            undo.IsFocused.ShouldBeTrue();
            quote.IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Running_An_Item_Raises_ActionInvoked_After_Its_Click()
    {
        var order = new List<string>();
        var group = Board(out var undo, out _, out _, out _);
        undo.Click += (_, _) => order.Add("click");
        group.ActionInvoked += (_, e) => order.Add($"action:{e.Item.Label}");
        var window = TestHelpers.Show(group);
        try
        {
            undo.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            order.ShouldBe(["click", "action:Undo"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Hover_Moves_The_Highlight_And_Leaving_Hides_It()
    {
        var group = Board(out var undo, out var redo, out _, out _);
        var outside = new Border { Height = 40 };
        var panel = new StackPanel { Children = { group, outside } };
        var window = TestHelpers.Show(panel);
        try
        {
            var highlight = Part<GlidingIndicator>(group, FloatingButtonGroup.HighlightPart);
            window.MouseMove(redo.TranslatePoint(new Point(10, 10), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            group.ActiveItem.ShouldBe(redo);
            redo.Classes.ShouldContain(":active");
            highlight.HasTarget.ShouldBeTrue();
            window.MouseMove(outside.TranslatePoint(new Point(10, 20), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            group.ActiveItem.ShouldBeNull();
            highlight.HasTarget.ShouldBeFalse();
            undo.Classes.ShouldNotContain(":active");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Source_Data_Becomes_Buttons_And_Vertical_Uses_Up_Down()
    {
        var group = new FloatingButtonGroup { Orientation = Orientation.Vertical, ItemsSource = new[] { "Fit", "50%", "100%" } };
        object? data = null;
        group.ActionInvoked += (_, e) => data = e.Data;
        var window = TestHelpers.Show(group);
        try
        {
            var buttons = group.GetRealizedContainers().OfType<FloatingButtonGroupItem>().ToList();
            buttons.Select(b => b.Label).ShouldBe(["Fit", "50%", "100%"]);
            buttons[0].Classes.ShouldContain(":vertical");
            buttons[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            buttons[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            data.ShouldBe("50%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checked_Items_Report_Toggle_State()
    {
        var present = new FloatingButtonGroupItem { Label = "Present", IsChecked = true };
        var group = new FloatingButtonGroup { Items = { present } };
        var window = TestHelpers.Show(group);
        try
        {
            present.Classes.ShouldContain(":checked");
            Peer(present).GetProvider<Avalonia.Automation.Provider.IToggleProvider>()!.ToggleState.ShouldBe(Avalonia.Automation.Provider.ToggleState.On);
        }
        finally
        {
            window.Close();
        }
    }
}

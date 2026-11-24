using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class TooltipTests
{
    [AvaloniaFact]
    public void Programmatic_Open_Shows_The_Bubble_With_Its_Text()
    {
        var target = new Button { Content = "Share" };
        Tooltip.SetTip(target, "Copy link");
        var window = TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(60), Children = { target } });
        try
        {
            Tooltip.SetIsOpen(target, true);
            Dispatcher.UIThread.RunJobs();
            var popup = Tooltip.GetPopup(target);
            popup.ShouldNotBeNull();
            popup!.IsOpen.ShouldBeTrue();
            ((Tooltip)popup.Surface!).Content.ShouldBe("Copy link");
            Tooltip.SetTip(target, "Copied");
            Dispatcher.UIThread.RunJobs();
            ((Tooltip)popup.Surface!).Content.ShouldBe("Copied");
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Control_As_Tip_Is_Shown_Itself_Not_Its_Type_Name()
    {
        var target = new Button { Content = "Share" };
        var tip = new TextBlock { Text = "Copy link" };
        Tooltip.SetTip(target, tip);
        var window = TestHelpers.Show(new StackPanel { Margin = new Avalonia.Thickness(60), Children = { target } });
        try
        {
            Tooltip.SetIsOpen(target, true);
            Dispatcher.UIThread.RunJobs();
            var bubble = (Tooltip)Tooltip.GetPopup(target)!.Surface!;
            var shown = bubble.GetVisualDescendants().OfType<TextBlock>().ToList();
            shown.ShouldContain(tip);
            shown.ShouldNotContain(t => t.Text == typeof(TextBlock).ToString());
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    private static Point Center(Control target, Window window)
        => target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

    private static bool OpensSoon(Control target) => TestHelpers.PumpUntil(() => Tooltip.GetPopup(target)?.IsOpen == true, 3000);

    [AvaloniaFact]
    public void ShowOnDisabled_Shows_The_Tip_Over_A_Disabled_Control()
    {
        // Issue #110: a disabled button could not say why it cannot be used.
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Add a title first");
        Tooltip.SetShowOnDisabled(target, true);
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeTrue();
            ((Tooltip)Tooltip.GetPopup(target)!.Surface!).Content.ShouldBe("Add a title first");

            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetPopup(target)!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_ShowOnDisabled_A_Disabled_Control_Shows_No_Tip()
    {
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Add a title first");
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            window.MouseMove(new Point(5, 5));
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Enabled_Control_With_ShowOnDisabled_Uses_Its_Own_Pointer_Events()
    {
        var target = new Button { Content = "Publish", IsEnabled = false };
        Tooltip.SetTip(target, "Publish now");
        Tooltip.SetShowOnDisabled(target, true);
        Tooltip.SetShowDelay(target, TimeSpan.Zero);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            target.IsEnabled = true;
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(5, 5));
            window.MouseMove(Center(target, window));
            OpensSoon(target).ShouldBeTrue();
            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            Tooltip.GetPopup(target)!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TipModel
    {
        public string Title { get; set; } = "";
    }

    private static Tooltip OpenBubble(Control target)
    {
        Tooltip.SetIsOpen(target, true);
        Dispatcher.UIThread.RunJobs();
        var popup = Tooltip.GetPopup(target);
        popup.ShouldNotBeNull();
        popup!.IsOpen.ShouldBeTrue();
        var bubble = (Tooltip)popup.Surface!;
        bubble.UpdateLayout();
        return bubble;
    }

    [AvaloniaFact]
    public void A_Control_Tip_Inherits_The_Triggers_DataContext()
    {
        // Issue #190: the tip's bindings resolved against the tip itself (the text template made the presenter set the
        // child's DataContext to the content), so {Binding Title} stayed empty.
        var model = new TipModel { Title = "Release notes" };
        var title = new TextBlock();
        title.Bind(TextBlock.TextProperty, new Binding(nameof(TipModel.Title)));
        var tip = new StackPanel { Children = { title } };
        var target = new Button { Content = "Share", DataContext = model };
        Tooltip.SetTip(target, tip);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            OpenBubble(target);
            tip.DataContext.ShouldBeSameAs(model);
            title.Text.ShouldBe("Release notes");

            var next = new TipModel { Title = "Changelog" };
            target.DataContext = next;
            Dispatcher.UIThread.RunJobs();
            title.Text.ShouldBe("Changelog");
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Control_Tip_Set_While_Open_Inherits_The_DataContext_After_A_Text_Tip()
    {
        var model = new TipModel { Title = "Release notes" };
        var title = new TextBlock();
        title.Bind(TextBlock.TextProperty, new Binding(nameof(TipModel.Title)));
        var target = new Button { Content = "Share", DataContext = model };
        Tooltip.SetTip(target, "Plain text");
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            var bubble = OpenBubble(target);
            bubble.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Plain text");
            Tooltip.SetTip(target, title);
            Dispatcher.UIThread.RunJobs();
            title.DataContext.ShouldBeSameAs(model);
            title.Text.ShouldBe("Release notes");
            Tooltip.SetTip(target, "Back to text");
            Dispatcher.UIThread.RunJobs();
            bubble.UpdateLayout();
            bubble.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Back to text");
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    private static Color ColorOf(IBrush? brush) => brush.ShouldBeAssignableTo<ISolidColorBrush>()!.Color;

    [AvaloniaFact]
    public void Host_Descendant_TextBlock_Styles_Do_Not_Recolor_The_Tip_Text()
    {
        // Issue #190: the popup is a logical child of its trigger, so `Button.my-btn TextBlock` reached the bubble's text.
        var target = new Button { Content = "Share", Classes = { "my-btn" } };
        Tooltip.SetTip(target, "Copy");
        var host = new StackPanel { Margin = new Thickness(60), Children = { target } };
        host.Styles.Add(new Style(x => x.OfType<Button>().Class("my-btn").Descendant().OfType<TextBlock>())
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.Black) }
        });
        var window = TestHelpers.Show(host);
        try
        {
            var bubble = OpenBubble(target);
            var text = bubble.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Copy");
            ColorOf(text.Foreground).ShouldBe(ColorOf(bubble.Foreground));
            ColorOf(text.Foreground).ShouldNotBe(Colors.Black);
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Global_TextBlock_Style_Does_Not_Recolor_The_Tip_Text()
    {
        // Issue #190: an app theme's global TextBlock foreground (FluentTheme-like) turned the text dark on the dark bubble.
        var global = new Style(x => x.OfType<TextBlock>())
        {
            Setters = { new Setter(TextBlock.ForegroundProperty, Brushes.Black) }
        };
        Application.Current!.Styles.Add(global);
        var target = new Button { Content = "Share" };
        Tooltip.SetTip(target, "Copy");
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(60), Children = { target } });
        try
        {
            var bubble = OpenBubble(target);
            var text = bubble.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Copy");
            ColorOf(text.Foreground).ShouldBe(ColorOf(bubble.Foreground));
            ColorOf(text.Foreground).ShouldNotBe(Colors.Black);

            // The text follows the bubble's own foreground when that changes.
            bubble.Foreground = Brushes.Orange;
            Dispatcher.UIThread.RunJobs();
            ColorOf(text.Foreground).ShouldBe(Colors.Orange);
            Tooltip.SetIsOpen(target, false);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
            Application.Current.Styles.Remove(global);
        }
    }
}

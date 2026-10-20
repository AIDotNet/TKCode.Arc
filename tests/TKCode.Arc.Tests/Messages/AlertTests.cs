using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

public class AlertTests
{
    [AvaloniaFact]
    public void Default_Alert_Shows_Info_Icon_Title_And_No_Dismiss()
    {
        var alert = new Alert { Title = "Card expires soon", Content = "Update it before Friday.", Width = 360 };
        var window = TestHelpers.Show(alert);
        try
        {
            var icon = TestHelpers.Descendant<MessagesGlyphSwap>(alert);
            icon.Data.ShouldBe(ArcIcons.Info);
            icon.Bounds.Width.ShouldBe(18, 0.5);
            var title = alert.GetVisualDescendants().OfType<MessagesTextSwap>().First(s => s.Name == "PART_Title");
            title.CurrentBlock!.Text.ShouldBe("Card expires soon");
            var description = alert.GetVisualDescendants().OfType<MessagesTextSwap>().First(s => s.Name == "PART_Description");
            description.CurrentBlock!.Text.ShouldBe("Update it before Friday.");
            description.Margin.Top.ShouldBe(4);
            var dismiss = alert.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dismiss");
            dismiss.IsVisible.ShouldBeFalse();
            var surface = alert.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Surface");
            surface.Padding.ShouldBe(new Thickness(16));
            surface.CornerRadius.TopLeft.ShouldBe(18);
            alert.Bounds.Height.ShouldBeGreaterThan(40);
            AutomationProperties.GetLiveSetting(alert).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tone_Changes_Icon_And_Danger_Is_Assertive()
    {
        var alert = new Alert { Title = "Saved" };
        var window = TestHelpers.Show(alert);
        try
        {
            var icon = TestHelpers.Descendant<MessagesGlyphSwap>(alert);
            alert.Tone = AlertTone.Success;
            Dispatcher.UIThread.RunJobs();
            icon.Data.ShouldBe(ArcIcons.Check);
            alert.Tone = AlertTone.Danger;
            Dispatcher.UIThread.RunJobs();
            icon.Data.ShouldBe(MessagesIcons.CircleX);
            TestHelpers.ColorOf(icon.Foreground).ShouldNotBe(Avalonia.Media.Colors.Transparent);
            AutomationProperties.GetLiveSetting(alert).ShouldBe(AutomationLiveSetting.Assertive);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_Button_Collapses_Then_Raises_Dismissed()
    {
        var alert = new Alert { Title = "Payment failed", Tone = AlertTone.Danger, IsDismissible = true, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(alert);
        try
        {
            var dismissed = 0;
            alert.Dismissed += (_, _) => dismissed++;
            var dismiss = alert.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dismiss");
            dismiss.IsVisible.ShouldBeTrue();
            dismiss.Bounds.Width.ShouldBe(28, 0.5);
            AutomationProperties.GetName(dismiss).ShouldBe("Dismiss: Payment failed");
            dismiss.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            alert.IsOpen.ShouldBeFalse();
            dismissed.ShouldBe(1);
            window.UpdateLayout();
            alert.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closed_Alert_Takes_No_Space_And_Reopens()
    {
        var alert = new Alert { Title = "Heads up", IsOpen = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(alert);
        try
        {
            alert.Bounds.Height.ShouldBe(0, 0.5);
            alert.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            alert.Bounds.Height.ShouldBeGreaterThan(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rich_Content_Uses_The_Presenter()
    {
        var alert = new Alert { Title = "Heads up", Content = new Button { Content = "Retry" } };
        var window = TestHelpers.Show(alert);
        try
        {
            var presenter = alert.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            presenter.IsVisible.ShouldBeTrue();
            alert.Classes.ShouldContain(":rich-content");
        }
        finally
        {
            window.Close();
        }
    }
}

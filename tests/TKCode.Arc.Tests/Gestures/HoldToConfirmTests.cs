using TKCode.Arc.Tests.Buttons;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using static TKCode.Arc.Tests.Buttons.ButtonsTestSupport;

namespace TKCode.Arc.Tests.Gestures;

public class HoldToConfirmTests
{
    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var button = new HoldToConfirm();
        var window = TestHelpers.Show(button);
        try
        {
            button.HoldDuration.ShouldBe(TimeSpan.FromMilliseconds(1200));
            button.ConfirmedLabel.ShouldBe("Done");
            button.Tone.ShouldBe(HoldToConfirmTone.Accent);
            button.Icon.ShouldBe(ArcIcons.Trash2);
            button.Bounds.Height.ShouldBe(44, 0.5);
            AutomationProperties.GetName(button).ShouldBe("Hold to delete");
            AutomationProperties.GetHelpText(button).ShouldBe("Press and hold for 1.2 seconds to confirm. With a keyboard, hold Space or Enter.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Holding_Space_Fills_And_Confirms_Once()
    {
        var confirmed = 0;
        var holding = new List<bool>();
        var button = new HoldToConfirm { Label = "Hold to delete project", ConfirmedLabel = "Deleted", HoldDuration = TimeSpan.FromMilliseconds(80) };
        button.Confirmed += (_, _) => confirmed++;
        button.HoldingChanged += (_, e) => holding.Add(e.IsHolding);
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            button.IsHolding.ShouldBeTrue();
            button.Classes.ShouldContain(":holding");
            // Auto-repeat while held does not restart the fill.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            WaitFor(() => button.IsConfirmed).ShouldBeTrue();
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            confirmed.ShouldBe(1);
            holding.ShouldBe([true, false]);
            button.Classes.ShouldContain(":confirmed");
            AutomationProperties.GetName(button).ShouldBe("Deleted");
            Peer(button).IsEnabled().ShouldBeFalse();
            button.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Letting_Go_Early_Rewinds_Without_Confirming()
    {
        var button = new HoldToConfirm { HoldDuration = TimeSpan.FromSeconds(5) };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump(60);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            button.IsHolding.ShouldBeFalse();
            button.IsConfirmed.ShouldBeFalse();
            Part<Avalonia.Controls.Border>(button, "PART_Fill").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Hold_Confirms_And_Sliding_Away_Cancels()
    {
        var button = new HoldToConfirm { HoldDuration = TimeSpan.FromMilliseconds(80) };
        var window = TestHelpers.Show(button);
        try
        {
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            button.IsHolding.ShouldBeTrue();
            window.MouseMove(new Point(center.X, center.Y + button.Bounds.Height / 2 + HoldToConfirm.CancelSlack + 10));
            Dispatcher.UIThread.RunJobs();
            button.IsHolding.ShouldBeFalse();
            window.MouseUp(center, MouseButton.Left);
            Pump(150);
            button.IsConfirmed.ShouldBeFalse();

            window.MouseDown(center, MouseButton.Left);
            WaitFor(() => button.IsConfirmed).ShouldBeTrue();
            window.MouseUp(center, MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Does_Not_Confirm_And_Reset_Restores_The_Label()
    {
        var button = new HoldToConfirm { Label = "Hold to archive", ConfirmedLabel = "Archived" };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            button.IsConfirmed.ShouldBeFalse();

            button.IsConfirmed = true;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(button).ShouldBe("Archived");
            button.IsConfirmed = false;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(button).ShouldBe("Hold to archive");
            button.Classes.ShouldNotContain(":confirmed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tones_Change_Ink()
    {
        var accent = new HoldToConfirm();
        var danger = new HoldToConfirm { Tone = HoldToConfirmTone.Danger };
        var panel = new Avalonia.Controls.StackPanel { Children = { accent, danger } };
        var window = TestHelpers.Show(panel);
        try
        {
            TestHelpers.ColorOf(accent.Foreground).ShouldBe(ArcTokens.GetColor(accent, DesignTokens.ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(danger.Foreground).ShouldBe(ArcTokens.GetColor(danger, DesignTokens.ArcDesignTokenKind.Danger));
        }
        finally
        {
            window.Close();
        }
    }
}

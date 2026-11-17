using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The key cap of Prompt studio: the platform's run shortcut text, the cap's look (border, radius, monospace 11 / 15.4,
/// secondary text) and the muted variant on the Run button, and what screen readers get.
/// </summary>
public class PromptKbdTests
{
    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void The_Run_Shortcut_Reads_Command_Return_On_Mac_And_Ctrl_Return_Elsewhere()
    {
        var saved = PromptKbd.PlatformOverride;
        try
        {
            PromptKbd.PlatformOverride = true;
            PromptKbd.IsMacPlatform.ShouldBeTrue();
            PromptKbd.RunShortcutText.ShouldBe("⌘↵");
            PromptKbd.ModifierName.ShouldBe("Cmd");
            PromptKbd.RunAcceleratorKey.ShouldBe("Cmd+Enter");

            PromptKbd.PlatformOverride = false;
            PromptKbd.IsMacPlatform.ShouldBeFalse();
            PromptKbd.RunShortcutText.ShouldBe("Ctrl ↵");
            PromptKbd.ModifierName.ShouldBe("Ctrl");
            PromptKbd.RunAcceleratorKey.ShouldBe("Ctrl+Enter");

            PromptKbd.PlatformOverride = null;
            PromptKbd.IsMacPlatform.ShouldBe(OperatingSystem.IsMacOS() || OperatingSystem.IsIOS());
        }
        finally
        {
            PromptKbd.PlatformOverride = saved;
        }
    }

    [AvaloniaFact]
    public void A_Cap_Is_Monospace_11_In_A_One_Pixel_Border_With_Radius_5()
    {
        var kbd = new PromptKbd { Text = "{{" };
        var window = Show(kbd);
        try
        {
            var cap = TestHelpers.Descendant<Border>(kbd, "PART_Cap");
            cap.BorderThickness.ShouldBe(new Thickness(1));
            cap.CornerRadius.ShouldBe(new CornerRadius(5));
            cap.Padding.ShouldBe(new Thickness(4, 0));
            TestHelpers.ColorOf(cap.BorderBrush).ShouldBe(ArcTokens.GetColor(kbd, ArcDesignTokenKind.Border));

            var text = TestHelpers.Descendant<TextBlock>(kbd, "PART_Text");
            text.Text.ShouldBe("{{");
            text.FontSize.ShouldBe(11);
            text.LineHeight.ShouldBe(15.4);
            text.FontFamily.ShouldBe(ArcTokens.Get<FontFamily>(kbd, ArcDesignTokenKind.MonoFontFamily));
            TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(kbd, ArcDesignTokenKind.TextSecondary));
            // 15.4 line + the two border pixels: Arc's 17.4 (laid out to whole pixels).
            kbd.DesiredSize.Height.ShouldBe(17.4, 0.61);
            kbd.Opacity.ShouldBe(1);
            kbd.Classes.Contains(PromptKbd.MutedPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Muted_Cap_Has_No_Border_And_Takes_The_Owners_Color_At_62_Percent()
    {
        var kbd = new PromptKbd { Text = "⌘↵", IsMuted = true, Foreground = Brushes.White };
        var window = Show(kbd);
        try
        {
            kbd.Classes.Contains(PromptKbd.MutedPseudoClass).ShouldBeTrue();
            var cap = TestHelpers.Descendant<Border>(kbd, "PART_Cap");
            cap.BorderThickness.ShouldBe(new Thickness(0));
            cap.Padding.ShouldBe(new Thickness(0));
            kbd.Opacity.ShouldBe(0.62);
            var text = TestHelpers.Descendant<TextBlock>(kbd, "PART_Text");
            text.FontSize.ShouldBe(12);
            text.LineHeight.ShouldBe(16.8);
            text.LetterSpacing.ShouldBe(0.24);
            text.FontFamily.ShouldBe(ArcTokens.Get<FontFamily>(kbd, ArcDesignTokenKind.BodyFontFamily));
            TestHelpers.ColorOf(text.Foreground).ShouldBe(Colors.White);

            // Back to a plain cap.
            kbd.IsMuted = false;
            window.UpdateLayout();
            kbd.Classes.Contains(PromptKbd.MutedPseudoClass).ShouldBeFalse();
            cap.BorderThickness.ShouldBe(new Thickness(1));
            text.LineHeight.ShouldBe(15.4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_The_Cap_As_One_Piece_Of_Text()
    {
        var kbd = new PromptKbd { Text = "{{" };
        var window = Show(kbd);
        try
        {
            kbd.Focusable.ShouldBeFalse();
            var peer = ControlAutomationPeer.CreatePeerForElement(kbd);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.GetName().ShouldBe("{{");
            peer.GetChildren().ShouldBeEmpty();
            kbd.Text = "Ctrl ↵";
            peer.GetName().ShouldBe("Ctrl ↵");
            AutomationProperties.GetName(kbd).ShouldBe("Ctrl ↵");
        }
        finally
        {
            window.Close();
        }
    }
}

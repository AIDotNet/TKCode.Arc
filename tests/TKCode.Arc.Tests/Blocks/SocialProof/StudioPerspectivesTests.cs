using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.SocialProof;

public class StudioPerspectivesTests
{
    private static Window Show(Control control, double width = 1000)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Show_The_First_Sample_Quote()
    {
        var stage = new StudioPerspectives();
        var window = Show(stage);
        try
        {
            stage.EffectiveQuotes.Count.ShouldBe(4);
            stage.SelectedIndex.ShouldBe(0);
            stage.CurrentQuote!.Name.ShouldBe("Amara Lindqvist");
            stage.PositionText.ShouldBe("01 / 04");
            stage.ProgressBar!.Count.ShouldBe(4);
            stage.QuoteSwap!.Content.ShouldBeOfType<StudioPerspectivesQuoteView>().Text.ShouldStartWith("We stopped arguing");
            stage.IsClockRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Previous_Wrap_And_Announce()
    {
        var stage = new StudioPerspectives();
        var changes = new List<StudioPerspectivesChangedEventArgs>();
        stage.QuoteChanged += (_, e) => changes.Add(e);
        var window = Show(stage);
        try
        {
            stage.Previous();
            stage.SelectedIndex.ShouldBe(3);
            stage.PositionText.ShouldBe("04 / 04");
            stage.Next();
            stage.SelectedIndex.ShouldBe(0);
            stage.Announcer!.Announcement.ShouldBe("Quote 1 of 4, Amara Lindqvist, Creative Director · Fieldnote Studio");
            changes.Select(c => c.Index).ShouldBe([3, 0]);
            changes.ShouldAllBe(c => !c.IsAutomatic);
            stage.ProgressBar!.Index.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_While_Focus_Is_Inside()
    {
        var stage = new StudioPerspectives();
        var window = Show(stage);
        try
        {
            TestHelpers.Descendant<Button>(stage, StudioPerspectives.NextPart).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            stage.IsClockRunning.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            stage.SelectedIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            stage.SelectedIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            stage.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Stops_The_Clock_And_Renames_The_Button()
    {
        var stage = new StudioPerspectives();
        var window = Show(stage);
        try
        {
            var pause = TestHelpers.Descendant<Button>(stage, StudioPerspectives.PausePart);
            Avalonia.Automation.AutomationProperties.GetName(pause).ShouldBe("Pause quotes");
            stage.Pause();
            stage.IsPaused.ShouldBeTrue();
            stage.IsClockRunning.ShouldBeFalse();
            stage.Classes.Contains(":paused").ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(pause).ShouldBe("Play quotes");
            stage.Play();
            stage.IsClockRunning.ShouldBeTrue();
            stage.IsAutoAdvance = false;
            stage.IsClockRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Quotes_Replace_The_Samples_And_One_Quote_Hides_The_Controls()
    {
        var stage = new StudioPerspectives();
        stage.Quotes.Add(new StudioPerspectivesQuote { Text = "Only one.", Name = "Solo Voice", Company = "Alone Inc" });
        var window = Show(stage);
        try
        {
            stage.EffectiveQuotes.Count.ShouldBe(1);
            stage.Classes.Contains(":single").ShouldBeTrue();
            stage.CurrentQuote!.Byline.ShouldBe("Alone Inc");
            stage.IsClockRunning.ShouldBeFalse();
            stage.SelectedIndex = 5;
            stage.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pressed_Progress_Bar_Jumps_To_Its_Quote()
    {
        var stage = new StudioPerspectives();
        var window = Show(stage);
        try
        {
            var bar = stage.ProgressBar!;
            var index = bar.SegmentAt(bar.Bounds.Width * 0.6);
            index.ShouldBe(2);
            bar.SegmentAt(-5).ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }
}

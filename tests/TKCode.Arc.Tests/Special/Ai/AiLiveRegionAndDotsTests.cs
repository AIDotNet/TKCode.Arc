using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The shared live region (<see cref="ExplainerAnnouncer"/>) and typing dots (<see cref="AiTypingDots"/>).</summary>
public class AiLiveRegionAndDotsTests
{
    [AvaloniaFact]
    public void The_Live_Region_Is_A_Polite_Text_Element_That_Reports_Every_Announcement()
    {
        var region = new ExplainerAnnouncer();
        var window = TestHelpers.Show(region);
        try
        {
            AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
            var peer = ControlAutomationPeer.CreatePeerForElement(region);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.IsContentElement().ShouldBeTrue();
            var names = new List<string?>();
            peer.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationElementIdentifiers.NameProperty)
                {
                    names.Add(e.NewValue as string);
                }
            };

            region.Announce("Saved");
            region.Announce("Saved");
            region.Announce("Sent");
            region.Announce(null);
            region.Announce(string.Empty);

            // The platforms announce on this event; the same text twice is reported twice (the second with a marker).
            names.Count.ShouldBe(3);
            names[0].ShouldBe("Saved");
            names[1]!.TrimEnd('​').ShouldBe("Saved");
            names[1].ShouldNotBe(names[0]);
            names[2].ShouldBe("Sent");
            peer.GetName().ShouldBe("Sent");
            region.Announcement.ShouldBe("Sent");
            region.History.ShouldBe(["Saved", "Saved", "Sent"]);
            region.DesiredSize.ShouldBe(default);

            region.Clear();
            region.Announcement.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Live_Region_Can_Interrupt()
    {
        var region = new ExplainerAnnouncer { IsAssertive = true };
        AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Assertive);
        region.IsAssertive = false;
        AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
    }

    [AvaloniaFact]
    public void The_Live_Region_Keeps_The_Last_64_Announcements()
    {
        var region = new ExplainerAnnouncer();
        for (var i = 0; i < 70; i++)
        {
            region.Announce("n" + i);
        }
        region.History.Count.ShouldBe(64);
        region.History[0].ShouldBe("n6");
        region.History[^1].ShouldBe("n69");
    }

    [Fact]
    public void Typing_Dots_Breathe_In_Turn_With_A_Peak_At_Thirty_Percent()
    {
        AiTypingDots.Sample(0, 1).ShouldBe((0.35, 0.0));
        AiTypingDots.Sample(0.36, 0).Opacity.ShouldBe(1, 1e-9);
        AiTypingDots.Sample(0.36, 0).Lift.ShouldBe(2, 1e-9);
        AiTypingDots.Sample(0.36 + 0.16, 1).Opacity.ShouldBe(1, 1e-9);
        AiTypingDots.Sample(0.36 + 0.32, 2, lift: 3).Lift.ShouldBe(3, 1e-9);
        // Falling by 60%, still for the rest of the period, and the same again a period later.
        AiTypingDots.Sample(0.8, 0).ShouldBe((0.35, 0.0));
        AiTypingDots.Sample(1.1, 0).ShouldBe((0.35, 0.0));
        AiTypingDots.Sample(0.18 + 1.2, 0).Opacity.ShouldBe(AiTypingDots.Sample(0.18, 0).Opacity, 1e-9);
        var rising = AiTypingDots.Sample(0.18, 0);
        rising.Opacity.ShouldBeInRange(0.36, 0.99);
        // A longer period stretches the breath.
        AiTypingDots.Sample(0.6, 0, period: 2.0).Opacity.ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void Typing_Dots_Move_Only_At_Full_Motion_And_Take_Their_Size()
    {
        var dots = new AiTypingDots { DotSize = 6, Gap = 4, Lift = 2 };
        var window = TestHelpers.Show(dots, ArcMotionLevel.Full);
        try
        {
            dots.DesiredSize.Width.ShouldBe(3 * 6 + 2 * 4);
            dots.DesiredSize.Height.ShouldBe(6 + 2);
            TestHelpers.PumpUntil(() => dots.IsBreathing).ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            dots.IsBreathing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Handoff_Dots_Keep_Their_Timing() =>
        AgentHandoffTypingDots.Sample(0.36 + AgentHandoffMotion.DotsStagger, 1)
            .ShouldBe(AiTypingDots.Sample(0.36 + AgentHandoffMotion.DotsStagger, 1, AgentHandoffMotion.DotsPeriod, AgentHandoffMotion.DotsStagger, 2));
}

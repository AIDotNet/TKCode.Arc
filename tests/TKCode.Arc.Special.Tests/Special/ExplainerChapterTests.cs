using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The DNS resolution explainer: the domain, the story through every server, stepping and layout.</summary>
public class DnsResolutionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(ManualFrameClock clock, Control card, double width = 760)
    {
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Dispatcher.UIThread.RunJobs();
    }

    // ---- DNS resolution -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Dns_Reads_The_Domain_And_Starts_Ready()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            card.Title.ShouldBe("DNS resolution");
            card.Step.ShouldBe(DnsResolutionStep.Ready);
            card.TldText.ShouldBe(".dev");
            card.ZoneText.ShouldBe("lumen-garden.dev");
            card.NameServerText.ShouldBe("ns1.lumen-garden.dev");
            card.StatusText.ShouldBe("Ready");
            card.CaptionText.ShouldBe(card.Labels.ReadyCaption(card.Domain));
            AutomationProperties.GetName(card).ShouldBe("DNS resolution");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Plays_Through_Every_Server_Then_Answers_From_The_Cache()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var steps = new List<DnsResolutionStep>();
        card.StepChanged += (_, e) => steps.Add(e.Step);
        var window = Open(clock, card);
        try
        {
            card.Play();
            Run(clock, 0.8);
            card.Step.ShouldBe(DnsResolutionStep.Query);
            card.Scene.Packet.ShouldNotBeNull().To.ShouldBe(DnsResolutionNode.Resolver);
            card.StatusText.ShouldBe("Resolving");
            Run(clock, 30);
            card.IsFinished.ShouldBeTrue();
            steps.ShouldBe([DnsResolutionStep.Query, DnsResolutionStep.Root, DnsResolutionStep.Tld, DnsResolutionStep.Authoritative,
                DnsResolutionStep.Answer, DnsResolutionStep.Cached, DnsResolutionStep.Done]);
            var scene = card.Scene;
            scene.HasCacheEntry.ShouldBeTrue();
            scene.HasClientAddress.ShouldBeTrue();
            scene.CacheFlash.ShouldBe(DnsResolutionCacheFlash.Hit);
            scene.RoundTrips.ShouldBe(1, "the cached lookup is one round trip");
            scene.ElapsedTarget.ShouldBe(DnsResolution.RoundTripMs[0]);
            scene.Log.Count.ShouldBe(12);
            scene.Log[3].Text.ShouldBe("Ask the .dev servers");
            scene.Log[7].Text.ShouldBe("203.0.113.42, keep it 300 s");
            card.CaptionText.ShouldBe(card.Labels.DoneCaption(97, 4));
            card.StatusText.ShouldBe("Resolved");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Steps_One_Chapter_At_A_Time_Under_Reduced_Motion()
    {
        var card = new DnsResolution { AutoPlay = false, IsRepeatLookupShown = false, Domain = "api.kite.io", Address = "198.51.100.9" };
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.Restart();
            card.Pause();
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Query);
            card.Scene.CacheFlash.ShouldBe(DnsResolutionCacheFlash.Miss);
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Root);
            card.Scene.RoundTrips.ShouldBe(1);
            card.Scene.Log[^1].Text.ShouldBe("Ask the .io servers");
            card.StepForward();
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Authoritative);
            card.Scene.Log[^1].Text.ShouldBe("198.51.100.9, keep it 300 s");
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Answer);
            card.Scene.ElapsedTarget.ShouldBe(97);
            card.StepForward();
            card.IsFinished.ShouldBeTrue();
            card.CaptionText.ShouldBe(card.Labels.DoneCaption(97, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Stage_Stacks_The_Servers_On_Narrow_Cards()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var window = Open(clock, card, 760);
        try
        {
            var stage = TestHelpers.Descendant<DnsResolutionStage>(card);
            stage.IsWide.ShouldBeTrue();
            stage.NodeRect(DnsResolutionNode.Root).X.ShouldBeGreaterThan(stage.NodeRect(DnsResolutionNode.Resolver).Right);
            window.Width = 380;
            Dispatcher.UIThread.RunJobs();
            stage.IsWide.ShouldBeFalse();
            stage.NodeRect(DnsResolutionNode.Root).Y.ShouldBeGreaterThan(stage.CacheRect.Bottom);
        }
        finally
        {
            window.Close();
        }
    }
}

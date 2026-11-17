using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// GenerationStats: the formats (times, speeds, tokens, prices, costs), derived speed and cost, the speed series, the copied
/// JSON, the simulation, the idle strip without data, a stream fed by the host (waiting, streaming, done, stopped, failed), the keyboard model (Details,
/// Retry, Copy), events, labels, the breakpoints and the inline variant.
/// </summary>
public class GenerationStatsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly GenerationModel Sonnet = new("Atlas 3 Pro", "anthropic", "atlas-3-pro");

    private static readonly GenerationPricing Prices = new(3, 15, 0.3);

    /// <summary>The test's own runs (invented): three settle, the last one fails once at 120 tokens.</summary>
    private static readonly IReadOnlyList<GenerationStatsRun> Runs =
    [
        Run(Sonnet, Prices, 3_682, 2_816, 284, 0, 412, 96),
        Run(new GenerationModel("Atlas 3", "anthropic", "atlas-3"), new GenerationPricing(2.5, 10, 0.25), 5_120, 4_096, 412, 768, 1_180, 132),
        Run(new GenerationModel("Atlas Mini", null, "atlas-mini"), new GenerationPricing(0.3, 2.5, 0.075), 2_048, 0, 356, 0, 286, 214),
        Run(new GenerationModel("Atlas Vision", null, "atlas-vision"), new GenerationPricing(2, 6, 0.2), 1_890, 0, 318, 0, 520, 72) with
        {
            FailAtTokens = 120,
            ErrorText = "The provider closed the stream.",
        },
    ];

    private static GenerationStatsRun Run(GenerationModel model, GenerationPricing pricing, long input, long cached, long output,
        long reasoning, double ttftMs, double peak) => new()
    {
        Model = model,
        Pricing = pricing,
        InputTokens = input,
        CachedTokens = cached,
        OutputTokens = output,
        ReasoningTokens = reasoning,
        TimeToFirstToken = TimeSpan.FromMilliseconds(ttftMs),
        PeakTokensPerSecond = peak,
    };

    private sealed class TestClock
    {
        public DateTimeOffset Now { get; set; } = T0;
    }

    private static (GenerationStats Stats, Window Window, TestClock Clock) Open(Action<GenerationStats>? setup = null, double width = 720,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var clock = new TestClock();
        var stats = new GenerationStats { Clock = () => clock.Now, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(stats);
        var window = TestHelpers.Show(stats, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (stats, window, clock);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static void Done(GenerationStats stats)
    {
        stats.Model = Sonnet;
        stats.Pricing = Prices;
        stats.StartedAt = T0;
        stats.Usage = new GenerationUsage
        {
            InputTokens = 3_682,
            CachedTokens = 2_816,
            OutputTokens = 284,
            Ttft = TimeSpan.FromMilliseconds(412),
            Duration = TimeSpan.FromMilliseconds(4_180),
        };
        stats.Status = GenerationStatus.Done;
    }

    // ── Formats ────────────────────────────────────────────────────────────

    [Fact]
    public void Latency_Reads_Milliseconds_Then_Seconds()
    {
        GenerationStatsFormat.LatencyText(412).ShouldBe("412ms");
        GenerationStatsFormat.LatencyText(0).ShouldBe("0ms");
        GenerationStatsFormat.LatencyText(999.4).ShouldBe("999ms");
        GenerationStatsFormat.LatencyText(999.6).ShouldBe("1.00s");
        GenerationStatsFormat.LatencyText(1_900).ShouldBe("1.90s");
        GenerationStatsFormat.LatencyText(4_180).ShouldBe("4.18s");
        GenerationStatsFormat.LatencyText(125_400).ShouldBe("125s");
        GenerationStatsFormat.Latency(412).ShouldBe(new GenerationNumber(412, 0, null, "ms"));
        GenerationStatsFormat.Latency(4_180).ShouldBe(new GenerationNumber(4.18, 2, null, "s"));
    }

    [Fact]
    public void Speed_Tokens_Prices_And_Costs_Are_Written_In_Tabular_Invariant_Form()
    {
        GenerationStatsFormat.SpeedText(87.6).ShouldBe("88");
        GenerationStatsFormat.Speed(87.5, " tok/s").Text.ShouldBe("88 tok/s");
        GenerationStatsFormat.TokensText(284).ShouldBe("284");
        GenerationStatsFormat.TokensText(12_840).ShouldBe("12,840");
        GenerationStatsFormat.CostText(0.0077031, "$").ShouldBe("$0.0077");
        GenerationStatsFormat.CostText(0.00005, "$").ShouldBe("$0.0001");
        GenerationStatsFormat.CostText(1.2345, "$").ShouldBe("$1.23");
        GenerationStatsFormat.CostDecimals(0.5).ShouldBe(4);
        GenerationStatsFormat.CostDecimals(2).ShouldBe(2);
        GenerationStatsFormat.PriceText(3, "$").ShouldBe("$3.00");
        GenerationStatsFormat.PriceText(0.3, "$").ShouldBe("$0.30");
        GenerationStatsFormat.PriceText(0.075, "$").ShouldBe("$0.075");
    }

    [Fact]
    public void Speed_Is_Output_Over_The_Generation_Time()
    {
        GenerationStatsFormat.DeriveSpeed(284, 412, 3_640)!.Value.ShouldBe(87.98, 0.01);
        GenerationStatsFormat.DeriveSpeed(284, null, 2_000)!.Value.ShouldBe(142, 0.001);
        GenerationStatsFormat.DeriveSpeed(null, 412, 3_640).ShouldBeNull();
        GenerationStatsFormat.DeriveSpeed(10, 400, 420).ShouldBeNull();
    }

    [Fact]
    public void Cost_Bills_Each_Row_At_Its_Rate()
    {
        var usage = new GenerationUsage { InputTokens = 3_682, CachedTokens = 2_816, OutputTokens = 284, ReasoningTokens = 100 };
        var rows = GenerationStatsFormat.CostRows(usage, Prices);
        rows.Input!.Value.ShouldBe(866 * 3 / 1e6, 1e-12);
        rows.Cached!.Value.ShouldBe(2_816 * 0.3 / 1e6, 1e-12);
        rows.Output!.Value.ShouldBe(284 * 15 / 1e6, 1e-12);
        rows.Reasoning!.Value.ShouldBe(100 * 15 / 1e6, 1e-12);
        rows.Total!.Value.ShouldBe(rows.Input.Value + rows.Cached.Value + rows.Output.Value + rows.Reasoning.Value, 1e-12);
        // Cached input defaults to the input rate.
        GenerationStatsFormat.CostRows(usage, new GenerationPricing(3, 15)).Cached!.Value.ShouldBe(2_816 * 3 / 1e6, 1e-12);
        // Without prices nothing is billed; a reported cost wins.
        GenerationStatsFormat.CostRows(usage, null).Total.ShouldBeNull();
        GenerationStatsFormat.TotalCost(usage with { CostUsd = 0.02 }, Prices).ShouldBe(0.02);
        GenerationStatsFormat.TotalTokens(usage).ShouldBe(3_682 + 284 + 100);
        GenerationStatsFormat.TotalTokens(new GenerationUsage()).ShouldBeNull();
    }

    [Fact]
    public void Speed_Series_Is_Smoothed_And_Compressed_At_Its_Cap()
    {
        GenerationStatsFormat.Smooth(null, 80).ShouldBe(80);
        GenerationStatsFormat.Smooth(80, 100).ShouldBe(87, 1e-9);
        var samples = new List<double>();
        for (var i = 0; i < GenerationStatsFormat.MaxSpeedSamples - 1; i++)
        {
            GenerationStatsFormat.AddSample(samples, i).ShouldBeFalse();
        }
        GenerationStatsFormat.AddSample(samples, 47).ShouldBeTrue();
        samples.Count.ShouldBe(24);
        samples[0].ShouldBe(0.5);
        samples[^1].ShouldBe(46.5);
    }

    [Fact]
    public void Json_Holds_The_Model_The_Usage_And_The_Times()
    {
        var usage = new GenerationUsage { InputTokens = 3_682, CachedTokens = 2_816, OutputTokens = 284, Ttft = TimeSpan.FromMilliseconds(412) };
        var json = GenerationStatsFormat.Json(Sonnet, GenerationStatus.Done, usage, Prices, 412, 4_180, 75.37, 0.0077031, null);
        json.ShouldContain("\"name\": \"Atlas 3 Pro\"");
        json.ShouldContain("\"id\": \"atlas-3-pro\"");
        json.ShouldContain("\"provider\": \"anthropic\"");
        json.ShouldContain("\"status\": \"done\"");
        json.ShouldContain("\"outputTokens\": 284");
        json.ShouldContain("\"ttftMs\": 412");
        json.ShouldContain("\"durationMs\": 4180");
        json.ShouldContain("\"tokensPerSecond\": 75.4");
        json.ShouldContain("\"cachedInput\": 0.3");
        json.ShouldNotContain("reasoningTokens");
        json.ShouldNotContain("error");
        // The provider key is written as the app gives it, and left out when there is none.
        GenerationStatsFormat.Json(new GenerationModel("Atlas 3 Pro", "my-gateway"), GenerationStatus.Done, usage, null, null, null, null, null, null)
            .ShouldContain("\"provider\": \"my-gateway\"");
        GenerationStatsFormat.Json(new GenerationModel("Atlas 3 Pro"), GenerationStatus.Done, usage, null, null, null, null, null, null)
            .ShouldNotContain("provider");
    }

    // ── Simulation ─────────────────────────────────────────────────────────

    [Fact]
    public void Simulation_Waits_Streams_In_Chunks_And_Settles()
    {
        var sim = new GenerationStatsSimulation { Runs = Runs, Loop = false };
        var changes = 0;
        sim.Changed += () => changes++;
        sim.Status.ShouldBe(GenerationStatus.Idle);
        sim.Advance(GenerationStatsSimulation.FirstDelaySeconds + 0.04);
        sim.Status.ShouldBe(GenerationStatus.Waiting);
        sim.Run.Model!.Name.ShouldBe("Atlas 3 Pro");
        sim.Advance(0.44);
        sim.Status.ShouldBe(GenerationStatus.Streaming);
        sim.Usage.Ttft!.Value.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(412);
        var seen = new List<long>();
        sim.Changed += () => seen.Add(sim.Usage.OutputTokens ?? 0);
        for (var i = 0; i < 200 && sim.Status == GenerationStatus.Streaming; i++)
        {
            sim.Advance(0.04);
        }
        sim.Status.ShouldBe(GenerationStatus.Done);
        sim.Usage.OutputTokens.ShouldBe(284);
        sim.Usage.Duration.ShouldNotBeNull();
        // Chunks of four or more tokens, never a token at a time.
        for (var i = 1; i < seen.Count - 1; i++)
        {
            (seen[i] - seen[i - 1]).ShouldBeGreaterThanOrEqualTo(4);
        }
        changes.ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Simulation_Holds_A_Settled_Run_Then_Moves_On_And_The_Last_Run_Fails_Once()
    {
        var sim = new GenerationStatsSimulation { Runs = Runs };
        sim.Settle();
        sim.Hold = true;
        sim.Advance(GenerationStatsSimulation.SettledSeconds + 1);
        sim.Index.ShouldBe(0);
        sim.Hold = false;
        sim.Advance(GenerationStatsSimulation.SettledSeconds + 0.1);
        sim.Index.ShouldBe(1);
        // Play on to the fourth run.
        for (var i = 0; i < 4000 && sim.Index != 3; i++)
        {
            sim.Advance(0.04);
        }
        sim.Run.Model!.Name.ShouldBe("Atlas Vision");
        for (var i = 0; i < 400 && sim.Status != GenerationStatus.Error; i++)
        {
            sim.Advance(0.04);
        }
        sim.Status.ShouldBe(GenerationStatus.Error);
        sim.Usage.OutputTokens.ShouldBe(120);
        sim.Error.ShouldNotBeNullOrEmpty();
        sim.Retry();
        sim.Status.ShouldBe(GenerationStatus.Waiting);
        for (var i = 0; i < 400 && sim.Status != GenerationStatus.Done; i++)
        {
            sim.Advance(0.04);
        }
        sim.Status.ShouldBe(GenerationStatus.Done);
        sim.Usage.OutputTokens.ShouldBe(318);
    }

    [Fact]
    public void A_Simulation_Without_Runs_Stays_Idle()
    {
        var sim = new GenerationStatsSimulation();
        sim.Advance(5);
        sim.Settle();
        sim.Status.ShouldBe(GenerationStatus.Idle);
        sim.Usage.OutputTokens.ShouldBeNull();
    }

    [AvaloniaFact]
    public void With_No_Status_And_No_Simulation_The_Strip_Is_Idle()
    {
        var (stats, window, _) = Open(level: ArcMotionLevel.Full);
        try
        {
            stats.IsSample.ShouldBeFalse();
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Idle);
            stats.StatusText!.Text.ShouldBe("Idle");
            stats.TtftNumber!.DisplayText.ShouldBe("–");
            stats.OutputNumber!.DisplayText.ShouldBe("–");
            stats.CostNumber!.DisplayText.ShouldBe("–");
            AutomationProperties.GetName(stats).ShouldBe("Model stats");
            stats.Step(2);
            Pump();
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Idle);
            // Nothing is simulated, so nothing says so.
            stats.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_While_On_Screen()
    {
        var sim = new GenerationStatsSimulation { Runs = Runs };
        var (stats, window, _) = Open(s => s.Simulation = sim, level: ArcMotionLevel.Full);
        try
        {
            stats.IsSample.ShouldBeTrue();
            stats.Step(GenerationStatsSimulation.FirstDelaySeconds + 0.04);
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Waiting);
            stats.StatusText!.Text.ShouldBe("Waiting");
            AutomationProperties.GetName(stats).ShouldBe("Atlas 3 Pro stats");
            for (var i = 0; i < 20 && stats.DisplayedStatus != GenerationStatus.Streaming; i++)
            {
                stats.Step(0.04);
            }
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Streaming);
            for (var i = 0; i < 40; i++)
            {
                stats.Step(0.04);
            }
            stats.OutputNumber!.Target!.Value.Value.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Status_Wins_Over_The_Simulation()
    {
        var sim = new GenerationStatsSimulation { Runs = Runs };
        var (stats, window, _) = Open(s =>
        {
            s.Simulation = sim;
            Done(s);
            s.Model = new GenerationModel("Own model");
        }, level: ArcMotionLevel.Full);
        try
        {
            stats.IsSample.ShouldBeFalse();
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Done);
            stats.OutputNumber!.DisplayText.ShouldBe("284");
            AutomationProperties.GetName(stats).ShouldBe("Own model stats");
            var before = sim.Now;
            stats.Step(2);
            sim.Now.ShouldBe(before);
            sim.Status.ShouldBe(GenerationStatus.Idle);
            // Clearing the status hands the strip back to the simulation, from its first run.
            stats.Status = null;
            stats.IsSample.ShouldBeTrue();
            stats.Step(GenerationStatsSimulation.FirstDelaySeconds + 0.04);
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Waiting);
            AutomationProperties.GetName(stats).ShouldBe("Atlas 3 Pro stats");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Simulation_To_Null_Stops_Playback()
    {
        var sim = new GenerationStatsSimulation { Runs = Runs };
        var (stats, window, _) = Open(s => s.Simulation = sim, level: ArcMotionLevel.Full);
        try
        {
            stats.Step(GenerationStatsSimulation.FirstDelaySeconds + 0.04);
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Waiting);
            stats.Simulation = null;
            stats.IsSample.ShouldBeFalse();
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Idle);
            stats.StatusText!.Text.ShouldBe("Idle");
            AutomationProperties.GetName(stats).ShouldBe("Model stats");
            var before = sim.Now;
            stats.Step(2);
            sim.Now.ShouldBe(before);
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_A_Simulation_It_Shows_A_Settled_Run_When_Motion_Is_Off()
    {
        var (stats, window, _) = Open(s => s.Simulation = new GenerationStatsSimulation { Runs = Runs });
        try
        {
            stats.IsSample.ShouldBeTrue();
            stats.DisplayedStatus.ShouldBe(GenerationStatus.Done);
            stats.StatusText!.Text!.ShouldStartWith("Done in ");
            stats.OutputNumber!.DisplayText.ShouldBe("284");
            stats.CostNumber!.DisplayText.ShouldStartWith("$0.0");
            stats.TtftNumber!.DisplayText.ShouldBe("412ms");
            AutomationProperties.GetName(stats).ShouldBe("Atlas 3 Pro stats");
            // The preview says once that it is simulated, then stays quiet.
            Pump();
            stats.Announcer!.History.ShouldBe(["Generation stats preview. The runs shown are simulated."]);
        }
        finally
        {
            window.Close();
        }
    }

    // ── A stream fed by the host ───────────────────────────────────────────

    [AvaloniaFact]
    public void Waiting_Counts_The_Time_To_First_Token_Live()
    {
        var (stats, window, clock) = Open(s =>
        {
            s.Model = Sonnet;
            s.StartedAt = T0;
            s.Status = GenerationStatus.Waiting;
        });
        try
        {
            stats.StatusText!.Text.ShouldBe("Waiting");
            stats.StatusGlyph!.Status.ShouldBe(GenerationStatus.Waiting);
            clock.Now = T0.AddMilliseconds(640);
            stats.Step(0.016);
            stats.TtftNumber!.DisplayText.ShouldBe("640ms");
            clock.Now = T0.AddMilliseconds(1_250);
            stats.Step(0.016);
            stats.TtftNumber.DisplayText.ShouldBe("1.25s");
            stats.OutputNumber!.DisplayText.ShouldBe("–");
            stats.SpeedNumber!.DisplayText.ShouldBe("–");
            AutomationProperties.GetName(stats.CellViews[0]!).ShouldBe("First token, 1.25s");
            // Live counts are never announced.
            Pump();
            stats.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streaming_Counts_Output_Cost_And_Speed_Measured_When_Chunks_Land()
    {
        var (stats, window, clock) = Open(s =>
        {
            s.Model = Sonnet;
            s.Pricing = Prices;
            s.StartedAt = T0;
            s.Status = GenerationStatus.Waiting;
        });
        try
        {
            var usage = new GenerationUsage { InputTokens = 3_682, CachedTokens = 2_816, Ttft = TimeSpan.FromMilliseconds(412), OutputTokens = 0 };
            clock.Now = T0.AddMilliseconds(412);
            stats.Usage = usage;
            stats.Status = GenerationStatus.Streaming;
            stats.TtftNumber!.DisplayText.ShouldBe("412ms");
            stats.StatusText!.Text.ShouldBe("Streaming");
            clock.Now = T0.AddMilliseconds(912);
            stats.Usage = usage with { OutputTokens = 44 };
            stats.OutputNumber!.DisplayText.ShouldBe("44");
            stats.SpeedNumber!.DisplayText.ShouldBe("88 tok/s");
            // Between chunks the speed holds still.
            clock.Now = T0.AddMilliseconds(1_200);
            stats.Step(0.16);
            stats.SpeedNumber.DisplayText.ShouldBe("88 tok/s");
            stats.DisplayedSamples.Count.ShouldBe(1);
            stats.DisplayedSamples[0].ShouldBe(88, 0.01);
            // No growth, no new reading.
            stats.Step(0.16);
            stats.DisplayedSamples.Count.ShouldBe(1);
            stats.CostNumber!.DisplayText.ShouldBe("$0.0041");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Settles_On_Exact_Values_And_Is_Announced_Once()
    {
        var (stats, window, _) = Open(Done);
        try
        {
            stats.StatusText!.Text.ShouldBe("Done in 4.18s");
            stats.StatusGlyph!.Status.ShouldBe(GenerationStatus.Done);
            stats.TtftNumber!.DisplayText.ShouldBe("412ms");
            stats.SpeedNumber!.DisplayText.ShouldBe("75 tok/s");
            stats.OutputNumber!.DisplayText.ShouldBe("284");
            stats.CostNumber!.DisplayText.ShouldBe("$0.0077");
            stats.ErrorBox!.IsExpanded.ShouldBeFalse();
            Pump();
            stats.Announcer!.Announcement.ShouldBe("Atlas 3 Pro done in 4.18s, 284 output tokens at 75 tokens per second, $0.0077");
            stats.Usage = stats.Usage! with { CostUsd = 0.0077 };
            Pump();
            stats.Announcer.History.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stopped_Reads_Its_Time_And_A_New_Request_Cuts_Back()
    {
        var (stats, window, _) = Open(s =>
        {
            s.Model = Sonnet;
            s.StartedAt = T0;
            s.Usage = new GenerationUsage { OutputTokens = 120, Ttft = TimeSpan.FromMilliseconds(400), Duration = TimeSpan.FromMilliseconds(1_900) };
            s.Status = GenerationStatus.Stopped;
        });
        try
        {
            stats.StatusText!.Text.ShouldBe("Stopped at 1.90s");
            stats.StatusGlyph!.Status.ShouldBe(GenerationStatus.Stopped);
            Pump();
            stats.Announcer!.Announcement.ShouldBe("Atlas 3 Pro stopped at 1.90s");
            // A new request: the values drop straight to the new ones.
            stats.Usage = new GenerationUsage();
            stats.Status = GenerationStatus.Waiting;
            stats.OutputNumber!.DisplayText.ShouldBe("–");
            stats.OutputNumber.IsGliding.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Request_Opens_The_Error_Row_And_Retry_Is_Reached_With_Tab()
    {
        var (stats, window, _) = Open(s =>
        {
            s.Model = Sonnet;
            s.StartedAt = T0;
            s.Usage = new GenerationUsage { OutputTokens = 120, Ttft = TimeSpan.FromMilliseconds(500) };
            s.ErrorText = "The provider closed the stream.";
            s.Status = GenerationStatus.Error;
        });
        try
        {
            var retries = 0;
            stats.RetryRequested += (_, _) => retries++;
            stats.StatusText!.Text.ShouldBe("Failed");
            stats.StatusGlyph!.Status.ShouldBe(GenerationStatus.Error);
            stats.ErrorBox!.IsExpanded.ShouldBeTrue();
            stats.Classes.Contains(":error").ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(stats, "PART_ErrorText").Text.ShouldBe("The provider closed the stream.");
            Pump();
            stats.Announcer!.Announcement.ShouldBe("Atlas 3 Pro failed: The provider closed the stream.");

            // Tab: Details, then Retry.
            Press(window, PhysicalKey.Tab);
            window.FocusManager!.GetFocusedElement().ShouldBe(stats.DetailsButton);
            Press(window, PhysicalKey.Tab);
            var retry = TestHelpers.Descendant<Button>(stats, "PART_Retry");
            window.FocusManager.GetFocusedElement().ShouldBe(retry);
            Press(window, PhysicalKey.Enter);
            retries.ShouldBe(1);

            // Without AllowsRetry the row keeps the reason only.
            stats.AllowsRetry = false;
            retry.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Details_Opens_The_Breakdown_With_Enter_And_Space_And_Tab_Reaches_Copy()
    {
        var (stats, window, _) = Open(Done);
        try
        {
            var toggles = new List<bool>();
            stats.DetailsToggled += (_, e) => toggles.Add(e.IsExpanded);
            var details = stats.DetailsButton!;
            AutomationProperties.GetName(details).ShouldBe("Show details");
            details.Text.ShouldBe("Details");
            Press(window, PhysicalKey.Tab);
            window.FocusManager!.GetFocusedElement().ShouldBe(details);
            Press(window, PhysicalKey.Enter);
            stats.IsExpanded.ShouldBeTrue();
            details.IsExpanded.ShouldBeTrue();
            AutomationProperties.GetName(details).ShouldBe("Hide details");
            stats.BreakdownBox!.IsExpanded.ShouldBeTrue();
            toggles.ShouldBe([true]);

            // The breakdown: latency and the token table.
            stats.LatencyTotalText!.Text.ShouldBe("Total 4.18s");
            TestHelpers.Descendant<TextBlock>(stats, "PART_LatencyFirst").Text.ShouldBe("First token 412ms");
            TestHelpers.Descendant<TextBlock>(stats, "PART_LatencyGeneration").Text.ShouldBe("Generation 3.77s");
            var rows = stats.TableRows;
            rows[0].Label.ShouldBe("Type");
            rows[1].Label.ShouldBe("Input");
            rows[1].Tokens.ShouldBe("866");
            rows[1].Rate.ShouldBe("$3.00 / M");
            rows[1].Cost.ShouldBe("$0.0026");
            rows[2].Tokens.ShouldBe("2,816");
            rows[2].Rate.ShouldBe("$0.30 / M");
            rows[3].Tokens.ShouldBe("284");
            rows[4].Tokens.ShouldBe("–");
            rows[5].Label.ShouldBe("Total");
            rows[5].Tokens.ShouldBe("3,966");
            rows[5].Cost.ShouldBe("$0.0077");
            rows[1].ShowsRate.ShouldBeTrue();
            AutomationProperties.GetName(rows[1]).ShouldBe("Input: Tokens 866, Rate $3.00 per million tokens, Cost $0.0026");
            stats.Copy!.Value!.ShouldContain("\"outputTokens\": 284");
            AutomationProperties.GetName(stats.Copy).ShouldBe("Copy stats as JSON");

            // Tab goes on to the copy button inside the breakdown.
            Press(window, PhysicalKey.Tab);
            window.FocusManager.GetFocusedElement().ShouldBe(stats.Copy);

            // Space on Details closes it.
            details.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            stats.IsExpanded.ShouldBeFalse();
            toggles.ShouldBe([true, false]);

            // Setting IsExpanded in code opens it without the event.
            stats.IsExpanded = true;
            stats.BreakdownBox.IsExpanded.ShouldBeTrue();
            toggles.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Prices_The_Rate_Column_Hides_And_A_Reported_Cost_Is_Shown()
    {
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.Pricing = null;
            s.Usage = s.Usage! with { CostUsd = 0.012 };
            s.IsExpanded = true;
        });
        try
        {
            stats.CostNumber!.DisplayText.ShouldBe("$0.0120");
            stats.TableRows[1].ShowsRate.ShouldBeFalse();
            stats.TableRows[1].Cost.ShouldBe("–");
            stats.TableRows[5].Cost.ShouldBe("$0.0120");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cells_Sit_In_One_Row_Above_600_And_In_A_Grid_Below_Details_Turns_Into_An_Icon_Below_440()
    {
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.IsExpanded = true;
        }, width: 720);
        try
        {
            stats.Cells!.IsGrid.ShouldBeFalse();
            var cells = stats.CellViews;
            cells[0]!.Bounds.Y.ShouldBe(cells[3]!.Bounds.Y);
            stats.DetailsButton!.IsCompact.ShouldBeFalse();

            window.Width = 520;
            Pump();
            stats.Cells.IsGrid.ShouldBeTrue();
            cells[2]!.Bounds.Y.ShouldBeGreaterThan(cells[0]!.Bounds.Y);
            cells[1]!.Bounds.X.ShouldBeGreaterThan(cells[0]!.Bounds.X);
            stats.DetailsButton.IsCompact.ShouldBeFalse();
            stats.TableRows[1].ShowsRate.ShouldBeTrue();

            window.Width = 400;
            Pump();
            stats.DetailsButton.IsCompact.ShouldBeTrue();
            AutomationProperties.GetName(stats.DetailsButton).ShouldBe("Hide details");
            stats.TableRows[1].ShowsRate.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(stats, "PART_ModelName").TextTrimming.ShouldBe(Avalonia.Media.TextTrimming.CharacterEllipsis);

            // The strip fills its container: its width never follows the numbers.
            stats.Bounds.Width.ShouldBe(400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Shows_One_Line_And_The_Same_Values()
    {
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.Variant = GenerationStatsVariant.Inline;
        });
        try
        {
            TestHelpers.Descendant<StackPanel>(stats, "PART_CardFace").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<WrapPanel>(stats, "PART_InlineFace").IsVisible.ShouldBeTrue();
            stats.TtftNumber!.DisplayText.ShouldBe("412ms");
            stats.SpeedNumber!.DisplayText.ShouldBe("75 tok/s");
            stats.OutputNumber!.DisplayText.ShouldBe("284");
            stats.CostNumber!.DisplayText.ShouldBe("$0.0077");
            TestHelpers.Descendant<TextBlock>(stats, "PART_InlineTtftUnit").Text.ShouldBe(" to first token");
            TestHelpers.Descendant<TextBlock>(stats, "PART_InlineOutputUnit").Text.ShouldBe(" tokens");
            stats.DetailsButton!.Name.ShouldBe("PART_InlineDetails");
            stats.StatusText!.Text.ShouldBe("Done in 4.18s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.Labels = new GenerationStatsLabels
            {
                DoneIn = time => $"Fertig in {time}",
                FirstToken = "Erstes Token",
                Details = "Einzelheiten",
                SpeedUnit = " T/s",
                Group = model => $"Statistik {model}",
            };
        });
        try
        {
            stats.StatusText!.Text.ShouldBe("Fertig in 4.18s");
            stats.DetailsButton!.Text.ShouldBe("Einzelheiten");
            stats.SpeedNumber!.DisplayText.ShouldBe("75 T/s");
            AutomationProperties.GetName(stats).ShouldBe("Statistik Atlas 3 Pro");
            AutomationProperties.GetName(stats.CellViews[0]!).ShouldBe("Erstes Token, 412ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Model_Mark_Is_A_Monogram_Unless_The_App_Passes_One()
    {
        GenerationStatsMark.LetterOf("Atlas 3 Pro").ShouldBe("A");
        GenerationStatsMark.LetterOf(" 4o mini").ShouldBe("4");
        GenerationStatsMark.LetterOf(null).ShouldBe("?");
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.Model = new GenerationModel("Atlas") { Mark = ArcIcons.Sparkles };
        });
        try
        {
            var mark = TestHelpers.Descendant<GenerationStatsMark>(stats, "PART_Mark");
            mark.Glyph.ShouldBe(ArcIcons.Sparkles);
            mark.Letter.ShouldBe("A");
            TestHelpers.Descendant<TextBlock>(stats, "PART_ModelName").Text.ShouldBe("Atlas");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Given_Speed_Samples_Replace_The_Measured_Series()
    {
        var (stats, window, _) = Open(s =>
        {
            Done(s);
            s.SpeedSamples = [40, 80, 96, 90];
        });
        try
        {
            TestHelpers.Descendant<GenerationStatsSparkline>(stats, "PART_Sparkline").Samples.ShouldBe([40, 80, 96, 90]);
        }
        finally
        {
            window.Close();
        }
    }
}

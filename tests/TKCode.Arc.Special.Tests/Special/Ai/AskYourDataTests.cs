using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AskYourData: the formats and the scale, a simulation (the gallery's) and the empty state, the run from question to chart (writing, running, done), the
/// SQL panel's fold, Stop, failures, no data, streamed SQL and progress, the chart switch, the chart readout's keyboard
/// model, the field's keys, the narrow layout and the labels.
/// </summary>
public class AskYourDataTests
{
    private static (AskYourData Card, Window Window) Open(Action<AskYourData>? setup = null, double width = 640,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var card = new AskYourData
        {
            Simulation = AskYourDataTestData.Simulation(), Width = width, AutoRun = false, Delay = static (_, _) => Task.CompletedTask
        };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        return (card, window);
    }

    private static string SwapText(AskYourData card, string part) =>
        (TestHelpers.Descendant<ArcContentSwap>(card, part).Content as TextBlock)?.Text ?? string.Empty;

    private static void Run(AskYourData card, string? question = null)
    {
        if (question is null)
        {
            card.Ask();
        }
        else
        {
            card.Ask(question);
        }
        TestHelpers.PumpUntil(() => card.CurrentRun?.IsCompleted != false, 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
    }

    // ---- Pure logic ----

    [Theory]
    [InlineData(214, "214ms")]
    [InlineData(999.4, "999ms")]
    [InlineData(1240, "1.2s")]
    [InlineData(12_000, "12.0s")]
    public void Duration_Reads_Milliseconds_Then_Seconds(double ms, string expected) =>
        AskYourDataFormat.Duration(ms).ShouldBe(expected);

    [Theory]
    [InlineData(99, 100, 25)]
    [InlineData(160, 200, 50)]
    [InlineData(4.4, 6, 2)]
    [InlineData(9.5, 10, 2.5)]
    [InlineData(129_100, 150_000, 50_000)]
    public void NiceScale_Rounds_The_Top_Up_To_A_Nice_Step(double max, double top, double step)
    {
        var scale = AskYourDataFormat.NiceScale(max);
        scale.Top.ShouldBe(top, 1e-9);
        scale.Step.ShouldBe(step, 1e-9);
    }

    [Theory]
    [InlineData(50, 1)]
    [InlineData(46, 1)]
    [InlineData(30, 2)]
    [InlineData(10, 5)]
    public void Labels_Thin_Out_To_Keep_46px_Apart(double band, int stride) =>
        AskYourDataFormat.LabelStride(band, AskYourDataChart.LabelGap).ShouldBe(stride);

    [Fact]
    public void Value_Format_Uses_En_Us_Grouping()
    {
        AskYourDataFormat.Value(1430).ShouldBe("1,430");
        AskYourDataFormat.Value(3.1).ShouldBe("3.1");
        AskYourDataFormat.Grouped(18_342).ShouldBe("18,342");
    }

    [Fact]
    public void Readout_Lists_Every_Series_And_The_Total_When_Stacked()
    {
        var result = AskYourDataTestData.Signups();
        AskYourDataFormat.Readout(result, 1, AskYourDataFormat.Value, AskYourDataLabels.Default)
            .ShouldBe("Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99");
        var rates = AskYourDataTestData.Churn();
        AskYourDataFormat.Readout(rates, 0, rates.FormatValue!, AskYourDataLabels.Default)
            .ShouldBe("May: Pro 4.1%, Team 2.8%, Enterprise 0.9%");
    }

    [Fact]
    public void Legend_Value_Defaults_To_Sum_When_Stacked_And_None_Otherwise()
    {
        var signups = AskYourDataTestData.Signups();
        AskYourDataFormat.LegendNumber(signups, signups.Series[0]).ShouldBe(863);
        var mrr = AskYourDataTestData.Mrr();
        AskYourDataFormat.LegendNumber(mrr, mrr.Series[2]).ShouldBe(61_300);
        var churn = AskYourDataTestData.Churn();
        AskYourDataFormat.LegendNumber(churn, churn.Series[0]).ShouldBeNull();
    }

    [Fact]
    public void Meta_Joins_Dialect_Rows_And_Time()
    {
        var labels = AskYourDataLabels.Default;
        AskYourDataFormat.Meta("PostgreSQL", null, AskYourDataTestData.Signups(), labels).ShouldBe("PostgreSQL · 48 rows · 214ms");
        AskYourDataFormat.Meta("PostgreSQL", "writing", null, labels).ShouldBe("PostgreSQL · writing");
        AskYourDataFormat.Meta(null, null, new AskYourDataResult { RowCount = 1 }, labels).ShouldBe("1 row");
    }

    [Fact]
    public void Token_Ends_Count_Words_Strings_And_Symbols()
    {
        var text = "SELECT a.b, 'x y' FROM t;";
        var ends = AskYourDataFormat.TokenEnds(text);
        ends.Select(e => text[..e]).ShouldBe(["SELECT", "SELECT a", "SELECT a.", "SELECT a.b", "SELECT a.b,", "SELECT a.b, 'x y'",
            "SELECT a.b, 'x y' FROM", "SELECT a.b, 'x y' FROM t", "SELECT a.b, 'x y' FROM t;"]);
        AskYourDataFormat.WordEnds("Two words  here").ShouldBe([3, 9, 15]);
    }

    [Theory]
    [InlineData("Weekly signups by plan since August", "Weekly signups by plan")]
    [InlineData("What is our MRR?", "MRR by plan")]
    [InlineData("churn by month", "Monthly churn rate by plan")]
    public void A_Simulation_Answers_By_Keyword(string question, string title)
    {
        var simulation = AskYourDataTestData.Simulation();
        simulation.ResultFor(question).Title.ShouldBe(title);
        simulation.SqlFor(question).ShouldStartWith(question.Contains("churn", StringComparison.Ordinal) ? "WITH" : "SELECT");
    }

    [Fact]
    public void A_Simulation_Falls_Back_For_Other_Questions()
    {
        var simulation = AskYourDataTestData.Simulation();
        simulation.ResultFor("How many llamas visited?").HasData.ShouldBeFalse();
        simulation.SqlFor("How many llamas visited?").ShouldContain("'%visited%'");
        simulation.ScannedFor("How many llamas visited?").ShouldBe(2_406);
        new AskYourDataSimulation().SqlFor("anything").ShouldBe(string.Empty);
        new AskYourDataSimulation().ResultFor("anything").HasData.ShouldBeFalse();
    }

    // ---- The card ----

    [AvaloniaFact]
    public void Idle_Card_Offers_The_Source_And_The_Suggestions()
    {
        var (card, window) = Open();
        try
        {
            card.Stage.ShouldBe(AskYourDataStage.Idle);
            card.Question.ShouldBeNull();
            card.EffectiveQuestion.ShouldBe("Weekly signups by plan since August");
            TestHelpers.Descendant<Border>(card, AskYourData.SqlBlockPart).IsVisible.ShouldBeFalse();
            SwapText(card, AskYourData.TitlePart).ShouldBe("Ask a question about analytics");
            TestHelpers.Descendant<TextBlock>(card, AskYourData.SourceNamePart).Text.ShouldBe("analytics");
            var chips = TestHelpers.Descendant<WrapPanel>(card, AskYourData.SuggestionsPart).Children;
            chips.Count.ShouldBe(3);
            TestHelpers.Descendant<StackPanel>(card, AskYourData.SuggestionsViewPart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Nothing_Wired_And_No_Simulation_The_Card_Waits_Empty()
    {
        var (card, window) = Open(c => c.Simulation = null);
        try
        {
            card.Stage.ShouldBe(AskYourDataStage.Idle);
            card.CanAsk.ShouldBeFalse();
            card.IsSimulated.ShouldBeFalse();
            card.EffectiveQuestion.ShouldBeNull();
            SwapText(card, AskYourData.TitlePart).ShouldBe("Ask a question about your data");
            TestHelpers.Descendant<TextBlock>(card, AskYourData.SourceNamePart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<StackPanel>(card, AskYourData.SuggestionsViewPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, AskYourData.AskPart).IsEnabled.ShouldBeFalse();
            card.Ask("Weekly signups by plan since August");
            card.CurrentRun.ShouldBeNull();
            card.Stage.ShouldBe(AskYourDataStage.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Apps_Delegates_Win_Over_The_Simulation()
    {
        var (card, window) = Open(c =>
        {
            c.GenerateSql = _ => Task.FromResult("SELECT 1;");
            c.RunQuery = _ => Task.FromResult(new AskYourDataResult
            {
                Title = "Ones", Categories = ["a"], Series = [new AskYourDataSeries { Id = "n", Label = "N", Values = [1] }], RowCount = 1,
            });
        });
        try
        {
            card.IsSimulated.ShouldBeFalse();
            card.Ask("Weekly signups by plan since August");
            await card.CurrentRun!;
            Dispatcher.UIThread.RunJobs();
            card.Sql.ShouldBe("SELECT 1;");
            card.Result!.Title.ShouldBe("Ones");
            card.Announcement!.ShouldNotContain("Simulated");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_And_Setting_It_To_Null_Stops_It()
    {
        var gate = new TaskCompletionSource();
        var (card, window) = Open(c => c.Delay = (_, token) => gate.Task.WaitAsync(token));
        try
        {
            card.Ask();
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Writing);
            card.Simulation = null;
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Idle);
            card.Result.ShouldBeNull();
            card.Sql.ShouldBe(string.Empty);
            gate.SetResult();
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Idle);
            SwapText(card, AskYourData.TitlePart).ShouldBe("Ask a question about your data");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Suggestions_Hide_Them()
    {
        var (card, window) = Open(c => c.Suggestions = []);
        try
        {
            TestHelpers.Descendant<StackPanel>(card, AskYourData.SuggestionsViewPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sample_Run_Writes_Sql_Runs_And_Charts_The_Answer()
    {
        var (card, window) = Open();
        try
        {
            AskYourDataResultEventArgs? ready = null;
            card.ResultReady += (_, e) => ready = e;
            Run(card);
            card.Stage.ShouldBe(AskYourDataStage.Done);
            card.Result!.Title.ShouldBe("Weekly signups by plan");
            card.Sql.ShouldStartWith("SELECT");
            card.IsSqlOpen.ShouldBeFalse();
            TestHelpers.Descendant<AiHeightBox>(card, AskYourData.SqlFoldPart).IsMounted.ShouldBeFalse();
            SwapText(card, AskYourData.SqlMetaPart).ShouldBe("PostgreSQL · 48 rows · 214ms");
            SwapText(card, AskYourData.TitlePart).ShouldBe("Weekly signups by plan");
            SwapText(card, AskYourData.SubtitlePart).ShouldBe("Aug 3 to Oct 25 · 1,430 signups");
            TestHelpers.Descendant<ToolCallStatusGlyph>(card, AskYourData.StageStatusPart).Status.ShouldBe(ToolCallStatus.Success);
            TestHelpers.Descendant<StackPanel>(card, AskYourData.ResultViewPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<WrapPanel>(card, AskYourData.LegendPart).Children.Count.ShouldBe(4);
            card.DisplayedChart.ShouldBe(AskYourDataChartKind.Bar);
            var insight = TestHelpers.Descendant<AskYourDataRevealText>(card, AskYourData.InsightPart);
            insight.FullText.ShouldStartWith("Weekly signups nearly doubled");
            insight.RevealedLength.ShouldBe(insight.FullText.Length);
            ready.ShouldNotBeNull();
            ready.Question.ShouldBe("Weekly signups by plan since August");
            ready.Sql.ShouldBe(card.Sql);
            card.Announcement!.ShouldContain("Simulated with sample data, nothing was queried.");
            card.Announcement!.ShouldStartWith("Weekly signups by plan. 48 rows.");
            AutomationProperties.GetName(TestHelpers.Descendant<AskYourDataSqlToggle>(card, AskYourData.SqlTogglePart))
                .ShouldBe("Generated SQL, PostgreSQL · 48 rows · 214ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sql_Is_Laid_Out_Whole_And_Revealed_Four_Tokens_A_Tick()
    {
        var ticks = new List<int>();
        var (card, window) = Open(c =>
        {
            c.KeepSqlOpen = true;
            c.Delay = (time, _) =>
            {
                if (time == AskYourData.Tick)
                {
                    // The panel was just mounted: its scroll viewer builds its template on the next layout.
                    c.UpdateLayout();
                    ticks.Add(TestHelpers.Descendant<AskYourDataRevealText>(c, AskYourData.SqlPart).RevealedLength);
                }
                return Task.CompletedTask;
            };
        }, level: ArcMotionLevel.Full);
        try
        {
            ArcMotion.GetEffectiveLevel(card).ShouldBe(ArcMotionLevel.Full);
            card.IsLoaded.ShouldBeTrue();
            Run(card);
            var sql = TestHelpers.Descendant<AskYourDataRevealText>(card, AskYourData.SqlPart);
            card.Stage.ShouldBe(AskYourDataStage.Done);
            var text = card.Sql;
            var ends = AskYourDataFormat.TokenEnds(text);
            ticks.Count.ShouldBeGreaterThan(3);
            ticks[0].ShouldBe(ends[3]);
            ticks[1].ShouldBe(ends[7]);
            sql.FullText.ShouldBe(text);
            sql.RevealedLength.ShouldBe(text.Length);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_Sql_And_Insight_Whole_And_Drops_The_Build()
    {
        var ticks = 0;
        var (card, window) = Open(c => c.Delay = (time, _) =>
        {
            if (time == AskYourData.Tick)
            {
                ticks++;
            }
            return Task.CompletedTask;
        }, level: ArcMotionLevel.Reduced);
        try
        {
            Run(card);
            card.Stage.ShouldBe(AskYourDataStage.Done);
            ticks.ShouldBe(0);
            TestHelpers.Descendant<AskYourDataChart>(card, AskYourData.BarChartPart).BuildProgress.ShouldBe(1);
            var insight = TestHelpers.Descendant<AskYourDataRevealText>(card, AskYourData.InsightPart);
            insight.RevealedLength.ShouldBe(insight.FullText.Length);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Builds_The_Bars_From_The_Baseline()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            Run(card);
            var chart = TestHelpers.Descendant<AskYourDataChart>(card, AskYourData.BarChartPart);
            chart.BuildProgress.ShouldBeLessThan(1);
            TestHelpers.PumpUntil(() => chart.BuildProgress >= 1, 5000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keep_Sql_Open_Leaves_The_Panel_Open()
    {
        var (card, window) = Open(c => c.KeepSqlOpen = true);
        try
        {
            Run(card);
            card.IsSqlOpen.ShouldBeTrue();
            TestHelpers.Descendant<AiHeightBox>(card, AskYourData.SqlFoldPart).IsMounted.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Persons_Choice_Wins_Over_The_Automatic_Fold()
    {
        var query = new TaskCompletionSource<AskYourDataResult>();
        var (card, window) = Open(c => c.RunQuery = _ => query.Task);
        try
        {
            card.Ask();
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Running);
            card.IsSqlOpen.ShouldBeFalse();
            var toggle = TestHelpers.Descendant<AskYourDataSqlToggle>(card, AskYourData.SqlTogglePart);
            toggle.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsSqlOpen.ShouldBeTrue();
            toggle.IsExpanded.ShouldBeTrue();
            query.SetResult(AskYourDataTestData.Signups());
            TestHelpers.PumpUntil(() => card.Stage == AskYourDataStage.Done).ShouldBeTrue();
            card.IsSqlOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Cancels_The_Query_Without_An_Error()
    {
        CancellationToken seen = default;
        var (card, window) = Open(c => c.RunQuery = async request =>
        {
            seen = request.CancellationToken;
            await Task.Delay(Timeout.Infinite, request.CancellationToken);
            return new AskYourDataResult();
        });
        try
        {
            var failed = false;
            card.Failed += (_, _) => failed = true;
            card.Ask();
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Running);
            TestHelpers.Descendant<ArcGlyph>(card, AskYourData.AskGlyphPart).Data.ShouldBe(ArcIcons.Square);
            AutomationProperties.GetName(TestHelpers.Descendant<Avalonia.Controls.Button>(card, AskYourData.AskPart)).ShouldBe("Stop");
            card.Stop();
            TestHelpers.PumpUntil(() => card.CurrentRun!.IsCompleted).ShouldBeTrue();
            card.Stage.ShouldBe(AskYourDataStage.Stopped);
            seen.IsCancellationRequested.ShouldBeTrue();
            failed.ShouldBeFalse();
            SwapText(card, AskYourData.TitlePart).ShouldBe("Stopped");
            TestHelpers.Descendant<ToolCallStatusGlyph>(card, AskYourData.StageStatusPart).Status.ShouldBe(ToolCallStatus.Denied);
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, AskYourData.RetryPart).IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Query_Error_Shows_The_Message_And_Try_Again_Asks_Again()
    {
        var calls = 0;
        var (card, window) = Open(c => c.RunQuery = _ =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<AskYourDataResult>(new InvalidOperationException("relation \"signups\" does not exist"))
                : Task.FromResult(AskYourDataTestData.Signups());
        });
        try
        {
            AskYourDataErrorEventArgs? failed = null;
            card.Failed += (_, e) => failed = e;
            Run(card);
            card.Stage.ShouldBe(AskYourDataStage.Error);
            failed.ShouldNotBeNull();
            failed.Stage.ShouldBe(AskYourDataErrorStage.Query);
            failed.Error.Message.ShouldBe("relation \"signups\" does not exist");
            TestHelpers.Descendant<SelectableTextBlock>(card, AskYourData.ErrorTextPart).Text.ShouldBe("relation \"signups\" does not exist");
            SwapText(card, AskYourData.TitlePart).ShouldBe("The query failed");
            TestHelpers.Descendant<ToolCallStatusGlyph>(card, AskYourData.SqlStatusPart).Status.ShouldBe(ToolCallStatus.Success);
            var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(card, AskYourData.RetryPart);
            retry.Content.ShouldBe("Try again");
            retry.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => card.Stage == AskYourDataStage.Done).ShouldBeTrue();
            calls.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sql_Error_Marks_The_Sql_Row()
    {
        var (card, window) = Open(c => c.GenerateSql = _ => Task.FromException<string>(new TimeoutException("The model timed out")));
        try
        {
            AskYourDataErrorEventArgs? failed = null;
            card.Failed += (_, e) => failed = e;
            Run(card);
            card.Stage.ShouldBe(AskYourDataStage.Error);
            failed!.Stage.ShouldBe(AskYourDataErrorStage.Sql);
            SwapText(card, AskYourData.TitlePart).ShouldBe("Couldn't write the SQL");
            TestHelpers.Descendant<ToolCallStatusGlyph>(card, AskYourData.SqlStatusPart).Status.ShouldBe(ToolCallStatus.Error);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Question_That_Matches_Nothing_Offers_The_Suggestions()
    {
        var (card, window) = Open();
        try
        {
            Run(card, "How many llamas visited the office?");
            card.Stage.ShouldBe(AskYourDataStage.Empty);
            SwapText(card, AskYourData.TitlePart).ShouldBe("No rows matched");
            TestHelpers.Descendant<StackPanel>(card, AskYourData.SuggestionsViewPart).IsVisible.ShouldBeTrue();
            var chip = (Avalonia.Controls.Button)TestHelpers.Descendant<WrapPanel>(card, AskYourData.SuggestionsPart).Children[1];
            chip.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => card.Stage == AskYourDataStage.Done).ShouldBeTrue();
            card.Question.ShouldBe("MRR by plan over the last 6 months");
            card.DisplayedChart.ShouldBe(AskYourDataChartKind.Line);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streamed_Sql_Grows_With_Each_Chunk()
    {
        static async IAsyncEnumerable<string> Chunks()
        {
            foreach (var chunk in new[] { "SELECT ", "count(*) ", "FROM users;" })
            {
                await Task.Yield();
                yield return chunk;
            }
        }

        var (card, window) = Open(c =>
        {
            c.StreamSql = _ => Chunks();
            c.RunQuery = request => Task.FromResult(new AskYourDataResult
            {
                Title = "Users",
                Categories = ["All"],
                Series = [new AskYourDataSeries { Id = "n", Label = "Users", Values = [request.Sql.Length] }],
            });
        });
        try
        {
            Run(card);
            card.Sql.ShouldBe("SELECT count(*) FROM users;");
            card.Stage.ShouldBe(AskYourDataStage.Done);
            card.Result!.Series[0].Values[0].ShouldBe(card.Sql.Length);
            card.Announcement!.ShouldNotContain("Simulated");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Progress_From_Any_Thread_Rolls_The_Count()
    {
        var query = new TaskCompletionSource<AskYourDataResult>();
        var (card, window) = Open(c => c.RunQuery = request =>
        {
            Task.Run(() => request.ReportProgress(1234, 50_000));
            return query.Task;
        });
        try
        {
            card.Ask();
            TestHelpers.PumpUntil(() => card.RowsScanned == 1234).ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, AskYourData.RowsReservePart).Text.ShouldBe("50,000");
            TestHelpers.Descendant<RollingDigits>(card, AskYourData.RowsPart).Value.ShouldBe(1234);
            SwapText(card, AskYourData.TitlePart).ShouldBe("Running query");
            query.SetResult(AskYourDataTestData.Signups());
            TestHelpers.PumpUntil(() => card.Stage == AskYourDataStage.Done).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Switch_Changes_The_View_And_Raises_ChartChanged()
    {
        var (card, window) = Open();
        try
        {
            Run(card);
            var changed = 0;
            card.ChartChanged += (_, _) => changed++;
            var segmented = TestHelpers.Descendant<SegmentedControl>(card, AskYourData.SwitchPart);
            segmented.IsVisible.ShouldBeTrue();
            segmented.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            card.DisplayedChart.ShouldBe(AskYourDataChartKind.Table);
            changed.ShouldBe(1);
            var table = TestHelpers.Descendant<AskYourDataTable>(card, AskYourData.TablePart);
            table.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<AskYourDataChart>(card, AskYourData.BarChartPart).IsVisible.ShouldBeFalse();
            var cells = table.CellGrid!.Children.OfType<TextBlock>().Select(t => t.Text).ToList();
            cells.ShouldContain("Week");
            cells.ShouldContain("Total");
            cells.ShouldContain("99");
            card.Chart.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controlled_Chart_Sets_The_View()
    {
        var (card, window) = Open(c => c.Chart = AskYourDataChartKind.Line);
        try
        {
            Run(card);
            card.DisplayedChart.ShouldBe(AskYourDataChartKind.Line);
            card.Chart = AskYourDataChartKind.Table;
            card.DisplayedChart.ShouldBe(AskYourDataChartKind.Table);
            TestHelpers.Descendant<SegmentedControl>(card, AskYourData.SwitchPart).SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            card.Chart.ShouldBe(AskYourDataChartKind.Bar);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Readout_Follows_The_Arrow_Keys()
    {
        var (card, window) = Open();
        try
        {
            Run(card);
            var chart = TestHelpers.Descendant<AskYourDataChart>(card, AskYourData.BarChartPart);
            AutomationProperties.GetName(chart).ShouldBe("Weekly signups by plan, chart");
            chart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            card.Announcement.ShouldBe("Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Scale_Holds_The_Tallest_Stack_And_Thins_Labels_When_Narrow()
    {
        var (card, window) = Open(width: 360);
        try
        {
            Run(card);
            var chart = TestHelpers.Descendant<AskYourDataChart>(card, AskYourData.BarChartPart);
            chart.AxisTop.ShouldBe(200);
            chart.LabelStride.ShouldBeGreaterThan(1);
            chart.BuildProgress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Asks_And_Escape_Stops_In_The_Field()
    {
        var query = new TaskCompletionSource<AskYourDataResult>();
        var (card, window) = Open(c => c.RunQuery = _ => query.Task);
        try
        {
            var field = TestHelpers.Descendant<Input>(card, AskYourData.FieldPart);
            field.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Running);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.Stage.ShouldBe(AskYourDataStage.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Writes_The_Question_And_An_Empty_Field_Disables_Ask()
    {
        var (card, window) = Open();
        try
        {
            var field = TestHelpers.Descendant<Input>(card, AskYourData.FieldPart);
            field.Text = "Churn rate by plan";
            card.Question.ShouldBe("Churn rate by plan");
            field.Text = "";
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, AskYourData.AskPart).IsEnabled.ShouldBeFalse();
            card.Question = "MRR by plan";
            field.Text.ShouldBe("MRR by plan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Card_Keeps_The_Source_Icon_And_Moves_The_Switch()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.IsCompact.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, AskYourData.SourceNamePart).IsVisible.ShouldBeFalse();
            Run(card);
            TestHelpers.Descendant<SegmentedControl>(card, AskYourData.SwitchPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<SegmentedControl>(card, AskYourData.CompactSwitchPart).IsVisible.ShouldBeTrue();
            card.Width = 640;
            Dispatcher.UIThread.RunJobs();
            card.IsCompact.ShouldBeFalse();
            TestHelpers.Descendant<SegmentedControl>(card, AskYourData.SwitchPart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Source_Reword_The_Card()
    {
        var (card, window) = Open(c =>
        {
            c.Source = new AskYourDataSource { Name = "warehouse", Dialect = "SQLite" };
            c.Labels = new AskYourDataLabels { EmptyTitle = name => "Frag " + name, GeneratedSql = "SQL", Rows = n => n + " Zeilen" };
        });
        try
        {
            SwapText(card, AskYourData.TitlePart).ShouldBe("Frag warehouse");
            Run(card);
            TestHelpers.Descendant<TextBlock>(card, AskYourData.SqlTitlePart).Text.ShouldBe("SQL");
            SwapText(card, AskYourData.SqlMetaPart).ShouldBe("SQLite · 48 Zeilen · 214ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AutoRun_Asks_Once_On_Screen_Only_Without_A_Model()
    {
        var card = new AskYourData { Simulation = AskYourDataTestData.Simulation(), Width = 640, Delay = static (_, _) => Task.CompletedTask };
        var window = TestHelpers.Show(card);
        try
        {
            TestHelpers.PumpUntil(() => card.Stage == AskYourDataStage.Done, 5000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
        var wired = new AskYourData
        {
            Simulation = AskYourDataTestData.Simulation(), Width = 640, GenerateSql = _ => Task.FromResult("SELECT 1;"),
            Delay = static (_, _) => Task.CompletedTask
        };
        var second = TestHelpers.Show(wired);
        try
        {
            Dispatcher.UIThread.RunJobs();
            wired.Stage.ShouldBe(AskYourDataStage.Idle);
        }
        finally
        {
            second.Close();
        }
    }

    [AvaloniaFact]
    public void Sql_Is_Highlighted_Through_The_Library_Highlighter()
    {
        var (card, window) = Open(c => c.KeepSqlOpen = true);
        try
        {
            Run(card);
            var sql = TestHelpers.Descendant<AskYourDataRevealText>(card, AskYourData.SqlPart);
            TestHelpers.PumpUntil(() => sql.WhenHighlighted.IsCompleted).ShouldBeTrue();
            sql.Tokens.ShouldNotBeNull();
            sql.Inlines!.Count.ShouldBeGreaterThan(10);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's AskYourData demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class AskYourDataTestData
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The question the card starts with.</summary>
    public const string Question = "Weekly signups by plan since August";

    /// <summary>The suggestions offered in the empty state.</summary>
    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "Weekly signups by plan since August",
        "MRR by plan over the last 6 months",
        "Churn rate by plan per month",
    ];

    /// <summary>The database the answers come from.</summary>
    public static AskYourDataSource Source { get; } = new() { Name = "analytics", Dialect = "PostgreSQL" };

    /// <summary>The three answers, in the order they are matched: churn, then MRR or revenue, then signups.</summary>
    public static IList<AskYourDataSimulationAnswer> Answers { get; } =
    [
        new()
        {
            Keywords = ["churn"],
            Sql = """
                WITH monthly AS (
                  SELECT date_trunc('month', m.month)::date AS month,
                         p.name AS plan,
                         count(*) FILTER (WHERE m.churned) AS churned,
                         count(*) AS active
                  FROM account_months m
                  JOIN plans p ON p.id = m.plan_id
                  WHERE m.month >= date '2026-05-01' AND p.name <> 'Free'
                  GROUP BY 1, 2
                )
                SELECT month, plan, round(100.0 * churned / active, 1) AS churn_rate
                FROM monthly
                ORDER BY month, plan;
                """,
            RowsScanned = 9_630,
            Result = Churn(),
        },
        new()
        {
            Keywords = ["mrr", "revenue"],
            Sql = """
                SELECT
                  date_trunc('month', s.period_start)::date AS month,
                  p.name AS plan,
                  sum(s.amount_cents) / 100.0 AS mrr
                FROM subscriptions s
                JOIN plans p ON p.id = s.plan_id
                WHERE s.status = 'active'
                  AND s.period_start >= date '2026-05-01'
                  AND p.name <> 'Free'
                GROUP BY month, plan
                ORDER BY month, plan;
                """,
            RowsScanned = 4_812,
            Result = Mrr(),
        },
        new()
        {
            Keywords = ["signup", "sign up", "sign-up"],
            Sql = """
                SELECT
                  date_trunc('week', u.created_at)::date AS week,
                  p.name AS plan,
                  count(*) AS signups
                FROM users u
                JOIN plans p ON p.id = u.plan_id
                WHERE u.created_at >= date '2026-08-03'
                GROUP BY week, plan
                ORDER BY week, plan;
                """,
            RowsScanned = 18_342,
            Result = Signups(),
        },
    ];

    /// <summary>Any other question: a search for its longest word that matches no rows.</summary>
    public static AskYourDataSimulationAnswer Fallback { get; } = new()
    {
        Sql = "SELECT e.name, count(*) AS events\nFROM events e\nWHERE e.name ILIKE '%" + AskYourDataSimulationAnswer.KeywordToken
              + "%'\nGROUP BY e.name\nORDER BY events DESC\nLIMIT 50;",
        RowsScanned = 2_406,
        Result = new AskYourDataResult { Title = string.Empty, RowCount = 0, DurationMs = 38 },
    };

    /// <summary>A new simulation of the model and database (tests and code use it; the XAML demos spell it out).</summary>
    public static AskYourDataSimulation Simulation() => new()
    {
        Question = Question,
        Suggestions = Suggestions,
        Source = Source,
        Answers = Answers,
        Fallback = Fallback,
    };

    /// <summary>Weekly signups by plan, 12 weeks from Aug 3: stacked bars with totals.</summary>
    public static AskYourDataResult Signups()
    {
        string[] weeks = ["Aug 3", "Aug 10", "Aug 17", "Aug 24", "Aug 31", "Sep 7", "Sep 14", "Sep 21", "Sep 28", "Oct 5", "Oct 12", "Oct 19"];
        double[] free = [52, 61, 58, 66, 70, 64, 73, 79, 75, 84, 88, 93];
        double[] pro = [21, 26, 24, 27, 30, 29, 33, 35, 34, 38, 41, 44];
        double[] team = [8, 10, 9, 11, 12, 11, 13, 14, 15, 16, 17, 19];
        double[] enterprise = [1, 2, 1, 2, 3, 2, 2, 3, 3, 4, 3, 4];
        var total = free.Sum() + pro.Sum() + team.Sum() + enterprise.Sum();
        return new AskYourDataResult
        {
            Title = "Weekly signups by plan",
            Subtitle = "Aug 3 to Oct 25 · " + total.ToString("N0", s_enUs) + " signups",
            Categories = weeks,
            Series =
            [
                new AskYourDataSeries { Id = "free", Label = "Free", Values = free },
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            CategoryLabel = "Week",
            Insight = "Weekly signups nearly doubled, from 82 to 160. Free drives the volume, and Pro more than doubled, from 21 to 44 a week.",
            RowCount = 48,
            DurationMs = 214,
        };
    }

    /// <summary>MRR by paid plan, May to October: lines, the legend shows the latest month.</summary>
    public static AskYourDataResult Mrr()
    {
        string[] months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct"];
        double[] pro = [31_200, 33_000, 34_900, 36_100, 38_400, 40_200];
        double[] team = [18_400, 19_900, 21_500, 23_800, 25_100, 27_600];
        double[] enterprise = [42_000, 42_000, 48_500, 48_500, 55_000, 61_300];
        return new AskYourDataResult
        {
            Title = "MRR by plan",
            Subtitle = "May to Oct · paid plans",
            Categories = months,
            Series =
            [
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            LegendValue = AskYourDataLegendValue.Last,
            Chart = AskYourDataChartKind.Line,
            CategoryLabel = "Month",
            FormatValue = Thousands,
            Insight = "MRR reached $129.1k in October, up from $91.6k in May. Enterprise added the most, $19.3k, as two annual contracts started.",
            RowCount = 18,
            DurationMs = 1240,
        };
    }

    /// <summary>Monthly churn rate by paid plan: rates, so bars group side by side and nothing is totalled.</summary>
    public static AskYourDataResult Churn()
    {
        string[] months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct"];
        double[] pro = [4.1, 3.9, 4.4, 3.6, 3.4, 3.1];
        double[] team = [2.8, 2.6, 2.9, 2.4, 2.2, 2.0];
        double[] enterprise = [0.9, 1.1, 0.8, 0.7, 0.9, 0.6];
        return new AskYourDataResult
        {
            Title = "Monthly churn rate by plan",
            Subtitle = "May to Oct · paid plans",
            Categories = months,
            Series =
            [
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            Stacked = false,
            CategoryLabel = "Month",
            FormatValue = Percent,
            Insight = "Churn fell on every paid plan since May. Pro is still the highest at 3.1%, down from 4.1%.",
            RowCount = 18,
            DurationMs = 87,
        };
    }

    private static string Thousands(double value) =>
        value >= 1000 ? "$" + (value / 1000).ToString("0.#", s_enUs) + "k" : "$" + value.ToString("0", s_enUs);

    private static string Percent(double value) => value.ToString("0.0", s_enUs) + "%";
}

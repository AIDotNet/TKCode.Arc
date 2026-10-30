using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The reasoning block: parsing, label and time per status, the streaming preview, the steps, Stop, Retry and copy, the
/// empty block and the simulation.
/// </summary>
public class ReasoningBlockTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("Two  words\nover lines", "Two words over lines")]
    public void Tail_Folds_Whitespace(string? text, string expected) =>
        ReasoningBlockText.Tail(text, 100).ShouldBe(expected);

    [Fact]
    public void Tail_Keeps_The_Newest_Words_From_A_Word_Start()
    {
        var tail = ReasoningBlockText.Tail("one two three four five six", 12);
        tail.ShouldBe("…five six");
    }

    [Fact]
    public void Parse_Splits_On_Bold_And_Hash_Headings()
    {
        var steps = ReasoningBlockText.Parse("**First**\n\nOne line\ncontinues.\n\nSecond paragraph.\n\n## Second\n\nBody.");
        steps.Count.ShouldBe(2);
        steps[0].Heading.ShouldBe("First");
        steps[0].Text.ShouldBe("One line continues.\n\nSecond paragraph.");
        steps[1].Heading.ShouldBe("Second");
        steps[1].Text.ShouldBe("Body.");
    }

    [Fact]
    public void Parse_Keeps_Text_Without_Headings_As_One_Step()
    {
        var steps = ReasoningBlockText.Parse("Just thinking out loud.");
        steps.Count.ShouldBe(1);
        steps[0].Heading.ShouldBeNull();
        steps[0].Text.ShouldBe("Just thinking out loud.");
    }

    [Fact]
    public void Parse_Hides_A_Heading_Still_Streaming_In()
    {
        var steps = ReasoningBlockText.Parse("**First**\n\nDone text.\n\n**Seco");
        steps.Count.ShouldBe(1);
        steps[0].Heading.ShouldBe("First");
    }

    [Fact]
    public void Default_Labels_Format_The_Times()
    {
        var labels = ReasoningBlockLabels.Default;
        labels.Duration(7).ShouldBe("for 7s");
        labels.Duration(65).ShouldBe("for 1m 5s");
        labels.After(4).ShouldBe("after 4s");
    }

    /// <summary>An invented reasoning in four steps (bold headings, paragraphs under them).</summary>
    private const string Reasoning =
        "**Reading the webhook handler**\n\nThe handler in api/billing/webhook.ts checks the signature and then calls fulfillOrder for every invoice.paid event. Nothing records which events it already handled, so a redelivered event runs the whole path again.\n\n" +
        "**Checking when Stripe retries**\n\nStripe sends an event again when the endpoint times out or answers with an error. The logs show fulfillOrder taking 11 seconds, because the receipt PDF renders before the handler answers.\n\n" +
        "**Tracing the second charge**\n\nEvery double charge lines up with a redelivery. Each retry calls createUsageCharge again with a key built from Date.now(), so Stripe sees a new request instead of a repeat.\n\n" +
        "**Choosing a fix**\n\nRecord event.id in a processed_events table inside the same transaction, derive the idempotency key from the invoice id, and move the receipt render to a queue so the handler answers in well under a second.";

    private static ReasoningBlock Finished() => new() { Reasoning = Reasoning, Elapsed = TimeSpan.FromSeconds(7) };

    private static ReasoningBlockSimulation Simulation() => new() { Reasoning = Reasoning };

    [AvaloniaFact]
    public void Block_Without_Data_Or_Simulation_Reads_Thought_Process_With_Nothing_To_Open()
    {
        var block = new ReasoningBlock { IsExpanded = true };
        var window = TestHelpers.Show(block);
        try
        {
            block.IsSimulating.ShouldBeFalse();
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thought process");
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).IsVisible.ShouldBeFalse();
            block.BodyBox!.IsMounted.ShouldBeFalse();
            block.PreviewBox!.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Thinks_Streams_And_Finishes()
    {
        var simulation = Simulation();
        var finished = 0;
        simulation.Finished += (_, _) => finished++;
        var block = new ReasoningBlock { Simulation = simulation };
        var window = TestHelpers.Show(block);
        try
        {
            block.IsSimulating.ShouldBeTrue();
            simulation.IsPlaying.ShouldBeTrue();
            var label = TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart);
            label.Text.ShouldBe("Thinking");
            label.IsShimmering.ShouldBeTrue();
            block.Classes.Contains(ReasoningBlock.ThinkingPseudoClass).ShouldBeTrue();
            simulation.Advance(TimeSpan.FromMilliseconds(50));
            simulation.Text.ShouldBe("**Reading the webhook");
            simulation.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.PreviewPart).Text.ShouldStartWith("The handler in");
            block.PreviewBox!.IsMounted.ShouldBeTrue();
            simulation.Advance(TimeSpan.FromSeconds(60));
            Dispatcher.UIThread.RunJobs();
            simulation.Status.ShouldBe(ReasoningStatus.Done);
            simulation.Text.ShouldBe(Reasoning);
            finished.ShouldBe(1);
            label.Text.ShouldBe("Thought");
            label.IsShimmering.ShouldBeFalse();
            // The thinking time stops at the tick that finished (46 ticks of 45ms), not at the minute passed in.
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).Text.ShouldBe("for 2s");
            block.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<StackPanel>(block, ReasoningBlock.StepsPart).Children.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Data_Wins_Over_The_Simulation_Which_Starts_Over_When_It_Is_Cleared()
    {
        var simulation = Simulation();
        var block = new ReasoningBlock { Reasoning = "**Own**\n\nMine.", IsExpanded = true, Simulation = simulation };
        var window = TestHelpers.Show(block);
        try
        {
            block.IsSimulating.ShouldBeFalse();
            simulation.HasStarted.ShouldBeFalse();
            var views = TestHelpers.Descendant<StackPanel>(block, ReasoningBlock.StepsPart).Children.Cast<ReasoningStepView>().ToArray();
            views.Length.ShouldBe(1);
            views[0].Heading.ShouldBe("Own");
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thought process");
            block.ClearValue(ReasoningBlock.ReasoningProperty);
            block.IsSimulating.ShouldBeTrue();
            simulation.Status.ShouldBe(ReasoningStatus.Thinking);
            simulation.Text.ShouldBeEmpty();
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thinking");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Simulation_Stops_Playback()
    {
        var simulation = Simulation();
        var block = new ReasoningBlock { Simulation = simulation };
        var window = TestHelpers.Show(block);
        try
        {
            simulation.Advance(TimeSpan.FromMilliseconds(500));
            block.IsSimulating.ShouldBeTrue();
            block.Simulation = null;
            Dispatcher.UIThread.RunJobs();
            block.IsSimulating.ShouldBeFalse();
            block.Classes.Contains(ReasoningBlock.ThinkingPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thought process");
            block.PreviewBox!.IsMounted.ShouldBeFalse();
            var before = simulation.Text;
            Dispatcher.UIThread.RunJobs();
            simulation.Text.ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Ends_A_Simulation_As_Stopped_And_Keeps_What_Streamed()
    {
        var simulation = Simulation();
        var block = new ReasoningBlock { Simulation = simulation, AllowsStop = true };
        var window = TestHelpers.Show(block);
        try
        {
            simulation.Advance(TimeSpan.FromMilliseconds(500));
            var streamed = simulation.Text;
            var stopped = false;
            block.StopRequested += (_, _) => stopped = true;
            TestHelpers.Descendant<Avalonia.Controls.Button>(block, ReasoningBlock.StopPart)
                .RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            stopped.ShouldBeTrue();
            simulation.Status.ShouldBe(ReasoningStatus.Stopped);
            simulation.Advance(TimeSpan.FromSeconds(1)).ShouldBeFalse();
            simulation.Text.ShouldBe(streamed);
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Stopped");
            TestHelpers.Descendant<Avalonia.Controls.Button>(block, ReasoningBlock.StopPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finished_Block_Reads_Its_Time_As_A_Card()
    {
        var block = Finished();
        var window = TestHelpers.Show(block);
        try
        {
            block.IsSimulating.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).Text.ShouldBe("for 7s");
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thought");
            AutomationProperties.GetName(TestHelpers.Descendant<ReasoningBlockToggle>(block)).ShouldBe("Thought for 7s, Show reasoning");
            block.BodyBox!.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Opens_And_Space_Closes_The_Steps_With_A_Footer()
    {
        var block = Finished();
        var window = TestHelpers.Show(block);
        try
        {
            var toggle = TestHelpers.Descendant<ReasoningBlockToggle>(block);
            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.IsExpanded.ShouldBeTrue();
            block.BodyBox!.IsMounted.ShouldBeTrue();
            toggle.IsExpanded.ShouldBeTrue();
            TestHelpers.Descendant<StackPanel>(block, ReasoningBlock.StepsPart).Children.Count.ShouldBe(4);
            TestHelpers.Descendant<Grid>(block, ReasoningBlock.FooterPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.StatusTextPart).Text.ShouldBe("Done");
            TestHelpers.Descendant<CopyButton>(block, ReasoningBlock.CopyPart).Value.ShouldNotBeNullOrEmpty();
            AutomationProperties.GetName(toggle).ShouldBe("Thought for 7s, Hide reasoning");
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.IsExpanded.ShouldBeFalse();
            block.BodyBox.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_Mark_Their_Place_On_The_Rail()
    {
        var block = new ReasoningBlock { IsExpanded = true, Reasoning = "**A**\n\none\n\n**B**\n\ntwo\n\n**C**\n\nthree" };
        var window = TestHelpers.Show(block);
        try
        {
            var views = TestHelpers.Descendant<StackPanel>(block, ReasoningBlock.StepsPart).Children.Cast<ReasoningStepView>().ToArray();
            views.Length.ShouldBe(3);
            views[0].Classes.Contains(ReasoningStepView.FirstPseudoClass).ShouldBeTrue();
            views[1].Classes.Contains(ReasoningStepView.FirstPseudoClass).ShouldBeFalse();
            views[1].Classes.Contains(ReasoningStepView.LastPseudoClass).ShouldBeFalse();
            views[2].Classes.Contains(ReasoningStepView.LastPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thinking_Streams_The_Latest_Words_Into_The_Preview()
    {
        var block = new ReasoningBlock { Status = ReasoningStatus.Thinking, Reasoning = "**Check**\n\nFirst words" };
        var window = TestHelpers.Show(block);
        try
        {
            var preview = TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.PreviewPart);
            var label = TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart);
            preview.Text.ShouldBe("First words");
            block.PreviewBox!.IsMounted.ShouldBeTrue();
            label.IsShimmering.ShouldBeTrue();
            label.Text.ShouldBe("Thinking");
            block.Reasoning += " and more";
            preview.Text.ShouldBe("First words and more");
            block.BodyBox!.IsMounted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finishing_Folds_The_Preview_And_Keeps_The_Time()
    {
        var block = new ReasoningBlock { Status = ReasoningStatus.Thinking, Elapsed = TimeSpan.FromSeconds(12), Reasoning = "text" };
        var window = TestHelpers.Show(block);
        try
        {
            var time = TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart);
            time.Text.ShouldBe("for 12s");
            block.Status = ReasoningStatus.Done;
            Dispatcher.UIThread.RunJobs();
            block.PreviewBox!.IsMounted.ShouldBeFalse();
            var label = TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart);
            label.Text.ShouldBe("Thought");
            label.IsShimmering.ShouldBeFalse();
            time.Text.ShouldBe("for 12s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Without_A_Time_Reads_Thought_Process()
    {
        var block = new ReasoningBlock { Reasoning = "Some thinking." };
        var window = TestHelpers.Show(block);
        try
        {
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thought process");
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stopped_Reads_After_The_Time_And_Footer_Says_Stopped()
    {
        var block = new ReasoningBlock { Status = ReasoningStatus.Stopped, Elapsed = TimeSpan.FromSeconds(4), Reasoning = "Partial.", IsExpanded = true };
        var window = TestHelpers.Show(block);
        try
        {
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Stopped");
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).Text.ShouldBe("after 4s");
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.StatusTextPart).Text.ShouldBe("Stopped");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Shows_Retry_The_Reason_And_No_Footer()
    {
        var block = new ReasoningBlock
        {
            Status = ReasoningStatus.Error,
            Reasoning = "Partial.",
            ErrorText = "The connection dropped.",
            IsExpanded = true,
        };
        var window = TestHelpers.Show(block);
        try
        {
            TestHelpers.Descendant<ToolCallText>(block, ReasoningBlock.LabelPart).Text.ShouldBe("Thinking interrupted");
            TestHelpers.Descendant<Avalonia.Controls.Button>(block, ReasoningBlock.RetryPart).IsVisible.ShouldBeTrue();
            var error = TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.ErrorPart);
            error.IsVisible.ShouldBeTrue();
            error.Text.ShouldBe("The connection dropped.");
            TestHelpers.Descendant<Grid>(block, ReasoningBlock.FooterPart).IsVisible.ShouldBeFalse();
            var retried = false;
            block.RetryRequested += (_, _) => retried = true;
            TestHelpers.Descendant<Avalonia.Controls.Button>(block, ReasoningBlock.RetryPart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            retried.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Shows_Only_While_Thinking_And_Raises_The_Event()
    {
        var block = new ReasoningBlock { Status = ReasoningStatus.Thinking, AllowsStop = true, Reasoning = "x" };
        var window = TestHelpers.Show(block);
        try
        {
            var stop = TestHelpers.Descendant<Avalonia.Controls.Button>(block, ReasoningBlock.StopPart);
            stop.IsVisible.ShouldBeTrue();
            var stopped = false;
            block.StopRequested += (_, _) => stopped = true;
            stop.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            stopped.ShouldBeTrue();
            block.Status = ReasoningStatus.Done;
            stop.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_While_Thinking_Hides_The_Preview_And_Shows_The_Steps()
    {
        var block = new ReasoningBlock { Status = ReasoningStatus.Thinking, IsExpanded = true, Reasoning = "**A**\n\none" };
        var window = TestHelpers.Show(block);
        try
        {
            block.BodyBox!.IsMounted.ShouldBeTrue();
            block.PreviewBox!.IsMounted.ShouldBeFalse();
            block.Reasoning += "\n\n**B**\n\ntwo";
            TestHelpers.Descendant<StackPanel>(block, ReasoningBlock.StepsPart).Children.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_Variant_Sets_The_Pseudo_Class()
    {
        var block = new ReasoningBlock { Variant = ReasoningBlockVariant.Plain };
        var window = TestHelpers.Show(block);
        try
        {
            block.Classes.Contains(":plain").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Labels_Replace_Every_Word()
    {
        var block = new ReasoningBlock
        {
            Elapsed = TimeSpan.FromSeconds(3),
            Steps = { new ReasoningStep(null, "x") },
            Labels = new ReasoningBlockLabels { Thought = "Réfléchi", Duration = s => $"pendant {s}s", Show = "Afficher" },
        };
        var window = TestHelpers.Show(block);
        try
        {
            TestHelpers.Descendant<TextBlock>(block, ReasoningBlock.TimePart).Text.ShouldBe("pendant 3s");
            AutomationProperties.GetName(TestHelpers.Descendant<ReasoningBlockToggle>(block)).ShouldBe("Réfléchi pendant 3s, Afficher");
        }
        finally
        {
            window.Close();
        }
    }
}

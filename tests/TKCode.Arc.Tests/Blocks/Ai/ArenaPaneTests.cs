using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ArenaPane"/>: the layout (head 48, answer 272, metrics 8 20 14), following its state (status words, metrics,
/// the first-token timer, the error card with retry or sample text), the privacy of the blind test across the visual,
/// logical and automation trees (the pane's own name included), the automation names, text appended while hidden, reset.
/// </summary>
public class ArenaPaneTests
{
    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public Rig(ArenaPane pane, ArcMotionLevel level = ArcMotionLevel.Off, int settleFrames = 30)
        {
            _clockInstall = Clock.Install();
            Pane = pane;
            Window = TestHelpers.Show(pane, level);
            Window.Width = 640;
            Window.Height = 600;
            Layout();
            Frames(settleFrames);
        }

        public ManualFrameClock Clock { get; } = new();
        public ArenaPane Pane { get; }
        public Window Window { get; }
        public ArenaPaneState State => Pane.State!;
        public ArenaAnswerView Answer => Pane.Answer!;
        public AiMetricLine Line => Pane.MetricLine!;
        public AiStatusPill Pill => Pane.StatusPill!;
        public ArenaIdentity Identity => Pane.Identity!;

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                CoreFrames.Run(Clock, 1);
                Layout();
            }
        }

        /// <summary>Waits until StreamMark has parsed what the answer was given.</summary>
        public async Task Parsed()
        {
            for (var i = 0; i < 600; i++)
            {
                Dispatcher.UIThread.RunJobs();
                var body = Answer.Body!;
                var document = body.Stream?.Document;
                if (document is null || document.Latest.Version >= document.Version)
                {
                    body.View?.ApplyLatest();
                    Layout();
                    if (document is null || ReferenceEquals(body.View?.Snapshot, document.Latest))
                    {
                        return;
                    }
                }
                await Task.Delay(5);
            }
            throw new TimeoutException("The Markdown was not parsed in time.");
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    private static Rig Show(ArenaPaneStatus status = ArenaPaneStatus.Idle, bool revealed = false, ArenaSlot slot = ArenaSlot.A,
        ArcMotionLevel level = ArcMotionLevel.Off)
        => new(ArenaPaneSamples.Pane(ArenaPaneSamples.State(slot, status: status), revealed), level);

    /// <summary>
    /// Everything the trees hold that could name or show a model: texts, marks, identity faces, automation names, help
    /// texts, item statuses and tooltips of every visual and logical descendant, and every automation peer below the root.
    /// </summary>
    internal static List<string> Traces(Control root)
    {
        var found = new List<string>();
        var visuals = root.GetVisualDescendants().Prepend(root).Concat(root.GetLogicalDescendants().OfType<Visual>()).Distinct();
        foreach (var visual in visuals)
        {
            switch (visual)
            {
                case TextBlock text when !string.IsNullOrEmpty(text.Text):
                    found.Add("text:" + text.Text);
                    break;
                case TextShimmer shimmer when !string.IsNullOrEmpty(shimmer.Text):
                    found.Add("shimmer:" + shimmer.Text);
                    break;
                case ModelMark { Mark: not null } mark:
                    found.Add("mark:" + mark.Mark);
                    break;
                case ArenaIdentityFace face:
                    found.Add("face:" + face.Text + (face.Mark is null ? "" : "+mark"));
                    break;
                case ContentPresenter { Content: string content }:
                    found.Add("content:" + content);
                    break;
            }
            if (visual is Control control)
            {
                foreach (var value in new[]
                         {
                             AutomationProperties.GetName(control), AutomationProperties.GetHelpText(control),
                             AutomationProperties.GetItemStatus(control), ToolTip.GetTip(control)?.ToString()
                         })
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        found.Add("automation:" + value);
                    }
                }
            }
        }
        var stack = new Stack<AutomationPeer>();
        stack.Push(ControlAutomationPeer.CreatePeerForElement(root));
        while (stack.Count > 0)
        {
            var peer = stack.Pop();
            found.Add("peer:" + peer.GetName() + "|" + peer.GetHelpText());
            foreach (var child in peer.GetChildren())
            {
                stack.Push(child);
            }
        }
        return found;
    }

    internal static void ShouldNotLeak(Control root, AiModel model)
    {
        foreach (var trace in Traces(root))
        {
            trace.ShouldNotContain(model.Name, Case.Insensitive);
            if (!string.IsNullOrEmpty(model.ShortName))
            {
                trace.ShouldNotContain(model.ShortName, Case.Insensitive);
            }
        }
        var visuals = root.GetVisualDescendants().Concat(root.GetLogicalDescendants().OfType<Visual>()).ToList();
        visuals.OfType<ModelMark>().ShouldAllBe(m => model.Mark == null || !ReferenceEquals(m.Mark, model.Mark));
        visuals.OfType<ArenaIdentityFace>().ShouldAllBe(f => !ReferenceEquals(f.Model, model));
        // Not even as a value waiting on the identity.
        visuals.OfType<ArenaIdentity>().ShouldAllBe(i => !ReferenceEquals(i.Model, model)
                                                        && (i.RevealedAutomationName == null || !i.RevealedAutomationName.Contains(model.Name)));
    }

    #region Layout

    [AvaloniaFact]
    public void The_Head_Is_48_The_Answer_272_And_The_Metrics_8_20_14()
    {
        using var rig = Show(ArenaPaneStatus.Done);
        var head = TestHelpers.Descendant<Border>(rig.Pane, ArenaPane.HeadPart);
        head.Bounds.Height.ShouldBe(48);
        head.Padding.ShouldBe(new Thickness(20, 4, 12, 0));
        head.TranslatePoint(default, rig.Pane)!.Value.Y.ShouldBe(0);
        rig.Answer.Bounds.Height.ShouldBe(272);
        rig.Answer.TranslatePoint(default, rig.Pane)!.Value.Y.ShouldBe(48);
        rig.Answer.Bounds.Width.ShouldBe(392);
        var metrics = rig.Pane.Metrics!;
        metrics.Padding.ShouldBe(new Thickness(20, 8, 20, 14));
        metrics.TranslatePoint(default, rig.Pane)!.Value.Y.ShouldBe(48 + 272);
        rig.Pane.Bounds.Height.ShouldBe(48 + 272 + metrics.Bounds.Height, 0.01);
        rig.Line.TranslatePoint(default, rig.Pane)!.Value.X.ShouldBe(20);

        // The identity on the left of the head, the status pill on the right with the head's 12px inside.
        var identity = rig.Identity.TranslatePoint(default, head)!.Value;
        identity.X.ShouldBe(20);
        var pill = rig.Pill.TranslatePoint(new Point(rig.Pill.Bounds.Width, 0), head)!.Value;
        pill.X.ShouldBe(392 - 12, 0.5);
        // Centered in the 44px under the 4px of padding.
        (rig.Pill.TranslatePoint(new Point(0, rig.Pill.Bounds.Height / 2), head)!.Value.Y).ShouldBe(4 + 22, 0.5);
    }

    [AvaloniaFact]
    public void AnswerHeight_Sets_The_Answer_Area_And_ShowsHeader_Hides_The_Head()
    {
        using var rig = Show(ArenaPaneStatus.Done);
        rig.Pane.AnswerHeight = 200;
        rig.Layout();
        rig.Answer.Bounds.Height.ShouldBe(200);

        rig.Pane.ShowsHeader = false;
        rig.Layout();
        rig.Pane.Classes.ShouldContain(ArenaPane.NoHeaderPseudoClass);
        TestHelpers.Descendant<Border>(rig.Pane, ArenaPane.HeadPart).IsVisible.ShouldBeFalse();
        rig.Answer.TranslatePoint(default, rig.Pane)!.Value.Y.ShouldBe(0);
        rig.Pane.Bounds.Height.ShouldBe(200 + rig.Pane.Metrics!.Bounds.Height, 0.01);
    }

    #endregion

    #region Status and metrics

    [AvaloniaFact]
    public void The_Pill_And_The_Answer_Follow_The_Status()
    {
        using var rig = Show();
        rig.Pill.Status.ShouldBe(ArenaPaneStatus.Idle);
        rig.Answer.IsStreaming.ShouldBeFalse();

        rig.State.Status = ArenaPaneStatus.Thinking;
        rig.Layout();
        rig.Pill.Label.ShouldBe("Thinking");
        rig.Answer.IsStreaming.ShouldBeTrue();
        AutomationProperties.GetItemStatus(rig.Answer).ShouldBe("Thinking");

        rig.State.Status = ArenaPaneStatus.Writing;
        rig.Pill.Label.ShouldBe("Writing");
        AutomationProperties.GetItemStatus(rig.Answer).ShouldBe("Writing");

        rig.State.Status = ArenaPaneStatus.FinishedFirst;
        rig.Pill.Label.ShouldBe("Finished first");
        rig.Pill.IsCompact.ShouldBeFalse();
        rig.Answer.IsStreaming.ShouldBeFalse();
        AutomationProperties.GetItemStatus(rig.Answer).ShouldBeNull();

        rig.State.Status = ArenaPaneStatus.Done;
        rig.Pill.Label.ShouldBe("Done");
        rig.State.Status = ArenaPaneStatus.Stopped;
        rig.Pill.Label.ShouldBe("Stopped");
    }

    [AvaloniaFact]
    public void The_Metric_Line_Shows_The_State_s_Metrics_With_The_Labels()
    {
        using var rig = Show();
        rig.Line.FirstTokenText.ShouldBe("First token");
        rig.Line.SpeedUnitText.ShouldBe("tok/s");
        rig.Line.SpeedName.ShouldBe("Speed");
        rig.Line.CostName.ShouldBe("Cost");
        rig.Line.Placeholder.ShouldBe("–");
        // A pane that never ran shows placeholders, the cost included.
        rig.Line.FirstToken.ShouldBeNull();
        rig.Line.TokensPerSecond.ShouldBeNull();
        rig.Line.CostUsd.ShouldBeNull();

        // A run shows its (input) cost from the start.
        rig.State.Status = ArenaPaneStatus.Thinking;
        rig.Line.CostUsd.ShouldBe(0m);
        rig.State.Metrics = ArenaMetrics.Empty with { CostUsd = 0.0001m };
        rig.Line.CostUsd.ShouldBe(0.0001m);
        rig.Line.FirstToken.ShouldBeNull();

        rig.State.Status = ArenaPaneStatus.Done;
        rig.State.Metrics = ArenaPaneSamples.Finished();
        rig.Line.FirstToken.ShouldBe(TimeSpan.FromSeconds(1.83));
        rig.Line.TokensPerSecond.ShouldBe(152);
        rig.Line.CostUsd.ShouldBe(0.0041m);
        AutomationProperties.GetName(rig.Line).ShouldBe("First token 1.83s, Speed 152 tok/s, Cost $0.0041");
    }

    [AvaloniaFact]
    public void While_Timing_The_Line_Shows_The_Elapsed_Time_In_The_Timing_Style()
    {
        using var rig = Show(ArenaPaneStatus.Thinking);
        rig.Pane.IsTiming = true;
        rig.Pane.Elapsed = TimeSpan.FromSeconds(0.5);
        rig.Line.IsTiming.ShouldBeTrue();
        rig.Line.Classes.ShouldContain(AiMetricLine.TimingPseudoClass);
        rig.Line.FirstToken.ShouldBe(TimeSpan.FromSeconds(0.5));
        rig.Pane.Elapsed = TimeSpan.FromSeconds(0.6);
        rig.Line.FirstTokenDisplay.ShouldBe("0.60s");

        // Without an elapsed time the state's own value (or the placeholder) shows.
        rig.Pane.Elapsed = null;
        rig.Line.FirstToken.ShouldBeNull();

        // The first token arrives: the timer stops and the measured value shows.
        rig.State.Metrics = ArenaPaneSamples.Finished(first: 0.64);
        rig.Pane.IsTiming = false;
        rig.Line.IsTiming.ShouldBeFalse();
        rig.Line.FirstToken.ShouldBe(TimeSpan.FromSeconds(0.64));
    }

    #endregion

    #region Error card

    [AvaloniaFact]
    public void Failed_Completes_The_Text_And_Shows_The_Retry_Card()
    {
        using var rig = Show(ArenaPaneStatus.Writing);
        rig.Pane.Append("Hi Jonas, could we move ");
        rig.State.ErrorTitle = "Connection dropped";
        rig.State.ErrorText = "The stream ended before this answer was complete.";
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Layout();
        rig.Answer.IsCompleted.ShouldBeTrue();
        rig.Answer.IsErrorShown.ShouldBeTrue();
        rig.Answer.ErrorTitle.ShouldBe("Connection dropped");
        rig.Answer.ErrorText.ShouldBe("The stream ended before this answer was complete.");
        rig.Answer.ErrorActionText.ShouldBe("Retry model A");
        rig.Answer.Classes.ShouldContain(ArenaAnswerView.RetryPseudoClass);
        rig.Pane.Classes.ShouldContain(ArenaPane.FailedPseudoClass);
        rig.Pill.Label.ShouldBe("Failed");
        rig.Answer.ErrorCard!.IsVisible.ShouldBeTrue();

        // Text after the failure is not shown.
        rig.Pane.Append("Monday?");
        rig.Answer.Text.ShouldBe("Hi Jonas, could we move ");
    }

    [AvaloniaFact]
    public void A_Card_That_Cannot_Retry_Offers_A_Sample_Prompt_And_Changes_In_Place()
    {
        using var rig = Show(ArenaPaneStatus.Thinking, slot: ArenaSlot.B);
        rig.State.IsRetryable = false;
        rig.State.ErrorTitle = "No demo answer";
        rig.State.ErrorText = "This demo only knows the three sample prompts.";
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Layout();
        rig.Answer.ErrorActionText.ShouldBe("Try a sample prompt");
        rig.Answer.Classes.ShouldNotContain(ArenaAnswerView.RetryPseudoClass);

        rig.State.IsRetryable = true;
        rig.Answer.ErrorActionText.ShouldBe("Retry model B");
        rig.State.ErrorText = "Another reason.";
        rig.Answer.ErrorText.ShouldBe("Another reason.");
        rig.Answer.IsErrorShown.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Without_Error_Texts_The_Card_Uses_The_Labels()
    {
        using var rig = Show(ArenaPaneStatus.Writing);
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Answer.ErrorTitle.ShouldBe("Connection dropped");
        rig.Answer.ErrorText.ShouldBe("The stream ended before this answer was complete.");
    }

    [AvaloniaFact]
    public void Leaving_Failed_Hides_The_Card_And_Reset_Leaves_The_State_Alone()
    {
        using var rig = Show(ArenaPaneStatus.Writing);
        rig.Pane.Append("Partial answer");
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Answer.IsErrorShown.ShouldBeTrue();

        // A retry: the status goes back to Thinking and the card closes; the text stays until the owner resets.
        rig.State.Status = ArenaPaneStatus.Thinking;
        rig.Layout();
        rig.Answer.IsErrorShown.ShouldBeFalse();
        rig.Pane.Classes.ShouldNotContain(ArenaPane.FailedPseudoClass);
        rig.Pane.Text.ShouldBe("Partial answer");

        rig.Pane.Reset();
        rig.Pane.Text.ShouldBeEmpty();
        rig.Answer.Text.ShouldBeEmpty();
        rig.Answer.IsCompleted.ShouldBeFalse();
        rig.State.Status.ShouldBe(ArenaPaneStatus.Thinking);
        rig.Pane.Append("A fresh answer.");
        rig.Answer.Text.ShouldBe("A fresh answer.");
    }

    [AvaloniaFact]
    public void Reset_Clears_A_Failed_Pane_At_Once()
    {
        using var rig = Show(ArenaPaneStatus.Writing, level: ArcMotionLevel.Full);
        rig.Pane.Append("Partial answer");
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Frames(40);
        rig.Answer.ErrorHost!.CurrentHeight.ShouldBeGreaterThan(100);

        rig.Pane.Reset();
        rig.Layout();
        rig.Answer.IsErrorShown.ShouldBeFalse();
        rig.Answer.ErrorHost.CurrentHeight.ShouldBe(0);
        rig.Answer.Text.ShouldBeEmpty();
        rig.State.Status.ShouldBe(ArenaPaneStatus.Failed);
    }

    [AvaloniaFact]
    public void The_Card_s_Button_Raises_ErrorActionRequested()
    {
        using var rig = Show(ArenaPaneStatus.Writing);
        var raised = new List<RoutedEventArgs>();
        var bubbled = 0;
        rig.Pane.ErrorActionRequested += (_, e) => raised.Add(e);
        rig.Window.AddHandler(ArenaAnswerView.ActionRequestedEvent, (_, _) => bubbled++);
        rig.State.Status = ArenaPaneStatus.Failed;
        rig.Layout();
        var button = rig.Answer.ErrorAction!;
        button.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Layout();
        raised.ShouldHaveSingleItem().Source.ShouldBe(rig.Pane);
        bubbled.ShouldBe(0);
    }

    #endregion

    #region Privacy and names

    [AvaloniaFact]
    public void The_Pane_Is_A_Group_Named_By_Its_Slot_With_A_Named_Answer_And_Metrics()
    {
        foreach (var slot in new[] { ArenaSlot.A, ArenaSlot.B })
        {
            using var rig = Show(ArenaPaneStatus.Done, slot: slot);
            var letter = slot == ArenaSlot.A ? "A" : "B";
            var peer = ControlAutomationPeer.CreatePeerForElement(rig.Pane);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Model " + letter);
            ControlAutomationPeer.CreatePeerForElement(rig.Answer).GetName().ShouldBe("Model " + letter + " answer");
            var metrics = ControlAutomationPeer.CreatePeerForElement(rig.Pane.Metrics!);
            metrics.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            metrics.GetName().ShouldBe("Model " + letter + " metrics");
            metrics.IsControlElement().ShouldBeTrue();
            // The line keeps its own spoken text inside the named group.
            metrics.GetChildren().ShouldHaveSingleItem().GetName().ShouldStartWith("First token");
            AutomationProperties.GetName(rig.Identity).ShouldBe("Model " + letter);
        }
    }

    [AvaloniaFact]
    public void Blind_Nothing_In_The_Trees_Names_The_Model_In_Any_State()
    {
        using var rig = Show();
        var model = rig.State.Model!;
        foreach (var status in Enum.GetValues<ArenaPaneStatus>())
        {
            rig.State.Status = status;
            rig.State.Metrics = ArenaPaneSamples.Finished();
            rig.Layout();
            ShouldNotLeak(rig.Pane, model);
            AutomationProperties.GetName(rig.Pane).ShouldBe("Model A");
        }
        rig.Pane.ShownModel.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Revealing_Shows_The_Model_And_Names_The_Pane_After_The_Turn()
    {
        using var rig = Show(ArenaPaneStatus.Done);
        var model = rig.State.Model!;
        rig.Pane.IsRevealed = true;
        // Not yet: showing waits for the end of the dispatcher turn.
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model A");
        ShouldNotLeak(rig.Pane, model);

        rig.Layout();
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model A, Lumen 3 Pro");
        rig.Pane.Classes.ShouldContain(ArenaPane.RevealedPseudoClass);
        rig.Identity.Model.ShouldBe(model);
        rig.Identity.RevealedAutomationName.ShouldBe("Model A, Lumen 3 Pro");
        rig.Identity.ShowsModel.ShouldBeTrue();
        rig.Identity.Face!.Text.ShouldBe("Lumen 3 Pro");

        // Hiding again is immediate.
        rig.Pane.IsRevealed = false;
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model A");
        rig.Identity.Model.ShouldBeNull();
        rig.Identity.RevealedAutomationName.ShouldBeNull();
        rig.Layout();
        ShouldNotLeak(rig.Pane, model);
    }

    [AvaloniaFact]
    public void A_New_Model_And_A_Hide_In_One_Turn_Never_Name_The_New_Model()
    {
        foreach (var modelFirst in new[] { true, false })
        {
            using var rig = Show(ArenaPaneStatus.Done, revealed: true, level: ArcMotionLevel.Full);
            rig.Identity.ShowsModel.ShouldBeTrue();
            var next = new AiModel { Id = "vireo-4", Name = "Vireo 4", ProviderId = "vireo", Mark = ArenaStatusSamples.Kite().Mark };
            var names = new List<string?>();
            rig.Pane.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationProperties.NameProperty)
                {
                    names.Add(AutomationProperties.GetName(rig.Pane));
                }
            };
            if (modelFirst)
            {
                rig.State.Model = next;
                ShouldNotLeak(rig.Pane, next);
                rig.Pane.IsRevealed = false;
            }
            else
            {
                rig.Pane.IsRevealed = false;
                rig.State.Model = next;
            }
            ShouldNotLeak(rig.Pane, next);
            rig.Frames(30);
            ShouldNotLeak(rig.Pane, next);
            names.ShouldAllBe(n => n == null || !n.Contains("Vireo"));
            AutomationProperties.GetName(rig.Pane).ShouldBe("Model A");
        }
    }

    [AvaloniaFact]
    public void A_Pane_Created_Revealed_Shows_Its_Model_At_Once()
    {
        using var rig = Show(ArenaPaneStatus.Done, revealed: true);
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model A, Lumen 3 Pro");
        rig.Identity.Face!.ShowsModel.ShouldBeTrue();
        rig.Identity.Face.Text.ShouldBe("Lumen 3 Pro");
    }

    [AvaloniaFact]
    public void With_Blind_Off_A_New_Round_s_Model_Shows_After_The_Turn()
    {
        using var rig = Show(ArenaPaneStatus.Done, revealed: true);
        var next = ArenaStatusSamples.Kite();
        rig.State.Model = next;
        rig.Layout();
        rig.Identity.Face!.Text.ShouldBe("Kite 5");
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model A, Kite 5");
    }

    #endregion

    #region Text

    [AvaloniaFact]
    public async Task Text_Appended_While_Hidden_From_The_Start_Is_There_When_Shown()
    {
        var pane = ArenaPaneSamples.Pane(ArenaPaneSamples.State(status: ArenaPaneStatus.Writing));
        pane.IsVisible = false;
        using var rig = new Rig(pane);
        pane.Answer.ShouldBeNull();
        pane.Append("Written while ");
        pane.Append("the pane was hidden.");
        rig.Layout();

        pane.IsVisible = true;
        rig.Layout();
        rig.Answer.Text.ShouldBe("Written while the pane was hidden.");
        await rig.Parsed();
        rig.Answer.Body!.Stream!.Document.GetText(0, 100).ShouldBe("Written while the pane was hidden.");
        AutomationProperties.GetName(rig.Answer).ShouldBe("Model A answer");
    }

    [AvaloniaFact]
    public async Task Text_Appended_After_Hiding_Is_There_And_At_The_Bottom_When_Shown_Again()
    {
        using var rig = Show(ArenaPaneStatus.Writing, level: ArcMotionLevel.Full);
        rig.Pane.Append("The opening line.\n\n");
        await rig.Parsed();
        rig.Pane.IsVisible = false;
        rig.Layout();
        rig.Pane.Append(ArenaPaneSamples.LongAnswer);
        rig.Pane.Append("\n\nThe closing line.");
        // Parsed while hidden (the stream keeps going in the background).
        await rig.Parsed();
        var scroll = rig.Answer.Scroll!;
        scroll.Offset.Y.ShouldBe(0);

        rig.Pane.IsVisible = true;
        rig.Layout();
        rig.Answer.Text.ShouldEndWith("The closing line.");
        rig.Answer.Body!.Stream!.Document.GetText(0, 10_000).ShouldEndWith("The closing line.");
        scroll.Extent.Height.ShouldBeGreaterThan(scroll.Viewport.Height + 100);
        // It lands at the bottom with the first layout, without a chase from the old offset.
        scroll.Offset.Y.ShouldBe(scroll.Extent.Height - scroll.Viewport.Height, 1);
        rig.Answer.Follow!.IsChasing.ShouldBeFalse();
        rig.Answer.IsFollowing.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Text_Appended_Before_The_Template_Keeps_Its_Completion()
    {
        var pane = ArenaPaneSamples.Pane(ArenaPaneSamples.State(status: ArenaPaneStatus.Done));
        pane.Append(ArenaPaneSamples.ShortAnswer);
        pane.Complete();
        using var rig = new Rig(pane);
        rig.Answer.Text.ShouldBe(ArenaPaneSamples.ShortAnswer);
        rig.Answer.IsCompleted.ShouldBeTrue();
        await rig.Parsed();
        rig.Answer.Body!.Stream!.Document.IsComplete.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Failed_State_Before_The_Template_Shows_The_Card_Open()
    {
        var state = ArenaPaneSamples.State(status: ArenaPaneStatus.Failed);
        state.IsRetryable = false;
        var pane = ArenaPaneSamples.Pane(state);
        pane.Append("Partial");
        using var rig = new Rig(pane, ArcMotionLevel.Full, settleFrames: 1);
        rig.Answer.IsErrorShown.ShouldBeTrue();
        rig.Answer.ErrorActionText.ShouldBe("Try a sample prompt");
        rig.Answer.IsCompleted.ShouldBeTrue();
        // Open from the first frame, not opening.
        rig.Answer.ErrorHost!.IsMoving.ShouldBeFalse();
        rig.Answer.ErrorHost.CurrentHeight.ShouldBeGreaterThan(100);
    }

    [AvaloniaFact]
    public void FocusAnswer_Focuses_The_Answer()
    {
        using var rig = Show(ArenaPaneStatus.Done);
        rig.Pane.FocusAnswer().ShouldBeTrue();
        rig.Answer.IsFocused.ShouldBeTrue();
        rig.Answer.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Without_Labels_The_Pane_Shows_No_Words()
    {
        var pane = ArenaPaneSamples.Pane(ArenaPaneSamples.State(status: ArenaPaneStatus.Failed));
        pane.Labels = null;
        using var rig = new Rig(pane);
        rig.Pill.Label.ShouldBeEmpty();
        AutomationProperties.GetName(rig.Pane).ShouldBeNull();
        rig.Answer.ErrorTitle.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_New_State_Is_Followed_And_The_Old_One_Is_Let_Go()
    {
        using var rig = Show(ArenaPaneStatus.Done);
        var old = rig.State;
        var next = ArenaPaneSamples.State(ArenaSlot.B, status: ArenaPaneStatus.Writing);
        rig.Pane.State = next;
        rig.Layout();
        rig.Pill.Label.ShouldBe("Writing");
        AutomationProperties.GetName(rig.Pane).ShouldBe("Model B");
        old.Status = ArenaPaneStatus.Failed;
        rig.Pill.Label.ShouldBe("Writing");
        rig.Answer.IsErrorShown.ShouldBeFalse();
    }

    #endregion
}

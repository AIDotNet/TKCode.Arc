using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The side panel's thread: the turn state machine, the streaming cadence, stop, failure and retry, suggestions.</summary>
public class AiSidePanelTests
{
    private static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string WordsText(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => "word" + i)) + ".";

    #region Rest

    [AvaloniaFact]
    public void Empty_thread_shows_the_title_the_line_and_every_suggestion()
    {
        using var rig = new AiSidePanelRig();
        var panel = rig.Panel;
        panel.EmptyState!.IsVisible.ShouldBeTrue();
        panel.Classes.Contains(":empty").ShouldBeTrue();
        panel.EmptySuggestions!.Prompts.ShouldBe(AiSidePanelSamples.Suggestions);
        panel.NameText!.Text.ShouldBe("Helper");
        panel.ModelText!.Text.ShouldBe("Model X1");
        panel.PagePill!.Text.ShouldBe("This page");
        rig.Box.Placeholder.ShouldBe("Ask about this page");
        panel.IsBusy.ShouldBeFalse();
        panel.FollowingSuggestions.ShouldBeNull();
        rig.Box.SendButton!.State.ShouldBe(AiSendState.Empty);
    }

    [AvaloniaFact]
    public void Empty_title_names_the_page_and_follows_a_new_title()
    {
        using var rig = new AiSidePanelRig();
        var title = TestHelpers.Descendant<Avalonia.Controls.TextBlock>(rig.Panel, AiSidePanel.EmptyTitlePart);
        title.Text.ShouldBe("Ask about Offline sync");
        rig.Panel.Page!.Title = "Sync v2";
        rig.Settle();
        title.Text.ShouldBe("Ask about Sync v2");
        var text = TestHelpers.Descendant<Avalonia.Controls.TextBlock>(rig.Panel, AiSidePanel.EmptyTextPart);
        text.Text.ShouldBe("Helper reads this page and suggests edits. The page only changes when you apply one.");
    }

    #endregion

    #region Reading and streaming

    [AvaloniaFact]
    public void Sending_from_the_box_adds_a_reading_turn_and_clears_the_box()
    {
        using var rig = new AiSidePanelRig();
        rig.Send("What changed?");
        rig.Panel.Turns.Count.ShouldBe(1);
        var turn = rig.LastTurn;
        turn.Prompt.ShouldBe("What changed?");
        turn.State.ShouldBe(AiPanelTurnState.Reading);
        rig.Panel.IsBusy.ShouldBeTrue();
        rig.Box.Text.ShouldBeNullOrEmpty();
        rig.Box.IsBusy.ShouldBeTrue();
        rig.Box.SendButton!.State.ShouldBe(AiSendState.Generating);
        rig.LastView.Reading!.IsVisible.ShouldBeTrue();
        rig.LastView.Reading!.Text.ShouldBe("Reading the page");
        rig.Announcement.ShouldBe("Model X1 is reading the page");
        rig.Call.Request.Prompt.ShouldBe("What changed?");
        rig.Call.Request.Page.ShouldBeSameAs(rig.Panel.Page);
        rig.Call.Request.Selection.ShouldBeNull();
        rig.Panel.EmptyState!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Reading_lasts_at_least_the_minimum_even_when_the_reply_is_immediate()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Summarize this note");
        rig.Settle();
        rig.Call.ReplyText("Short answer here.");
        rig.Run(0.5);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Reading);
        rig.LastTurn.Answer.ShouldBeEmpty();
        rig.RunUntil(() => rig.LastTurn.State != AiPanelTurnState.Reading, 0.3).ShouldBeTrue();
        rig.Panel.StreamClock.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(600 - 1);
    }

    [AvaloniaFact]
    public void Answer_is_released_in_whole_word_steps_of_four_to_seven_every_fifty_ms()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Explain");
        rig.Settle();
        var call = rig.Call.Stream();
        call.Push(WordsText(60));
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        var counts = new List<int> { Words(rig.LastTurn.Answer) };
        for (var i = 0; i < 6; i++)
        {
            // Three 60 Hz frames: the next step (50ms).
            rig.Frame();
            rig.Frame();
            rig.Frame();
            counts.Add(Words(rig.LastTurn.Answer));
        }
        counts[0].ShouldBe(4);
        for (var i = 1; i < counts.Count; i++)
        {
            (counts[i] - counts[i - 1]).ShouldBeInRange(4, 7);
        }
        // Never part of a word.
        rig.LastTurn.Answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).ShouldAllBe(w => w.StartsWith("word"));
    }

    [AvaloniaFact]
    public void A_long_backlog_is_caught_up_at_the_most_words_a_step()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Explain");
        rig.Settle();
        rig.Call.ReplyText(WordsText(400));
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        var before = Words(rig.LastTurn.Answer);
        rig.Frame();
        rig.Frame();
        rig.Frame();
        (Words(rig.LastTurn.Answer) - before).ShouldBe(7);
    }

    [AvaloniaFact]
    public void Turn_is_done_after_the_stream_ended_and_the_backlog_was_shown()
    {
        using var rig = new AiSidePanelRig();
        var task = rig.Panel.AskAsync("Explain");
        rig.Settle();
        var call = rig.Call.Stream();
        call.Push("One two three four five six seven eight nine ten.");
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        rig.Run(1);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Streaming);
        rig.Panel.IsBusy.ShouldBeTrue();
        call.End();
        rig.RunUntil(() => !rig.Panel.IsBusy).ShouldBeTrue();
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Done);
        rig.LastTurn.Answer.ShouldBe("One two three four five six seven eight nine ten.");
        rig.Announcement.ShouldBe("Reply ready");
        rig.Box.SendButton!.State.ShouldBe(AiSendState.Empty);
        task.IsCompleted.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_blank_line_starts_a_new_paragraph()
    {
        using var rig = new AiSidePanelRig();
        var turn = rig.Answer("Summarize this note", "First paragraph here.\n\nSecond one\nwith a line break.");
        turn.State.ShouldBe(AiPanelTurnState.Done);
        var answer = rig.LastView.Answer!;
        answer.IsVisible.ShouldBeTrue();
        answer.Paragraphs.Select(p => p.Text).ShouldBe(["First paragraph here.", "Second one\nwith a line break."]);
        answer.Spacing.ShouldBe(21.7, 0.01);
    }

    [AvaloniaFact]
    public void Steps_fade_in_at_full_motion_only()
    {
        using (var rig = new AiSidePanelRig(level: ArcMotionLevel.Full))
        {
            _ = rig.Panel.AskAsync("Explain");
            rig.Settle();
            rig.Call.ReplyText(WordsText(30));
            rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
            rig.LastView.Answer!.Paragraphs[0].Fades.IsActive.ShouldBeTrue();
        }
        using (var rig = new AiSidePanelRig(level: ArcMotionLevel.Reduced))
        {
            _ = rig.Panel.AskAsync("Explain");
            rig.Settle();
            rig.Call.ReplyText(WordsText(30));
            rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
            rig.LastView.Answer!.Paragraphs[0].Fades.IsActive.ShouldBeFalse();
            // Still in steps.
            Words(rig.LastTurn.Answer).ShouldBe(4);
        }
    }

    [AvaloniaFact]
    public void A_streamed_step_does_not_lay_out_the_earlier_turns()
    {
        using var rig = new AiSidePanelRig();
        rig.Answer("First", "An earlier answer.");
        var first = rig.Views[0];
        _ = rig.Panel.AskAsync("Second");
        rig.Settle();
        rig.Call.ReplyText(WordsText(80));
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        rig.Run(0.1);
        var measured = first.MeasureCount;
        rig.Run(0.3);
        first.MeasureCount.ShouldBe(measured);
    }

    #endregion

    #region Stop

    [AvaloniaFact]
    public void Stop_keeps_the_shown_text_and_makes_no_card()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Shorten the summary");
        rig.Settle();
        var call = rig.Call.Stream(new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Text = "Short." }));
        call.Push(WordsText(40));
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        rig.Frame();
        rig.Frame();
        rig.Frame();
        var shown = rig.LastTurn.Answer;
        rig.Panel.Stop();
        rig.Settle();
        call.IsCancelled.ShouldBeTrue();
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Stopped);
        rig.LastTurn.Answer.ShouldBe(shown);
        rig.LastTurn.Change.ShouldBeNull();
        rig.LastView.StoppedLine!.IsVisible.ShouldBeTrue();
        rig.LastView.Card.ShouldBeNull();
        rig.Panel.IsBusy.ShouldBeFalse();
        rig.Announcement.ShouldBe("Reply stopped");
        rig.Run(1);
        rig.LastTurn.Answer.ShouldBe(shown);
    }

    [AvaloniaFact]
    public void The_stop_button_and_Escape_in_the_box_stop_while_reading()
    {
        using var rig = new AiSidePanelRig();
        rig.Send("First");
        rig.Click(rig.Box.SendButton!);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Stopped);
        rig.LastTurn.Answer.ShouldBeEmpty();
        rig.Panel.IsOpen.ShouldBeTrue();

        rig.Send("Second");
        rig.Box.FocusInput();
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Stopped);
        // Escape stopped the answer; it did not close the panel.
        rig.Panel.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Enter_while_busy_neither_sends_nor_clears_the_text()
    {
        using var rig = new AiSidePanelRig();
        rig.Send("First");
        rig.Window.KeyTextInput("Next one");
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        rig.Panel.Turns.Count.ShouldBe(1);
        rig.Box.Text.ShouldBe("Next one");
        rig.Responder!.Calls.Count.ShouldBe(1);
    }

    #endregion

    #region Failure and retry

    [AvaloniaFact]
    public void A_failure_shows_the_error_card_after_the_reading_minimum()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("When does it ship");
        rig.Settle();
        rig.Call.Fail("Connection timed out");
        rig.Run(0.3);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Reading);
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Failed, 1).ShouldBeTrue();
        rig.LastTurn.ErrorMessage.ShouldBe("Connection timed out");
        var card = rig.LastView.ErrorCard!;
        card.IsVisible.ShouldBeTrue();
        card.Title.ShouldBe("Helper could not answer");
        card.Detail.ShouldBe("Connection timed out");
        card.RetryText.ShouldBe("Retry");
        rig.Panel.IsBusy.ShouldBeFalse();
        rig.Announcement.ShouldBe("Model X1 could not answer: Connection timed out. You can retry.");
        rig.Panel.FollowingSuggestions.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_failure_in_the_stream_keeps_the_text_shown()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Explain");
        rig.Settle();
        var call = rig.Call.Stream();
        call.Push(WordsText(8));
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Streaming, 2).ShouldBeTrue();
        call.FailStream("Lost the connection");
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Failed, 1).ShouldBeTrue();
        rig.LastTurn.Answer.ShouldNotBeEmpty();
        rig.LastView.Answer!.IsVisible.ShouldBeTrue();
        rig.LastView.ErrorCard!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Retry_runs_the_same_question_again_about_the_page_as_it_is()
    {
        using var rig = new AiSidePanelRig();
        rig.AskAboutBlock("scope");
        _ = rig.Panel.AskAsync("What does this mean");
        rig.Settle();
        var first = rig.Call;
        first.Fail("Connection timed out");
        rig.RunUntil(() => rig.LastTurn.State == AiPanelTurnState.Failed, 2).ShouldBeTrue();
        rig.Panel.ClearSelection();
        rig.Click(rig.LastView.ErrorCard!.RetryButton!);
        rig.Responder!.Calls.Count.ShouldBe(2);
        var second = rig.Call;
        second.Request.Prompt.ShouldBe("What does this mean");
        second.Request.Selection.ShouldBe(first.Request.Selection);
        rig.Panel.Turns.Count.ShouldBe(1);
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Reading);
        rig.LastView.ErrorCard!.IsVisible.ShouldBeFalse();
        rig.LastView.Reading!.IsVisible.ShouldBeTrue();
        second.ReplyText("It is about caching.");
        rig.RunUntil(() => !rig.Panel.IsBusy).ShouldBeTrue();
        rig.LastTurn.State.ShouldBe(AiPanelTurnState.Done);
        rig.LastTurn.Answer.ShouldBe("It is about caching.");
    }

    #endregion

    #region Suggestions

    [AvaloniaFact]
    public void Unused_suggestions_follow_each_answer_and_a_new_question_removes_them()
    {
        using var rig = new AiSidePanelRig();
        rig.Click(rig.Panel.EmptySuggestions!.Pills.First());
        rig.LastTurn.Prompt.ShouldBe("Summarize this note");
        rig.Panel.FollowingSuggestions.ShouldBeNull();
        rig.Call.ReplyText("A summary.");
        rig.RunUntil(() => !rig.Panel.IsBusy).ShouldBeTrue();
        rig.Panel.FollowingSuggestions!.Prompts.ShouldBe(["Shorten the summary", "Add a sync interval column"]);
        rig.Panel.TurnsPanel!.Children[^1].ShouldBeSameAs(rig.Panel.FollowingSuggestions);

        _ = rig.Panel.AskAsync("Something else");
        rig.Settle();
        rig.Panel.FollowingSuggestions.ShouldBeNull();
        rig.Call.ReplyText("Sure.");
        rig.RunUntil(() => !rig.Panel.IsBusy).ShouldBeTrue();
        rig.Panel.FollowingSuggestions!.Prompts.ShouldBe(["Shorten the summary", "Add a sync interval column"]);

        rig.Answer("Shorten the summary", "Done.");
        rig.Answer("Add a sync interval column", "Done.");
        rig.Panel.FollowingSuggestions.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Suggestions_follow_a_stopped_answer()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Summarize this note");
        rig.Settle();
        rig.Panel.Stop();
        rig.Settle();
        rig.Panel.FollowingSuggestions!.Prompts.ShouldBe(["Shorten the summary", "Add a sync interval column"]);
    }

    #endregion

    #region Without a responder

    [AvaloniaFact]
    public void Without_a_responder_nothing_is_sent()
    {
        using var rig = new AiSidePanelRig(responder: false);
        rig.Box.CanSend.ShouldBeFalse();
        rig.Send("Hello");
        rig.Panel.Turns.ShouldBeEmpty();
        rig.Panel.AskAsync("Hello").IsCompleted.ShouldBeTrue();
        rig.Panel.Turns.ShouldBeEmpty();
    }

    #endregion

    #region Restored turns

    [AvaloniaFact]
    public void Turns_the_host_adds_show_as_they_are()
    {
        using var rig = new AiSidePanelRig();
        var turn = new AiPanelTurn { Prompt = "Earlier", Answer = "An answer from before.", State = AiPanelTurnState.Done, ContextLabel = "Scope" };
        rig.Panel.Turns.Add(turn);
        rig.Settle();
        var view = rig.LastView;
        view.Answer!.Paragraphs.Single().Text.ShouldBe("An answer from before.");
        view.ContextLine!.IsVisible.ShouldBeTrue();
        view.Prompt!.Text.ShouldBe("Earlier");
        rig.Panel.FollowingSuggestions.ShouldNotBeNull();
        rig.Panel.Turns.Clear();
        rig.Settle();
        rig.Panel.TurnViews.ShouldBeEmpty();
        rig.Panel.EmptyState!.IsVisible.ShouldBeTrue();
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Lists_and_controls_are_named()
    {
        using var rig = new AiSidePanelRig();
        AutomationProperties.GetName(rig.Panel.TurnsPanel!).ShouldBe("Conversation");
        AutomationProperties.GetName(rig.Panel.CloseButton!).ShouldBe("Close assistant");
        AutomationProperties.GetName(rig.Input).ShouldBe("Message");
        rig.Box.SendButton!.SendName.ShouldBe("Send message");
        rig.Box.SendButton!.StopName.ShouldBe("Stop");
        AutomationProperties.GetName(rig.Panel).ShouldBe("Helper panel");
    }

    #endregion
}

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The answer page: structure, the empty page, asking and the status machine of one answer.</summary>
public class AiAnswerTests
{
    [AvaloniaFact]
    public void An_Empty_Page_Shows_Its_Heading_Line_And_Suggestions_With_New_Thread_And_Share_Disabled()
    {
        using var rig = new AiAnswerRig();
        var answer = rig.Answer;
        Assert.Equal(AiAnswerStatus.Empty, answer.Status);
        Assert.False(answer.IsBusy);
        Assert.True(answer.EmptyPage!.IsVisible);
        Assert.False(answer.Article!.IsVisible);
        Assert.Equal("New thread", answer.TitleText);
        Assert.Equal("Ask a question", answer.EmptyTitleText!.Text);
        Assert.Equal(4, answer.SuggestionList!.Rows.Count);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, answer.SuggestionList.Rows[0].Question);
        Assert.False(answer.NewThreadButton!.IsEnabled);
        Assert.False(answer.ShareButton!.IsEnabled);
        Assert.True(answer.ShareButton.IsVisible);
        Assert.Equal("Ask anything", rig.Composer.Placeholder);
        Assert.Equal("AI answer", AutomationProperties.GetName(answer));
        Assert.Equal("Suggested questions", answer.SuggestionList.ListName);
    }

    [AvaloniaFact]
    public void Asking_Replaces_The_Page_And_Thinks()
    {
        using var rig = new AiAnswerRig();
        var answer = rig.Answer;
        _ = answer.AskAsync("  " + AiAnswerPageSamples.MoveBuilds + " ");
        rig.Settle();
        Assert.Equal((AiAnswerPageSamples.MoveBuilds, AiAnswerQuestionOrigin.Programmatic), rig.Asked.Single());
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, answer.Question);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, answer.TitleText);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, answer.QuestionHeading!.Text);
        Assert.Equal(AiAnswerStatus.Thinking, answer.Status);
        Assert.True(answer.IsBusy);
        Assert.True(rig.Composer.IsGenerating);
        Assert.Equal("Thinking", rig.Announcement);
        Assert.Equal("Thinking", AutomationProperties.GetItemStatus(answer));
        Assert.True(answer.Article!.IsVisible);
        Assert.False(answer.EmptyPage!.IsVisible);
        Assert.True(answer.NewThreadButton!.IsEnabled);
        Assert.Equal("Ask a follow-up", rig.Composer.Placeholder);
        var request = rig.Call.Request;
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, request.Question);
        Assert.Equal(AiAnswerMode.Answer, request.Mode);
        Assert.Equal("lumen-pro", request.Model?.Id);
        Assert.Null(request.PreviousAnswerMarkdown);
        Assert.Single(answer.Versions);
        Assert.Equal(0, answer.CurrentVersionIndex);
        // Thinking: the reasoning row shimmers, no sources yet, the footer keeps its place but shows nothing, no related.
        Assert.True(rig.Reasoning.IsVisible);
        Assert.True(rig.Reasoning.IsThinking);
        Assert.False(rig.SourcesRow.IsVisible);
        Assert.True(rig.Actions.IsBusy);
        Assert.True(answer.Footer!.IsVisible);
        Assert.False(answer.Related!.IsVisible);
        Assert.Equal(new[] { AiAnswerStatus.Thinking }, rig.Statuses);
    }

    [AvaloniaFact]
    public void Early_Sources_Fill_The_Row_While_It_Thinks_Numbered_From_One()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.BlockingChecks);
        rig.Settle();
        var unnumbered = new[] { new AiSource { Title = "A" }, new AiSource { Title = "B" }, new AiSource { Title = "C", Index = 7 } };
        rig.Call.Sources(unnumbered);
        rig.Settle();
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
        Assert.True(rig.SourcesRow.IsVisible);
        Assert.Equal("3 sources", rig.SourcesRow.LabelText);
        Assert.Equal(new[] { 1, 2, 7 }, rig.Answer.Versions[0].Sources.Select(s => s.Index));
        Assert.Same(rig.Answer.Versions[0].Sources, rig.SourcesRow.Sources);
        rig.Call.Sources(new AiSource { Title = "D" });
        rig.Settle();
        Assert.Equal("4 sources", rig.SourcesRow.LabelText);
        Assert.Equal(4, rig.Answer.Versions[0].Sources[3].Index);
    }

    [AvaloniaFact]
    public void Reasoning_Streams_Into_The_Closed_Panel_And_Shows_Live_When_Opened()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.MoveBuilds);
        rig.Settle();
        rig.Call.Reasoning("The risk is not the new pipeline but switching ");
        rig.Run(0.5);
        Assert.Equal("The risk is not the new pipeline but switching ", rig.Answer.Versions[0].Reasoning);
        Assert.StartsWith("The risk is not", rig.Reasoning.Reasoning);
        Assert.False(rig.Answer.IsReasoningExpanded);
        rig.Click(rig.Reasoning.Button!);
        Assert.True(rig.Answer.IsReasoningExpanded);
        Assert.True(rig.Reasoning.Body!.IsExpanded);
        rig.Call.Reasoning("required checks too early.");
        rig.Run(0.5);
        var text = rig.Find<AiAnswerReasoningText>()!;
        Assert.Contains("switching required", string.Join(" ", text.Paragraphs));
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void The_Thought_Event_Ends_Thinking_And_Writing_Starts()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.MoveBuilds);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(8.4));
        rig.Settle();
        Assert.Equal(AiAnswerStatus.Writing, rig.Answer.Status);
        Assert.False(rig.Reasoning.IsThinking);
        Assert.Equal("Thought for 8s", rig.Reasoning.ThoughtText);
        Assert.Equal("Writing the answer", rig.Announcement);
        Assert.Equal("Writing the answer", AutomationProperties.GetItemStatus(rig.Answer));
    }

    [AvaloniaFact]
    public void Without_A_Thought_Event_The_Time_To_The_First_Text_Counts_At_Least_One_Second()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.MoveBuilds);
        rig.Settle();
        rig.Time.Advance(TimeSpan.FromSeconds(3.4));
        rig.Call.Text("Keep both ");
        rig.Settle();
        Assert.Equal("Thought for 3s", rig.Reasoning.ThoughtText);
        Assert.Equal(AiAnswerStatus.Writing, rig.Answer.Status);

        _ = rig.Answer.AskAsync(AiAnswerPageSamples.BuildTimes);
        rig.Settle();
        rig.Time.Advance(TimeSpan.FromMilliseconds(120));
        rig.Call.Thought();
        rig.Settle();
        Assert.Equal("Thought for 1s", rig.Reasoning.ThoughtText);
    }

    [AvaloniaFact]
    public void The_Answer_Is_Ready_Only_When_The_Stream_Ended_And_The_Text_Was_Shown()
    {
        using var rig = new AiAnswerRig();
        var call = rig.AnswerFirst(finish: false);
        rig.Settle();
        Assert.Equal(AiAnswerStatus.Writing, rig.Answer.Status);
        Assert.True(rig.Document.IsRevealing);
        // Pacing: the text is not all shown yet.
        Assert.True(rig.Document.RevealedMarkdown.Length < AiAnswerSamples.Answer.Length);
        call.End();
        Assert.Equal(AiAnswerStatus.Writing, rig.Answer.Status);
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 30));
        var version = rig.Answer.Versions[0];
        Assert.Equal(AiAnswerSamples.Answer, version.Markdown);
        Assert.Equal(AiAnswerOutcome.Complete, version.Outcome);
        Assert.Equal(new AiAnswerUsage(812, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(18.6)), version.Usage);
        Assert.Equal(3, version.FollowUps.Count);
        Assert.False(rig.Answer.IsBusy);
        Assert.False(rig.Composer.IsGenerating);
        Assert.Equal("Answer ready", rig.Answer.LastStatusAnnouncement);
        Assert.Null(AutomationProperties.GetItemStatus(rig.Answer));
        Assert.False(rig.Actions.IsBusy);
        Assert.True(rig.Answer.Related!.IsVisible);
        Assert.Equal(3, rig.Answer.Related.Rows.Count);
        Assert.Equal("Lumen Pro 4", rig.Answer.ModelLineView!.Model?.Name);
        Assert.Equal("812 tokens", rig.Answer.ModelLineView.TokensText);
        Assert.Equal("18.6s", rig.Answer.ModelLineView.DurationText);
        Assert.False(rig.Answer.ModelLineView.IsStopped);
        Assert.False(rig.Answer.Note!.IsVisible);
        Assert.Equal(new[] { AiAnswerStatus.Thinking, AiAnswerStatus.Writing, AiAnswerStatus.Ready }, rig.Statuses);
    }

    [AvaloniaFact]
    public void AskAsync_Completes_When_The_Answer_Is_Ready()
    {
        using var rig = new AiAnswerRig();
        var task = rig.Answer.AskAsync(AiAnswerPageSamples.BuildTimes);
        rig.Settle();
        Assert.False(task.IsCompleted);
        rig.Call.Text("Warm caches first. [2]").Done(100).End();
        rig.RunUntil(() => task.IsCompleted, 10);
        Assert.True(task.IsCompleted);
        Assert.Equal(AiAnswerStatus.Ready, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void Stopping_Keeps_What_Was_Shown_And_Offers_Try_Again()
    {
        using var rig = new AiAnswerRig();
        var call = rig.AnswerFirst(finish: false);
        rig.Run(0.3);
        var shown = rig.Document.RevealedMarkdown;
        Assert.NotEmpty(shown);
        rig.Answer.Stop();
        rig.Settle();
        Assert.True(call.IsCancelled);
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);
        var version = rig.Answer.Versions[0];
        Assert.Equal(AiAnswerOutcome.Stopped, version.Outcome);
        Assert.Equal(shown, version.Markdown);
        Assert.Equal("Answer stopped", rig.Answer.LastStatusAnnouncement);
        Assert.True(rig.Answer.Note!.IsVisible);
        Assert.Equal(AiStatusNoteTone.Stopped, rig.Answer.Note.Tone);
        Assert.Equal("Stopped. This answer may be incomplete.", rig.Answer.Note.Text);
        Assert.Equal("Try again", rig.Answer.Note.ActionText);
        Assert.True(rig.Answer.ModelLineView!.IsStopped);
        Assert.False(rig.Actions.IsBusy);
        // A stopped answer shows its related questions too.
        Assert.True(rig.Answer.Related!.IsVisible);
        Assert.False(rig.Composer.IsGenerating);
    }

    [AvaloniaFact]
    public void Stopping_Before_Any_Text_Says_So()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.BuildTimes);
        rig.Settle();
        rig.Time.Advance(TimeSpan.FromSeconds(2.2));
        rig.Answer.Stop();
        rig.Settle();
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);
        Assert.Equal("Stopped before the answer began.", rig.Answer.Note!.Text);
        Assert.Equal("Thought for 2s", rig.Reasoning.ThoughtText);
        Assert.False(rig.Reasoning.IsThinking);
    }

    [AvaloniaFact]
    public void A_Responder_That_Throws_Fails_The_Answer_With_Its_Message()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.BuildTimes);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(2)).Text("Most of the time ");
        rig.Run(0.2);
        rig.Call.Throw(new InvalidOperationException("The model is overloaded."));
        rig.Settle();
        Assert.Equal(AiAnswerStatus.Failed, rig.Answer.Status);
        Assert.Equal("The model is overloaded.", rig.Answer.ErrorMessage);
        Assert.Equal(AiAnswerOutcome.Failed, rig.Answer.Versions[0].Outcome);
        Assert.Equal("The model is overloaded.", rig.Answer.Versions[0].ErrorMessage);
        Assert.Equal(AiStatusNoteTone.Error, rig.Answer.Note!.Tone);
        Assert.Equal("The answer failed: The model is overloaded.", rig.Answer.Note.Text);
        Assert.Equal("The answer failed: The model is overloaded.", rig.Answer.LastStatusAnnouncement);
        Assert.False(rig.Answer.IsBusy);
    }

    [AvaloniaFact]
    public void Try_Again_On_The_Note_Adds_A_Retry_Version()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        rig.Answer.Stop();
        rig.Settle();
        rig.Click(rig.Answer.Note!.ActionButton!);
        Assert.Equal(2, rig.Answer.Versions.Count);
        Assert.Equal(1, rig.Answer.CurrentVersionIndex);
        Assert.Equal(AiAnswerMode.Retry, rig.Call.Request.Mode);
        Assert.Equal(rig.Answer.Versions[0].Markdown, rig.Call.Request.PreviousAnswerMarkdown);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
        Assert.False(rig.Answer.Note.IsVisible);
    }

    [AvaloniaFact]
    public void A_New_Question_Cancels_The_Running_Answer_And_Starts_Over()
    {
        using var rig = new AiAnswerRig();
        var first = rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.FlakyTests);
        rig.Settle();
        Assert.True(first.IsCancelled);
        Assert.Equal(2, rig.Responder!.Calls.Count);
        Assert.Single(rig.Answer.Versions);
        Assert.Equal(AiAnswerPageSamples.FlakyTests, rig.Answer.Question);
        Assert.Equal(AiAnswerPageSamples.FlakyTests, rig.Answer.TitleText);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
        Assert.Empty(rig.Document.RevealedMarkdown);
        // The replaced answer leaves without "Answer stopped".
        Assert.Equal("Thinking", rig.Answer.LastStatusAnnouncement);
    }

    [AvaloniaFact]
    public void A_Cancelled_Question_Changes_Nothing()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.CancelQuestions = true;
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.FlakyTests);
        rig.Settle();
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, rig.Answer.Question);
        Assert.Single(rig.Responder!.Calls);
        Assert.Equal(AiAnswerStatus.Ready, rig.Answer.Status);
        Assert.Equal(2, rig.Asked.Count);
    }

    [AvaloniaFact]
    public void Setting_The_Question_Asks_It()
    {
        using var rig = new AiAnswerRig();
        rig.Answer.Question = AiAnswerPageSamples.BuildTimes;
        rig.Settle();
        Assert.Equal((AiAnswerPageSamples.BuildTimes, AiAnswerQuestionOrigin.Programmatic), rig.Asked.Single());
        Assert.Equal(AiAnswerPageSamples.BuildTimes, rig.Call.Request.Question);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void A_Question_Set_Before_The_Window_Is_Asked_Once_It_Is_Shown()
    {
        using var rig = new AiAnswerRig(a => a.Question = AiAnswerPageSamples.FlakyTests);
        Assert.Single(rig.Responder!.Calls);
        Assert.Equal(AiAnswerPageSamples.FlakyTests, rig.Call.Request.Question);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void The_Box_Asks_A_Follow_Up_Clears_Itself_And_Keeps_The_Focus()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Send("What about secrets?");
        Assert.Equal(("What about secrets?", AiAnswerQuestionOrigin.Composer), rig.Asked[^1]);
        Assert.Equal("What about secrets?", rig.Answer.Question);
        Assert.Equal(string.Empty, rig.Composer.Text);
        Assert.Equal(string.Empty, rig.Answer.ComposerText);
        Assert.True(rig.HasFocus(rig.Composer));
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void Composer_Text_Is_Two_Way()
    {
        using var rig = new AiAnswerRig();
        rig.Answer.ComposerText = "Draft";
        rig.Settle();
        Assert.Equal("Draft", rig.Composer.Text);
        rig.Composer.Text = "Draft two";
        rig.Settle();
        Assert.Equal("Draft two", rig.Answer.ComposerText);
    }

    [AvaloniaFact]
    public void Escape_In_The_Box_Stops_The_Answer()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        rig.Composer.FocusInput();
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void A_Related_Question_Is_Asked_In_Place_And_The_Focus_Moves_To_The_New_Question()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        var row = rig.Answer.Related!.Rows[1];
        var question = row.Question!;
        row.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        rig.Run(0.2);
        Assert.Equal((question, AiAnswerQuestionOrigin.Related), rig.Asked[^1]);
        Assert.Equal(question, rig.Answer.Question);
        Assert.Same(rig.Answer.QuestionHeading, rig.Focused);
    }

    [AvaloniaFact]
    public void A_Suggestion_Asks_From_The_Empty_Page_And_Focuses_The_Question()
    {
        using var rig = new AiAnswerRig();
        rig.Click(rig.Answer.SuggestionList!.Rows[2]);
        rig.Run(0.2);
        Assert.Equal((AiAnswerPageSamples.BuildTimes, AiAnswerQuestionOrigin.Suggestion), rig.Asked.Single());
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
        Assert.Same(rig.Answer.QuestionHeading, rig.Focused);
        Assert.False(rig.Answer.EmptyPage!.IsVisible);
    }

    [AvaloniaFact]
    public void New_Thread_Shows_The_Empty_Page_And_Focuses_The_Box()
    {
        using var rig = new AiAnswerRig();
        var call = rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        rig.Click(rig.Answer.NewThreadButton!);
        Assert.True(call.IsCancelled);
        Assert.Equal(AiAnswerStatus.Empty, rig.Answer.Status);
        Assert.Null(rig.Answer.Question);
        Assert.Empty(rig.Answer.Versions);
        Assert.True(rig.Answer.EmptyPage!.IsVisible);
        Assert.Equal("New thread", rig.Answer.TitleText);
        Assert.Equal("Ask anything", rig.Composer.Placeholder);
        Assert.True(rig.HasFocus(rig.Composer));
        Assert.False(rig.Answer.NewThreadButton!.IsEnabled);
        Assert.False(rig.Answer.ShareButton!.IsEnabled);
        Assert.Equal("Ask a question", rig.Answer.LastStatusAnnouncement);
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Stops_The_Answer()
    {
        using var rig = new AiAnswerRig();
        var call = rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        rig.Window.Content = null;
        rig.Settle();
        Assert.True(call.IsCancelled);
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void A_Finished_Answer_Without_A_Responder_Shows_Whole_And_Has_No_Box()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var answer = rig.Answer;
        Assert.Equal(AiAnswerStatus.Ready, answer.Status);
        Assert.False(answer.IsBusy);
        Assert.Single(answer.Versions);
        Assert.Equal(AiAnswerSamples.Answer, rig.Document.RevealedMarkdown);
        Assert.False(rig.Document.IsRevealing);
        Assert.Equal("8 sources", rig.SourcesRow.LabelText);
        Assert.Equal("Thought for 8s", rig.Reasoning.ThoughtText);
        Assert.Equal("812 tokens", answer.ModelLineView!.TokensText);
        Assert.True(answer.Related!.IsVisible);
        Assert.False(answer.Dock!.IsVisible);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, answer.TitleText);
        // A new static answer replaces the shown one at once.
        answer.Answer = "Short answer. [2]";
        rig.Settle();
        Assert.Equal("Short answer. [2]", rig.Document.RevealedMarkdown);
        Assert.Single(answer.Versions);
    }

    [AvaloniaFact]
    public void The_Title_Prefers_Title_Over_The_Question()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        rig.Answer.Title = "CI migration";
        rig.Settle();
        Assert.Equal("CI migration", rig.Answer.TitleText);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, rig.Answer.QuestionHeading!.Text);
    }

    [AvaloniaFact]
    public void An_Answer_Is_Collected_While_A_Version_It_Showed_Lives_On()
    {
        // An app can keep the versions (a history of answers) after the page is gone.
        AiAnswerVersion? kept = null;
        var weak = TestHelpers.ShowAndClose(() => AiAnswerPageSamples.StaticPage(), answer => kept = answer.Versions[0]);
        TestHelpers.Collect(weak);
        Assert.False(weak.IsAlive);
        kept!.Sources.Add(new AiSource { Index = 9, Title = "Runner images", Domain = "docs.example.org" });
        kept.FollowUps.Add("And the runners?");
    }

    [AvaloniaFact]
    public void An_Answer_Taken_Out_And_Put_Back_Shows_What_Its_Version_Got_Meanwhile()
    {
        var answer = AiAnswerPageSamples.StaticPage();
        var host = new Border { Child = answer };
        var window = TestHelpers.Show(host);
        try
        {
            var version = answer.Versions[0];
            Assert.Equal("8 sources", answer.SourcesRow!.LabelText);
            Assert.True(answer.Related!.IsVisible);

            host.Child = null;
            version.Sources.Add(new AiSource { Index = 9, Title = "Runner images", Domain = "docs.example.org" });
            version.FollowUps.Clear();
            host.Child = answer;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("9 sources", answer.SourcesRow.LabelText);
            Assert.Same(version.Sources[8], answer.Document!.FindSource(9));
            Assert.False(answer.Related.IsVisible);

            // And it follows the version again.
            version.Sources.Add(new AiSource { Index = 10, Title = "Cache keys", Domain = "docs.example.org" });
            Assert.Equal("10 sources", answer.SourcesRow.LabelText);
            Assert.Same(version.Sources[9], answer.Document.FindSource(10));
        }
        finally
        {
            window.Close();
        }
    }
}

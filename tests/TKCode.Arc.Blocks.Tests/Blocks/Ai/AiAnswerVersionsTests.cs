using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The answer's versions: answering again in every mode, switching models, paging, and each version's own state.</summary>
public class AiAnswerVersionsTests
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    /// <summary>Writes the running version to the end (text, done, end) and waits until it is ready.</summary>
    private static void Finish(AiAnswerRig rig, string text, int tokens, double seconds)
    {
        rig.Call.Thought(TimeSpan.FromSeconds(4)).Text(text).Done(tokens, TimeSpan.FromSeconds(seconds), "Next?").End();
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 20));
    }

    [AvaloniaFact]
    public void Answering_Again_Adds_A_Version_With_Its_Mode_And_The_Shown_Answer_As_The_Previous_One()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        var first = rig.Answer.Versions[0];
        rig.Answer.Regenerate(AiAnswerMode.Concise);
        rig.Settle();
        Assert.Equal(2, rig.Answer.Versions.Count);
        Assert.Equal(1, rig.Answer.CurrentVersionIndex);
        var request = rig.Call.Request;
        Assert.Equal(AiAnswerMode.Concise, request.Mode);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, request.Question);
        Assert.Equal(first.Markdown, request.PreviousAnswerMarkdown);
        Assert.Equal("lumen-pro", request.Model?.Id);
        Assert.Equal(AiAnswerMode.Concise, rig.Answer.Versions[1].Mode);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
        Assert.True(rig.Actions.IsBusy);
        Assert.False(rig.Answer.Related!.IsVisible);
        Assert.Empty(rig.Document.RevealedMarkdown);
        // The new version's rows start over: thinking, no sources yet.
        Assert.True(rig.Reasoning.IsThinking);
        Assert.False(rig.SourcesRow.IsVisible);
        Finish(rig, AiAnswerPageSamples.MoveBuildsConcise, 430, 10.2);
        Assert.Equal(2, rig.Actions.VersionCount);
        Assert.Equal(1, rig.Actions.VersionIndex);
        Assert.Equal("430 tokens", rig.Answer.ModelLineView!.TokensText);
        Assert.Equal("10.2s", rig.Answer.ModelLineView.DurationText);
    }

    [AvaloniaFact]
    public void The_Menu_Answers_Again_More_Detailed_And_Runs_The_Command()
    {
        var command = new RecordingCommand();
        using var rig = new AiAnswerRig(a => a.RegenerateCommand = command);
        rig.AnswerFirst();
        var button = rig.Actions.RegenerateButton!;
        button.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        Assert.True(rig.Actions.Menu!.IsOpen);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        rig.Settle();
        Assert.Equal(AiAnswerMode.Detailed, rig.Call.Request.Mode);
        Assert.Equal(new object?[] { AiAnswerMode.Detailed }, command.Parameters);
        Assert.Equal(2, rig.Answer.Versions.Count);
    }

    [AvaloniaFact]
    public void Switch_Model_Lists_The_Other_Models_And_Answers_With_The_Chosen_One()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        Assert.Equal(new[] { "lumen", "lumen-mini", "atlas" }, rig.Actions.Models!.Select(m => m.Id));
        var atlas = rig.Answer.Models.Single(m => m.Id == "atlas");
        rig.Answer.Regenerate(AiAnswerMode.SwitchModel, atlas);
        rig.Settle();
        Assert.Equal(AiAnswerMode.SwitchModel, rig.Call.Request.Mode);
        Assert.Same(atlas, rig.Call.Request.Model);
        Assert.Same(atlas, rig.Answer.Model);
        Assert.Same(atlas, rig.Answer.Versions[1].Model);
        Assert.Equal(new[] { "lumen-pro", "lumen", "lumen-mini" }, rig.Actions.Models!.Select(m => m.Id));
        Finish(rig, "Short.", 120, 3);
        Assert.Equal("Atlas 2.1", rig.Answer.ModelLineView!.Model?.Name);
        // The first version keeps its model.
        rig.Answer.ShowVersion(0);
        rig.Settle();
        Assert.Equal("Lumen Pro 4", rig.Answer.ModelLineView.Model?.Name);
        Assert.Equal(new[] { "lumen", "lumen-mini", "atlas" }, rig.Actions.Models!.Select(m => m.Id));
        // Switch model needs a model.
        rig.Answer.Regenerate(AiAnswerMode.SwitchModel);
        Assert.Equal(2, rig.Answer.Versions.Count);
    }

    [AvaloniaFact]
    public void Each_Version_Keeps_Its_Own_Text_Usage_Rating_And_Stop()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Click(rig.Actions.GoodButton!);
        Assert.Equal(AiAnswerFeedbackValue.Up, rig.Answer.Versions[0].Feedback);
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(3)).Text("A second try at the answer that stops ");
        rig.Run(0.2);
        rig.Answer.Stop();
        rig.Settle();
        var second = rig.Answer.Versions[1];
        Assert.Equal(AiAnswerOutcome.Stopped, second.Outcome);
        Assert.Equal(AiAnswerFeedbackValue.None, rig.Actions.Feedback);
        Assert.True(rig.Answer.Note!.IsVisible);
        Assert.True(rig.Answer.ModelLineView!.IsStopped);
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);

        // Back to the first: its text, its rating, its usage, no note.
        rig.Click(rig.Actions.Pager!.PreviousButton!);
        Assert.Equal(0, rig.Answer.CurrentVersionIndex);
        Assert.Equal(rig.Answer.Versions[0].Markdown, rig.Document.RevealedMarkdown);
        Assert.Equal(AiAnswerFeedbackValue.Up, rig.Actions.Feedback);
        Assert.False(rig.Answer.Note.IsVisible);
        Assert.False(rig.Answer.ModelLineView.IsStopped);
        Assert.Equal("812 tokens", rig.Answer.ModelLineView.TokensText);
        Assert.Equal("Thought for 8s", rig.Reasoning.ThoughtText);
        Assert.Equal(AiAnswerStatus.Ready, rig.Answer.Status);
        Assert.Equal("Showing version 1 of 2", rig.Answer.LastActionAnnouncement);

        // And forward again: the stopped one.
        rig.Click(rig.Actions.Pager.NextButton!);
        Assert.Equal(1, rig.Answer.CurrentVersionIndex);
        Assert.Equal(second.Markdown, rig.Document.RevealedMarkdown);
        Assert.Equal(AiAnswerStatus.Stopped, rig.Answer.Status);
        Assert.Equal("Thought for 3s", rig.Reasoning.ThoughtText);
        Assert.Equal("Showing version 2 of 2", rig.Answer.LastActionAnnouncement);
    }

    [AvaloniaFact]
    public void The_Version_Index_Is_Two_Way_And_Clamped()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        Finish(rig, "Second.", 10, 1);
        rig.Answer.CurrentVersionIndex = 7;
        rig.Settle();
        Assert.Equal(1, rig.Answer.CurrentVersionIndex);
        rig.Answer.CurrentVersionIndex = -2;
        rig.Settle();
        Assert.Equal(0, rig.Answer.CurrentVersionIndex);
        Assert.Equal(rig.Answer.Versions[0].Markdown, rig.Document.RevealedMarkdown);
        Assert.Equal(0, rig.Actions.VersionIndex);
        // Code moving the index is not announced.
        Assert.NotEqual("Showing version 1 of 2", rig.Answer.LastActionAnnouncement);
    }

    [AvaloniaFact]
    public void While_A_Version_Is_Written_The_Index_Stays_On_It()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        rig.Answer.CurrentVersionIndex = 0;
        rig.Settle();
        Assert.Equal(1, rig.Answer.CurrentVersionIndex);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void Answering_Again_While_One_Is_Written_Stops_It_And_Starts_Another()
    {
        using var rig = new AiAnswerRig();
        var first = rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        Assert.True(first.IsCancelled);
        Assert.Equal(AiAnswerOutcome.Stopped, rig.Answer.Versions[0].Outcome);
        Assert.Equal(2, rig.Answer.Versions.Count);
        Assert.Equal(AiAnswerStatus.Thinking, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void Without_A_Responder_Nothing_Is_Answered_Again()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        Assert.Single(rig.Answer.Versions);
        Assert.Equal(AiAnswerStatus.Ready, rig.Answer.Status);
    }

    [AvaloniaFact]
    public void A_New_Question_Starts_A_New_List_Of_Versions()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Concise);
        rig.Settle();
        Finish(rig, "Short.", 10, 1);
        Assert.Equal(2, rig.Answer.Versions.Count);
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.FlakyTests);
        rig.Settle();
        Assert.Single(rig.Answer.Versions);
        Assert.Equal(0, rig.Answer.CurrentVersionIndex);
        Assert.Equal(AiAnswerMode.Answer, rig.Call.Request.Mode);
        Assert.Null(rig.Call.Request.PreviousAnswerMarkdown);
    }
}

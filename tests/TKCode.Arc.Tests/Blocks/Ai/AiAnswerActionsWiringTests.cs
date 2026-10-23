using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>What the answer's actions do: copy, share, rate, open sources, and the lighting between citations and cards.</summary>
public class AiAnswerActionsWiringTests
{
    [AvaloniaFact]
    public void Copy_Answer_Puts_The_Text_And_Its_Sources_On_The_Clipboard()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        rig.Click(rig.Actions.CopyButton!);
        var text = rig.Copied.Single();
        Assert.StartsWith("Keep both pipelines running for a while", text);
        Assert.Contains("[1]", text);
        Assert.Contains("freeze_days_before_release: 3", text);
        Assert.Contains("\n\n[1] Running two pipelines side by side - docs.pipewright.dev - https://docs.pipewright.dev/", text);
        Assert.EndsWith("[8] Build provenance basics - openbuildnotes.org - https://openbuildnotes.org/", text);
        var copied = rig.CopiedEvents.Single();
        Assert.Equal(AiAnswerCopyKind.Answer, copied.Kind);
        Assert.Equal(text, copied.Text);
        Assert.Equal("Answer copied with its sources", rig.Answer.LastActionAnnouncement);
        Assert.True(rig.Actions.IsCopied);
    }

    [AvaloniaFact]
    public void Share_Copies_The_Thread_Link_And_Says_Copied_For_A_Moment()
    {
        var infos = new List<AiAnswerShareInfo>();
        using var rig = new AiAnswerRig(a => a.ShareLinkProvider = (info, _) =>
        {
            infos.Add(info);
            return ValueTask.FromResult("https://example.com/t/1");
        });
        rig.AnswerFirst();
        Assert.Equal("Share", rig.Answer.ShareText);
        rig.Click(rig.Answer.ShareButton!);
        var info = infos.Single();
        Assert.Null(info.VersionIndex);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, info.Question);
        Assert.Equal(rig.Answer.Versions[0].Markdown, info.AnswerMarkdown);
        Assert.Equal("https://example.com/t/1", rig.Copied.Single());
        Assert.Equal("Copied", rig.Answer.ShareText);
        Assert.Equal(AiAnswerCopyKind.ThreadLink, rig.CopiedEvents.Single().Kind);
        Assert.Equal("Link copied", rig.Answer.LastActionAnnouncement);
        rig.Time.Advance(TimeSpan.FromSeconds(1.5));
        rig.Answer.TickShare();
        Assert.Equal("Copied", rig.Answer.ShareText);
        rig.Time.Advance(TimeSpan.FromSeconds(0.2));
        rig.Answer.TickShare();
        Assert.Equal("Share", rig.Answer.ShareText);
    }

    [AvaloniaFact]
    public void A_Share_Link_That_Fails_Says_Failed()
    {
        using var rig = new AiAnswerRig(a => a.ShareLinkProvider = (_, _) => ValueTask.FromException<string>(new InvalidOperationException("offline")));
        rig.AnswerFirst();
        rig.Click(rig.Answer.ShareButton!);
        Assert.Equal("Failed", rig.Answer.ShareText);
        Assert.Empty(rig.Copied);
        Assert.Empty(rig.CopiedEvents);
    }

    [AvaloniaFact]
    public void The_Link_Button_Copies_The_Link_Of_The_Shown_Version()
    {
        var infos = new List<AiAnswerShareInfo>();
        using var rig = new AiAnswerRig(a => a.ShareLinkProvider = (info, _) =>
        {
            infos.Add(info);
            return ValueTask.FromResult($"https://example.com/a?v={info.VersionIndex}");
        });
        rig.AnswerFirst();
        Assert.True(rig.Actions.LinkButton!.IsVisible);
        rig.Click(rig.Actions.LinkButton);
        Assert.Equal(0, infos.Single().VersionIndex);
        Assert.Equal("https://example.com/a?v=0", rig.Copied.Single());
        Assert.True(rig.Actions.IsLinkCopied);
        Assert.Equal(AiAnswerCopyKind.ShareLink, rig.CopiedEvents.Single().Kind);
        Assert.Equal("Link copied", rig.Answer.LastActionAnnouncement);
    }

    [AvaloniaFact]
    public void Without_A_Link_Provider_Share_And_The_Link_Button_Are_Hidden()
    {
        using var rig = new AiAnswerRig(a => a.ShareLinkProvider = null);
        rig.AnswerFirst();
        Assert.False(rig.Answer.ShareButton!.IsVisible);
        Assert.False(rig.Actions.HasShare);
        Assert.False(rig.Actions.LinkButton!.IsVisible);
    }

    [AvaloniaFact]
    public void Rating_Up_Is_At_Once_And_Pressing_It_Again_Takes_It_Back()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Click(rig.Actions.GoodButton!);
        var up = rig.Feedback.Single();
        Assert.Equal(AiAnswerFeedbackValue.Up, up.Value);
        Assert.Null(up.Reason);
        Assert.Equal(AiAnswerPageSamples.MoveBuilds, up.Question);
        Assert.Same(rig.Answer.Versions[0], up.Version);
        Assert.Equal(0, up.VersionIndex);
        Assert.Equal(AiAnswerFeedbackValue.Up, rig.Answer.Versions[0].Feedback);
        Assert.Equal("Feedback sent: good answer", rig.Answer.LastActionAnnouncement);
        rig.Click(rig.Actions.GoodButton!);
        Assert.Equal(AiAnswerFeedbackValue.None, rig.Feedback[^1].Value);
        Assert.Equal(AiAnswerFeedbackValue.None, rig.Answer.Versions[0].Feedback);
        Assert.Equal("Feedback removed", rig.Answer.LastActionAnnouncement);
    }

    [AvaloniaFact]
    public void Rating_Down_Asks_Why_Then_Reports_The_Reason()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        rig.Click(rig.Actions.BadButton!);
        Assert.True(rig.Actions.ReasonPicker!.IsOpen);
        Assert.Empty(rig.Feedback);
        var reason = rig.FindAll<AiFeedbackReasonButton>().Single(b => b.Reason == "Out of date");
        rig.Click(reason);
        var down = rig.Feedback.Single();
        Assert.Equal(AiAnswerFeedbackValue.Down, down.Value);
        Assert.Equal("Out of date", down.Reason);
        Assert.Equal("Out of date", rig.Answer.Versions[0].FeedbackReason);
        Assert.Equal("Feedback sent: bad answer", rig.Answer.LastActionAnnouncement);
        // The thanks reads the reason back.
        Assert.Contains(rig.FindAll<Avalonia.Controls.TextBlock>(), t => t.Text == "Noted: out of date");
    }

    [AvaloniaFact]
    public void The_Labels_Reasons_Are_Offered_Unless_The_Block_Has_Its_Own()
    {
        using var rig = new AiAnswerRig();
        Assert.Equal(new[] { "Not correct", "Out of date", "Needs a source", "Too long", "Didn't answer my question" }, rig.Actions.FeedbackReasons!);
        rig.Answer.FeedbackReasons.Add("Off topic");
        Assert.Equal(new[] { "Off topic" }, rig.Actions.FeedbackReasons!);
    }

    [AvaloniaFact]
    public void Pointing_At_A_Citation_Lights_Its_Card_And_A_Card_Lights_The_Sentences()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true, configure: a => a.IsSourcesExpanded = true);
        var pill = rig.FindAll<AiAnswerCitation>().First(p => p.Number == 5);
        rig.Hover(pill);
        Assert.Equal(5, rig.SourcesRow.ActiveNumber);
        Assert.True(rig.SourcesRow.Cards.Single(c => c.Source?.Index == 5).IsLit);
        rig.Leave();
        Assert.Equal(0, rig.SourcesRow.ActiveNumber);

        var card = rig.SourcesRow.Cards.Single(c => c.Source?.Index == 1);
        rig.Hover(card);
        Assert.Equal(1, rig.Document.LitSourceNumber);
        rig.Leave();
        Assert.Equal(0, rig.Document.LitSourceNumber);
    }

    [AvaloniaFact]
    public void A_Card_Opens_Its_Source_Unless_The_Host_Handles_It()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true, configure: a => a.IsSourcesExpanded = true);
        var card = rig.SourcesRow.Cards.Single(c => c.Source?.Index == 2);
        rig.Click(card);
        var invoked = rig.Sources.Single();
        Assert.False(invoked.FromCitation);
        Assert.Equal("eng.northgate.io", invoked.Item.Domain);
        Assert.Equal(new Uri("https://eng.northgate.io/"), rig.Opened.Single());

        rig.HandleSources = true;
        rig.Click(card);
        Assert.Equal(2, rig.Sources.Count);
        Assert.Single(rig.Opened);
    }

    [AvaloniaFact]
    public void A_Citation_Opens_Its_Source_From_The_Keyboard()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var pill = rig.FindAll<AiAnswerCitation>().First(p => p.Number == 3);
        pill.Focus(NavigationMethod.Tab);
        rig.Settle();
        Assert.Equal(3, rig.SourcesRow.ActiveNumber);
        rig.Key(PhysicalKey.Enter);
        var invoked = rig.Sources.Single();
        Assert.True(invoked.FromCitation);
        Assert.Equal(3, invoked.Item.Index);
        Assert.Equal(new Uri("https://handbook.fieldnote.app/"), rig.Opened.Single());
    }

    [AvaloniaFact]
    public void A_Copied_Code_Block_Is_Reported_As_Code()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        rig.Document.RaiseEvent(new AiAnswerCopiedEventArgs(AiAnswerDocument.CodeCopiedEvent, rig.Document, AiAnswerCopyKind.Code, "required:"));
        var copied = rig.CopiedEvents.Single();
        Assert.Equal(AiAnswerCopyKind.Code, copied.Kind);
        Assert.Equal("required:", copied.Text);
        Assert.Same(rig.Answer, copied.Source);
    }

    [AvaloniaFact]
    public void The_Panels_Follow_Their_Properties_Both_Ways()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        Assert.False(rig.SourcesRow.IsExpanded);
        rig.Answer.IsSourcesExpanded = true;
        rig.Settle();
        Assert.True(rig.SourcesRow.IsExpanded);
        rig.Click(rig.SourcesRow.Toggle!);
        Assert.False(rig.Answer.IsSourcesExpanded);

        rig.Answer.IsReasoningExpanded = true;
        rig.Settle();
        Assert.True(rig.Reasoning.IsExpanded);
        Assert.True(rig.Reasoning.Body!.IsExpanded);
        rig.Click(rig.Reasoning.Button!);
        Assert.False(rig.Answer.IsReasoningExpanded);
    }

    [AvaloniaFact]
    public void Commands_Run_On_The_Persons_Actions()
    {
        var asked = new List<object?>();
        var stopped = 0;
        var threads = 0;
        using var rig = new AiAnswerRig(a =>
        {
            a.AskCommand = new Relay(p => asked.Add(p));
            a.StopCommand = new Relay(_ => stopped++);
            a.NewThreadCommand = new Relay(_ => threads++);
        });
        _ = rig.Answer.AskAsync("Programmatic question");
        rig.Settle();
        Assert.Empty(asked);
        // A busy box does not send: stop first (code stopping runs no command).
        rig.Answer.Stop();
        rig.Settle();
        Assert.Equal(0, stopped);
        rig.Send("Typed question");
        Assert.Equal(new object?[] { "Typed question" }, asked);
        rig.Composer.FocusInput();
        rig.Settle();
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(1, stopped);
        rig.Click(rig.Answer.NewThreadButton!);
        Assert.Equal(1, threads);
    }

    private sealed class Relay(Action<object?> run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run(parameter);
    }
}

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The answer page's own small parts, the citation preview's meta row, and page details (narrow, dark, static asking).</summary>
public class AiAnswerPartsTests
{
    [AvaloniaFact]
    public void The_Reasoning_Splits_Into_Paragraphs_At_Blank_Lines()
    {
        Assert.Equal(new[] { "One.\nStill one.", "Two." }, AiAnswerReasoningText.Split("One.\nStill one.\n\n\n Two. \n"));
        Assert.Empty(AiAnswerReasoningText.Split("  \n\n "));
        var text = new AiAnswerReasoningText { Text = "A\n\nB" };
        var window = TestHelpers.Show(text);
        var first = (SelectableTextBlock)text.Children[0];
        text.Text = "A\n\nB grows";
        Assert.Same(first, text.Children[0]);
        Assert.Equal(new[] { "A", "B grows" }, text.Paragraphs);
        text.Text = "A";
        Assert.Single(text.Children);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Answers_Citation_Preview_Ends_With_Domain_And_Date_The_Chats_Does_Not()
    {
        var source = AiSourcesSamples.Sources()[4];
        var card = new AiCitationCard();
        card.Show(source, 5);
        Assert.False(card.HasMeta);
        card.ShowsMeta = true;
        Assert.True(card.HasMeta);
        Assert.True(card.HasMetaDot);
        Assert.Equal("Sep 9, 2026", card.Date);
        card.Show(AiSourcesSamples.Make(2, "No date", "example.org"), 2);
        Assert.True(card.HasMeta);
        Assert.False(card.HasMetaDot);

        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var pill = rig.FindAll<AiAnswerCitation>().First(p => p.Number == 5);
        rig.Hover(pill);
        rig.Document.FlushCardTimers();
        rig.Settle();
        Assert.True(rig.Document.IsCardOpen);
        Assert.True(rig.Document.Card!.HasMeta);
        Assert.Equal("quarry.dev", rig.Document.Card.Domain);
        Assert.Equal("Sep 9, 2026", rig.Document.Card.Date);
    }

    [AvaloniaFact]
    public void The_Question_Is_A_Header_Named_By_Its_Text()
    {
        var heading = new AiAnswerHeading { Text = "Which checks block?" };
        var window = TestHelpers.Show(heading);
        var peer = ControlAutomationPeer.CreatePeerForElement(heading);
        Assert.Equal(AutomationControlType.Header, peer.GetAutomationControlType());
        Assert.Equal("Which checks block?", peer.GetName());
        window.Close();
    }

    [AvaloniaFact]
    public void The_Pill_Presses_Half_Muted_Half_Border()
    {
        var pill = new AiAnswerPillButton { Content = "Try again" };
        var window = TestHelpers.Show(pill);
        Assert.IsType<Avalonia.Media.Immutable.ImmutableSolidColorBrush>(pill.PressedFill);
        Assert.Equal(32, pill.Bounds.Height, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Stage_Springs_Only_At_Full_Motion()
    {
        var stage = new AiAnswerStage { Child = new Border { Height = 100 }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(stage, ArcMotionLevel.Reduced);
        stage.Arm();
        ((Border)stage.Child).Height = 40;
        window.UpdateLayout();
        Assert.False(stage.IsMoving);
        Assert.Equal(40, stage.Bounds.Height, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_The_Sources_Scroll_In_A_Row_And_The_Model_Line_Moves_Under_The_Toolbar()
    {
        using var rig = new AiAnswerRig(width: 390, configure: a => a.IsSourcesExpanded = true);
        rig.AnswerFirst();
        Assert.True(rig.SourcesRow.IsNarrow);
        var toolbar = rig.Actions.Toolbar!;
        var line = rig.Answer.ModelLineView!;
        Assert.True(rig.TopLeftOf(line).Y > rig.TopLeftOf(toolbar).Y + 20);
        rig.Window.Width = 745;
        rig.Settle();
        Assert.False(rig.SourcesRow.IsNarrow);
        var lineMiddle = rig.TopLeftOf(line).Y + line.Bounds.Height / 2;
        var toolbarMiddle = rig.TopLeftOf(toolbar).Y + toolbar.Bounds.Height / 2;
        Assert.InRange(lineMiddle, toolbarMiddle - 1, toolbarMiddle + 1);
    }

    [AvaloniaFact]
    public void The_Top_Bars_Hairline_Is_Border_Subtle_On_Light_And_Border_On_Dark()
    {
        using var rig = new AiAnswerRig();
        Assert.Same(rig.Answer.SubtleBorderBrush, rig.Answer.HairlineBrush);
        rig.Answer.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Assert.Same(rig.Answer.BorderBrush, rig.Answer.HairlineBrush);
        Assert.Same(rig.Answer.HairlineBrush, rig.Answer.TopBar!.BorderBrush);
    }

    [AvaloniaFact]
    public void Without_A_Responder_A_Picked_Question_Shows_What_The_Host_Answers()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var answer = rig.Answer;
        answer.QuestionAsked += (_, e) =>
        {
            if (e.Question == AiAnswerPageSamples.BuildTimes)
            {
                answer.Answer = AiAnswerPageSamples.BuildTimesAnswer;
            }
        };
        // A question the host does not answer: the page shows it with nothing under it.
        rig.Click(answer.Related!.Rows[0]);
        Assert.Equal(AiAnswerPageSamples.BlockingChecks, answer.Question);
        Assert.Empty(answer.Versions);
        Assert.Equal(AiAnswerStatus.Ready, answer.Status);
        Assert.Empty(rig.Document.RevealedMarkdown);
        // The host answers this one while it is asked.
        _ = answer.AskAsync(AiAnswerPageSamples.BuildTimes);
        rig.Settle();
        Assert.Equal(AiAnswerPageSamples.BuildTimesAnswer, rig.Document.RevealedMarkdown);
        Assert.Single(answer.Versions);
    }

    [AvaloniaFact]
    public void A_Question_Asked_Before_The_Window_Is_Asked_Once()
    {
        var responder = new ScriptedAnswerResponder();
        var answer = AiAnswerPageSamples.LivePage(responder);
        var asked = 0;
        answer.QuestionAsked += (_, _) => asked++;
        _ = answer.AskAsync(AiAnswerPageSamples.FlakyTests);
        Assert.Equal(1, asked);
        Assert.Empty(responder.Calls);
        var window = TestHelpers.Show(answer);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, asked);
        Assert.Single(responder.Calls);
        Assert.Equal(AiAnswerStatus.Thinking, answer.Status);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Finished_Answer_Set_With_A_Responder_Is_Shown_Not_Asked_Again()
    {
        var responder = new ScriptedAnswerResponder();
        var answer = new AiAnswer { Responder = responder };
        answer.Question = AiAnswerPageSamples.MoveBuilds;
        answer.Answer = AiAnswerSamples.Answer;
        var window = TestHelpers.Show(answer);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(responder.Calls);
        Assert.Equal(AiAnswerStatus.Ready, answer.Status);
        Assert.Single(answer.Versions);
        Assert.True(answer.Dock!.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void A_Finished_Answer_Without_A_Thinking_Time_Reads_Reasoning()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true, configure: a => a.Usage = new AiAnswerUsage(100));
        Assert.Equal("Reasoning", rig.Reasoning.ThoughtText);
        Assert.False(rig.Reasoning.IsThinking);
    }
}

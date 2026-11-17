using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The answer page's geometry, its width rules, the tab order and focus, and its motion at each level.</summary>
public class AiAnswerLayoutTests
{
    private static void AssertThickness(Thickness expected, Thickness actual)
    {
        Assert.Equal(expected.Left, actual.Left, 3);
        Assert.Equal(expected.Top, actual.Top, 3);
        Assert.Equal(expected.Right, actual.Right, 3);
        Assert.Equal(expected.Bottom, actual.Bottom, 3);
    }

    [AvaloniaFact]
    public void The_Column_Follows_The_Blocks_Width()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var answer = rig.Answer;
        Assert.Equal(32, answer.SidePadding, 3);
        AssertThickness(new Thickness(32, 44.7, 32, 48), answer.Column!.Padding);
        Assert.Equal(768, answer.Column.MaxWidth);
        Assert.Equal(28, answer.QuestionFontSize, 3);
        Assert.Equal(30.8, answer.QuestionHeading!.LineHeight, 3);
        Assert.Equal(-0.56, answer.QuestionHeading.LetterSpacing, 3);
        Assert.Equal(32, rig.SourcesRow.Bleed, 3);
        AssertThickness(new Thickness(32, 6, 32, 14), answer.Dock!.Padding);

        rig.Window.Width = 390;
        rig.Settle();
        Assert.Equal(19.5, answer.SidePadding, 3);
        AssertThickness(new Thickness(19.5, 28, 19.5, 48), answer.Column.Padding);
        Assert.Equal(17.6 + 390 * 0.016, answer.QuestionFontSize, 3);
        Assert.Equal(19.5, rig.SourcesRow.Bleed, 3);

        rig.Window.Width = 320;
        rig.Settle();
        Assert.Equal(16, answer.SidePadding, 3);
        Assert.Equal(22.72, answer.QuestionFontSize, 3);

        // A very wide block: the column stays 768 wide (paddings included) and centered.
        rig.Window.Width = 1200;
        rig.Settle();
        Assert.Equal(768, answer.Column.Bounds.Width, 1);
        Assert.Equal((1200 - 768) / 2.0, answer.Column.TranslatePoint(default, rig.Window)!.Value.X, 1);
    }

    [AvaloniaFact]
    public void The_Page_Is_Laid_Out_Like_Arcs()
    {
        using var rig = new AiAnswerRig(staticPage: true);
        var answer = rig.Answer;
        Assert.Equal(52, answer.TopBar!.Bounds.Height, 1);
        var newThread = answer.NewThreadButton!;
        Assert.Equal(new Size(36, 36), newThread.Bounds.Size);
        Assert.Equal(10, rig.TopLeftOf(newThread).X, 1);
        var share = answer.ShareButton!;
        Assert.Equal(32, share.Bounds.Height, 1);
        Assert.Equal(745 - 10, rig.TopLeftOf(share).X + share.Bounds.Width, 1);
        // The question sits 44.7 under the top bar; the rows follow 14 under it: 40 for the reasoning, 42 for the sources.
        var heading = answer.QuestionHeading!;
        Assert.InRange(rig.TopLeftOf(heading).Y, 52 + 44.7 - 0.5, 52 + 44.7 + 0.5);
        var steps = answer.Steps!;
        Assert.Equal(rig.TopLeftOf(heading).Y + heading.Bounds.Height + 14, rig.TopLeftOf(steps).Y, 1);
        Assert.Equal(40, rig.Reasoning.Bounds.Height, 1);
        Assert.Equal(42, rig.SourcesRow.Bounds.Height, 1);
        Assert.Equal(rig.TopLeftOf(steps).Y + 82 + 20, rig.TopLeftOf(answer.Stage!).Y, 1);
        // The footer is 20 under the answer and the related questions 36 under the footer.
        var footer = answer.Footer!;
        Assert.Equal(rig.TopLeftOf(answer.Stage!).Y + answer.Stage!.Bounds.Height + 20, rig.TopLeftOf(footer).Y, 1);
        Assert.True(footer.Bounds.Height >= 36);
        Assert.Equal(rig.TopLeftOf(footer).Y + footer.Bounds.Height + 36, rig.TopLeftOf(answer.Related!).Y, 1);
        // The box: 6 above, 14 below, as wide as the column's content.
        var composer = rig.Composer;
        Assert.Equal(745 - 2 * 32, composer.Bounds.Width, 1);
        Assert.Equal(new CornerRadius(26), composer.CornerRadius);
    }

    [AvaloniaFact]
    public void The_Empty_Page_Leaves_More_Room_Above_Its_Heading()
    {
        using var rig = new AiAnswerRig();
        var empty = rig.Answer.EmptyPage!;
        Assert.Equal(745 * 0.08, empty.Margin.Top, 3);
        var title = rig.Answer.EmptyTitleText!;
        Assert.Equal(28, title.FontSize, 3);
        Assert.InRange(rig.TopLeftOf(title).Y, 52 + 44.7 + 59.6 - 1, 52 + 44.7 + 59.6 + 1);
    }

    [AvaloniaFact]
    public void A_Long_Question_Is_Balanced_Over_Its_Lines()
    {
        using var rig = new AiAnswerRig(responder: false, staticPage: true);
        var heading = rig.Answer.QuestionHeading!;
        Assert.True(heading.TextLayout.TextLines.Count >= 2);
        Assert.True(heading.BalancedWidth < 680, $"balanced {heading.BalancedWidth}");
        Assert.True(heading.Focusable);
        Assert.False(heading.IsTabStop);
    }

    [AvaloniaFact]
    public void The_Tab_Order_Follows_The_Page()
    {
        using var rig = new AiAnswerRig(responder: true, staticPage: true, configure: a => a.IsSourcesExpanded = true);
        var answer = rig.Answer;
        answer.NewThreadButton!.Focus(NavigationMethod.Tab);
        rig.Settle();
        var order = new List<Visual>();
        for (var i = 0; i < 60; i++)
        {
            if (rig.Focused is Visual focused)
            {
                order.Add(focused);
                if (focused is TextBox || rig.HasFocus(rig.Composer))
                {
                    break;
                }
            }
            rig.Key(PhysicalKey.Tab);
        }
        int IndexOf(Visual v) => order.FindIndex(f => ReferenceEquals(f, v) || v.IsVisualAncestorOf(f));
        Assert.Equal(0, IndexOf(answer.NewThreadButton));
        Assert.Equal(1, IndexOf(answer.ShareButton!));
        Assert.Equal(2, IndexOf(rig.Reasoning.Button!));
        Assert.Equal(3, IndexOf(rig.SourcesRow.Toggle!));
        Assert.Equal(4, IndexOf(rig.SourcesRow.Cards[0]));
        var firstCitation = order.FindIndex(v => v is AiAnswerCitation);
        Assert.True(firstCitation > IndexOf(rig.SourcesRow.Cards[^1]));
        var copy = IndexOf(rig.Actions.CopyButton!);
        Assert.True(copy > order.FindLastIndex(v => v is AiAnswerCitation));
        Assert.True(IndexOf(rig.Answer.Related!.Rows[0]) > copy);
        Assert.True(rig.HasFocus(rig.Composer));
        Assert.True(order.FindIndex(v => rig.Composer.IsVisualAncestorOf(v)) > IndexOf(rig.Answer.Related.Rows[^1]));
    }

    [AvaloniaFact]
    public void The_Menu_Gives_The_Focus_Back_To_Answer_Again()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        var button = rig.Actions.RegenerateButton!;
        button.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        Assert.True(rig.Actions.Menu!.IsOpen);
        rig.Key(PhysicalKey.Escape);
        Assert.False(rig.Actions.Menu.IsOpen);
        Assert.Same(button, rig.Focused);
    }

    [AvaloniaFact]
    public void The_Status_Region_And_The_Action_Region_Speak_Separately()
    {
        using var rig = new AiAnswerRig();
        rig.AnswerFirst();
        Assert.Equal("Answer ready", rig.Answer.LastStatusAnnouncement);
        rig.Click(rig.Actions.GoodButton!);
        Assert.Equal("Feedback sent: good answer", rig.Answer.LastActionAnnouncement);
        Assert.Equal("Answer ready", rig.Answer.LastStatusAnnouncement);
    }

    [AvaloniaFact]
    public void At_Full_Motion_Another_Version_Crossfades_While_The_Height_Springs()
    {
        using var rig = new AiAnswerRig(level: ArcMotionLevel.Full);
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Concise);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(4)).Text(AiAnswerPageSamples.MoveBuildsConcise).Done(430).End();
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 20));
        rig.Run(1);
        var stage = rig.Answer.Stage!;
        var shortHeight = stage.Bounds.Height;
        rig.Answer.ShowVersion(0);
        rig.Settle();
        // The old answer fades out as a picture over the new one; the area grows on a spring (both ways, ai.md 8).
        Assert.True(stage.IsMoving);
        Assert.InRange(stage.Bounds.Height, shortHeight - 1, shortHeight + 40);
        rig.Run(0.08);
        Assert.InRange(stage.Child!.Opacity, 0.05, 0.95);
        rig.Run(1.2);
        Assert.False(stage.IsMoving);
        Assert.False(stage.HasGhost);
        Assert.Equal(1, stage.Child.Opacity, 3);
        Assert.True(stage.Bounds.Height > shortHeight + 200);
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_Versions_Crossfade_Without_The_Spring()
    {
        using var rig = new AiAnswerRig(level: ArcMotionLevel.Reduced);
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Concise);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(4)).Text(AiAnswerPageSamples.MoveBuildsConcise).Done(430).End();
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 20));
        var stage = rig.Answer.Stage!;
        rig.Answer.ShowVersion(0);
        rig.Settle();
        Assert.False(stage.IsMoving);
        rig.Run(0.06);
        Assert.InRange(stage.Child!.Opacity, 0.2, 0.8);
        rig.Run(0.2);
        Assert.Equal(1, stage.Child.Opacity, 3);
    }

    [AvaloniaFact]
    public void Without_Motion_Versions_Switch_At_Once()
    {
        using var rig = new AiAnswerRig(level: ArcMotionLevel.Off);
        rig.AnswerFirst();
        rig.Answer.Regenerate(AiAnswerMode.Concise);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(4)).Text(AiAnswerPageSamples.MoveBuildsConcise).Done(430).End();
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 20));
        var stage = rig.Answer.Stage!;
        rig.Answer.ShowVersion(0);
        rig.Settle();
        Assert.False(stage.IsMoving);
        Assert.False(stage.HasGhost);
        Assert.Equal(1, stage.Child!.Opacity, 3);
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Footer_And_The_Related_Questions_Come_In_When_The_Answer_Is_Ready()
    {
        using var rig = new AiAnswerRig(level: ArcMotionLevel.Full);
        rig.AnswerFirst(finish: false);
        rig.Run(0.2);
        Assert.True(rig.Actions.IsBusy);
        Assert.False(rig.Answer.Related!.IsVisible);
        rig.Call.End();
        Assert.True(rig.RunUntil(() => rig.Answer.Status == AiAnswerStatus.Ready, 30));
        Assert.True(rig.Answer.Related.IsVisible);
        rig.Run(0.5);
        Assert.Equal(1, rig.Answer.Related.Opacity, 3);
        // Answering again: the related questions fade out, the footer keeps its place and empties.
        rig.Answer.Regenerate(AiAnswerMode.Retry);
        rig.Settle();
        Assert.True(rig.Actions.IsBusy);
        rig.Run(0.3);
        Assert.False(rig.Answer.Related.IsVisible);
        Assert.True(rig.Answer.Footer!.Bounds.Height >= 36);
    }

    [AvaloniaFact]
    public void Streaming_Text_Grows_Only_The_Answer()
    {
        using var rig = new AiAnswerRig();
        _ = rig.Answer.AskAsync(AiAnswerPageSamples.MoveBuilds);
        rig.Settle();
        rig.Call.Thought(TimeSpan.FromSeconds(2));
        var stage = rig.Answer.Stage!;
        var heading = rig.Answer.QuestionHeading!;
        var headingY = rig.TopLeftOf(heading).Y;
        var heights = new List<double>();
        foreach (var chunk in AiAnswerSampleResponder.Split(AiAnswerSamples.Answer, 24).Take(30))
        {
            rig.Call.Text(chunk);
            rig.Run(0.05);
            heights.Add(stage.Bounds.Height);
        }
        // Height follows the text at once (no spring while streaming) and only ever grows; the question stays put.
        Assert.False(stage.IsMoving);
        Assert.True(heights[^1] > heights[0]);
        Assert.Equal(headingY, rig.TopLeftOf(heading).Y, 1);
    }
}

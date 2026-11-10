using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>Onboarding survey: required answers gate Continue, limits, skipping, going back keeps answers, and the report.</summary>
public class OnboardingSurveyTests
{
    private static (OnboardingSurvey Survey, Window Window) Show()
    {
        var survey = new OnboardingSurvey();
        var window = TestHelpers.Show(survey);
        window.Width = 700;
        window.Height = 900;
        Settle();
        return (survey, window);
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static AvaloniaButton Part(OnboardingSurvey survey, string name) => TestHelpers.Descendant<AvaloniaButton>(survey, name);

    private static T OnStage<T>(OnboardingSurvey survey) where T : Control
        => TestHelpers.Descendant<ArcContentSwap>(survey, OnboardingSurvey.StagePart).GetVisualDescendants().OfType<T>().First(c => c.IsEffectivelyVisible);

    [AvaloniaFact]
    public void Walks_The_Sample_Questions_And_Reports_Every_Answer()
    {
        var (survey, window) = Show();
        try
        {
            OnboardingSurveyCompletedEventArgs? done = null;
            survey.Completed += (_, e) => done = e;
            survey.ShownQuestions.Count.ShouldBe(4);
            var next = Part(survey, OnboardingSurvey.ContinueButtonPart);
            next.IsEnabled.ShouldBeFalse();
            survey.Next().ShouldBeFalse();

            OnStage<RadioCards>(survey).SelectedIndex = 1;
            Settle();
            next.IsEnabled.ShouldBeTrue();
            survey.Next().ShouldBeTrue();
            Settle();
            survey.CurrentIndex.ShouldBe(1);
            TestHelpers.Descendant<TextBlock>(survey, OnboardingSurvey.CounterPart).Text.ShouldBe("Question 2 of 4");

            // The number keys pick an option.
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.None);
            Settle();
            survey.AnswerOf(survey.ShownQuestions[1]).Values.ShouldBe(["11-50"]);
            survey.Next();
            Settle();

            // At most three goals: the rest are disabled at the limit.
            var checks = TestHelpers.Descendant<ArcContentSwap>(survey, OnboardingSurvey.StagePart).GetVisualDescendants().OfType<Checkbox>().ToList();
            checks.Count.ShouldBe(5);
            checks[0].IsChecked = true;
            checks[2].IsChecked = true;
            checks[4].IsChecked = true;
            Settle();
            checks[1].IsEnabled.ShouldBeFalse();
            checks[3].IsEnabled.ShouldBeFalse();
            survey.AnswerOf(survey.ShownQuestions[2]).Values.ShouldBe(["Plan a project", "Write docs", "Bring in my team"]);

            // Back keeps the answers.
            survey.GoBack().ShouldBeTrue();
            Settle();
            survey.AnswerOf(survey.ShownQuestions[1]).Values.ShouldBe(["11-50"]);
            survey.Next();
            survey.Next();
            Settle();

            // The note is optional: skip it.
            survey.CurrentIndex.ShouldBe(3);
            Part(survey, OnboardingSurvey.SkipQuestionButtonPart).IsVisible.ShouldBeTrue();
            Part(survey, OnboardingSurvey.ContinueButtonPart).Content.ShouldBe("Finish");
            survey.SkipQuestion().ShouldBeTrue();
            Settle();
            survey.IsComplete.ShouldBeTrue();
            done.ShouldNotBeNull();
            done!.IsSkipped.ShouldBeFalse();
            done.Answers.Select(a => a.QuestionId).ShouldBe(["role", "size", "goals", "note"]);
            done.Answers[0].Values.ShouldBe(["Design"]);
            done.Answers[3].IsEmpty.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(survey, OnboardingSurvey.DoneTitlePart).Text.ShouldBe("Thanks, you're all set");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skip_Survey_Ends_It_And_Reset_Starts_Over()
    {
        var (survey, window) = Show();
        try
        {
            OnboardingSurveyCompletedEventArgs? done = null;
            survey.Completed += (_, e) => done = e;
            survey.SetAnswer(survey.ShownQuestions[0], ["Engineering"]);
            Part(survey, OnboardingSurvey.SkipSurveyButtonPart).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Settle();
            done!.IsSkipped.ShouldBeTrue();
            done.Answers[0].Values.ShouldBe(["Engineering"]);
            TestHelpers.Descendant<TextBlock>(survey, OnboardingSurvey.DoneTitlePart).Text.ShouldBe("No problem");

            survey.Reset();
            Settle();
            survey.IsComplete.ShouldBeFalse();
            survey.CurrentIndex.ShouldBe(0);
            survey.AnswerOf(survey.ShownQuestions[0]).IsEmpty.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Text_Question_Counts_Characters_And_Ctrl_Enter_Continues()
    {
        var survey = new OnboardingSurvey();
        survey.Questions.Add(new OnboardingSurveyQuestion { Id = "why", Title = "Why are you here?", Kind = OnboardingSurveyQuestionKind.Text, MaxLength = 50 });
        survey.Questions.Add(new OnboardingSurveyQuestion { Id = "more", Title = "More?", Kind = OnboardingSurveyQuestionKind.Text, IsOptional = true });
        var window = TestHelpers.Show(survey);
        try
        {
            Settle();
            survey.ShownQuestions.Count.ShouldBe(2);
            var box = OnStage<Textarea>(survey);
            box.Text = "To plan the launch";
            Settle();
            survey.AnswerOf(survey.ShownQuestions[0]).Text.ShouldBe("To plan the launch");
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            Settle();
            survey.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}

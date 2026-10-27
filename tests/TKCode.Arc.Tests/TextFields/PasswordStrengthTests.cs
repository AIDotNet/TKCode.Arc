using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.TextFields;

public class PasswordStrengthTests
{
    [AvaloniaTheory]
    [InlineData(280, ArcMotionLevel.Off)]
    [InlineData(480, ArcMotionLevel.Off)]
    [InlineData(280, ArcMotionLevel.Full)]
    [InlineData(480, ArcMotionLevel.Full)]
    public void Localized_Strength_Labels_Keep_Their_Gap_And_Fit_The_Field(int width, ArcMotionLevel level)
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var field = new PasswordStrength { Label = "Password", Width = width };
        var window = TestHelpers.Show(field, level);
        try
        {
            foreach (var label in new[] { "Strong", "符合输入要求", "Exigences respectées", "Good" })
            {
                field.Messages = new PasswordStrengthMessages { Strong = label };
                field.Text = "Correct-Horse-9";
                Dispatcher.UIThread.RunJobs();
                clock.Advance(TimeSpan.FromSeconds(1));
                Dispatcher.UIThread.RunJobs();
                var word = field.GetVisualDescendants().OfType<FieldSwapText>().Single();
                var meter = field.GetVisualDescendants().OfType<PasswordStrengthMeter>().Single();
                var drawing = new DrawingGroup();
                using (var context = drawing.Open())
                {
                    word.Render(context);
                }
                var bounds = drawing.GetBounds();
                var textOrigin = word.TranslatePoint(default, window)!.Value;
                var meterOrigin = meter.TranslatePoint(default, window)!.Value;
                var fieldOrigin = field.TranslatePoint(default, window)!.Value;
                (textOrigin.X + bounds.Left - meterOrigin.X - meter.Bounds.Width)
                    .ShouldBeGreaterThanOrEqualTo(11.5, label);
                (textOrigin.X + bounds.Right).ShouldBeLessThanOrEqualTo(fieldOrigin.X + width + 0.5);
                word.Bounds.Width.ShouldBeGreaterThanOrEqualTo(72);
                meter.Bounds.Width.ShouldBeGreaterThan(0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData("", 0, false)]
    [InlineData("Abc1!", 1, true)]
    [InlineData("abcdefgh", 1, false)]
    [InlineData("abcdefgh1", 1, false)]
    [InlineData("Abcdefgh1", 2, false)]
    [InlineData("Abcdefgh1!", 3, false)]
    [InlineData("Correct-Horse-9", 4, false)]
    public void Estimator_Matches_Arc(string password, int level, bool isShort)
    {
        var score = PasswordStrengthEstimator.Default.Score(password, PasswordRules.Default);
        score.Level.ShouldBe(level);
        score.IsShort.ShouldBe(isShort);
    }

    [Fact]
    public void Rules_Count_Code_Points_And_Report_Remaining()
    {
        var length = PasswordRules.Default.First(r => r.Id == "length");
        length.Remaining!("abc").ShouldBe(9);
        PasswordRules.CountCodePoints("a\U0001F600").ShouldBe(2);
        PasswordRules.Default.First(r => r.Id == "symbol").Test("abc def").ShouldBeFalse();
        PasswordRules.Default.First(r => r.Id == "symbol").Test("abc-def").ShouldBeTrue();
        PasswordRules.Create(minLength: 4).First(r => r.Id == "length").Label.ShouldBe("At least 4 characters");
    }

    [AvaloniaFact]
    public void Typing_Scores_The_Password_And_Raises_StrengthChanged()
    {
        var field = new PasswordStrength { Label = "New password", Width = 380 };
        var window = TestHelpers.Show(field);
        try
        {
            field.Level.ShouldBe(0);
            PasswordStrengthChangedEventArgs? last = null;
            field.StrengthChanged += (_, e) => last = e;
            field.Text = "Correct-Horse-9";
            Dispatcher.UIThread.RunJobs();
            field.Level.ShouldBe(4);
            field.StrengthLabel.ShouldBe("Strong");
            last.ShouldNotBeNull();
            last!.Label.ShouldBe("Strong");
            last.Score.MetRuleIds.Count.ShouldBe(4);
            field.Classes.ShouldContain(":level-4");
            var meter = field.GetVisualDescendants().OfType<PasswordStrengthMeter>().First();
            meter.Level.ShouldBe(4);
            meter.ValueText.ShouldBe("Strong");
            var marks = field.GetVisualDescendants().OfType<PasswordRuleMark>().ToList();
            marks.Count.ShouldBe(4);
            marks.ShouldAllBe(m => m.IsMet);

            field.Text = "abc";
            Dispatcher.UIThread.RunJobs();
            field.Level.ShouldBe(1);
            field.StrengthLabel.ShouldBe("Too short");
            field.GetVisualDescendants().OfType<FieldSwapText>().First().Text.ShouldBe("Too short");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Slow_Scorer_Results_Arriving_Late_Are_Ignored()
    {
        var scorer = new ControlledScorer();
        var field = new PasswordStrength { Label = "New password", Scorer = scorer, Width = 380 };
        var window = TestHelpers.Show(field);
        try
        {
            field.Text = "first";
            field.Text = "second";
            Dispatcher.UIThread.RunJobs();
            scorer.Complete("second", 3);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            field.Level.ShouldBe(3);
            scorer.Complete("first", 1);
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            field.Level.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reveal_Toggle_Keeps_One_Name_And_Error_Opens_The_Row()
    {
        var field = new PasswordStrength { Label = "New password", Width = 380 };
        var window = TestHelpers.Show(field, Motion.ArcMotionLevel.Full);
        try
        {
            var toggle = field.GetVisualDescendants().OfType<FieldIconButton>().First();
            Avalonia.Automation.AutomationProperties.GetName(toggle).ShouldBe("Show password");
            field.IsRevealed = true;
            Dispatcher.UIThread.RunJobs();
            Avalonia.Automation.AutomationProperties.GetName(toggle).ShouldBe("Show password");
            toggle.IsOn.ShouldBeTrue();

            field.ErrorMessage = "Choose a stronger password.";
            Dispatcher.UIThread.RunJobs();
            field.IsInvalid.ShouldBeTrue();
            var error = field.GetVisualDescendants().OfType<FieldMessage>().First(m => m.Name == "PART_Error");
            error.IsOpen.ShouldBeTrue();
            error.Swap.ShouldBe(FieldMessageSwap.Whole);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class ControlledScorer : IPasswordStrengthScorer
    {
        private readonly Dictionary<string, TaskCompletionSource<PasswordStrengthScore>> _pending = new();

        public ValueTask<PasswordStrengthScore> ScoreAsync(string password, IReadOnlyList<PasswordRule> rules, CancellationToken cancellationToken)
        {
            if (password.Length == 0)
            {
                return new ValueTask<PasswordStrengthScore>(new PasswordStrengthScore(0, Array.Empty<string>(), false));
            }
            var source = new TaskCompletionSource<PasswordStrengthScore>();
            _pending[password] = source;
            return new ValueTask<PasswordStrengthScore>(source.Task);
        }

        public void Complete(string password, int level)
            => _pending[password].SetResult(new PasswordStrengthScore(level, Array.Empty<string>(), false));
    }
}

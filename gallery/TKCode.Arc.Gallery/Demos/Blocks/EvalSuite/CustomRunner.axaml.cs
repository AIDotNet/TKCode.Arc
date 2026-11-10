using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class EvalSuiteCustomRunnerDemo : UserControl
{
    public EvalSuiteCustomRunnerDemo()
    {
        InitializeComponent();
        Suite.Versions =
        [
            new EvalVersion { Id = "draft-a", Name = "Draft A", Note = "Bullet points" },
            new EvalVersion { Id = "draft-b", Name = "Draft B", Note = "One paragraph" },
        ];
        Suite.Cases =
        [
            new EvalCase { Id = "version", Title = "Names the version", Input = "Summarize release 4.2 of Tidepool.", Expected = "4.2" },
            new EvalCase { Id = "fixes", Title = "Counts the fixes", Input = "Release 4.2 fixed 7 bugs.", Expected = @"\b7 (bugs|fixes)\b", Check = EvalCheckKind.Regex },
            new EvalCase { Id = "short", Title = "Short enough", Input = "Keep it under 40 words.", Expected = "Fewer than 40 words", Check = EvalCheckKind.Rubric },
            new EvalCase { Id = "tone", Title = "Plain words", Input = "Avoid marketing language.", Expected = "No hype words", Check = EvalCheckKind.Rubric, Threshold = 0.8 },
        ];
        Suite.Results = [];
        // Stand-in for a model call: waits a moment; Draft B is better at brevity and worse at tone.
        Suite.RunCaseAsync = async (evalCase, version, cancellation) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500 + evalCase.Title.Length * 20), cancellation);
            var b = version.Id == "draft-b";
            var output = evalCase.Id switch
            {
                "version" => b ? "Tidepool 4.2 makes sync faster and fixes 7 bugs." : "- Faster sync\n- 7 bugs fixed",
                "fixes" => "Tidepool 4.2 fixed 7 bugs and made sync faster.",
                "short" => b ? "Tidepool 4.2: faster sync, 7 fixes." : "- Sync is now faster on large libraries\n- 7 bugs fixed\n- New export button\n- Better errors",
                _ => b ? "An amazing, game-changing release!" : "Sync is faster and 7 bugs are fixed."
            };
            double? score = evalCase.Id switch { "short" => b ? 0.94 : 0.58, "tone" => b ? 0.42 : 0.9, _ => null };
            return EvalSuite.Grade(evalCase, version.Id, output, score);
        };
        Log.Text = "Press Run suite: no results are loaded yet.";
        Suite.RunStarted += (_, e) => Log.Text = $"Running {e.Cases.Count} cases on {e.Baseline.Name} and {e.Candidate.Name}…";
        Suite.CaseCompleted += (_, e) => Log.Text = $"{e.Case.Title}: {e.Status}";
        Suite.RunCompleted += (_, e) => Log.Text =
            $"{(e.WasStopped ? "Stopped" : "Done")} in {e.Elapsed.TotalSeconds:0.0} s · {e.Summary.CandidatePassed} of {e.Summary.Compared} pass on the candidate · {e.Summary.Regressions} regressions";
    }
}

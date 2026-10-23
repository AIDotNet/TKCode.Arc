using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelArenaProgrammaticDemo : UserControl
{
    public ModelArenaProgrammaticDemo()
    {
        InitializeComponent();
        var runner = new ModelArenaDemoData.Runner();
        Arena.Contestants.AddRange(ModelArenaDemoData.Models());
        Arena.Prompts.AddRange(ModelArenaDemoData.Prompts());
        Arena.Runner = runner;

        RunButton.Click += async (_, _) => await Arena.RunAsync();
        NextButton.Click += (_, _) => Arena.NextRound();
        BreakBox.IsCheckedChanged += (_, _) => runner.BreakNext = BreakBox.IsChecked == true;

        // VoteCast comes once per vote with both models and what each answer cost.
        Arena.VoteCast += (_, e) =>
        {
            var vote = e.Vote;
            LastVote.Text = $"Round {vote.Round}: {vote.Choice}, {vote.ModelA.Name} ${vote.MetricsA.CostUsd:0.0000} vs {vote.ModelB.Name} ${vote.MetricsB.CostUsd:0.0000}";
        };
    }
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MixtureOfExpertsRouteLogDemo : UserControl
{
    private static readonly string[] s_experts = MixtureOfExpertsSamples.CreateExperts().Select(x => x.Name).ToArray();
    private readonly Queue<string> _lines = new();

    public MixtureOfExpertsRouteLogDemo()
    {
        InitializeComponent();
    }

    private void OnRouted(object? sender, MixtureOfExpertsRoutedEventArgs e)
    {
        // Each route names its experts best first with their share of the token.
        var experts = s_experts;
        var choices = e.Experts.Select((expert, rank) => e.Dropped.Contains(expert)
            ? $"{experts[expert]} (full)"
            : $"{experts[expert]} {Math.Round(e.Weights[rank] * 100).ToString(CultureInfo.InvariantCulture)}%");
        _lines.Enqueue($"{e.Index + 1,2}. {e.Token.Trim()} → {string.Join(", ", choices)}");
        while (_lines.Count > 5)
        {
            _lines.Dequeue();
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        _lines.Clear();
        Log.Text = "Waiting for the first token…";
        Card.Restart();
    }
}

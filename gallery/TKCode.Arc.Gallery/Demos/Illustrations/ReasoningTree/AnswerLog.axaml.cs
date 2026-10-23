using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReasoningTreeAnswerLogDemo : UserControl
{
    private readonly List<string> _answers = new();

    public ReasoningTreeAnswerLogDemo()
    {
        InitializeComponent();
        Tree.PropertyChanged += OnTreePropertyChanged;
        ShowState();
    }

    private void OnTreePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // BeamWidth is two-way: the card's own control writes it too.
        if (e.Property == ReasoningTree.BeamWidthProperty || e.Property == ReasoningTree.SeedProperty)
        {
            ShowState();
        }
    }

    private void OnAnswered(object? sender, ReasoningTreeAnsweredEventArgs e)
    {
        var path = string.Join(" → ", e.Path.Select(t => t.Label));
        _answers.Insert(0, $"Beam {e.BeamWidth}, seed {Tree.Seed}: {e.Answer.Label} ({e.Score.ToString("0.00", CultureInfo.InvariantCulture)}) via {path}");
        if (_answers.Count > 4)
        {
            _answers.RemoveAt(_answers.Count - 1);
        }
        Log.Text = string.Join("\n", _answers);
    }

    private void ShowState() => State.Text = $"Beam width {Tree.BeamWidth} · seed {Tree.Seed}";

    private void OnBeamOneClick(object? sender, RoutedEventArgs e) => Tree.BeamWidth = 1;

    private void OnBeamTwoClick(object? sender, RoutedEventArgs e) => Tree.BeamWidth = 2;

    private void OnBeamThreeClick(object? sender, RoutedEventArgs e) => Tree.BeamWidth = 3;

    private void OnNextSeedClick(object? sender, RoutedEventArgs e) => Tree.Seed++;
}

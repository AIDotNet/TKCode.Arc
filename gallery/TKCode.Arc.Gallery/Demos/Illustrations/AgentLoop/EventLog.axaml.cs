using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentLoopEventLogDemo : UserControl
{
    private readonly List<string> _entries = new();

    public AgentLoopEventLogDemo()
    {
        InitializeComponent();
        Loop.PropertyChanged += (_, e) =>
        {
            if (e.Property == Explainer.IsPlayingProperty)
            {
                PlayButton.Content = Loop.IsPlaying ? "Pause" : "Play";
            }
        };
    }

    private void OnStationChanged(object? sender, AgentLoopStationChangedEventArgs e)
    {
        _entries.Add($"Loop {e.Loop + 1}: {e.Station}");
        if (_entries.Count > 4)
        {
            _entries.RemoveAt(0);
        }
        Log.Text = string.Join("  ·  ", _entries);
    }

    private void OnFinished(object? sender, RoutedEventArgs e)
    {
        Log.Text = Loop.Phase == AgentLoopPhase.Stopped
            ? "Finished: stopped, the last check did not pass."
            : "Finished: done, the last check passed.";
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (Loop.IsPlaying)
        {
            Loop.Pause();
        }
        else
        {
            Loop.Play();
        }
    }

    private void OnStepClick(object? sender, RoutedEventArgs e)
    {
        Loop.StepForward();
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        _entries.Clear();
        Log.Text = "Started over.";
        Loop.Restart();
    }

    private void OnFastChanged(object? sender, RoutedEventArgs e)
    {
        Loop.Speed = Fast.IsChecked == true ? 2 : 1;
    }
}

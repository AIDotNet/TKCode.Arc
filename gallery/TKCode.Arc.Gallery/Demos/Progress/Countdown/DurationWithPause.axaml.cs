using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CountdownDurationWithPauseDemo : UserControl
{
    public CountdownDurationWithPauseDemo()
    {
        InitializeComponent();
    }

    private void OnCompleted(object? sender, RoutedEventArgs e)
    {
        Status.Text = "Completed fired.";
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
        Timer.IsPaused = !Timer.IsPaused;
        PauseButton.Content = Timer.IsPaused ? "Resume" : "Pause";
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Timer.Restart();
        Timer.IsPaused = false;
        PauseButton.Content = "Pause";
        Status.Text = "Counting down 12 seconds.";
    }
}

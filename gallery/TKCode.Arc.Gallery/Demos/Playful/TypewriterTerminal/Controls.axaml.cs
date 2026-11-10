using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TypewriterTerminalControlsDemo : UserControl
{
    public TypewriterTerminalControlsDemo()
    {
        InitializeComponent();
    }

    private void OnPlay(object? sender, RoutedEventArgs e)
    {
        Terminal.Play();
        Status.Text = "Playing";
    }

    private void OnPause(object? sender, RoutedEventArgs e)
    {
        Terminal.Pause();
        Status.Text = $"Paused after {Terminal.CompletedLines} lines";
    }

    private void OnComplete(object? sender, RoutedEventArgs e) => Terminal.Complete();

    private void OnLineCompleted(object? sender, TypewriterTerminalLineEventArgs e) =>
        Status.Text = $"Line {e.Index + 1} of {Terminal.Lines.Count}";

    private void OnCompleted(object? sender, RoutedEventArgs e) => Status.Text = "Done";
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class SpeculativeDecodingStatusStatesDemo : UserControl
{
    private readonly DispatcherTimer _load = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public SpeculativeDecodingStatusStatesDemo()
    {
        InitializeComponent();
        _load.Tick += (_, _) =>
        {
            _load.Stop();
            Decoder.Status = SpeculativeDecodingStatus.Ready;
        };
    }

    private void OnLoadingClick(object? sender, RoutedEventArgs e)
    {
        _load.Stop();
        Decoder.Status = SpeculativeDecodingStatus.Loading;
    }

    private void OnErrorClick(object? sender, RoutedEventArgs e)
    {
        _load.Stop();
        Decoder.Status = SpeculativeDecodingStatus.Error;
    }

    private void OnReadyClick(object? sender, RoutedEventArgs e)
    {
        _load.Stop();
        Decoder.Status = SpeculativeDecodingStatus.Ready;
    }

    // Retry: show the loading state again, then the run (a real page would fetch the trace here).
    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        Decoder.Status = SpeculativeDecodingStatus.Loading;
        _load.Start();
    }
}

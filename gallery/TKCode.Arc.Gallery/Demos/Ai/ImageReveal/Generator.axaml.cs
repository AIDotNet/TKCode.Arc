using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An image job behind a <see cref="ImageRevealGenerator"/>: 30 steps of 140ms, or a failure half way.</summary>
public partial class ImageRevealGeneratorDemo : UserControl
{
    private bool _failNext;

    public ImageRevealGeneratorDemo()
    {
        InitializeComponent();
        Reveal.Generator = GenerateAsync;
    }

    private async Task<IImage?> GenerateAsync(IProgress<ImageRevealProgress> progress, CancellationToken cancellationToken)
    {
        var fails = _failNext;
        _failNext = false;
        var picture = ImageRevealDemoPicture.Get();
        // A real service queues first; the canvas shows the queued look until the first report.
        await Task.Delay(700, cancellationToken);
        for (var step = 1; step <= 30; step++)
        {
            await Task.Delay(140, cancellationToken);
            if (fails && step == 14)
            {
                throw new InvalidOperationException("The sample service failed.");
            }
            progress.Report(new ImageRevealProgress(step / 30.0, picture));
        }
        return picture;
    }

    private void OnGenerate(object? sender, RoutedEventArgs e) => Reveal.Restart();

    private void OnFailNext(object? sender, RoutedEventArgs e) => _failNext = true;
}

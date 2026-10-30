using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Wires a generator: the card's simulation stands in for an image API, and one tile fails once.</summary>
public partial class ImageGenerationYourGeneratorDemo : UserControl
{
    private bool _failedOnce;

    public ImageGenerationYourGeneratorDemo()
    {
        InitializeComponent();
        Card.Generator = async (request, context) =>
        {
            // Call your image API here: send request.Prompt, request.Model.Id and request.Aspect, report progress with
            // context.ReportProgress and pass context.CancellationToken on so Stop cancels the call.
            if (request.Index == 2 && !_failedOnce)
            {
                _failedOnce = true;
                await Task.Delay(1200, context.CancellationToken);
                throw new InvalidOperationException("The sample service timed out.");
            }
            return await Card.Simulation!.Generate(request, context);
        };
    }
}

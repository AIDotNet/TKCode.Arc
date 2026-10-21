using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SignaturePadUseTheSignatureDemo : UserControl
{
    public SignaturePadUseTheSignatureDemo()
    {
        InitializeComponent();
    }

    private void OnStrokesChanged(object? sender, SignatureStrokesChangedEventArgs e)
    {
        var previous = Preview.Source as IDisposable;
        if (e.Strokes.Count == 0)
        {
            Preview.Source = null;
            Status.Text = "Draw to see the exported image.";
        }
        else
        {
            Preview.Source = Pad.ExportBitmap(2);
            Status.Text = $"{e.Strokes.Count} {(e.Strokes.Count == 1 ? "stroke" : "strokes")} · SVG {Pad.ExportSvg().Length / 1024.0:0.0} KB";
        }
        previous?.Dispose();
    }
}

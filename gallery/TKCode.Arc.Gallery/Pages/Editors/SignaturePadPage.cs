using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Pages;

public static class SignaturePadPage
{
    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Sign",
            "Width follows pen pressure, or speed for a mouse or finger, and the ends taper. ⌘Z undoes, ⇧⌘Z redoes, Delete clears with a wipe; replay draws it again in its own rhythm.",
            Frame(new SignaturePad { Signer = "Ada Lovelace" })),
        GalleryUi.Card("Ink and nib",
            "Each stroke keeps the ink and nib it was drawn with; the arrow keys move between options.",
            Frame(new SignaturePad { InkColor = SignatureInkColor.Blue, InkWidth = SignatureInkWidth.Bold, Hint = "Sign in blue" })),
        GalleryUi.Card("Use the signature",
            "StrokesChanged reports every change; ExportBitmap, ExportPng and ExportSvg give the ink cropped and in print colors.",
            Preview()));

    private static Border Frame(Control content) => new() { MaxWidth = 560, Child = content };

    private static Control Preview()
    {
        var pad = new SignaturePad { Signer = "Signed in the gallery", FileName = "gallery-signature" };
        var image = new Image { Height = 64, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        var status = GalleryUi.Text("Draw to see the exported image.", ArcDesignTokenKind.TextMuted, 13);
        pad.StrokesChanged += (_, e) =>
        {
            var previous = image.Source as IDisposable;
            if (e.Strokes.Count == 0)
            {
                image.Source = null;
                status.Text = "Draw to see the exported image.";
            }
            else
            {
                image.Source = pad.ExportBitmap(2);
                status.Text = $"{e.Strokes.Count} {(e.Strokes.Count == 1 ? "stroke" : "strokes")} · SVG {pad.ExportSvg().Length / 1024.0:0.0} KB";
            }
            previous?.Dispose();
        };
        return Frame(GalleryUi.Column(16, pad, status, image));
    }
}

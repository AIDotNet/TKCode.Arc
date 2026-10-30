using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The changing label of a button (Generate and Stop, Upscale, Upscaling and Upscaled, Download and Saved): every face is
/// stacked in one cell, so the cell is as wide as the widest and the button never changes width, and the shown face
/// crossfades (the old one out over 100ms, the new one in over 160ms). The faces are built here inside a stock Arc
/// button, whose theme cannot reach them, so their opacity is driven in code with one motion value each.
/// </summary>
internal sealed class ImageGenerationFaces : Grid
{
    private static readonly TimeSpan s_out = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_in = TimeSpan.FromMilliseconds(160);

    private readonly List<(StackPanel Face, ArcGlyph? Glyph, TextBlock Text, MotionValue<double> Opacity)> _faces = [];
    private int _shown = -1;

    /// <summary>Adds a face: an optional 16px glyph and a word.</summary>
    public ImageGenerationFaces Add(Geometry? glyph, string text)
    {
        var face = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        ArcGlyph? icon = null;
        if (glyph is not null)
        {
            icon = new ArcGlyph { Data = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Center };
            face.Children.Add(icon);
        }
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        face.Children.Add(label);
        var opacity = new MotionValue<double>(face, 0, MotionVectors.Double);
        opacity.Changed += value => face.Opacity = value;
        _faces.Add((face, icon, label, opacity));
        Children.Add(face);
        if (_shown < 0)
        {
            Show(0);
        }
        return this;
    }

    /// <summary>The face on show.</summary>
    public int Shown => _shown;

    /// <summary>The word of the face on show.</summary>
    public string ShownText => _shown >= 0 ? _faces[_shown].Text.Text ?? string.Empty : string.Empty;

    /// <summary>Rewords a face (the labels changed).</summary>
    public void SetText(int index, string text)
    {
        if (index >= 0 && index < _faces.Count)
        {
            _faces[index].Text.Text = text;
        }
    }

    /// <summary>Crossfades to a face; the first time, or with motion off, it switches at once.</summary>
    public void Show(int index)
    {
        if (index == _shown || index < 0 || index >= _faces.Count)
        {
            return;
        }
        var instant = _shown < 0 || TopLevel.GetTopLevel(this) is null || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off;
        for (var i = 0; i < _faces.Count; i++)
        {
            var (_, _, _, opacity) = _faces[i];
            var target = i == index ? 1.0 : 0.0;
            if (instant)
            {
                opacity.Jump(target);
                _faces[i].Face.Opacity = target;
            }
            else if (opacity.Target != target)
            {
                _ = opacity.AnimateTo(target, MotionSpec.Tween(target > 0 ? s_in : s_out, target > 0 ? ArcEasings.Enter : ArcEasings.Standard));
            }
        }
        _shown = index;
    }
}
